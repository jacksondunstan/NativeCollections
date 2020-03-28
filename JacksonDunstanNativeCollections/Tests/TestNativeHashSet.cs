//-----------------------------------------------------------------------
// <copyright file="TestNativeHashSet.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace JacksonDunstan.NativeCollections.Tests
{
    /// <summary>
    /// Unit tests for <see cref="NativeHashSet{T}"/> and
    /// <see cref="NativeHashSet{T}.ParallelWriter"/>.
    /// </summary>
    public class TestNativeHashSet
    {
        private static NativeHashSet<int> CreateEmptySet()
        {
            return new NativeHashSet<int>(0, Allocator.TempJob);
        }
        
        private static void AssertRequiresReadOrWriteAccess(
            NativeHashSet<int> set,
            Action action)
        {
            set.TestUseOnlySetAllowReadAndWriteAccess(false);
            try
            {
                Assert.That(
                    () => action(),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                set.TestUseOnlySetAllowReadAndWriteAccess(true);
            }
        }
        
        private static void AssertRequiresReadOrWriteAccess(
            NativeHashSet<int>.ParallelWriter writer,
            Action action)
        {
            writer.TestUseOnlySetAllowReadAndWriteAccess(false);
            try
            {
                Assert.That(
                    () => action(),
                    Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                writer.TestUseOnlySetAllowReadAndWriteAccess(true);
            }
        }
        
        [Test]
        public void ConstructorCreatesEmptySet()
        {
            using (NativeHashSet<int> set = new NativeHashSet<int>(1, Allocator.Temp))
            {
                Assert.That(set.Length, Is.EqualTo(0));
            }
        }
        
        [Test]
        public void ConstructorClampsToMinimumCapacity()
        {
            using (NativeHashSet<int> set = new NativeHashSet<int>(1, Allocator.Temp))
            {
                Assert.That(set.Capacity, Is.GreaterThan(0));
            }
        }
        
        [Test]
        public void ConstructorRequiresValidAllocator()
        {
            Assert.That(
                () => new NativeHashSet<int>(1, default(Allocator)),
                Throws.Exception);
        }

#if !CSHARP_7_3_OR_NEWER
        private struct NonBlittableType
        {
            public string Str;
        }
        
        [Test]
        public void ConstructorRequiresBlittableType()
        {
            Assert.That(
                () => new NativeHashSet<NonBlittableType>(1, Allocator.Temp),
                Throws.Exception);
        }
#endif
        
        [Test]
        public void GetLengthRequiresReadAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                int len;
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => len = set.Length);
            }
        }
        
        [Test]
        public void GetCapacityReturnsSetCapacity()
        {
            using (NativeHashSet<int> set = new NativeHashSet<int>(
                100,
                Allocator.Temp))
            {
                Assert.That(set.Capacity, Is.EqualTo(100));
            }
        }
        
        [Test]
        public void GetCapacityRequiresReadAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                int cap;
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => cap = set.Capacity);
            }
        }
        
        [Test]
        public void SetCapacityRequiresWriteAccess()
        {
            NativeHashSet<int> set = CreateEmptySet();
            try
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Capacity = 100);
            }
            finally
            {
                set.Dispose();
            }
        }
        
        [Test]
        public void SetCapacityGrowsCapacity()
        {
            NativeHashSet<int> set = CreateEmptySet();
            try
            {
                set.Capacity = 100;
                Assert.That(set.Capacity, Is.EqualTo(100));
            }
            finally
            {
                set.Dispose();
            }
        }
        
        [Test]
        public void SetCapacityCannotShrinkCapacity()
        {
            NativeHashSet<int> set = new NativeHashSet<int>(10, Allocator.Temp);
            try
            {
                Assert.That(() => set.Capacity = 1, Throws.Exception);
            }
            finally
            {
                set.Dispose();
            }
        }
        
        [Test]
        public void TryAddAddsWhenNotPresent()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                Assert.That(set.TryAdd(1), Is.True);
                Assert.That(set.Contains(1), Is.True);
            }
        }
        
        [Test]
        public void TryAddReturnsFalseWhenPresent()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                Assert.That(set.TryAdd(1), Is.False);
            }
        }
        
        [Test]
        public void TryAddGrowsWhenAtCapacity()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                int originalCapacity = set.Capacity;
                for (int i = 0; i < originalCapacity; ++i)
                {
                    set.TryAdd(i);
                }

                Assert.That(set.TryAdd(originalCapacity), Is.True);
                Assert.That(set.Capacity, Is.GreaterThan(originalCapacity));
            }
        }
        
        [Test]
        public void TryAddRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.TryAdd(1));
            }
        }
        
        [Test]
        public void ClearRemovesAllElements()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                set.Clear();
                
                Assert.That(set.Length, Is.EqualTo(0));
                Assert.That(set.Contains(1), Is.False);
            }
        }
        
        [Test]
        public void ClearRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Clear());
            }
        }
        
        [Test]
        public void RemoveRemovesContainedElement()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                Assert.That(set.Remove(1), Is.True);
                
                Assert.That(set.Length, Is.EqualTo(0));
                Assert.That(set.Contains(1), Is.False);
            }
        }
        
        [Test]
        public void RemoveReturnsFalseWhenElementIsNotContained()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                Assert.That(set.Remove(2), Is.False);
                
                Assert.That(set.Length, Is.EqualTo(1));
                Assert.That(set.Contains(1), Is.True);
            }
        }
        
        [Test]
        public void RemoveRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Remove(0));
            }
        }
        
        [Test]
        public void ContainsReturnsTrueForContainedElement()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                Assert.That(set.Contains(1), Is.True);
            }
        }
        
        [Test]
        public void ContainsReturnsFalseForNotContainedElement()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                
                Assert.That(set.Contains(2), Is.False);
            }
        }
        
        [Test]
        public void ContainsRequiresReadAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Contains(0));
            }
        }
        
        [Test]
        public void IsCreatedReturnsTrueForDefaultStruct()
        {
            NativeHashSet<int> set = default(NativeHashSet<int>);
            Assert.That(set.IsCreated, Is.False);
        }
        
        [Test]
        public void IsCreatedReturnsTrueAfterConstructor()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                Assert.That(set.IsCreated, Is.True);
            }
        }
        
        [Test]
        public void OperationsAfterDisposeFail()
        {
            NativeHashSet<int> set = CreateEmptySet();
            set.Dispose();
            Assert.That(
                () => set.Contains(0),
                Throws.Exception);
        }
        
        [Test]
        public void IsCreatedReturnsFalseAfterDispose()
        {
            NativeHashSet<int> set = CreateEmptySet();
            set.Dispose();
            Assert.That(set.IsCreated, Is.False);
        }
        
        [Test]
        public void DisposeRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Dispose());
            }
        }

        private struct PreDisposeJob : IJob
        {
            [WriteOnly] public NativeArray<int> Executed;
            
            public void Execute()
            {
                Executed[0] = 1;
            }
        }
        
        [Test]
        public void DisposeJobDisposesAfterGivenHandle()
        {
            using (NativeArray<int> executed = new NativeArray<int>(
                1,
                Allocator.TempJob))
            {
                NativeHashSet<int> set = CreateEmptySet();
                try
                {
                    PreDisposeJob preDisposeJob = new PreDisposeJob
                    {
                        Executed = executed
                    };
                    JobHandle preDisposeHandle = preDisposeJob.Schedule();

                    JobHandle disposeHandle = set.Dispose(preDisposeHandle);
                    disposeHandle.Complete();
                    
                    Assert.That(set.IsCreated, Is.False);
                    Assert.That(executed[0], Is.EqualTo(1));
                }
                finally
                {
                    if (set.IsCreated)
                    {
                        set.Dispose();
                    }
                }
            }
        }
        
        [Test]
        public void DisposeJobRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                AssertRequiresReadOrWriteAccess(
                    set,
                    () => set.Dispose(default(JobHandle)).Complete());
            }
        }
        
        [Test]
        public void ToNativeArrayCopiesAllElementsToArrayAtGivenIndex()
        {
            using (NativeArray<int> array = new NativeArray<int>(
                5,
                Allocator.TempJob))
            {
                using (NativeHashSet<int> set = CreateEmptySet())
                {
                    set.TryAdd(1);
                    set.TryAdd(2);
                    set.TryAdd(3);

                    NativeArray<int> toArray = set.ToNativeArray(array, 1);

                    // Didn't overwrite out of given bounds
                    Assert.That(array[0], Is.EqualTo(0));
                    Assert.That(array[4], Is.EqualTo(0));

                    // Written values are correct
                    int[] managedArray = {array[1], array[2], array[3]};
                    Array.Sort(managedArray);
                    Assert.That(managedArray, Is.EqualTo(new[] {1, 2, 3}));

                    // Returned array is the same array
                    // Check by writing to one and reading from the other
                    toArray[0] = 4;
                    Assert.That(array[0], Is.EqualTo(4));
                }
            }
        }
        
        [Test]
        public void ToNativeArrayCopiesAllElementsToNewArrayWhenNotIsCreated()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                set.TryAdd(1);
                set.TryAdd(2);
                set.TryAdd(3);

                using (NativeArray<int> array = set.ToNativeArray(
                    default(NativeArray<int>),
                    1))
                {
                    // Created enough room
                    Assert.That(array.Length, Is.EqualTo(4));

                    // Didn't overwrite out of given bounds
                    Assert.That(array[0], Is.EqualTo(0));

                    // Written values are correct
                    int[] managedArray = {array[1], array[2], array[3]};
                    Array.Sort(managedArray);
                    Assert.That(managedArray, Is.EqualTo(new[] {1, 2, 3}));
                }
            }
        }
        
        [Test]
        public void ToNativeArrayCopiesAllElementsToNewArrayWhenNotLongEnough()
        {
            using (NativeArray<int> shortArray = new NativeArray<int>(
                2,
                Allocator.TempJob))
            {
                using (NativeHashSet<int> set = CreateEmptySet())
                {
                    set.TryAdd(1);
                    set.TryAdd(2);
                    set.TryAdd(3);

                    using (NativeArray<int> toArray = set.ToNativeArray(shortArray, 1))
                    {
                        // Created enough room
                        Assert.That(toArray.Length, Is.EqualTo(4));

                        // Didn't overwrite out of given bounds
                        Assert.That(toArray[0], Is.EqualTo(0));

                        // Written values are correct
                        int[] managedArray = {toArray[1], toArray[2], toArray[3]};
                        Array.Sort(managedArray);
                        Assert.That(managedArray, Is.EqualTo(new[] {1, 2, 3}));

                        // Returned array is a different array
                        // Check by writing to one and reading from the other
                        NativeArray<int> toArrayCopy = toArray;
                        toArrayCopy[0] = 4;
                        Assert.That(shortArray[0], Is.Not.EqualTo(4));
                    }
                }
            }
        }
        
        [Test]
        public void ToNativeArrayRequiresReadAccess()
        {
            using (NativeArray<int> array = new NativeArray<int>(
                2,
                Allocator.TempJob))
            {
                using (NativeHashSet<int> set = CreateEmptySet())
                {
                    set.TryAdd(1);

                    AssertRequiresReadOrWriteAccess(
                        set,
                        () => set.ToNativeArray(array, 1));
                }
            }
        }
        
        [Test]
        public void AsParallelWriterReturnsUsableWriter()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                NativeHashSet<int>.ParallelWriter writer = set.AsParallelWriter();
                
                Assert.That(writer.Capacity, Is.EqualTo(set.Capacity));

                Assert.That(writer.TryAdd(1), Is.True);
                
                Assert.That(set.Contains(1), Is.True);
            }
        }
        
        [Test]
        public void ParallelWriterGetCapacityRequiresReadAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                NativeHashSet<int>.ParallelWriter writer = set.AsParallelWriter();

                int cap;
                AssertRequiresReadOrWriteAccess(
                    writer,
                    () => cap = writer.Capacity);
            }
        }
        
        [Test]
        public void ParallelWriterTryAddRequiresWriteAccess()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                NativeHashSet<int>.ParallelWriter writer = set.AsParallelWriter();

                AssertRequiresReadOrWriteAccess(
                    writer,
                    () => writer.TryAdd(1));
            }
        }

        struct ParallelWriterJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> Array;
            [WriteOnly] public NativeHashSet<int>.ParallelWriter Writer;
            
            public void Execute(int index)
            {
                Writer.TryAdd(Array[index]);
            }
        }
        
        [Test]
        public void AsParallelWriterReturnsUsableWriterInJob()
        {
            using (NativeHashSet<int> set = CreateEmptySet())
            {
                using (NativeArray<int> array = new NativeArray<int>(
                    2,
                    Allocator.TempJob))
                {
                    NativeArray<int> arrayRef = array;
                    arrayRef[0] = 1;
                    arrayRef[1] = 2;
                    
                    ParallelWriterJob job = new ParallelWriterJob
                    {
                        Array = array,
                        Writer = set.AsParallelWriter()
                    };
                    
                    JobHandle handle = job.Schedule(array.Length, 64);
                    handle.Complete();
                    
                    Assert.That(set.Length, Is.EqualTo(2));
                }
            }
        }
    }
}