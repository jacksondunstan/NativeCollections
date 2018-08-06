using System;
using System.Diagnostics;
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
	/// </license.
	public struct NativeLinkedListIterator
	{
		/// <summary>
		/// Index of the node
		/// </summary>
		internal readonly int Index;
		
		/// <summary>
		/// Create the iterator for a particular node
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the node. Out-of-bounds values are OK.
		/// </param>
		internal NativeLinkedListIterator(int index)
		{
			Index = index;
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
			return new NativeLinkedListIterator(-1);
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
		/// <param name="other">
		/// Iterator to compare with
		/// </param>
		/// 
		/// <returns>
		/// If the given iterator refers to the same node as this iterator and
		/// is of the same type.
		/// </returns>
		public override bool Equals(object other)
		{
			return other is NativeLinkedListIterator
				&& ((NativeLinkedListIterator)other).Index == Index;
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
	/// </license.
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("count = {count}. Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(NativeLinkedListDebugView<>))]
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
		
		// These are all required when checks are enabled
		// They must have these exact types, names, and attributes
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
			// Create the backing arrays
			datas = new NativeArray<T>(capacity, allocator);
			nextIndexes = new NativeArray<int>(capacity, allocator);
			prevIndexes = new NativeArray<int>(capacity, allocator);
			count = 0;
			this.allocator = allocator;
			m_Length = capacity;
			headIndex = -1;
			tailIndex = -1;
	
			// Initialize fields for safety checks
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
				return m_Length;
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
			return new NativeLinkedListIterator(headIndex);
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
			return new NativeLinkedListIterator(tailIndex);
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
			// Must be able to read from the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			
			if (IsValid(it))
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				FailIfIndexOutOfCheckBounds(it.Index);
#endif
				int nextIndex = nextIndexes[it.Index];
				return new NativeLinkedListIterator(nextIndex);
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
			// Must be able to read from the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			
			if (IsValid(it))
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				FailIfIndexOutOfCheckBounds(it.Index);
#endif
				int prevIndex = prevIndexes[it.Index];
				return new NativeLinkedListIterator(prevIndex);
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
		/// Iterator to get the data for
		/// </param>
		/// 
		/// <returns>
		/// The data for the given iterator
		/// </returns>
		public T GetData(NativeLinkedListIterator it)
		{
			// Must be able to read from the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			FailIfIndexOutOfCheckBounds(it.Index);
#endif
			
			return datas[it.Index];
		}
		
		/// <summary>
		/// Set a node's data
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="it">
		/// Iterator to the node whose data should be set.
		/// </param>
		/// 
		/// <param name="data">
		/// Data to set for the node
		/// </param>
		public void SetData(NativeLinkedListIterator it, T data)
		{
			// Must be able to write to the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			FailIfIndexOutOfCheckBounds(it.Index);
#endif
			
			datas[it.Index] = data;
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
			return it.Index >= 0 && it.Index < count;
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
		public NativeLinkedListIterator PushBack(T value)
		{
			// Must be able to read and write to the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			FailIfCalledFromParallelFor("PushBack");
	#endif
			
			// The list is full. Resize.
			if (count == m_Length)
			{
				int newLength = count * 2;
				
				// Resize datas
				NativeArray<T> newDatas = new NativeArray<T>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newDatas.GetUnsafePtr(),
					datas.GetUnsafePtr(),
					m_Length * (long)UnsafeUtility.SizeOf<T>());
				datas.Dispose();
				datas = newDatas;
				
				// Resize nextIndexes
				NativeArray<int> newNextIndexes = new NativeArray<int>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newNextIndexes.GetUnsafePtr(),
					nextIndexes.GetUnsafePtr(),
					m_Length * (long)sizeof(int));
				nextIndexes.Dispose();
				nextIndexes = newNextIndexes;
				
				// Resize prevIndexes
				NativeArray<int> newPrevIndexes = new NativeArray<int>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newPrevIndexes.GetUnsafePtr(),
					prevIndexes.GetUnsafePtr(),
					m_Length * (long)sizeof(int));
				prevIndexes.Dispose();
				prevIndexes = newPrevIndexes;
				
				m_Length = newLength;
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
			
			// Mark the new maximum index that can be read
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MaxIndex = insertIndex;
	#endif
	 
			// Count the newly-added node
			count = insertIndex + 1;
			
			return new NativeLinkedListIterator(insertIndex);
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
		public NativeLinkedListIterator PushFront(T value)
		{
			// Must be able to read and write to the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			FailIfCalledFromParallelFor("PushFront");
#endif
			
			// The list is full. Resize.
			if (count == m_Length)
			{
				int newLength = count * 2;
				
				// Resize datas
				NativeArray<T> newDatas = new NativeArray<T>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newDatas.GetUnsafePtr(),
					datas.GetUnsafePtr(),
					m_Length * (long)UnsafeUtility.SizeOf<T>());
				datas.Dispose();
				datas = newDatas;
				
				// Resize nextIndexes
				NativeArray<int> newNextIndexes = new NativeArray<int>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newNextIndexes.GetUnsafePtr(),
					nextIndexes.GetUnsafePtr(),
					m_Length * (long)sizeof(int));
				nextIndexes.Dispose();
				nextIndexes = newNextIndexes;
				
				// Resize prevIndexes
				NativeArray<int> newPrevIndexes = new NativeArray<int>(
					newLength,
					allocator);
				UnsafeUtility.MemCpy(
					newPrevIndexes.GetUnsafePtr(),
					prevIndexes.GetUnsafePtr(),
					m_Length * (long)sizeof(int));
				prevIndexes.Dispose();
				prevIndexes = newPrevIndexes;
				
				m_Length = newLength;
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
			
			// Mark the new maximum index that can be read
	#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MaxIndex = insertIndex;
	#endif
	 
			// Count the newly-added node
			count = insertIndex + 1;
			
			return new NativeLinkedListIterator(insertIndex);
		}

		/// <summary>
		/// Remove a node. This invalidates all iterators, including the given
		/// iterator. Note that the node's value is not cleared since it's
		/// blittable and therefore can't hold any managed reference that could
		/// be garbage-collected.
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
		public NativeLinkedListIterator Remove(NativeLinkedListIterator it)
		{
			// Must be able to read and write to the collection
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
			
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				FailIfIndexOutOfCheckBounds(it.Index);
#endif
				
				// Node to remove is the head
				if (it.Index == headIndex)
				{
					// Move the head pointer forward one
					int headNextIndex = nextIndexes[it.Index];
					headIndex = headNextIndex;
					
					// Make the new head's previous node be invalid
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					FailIfIndexOutOfCheckBounds(headNextIndex);
#endif
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					FailIfIndexOutOfCheckBounds(tailPrevIndex);
#endif
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					FailIfIndexOutOfCheckBounds(prevIndex);
					FailIfIndexOutOfCheckBounds(nextIndex);
#endif
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					FailIfIndexOutOfCheckBounds(lastIndex);
#endif
					int lastNextIndex = nextIndexes[lastIndex];
					int lastPrevIndex = prevIndexes[lastIndex];
					datas[it.Index] = datas[lastIndex];
					nextIndexes[it.Index] = lastNextIndex;
					prevIndexes[it.Index] = lastPrevIndex;
					
					// If the last node wasn't the tail, set its next node's
					// previous index to where the last node was moved to
					if (lastNextIndex >= 0)
					{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
						FailIfIndexOutOfCheckBounds(lastNextIndex);
#endif
						prevIndexes[lastNextIndex] = it.Index;
					}
					
					// If the last node wasn't the head, set its previous node's
					// next index to where the last node was moved to
					if (lastPrevIndex >= 0)
					{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
						FailIfIndexOutOfCheckBounds(lastPrevIndex);
#endif
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MaxIndex = newCount - 1;
#endif
			
			// Return the appropriate iterator
			return new NativeLinkedListIterator(retIndex);
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
			FailIfCalledFromParallelFor("CopyToNativeArray");
#endif

			// Go to the start of the source
			int i = headIndex;
			while (srcIndex > 0)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				FailIfIndexOutOfCheckBounds(i);
#endif
				i = nextIndexes[i];
				srcIndex--;
			}

			// Copy all nodes to the array
			while (length > 0)
			{
				// Copy the node
				array[destIndex] = datas[i];

				// Go to the next node
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				FailIfIndexOutOfCheckBounds(i);
#endif
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
		}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// Throw an error if the given index isn't in the range
		// [m_MinIndex, m_MaxIndex]
		private void FailIfIndexOutOfCheckBounds(int index)
		{
			if (index < m_MinIndex || index > m_MaxIndex)
			{
				FailOutOfRangeError(index);
			}
		}
		
		// Throw an error if the collection is currently being accessed by a
		// ParallelFor job.
		private void FailIfCalledFromParallelFor(string opName)
		{
			// Min is only ever non-zero and max is only ever not (count-1)
			// when the collection is currently being accessed by a ParallelFor
			// job.
			if (count > 0 && (m_MinIndex != 0 || m_MaxIndex != count - 1))
			{
				throw new IndexOutOfRangeException(
					string.Format(
						"Can't call {0} in an ParallelFor job.",
						opName));
			}
		}
		
		// Throw an appropriate exception when safety checks are enabled
		private void FailOutOfRangeError(int index)
		{
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
		}
#endif
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