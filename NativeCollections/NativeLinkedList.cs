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
		/// Each node's data. Indices correspond with nextIndexes.
		/// </summary>
		internal void* Datas;

		/// <summary>
		/// Each node's next node index. Indices correspond with datas.
		/// </summary>
		internal int* NextIndexes;

		/// <summary>
		/// Each node's previous node index. Indices correspond with datas.
		/// </summary>
		internal int* PrevIndexes;

		/// <summary>
		/// Index of the first node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int HeadIndex;

		/// <summary>
		/// Index of the last node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int TailIndex;

		/// <summary>
		/// Number of nodes contained
		/// </summary>
		internal int Length;

		/// <summary>
		/// Number of nodes that can be contained
		/// </summary>
		internal int Capacity;

		/// <summary>
		/// Version of enumerators that are valid for this list. This starts at
		/// 1 and increases by one with each change that invalidates the list's
		/// enumerators.
		/// </summary>
		internal int Version;

		/// <summary>
		/// Allocator used to create the backing arrays
		/// </summary>
		internal Allocator Allocator;
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
			internal int Index;

			/// <summary>
			/// Version of the list that this enumerator is valid for
			/// </summary>
			internal int Version;

			/// <summary>
			/// List to iterate
			/// </summary>
			internal NativeLinkedList<T> list;

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
				Index = index;
				Version = version;
				this.list = list;
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
			/// Get an enumerator to the next node or an invalid enumerator if
			/// this enumerator is at the tail of the list or is invalid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator to the next node or an invalid enumerator if this
			/// enumerator is at the tail of the list or is invalid.
			/// </returns>
			public Enumerator Next
			{
				get
				{
					list.RequireValidState();
					list.RequireReadAccess();

					if (IsValid)
					{
						list.RequireIndexInBounds(Index);
						return new Enumerator(
							list,
							list.state->NextIndexes[Index],
							Version);
					}
					return MakeInvalid();
				}
			}

			/// <summary>
			/// Get an enumerator to the previous node or an invalid enumerator
			/// if this enumerator is at the head of the list or is invalid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator to the previous node or an invalid enumerator if
			/// this enumerator is at the head of the list or is invalid.
			/// </returns>
			public Enumerator Prev
			{
				get
				{
					list.RequireValidState();
					list.RequireReadAccess();

					if (IsValid)
					{
						list.RequireIndexInBounds(Index);
						return new Enumerator(
							list,
							list.state->PrevIndexes[Index],
							Version);
					}
					return MakeInvalid();
				}
			}

			/// <summary>
			/// Move to the next node or invalid this enumerator if at the tail.
			/// This function has no effect if this enumerator is invalid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is still valid
			/// </returns>
			public bool MoveNext()
			{
				list.RequireValidState();
				list.RequireReadAccess();

				if (IsValid)
				{
					list.RequireIndexInBounds(Index);
					Index = list.state->NextIndexes[Index];
					return Index >= 0;
				}

				// Invalidate
				Index = -1;
				Version = -1;
				list = default(NativeLinkedList<T>);
				return false;
			}

			/// <summary>
			/// Move to the previous node or invalid this enumerator if at the
			/// head. This function has no effect if this enumerator is invalid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this iterator is still valid
			/// </returns>
			public bool MovePrev()
			{
				list.RequireValidState();
				list.RequireReadAccess();

				if (IsValid)
				{
					list.RequireIndexInBounds(Index);
					Index = list.state->PrevIndexes[Index];
					return Index >= 0;
				}

				// Invalidate
				Index = -1;
				Version = -1;
				list = default(NativeLinkedList<T>);
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
					return Index >= 0
						&& list.state != null
						&& Index < list.state->Length
						&& Version == list.state->Version;
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
					&& a.Index == b.Index
					&& a.list.state == b.list.state;
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
					|| a.Index != b.Index
					|| a.list.state != b.list.state;
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
				return Index;
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
					list.RequireValidState();
					list.RequireReadAccess();
					Index = list.state->HeadIndex;
				}
			}

			/// <summary>
			/// Get or set a node's data
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value. If the enumerator is invalid, the default
			/// is returned in the 'get' case and the 'set' has no effect.
			/// </value>
			public T Current
			{
				get
				{
					if (IsValid)
					{
						list.RequireValidState();
						list.RequireReadAccess();
						list.RequireIndexInBounds(Index);
						return UnsafeUtility.ReadArrayElement<T>(
							list.state->Datas,
							Index);
					}
					return default(T);
				}

				[WriteAccessRequired]
				set
				{
					if (IsValid)
					{
						list.RequireValidState();
						list.RequireWriteAccess();
						list.RequireIndexInBounds(Index);
						UnsafeUtility.WriteArrayElement(
							list.state->Datas,
							Index,
							value);
					}
				}
			}

			/// <summary>
			/// Get a node's data. Prefer using the generic version of
			/// <see cref="Current"/> as this will cause boxing when enumerating
			/// value type node data. This is provided only for compatibility
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
					if (IsValid)
					{
						list.RequireValidState();
						list.RequireReadAccess();
						list.RequireIndexInBounds(Index);
						return UnsafeUtility.ReadArrayElement<T>(
							list.state->Datas,
							Index);
					}
					return null;
				}
			}
		}

		/// <summary>
		/// State of the list or null after being disposed. This is shared among
		/// all instances of the list.
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		private NativeLinkedListState* state;

		// These fields are all required when safety checks are enabled
		// They must have these exact types, names, attributes, and order
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once InconsistentNaming
		internal int m_Length;

		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once FieldCanBeMadeReadOnly.Global
		// ReSharper disable once InconsistentNaming
		internal int m_MinIndex;

		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once InconsistentNaming
		internal int m_MaxIndex;

		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once InconsistentNaming
		internal AtomicSafetyHandle m_Safety;

		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once InconsistentNaming
		[NativeSetClassTypeToNullOnSchedule]
		internal DisposeSentinel m_DisposeSentinel;
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
			state = (NativeLinkedListState*)UnsafeUtility.Malloc(
				sizeof(NativeLinkedListState),
				UnsafeUtility.AlignOf<NativeLinkedListState>(),
				allocator);

			// Create the backing arrays. There's no need to clear them since we
			// make no assumptions about the contents anyways.
			state->Datas = UnsafeUtility.Malloc(
				UnsafeUtility.SizeOf<T>() * capacity,
				UnsafeUtility.AlignOf<T>(),
				allocator
			);
			state->NextIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			state->PrevIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);

			state->Allocator = allocator;

			// Initially empty with the given capacity
			state->Length = 0;
			state->Capacity = capacity;
			state->HeadIndex = -1;
			state->TailIndex = -1;

			// Version starts at one so that the default (0) is never used
			state->Version = 1;

			// Initialize safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = 0;
			m_MinIndex = 0;
			m_MaxIndex = -1;
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
				return state->Capacity;
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
				return state->Length;
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
				return new Enumerator(this, state->HeadIndex, state->Version);
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
				return new Enumerator(this, state->TailIndex, state->Version);
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
		public Enumerator GetEnumerator()
		{
			return Head;
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
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return Head;
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
		IEnumerator IEnumerable.GetEnumerator()
		{
			return Head;
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
				return UnsafeUtility.ReadArrayElement<T>(state->Datas, index);
			}

			[WriteAccessRequired]
			set
			{
				RequireValidState();
				RequireWriteAccess();
				RequireIndexInBounds(index);
				UnsafeUtility.WriteArrayElement(state->Datas, index, value);
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

			// The list is full. Resize.
			if (state->Length == state->Capacity)
			{
				IncreaseCapacity(state->Length * 2);
			}

			// Insert at the end
			int insertIndex = state->Length;
			RequireIndexInBounds(insertIndex);
			UnsafeUtility.WriteArrayElement(state->Datas, insertIndex, value);
			state->NextIndexes[insertIndex] = -1;
			state->PrevIndexes[insertIndex] = state->TailIndex;

			// The list was empty, so this is the head and the tail now
			if (state->HeadIndex < 0)
			{
				state->HeadIndex = insertIndex;
				state->TailIndex = insertIndex;
			}
			// The list wasn't empty, so point the tail at the added node and
			// point the added node at the tail
			else
			{
				RequireIndexInBounds(state->TailIndex);
				state->NextIndexes[state->TailIndex] = insertIndex;
				RequireIndexInBounds(insertIndex);
				state->PrevIndexes[insertIndex] = state->TailIndex;
			}

			// The added node is now the tail
			state->TailIndex = insertIndex;

			// Update safety ranges
			SetSafetyRange(insertIndex + 1);

			// Count the newly-added node
			state->Length = insertIndex + 1;

			// Return an enumerator to the added node
			return new Enumerator(this, insertIndex, state->Version);
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

			// There's nothing to add when given an empty list.
			// Return an enumerator to the tail or an invalid enumerator if this
			// list is also empty.
			if (list.state->Length == 0)
			{
				return new Enumerator(this, state->TailIndex, state->Version);
			}

			// The list is full. Resize.
			if (state->Length + list.state->Length > state->Capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(
					Math.Max(
						state->Length * 2,
						state->Length + list.state->Length));
			}

			// Insert the list at the end
			int endIndex = state->Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (state->HeadIndex < 0)
			{
				state->HeadIndex = list.state->HeadIndex;
				state->TailIndex = list.state->TailIndex;
			}
			// The list wasn't empty, so point the tail at the head of the
			// pushed list and the head of the pushed list at the tail
			else
			{
				RequireIndexInBounds(state->TailIndex);
				state->NextIndexes[state->TailIndex] = copiedHeadIndex;
				RequireIndexInBounds(copiedHeadIndex);
				state->PrevIndexes[copiedHeadIndex] = state->TailIndex;
			}

			// The added list's tail is now the tail
			state->TailIndex = copiedTailIndex;

			// Update safety ranges
			SetSafetyRange(endIndex + list.state->Length);

			// Count the newly-added list's nodes
			state->Length = endIndex + list.state->Length;

			// Return an enumerator to where we inserted the list's head node
			return new Enumerator(this, copiedHeadIndex, state->Version);
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

			// The list is full. Resize.
			if (state->Length == state->Capacity)
			{
				IncreaseCapacity(state->Length * 2);
			}

			// Insert at the end
			int insertIndex = state->Length;
			RequireIndexInBounds(insertIndex);
			UnsafeUtility.WriteArrayElement(state->Datas, insertIndex, value);
			state->NextIndexes[insertIndex] = state->HeadIndex;
			state->PrevIndexes[insertIndex] = -1;

			// The list was empty, so this is the head and the tail now
			if (state->HeadIndex < 0)
			{
				state->HeadIndex = insertIndex;
				state->TailIndex = insertIndex;
			}
			// The list wasn't empty, so point the head at the added node and
			// point the added node at the head
			else
			{
				RequireIndexInBounds(state->HeadIndex);
				state->PrevIndexes[state->HeadIndex] = insertIndex;
				RequireIndexInBounds(insertIndex);
				state->NextIndexes[insertIndex] = state->HeadIndex;
			}

			// The added node is now the head
			state->HeadIndex = insertIndex;

			// Update safety ranges
			SetSafetyRange(insertIndex + 1);

			// Count the newly-added node
			state->Length = insertIndex + 1;

			return new Enumerator(this, insertIndex, state->Version);
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

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.state->Length == 0)
			{
				return new Enumerator(this, state->HeadIndex, state->Version);
			}

			// The list is full. Resize.
			if (state->Length + list.state->Length > state->Capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(
					Math.Max(
						state->Length * 2,
						state->Length + list.state->Length));
			}

			// Insert the list at the end
			int endIndex = state->Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (state->HeadIndex < 0)
			{
				state->HeadIndex = list.state->HeadIndex;
				state->TailIndex = list.state->TailIndex;
			}
			// The list wasn't empty, so point the head at the tail of the
			// pushed list and the tail of the pushed list at the head
			else
			{
				RequireIndexInBounds(state->HeadIndex);
				state->PrevIndexes[state->HeadIndex] = copiedTailIndex;
				RequireIndexInBounds(copiedTailIndex);
				state->NextIndexes[copiedTailIndex] = state->HeadIndex;
			}

			// The added list's head is now the head
			state->HeadIndex = copiedHeadIndex;

			// Update safety ranges
			SetSafetyRange(endIndex + list.state->Length);

			// Count the newly-added list's nodes
			state->Length = endIndex + list.state->Length;

			// Return an enumerator to where we inserted the list's tail node
			return new Enumerator(this, copiedTailIndex, state->Version);
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

			// Can't insert after an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// The list is full. Resize.
			if (state->Length == state->Capacity)
			{
				IncreaseCapacity(state->Length * 2);
			}

			// By adding C after B, we're changing from this:
			//   datas:       [  A, B,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   datas:       [  A, B,  D, C ]
			//   nextIndexes: [  1, 3, -1, 2 ]
			//   prevIndexes: [ -1, 0,  3, 1 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the data to insert at the end node
			int endIndex = state->Length;
			RequireIndexInBounds(endIndex);
			UnsafeUtility.WriteArrayElement(state->Datas, endIndex, value);

			// Point the end node's next to the next node
			RequireIndexInBounds(enumerator.Index);
			int insertNextIndex = state->NextIndexes[enumerator.Index];
			state->NextIndexes[endIndex] = insertNextIndex;

			// Point the end node's previous to the insert node
			int insertPrevIndex = state->PrevIndexes[enumerator.Index];
			state->PrevIndexes[endIndex] = enumerator.Index;

			// Point the insert node's next to the end node
			state->NextIndexes[enumerator.Index] = endIndex;

			// Point the next node's prev to the end node
			if (insertNextIndex >= 0)
			{
				RequireIndexInBounds(insertNextIndex);
				state->PrevIndexes[insertNextIndex] = endIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the end node where we moved it
			else
			{
				state->TailIndex = endIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + 1);

			// Count the newly-added node
			state->Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, state->Version);
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

			// Can't insert after an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.state->Length == 0)
			{
				return new Enumerator(this, state->HeadIndex, state->Version);
			}

			// The list is full. Resize.
			if (state->Length + list.state->Length > state->Capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(
					Math.Max(
						state->Length * 2,
						state->Length + list.state->Length));
			}

			// Insert the list at the end
			int endIndex = state->Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			RequireIndexInBounds(enumerator.Index);
			RequireIndexInBounds(copiedTailIndex);
			int insertNextIndex = state->NextIndexes[enumerator.Index];
			state->NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			int insertPrevIndex = state->PrevIndexes[enumerator.Index];
			RequireIndexInBounds(copiedHeadIndex);
			state->PrevIndexes[copiedHeadIndex] = enumerator.Index;

			// Point the insert node's next to the inserted head node
			state->NextIndexes[enumerator.Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				RequireIndexInBounds(insertNextIndex);
				state->PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				state->TailIndex = copiedTailIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + list.state->Length);

			// Count the newly-added nodes
			state->Length = endIndex + list.state->Length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, state->Version);
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

			// Can't insert before an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// The list is full. Resize.
			if (state->Length == state->Capacity)
			{
				IncreaseCapacity(state->Length * 2);
			}

			// By adding B before C, we're changing from this:
			//   datas:       [  A, C,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   datas:       [  A, C,  D, B ]
			//   nextIndexes: [  3, 2, -1, 1 ]
			//   prevIndexes: [ -1, 3,  1, 0 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the data to insert at the end node
			int endIndex = state->Length;
			RequireIndexInBounds(endIndex);
			UnsafeUtility.WriteArrayElement(state->Datas, endIndex, value);

			// Point the end node's next to the insert node
			RequireIndexInBounds(enumerator.Index);
			int insertNextIndex = state->NextIndexes[enumerator.Index];
			state->NextIndexes[endIndex] = enumerator.Index;

			// Point the end node's previous to the prev node
			int insertPrevIndex = state->PrevIndexes[enumerator.Index];
			state->PrevIndexes[endIndex] = insertPrevIndex;

			// Point the insert node's prev to the end node
			state->PrevIndexes[enumerator.Index] = endIndex;

			// Point the prev node's next to the end node
			if (insertPrevIndex >= 0)
			{
				RequireIndexInBounds(insertPrevIndex);
				state->NextIndexes[insertPrevIndex] = endIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the end node where we moved it
			else
			{
				state->HeadIndex = endIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + 1);

			// Count the newly-added node
			state->Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, state->Version);
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

			// Can't insert before an invalid enumerator
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an enumerator to the head or an invalid enumerator if this
			// list is also empty.
			if (list.state->Length == 0)
			{
				return new Enumerator(this, state->HeadIndex, state->Version);
			}

			// The list is full. Resize.
			if (state->Length + list.state->Length > state->Capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(
					Math.Max(
						state->Length * 2,
						state->Length + list.state->Length));
			}

			// Insert the list at the end
			int endIndex = state->Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			RequireIndexInBounds(enumerator.Index);
			int insertNextIndex = state->NextIndexes[enumerator.Index];
			RequireIndexInBounds(copiedTailIndex);
			state->NextIndexes[copiedTailIndex] = enumerator.Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = state->PrevIndexes[enumerator.Index];
			RequireIndexInBounds(copiedHeadIndex);
			state->PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			state->PrevIndexes[enumerator.Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				RequireIndexInBounds(insertPrevIndex);
				state->NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				state->HeadIndex = copiedHeadIndex;
			}

			// Update safety ranges
			SetSafetyRange(endIndex + list.state->Length);

			// Count the newly-added nodes
			state->Length = endIndex + list.state->Length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, state->Version);
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

			// Can't remove invalid enumerators
			if (!enumerator.IsValid)
			{
				return Enumerator.MakeInvalid();
			}

			int newLength;
			int retIndex;

			// Node to remove is the only node
			if (state->Length == 1)
			{
				state->HeadIndex = -1;
				state->TailIndex = -1;
				newLength = 0;
				retIndex = -1;
			}
			// There are at least two nodes in the list
			else
			{
				RequireIndexInBounds(enumerator.Index);

				// Node to remove is the head
				if (enumerator.Index == state->HeadIndex)
				{
					// Move the head pointer forward one
					int headNextIndex = state->NextIndexes[enumerator.Index];
					state->HeadIndex = headNextIndex;

					// Make the new head's previous node be invalid
					RequireIndexInBounds(headNextIndex);
					state->PrevIndexes[headNextIndex] = -1;

					// Return an enumerator to the new head
					retIndex = headNextIndex;
				}
				// Node to remove is the tail
				else if (enumerator.Index == state->TailIndex)
				{
					// Move the tail pointer back one
					int tailPrevIndex = state->PrevIndexes[enumerator.Index];
					state->TailIndex = tailPrevIndex;

					// Make the new tail's next node be invalid
					RequireIndexInBounds(tailPrevIndex);
					state->NextIndexes[tailPrevIndex] = -1;

					// Return an enumerator to the new tail
					retIndex = tailPrevIndex;
				}
				// Node to remove is an interior node.
				else
				{
					// Link the previous node to the next node and the next node
					// to the previous node
					int prevIndex = state->PrevIndexes[enumerator.Index];
					int nextIndex = state->NextIndexes[enumerator.Index];
					RequireIndexInBounds(prevIndex);
					state->NextIndexes[prevIndex] = nextIndex;
					RequireIndexInBounds(nextIndex);
					state->PrevIndexes[nextIndex] = prevIndex;

					// Return an enumerator to the previous node
					retIndex = prevIndex;
				}

				// Move the last node to where the node was removed from
				int lastIndex = state->Length - 1;
				if (enumerator.Index != lastIndex)
				{
					// Copy the last node to where the removed node was
					RequireIndexInBounds(lastIndex);
					int lastNextIndex = state->NextIndexes[lastIndex];
					int lastPrevIndex = state->PrevIndexes[lastIndex];
					UnsafeUtility.WriteArrayElement(
						state->Datas,
						enumerator.Index,
						UnsafeUtility.ReadArrayElement<T>(
							state->Datas,
							lastIndex));
					state->NextIndexes[enumerator.Index] = lastNextIndex;
					state->PrevIndexes[enumerator.Index] = lastPrevIndex;

					// If the last node wasn't the tail, set its next node's
					// previous index to where the last node was moved to
					if (lastNextIndex >= 0)
					{
						RequireIndexInBounds(lastNextIndex);
						state->PrevIndexes[lastNextIndex] = enumerator.Index;
					}

					// If the last node wasn't the head, set its previous node's
					// next index to where the last node was moved to
					if (lastPrevIndex >= 0)
					{
						RequireIndexInBounds(lastPrevIndex);
						state->NextIndexes[lastPrevIndex] = enumerator.Index;
					}

					// If the last node was the head, update the head index
					if (lastIndex == state->HeadIndex)
					{
						state->HeadIndex = enumerator.Index;
					}

					// If the last node was the tail, update the tail index
					if (lastIndex == state->TailIndex)
					{
						state->TailIndex = enumerator.Index;
					}

					// If the last node was the return, update the return
					if (lastIndex == retIndex)
					{
						retIndex = enumerator.Index;
					}
				}

				// Account for the removed node
				newLength = lastIndex;
			}

			// Set the new length
			state->Length = newLength;

			// Update safety ranges
			SetSafetyRange(newLength);

			// Invalidate all enumerators
			state->Version++;

			// Return the appropriate enumerator
			return new Enumerator(this, retIndex, state->Version);
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

			// Swap the data for the head with the data for the first element,
			// then the the node after the head that to the second element,
			// until the tail is reached
			for (int curIndex = state->HeadIndex, startIndex = 0;
				 curIndex >= 0;
				 curIndex = state->NextIndexes[curIndex], startIndex++)
			{
				// Never swap backwards. The part of the array up to startIndex
				// is already in order.
				if (curIndex > startIndex)
				{
					T startData = UnsafeUtility.ReadArrayElement<T>(
						state->Datas,
						startIndex);
					UnsafeUtility.WriteArrayElement(
						state->Datas,
						startIndex,
						UnsafeUtility.ReadArrayElement<T>(
							state->Datas,
							curIndex));
					UnsafeUtility.WriteArrayElement(
						state->Datas,
						curIndex,
						startData);
				}
			}

			int endIndex = state->Length - 1;

			// Set all the next pointers to point to the next index now that
			// the datas are sequential. The last one points to null.
			for (int i = 0; i <= endIndex; ++i)
			{
				state->NextIndexes[i] = i + 1;
			}
			state->NextIndexes[endIndex] = -1;

			// Set all the prev pointers to point to the prev index now that
			// the datas are sequential
			for (int i = 0; i <= endIndex; ++i)
			{
				state->PrevIndexes[i] = i - 1;
			}


			// The head is now at the beginning and the tail is now at the end
			state->HeadIndex = 0;
			state->TailIndex = endIndex;

			// Invalidate all enumerators
			state->Version++;
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
			if (array == null || array.Length < state->Length)
			{
				array = new T[state->Length];
			}

			// Copy all nodes to the array
			for (int i = state->HeadIndex, arrIndex = 0;
				i >= 0;
				i = state->NextIndexes[i], arrIndex++)
			{
				array[arrIndex] = UnsafeUtility.ReadArrayElement<T>(
					state->Datas,
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
			if (array == null || array.Length < state->Length)
			{
				array = new T[state->Length];
			}

			// Copy all nodes to the array
			for (int i = state->TailIndex, arrIndex = 0;
				i >= 0;
				i = state->PrevIndexes[i], arrIndex++)
			{
				array[arrIndex] = UnsafeUtility.ReadArrayElement<T>(
					state->Datas,
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

			// Copy the nodes' datas to the array
			while (length > 0)
			{
				// Copy the node's data
				RequireIndexInBounds(srcEnumerator.Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					state->Datas,
					srcEnumerator.Index);

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
				return state != null;
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

			if (state != null)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif

				// Free the state's contents
				UnsafeUtility.Free(state->Datas, state->Allocator);
				state->Datas = null;
				UnsafeUtility.Free(state->NextIndexes, state->Allocator);
				state->NextIndexes = null;
				UnsafeUtility.Free(state->PrevIndexes, state->Allocator);
				state->PrevIndexes = null;

				// Forget the list
				state->HeadIndex = -1;
				state->TailIndex = -1;
				state->Length = 0;
				state->Capacity = 0;

				// Invalidate all enumerators
				state->Version++;

				// Free the state itself
				UnsafeUtility.Free(state, state->Allocator);
				state = null;
			}
		}

		/// <summary>
		/// Copy all the nodes of a list to the end of the arrays. The list must
		/// already have sufficient capacity to hold all the nodes of the list
		/// to copy.
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
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();

			// Copy the list's node datas at the end. Copying with stride is
			// the same way NativeSlice<T> copies.
			int endIndex = state->Length;
			RequireRangeInBounds(
				endIndex,
				endIndex + list.Length - 1);
			int sizeofT = UnsafeUtility.SizeOf<T>();
			UnsafeUtility.MemCpyStride(
				(byte*)state->Datas + sizeofT * endIndex,
				sizeofT,
				list.state->Datas,
				sizeofT,
				sizeofT,
				list.state->Length);

			// Copy the list's next and prev pointers at the end, offset for
			// the new location at the end of this list.
			for (int i = 0; i < list.state->Length; ++i)
			{
				state->NextIndexes[endIndex + i]
					= list.state->NextIndexes[i] + endIndex;
			}
			for (int i = 0; i < list.state->Length; ++i)
			{
				state->PrevIndexes[endIndex + i]
					= list.state->PrevIndexes[i] + endIndex;
			}

			// Re-set the list's head and tail pointers since we offset them
			copiedHeadIndex = endIndex + list.state->HeadIndex;
			copiedTailIndex = endIndex + list.state->TailIndex;
			state->NextIndexes[copiedTailIndex] = -1;
			state->PrevIndexes[copiedHeadIndex] = -1;
		}

		/// <summary>
		/// Increase the capacity of the list
		/// </summary>
		/// 
		/// <param name="newCapacity">
		/// New capacity of the list
		/// </param>
		private void IncreaseCapacity(int newCapacity)
		{
			RequireValidState();
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Resize datas
			int sizeofT = UnsafeUtility.SizeOf<T>();
			void* newDatas = UnsafeUtility.Malloc(
				newCapacity * sizeofT,
				UnsafeUtility.AlignOf<T>(),
				state->Allocator);
			UnsafeUtility.MemCpyStride(
				newDatas,
				sizeofT,
				state->Datas,
				sizeofT,
				sizeofT,
				state->Length);
			UnsafeUtility.Free(state->Datas, state->Allocator);
			state->Datas = newDatas;

			// Resize nextIndexes
			int* newNextIndexes = (int*)UnsafeUtility.Malloc(
				newCapacity * sizeof(int),
				UnsafeUtility.AlignOf<int>(),
				state->Allocator);
			UnsafeUtility.MemCpy(
				newNextIndexes,
				state->NextIndexes,
				state->Length * sizeof(int));
			UnsafeUtility.Free(state->NextIndexes, state->Allocator);
			state->NextIndexes = newNextIndexes;

			// Resize prevIndexes
			int* newPrevIndexes = (int*)UnsafeUtility.Malloc(
				newCapacity * sizeof(int),
				UnsafeUtility.AlignOf<int>(),
				state->Allocator);
			UnsafeUtility.MemCpy(
				newPrevIndexes,
				state->PrevIndexes,
				state->Length * sizeof(int));
			UnsafeUtility.Free(state->PrevIndexes, state->Allocator);
			state->PrevIndexes = newPrevIndexes;

			state->Capacity = newCapacity;
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
			if (index < 0 || index > state->Capacity)
			{
				throw new IndexOutOfRangeException(
					"Index " + index + " is out of range of '" +
					state->Capacity + "' Capacity.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the safety check bounds don't encompass the
		/// full list.
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireFullListSafetyCheckBounds()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (m_MinIndex > 0 || m_MaxIndex >= m_Length)
			{
				throw new IndexOutOfRangeException(
					"This operation cannot be performed from a ParallelFor " +
					"job because access to the full list is required.");
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
			if (m_MinIndex < 0 || m_MaxIndex >= state->Capacity)
			{
				throw new IndexOutOfRangeException(
					"Range [" + startIndex + "..." + endIndex + "] is out of " +
					"range of '" + state->Capacity + "' Capacity.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the list isn't readable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
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
		private unsafe void RequireValidState()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (state == null)
			{
				throw new InvalidOperationException(
					"NativeList was either not initialized via a non-default " +
					"constructor or was used after calling Disposed()");
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
		// ReSharper disable once UnusedMember.Global - only used by debugger
		public T[] Items
		{
			get
			{
				return list.ToArray();
			}
		}
	}
}