//-----------------------------------------------------------------------
// <copyright file="SharedDisposable.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JacksonDunstan.NativeCollections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// A reference-counted <see cref="IDisposable"/>.
	/// </summary>
	/// 
	/// <typeparam name="TDisposable">
	/// Type of disposable that is shared.
	/// </typeparam>
	[NativeContainer]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	[DebuggerTypeProxy(typeof(SharedDisposableDebugView<>))]
	[DebuggerDisplay("Disposable = {Value}")]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct SharedDisposable<TDisposable> : IDisposable
		where TDisposable : IDisposable
	{
		/// <summary>
		/// Pointer to the ref count in native memory. Must be named exactly
		/// this way to allow for
		/// [NativeContainerSupportsDeallocateOnJobCompletion]
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		internal int* m_Buffer;

		/// <summary>
		/// Allocator used to create the backing memory
		/// 
		/// This field must be named this way to comply with
		/// [NativeContainerSupportsDeallocateOnJobCompletion]
		/// </summary>
		internal readonly Allocator m_AllocatorLabel;

		/// <summary>
		/// Disposable that is being shared
		/// </summary>
		private readonly TDisposable m_Disposable;

		// These fields are all required when safety checks are enabled
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		/// <summary>
		/// A handle to information about what operations can be safely
		/// performed on the object at any given time.
		/// </summary>
		private AtomicSafetyHandle m_Safety;

		/// <summary>
		/// A handle that can be used to tell if the object has been disposed
		/// yet or not, which allows for error-checking double disposal.
		/// </summary>
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif

		/// <summary>
		/// Allocate memory and save the disposable
		/// </summary>
		/// 
		/// <param name="disposable">
		/// Disposable that is being shared
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate and deallocate with. Must be valid.
		/// </param>
		public SharedDisposable(
#if CSHARP_7_3_OR_NEWER
			in
#endif
			TDisposable disposable,
			Allocator allocator)
		{
			// Require a valid allocator
			if (!UnsafeUtility.IsValidAllocator(allocator))
			{
				throw new ArgumentException(
					"Invalid allocator",
					"allocator");
			}

			// Allocate the memory for the ref count and initialize to 1
			m_Buffer = (int*)UnsafeUtility.Malloc(
				sizeof(int),
				UnsafeUtility.AlignOf<int>(),
				allocator);
			*m_Buffer = 1;

			// Store the allocator to use when deallocating
			m_AllocatorLabel = allocator;

			// Create the dispose sentinel
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif

			// Save the disposable
			m_Disposable = disposable;
		}

		/// <summary>
		/// Get or set the contained disposable
		/// 
		/// This operation requires read access.
		/// </summary>
		/// 
		/// <value>
		/// The contained disposable
		/// </value>
		public TDisposable Value
		{
			get
			{
				RequireReadAccess();
				return m_Disposable;
			}
		}

		/// <summary>
		/// Check if the underlying unmanaged memory has been created and not
		/// freed via a call to <see cref="Dispose"/>.
		/// 
		/// This operation has no access requirements.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// Initially true when a non-default constructor is called but
		/// initially false when the default constructor is used. After
		/// <see cref="Dispose"/> is called, this becomes false. Note that
		/// calling <see cref="Dispose"/> on one copy of this object doesn't
		/// result in this becoming false for all copies if it was true before.
		/// This property should <i>not</i> be used to check whether the object
		/// is usable, only to check whether it was <i>ever</i> usable.
		/// </value>
		public bool IsCreated
		{
			get
			{
				return m_Buffer != null;
			}
		}

		/// <summary>
		/// Increment the reference count.
		/// 
		/// This operation requires write access.
		/// </summary>
		/// 
		/// <returns>
		/// A reference to this object.
		/// </returns>
		[WriteAccessRequired]
		public SharedDisposable<TDisposable> Ref()
		{
			*m_Buffer = *m_Buffer + 1;
			return this;
		}

		/// <summary>
		/// Release the object's unmanaged memory. Do not use it after this. Do
		/// not call <see cref="Dispose"/> on copies of the object either.
		/// 
		/// This operation requires write access.
		/// 
		/// This complexity of this operation is O(1) plus the allocator's
		/// deallocation complexity.
		/// </summary>
		[WriteAccessRequired]
		public void Dispose()
		{
			RequireWriteAccess();

			int newRefCount = *m_Buffer - 1;
			*m_Buffer = newRefCount;
			if (newRefCount == 0)
			{
				// Make sure we're not double-disposing
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
				DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
				DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

				UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
				m_Buffer = null;
				
				m_Disposable.Dispose();
			}
		}

		/// <summary>
		/// Set whether both read and write access should be allowed. This is
		/// used for automated testing purposes only.
		/// </summary>
		/// 
		/// <param name="allowReadOrWriteAccess">
		/// If both read and write access should be allowed
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		public void TestUseOnlySetAllowReadAndWriteAccess(
			bool allowReadOrWriteAccess)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.SetAllowReadOrWriteAccess(
				m_Safety,
				allowReadOrWriteAccess);
#endif
		}

		/// <summary>
		/// Throw an exception if the object isn't readable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the object isn't writable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}
	}

	/// <summary>
	/// Provides a debugger view of <see cref="SharedDisposable{T}"/>.
	/// </summary>
	/// 
	/// <typeparam name="TDisposable">
	/// Type of disposable that is shared.
	/// </typeparam>
	internal sealed class SharedDisposableDebugView<TDisposable>
		where TDisposable : IDisposable
	{
		/// <summary>
		/// The object to provide a debugger view for
		/// </summary>
		private SharedDisposable<TDisposable> m_Ptr;

		/// <summary>
		/// Create the debugger view
		/// </summary>
		/// 
		/// <param name="ptr">
		/// The object to provide a debugger view for
		/// </param>
		public SharedDisposableDebugView(SharedDisposable<TDisposable> ptr)
		{
			m_Ptr = ptr;
		}

		/// <summary>
		/// Get the viewed object's disposable
		/// </summary>
		/// 
		/// <value>
		/// The viewed object's disposable
		/// </value>
		public TDisposable Disposable
		{
			get
			{
				return m_Ptr.Value;
			}
		}
	}
}

/// <summary>
/// Extensions to <see cref="IDisposable"/> to support
/// <see cref="SharedDisposable{TDisposable}"/>.
/// </summary>
public static class IDisposableExtensions
{
	/// <summary>
	/// Allocate memory and save the disposable
	/// </summary>
	/// 
	/// <param name="disposable">
	/// Disposable that is being shared
	/// </param>
	/// 
	/// <param name="allocator">
	/// Allocator to allocate and deallocate with. Must be valid.
	/// </param>
	public static SharedDisposable<TDisposable> Share<TDisposable>(
		this TDisposable disposable,
		Allocator allocator)
		where TDisposable : IDisposable
	{
		return new SharedDisposable<TDisposable>(disposable, allocator);
	}
}
