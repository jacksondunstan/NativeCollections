//-----------------------------------------------------------------------
// <copyright file="NativePerJobThreadIntPtrs.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// A pointer to an int array stored in native (i.e. unmanaged) memory. One
	/// integer array is stored for each of the maximum number of job threads.
	/// As of Unity 2018.2, this results in 8 KB of memory usage for every 16
	/// integers. The advantage over <see cref="NativeIntPtr"/> is that all
	/// operations on <see cref="Parallel"/> are faster due to not being
	/// atomic. The resulting array ints are collected with a loop. This is
	/// therefore a good option when most usage is via <see cref="Parallel"/>
	/// and memory usage is not a concern.
	/// </summary>
	[NativeContainer]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	[DebuggerTypeProxy(typeof(NativePerJobThreadIntPtrsDebugView))]
	[DebuggerDisplay("Value = NativeArray")]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativePerJobThreadIntPtrs : IDisposable
	{
		/// <summary>
		/// An atomic write-only version of the object suitable for use in a
		/// ParallelFor job
		/// </summary>
		[NativeContainer]
		[NativeContainerIsAtomicWriteOnly]
		public struct Parallel
		{

			/// <summary>
			/// The number of integers stored in the array.
			/// </summary>
			public int Length;

			/// <summary>
			/// Pointers to the integer values in native memory
			/// </summary>
			[NativeDisableUnsafePtrRestriction]
			internal int* m_Buffer;

			/// <summary>
			/// Thread index of the job using this object. This is set by Unity
			/// and must have this exact name and type.
			/// </summary>
			[NativeSetThreadIndex]
			internal int m_ThreadIndex;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			/// <summary>
			/// A handle to information about what operations can be safely
			/// performed on the object at any given time.
			/// </summary>
			internal AtomicSafetyHandle m_Safety;

			/// <summary>
			/// Create a parallel version of the object
			/// </summary>
			/// 
			/// <param name="length">
			/// The number of integers stored in the array
			/// </param>
			/// 
			/// <param name="value">
			/// Pointer to the value
			/// </param>
			/// 
			/// <param name="safety">
			/// Atomic safety handle for the object
			/// </param>
			internal Parallel(int length, int* value, AtomicSafetyHandle safety)
			{
				Length = length;
				m_Buffer = value;
				m_ThreadIndex = 0;
				m_Safety = safety;
			}
#else
			/// <summary>
			/// Create a parallel version of the object
			/// </summary>
			/// 
			/// <param name="length">
			/// The number of integers stored in the array
			/// </param>
			/// 
			/// <param name="value">
			/// Pointer to the value
			/// </param>
			internal Parallel(int length, int* value)
			{
				Length = length;
				m_Buffer = value;
				m_ThreadIndex = 0;
			}
#endif

			/// <summary>
			/// Increment the stored value
			/// </summary>
			[WriteAccessRequired]
			public void Increment(int index)
			{
				RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if (index < 0 || index >= Length) throw new ArgumentException("Index must be between 0 and Length - 1.", nameof(index));
#endif
				m_Buffer[IntsPerCacheLine * m_ThreadIndex + index]++;
			}

			/// <summary>
			/// Decrement the stored value
			/// </summary>
			[WriteAccessRequired]
			public void Decrement(int index)
			{
				RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if (index < 0 || index >= Length) throw new ArgumentException("Index must be between 0 and Length - 1.", nameof(index));
#endif
				m_Buffer[IntsPerCacheLine * m_ThreadIndex + index]--;
			}

			/// <summary>
			/// Add to the stored value
			/// </summary>
			/// 
			/// <param name="value">
			/// Value to add. Use negative values for subtraction.
			/// </param>
			[WriteAccessRequired]
			public void Add(int index, int value)
			{
				RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if (index < 0 || index >= Length) throw new ArgumentException("Index must be between 0 and Length - 1.", nameof(index));
#endif
				m_Buffer[IntsPerCacheLine * m_ThreadIndex + index] += value;
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
		/// The number of integers stored in the array.
		/// </summary>
		public readonly int Length;

		/// <summary>
		/// Pointers to the integer values in native memory. Must be named
		/// exactly this way to allow for
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
		internal Allocator m_AllocatorLabel;

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
		/// The number of integers that fit into a CPU cache line
		/// </summary>
		private const int IntsPerCacheLine = JobsUtility.CacheLineSize / sizeof(int);

		/// <summary>
		/// The number of integers that fit into a CPU cache line
		/// </summary>
		private readonly int NumCacheLines;

		/// <summary>
		/// Allocate memory and set the initial value
		/// </summary>
		/// 
		/// <param name="length">
		/// The number of logical integers to allocate. Initial value is 0 for
		/// all array elements.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate and deallocate with. Must be valid.
		/// </param>
		public NativePerJobThreadIntPtrs(int length, Allocator allocator)
		{
			// Require a valid allocator
			if (UnsafeUtility.IsValidAllocator(allocator))
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			// Need a multiple of the number of cache lines
			Length = length;
			NumCacheLines = (int)Math.Ceiling((double)length / IntsPerCacheLine);

			// Allocate the memory for the values
			int bufferSize = JobsUtility.CacheLineSize * NumCacheLines * JobsUtility.MaxJobThreadCount;
			m_Buffer = (int*)UnsafeUtility.Malloc(
				bufferSize,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			UnsafeUtility.MemClear(m_Buffer, bufferSize);

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
		}

		/// <summary>
		/// Get or set the contained value at the provided index
		/// 
		/// This operation requires read access to the node for 'get' and write
		/// access to the node for 'set'.
		/// </summary>
		/// 
		/// <value>
		/// The contained value at the provided index.
		/// </value>
		public int this[int index]
		{
			get
			{
				RequireReadAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if (index < 0 || index >= Length) throw new ArgumentException("Index must be between 0 and Length - 1.", nameof(index));
#endif
				int value = 0;
				for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
				{
					value += m_Buffer[IntsPerCacheLine * i + index];
				}
				return value;
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				if (index < 0 || index >= Length) throw new ArgumentException("Index must be between 0 and Length - 1.", nameof(index));
#endif
				*(m_Buffer + index) = value;
				for (int i = 1; i < JobsUtility.MaxJobThreadCount; ++i)
				{
					m_Buffer[IntsPerCacheLine * i + index] = 0;
				}
			}
		}

		/// <summary>
		/// Get a version of this object suitable for use in a ParallelFor job
		/// </summary>
		/// 
		/// <returns>
		/// A version of this object suitable for use in a ParallelFor job
		/// </returns>
		public Parallel GetParallel()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			Parallel parallel = new Parallel(Length, m_Buffer, m_Safety);
			AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
			Parallel parallel = new Parallel(Length, m_Buffer);
#endif
			return parallel;
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
		[BurstDiscard]
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
	/// Provides a debugger view of <see cref="NativePerJobThreadIntPtrs"/>.
	/// </summary>
	internal sealed class NativePerJobThreadIntPtrsDebugView
	{
		/// <summary>
		/// The object to provide a debugger view for
		/// </summary>
		private NativePerJobThreadIntPtrs m_Ptrs;

		/// <summary>
		/// Create the debugger view
		/// </summary>
		/// 
		/// <param name="ptrs">
		/// The object to provide a debugger view for
		/// </param>
		public NativePerJobThreadIntPtrsDebugView(NativePerJobThreadIntPtrs ptrs)
		{
			m_Ptrs = ptrs;
		}

		/// <summary>
        /// Get the elements of the array as a managed array
        /// </summary>
        public int[] Items
        {
            get
            {
            	int[] arr = new int[m_Ptrs.Length];
            	for (int i = 0; i < arr.Length; i++) arr[i] = m_Ptrs[i];
                return arr;
            }
        }
	}
}
