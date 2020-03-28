//-----------------------------------------------------------------------
// <copyright file="NativeHashSet.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
    /// <summary>
    /// The state of a <see cref="NativeHashSet{T}"/>. Shared among instances
    /// of the struct via a pointer to unmanaged memory. This has no type
    /// parameters, so it can be used by all set types.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct NativeHashSetState
    {
        /// <summary>
        /// Item data
        /// </summary>
        [FieldOffset(0)]
        internal byte* Items;
        
        // 4-byte padding on 32-bit architectures here

        /// <summary>
        /// Next bucket data
        /// </summary>
        [FieldOffset(8)]
        internal byte* Next;
        
        // 4-byte padding on 32-bit architectures here

        /// <summary>
        /// Bucket data
        /// </summary>
        [FieldOffset(16)]
        internal byte* Buckets;
        
        // 4-byte padding on 32-bit architectures here

        /// <summary>
        /// Capacity of the <see cref="Items"/> array
        /// </summary>
        [FieldOffset(24)]
        internal int ItemCapacity;

        /// <summary>
        /// Bucket capacity - 1
        /// </summary>
        [FieldOffset(28)]
        internal int BucketCapacityMask;

        /// <summary>
        /// Allocated index length
        /// </summary>
        [FieldOffset(32)]
        internal int AllocatedIndexLength;

        /// <summary>
        /// Array indexed by thread index of first free 
        /// </summary>
        [FieldOffset(JobsUtility.CacheLineSize < 64 ? 64 : JobsUtility.CacheLineSize)]
        internal fixed int FirstFreeTLS[JobsUtility.MaxJobThreadCount * IntsPerCacheLine];

        /// <summary>
        /// Number of ints that fit in one cache line
        /// </summary>
        internal const int IntsPerCacheLine = JobsUtility.CacheLineSize / sizeof(int);
    }
    
    /// <summary>
    /// A hash set native collection.
    /// </summary>
    /// 
    /// <typeparam name="T">
    /// Type of items in the set. Must be blittable.
    /// </typeparam>
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}. Capacity = {Capacity}")]
    [DebuggerTypeProxy(typeof(NativeHashSetDebugView<>))]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeHashSet<T> : IDisposable
#if CSHARP_7_3_OR_NEWER
        where T : unmanaged
#else
		where T : struct
#endif
        , IEquatable<T>
    {
        /// <summary>
        /// Iterator over the set
        /// </summary>
        private struct NativeMultiHashSetIterator
        {
            /// <summary>
            /// Currently iterated item
            /// </summary>
            internal T Item;

            /// <summary>
            /// Index of the next entry in the set
            /// </summary>
            internal int NextEntryIndex;
        }
        
        /// <summary>
        /// State of the set or null if the list is created with the default
        /// constructor or <see cref="Dispose()"/> has been called. This is
        /// shared among all instances of the set.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        NativeHashSetState* m_State;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// A handle to information about what operations can be safely
        /// performed on the set at any given time.
        /// </summary>
        AtomicSafetyHandle m_Safety;

        /// <summary>
        /// A handle that can be used to tell if the set has been disposed yet
        /// or not, which allows for error-checking double disposal.
        /// </summary>
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif

        /// <summary>
        /// Allocator to allocate unmanaged memory with
        /// </summary>
        readonly Allocator m_Allocator;
        
        /// <summary>
        /// Create an empty hash set with a given capacity
        /// </summary>
        /// 
        /// <param name="capacity">
        /// Capacity of the hash set. If less than four, four is used.
        /// </param>
        /// 
        /// <param name="allocator">
        /// Allocator to allocate unmanaged memory with. Must be valid.
        /// </param>
        public NativeHashSet(int capacity, Allocator allocator)
        {
            // Require a valid allocator
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException(
                    "Allocator must be Temp, TempJob or Persistent",
                    "allocator");
            }
            
            RequireBlittable();

            // Insist on a minimum capacity
            if (capacity < 4)
            {
                capacity = 4;
            }

            m_Allocator = allocator;
            
            // Allocate the state
            NativeHashSetState* state = (NativeHashSetState*)UnsafeUtility.Malloc(
                sizeof(NativeHashSetState),
                UnsafeUtility.AlignOf<NativeHashSetState>(),
                allocator);

            state->ItemCapacity = capacity;
            
            // To reduce collisions, use twice as many buckets
            int bucketLength = capacity * 2;
            bucketLength = NextHigherPowerOfTwo(bucketLength);
            state->BucketCapacityMask = bucketLength - 1;
            
            // Allocate state arrays
            int nextOffset;
            int bucketOffset;
            int totalSize = CalculateDataLayout(
                capacity,
                bucketLength,
                out nextOffset,
                out bucketOffset);
            state->Items = (byte*)UnsafeUtility.Malloc(
                totalSize,
                JobsUtility.CacheLineSize,
                allocator);
            state->Next = state->Items + nextOffset;
            state->Buckets = state->Items + bucketOffset;

            m_State = state;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Create(
                out m_Safety,
                out m_DisposeSentinel,
                1,
                allocator);
#else
			DisposeSentinel.Create(
                out m_Safety,
                out m_DisposeSentinel,
                1);
#endif
#endif
            
            Clear();
        }

        /// <summary>
        /// Get the number of items in the set.
        /// 
        /// This operation requires read access.
        /// </summary>
        public int Length
        {
            get
            {
                RequireReadAccess();

                NativeHashSetState* state = m_State;
                int* nextPtrs = (int*)state->Next;
                int freeListSize = 0;
                for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
                {
                    for (int freeIdx = state->FirstFreeTLS[tls * NativeHashSetState.IntsPerCacheLine];
                        freeIdx >= 0;
                        freeIdx = nextPtrs[freeIdx])
                    {
                        ++freeListSize;
                    }
                }

                return Math.Min(state->ItemCapacity, state->AllocatedIndexLength) - freeListSize;
            }
        }

        /// <summary>
        /// Get or set the set's capacity to hold items. The capacity can't be
        /// set to be lower than it currently is.
        /// 
        /// The 'get' operation requires read access and the 'set' operation
        /// requires write access.
        /// </summary>
        public int Capacity
        {
            get
            {
                RequireReadAccess();

                return m_State->ItemCapacity;
            }
            set
            {
                RequireWriteAccess();

                Reallocate(value);
            }
        }
        
        /// <summary>
        /// Try to add an item to the set.
        ///
        /// This operation requires write access.
        /// </summary>
        /// 
        /// <param name="item">
        /// Item to add
        /// </param>
        /// 
        /// <returns>
        /// If the item was added to the set. This is false only if the item was
        /// already in the set.</returns>
        public bool TryAdd(T item)
        {
            RequireWriteAccess();
            
            NativeMultiHashSetIterator tempIt;
            if (TryGetFirstValueAtomic(m_State, item, out tempIt))
            {
                return false;
            }

            // Allocate an entry from the free list
            int idx;
            int* nextPtrs;

            if (m_State->AllocatedIndexLength >= m_State->ItemCapacity
                && m_State->FirstFreeTLS[0] < 0)
            {
                for (int tls = 1; tls < JobsUtility.MaxJobThreadCount; ++tls)
                {
                    if (m_State->FirstFreeTLS[tls * NativeHashSetState.IntsPerCacheLine] >= 0)
                    {
                        idx = m_State->FirstFreeTLS[tls * NativeHashSetState.IntsPerCacheLine];
                        nextPtrs = (int*)m_State->Next;
                        m_State->FirstFreeTLS[tls * NativeHashSetState.IntsPerCacheLine] = nextPtrs[idx];
                        nextPtrs[idx] = -1;
                        m_State->FirstFreeTLS[0] = idx;
                        break;
                    }
                }
                if (m_State->FirstFreeTLS[0] < 0)
                {
                    int capacity = m_State->ItemCapacity;
                    int newCapacity = capacity == 0 ? 1 : capacity * 2;

                    Reallocate(newCapacity);
                }
            }
            idx = m_State->FirstFreeTLS[0];
            if (idx >= 0)
            {
                m_State->FirstFreeTLS[0] = ((int*)m_State->Next)[idx];
            }
            else
            {
                idx = m_State->AllocatedIndexLength++;
            }

            // Write the new value to the entry
            UnsafeUtility.WriteArrayElement(m_State->Items, idx, item);

            int bucket = item.GetHashCode() & m_State->BucketCapacityMask;
            // Add the index to the hash-set
            int* buckets = (int*)m_State->Buckets;
            nextPtrs = (int*)m_State->Next;

            nextPtrs[idx] = buckets[bucket];
            buckets[bucket] = idx;

            return true;
        }

        /// <summary>
        /// Remove all items from the set.
        ///
        /// This operation requires write access.
        /// </summary>
        public void Clear()
        {
            RequireWriteAccess();
            
            int* buckets = (int*)m_State->Buckets;
            for (int i = 0; i <= m_State->BucketCapacityMask; ++i)
            {
                buckets[i] = -1;
            }

            int* nextPtrs = (int*)m_State->Next;
            for (int i = 0; i < m_State->ItemCapacity; ++i)
            {
                nextPtrs[i] = -1;
            }

            for (int tls = 0; tls < JobsUtility.MaxJobThreadCount; ++tls)
            {
                m_State->FirstFreeTLS[tls * NativeHashSetState.IntsPerCacheLine] = -1;
            }

            m_State->AllocatedIndexLength = 0;
        }

        /// <summary>
        /// Remove an item from the set.
        ///
        /// This operation requires write access.
        /// </summary>
        /// 
        /// <param name="item">
        /// Item to remove
        /// </param>
        ///
        /// <returns>
        /// If the item was removed. This is false only if the item wasn't
        /// contained in the set.
        /// </returns>
        public bool Remove(T item)
        {
            RequireWriteAccess();
            
            // First find the slot based on the hash
            int* buckets = (int*)m_State->Buckets;
            int* nextPtrs = (int*)m_State->Next;
            int bucket = item.GetHashCode() & m_State->BucketCapacityMask;
            int prevEntry = -1;
            int entryIdx = buckets[bucket];

            while (entryIdx >= 0 && entryIdx < m_State->ItemCapacity)
            {
                if (UnsafeUtility.ReadArrayElement<T>(m_State->Items, entryIdx).Equals(item))
                {
                    // Found matching element, remove it
                    if (prevEntry < 0)
                    {
                        buckets[bucket] = nextPtrs[entryIdx];
                    }
                    else
                    {
                        nextPtrs[prevEntry] = nextPtrs[entryIdx];
                    }

                    // And free the index
                    nextPtrs[entryIdx] = m_State->FirstFreeTLS[0];
                    m_State->FirstFreeTLS[0] = entryIdx;
                    return true;
                }

                prevEntry = entryIdx;
                entryIdx = nextPtrs[entryIdx];
            }

            return false;
        }

        /// <summary>
        /// Check if the set contains a given item
        ///
        /// This operation requires read access.
        /// </summary>
        /// 
        /// <param name="item">
        /// Item to check
        /// </param>
        /// 
        /// <returns>
        /// If the set contains the given item
        /// </returns>
        public bool Contains(T item)
        {
            RequireReadAccess();
            
            NativeMultiHashSetIterator tempIt;
            return TryGetFirstValueAtomic(m_State, item, out tempIt);
        }

        /// <summary>
        /// Check if the underlying unmanaged memory has been created and not
        /// freed via a call to <see cref="Dispose()"/>.
        /// 
        /// This operation has no access requirements.
        ///
        /// This operation is O(1).
        /// </summary>
        /// 
        /// <value>
        /// Initially true when a non-default constructor is called but
        /// initially false when the default constructor is used. After
        /// <see cref="Dispose()"/> is called, this becomes false. Note that
        /// calling <see cref="Dispose()"/> on one copy of a set doesn't result
        /// in this becoming false for all copies if it was true before. This
        /// property should <i>not</i> be used to check whether the set is
        /// usable, only to check whether it was <i>ever</i> usable.
        /// </value>
        public bool IsCreated
        {
            get
            {
                return m_State != null;
            }
        }

        /// <summary>
        /// Release the set's unmanaged memory. Do not use it after this. Do
        /// not call <see cref="Dispose()"/> on copies of the set either.
        /// 
        /// This operation requires write access.
        /// 
        /// This complexity of this operation is O(1) plus the allocator's
        /// deallocation complexity.
        /// </summary>
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
            
            Deallocate();
        }

        /// <summary>
        /// Schedule a job to release the set's unmanaged memory after the given
        /// dependency jobs. Do not use it after this job executes. Do
        /// not call <see cref="Dispose()"/> on copies of the set either.
        /// 
        /// This operation requires write access.
        /// 
        /// This complexity of this operation is O(1) plus the allocator's
        /// deallocation complexity.
        /// </summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            RequireWriteAccess();
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Clear the dispose sentinel, but don't Dispose it
            DisposeSentinel.Clear(ref m_DisposeSentinel);
#endif
            
            // Schedule the job
            DisposeJob disposeJob = new DisposeJob { Set = this };
            JobHandle jobHandle = disposeJob.Schedule(inputDeps);

            // Release the atomic safety handle now that the job is scheduled
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_Safety);
#endif
            
            m_State = null;
            return jobHandle;
        }

        /// <summary>
        /// A job to call <see cref="NativeHashSet{T}.Deallocate"/>
        /// </summary>
        private struct DisposeJob : IJob
        {
            internal NativeHashSet<T> Set;

            public void Execute()
            {
                Set.Deallocate();
            }
        }

        /// <summary>
        /// Add all of the items in the set to an array
        /// </summary>
        ///
        /// <param name="array">
        /// Array to add to. If <see cref="NativeArray{T}.IsCreated"/> is false
        /// or the array isn't long enough to hold all the items of the set,
        /// a new <see cref="NativeArray{T}"/> will be created with length
        /// equal to the number of items in the set and with the same
        /// allocator as the set.
        /// </param>
        ///
        /// <param name="index">
        /// Index into <paramref name="array"/> to start adding items at
        /// </param>
        public NativeArray<T> ToNativeArray(
            NativeArray<T> array = default(NativeArray<T>),
            int index = 0)
        {
            RequireReadAccess();
            
            if (index < 0)
            {
                index = 0;
            }

            int length = Length;
            if (!array.IsCreated || array.Length - index < length)
            {
                array = new NativeArray<T>(length + index, m_Allocator);
            }
            
            int* bucketArray = (int*)m_State->Buckets;
            int* bucketNext = (int*)m_State->Next;
            for (int i = 0; i <= m_State->BucketCapacityMask; ++i)
            {
                for (int b = bucketArray[i]; b != -1; b = bucketNext[b])
                {
                    array[index] = UnsafeUtility.ReadArrayElement<T>(
                        m_State->Items,
                        b);
                    index++;
                }
            }

            return array;
        }

        /// <summary>
        /// Create a <see cref="ParallelWriter"/> that writes to this set.
        /// 
        /// This operation has no access requirements, but using the writer
        /// requires write access.
        /// </summary>
        ///
        /// <returns>
        /// A <see cref="ParallelWriter"/> that writes to this set
        /// </returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            writer.m_Safety = m_Safety;
#endif
            writer.m_ThreadIndex = 0;

            writer.m_State = m_State;
            return writer;
        }

        /// <summary>
        /// Deallocate the state's contents then the state itself.
        /// </summary>
        private void Deallocate()
        {
            UnsafeUtility.Free(m_State->Items, m_Allocator);
            m_State->Items = null;
            m_State->Next = null;
            m_State->Buckets = null;
            UnsafeUtility.Free(m_State, m_Allocator);
            m_State = null;
        }
        
        /// <summary>
        /// Allocate an entry from the free list. The list must not be full.
        /// </summary>
        /// 
        /// <param name="state">
        /// State to allocate from.
        /// </param>
        /// 
        /// <param name="threadIndex">
        /// Index of the allocating thread.
        /// </param>
        /// 
        /// <returns>
        /// The allocated free list entry index
        /// </returns>
        private static int AllocFreeListEntry(
            NativeHashSetState* state,
            int threadIndex)
        {
            int idx;
            int* nextPtrs = (int*)state->Next;
            do
            {
                idx = state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine];
                if (idx < 0)
                {
                    // Try to refill local cache
                    Interlocked.Exchange(
                        ref state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine],
                        -2);
                    
                    // If it failed try to get one from the never-allocated array
                    if (state->AllocatedIndexLength < state->ItemCapacity)
                    {
                        idx = Interlocked.Add(ref state->AllocatedIndexLength, 16) - 16;
                        if (idx < state->ItemCapacity - 1)
                        {
                            int count = Math.Min(16, state->ItemCapacity - idx);
                            for (int i = 1; i < count; ++i)
                            {
                                nextPtrs[idx + i] = idx + i + 1;
                            }

                            nextPtrs[idx + count - 1] = -1;
                            nextPtrs[idx] = -1;
                            Interlocked.Exchange(
                                ref state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine],
                                idx + 1);
                            return idx;
                        }

                        if (idx == state->ItemCapacity - 1)
                        {
                            Interlocked.Exchange(
                                ref state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine],
                                -1);
                            return idx;
                        }
                    }
                    Interlocked.Exchange(
                        ref state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine],
                        -1);
                    // Failed to get any, try to get one from another free list
                    bool again = true;
                    while (again)
                    {
                        again = false;
                        for (int other = (threadIndex + 1) % JobsUtility.MaxJobThreadCount;
                            other != threadIndex;
                            other = (other + 1) % JobsUtility.MaxJobThreadCount)
                        {
                            do
                            {
                                idx = state->FirstFreeTLS[other * NativeHashSetState.IntsPerCacheLine];
                                if (idx < 0)
                                {
                                    break;
                                }
                            } while (Interlocked.CompareExchange(
                                ref state->FirstFreeTLS[other * NativeHashSetState.IntsPerCacheLine],
                                nextPtrs[idx], idx) != idx);
                            if (idx == -2)
                            {
                                again = true;
                            }
                            else if (idx >= 0)
                            {
                                nextPtrs[idx] = -1;
                                return idx;
                            }
                        }
                    }
                    throw new InvalidOperationException("HashSet is full");
                }
                if (idx >= state->ItemCapacity)
                {
                    throw new InvalidOperationException("HashSet is full");
                }
            } while (Interlocked.CompareExchange(
                ref state->FirstFreeTLS[threadIndex * NativeHashSetState.IntsPerCacheLine],
                nextPtrs[idx],
                idx) != idx);
            nextPtrs[idx] = -1;
            return idx;
        }

        private static bool TryGetFirstValueAtomic(
            NativeHashSetState* state,
            T item,
            out NativeMultiHashSetIterator it)
        {
            it.Item = item;
            if (state->AllocatedIndexLength <= 0)
            {
                it.NextEntryIndex = -1;
                return false;
            }
            // First find the slot based on the hash
            int* buckets = (int*)state->Buckets;
            int bucket = item.GetHashCode() & state->BucketCapacityMask;
            it.NextEntryIndex = buckets[bucket];
            return TryGetNextValueAtomic(state, ref it);
        }

        private static bool TryGetNextValueAtomic(
            NativeHashSetState* state,
            ref NativeMultiHashSetIterator it)
        {
            int entryIdx = it.NextEntryIndex;
            it.NextEntryIndex = -1;
            if (entryIdx < 0 || entryIdx >= state->ItemCapacity)
            {
                return false;
            }

            int* nextPtrs = (int*)state->Next;
            while (!UnsafeUtility.ReadArrayElement<T>(
                state->Items,
                entryIdx).Equals(it.Item))
            {
                entryIdx = nextPtrs[entryIdx];
                if (entryIdx < 0 || entryIdx >= state->ItemCapacity)
                {
                    return false;
                }
            }
            it.NextEntryIndex = nextPtrs[entryIdx];

            return true;
        }

        /// <summary>
        /// Implements parallel writer. Use AsParallelWriter to obtain it from container.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public struct ParallelWriter
        {
            /// <summary>
            /// State of the set or null if the list is created with the default
            /// constructor or <see cref="Dispose()"/> has been called. This is
            /// shared among all instances of the set.
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            internal NativeHashSetState* m_State;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            /// <summary>
            /// A handle to information about what operations can be safely
            /// performed on the set at any given time.
            /// </summary>
            internal AtomicSafetyHandle m_Safety;
#endif
            
            /// <summary>
            /// Thread index of the job using this object. This is set by Unity
            /// and must have this exact name and type.
            /// </summary>
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            /// <summary>
            /// Get or set the set's capacity to hold items. The capacity can't
            /// be set to be lower than it currently is.
            /// 
            /// The 'get' operation requires read access and the 'set' operation
            /// requires write access.
            /// </summary>
            public int Capacity
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    return m_State->ItemCapacity;
                }
            }

            /// <summary>
            /// Try to add an item to the set.
            ///
            /// This operation requires write access.
            /// </summary>
            /// 
            /// <param name="item">
            /// Item to add
            /// </param>
            /// 
            /// <returns>
            /// If the item was added to the set. This is false only if the item
            /// was already in the set.
            /// </returns>
            public bool TryAdd(T item)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                NativeMultiHashSetIterator tempIt;
                if (TryGetFirstValueAtomic(m_State, item, out tempIt))
                {
                    return false;
                }
                // Allocate an entry from the free list
                int idx = AllocFreeListEntry(m_State, m_ThreadIndex);

                // Write the new value to the entry
                UnsafeUtility.WriteArrayElement(m_State->Items, idx, item);

                int bucket = item.GetHashCode() & m_State->BucketCapacityMask;
                // Add the index to the hash-map
                int* buckets = (int*)m_State->Buckets;
                if (Interlocked.CompareExchange(
                    ref buckets[bucket],
                    idx,
                    -1) != -1)
                {
                    int* nextPtrs = (int*)m_State->Next;
                    do
                    {
                        nextPtrs[idx] = buckets[bucket];
                        if (TryGetFirstValueAtomic(m_State, item, out tempIt))
                        {
                            // Put back the entry in the free list if someone
                            // else added it while trying to add
                            do
                            {
                                nextPtrs[idx] = m_State->FirstFreeTLS[
                                    m_ThreadIndex
                                    * NativeHashSetState.IntsPerCacheLine];
                            } while (Interlocked.CompareExchange(
                                ref m_State->FirstFreeTLS[
                                    m_ThreadIndex
                                    * NativeHashSetState.IntsPerCacheLine],
                                idx,
                                nextPtrs[idx]) != nextPtrs[idx]);

                            return false;
                        }
                    } while (Interlocked.CompareExchange(
                        ref buckets[bucket],
                        idx,
                        nextPtrs[idx]) != nextPtrs[idx]);
                }
                return true;
            }
            
            /// <summary>
            /// Set whether both read and write access should be allowed for the
            /// set. This is used for automated testing purposes only.
            /// </summary>
            /// 
            /// <param name="allowReadOrWriteAccess">
            /// If both read and write access should be allowed for the set
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
        }

        /// <summary>
        /// Reallocate to grow the capacity of the set
        /// </summary>
        ///
        /// <param name="newCapacity">
        /// Capacity to grow to. Must be larger than the current capacity.
        /// </param>
        private void Reallocate(int newCapacity)
        {
            int newBucketCapacity = newCapacity * 2;
            newBucketCapacity = NextHigherPowerOfTwo(newBucketCapacity);

            // No change in capacity
            if (m_State->ItemCapacity == newCapacity
                && m_State->BucketCapacityMask + 1 == newBucketCapacity)
            {
                return;
            }

            // Can't shrink
            if (newCapacity < m_State->ItemCapacity)
            {
                throw new Exception("NativeHashSet<T> can't shrink");
            }

            // Allocate new data
            int nextOffset;
            int bucketOffset;
            int totalSize = CalculateDataLayout(
                newCapacity,
                newBucketCapacity,
                out nextOffset,
                out bucketOffset);
            byte* newData = (byte*)UnsafeUtility.Malloc(
                totalSize,
                JobsUtility.CacheLineSize,
                m_Allocator);
            byte* newItems = newData;
            byte* newNext = newData + nextOffset;
            byte* newBuckets = newData + bucketOffset;

            // The items are taken from a free-list and might not be tightly
            // packed, copy all of the old capacity
            UnsafeUtility.MemCpy(
                newItems,
                m_State->Items,
                m_State->ItemCapacity * UnsafeUtility.SizeOf<T>());
            UnsafeUtility.MemCpy(
                newNext,
                m_State->Next,
                m_State->ItemCapacity * UnsafeUtility.SizeOf<int>());
            for (
                int emptyNext = m_State->ItemCapacity;
                emptyNext < newCapacity;
                ++emptyNext)
            {
                ((int*)newNext)[emptyNext] = -1;
            }

            // Re-hash the buckets, first clear the new bucket list, then insert
            // all values from the old list
            for (int bucket = 0; bucket < newBucketCapacity; ++bucket)
            {
                ((int*)newBuckets)[bucket] = -1;
            }
            for (int bucket = 0; bucket <= m_State->BucketCapacityMask; ++bucket)
            {
                int* buckets = (int*)m_State->Buckets;
                int* nextPtrs = (int*)newNext;
                while (buckets[bucket] >= 0)
                {
                    int curEntry = buckets[bucket];
                    buckets[bucket] = nextPtrs[curEntry];
                    int newBucket = UnsafeUtility.ReadArrayElement<T>(
                        m_State->Items,
                        curEntry).GetHashCode() & (newBucketCapacity - 1);
                    nextPtrs[curEntry] = ((int*)newBuckets)[newBucket];
                    ((int*)newBuckets)[newBucket] = curEntry;
                }
            }

            // Free the old state contents
            UnsafeUtility.Free(m_State->Items, m_Allocator);
            
            // Set the new state contents
            if (m_State->AllocatedIndexLength > m_State->ItemCapacity)
            {
                m_State->AllocatedIndexLength = m_State->ItemCapacity;
            }
            m_State->Items = newData;
            m_State->Next = newNext;
            m_State->Buckets = newBuckets;
            m_State->ItemCapacity = newCapacity;
            m_State->BucketCapacityMask = newBucketCapacity - 1;
        }

        /// <summary>
        /// Throw an exception if <typeparam name="T"/> is not blittable
        /// </summary>
        private static void RequireBlittable()
        {
// No check is necessary because C# 7.3 uses `where T : unmanaged`
#if !CSHARP_7_3_OR_NEWER
			if (!UnsafeUtility.IsBlittable<T>())
			{
				throw new ArgumentException(
					"Type used in NativeHashSet must be blittable");
			}
#endif
        }
        
        /// <summary>
        /// Throw an exception if the set isn't readable
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void RequireReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        /// <summary>
        /// Throw an exception if the set isn't writable
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void RequireWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        /// <summary>
        /// Compute the smallest power of two greater than or equal to the given
        /// value
        /// </summary>
        ///
        /// <param name="val">
        /// Value to compute the smallest power of two greater than or equal to
        /// </param>
        ///
        /// <returns>
        /// The smallest power of two greater than or equal to the given value
        /// </returns>
        private static int NextHigherPowerOfTwo(int val)
        {
            val -= 1;
            val |= val >> 1;
            val |= val >> 2;
            val |= val >> 4;
            val |= val >> 8;
            val |= val >> 16;
            return val + 1;
        }

        /// <summary>
        /// Calculate the data layout of the state
        /// </summary>
        /// 
        /// <param name="length">
        /// Number of elements
        /// </param>
        /// 
        /// <param name="bucketLength">
        /// Number of buckets
        /// </param>
        /// 
        /// <param name="nextOffset">
        /// Output of the next item offset
        /// </param>
        /// 
        /// <param name="bucketOffset">
        /// Output of the bucket offset
        /// </param>
        /// 
        /// <returns>
        /// The total size of the data
        /// </returns>
        private static int CalculateDataLayout(
            int length,
            int bucketLength,
            out int nextOffset,
            out int bucketOffset)
        {
            int itemSize = UnsafeUtility.SizeOf<T>();

            // Offset is rounded up to be an even cacheLineSize
            nextOffset = itemSize * length + JobsUtility.CacheLineSize - 1;
            nextOffset -= nextOffset % JobsUtility.CacheLineSize;

            bucketOffset = nextOffset
                + UnsafeUtility.SizeOf<int>() * length
                + JobsUtility.CacheLineSize - 1;
            bucketOffset -= bucketOffset % JobsUtility.CacheLineSize;

            int totalSize = bucketOffset + UnsafeUtility.SizeOf<int>() * bucketLength;
            return totalSize;
        }
        
        /// <summary>
        /// Set whether both read and write access should be allowed for the
        /// set. This is used for automated testing purposes only.
        /// </summary>
        /// 
        /// <param name="allowReadOrWriteAccess">
        /// If both read and write access should be allowed for the set
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
    }
    
    /// <summary>
    /// Provides a debugger view of <see cref="NativeHashSet{T}"/>.
    /// </summary>
    /// 
    /// <typeparam name="T">
    /// Type of elements in the set
    /// </typeparam>
    internal sealed class NativeHashSetDebugView<T>
#if CSHARP_7_3_OR_NEWER
        where T : unmanaged
#else
		where T : struct
#endif
        , IEquatable<T>
    {
        /// <summary>
        /// Set to provide a view of
        /// </summary>
        private readonly NativeHashSet<T> m_Set;
        
        /// <summary>
        /// Create the debug view for a given set
        /// </summary>
        /// 
        /// <param name="set">
        /// Set to provide a view of
        /// </param>
        public NativeHashSetDebugView(NativeHashSet<T> set)
        {
            m_Set = set;
        }
        
        /// <summary>
        /// Get all the items in the set.
        ///
        /// This operation requires read access.
        /// </summary>
        public List<T> Items
        {
            get
            {
                using (NativeArray<T> array = m_Set.ToNativeArray())
                {
                    
                    List<T> result = new List<T>(array.Length);
                    foreach (T item in array)
                    {
                        if (m_Set.Contains(item))
                        {
                            result.Add(item);
                        }
                    }
                    return result;
                }
            }
        }
    }
}