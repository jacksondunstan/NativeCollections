//-----------------------------------------------------------------------
// <copyright file="NativeLinkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// The state of a <see cref="NativeLinkedList{T}"/>. Shared among instances
	/// of the struct via a pointer to unmanaged memory. This has no type
	/// parameters, so it can be used by all list types.
	/// </summary>
	internal unsafe struct NativeLinkedListState
	{
		/// <summary>
		/// Each node's value. Indices correspond with nextIndexes.
		/// </summary>
		internal void* m_Values;

		/// <summary>
		/// Each node's next node index. Indices correspond with values.
		/// </summary>
		internal int* m_NextIndexes;

		/// <summary>
		/// Each node's previous node index. Indices correspond with values.
		/// </summary>
		internal int* m_PrevIndexes;

		/// <summary>
		/// Index of the first node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int m_HeadIndex;

		/// <summary>
		/// Index of the last node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int m_TailIndex;

		/// <summary>
		/// Number of nodes contained
		/// </summary>
		internal int m_Length;

		/// <summary>
		/// Number of nodes that can be contained
		/// </summary>
		internal int m_Capacity;

		/// <summary>
		/// Version of enumerators that are valid for this list. This starts at
		/// 1 and increases by one with each change that invalidates the list's
		/// enumerators.
		/// </summary>
		internal int m_Version;

		/// <summary>
		/// Allocator used to create the backing arrays
		/// </summary>
		internal Allocator m_Allocator;
	}

	/// <summary>
	/// A doubly-linked list native collection.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of nodes in the list. Must be blittable.
	/// </typeparam>
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("Length = {Length}. Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(NativeLinkedListDebugView<>))]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativeLinkedList<T>
		: IEnumerable<T>
		, IEnumerable
		, IDisposable
		where T : struct
	{
		/// <summary>
		/// An enumerator for <see cref="NativeLinkedList{T}"/>
		/// </summary>
		public unsafe struct Enumerator
			: IEnumerator<T>
			, IEnumerator
			, IDisposable
		{
			/// <summary>
			/// Index of the node
			/// </summary>
			internal int m_Index;

			/// <summary>
			/// Version of the list that this enumerator is valid for
			/// </summary>
			private readonly int m_Version;

			/// <summary>
			/// List to iterate
			/// </summary>
			private readonly NativeLinkedList<T> m_List;

			/// <summary>
			/// Create the enumerator for a particular node
			/// </summary>
			/// 
			/// <param name="list">
			/// List to iterate
			/// </param>
			/// 
			/// <param name="index">
			/// Index of the node. Out-of-bounds values are OK.
			/// </param>
			/// 
			/// <param name="version">
			/// Version of the list that this enumerator is valid for
			/// </param>
			internal Enumerator(
				NativeLinkedList<T> list,
				int index,
				int version)
			{
				m_Index = index;
				m_Version = version;
				m_List = list;
			}

			/// <summary>
			/// Make an enumerator that is invalid for all lists.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator that is invalid for all lists
			/// </returns>
			public static Enumerator MakeInvalid()
			{
				return new Enumerator(
					default(NativeLinkedList<T>),
					-1,
					-1);
			}

			/// <summary>
			/// Get an enumerator to the next node.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid, an enumerator to the next node or
			/// an invalid enumerator if at the tail node. If this enumerator is
			/// invalid but was constructed via the non-default constructor and
			/// the list it enumerates has not been disposed and has not
			/// invalidated this enumerator, an enumerator to the head node.
			/// Otherwise, an invalid enumerator.
			/// </returns>
			public Enumerator Next
			{
				get
				{
					// Have a valid list and the version matches
					if (m_List.m_State != null
					    && m_Version == m_List.m_State->m_Version)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Return the next node
							m_List.RequireReadAccess();
							m_List.RequireIndexInBounds(m_Index);
							return new Enumerator(
								m_List,
								m_List.m_State->m_NextIndexes[m_Index],
								m_Version);
						}

						// Not within the list. Return the head.
						return new Enumerator(
							m_List,
							m_List.m_State->m_HeadIndex,
							m_Version);
					}

					// No valid list
					return MakeInvalid();
				}
			}

			/// <summary>
			/// Get an enumerator to the previous node
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid, an enumerator to the previous node
			/// or an invalid enumerator if at the head node. If this enumerator
			/// is invalid but was constructed via the non-default constructor
			/// and the list it enumerates has not been disposed and has not
			/// invalidated this enumerator, an enumerator to the tail node.
			/// Otherwise, an invalid enumerator.
			/// </returns>
			public Enumerator Prev
			{
				get
				{
					// Have a valid list and the version matches
					if (m_List.m_State != null
					    && m_Version == m_List.m_State->m_Version)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Return the previous node
							m_List.RequireReadAccess();
							m_List.RequireIndexInBounds(m_Index);
							return new Enumerator(
								m_List,
								m_List.m_State->m_PrevIndexes[m_Index],
								m_Version);
						}

						// Not within the list. Return the tail.
						return new Enumerator(
							m_List,
							m_List.m_State->m_TailIndex,
							m_Version);
					}

					// No valid list
					return MakeInvalid();
				}
			}

			/// <summary>
			/// If this enumerator is valid, move to the next node or
			/// invalidate this enumerator if at the tail node. If this
			/// enumerator is invalid but was constructed via the non-default
			/// constructor and the list it enumerates has not been disposed and
			/// has not invalidated this enumerator, move to the head node.
			/// Otherwise, this function has no effect.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MoveNext()
			{
				// Have a valid list and the version matches
				if (m_List.m_State != null
				    && m_Version == m_List.m_State->m_Version)
				{
					// Still within the list. The enumerator is valid.
					if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
					{
						// Go to the next node
						m_List.RequireReadAccess();
						m_List.RequireIndexInBounds(m_Index);
						m_Index = m_List.m_State->m_NextIndexes[m_Index];
						return m_Index >= 0;
					}

					// Not within the list. Go to the head.
					m_Index = m_List.m_State->m_HeadIndex;
					return true;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// If this enumerator is valid, move to the previous node or
			/// invalidate this enumerator if at the head node. If this
			/// enumerator is invalid but was constructed via the non-default
			/// constructor and the list it enumerates has not been disposed and
			/// has not invalidated this enumerator, move to the tail node.
			/// Otherwise, this function has no effect.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MovePrev()
			{
				// Have a valid list and the version matches
				if (m_List.m_State != null
				    && m_Version == m_List.m_State->m_Version)
				{
					// Still within the list. The enumerator is valid.
					if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
					{
						// Go to the previous node
						m_List.RequireReadAccess();
						m_List.RequireIndexInBounds(m_Index);
						m_Index = m_List.m_State->m_PrevIndexes[m_Index];
						return m_Index >= 0;
					}

					// Not within the list. Go to the tail.
					m_Index = m_List.m_State->m_TailIndex;
					return true;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// Check if an enumerator is valid
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If the given enumerator is valid
			/// </returns>
			public bool IsValid
			{
				get
				{
					return m_Index >= 0
						&& m_List.m_State != null
						&& m_Index < m_List.m_State->m_Length
						&& m_Version == m_List.m_State->m_Version;
				}
			}

			/// <summary>
			/// Check if two enumerators refer to the same node lists.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="a">
			/// First enumerator to compare
			/// </param>
			/// 
			/// <param name="b">
			/// Second enumerator to compare
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerators refer to the same node and neither
			/// enumerator is invalid.
			/// </returns>
			public static bool operator ==(Enumerator a, Enumerator b)
			{
				return a.IsValid
					&& b.IsValid
					&& a.m_Index == b.m_Index
					&& a.m_List.m_State == b.m_List.m_State;
			}

			/// <summary>
			/// Check if two enumerators refer to different nodes.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="a">
			/// First enumerator to compare
			/// </param>
			/// 
			/// <param name="b">
			/// Second enumerator to compare
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerators refer to different nodes or either
			/// enumerator is invalid.
			/// </returns>
			public static bool operator !=(Enumerator a, Enumerator b)
			{
				return !a.IsValid
					|| !b.IsValid
					|| a.m_Index != b.m_Index
					|| a.m_List.m_State != b.m_List.m_State;
			}

			/// <summary>
			/// Check if this enumerator refer to the same node as another
			/// enumerator.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="obj">
			/// Enumerator to compare with
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerator refers to the same node as this
			/// enumerator and is of the same type and neither enumerator is
			/// invalid.
			/// </returns>
			public override bool Equals(object obj)
			{
				return obj is Enumerator && this == (Enumerator)obj;
			}

			/// <summary>
			/// Get a hash code for this enumerator. If the enumerator is
			/// mutated such as by calling <see cref="MoveNext"/>, the returned
			/// hash code will no longer match values returned by subsequent
			/// calls to this function.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// A hash code for this enumerator
			/// </returns>
			public override int GetHashCode()
			{
				// Suppress "non-readonly field" warning because we don't have a
				// readonly-only way to generate a hash code since Index is
				// mutable to comply with the IEnumerator interface.
#pragma warning disable RECS0025
				return m_Index;
#pragma warning restore RECS0025
			}

			/// <summary>
			/// Dispose the enumerator. This operation has no effect and exists
			/// only to satisfy the requirements of <see cref="IDisposable"/>.
			/// 
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}

			/// <summary>
			/// Reset the enumerator to the head of the list or invalidate it if
			/// the list is empty. This function has no effect if this
			/// enumerator is already invalid.
			/// 
			/// This operation is O(1).
			/// </summary>
			public void Reset()
			{
				if (IsValid)
				{
					m_List.RequireValidState();
					m_List.RequireReadAccess();
					m_Index = m_List.m_State->m_HeadIndex;
				}
			}

			/// <summary>
			/// Get or set a node's value
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value
			/// </value>
			public T Current
			{
				get
				{
					m_List.RequireValidState();
					m_List.RequireReadAccess();
					m_List.RequireIndexInBounds(m_Index);
					return UnsafeUtility.ReadArrayElement<T>(
						m_List.m_State->m_Values,
						m_Index);
				}

				[WriteAccessRequired]
				set
				{
					m_List.RequireValidState();
					m_List.RequireWriteAccess();
					m_List.RequireIndexInBounds(m_Index);
					UnsafeUtility.WriteArrayElement(
						m_List.m_State->m_Values,
						m_Index,
						value);
				}
			}

			/// <summary>
			/// Get a node's value. Prefer using the generic version of
			/// <see cref="Current"/> as this will cause boxing when enumerating
			/// value type node value. This is provided only for compatibility
			/// with <see cref="IEnumerator"/>. As such, there is no 'set' for
			/// this non-generic property.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value or null if this enumerator is invalid.
			/// </value>
			object IEnumerator.Current
			{
				get
				{
					m_List.RequireValidState();
					m_List.RequireReadAccess();
					m_List.RequireIndexInBounds(m_Index);
					return UnsafeUtility.ReadArrayElement<T>(
						m_List.m_State->m_Values,
						m_Index);
				}
			}
		}

		/// <summary>
		/// State of the list or null after being disposed. This is shared among
		/// all instances of the list.
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		private NativeLinkedListState* m_State;

		// These fields are all required when safety checks are enabled
		// They must have these exact types, names, and order
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		/// <summary>
		/// Length of the array. Equal to the number of nodes currently stored.
		/// This is set by ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// </summary>
		private int m_Length;

		/// <summary>
		/// The minimum index that can safely be accessed. This is zero outside
		/// of a job and in a regular, non-ParallelFor job but set higher by
		/// ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// 
		/// This field must immediately follow <see cref="m_Length"/>.
		/// </summary>
		private int m_MinIndex;

		/// <summary>
		/// The maximum index that can safely be accessed. This is equal to
		/// (m_Length-1) outside of a job and in a regular, non-ParallelFor job
		/// but set lower by ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// 
		/// This field must immediately follow <see cref="m_MaxIndex"/>.
		/// </summary>
		private int m_MaxIndex;

		/// <summary>
		/// A handle to information about what operations can be safely
		/// performed on the list at any given time.
		/// </summary>
		private AtomicSafetyHandle m_Safety;

		/// <summary>
		/// A handle that can be used to tell if the list has been disposed yet
		/// or not, which allows for error-checking double disposal.
		/// </summary>
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif

		/// <summary>
		/// Create the list with an initial capacity. It initially has no nodes.
		///
		/// This complexity of this operation is O(1) plus the allocator's
		/// allocation complexity.
		/// </summary>
		/// 
		/// <param name="capacity">
		/// Initial capacity. If less than four, four is used.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with
		/// </param>
		public NativeLinkedList(int capacity, Allocator allocator)
		{
			// Insist on a minimum capacity
			if (capacity < 4)
			{
				capacity = 4;
			}

			// Allocate the state. It is freed in Dispose().
			m_State = (NativeLinkedListState*)UnsafeUtility.Malloc(
				sizeof(NativeLinkedListState),
				UnsafeUtility.AlignOf<NativeLinkedListState>(),
				allocator);

			// Create the backing arrays. There's no need to clear them since we
			// make no assumptions about the contents anyways.
			m_State->m_Values = UnsafeUtility.Malloc(
				UnsafeUtility.SizeOf<T>() * capacity,
				UnsafeUtility.AlignOf<T>(),
				allocator
			);
			m_State->m_NextIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			m_State->m_PrevIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);

			// Store the allocator for future allocation and deallocation
			// operations
			m_State->m_Allocator = allocator;

			// Initially empty with the given capacity
			m_State->m_Length = 0;
			m_State->m_Capacity = capacity;

			// The list is empty so there is no head or tail
			m_State->m_HeadIndex = -1;
			m_State->m_TailIndex = -1;

			// Version starts at one so that the default (0) is never used
			m_State->m_Version = 1;

			// Initialize safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = 0;
			m_MinIndex = 0;
			m_MaxIndex = -1;
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
		}

		/// <summary>
		/// Create the list with an initial capacity. It initially has no nodes.
		///
		/// This complexity of this operation is O(N) plus the allocator's
		/// allocation complexity.
		/// </summary>
		/// 
		/// <param name="capacity">
		/// Initial capacity. This is capped at a minimum of four and the given
		/// count.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to add. This is capped at a minimum of zero.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with
		/// </param>
		public NativeLinkedList(int capacity, int length, Allocator allocator)
		{
			// Insist on a non-negative length
			if (length < 0)
			{
				length = 0;
			}

			// Insist on a minimum capacity
			int requiredCapacity = Math.Max(4, length);
			if (capacity < requiredCapacity)
			{
				capacity = requiredCapacity;
			}

			// Allocate the state. It is freed in Dispose().
			m_State = (NativeLinkedListState*)UnsafeUtility.Malloc(
				sizeof(NativeLinkedListState),
				UnsafeUtility.AlignOf<NativeLinkedListState>(),
				allocator);
			
			// Create the backing arrays.
			int valuesSize = UnsafeUtility.SizeOf<T>() * capacity;
			m_State->m_Values = UnsafeUtility.Malloc(
				valuesSize,
				UnsafeUtility.AlignOf<T>(),
				allocator
			);
			m_State->m_NextIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			m_State->m_PrevIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);

			// If there are any nodes to initialize
			int endIndex = length - 1;
			if (length > 0)
			{
				// Clear the node values to their defaults
				UnsafeUtility.MemClear(m_State->m_Values, valuesSize);

				// Initialize next pointers to the next index and the last next
				// pointer to an invalid index
				for (int i = 0; i < endIndex; ++i)
				{
					m_State->m_NextIndexes[i] = i + 1;
				}
				m_State->m_NextIndexes[endIndex] = -1;

				// Initialize prev pointers to the previous index and the first
				// prev pointer to an invalid index
				m_State->m_PrevIndexes[0] = -1;
				for (int i = 1; i < length; ++i)
				{
					m_State->m_PrevIndexes[i] = i - 1;
				}

				// The first node is the head and the last node is the tail
				m_State->m_HeadIndex = 0;
				m_State->m_TailIndex = endIndex;
			}
			else
			{
				// The list is empty so there is no head or tail
				m_State->m_HeadIndex = -1;
				m_State->m_TailIndex = -1;
			}

			// Store the allocator for future allocation and deallocation
			// operations
			m_State->m_Allocator = allocator;

			// Initially sized to the given count with the given capacity
			m_State->m_Length = length;
			m_State->m_Capacity = capacity;

			// Version starts at one so that the default (0) is never used
			m_State->m_Version = 1;

			// Initialize safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = length;
			m_MinIndex = 0;
			m_MaxIndex = endIndex;
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
		}

		/// <summary>
		/// Get the capacity of the list. This is always greater than or equal
		/// to its <see cref="Length"/>.
		///
		/// This operation is O(1).
		/// </summary>
		public int Capacity
		{
			get
			{
				RequireValidState();
				return m_State->m_Capacity;
			}
		}

		/// <summary>
		/// Get the number of nodes currently in the list. This is always less
		/// than or equal to the <see cref="Capacity"/>.
		///
		/// This operation is O(1).
		/// </summary>
		public int Length
		{
			get
			{
				RequireValidState();
				return m_State->m_Length;
			}
		}

		/// <summary>
		/// Get an enumerator to the head of the list or an invalid enumerator
		/// if the list is empty.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the head of the list or an invalid enumerator if
		/// the list is empty.
		/// </value>
		public Enumerator Head
		{
			get
			{
				RequireValidState();
				return new Enumerator(this, m_State->m_HeadIndex, m_State->m_Version);
			}
		}

		/// <summary>
		/// Get an enumerator to the tailI of the list or an invalid enumerator
		/// if the list is empty.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the tailI of the list or an invalid enumerator if
		/// the list is empty.
		/// </value>
		public Enumerator Tail
		{
			get
			{
				RequireValidState();
				return new Enumerator(
					this,
					m_State->m_TailIndex,
					m_State->m_Version);
			}
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		public Enumerator GetEnumerator()
		{
			RequireValidState();
			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			RequireValidState();
			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		IEnumerator IEnumerable.GetEnumerator()
		{
			RequireValidState();
			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Index into the list as if it were an array. Do not use this after
		/// modifying the list until calling
		/// <see cref="SortNodeMemoryAddresses"/>.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the node to get or set. Must be greater than or equal to
		/// zero and less than <see cref="Length"/>.
		/// </param>
		public T this[int index]
		{
			get
			{
				RequireValidState();
				RequireReadAccess();
				RequireIndexInBounds(index);
				return UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					index);
			}

			[WriteAccessRequired]
			set
			{
				RequireValidState();
				RequireWriteAccess();
				RequireIndexInBounds(index);
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					index,
					value);
			}
		}

		/// <summary>
		/// Add an node to the end of the list. If the list is full, it will be
		/// automatically resized by allocating new unmanaged memory with double
		/// the <see cref="Capacity"/> and copying over all existing nodes. Any
		/// existing enumerators will _not_ be invalidated.
		///
		/// This operation is O(1) when the list has enough capacity to hold the
		/// added node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="value">
		/// Node to add
		/// </param>
		///
		/// <returns>
		/// An enumerator to the added node
		/// </returns>
		[WriteAccessRequired]
		public Enumerator PushBack(T value)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Need room for one more node
			EnsureCapacity(1);

			// Insert at the end
			int insertIndex = m_State->m_Length;
			UnsafeUtility.WriteArrayElement(
				m_State->m_Values,
				insertIndex,
				value);
			m_State->m_NextIndexes[insertIndex] = -1;
			m_State->m_PrevIndexes[insertIndex] = m_State->m_TailIndex;

			// The list was empty, so this is the head and the tail now
			if (m_State->m_HeadIndex < 0)
			{
				m_State->m_HeadIndex = insertIndex;
				m_State->m_TailIndex = insertIndex;
			}
			// The list wasn't empty, so point the tail at the added node and
			// point the added node at the tail
			else
			{
				m_State->m_NextIndexes[m_State->m_TailIndex] = insertIndex;
				m_State->m_PrevIndexes[insertIndex] = m_State->m_TailIndex;
			}

			// The added node is now the tail
			m_State->m_TailIndex = insertIndex;

			// Update safety ranges
			SetSafetyRange(insertIndex + 1);

			// Count the newly-added node
			m_State->m_Length = insertIndex + 1;

			// Return an enumerator to the added node
			return new Enumerator(this, insertIndex, m_State->m_Version);
		}

		/// <summary>
		/// Add an node to the end of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with the greater of double the <see cref="Capacity"/> and the
		/// current <see cref="Capacity"/> plus the <see cref="Length"/> of the
		/// list and copying over all existing nodes. Any existing enumerators
		/// will _not_ be invalidated.
		///
		/// This operation is O(N) plus the allocator's deallocation and
		/// allocation complexity when the list doesn't have enough capacity to
		/// add all the nodes.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to add the nodes from
		/// </param>
		///
		/// <returns>
		/// An enumerator to the first added node or the tail if the given list
		/// is empty and this list isn't or an invalid enumerator if both lists
		/// are empty.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator PushBack(NativeLinkedList<T> list)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// There's nothing to add when given an empty list.
			// Return an enumerator to the tail or an invalid enumerator if this
			// list is also empty.
			if (list.m_State->m_Length == 0)
			{
				return new Enumerator(
					this,
					m_State->m_TailIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (m_State->m_HeadIndex < 0)
			{
				m_State->m_HeadIndex = list.m_State->m_HeadIndex;
				m_State->m_TailIndex = list.m_State->m_TailIndex;
			}
			// The list wasn't empty, so point the tail at the head of the
			// pushed list and the head of the pushed list at the tail
			else
			{
				m_State->m_NextIndexes[m_State->m_TailIndex] = copiedHeadIndex;
				m_State->m_PrevIndexes[copiedHeadIndex] = m_State->m_TailIndex;
			}

			// The added list's tail is now the tail
			m_State->m_TailIndex = copiedTailIndex;

			// Update safety ranges
			SetSafetyRange(endIndex + list.m_State->m_Length);

			// Count the newly-added list's nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// Return an enumerator to where we inserted the list's head node
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Add an node to the beginning of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with double the <see cref="Capacity"/> and copying over all existing
		/// nodes. Any existing enumerators will _not_ be invalidated.
		///
		/// This operation is O(1) when the list has enough capacity to hold the
		/// added node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="value">
		/// Node to add
		/// </param>
		///
		/// <returns>
		/// An enumerator to the added node
		/// </returns>
		[WriteAccessRequired]
		public Enumerator PushFront(T value)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Need room for one more node
			EnsureCapacity(1);

			// Insert at the end
			int insertIndex = m_State->m_Length;
			UnsafeUtility.WriteArrayElement(
				m_State->m_Values,
				insertIndex,
				value);
			m_State->m_NextIndexes[insertIndex] = m_State->m_HeadIndex;
			m_State->m_PrevIndexes[insertIndex] = -1;

			// The list was empty, so this is the head and the tail now
			if (m_State->m_HeadIndex < 0)
			{
				m_State->m_HeadIndex = insertIndex;
				m_State->m_TailIndex = insertIndex;
			}
			// The list wasn't empty, so point the head at the added node and
			// point the added node at the head
			else
			{
				m_State->m_PrevIndexes[m_State->m_HeadIndex] = insertIndex;
				m_State->m_NextIndexes[insertIndex] = m_State->m_HeadIndex;
			}

			// The added node is now the head
			m_State->m_HeadIndex = insertIndex;

			// Update safety ranges
			SetSafetyRange(insertIndex + 1);

			// Count the newly-added node
			m_State->m_Length = insertIndex + 1;

			return new Enumerator(this, insertIndex, m_State->m_Version);
		}

		/// <summary>
		/// Add an node to the beginning of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with the greater of double the <see cref="Capacity"/> and the
		/// current <see cref="Capacity"/> plus the <see cref="Length"/> of the
		/// list and copying over all existing nodes. Any existing enumerators
		/// will _not_ be invalidated.
		///
		/// This operation is O(N) plus the allocator's deallocation and
		/// allocation complexity when the list doesn't have enough capacity to
		/// add all the nodes.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to add the nodes from
		/// </param>
		///
		/// <returns>
		/// An enumerator to the last added node or the head if the given list
		/// is empty and this list isn't or an invalid enumerator if both lists
		/// are empty.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator PushFront(NativeLinkedList<T> list)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.m_State->m_Length == 0)
			{
				return new Enumerator(
					this,
					m_State->m_HeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (m_State->m_HeadIndex < 0)
			{
				m_State->m_HeadIndex = list.m_State->m_HeadIndex;
				m_State->m_TailIndex = list.m_State->m_TailIndex;
			}
			// The list wasn't empty, so point the head at the tail of the
			// pushed list and the tail of the pushed list at the head
			else
			{
				m_State->m_PrevIndexes[m_State->m_HeadIndex] = copiedTailIndex;
				m_State->m_NextIndexes[copiedTailIndex] = m_State->m_HeadIndex;
			}

			// The added list's head is now the head
			m_State->m_HeadIndex = copiedHeadIndex;

			// Update safety ranges
			SetSafetyRange(endIndex + list.m_State->m_Length);

			// Count the newly-added list's nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// Return an enumerator to where we inserted the list's tail node
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a node after the node referred to by the given enumerator.
		/// This doesn't invalidate any enumerators.
		/// 
		/// This operation is O(1) when the list has enough capacity to hold the
		/// inserted node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid, this function
		/// has no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted node or an invalid enumerator if the
		/// given enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(Enumerator enumerator, T value)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't insert after an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// Need room for one more node
			EnsureCapacity(1);

			// By adding C after B, we're changing from this:
			//   values:      [  A, B,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   values:      [  A, B,  D, C ]
			//   nextIndexes: [  1, 3, -1, 2 ]
			//   prevIndexes: [ -1, 0,  3, 1 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the value to insert at the end node
			int endIndex = m_State->m_Length;
			UnsafeUtility.WriteArrayElement(m_State->m_Values, endIndex, value);

			// Point the end node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[endIndex] = insertNextIndex;

			// Point the end node's previous to the insert node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[endIndex] = enumerator.m_Index;

			// Point the insert node's next to the end node
			m_State->m_NextIndexes[enumerator.m_Index] = endIndex;

			// Point the next node's prev to the end node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = endIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the end node where we moved it
			else
			{
				m_State->m_TailIndex = endIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + 1);

			// Count the newly-added node
			m_State->m_Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the nodes of a given list after the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation is O(N) plus the allocator's deallocation and
		/// allocation complexity when the list doesn't have enough capacity to
		/// add all the nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid, this function
		/// has no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted head node or the given enumerator if
		/// the given list is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			NativeLinkedList<T> list)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't insert after an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.m_State->m_Length == 0)
			{
				return new Enumerator(this, m_State->m_HeadIndex, m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + list.m_State->m_Length);

			// Count the newly-added nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a node before the node referred to by the given enumerator.
		/// This doesn't invalidate any enumerators.
		/// 
		/// This operation is O(1) when the list has enough capacity to hold the
		/// inserted node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid, this function
		/// has no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted node or an invalid enumerator if the
		/// given enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(Enumerator enumerator, T value)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't insert before an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// Need room for one more node
			EnsureCapacity(1);

			// By adding B before C, we're changing from this:
			//   values:      [  A, C,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   values:      [  A, C,  D, B ]
			//   nextIndexes: [  3, 2, -1, 1 ]
			//   prevIndexes: [ -1, 3,  1, 0 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the value to insert at the end node
			int endIndex = m_State->m_Length;
			UnsafeUtility.WriteArrayElement(m_State->m_Values, endIndex, value);

			// Point the end node's next to the insert node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[endIndex] = enumerator.m_Index;

			// Point the end node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[endIndex] = insertPrevIndex;

			// Point the insert node's prev to the end node
			m_State->m_PrevIndexes[enumerator.m_Index] = endIndex;

			// Point the prev node's next to the end node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = endIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the end node where we moved it
			else
			{
				m_State->m_HeadIndex = endIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + 1);

			// Count the newly-added node
			m_State->m_Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the nodes of a given list before the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation is O(N) plus the allocator's deallocation and
		/// allocation complexity when the list doesn't have enough capacity to
		/// add all the nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid, this function
		/// has no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given list is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			NativeLinkedList<T> list)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't insert before an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.m_State->m_Length == 0)
			{
				return new Enumerator(
					this,
					m_State->m_HeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + list.m_State->m_Length);

			// Count the newly-added nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Remove a node. This invalidates all enumerators, including the given
		/// enumerator, if the given enumerator is valid. Note that the node's
		/// value is not cleared since it's blittable and therefore can't hold
		/// any managed reference that could be garbage-collected.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to remove. Is invalid, this function has no
		/// effect.
		/// <see cref="Enumerator.IsValid"/>.
		/// </param>
		///
		/// <returns>
		/// An invalid enumerator if this is the only node in the list or the
		/// given enumerator is invalid, the next node if this is the head of
		/// the list, otherwise the previous node.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator Remove(Enumerator enumerator)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't remove invalid enumerators
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			int newLength;
			int retIndex;

			// Node to remove is the only node
			if (m_State->m_Length == 1)
			{
				m_State->m_HeadIndex = -1;
				m_State->m_TailIndex = -1;
				newLength = 0;
				retIndex = -1;
			}
			// There are at least two nodes in the list
			else
			{
				// Node to remove is the head
				if (enumerator.m_Index == m_State->m_HeadIndex)
				{
					// Move the head pointer forward one
					int nextIndex = m_State->m_NextIndexes[enumerator.m_Index];
					m_State->m_HeadIndex = nextIndex;

					// Make the new head's previous node be invalid
					m_State->m_PrevIndexes[nextIndex] = -1;

					// Return an enumerator to the new head
					retIndex = nextIndex;
				}
				// Node to remove is the tail
				else if (enumerator.m_Index == m_State->m_TailIndex)
				{
					// Move the tail pointer back one
					int prevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
					m_State->m_TailIndex = prevIndex;

					// Make the new tail's next node be invalid
					m_State->m_NextIndexes[prevIndex] = -1;

					// Return an enumerator to the new tail
					retIndex = prevIndex;
				}
				// Node to remove is an interior node.
				else
				{
					// Link the previous node to the next node and the next node
					// to the previous node
					int prevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
					int nextIndex = m_State->m_NextIndexes[enumerator.m_Index];
					m_State->m_NextIndexes[prevIndex] = nextIndex;
					m_State->m_PrevIndexes[nextIndex] = prevIndex;

					// Return an enumerator to the previous node
					retIndex = prevIndex;
				}

				// Move the last node to where the node was removed from
				int lastIndex = m_State->m_Length - 1;
				if (enumerator.m_Index != lastIndex)
				{
					// Copy the last node to where the removed node was
					int lastNextIndex = m_State->m_NextIndexes[lastIndex];
					int lastPrevIndex = m_State->m_PrevIndexes[lastIndex];
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						enumerator.m_Index,
						UnsafeUtility.ReadArrayElement<T>(
							m_State->m_Values,
							lastIndex));
					m_State->m_NextIndexes[enumerator.m_Index] = lastNextIndex;
					m_State->m_PrevIndexes[enumerator.m_Index] = lastPrevIndex;

					// If the last node wasn't the tail, set its next node's
					// previous index to where the last node was moved to
					if (lastNextIndex >= 0)
					{
						m_State->m_PrevIndexes[lastNextIndex] = enumerator.m_Index;
					}

					// If the last node wasn't the head, set its previous node's
					// next index to where the last node was moved to
					if (lastPrevIndex >= 0)
					{
						m_State->m_NextIndexes[lastPrevIndex] = enumerator.m_Index;
					}

					// If the last node was the head, update the head index
					if (lastIndex == m_State->m_HeadIndex)
					{
						m_State->m_HeadIndex = enumerator.m_Index;
					}

					// If the last node was the tail, update the tail index
					if (lastIndex == m_State->m_TailIndex)
					{
						m_State->m_TailIndex = enumerator.m_Index;
					}

					// If the last node was the return, update the return
					if (lastIndex == retIndex)
					{
						retIndex = enumerator.m_Index;
					}
				}

				// Account for the removed node
				newLength = lastIndex;
			}

			// Set the new length
			m_State->m_Length = newLength;

			// Update safety ranges
			SetSafetyRange(newLength);

			// Invalidate all enumerators
			m_State->m_Version++;

			// Return the appropriate enumerator
			return new Enumerator(this, retIndex, m_State->m_Version);
		}

		/// <summary>
		/// Reorder the list such that its order is preserved but the nodes are
		/// laid out sequentially in memory. This allows for indexing into the
		/// list after a call to <see cref="Remove"/>. This invalidates all
		/// enumerators.
		///
		/// This operation is O(N).
		/// </summary>
		[WriteAccessRequired]
		public void SortNodeMemoryAddresses()
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Swap the value for the head with the value for the first element,
			// then the the node after the head that to the second element,
			// until the tail is reached
			for (int curIndex = m_State->m_HeadIndex, startIndex = 0;
				 curIndex >= 0;
				 curIndex = m_State->m_NextIndexes[curIndex], startIndex++)
			{
				// Never swap backwards. The part of the array up to startIndex
				// is already in order.
				if (curIndex > startIndex)
				{
					T startValue = UnsafeUtility.ReadArrayElement<T>(
						m_State->m_Values,
						startIndex);
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						startIndex,
						UnsafeUtility.ReadArrayElement<T>(
							m_State->m_Values,
							curIndex));
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						curIndex,
						startValue);
				}
			}

			int endIndex = m_State->m_Length - 1;

			// Set all the next pointers to point to the next index now that
			// the values are sequential. The last one points to null.
			for (int i = 0; i <= endIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[endIndex] = -1;

			// Set all the prev pointers to point to the prev index now that
			// the values are sequential
			for (int i = 0; i <= endIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}


			// The head is now at the beginning and the tail is now at the end
			m_State->m_HeadIndex = 0;
			m_State->m_TailIndex = endIndex;

			// Invalidate all enumerators
			m_State->m_Version++;
		}

		/// <summary>
		/// Copy all nodes to a managed array, which is optionally allocated.
		///
		/// This operation is O(N).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If null or less than <see cref="Length"/>,
		/// a new array will be allocated.
		/// </param>
		/// 
		/// <returns>
		/// A managed array with all of the list's nodes. This is either the
		/// given array if it was non-null and at least as long as
		/// <see cref="Length"/> or a newly-allocated array with length equal to
		/// <see cref="Length"/> otherwise.
		/// </returns>
		public T[] ToArray(T[] array = null)
		{
			RequireValidState();
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
			if (array == null || array.Length < m_State->m_Length)
			{
				array = new T[m_State->m_Length];
			}

			// Copy all nodes to the array
			for (int i = m_State->m_HeadIndex, arrIndex = 0;
				i >= 0;
				i = m_State->m_NextIndexes[i], arrIndex++)
			{
				array[arrIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					i);
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes to a managed array, which is optionally allocated.
		/// The nodes are copied from tail to head.
		///
		/// This operation is O(N).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If null or less than <see cref="Length"/>,
		/// a new array will be allocated.
		/// </param>
		/// 
		/// <returns>
		/// A managed array with all of the list's nodes. This is either the
		/// given array if it was non-null and at least as long as
		/// <see cref="Length"/> or a newly-allocated array with length equal to
		/// <see cref="Length"/> otherwise.
		/// </returns>
		public T[] ToArrayReverse(T[] array = null)
		{
			RequireValidState();
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
			if (array == null || array.Length < m_State->m_Length)
			{
				array = new T[m_State->m_Length];
			}

			// Copy all nodes to the array
			for (int i = m_State->m_TailIndex, arrIndex = 0;
				i >= 0;
				i = m_State->m_PrevIndexes[i], arrIndex++)
			{
				array[arrIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					i);
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes to a <see cref="NativeArray{T}"/>
		///
		/// This operation is O(N).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to
		/// </param>
		/// 
		/// <param name="srcEnumerator">
		/// Enumerator to the first node to copy. This function has no effect
		/// if this is invalid. By default, the head node is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. By default, the first element is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy. By default, the entire list is used.
		/// </param>
		public void CopyToNativeArray(
			NativeArray<T> array,
			Enumerator srcEnumerator = default(Enumerator),
			int destIndex = 0,
			int length = -1)
		{
			RequireValidState();
			RequireReadAccess();

			// Copy the nodes' values to the array
			while (length > 0)
			{
				// Copy the node's value
				RequireIndexInBounds(srcEnumerator.m_Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcEnumerator.m_Index);

				// Go to the next node
				srcEnumerator.MoveNext();

				// Count the copy
				destIndex++;
				length--;
			}
		}

		/// <summary>
		/// Check if the underlying unmanaged memory has been created. This is
		/// initially true then false after <see cref="Dispose"/> is called.
		///
		/// This operation is O(1).
		/// </summary>
		public bool IsCreated
		{
			get
			{
				return m_State != null;
			}
		}

		/// <summary>
		/// Release the list's unmanaged memory. Do not use it after this.
		/// 
		/// This complexity of this operation is O(1) plus the allocator's
		/// deallocation complexity.
		/// </summary>
		[WriteAccessRequired]
		public void Dispose()
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			if (m_State != null)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif

				// Free the state's contents
				UnsafeUtility.Free(
					m_State->m_Values,
					m_State->m_Allocator);
				m_State->m_Values = null;
				UnsafeUtility.Free(
					m_State->m_NextIndexes,
					m_State->m_Allocator);
				m_State->m_NextIndexes = null;
				UnsafeUtility.Free(
					m_State->m_PrevIndexes,
					m_State->m_Allocator);
				m_State->m_PrevIndexes = null;

				// Forget the list
				m_State->m_HeadIndex = -1;
				m_State->m_TailIndex = -1;
				m_State->m_Length = 0;
				m_State->m_Capacity = 0;

				// Invalidate all enumerators
				m_State->m_Version++;

				// Free the state itself
				UnsafeUtility.Free(m_State, m_State->m_Allocator);
				m_State = null;
			}
		}

		/// <summary>
		/// Copy all the nodes of a list to the end of the arrays. The list must
		/// already have sufficient capacity to hold all the nodes of the list
		/// to copy.
		/// 
		/// This operation requires valid state and read-write access to the
		/// portion of the list starting at m_State->m_Length as well as the
		/// next list->m_State->m_Length -1 nodes.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to copy. Must not be empty.
		/// </param>
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			NativeLinkedList<T> list,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Copy the list's node values at the end. Copying with stride is
			// the same way NativeSlice<T> copies.
			int endIndex = m_State->m_Length;
			RequireRangeInBounds(
				endIndex,
				endIndex + list.m_State->m_Length - 1);
			int sizeofT = UnsafeUtility.SizeOf<T>();
			UnsafeUtility.MemCpyStride(
				(byte*)m_State->m_Values + sizeofT * endIndex,
				sizeofT,
				list.m_State->m_Values,
				sizeofT,
				sizeofT,
				list.m_State->m_Length);

			// Copy the list's next and prev pointers at the end, offset for
			// the new location at the end of this list.
			for (int i = 0; i < list.m_State->m_Length; ++i)
			{
				m_State->m_NextIndexes[endIndex + i]
					= list.m_State->m_NextIndexes[i] + endIndex;
			}
			for (int i = 0; i < list.m_State->m_Length; ++i)
			{
				m_State->m_PrevIndexes[endIndex + i]
					= list.m_State->m_PrevIndexes[i] + endIndex;
			}

			// Re-set the list's head and tail pointers since we offset them
			copiedHeadIndex = endIndex + list.m_State->m_HeadIndex;
			copiedTailIndex = endIndex + list.m_State->m_TailIndex;
			m_State->m_NextIndexes[copiedTailIndex] = -1;
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
		}

		/// <summary>
		/// Ensure that the capacity of the list is sufficient to store a given
		/// number of new nodes.
		/// 
		/// This operation requires valid state and exclusive read-write access.
		/// 
		/// This operation is O(N) if the list has insufficient capacity to
		/// store the new nodes and O(1) otherwise.
		/// </summary>
		/// 
		/// <param name="numNewNodes">
		/// Number of new nodes that must be stored.
		/// </param>
		private void EnsureCapacity(int numNewNodes)
		{
			if (m_State->m_Length + numNewNodes > m_State->m_Capacity)
			{
				// The new capacity must be at least double to avoid excessive
				// and expensive capacity increase operations.
				int newCapacity = Math.Max(
					m_State->m_Length * 2,
					m_State->m_Length + numNewNodes);
				
				// Resize values
				int sizeofT = UnsafeUtility.SizeOf<T>();
				void* newvalues = UnsafeUtility.Malloc(
					newCapacity * sizeofT,
					UnsafeUtility.AlignOf<T>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpyStride(
					newvalues,
					sizeofT,
					m_State->m_Values,
					sizeofT,
					sizeofT,
					m_State->m_Length);
				UnsafeUtility.Free(
					m_State->m_Values,
					m_State->m_Allocator);
				m_State->m_Values = newvalues;

				// Resize nextIndexes
				int* newNextIndexes = (int*)UnsafeUtility.Malloc(
					newCapacity * sizeof(int),
					UnsafeUtility.AlignOf<int>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpy(
					newNextIndexes,
					m_State->m_NextIndexes,
					m_State->m_Length * sizeof(int));
				UnsafeUtility.Free(
					m_State->m_NextIndexes,
					m_State->m_Allocator);
				m_State->m_NextIndexes = newNextIndexes;

				// Resize prevIndexes
				int* newPrevIndexes = (int*)UnsafeUtility.Malloc(
					newCapacity * sizeof(int),
					UnsafeUtility.AlignOf<int>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpy(
					newPrevIndexes,
					m_State->m_PrevIndexes,
					m_State->m_Length * sizeof(int));
				UnsafeUtility.Free(
					m_State->m_PrevIndexes,
					m_State->m_Allocator);
				m_State->m_PrevIndexes = newPrevIndexes;

				m_State->m_Capacity = newCapacity;
			}
		}

		/// <summary>
		/// Update the safety range's m_Length and also m_MaxIndex if it was
		/// at the end of the array and the list isn't currently being used by
		/// a ParallelFor job.
		/// </summary>
		/// 
		/// <param name="length">
		/// New length to set
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void SetSafetyRange(int length)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// Max is at the last element, so it's a candidate for adjustment.
			// We don't want to adjust it if we're in the middle of a
			// ParallelFor job though since we'd then widen the acceptable
			// range of access which could cause data contention issues among
			// parallel executions of the same job. So check for that case and
			// only adjust the max if the min is zero, as it would only be
			// outside of a ParallelFor job execution. This is the case because
			// the min and max are set during the job execution with the min
			// only being zero when the job is processing the first element,
			// which wouldn't be the case if the max is also at the last
			// element.
			if (m_MaxIndex == m_Length - 1 && m_MinIndex == 0)
			{
				m_MaxIndex = length - 1;
			}

			m_Length = length;
#endif
		}

		/// <summary>
		/// Throw an exception when an index is out of the safety check bounds:
		///   [m_MinIndex, m_MaxIndex]
		/// and the list is being used by a ParallelFor job or when the index
		/// is out of the capacity:
		///   [0, state->Capacity]
		/// </summary>
		/// 
		/// <param name="index">
		/// Index that must be in the safety check bounds
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private void RequireIndexInBounds(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// The index is out of the safety check bounds and the min or max
			// is set to their non-default values indicating that the list is
			// being used in a ParallelFor job.
			if (
				(index < m_MinIndex || index > m_MaxIndex)
				&& (m_MinIndex != 0 || m_MaxIndex != m_Length - 1))
			{
				throw new IndexOutOfRangeException(
					"Index " + index + " is out of restricted " +
					"ParallelFor range [" + m_MinIndex +
					"..." + m_MaxIndex + "] in ReadWriteBuffer.\n" +
					"ReadWriteBuffers are restricted to only read and " +
					"write the node at the job index. You can " +
					"use double buffering strategies to avoid race " +
					"conditions due to reading and writing in parallel " +
					"to the same nodes from a ParallelFor job.");
			}

			// The index is out of the capacity
			RequireValidState();
			if (index < 0 || index > m_State->m_Capacity)
			{
				throw new IndexOutOfRangeException(
					"Index " + index + " is out of range of '" +
					m_State->m_Capacity + "' Capacity.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the safety check bounds don't encompass the
		/// full list.
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private void RequireFullListSafetyCheckBounds()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (m_MinIndex > 0 || m_MaxIndex >= m_Length)
			{
				throw new IndexOutOfRangeException(
					"This operation cannot be performed from a ParallelFor " +
					"job because exclusive access to the full list is " +
					"required to prevent errors. You can " +
					"use double buffering strategies to avoid race " +
					"conditions due to reading and writing in parallel " +
					"to the same nodes from a ParallelFor job.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the safety check bounds:
		///   [m_MinIndex, m_MaxIndex]
		/// don't encompass the given range and the list is being used by a
		/// ParallelFor job or when the given range has any indices outside of
		/// the list's capacity:
		///   [0, state->Capacity]
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private void RequireRangeInBounds(
			int startIndex,
			int endIndex)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// The range is out of the safety check bounds and the min or max
			// is set to their non-default values indicating that the list is
			// being used in a ParallelFor job.
			if (
				(startIndex < m_MinIndex || endIndex > m_MaxIndex)
				&& (m_MinIndex != 0 || m_MaxIndex != m_Length - 1))
			{
				throw new IndexOutOfRangeException(
					"Range [" + startIndex + "..." + endIndex + "] is out of " +
					"restricted ParallelFor range [" + m_MinIndex +
					"..." + m_MaxIndex + "] in ReadWriteBuffer.\n" +
					"ReadWriteBuffers are restricted to only read and " +
					"write the node at the job index. You can " +
					"use double buffering strategies to avoid race " +
					"conditions due to reading and writing in parallel " +
					"to the same nodes from a ParallelFor job.");
			}

			// The range is out of the capacity
			RequireValidState();
			if (m_MinIndex < 0 || m_MaxIndex >= m_State->m_Capacity)
			{
				throw new IndexOutOfRangeException(
					"Range [" + startIndex + "..." + endIndex + "] is out of " +
					"range of '" + m_State->m_Capacity + "' Capacity.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the list isn't readable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private unsafe void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the list isn't writable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private unsafe void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the state is null
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[BurstDiscard]
		private unsafe void RequireValidState()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (m_State == null)
			{
				throw new InvalidOperationException(
					"NativeList was either not initialized via a " +
					"non-default constructor or was used after calling " +
					"Dispose().");
			}
#endif
		}
	}
	 
	/// <summary>
	/// Provides a debugger view of <see cref="NativeLinkedList{T}"/>.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of nodes in the list
	/// </typeparam>
	internal sealed class NativeLinkedListDebugView<T>
		where T : struct
	{
		/// <summary>
		/// List to view
		/// </summary>
		private NativeLinkedList<T> list;
	 
		/// <summary>
		/// Create the view for a given list
		/// </summary>
		/// 
		/// <param name="list">
		/// List to view
		/// </param>
		public NativeLinkedListDebugView(NativeLinkedList<T> list)
		{
			this.list = list;
		}
	 
		/// <summary>
		/// Get a managed array version of the list's nodes to be viewed in the
		/// debugger.
		/// </summary>
		public T[] Items
		{
			get
			{
				return list.ToArray();
			}
		}
	}
}