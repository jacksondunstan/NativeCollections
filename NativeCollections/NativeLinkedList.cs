using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeCollections
{
	/// <summary>
	/// An iterator for <see cref="NativeLinkedList{T}"/>
	/// </summary>
	/// 
	/// <author>
	/// Jackson Dunstan, http://JacksonDunstan.com/articles/4865
	/// </author>
	/// 
	/// <license>
	/// MIT
	/// </license>
	public struct NativeLinkedListIterator
	{
		/// <summary>
		/// Index of the node
		/// </summary>
		internal readonly int Index;

		/// <summary>
		/// Version of the list that this iterator is valid for
		/// </summary>
		internal readonly int Version;
		
		/// <summary>
		/// Create the iterator for a particular node
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the node. Out-of-bounds values are OK.
		/// </param>
		/// 
		/// <param name="version">
		/// Version of the list that this iterator is valid for
		/// </param>
		internal NativeLinkedListIterator(int index, int version)
		{
			Index = index;
			Version = version;
		}
		
		/// <summary>
		/// Make an iterator that is invalid for all lists.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <returns>
		/// An iterator that is invalid for all lists
		/// </returns>
		public static NativeLinkedListIterator MakeInvalid()
		{
			return new NativeLinkedListIterator(-1, -1);
		}
		
		/// <summary>
		/// Check if two iterators refer to the same node. This has no meaning
		/// if either iterator is invalid or the iterators are for different
		/// lists.
		/// </summary>
		/// 
		/// <param name="itA">
		/// First iterator to compare
		/// </param>
		/// <param name="itB">
		/// Second iterator to compare
		/// </param>
		/// 
		/// <returns>
		/// If the given iterators refer to the same node
		/// </returns>
		public static bool operator==(
			NativeLinkedListIterator itA,
			NativeLinkedListIterator itB)
		{
			return itA.Index == itB.Index;
		}
		
		/// <summary>
		/// Check if two iterators refer to different nodes. This has no meaning
		/// if either iterator is invalid or the iterators are for different
		/// lists.
		/// </summary>
		/// 
		/// <param name="itA">
		/// First iterator to compare
		/// </param>
		/// 
		/// <param name="itB">
		/// Second iterator to compare
		/// </param>
		/// 
		/// <returns>
		/// If the given iterators refer to different nodes
		/// </returns>
		public static bool operator!=(
			NativeLinkedListIterator itA,
			NativeLinkedListIterator itB)
		{
			return itA.Index != itB.Index;
		}

		/// <summary>
		/// Check if this iterator refer to the same node as another iterator.
		/// This has no meaning if either iterator is invalid or the iterators
		/// are for different lists.
		/// </summary>
		/// 
		/// <param name="obj">
		/// Iterator to compare with
		/// </param>
		/// 
		/// <returns>
		/// If the given iterator refers to the same node as this iterator and
		/// is of the same type.
		/// </returns>
		public override bool Equals(object obj)
		{
			return obj is NativeLinkedListIterator
				&& ((NativeLinkedListIterator)obj).Index == Index;
		}

		/// <summary>
		/// Get a hash code for this iterator
		/// </summary>
		/// 
		/// <returns>
		/// A hash code for this iterator
		/// </returns>
		public override int GetHashCode()
		{
			return Index;
		}
	}
	
	/// <summary>
	/// A doubly-linked list native collection.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of nodes in the list. Must be blittable.
	/// </typeparam>
	///
	/// <author>
	/// Jackson Dunstan, http://JacksonDunstan.com/articles/4865
	/// </author>
	/// 
	/// <license>
	/// MIT
	/// </license>
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("Count = {Count}. Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(NativeLinkedListDebugView<>))]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativeLinkedList<T> : IDisposable
		where T : struct
	{
		// Each node's data. Indices correspond with nextIndexes.
		private NativeArray<T> datas;
		
		// Each node's next node index. Indices correspond with datas.
		private NativeArray<int> nextIndexes;
		
		// Each node's previous node index. Indices correspond with datas.
		private NativeArray<int> prevIndexes;
		
		// Allocator used to create the backing array
		private readonly Allocator allocator;
		
		// Index of the first node in the list or -1 if there are no nodes in
		// the list
		private int headIndex;
	
		// Index of the last node in the list or -1 if there are no nodes in
		// the list
		private int tailIndex;
		
		// Number of nodes contained
		private int count;

		// Number of nodes that can be contained
		private int capacity;

		// Version of iterators that are valid for this list. This starts at 1
		// and increases by one with each change that invalidates iterators.
		private int version;
		
		// These are all required when checks are enabled
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
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="capacity">
		/// Initial capacity. This will be doubled if too many nodes are added.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with
		/// </param>
		public NativeLinkedList(int capacity, Allocator allocator)
		{
			// Create the backing arrays. There's no need to clear them since we
			// make no assumptions about the contents anyways.
			datas = new NativeArray<T>(
				capacity,
				allocator,
				NativeArrayOptions.UninitializedMemory);
			nextIndexes = new NativeArray<int>(
				capacity,
				allocator,
				NativeArrayOptions.UninitializedMemory);
			prevIndexes = new NativeArray<int>(
				capacity,
				allocator,
				NativeArrayOptions.UninitializedMemory);
			this.allocator = allocator;

			// Initially empty with the given capacity
			count = 0;
			this.capacity = capacity;
			headIndex = -1;
			tailIndex = -1;

			// Version starts at one so that the default (0) is never used
			version = 1;
	
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
		/// to its <see cref="count"/>.
		///
		/// This operation is O(1).
		/// </summary>
		public int Capacity
		{
			get
			{
				return capacity;
			}
		}

		/// <summary>
		/// Get the number of nodes currently in the list. This is always less
		/// than or equal to the <see cref="Capacity"/>.
		///
		/// This operation is O(1).
		/// </summary>
		// ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
		public int Count
		{
			get
			{
				return count;
			}
		}

		/// <summary>
		/// Get an iterator to the head of the list or an invalid iterator if
		/// the list is empty.
		/// </summary>
		/// 
		/// <returns>
		/// An iterator to the head of the list or an invalid iterator if the
		/// list is empty.
		///
		/// This operation is O(1).
		/// </returns>
		public NativeLinkedListIterator GetHead()
		{
			return new NativeLinkedListIterator(headIndex, version);
		}

		/// <summary>
		/// Get an iterator to the tailI of the list or an invalid iterator if
		/// the list is empty.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <returns>
		/// An iterator to the tailI of the list or an invalid iterator if the
		/// list is empty.
		/// </returns>
		public NativeLinkedListIterator GetTail()
		{
			return new NativeLinkedListIterator(tailIndex, version);
		}
		
		/// <summary>
		/// Get an iterator to the next node or an invalid iterator if the
		/// given iterator is at the tail of the list or is invalid.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to get the next node's iterator for
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the next node or an invalid iterator if the
		/// given iterator is at the tail of the list or is invalid.
		/// </returns>
		public NativeLinkedListIterator GetNext(NativeLinkedListIterator it)
		{
			RequireReadAccess();
			
			if (IsValid(it))
			{
				FailIfIndexOutOfCheckBounds(it.Index);
				int nextIndex = nextIndexes[it.Index];
				return new NativeLinkedListIterator(nextIndex, version);
			}
	
			return NativeLinkedListIterator.MakeInvalid();
		}
		
		/// <summary>
		/// Get an iterator to the previous node or an invalid iterator if the
		/// given iterator is at the head of the list or is invalid.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to get the previous node's iterator for
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the previous node or an invalid iterator if the
		/// given iterator is at the head of the list or is invalid.
		/// </returns>
		public NativeLinkedListIterator GetPrev(NativeLinkedListIterator it)
		{
			RequireReadAccess();
			
			if (IsValid(it))
			{
				FailIfIndexOutOfCheckBounds(it.Index);
				int prevIndex = prevIndexes[it.Index];
				return new NativeLinkedListIterator(prevIndex, version);
			}
			
			return NativeLinkedListIterator.MakeInvalid();
		}
		
		/// <summary>
		/// Get a node's data
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to get the data for. If invalid, this returns default(T).
		/// </param>
		/// 
		/// <returns>
		/// The data for the given iterator or default(T) if the given iterator
		/// is invalid.
		/// </returns>
		public T GetData(NativeLinkedListIterator it)
		{
			RequireReadAccess();
			FailIfIndexOutOfCheckBounds(it.Index);
			return IsValid(it) ? datas[it.Index] : default(T);
		}
		
		/// <summary>
		/// Set a node's data
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node whose data should be set. If invalid, this
		/// function has no effect.
		/// </param>
		/// 
		/// <param name="data">
		/// Data to set for the node
		/// </param>
		[WriteAccessRequired]
		public void SetData(NativeLinkedListIterator it, T data)
		{
			RequireWriteAccess();
			FailIfIndexOutOfCheckBounds(it.Index);
			if (IsValid(it))
			{
				datas[it.Index] = data;
			}
		}
		
		/// <summary>
		/// Check if an iterator is valid
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to check
		/// </param>
		/// 
		/// <returns>
		/// If the given iterator is valid
		/// </returns>
		public bool IsValid(NativeLinkedListIterator it)
		{
			return it.Index >= 0 && it.Index < count && it.Version == version;
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
		/// zero and less than <see cref="Count"/>.
		/// </param>
		public T this[int index]
		{
			get
			{
				RequireReadAccess();
				FailIfIndexOutOfCheckBounds(index);
				return datas[index];
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				FailIfIndexOutOfCheckBounds(index);
				datas[index] = value;
			}
		}

		/// <summary>
		/// Add an node to the end of the list. If the list is full, it will be
		/// automatically resized by allocating new unmanaged memory with double
		/// the <see cref="Capacity"/> and copying over all existing nodes. Any
		/// existing iterators will _not_ be invalidated.
		///
		/// This operation is O(1) when the list isn't full (i.e.
		/// <see cref="Count"/> == <see cref="Capacity"/>) and O(N) when it is.
		/// </summary>
		/// 
		/// <param name="value">
		/// Node to add
		/// </param>
		///
		/// <returns>
		/// An iterator to the added node
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator PushBack(T value)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// The list is full. Resize.
			if (count == capacity)
			{
				IncreaseCapacity(count * 2);
			}
	 
			// Insert at the end
			int insertIndex = count;
			datas[insertIndex] = value;
			nextIndexes[insertIndex] = -1;
			prevIndexes[insertIndex] = tailIndex;
			
			// The list was empty, so this is the head and the tail now
			if (headIndex < 0)
			{
				headIndex = insertIndex;
				tailIndex = insertIndex;
			}
			// The list wasn't empty, so point the tail at the added node and
			// point the added node at the tail
			else
			{
				nextIndexes[tailIndex] = insertIndex;
				prevIndexes[insertIndex] = tailIndex;
			}
			
			// The added node is now the tail
			tailIndex = insertIndex;

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = insertIndex + 1;
			m_MaxIndex = insertIndex;
#endif
	 
			// Count the newly-added node
			count = insertIndex + 1;
			
			return new NativeLinkedListIterator(insertIndex, version);
		}

		/// <summary>
		/// Add an node to the end of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with the greater of double the <see cref="Capacity"/> and the
		/// current <see cref="Capacity"/> plus the <see cref="Count"/> of the
		/// list and copying over all existing nodes. Any existing iterators
		/// will _not_ be invalidated.
		///
		/// This operation is O(1) when the list has enough capacity to add all
		/// the nodes in the given list and O(N) when it doesn't.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to add the nodes from
		/// </param>
		///
		/// <returns>
		/// An iterator to the first added node or the tail if the given list is
		/// empty and this list isn't or an invalid iterator if both lists are
		/// empty.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator PushBack(NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// There's nothing to add when given an empty list.
			// Return an iterator to the tail or an invalid iterator if this
			// list is also empty.
			if (list.count == 0)
			{
				return new NativeLinkedListIterator(tailIndex, version);
			}

			// The list is full. Resize.
			if (count + list.count > capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(Math.Max(count * 2, count + list.count));
			}

			// Insert the list at the end
			int endIndex = count;
			int insertedHeadIndex;
			int insertedTailIndex;
			InsertAtEnd(list, out insertedHeadIndex, out insertedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (headIndex < 0)
			{
				headIndex = list.headIndex;
				tailIndex = list.tailIndex;
			}
			// The list wasn't empty, so point the tail at the head of the
			// pushed list and the head of the pushed list at the tail
			else
			{
				nextIndexes[tailIndex] = insertedHeadIndex;
				prevIndexes[insertedHeadIndex] = tailIndex;
			}

			// The added list's tail is now the tail
			tailIndex = insertedTailIndex;

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + list.count;
			m_MaxIndex = endIndex + list.count - 1;
#endif

			// Count the newly-added list's nodes
			count = endIndex + list.count;

			// Return an iterator to where we inserted the list's head node
			return new NativeLinkedListIterator(insertedHeadIndex, version);
		}
		
		/// <summary>
		/// Add an node to the beginning of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with double the <see cref="Capacity"/> and copying over all existing
		/// nodes. Any existing iterators will _not_ be invalidated.
		///
		/// This operation is O(1) when the list isn't full (i.e.
		/// <see cref="Count"/> == <see cref="Capacity"/>) and O(N) when it is.
		/// </summary>
		/// 
		/// <param name="value">
		/// Node to add
		/// </param>
		///
		/// <returns>
		/// An iterator to the added node
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator PushFront(T value)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// The list is full. Resize.
			if (count == capacity)
			{
				IncreaseCapacity(count * 2);
			}
	 
			// Insert at the end
			int insertIndex = count;
			datas[insertIndex] = value;
			nextIndexes[insertIndex] = headIndex;
			prevIndexes[insertIndex] = -1;
			
			// The list was empty, so this is the head and the tail now
			if (headIndex < 0)
			{
				headIndex = insertIndex;
				tailIndex = insertIndex;
			}
			// The list wasn't empty, so point the head at the added node and
			// point the added node at the head
			else
			{
				prevIndexes[headIndex] = insertIndex;
				nextIndexes[insertIndex] = headIndex;
			}
			
			// The added node is now the head
			headIndex = insertIndex;
			
			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = insertIndex + 1;
			m_MaxIndex = insertIndex;
#endif
	 
			// Count the newly-added node
			count = insertIndex + 1;
			
			return new NativeLinkedListIterator(insertIndex, version);
		}

		/// <summary>
		/// Add an node to the beginning of the list. If the list is full, it
		/// will be automatically resized by allocating new unmanaged memory
		/// with the greater of double the <see cref="Capacity"/> and the
		/// current <see cref="Capacity"/> plus the <see cref="Count"/> of the
		/// list and copying over all existing nodes. Any existing iterators
		/// will _not_ be invalidated.
		///
		/// This operation is O(1) when the list has enough capacity to add all
		/// the nodes in the given list and O(N) when it doesn't.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to add the nodes from
		/// </param>
		///
		/// <returns>
		/// An iterator to the last added node or the head if the given list is
		/// empty and this list isn't or an invalid iterator if both lists are
		/// empty.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator PushFront(NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// There's nothing to add when given an empty list.
			// Return an iterator to the head or an invalid iterator if this
			// list is also empty.
			if (list.count == 0)
			{
				return new NativeLinkedListIterator(headIndex, version);
			}

			// The list is full. Resize.
			if (count + list.count > capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(Math.Max(count * 2, count + list.count));
			}

			// Insert the list at the end
			int endIndex = count;
			int insertedHeadIndex;
			int insertedTailIndex;
			InsertAtEnd(list, out insertedHeadIndex, out insertedTailIndex);

			// The list was empty, so use the pushed list's head and tail
			if (headIndex < 0)
			{
				headIndex = list.headIndex;
				tailIndex = list.tailIndex;
			}
			// The list wasn't empty, so point the head at the tail of the
			// pushed list and the tail of the pushed list at the head
			else
			{
				prevIndexes[headIndex] = insertedTailIndex;
				nextIndexes[insertedTailIndex] = headIndex;
			}

			// The added list's head is now the head
			headIndex = insertedHeadIndex;

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + list.count;
			m_MaxIndex = endIndex + list.count - 1;
#endif

			// Count the newly-added list's nodes
			count = endIndex + list.count;

			// Return an iterator to where we inserted the list's tail node
			return new NativeLinkedListIterator(insertedTailIndex, version);
		}

		/// <summary>
		/// Insert a node after the node referred to by the given iterator. This
		/// doesn't invalidate any iterators.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node to insert after. If invalid, this function has
		/// no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the inserted node or an invalid iterator if the given
		/// iterator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator InsertAfter(
			NativeLinkedListIterator it,
			T value)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Can't insert after an invalid iterator
			if (!IsValid(it))
			{
				return NativeLinkedListIterator.MakeInvalid();
			}

			// The list is full. Resize.
			if (count == capacity)
			{
				IncreaseCapacity(count * 2);
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
			int endIndex = count;
			datas[endIndex] = value;

			// Point the end node's next to the next node
			int insertNextIndex = nextIndexes[it.Index];
			nextIndexes[endIndex] = insertNextIndex;

			// Point the end node's previous to the insert node
			int insertPrevIndex = prevIndexes[it.Index];
			prevIndexes[endIndex] = it.Index;

			// Point the insert node's next to the end node
			nextIndexes[it.Index] = endIndex;

			// Point the next node's prev to the end node
			if (insertNextIndex >= 0)
			{
				prevIndexes[insertNextIndex] = endIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the end node where we moved it
			else
			{
				tailIndex = endIndex;
			}

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + 1;
			m_MaxIndex = endIndex;
#endif

			// Count the newly-added node
			count = endIndex + 1;

			// The inserted node 
			return new NativeLinkedListIterator(endIndex, version);
		}

		/// <summary>
		/// Insert the nodes of a given list after the node referred to by the
		/// given iterator. This doesn't invalidate any iterators.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node to insert after. If invalid, this function has
		/// no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the inserted head node or the given iterator if the
		/// given list is empty or an invalid iterator if the given iterator is
		/// invalid.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator InsertAfter(
			NativeLinkedListIterator it,
			NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Can't insert after an invalid iterator
			if (!IsValid(it))
			{
				return NativeLinkedListIterator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an iterator to the head or an invalid iterator if this
			// list is also empty.
			if (list.count == 0)
			{
				return new NativeLinkedListIterator(headIndex, version);
			}

			// The list is full. Resize.
			if (count + list.count > capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(Math.Max(count * 2, count + list.count));
			}

			// Insert the list at the end
			int endIndex = count;
			int insertedHeadIndex;
			int insertedTailIndex;
			InsertAtEnd(list, out insertedHeadIndex, out insertedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = nextIndexes[it.Index];
			nextIndexes[insertedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			int insertPrevIndex = prevIndexes[it.Index];
			prevIndexes[insertedHeadIndex] = it.Index;

			// Point the insert node's next to the inserted head node
			nextIndexes[it.Index] = insertedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				prevIndexes[insertNextIndex] = insertedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				tailIndex = insertedTailIndex;
			}

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + list.count;
			m_MaxIndex = endIndex + list.count;
#endif

			// Count the newly-added nodes
			count = endIndex + list.count;

			// The first inserted node 
			return new NativeLinkedListIterator(insertedHeadIndex, version);
		}

		/// <summary>
		/// Insert a node before the node referred to by the given iterator.
		/// This doesn't invalidate any iterators.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node to insert before. If invalid, this function has
		/// no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the inserted node or an invalid iterator if the given
		/// iterator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator InsertBefore(
			NativeLinkedListIterator it,
			T value)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Can't insert before an invalid iterator
			if (!IsValid(it))
			{
				return NativeLinkedListIterator.MakeInvalid();
			}

			// The list is full. Resize.
			if (count == capacity)
			{
				IncreaseCapacity(count * 2);
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
			int endIndex = count;
			datas[endIndex] = value;

			// Point the end node's next to the insert node
			int insertNextIndex = nextIndexes[it.Index];
			nextIndexes[endIndex] = it.Index;

			// Point the end node's previous to the prev node
			int insertPrevIndex = prevIndexes[it.Index];
			prevIndexes[endIndex] = insertPrevIndex;

			// Point the insert node's prev to the end node
			prevIndexes[it.Index] = endIndex;

			// Point the prev node's next to the end node
			if (insertPrevIndex >= 0)
			{
				nextIndexes[insertPrevIndex] = endIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the end node where we moved it
			else
			{
				headIndex = endIndex;
			}

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + 1;
			m_MaxIndex = endIndex;
#endif

			// Count the newly-added node
			count = endIndex + 1;

			// The inserted node 
			return new NativeLinkedListIterator(endIndex, version);
		}

		/// <summary>
		/// Insert the nodes of a given list before the node referred to by the
		/// given iterator. This doesn't invalidate any iterators.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node to insert before. If invalid, this function has
		/// no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert
		/// </param>
		/// 
		/// <returns>
		/// An iterator to the inserted tail node or the given iterator if the
		/// given list is empty or an invalid iterator if the given iterator is
		/// invalid.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator InsertBefore(
			NativeLinkedListIterator it,
			NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Can't insert before an invalid iterator
			if (!IsValid(it))
			{
				return NativeLinkedListIterator.MakeInvalid();
			}

			// There's nothing to add when given an empty list.
			// Return an iterator to the head or an invalid iterator if this
			// list is also empty.
			if (list.count == 0)
			{
				return new NativeLinkedListIterator(headIndex, version);
			}

			// The list is full. Resize.
			if (count + list.count > capacity)
			{
				// We need enough capacity to store the whole list and want to
				// at least double our capacity.
				IncreaseCapacity(Math.Max(count * 2, count + list.count));
			}

			// Insert the list at the end
			int endIndex = count;
			int insertedHeadIndex;
			int insertedTailIndex;
			InsertAtEnd(list, out insertedHeadIndex, out insertedTailIndex);

			// Point the inserted tail node's next to the insert node
			int insertNextIndex = nextIndexes[it.Index];
			nextIndexes[insertedTailIndex] = it.Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = prevIndexes[it.Index];
			prevIndexes[insertedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			prevIndexes[it.Index] = insertedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				nextIndexes[insertPrevIndex] = insertedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				headIndex = insertedHeadIndex;
			}

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = endIndex + list.count;
			m_MaxIndex = endIndex + list.count;
#endif

			// Count the newly-added nodes
			count = endIndex + list.count;

			// The inserted tail node 
			return new NativeLinkedListIterator(insertedTailIndex, version);
		}

		/// <summary>
		/// Remove a node. This invalidates all iterators, including the given
		/// iterator, if the given iterator is valid. Note that the node's value
		/// is not cleared since it's blittable and therefore can't hold any
		/// managed reference that could be garbage-collected.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node to remove. Is invalid, this function has no
		/// effect.
		/// <see cref="IsValid"/>.
		/// </param>
		///
		/// <returns>
		/// An invalid iterator if this is the only node in the list or the
		/// given iterator is invalid, the next node if this is the head of the
		/// list, otherwise the previous node.
		/// </returns>
		[WriteAccessRequired]
		public NativeLinkedListIterator Remove(NativeLinkedListIterator it)
		{
			RequireReadAccess();
			RequireWriteAccess();
			
			// Can't remove invalid iterators
			if (!IsValid(it))
			{
				return NativeLinkedListIterator.MakeInvalid();
			}
			
			int newCount;
			int retIndex;
			
			// Node to remove is the only node
			if (count == 1)
			{
				headIndex = -1;
				tailIndex = -1;
				newCount = 0;
				retIndex = -1;
			}
			// There are at least two nodes in the list
			else
			{
				FailIfIndexOutOfCheckBounds(it.Index);

				// Node to remove is the head
				if (it.Index == headIndex)
				{
					// Move the head pointer forward one
					int headNextIndex = nextIndexes[it.Index];
					headIndex = headNextIndex;
					
					// Make the new head's previous node be invalid
					FailIfIndexOutOfCheckBounds(headNextIndex);
					prevIndexes[headNextIndex] = -1;
					
					// Return an iterator to the new head
					retIndex = headNextIndex;
				}
				// Node to remove is the tail
				else if (it.Index == tailIndex)
				{
					// Move the tail pointer back one
					int tailPrevIndex = prevIndexes[it.Index];
					tailIndex = tailPrevIndex;

					// Make the new tail's next node be invalid
					FailIfIndexOutOfCheckBounds(tailPrevIndex);
					nextIndexes[tailPrevIndex] = -1;
					
					// Return an iterator to the new tail
					retIndex = tailPrevIndex;
				}
				// Node to remove is an interior node.
				else
				{
					// Link the previous node to the next node and the next node
					// to the previous node
					int prevIndex = prevIndexes[it.Index];
					int nextIndex = nextIndexes[it.Index];
					FailIfIndexOutOfCheckBounds(prevIndex);
					FailIfIndexOutOfCheckBounds(nextIndex);
					nextIndexes[prevIndex] = nextIndex;
					prevIndexes[nextIndex] = prevIndex;
					
					// Return an iterator to the previous node
					retIndex = prevIndex;
				}
				
				// Move the last node to where the node was removed from
				int lastIndex = count - 1;
				if (it.Index != lastIndex)
				{
					// Copy the last node to where the removed node was
					FailIfIndexOutOfCheckBounds(lastIndex);
					int lastNextIndex = nextIndexes[lastIndex];
					int lastPrevIndex = prevIndexes[lastIndex];
					datas[it.Index] = datas[lastIndex];
					nextIndexes[it.Index] = lastNextIndex;
					prevIndexes[it.Index] = lastPrevIndex;
					
					// If the last node wasn't the tail, set its next node's
					// previous index to where the last node was moved to
					if (lastNextIndex >= 0)
					{
						FailIfIndexOutOfCheckBounds(lastNextIndex);
						prevIndexes[lastNextIndex] = it.Index;
					}
					
					// If the last node wasn't the head, set its previous node's
					// next index to where the last node was moved to
					if (lastPrevIndex >= 0)
					{
						FailIfIndexOutOfCheckBounds(lastPrevIndex);
						nextIndexes[lastPrevIndex] = it.Index;
					}

					// If the last node was the head, update the head index
					if (lastIndex == headIndex)
					{
						headIndex = it.Index;
					}
					
					// If the last node was the tail, update the tail index
					if (lastIndex == tailIndex)
					{
						tailIndex = it.Index;
					}
					
					// If the last node was the return, update the return
					if (lastIndex == retIndex)
					{
						retIndex = it.Index;
					}
				}

				// Account for the removed node
				newCount = lastIndex;
			}

			// Set the new count
			count = newCount;

			// Update safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = newCount;
			m_MaxIndex = newCount - 1;
#endif

			// Invalidate all iterators
			version++;
			
			// Return the appropriate iterator
			return new NativeLinkedListIterator(retIndex, version);
		}

		/// <summary>
		/// Reorder the list such that its order is preserved but the nodes are
		/// laid out sequentially in memory. This allows for indexing into the
		/// list after a call to <see cref="Remove"/>. This invalidates all
		/// iterators.
		///
		/// This operation is O(N).
		/// </summary>
		[WriteAccessRequired]
		public void SortNodeMemoryAddresses()
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Swap the data for the head with the data for the first element,
			// then the the node after the head that to the second element, until
			// the tail is reached
			for (int curIndex = headIndex, startIndex = 0;
			     curIndex >= 0;
			     curIndex = nextIndexes[curIndex], startIndex++)
			{
				// Never swap backwards. The part of the array up to startIndex
				// is already in order.
				if (curIndex > startIndex)
				{
					T startData = datas[startIndex];
					datas[startIndex] = datas[curIndex];
					datas[curIndex] = startData;
				}
			}

			int endIndex = count - 1;

			// Set all the next pointers to point to the next index now that
			// the datas are sequential. The last one points to null.
			for (int i = 0; i <= endIndex; ++i)
			{
				nextIndexes[i] = i + 1;
			}
			nextIndexes[endIndex] = -1;

			// Set all the prev pointers to point to the prev index now that
			// the datas are sequential
			for (int i = 0; i <= endIndex; ++i)
			{
				prevIndexes[i] = i - 1;
			}


			// The head is now at the beginning and the tail is now at the end
			headIndex = 0;
			tailIndex = endIndex;

			// Invalidate all iterators
			version++;
		}

		/// <summary>
		/// Copy all nodes to a managed array, which is optionally allocated.
		///
		/// This operation is O(N).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If null or less than <see cref="Count"/>,
		/// a new array will be allocated.
		/// </param>
		/// 
		/// <returns>
		/// A managed array with all of the list's nodes. This is either the
		/// given array if it was non-null and at least as long as
		/// <see cref="Count"/> or a newly-allocated array with length equal to
		/// <see cref="Count"/> otherwise.
		/// </returns>
		public T[] ToArray(T[] array = null)
		{
			RequireReadAccess();

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
			if (array == null || array.Length < count)
			{
				array = new T[count];
			}
			
			// Copy all nodes to the array
			for (int i = headIndex, arrIndex = 0;
				i >= 0;
				i = nextIndexes[i], arrIndex++)
			{
				array[arrIndex] = datas[i];
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
		/// Array to copy nodes to. If null or less than <see cref="Count"/>,
		/// a new array will be allocated.
		/// </param>
		/// 
		/// <returns>
		/// A managed array with all of the list's nodes. This is either the
		/// given array if it was non-null and at least as long as
		/// <see cref="Count"/> or a newly-allocated array with length equal to
		/// <see cref="Count"/> otherwise.
		/// </returns>
		public T[] ToArrayReverse(T[] array = null)
		{
			RequireReadAccess();

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
			if (array == null || array.Length < count)
			{
				array = new T[count];
			}

			// Copy all nodes to the array
			for (int i = tailIndex, arrIndex = 0;
				i >= 0;
				i = prevIndexes[i], arrIndex++)
			{
				array[arrIndex] = datas[i];
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
		/// <param name="srcIndex">
		/// Index to start copying from
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy
		/// </param>
		public void CopyToNativeArray(
			NativeArray<T> array,
			int srcIndex = 0,
			int destIndex = 0,
			int length = -1)
		{
			RequireReadAccess();

			// Go to the start of the source
			int i = headIndex;
			while (srcIndex > 0)
			{
				FailIfIndexOutOfCheckBounds(i);
				i = nextIndexes[i];
				srcIndex--;
			}

			// Copy all nodes to the array
			while (length > 0)
			{
				// Copy the node
				array[destIndex] = datas[i];

				// Go to the next node
				FailIfIndexOutOfCheckBounds(i);
				i = nextIndexes[i];

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
				return datas.IsCreated;
			}
		}
	 
		/// <summary>
		/// Release the list's unmanaged memory. Do not use it after this.
		///
		/// This operation is O(1).
		/// </summary>
		public void Dispose()
		{
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
	#endif
			datas.Dispose();
			nextIndexes.Dispose();
			prevIndexes.Dispose();

			// Forget the list
			headIndex = -1;
			tailIndex = -1;
			count = 0;
			capacity = 0;

			// Invalidate all iterators
			version++;
		}

		private void InsertAtEnd(
			NativeLinkedList<T> list,
			out int insertedHeadIndex,
			out int insertedTailIndex)
		{
			// Insert the list's node datas at the end. Copying datas with
			// slices is more efficient due to the use of UnsafeUtility.MemCpy.
			int endIndex = count;
			new NativeSlice<T>(datas, endIndex, list.count).CopyFrom(
				new NativeSlice<T>(list.datas, 0, list.count));

			// Insert the list's next and prev pointers at the end, offset for
			// the new location at the end of this list.
			for (int i = 0; i < list.count; ++i)
			{
				nextIndexes[endIndex + i] = list.nextIndexes[i] + endIndex;
			}
			for (int i = 0; i < list.count; ++i)
			{
				prevIndexes[endIndex + i] = list.prevIndexes[i] + endIndex;
			}

			// Re-set the list's head and tail pointers since we offset them
			insertedHeadIndex = endIndex + list.headIndex;
			insertedTailIndex = endIndex + list.tailIndex;
			nextIndexes[insertedTailIndex] = -1;
			prevIndexes[insertedHeadIndex] = -1;
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
			// Resize datas
			NativeArray<T> newDatas = new NativeArray<T>(
				newCapacity,
				allocator);
			UnsafeUtility.MemCpy(
				newDatas.GetUnsafePtr(),
				datas.GetUnsafePtr(),
				capacity * (long)UnsafeUtility.SizeOf<T>());
			datas.Dispose();
			datas = newDatas;

			// Resize nextIndexes
			NativeArray<int> newNextIndexes = new NativeArray<int>(
				newCapacity,
				allocator);
			UnsafeUtility.MemCpy(
				newNextIndexes.GetUnsafePtr(),
				nextIndexes.GetUnsafePtr(),
				capacity * (long)sizeof(int));
			nextIndexes.Dispose();
			nextIndexes = newNextIndexes;

			// Resize prevIndexes
			NativeArray<int> newPrevIndexes = new NativeArray<int>(
				newCapacity,
				allocator);
			UnsafeUtility.MemCpy(
				newPrevIndexes.GetUnsafePtr(),
				prevIndexes.GetUnsafePtr(),
				capacity * (long)sizeof(int));
			prevIndexes.Dispose();
			prevIndexes = newPrevIndexes;

			capacity = newCapacity;
		}

		// Throw an error if the given index isn't in the range
		// [m_MinIndex, m_MaxIndex]
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void FailIfIndexOutOfCheckBounds(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < m_MinIndex || index > m_MaxIndex)
			{
				FailOutOfRangeError(index);
			}
#endif
		}

		// Throw an appropriate exception when safety checks are enabled
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void FailOutOfRangeError(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (index < m_Length
				&& (m_MinIndex != 0 || m_MaxIndex != m_Length - 1))
			{
				throw new IndexOutOfRangeException(
					string.Format("Index {0} is out of restricted ParallelFor ", index) +
					string.Format("range [{0}...{1}] in ", m_MinIndex, m_MaxIndex) +
					"ReadWriteBuffer.\n ReadWriteBuffers are restricted to " +
					"only read & write the node at the job index. You can " +
					"use double buffering strategies to avoid race " +
					"conditions due to reading & writing in parallel to the " +
					"same nodes from a job.");
			}

			throw new IndexOutOfRangeException(
				string.Format(
					"Index {0} is out of range of '{1}' Length.",
					index,
					m_Length));
#endif
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private unsafe void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private unsafe void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
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
		// List to view
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