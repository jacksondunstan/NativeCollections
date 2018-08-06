using NUnit.Framework;
using Unity.Collections;

namespace NativeCollections.Tests
{
    /// <summary>
    /// Unit tests for <see cref="NativeLinkedList{T}"/> and
    /// <see cref="NativeLinkedListIterator"/>
    /// </summary>
	/// 
	/// <author>
	/// Jackson Dunstan, http://JacksonDunstan.com/articles/4865
	/// </author>
    public class TestNativeLinkedList
    {
        // Assert general invariants of the type
        private static void AssertGeneralInvariants(NativeLinkedList<int> list)
        {
            // Count and capacity can't be negative
            Assert.That(list.Count, Is.GreaterThanOrEqualTo(0));
            Assert.That(list.Capacity, Is.GreaterThanOrEqualTo(0));
            
            // Count <= Capacity
            Assert.That(list.Count, Is.LessThanOrEqualTo(list.Capacity));
            
            // Either head and tail are both valid or both invalid
            if (list.IsValid(list.GetHead()) && !list.IsValid(list.GetTail()))
            {
                Assert.Fail("Head is valid but Tail is invalid");
            }
            if (!list.IsValid(list.GetHead()) && list.IsValid(list.GetTail()))
            {
                Assert.Fail("Tail is valid but Head is invalid");
            }
            
            // If not empty, head and tail must be valid. Otherwise, head and
            // tail must be invalid.
            Assert.That(
                list.Count > 0
                    && list.IsValid(list.GetHead())
                    && list.IsValid(list.GetTail())
                || list.Count == 0
                    && !list.IsValid(list.GetHead())
                    && !list.IsValid(list.GetTail()),
                Is.True);
            
            // Forward iteration must cover exactly Count steps and match the
            // array returned by ToArray.
            int[] datas = list.ToArray();
            int numForwardIterations = 0;
            for (NativeLinkedListIterator it = list.GetHead();
                list.IsValid(it);
                it = list.GetNext(it))
            {
                Assert.That(
                    datas[numForwardIterations],
                    Is.EqualTo(list.GetData(it)));
                numForwardIterations++;
            }
            Assert.That(numForwardIterations, Is.EqualTo(list.Count));
            
            // Backward iteration must cover exactly Count steps and match the
            // array returned by ToArray.
            int numBackwardIterations = 0;
            for (NativeLinkedListIterator it = list.GetTail();
                list.IsValid(it);
                it = list.GetPrev(it))
            {
                Assert.That(
                    datas[list.Count - 1 - numBackwardIterations],
                    Is.EqualTo(list.GetData(it)));
                numBackwardIterations++;
            }
            Assert.That(numBackwardIterations, Is.EqualTo(list.Count));
            
            // Forward and backward iteration must take the same number of steps
            Assert.That(
                numBackwardIterations,
                Is.EqualTo(numForwardIterations));
        }
        
        [Test]
        public void PushBackIncreasesCount()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                Assert.That(list.Count, Is.EqualTo(0));
                
                list.PushBack(10);
                Assert.That(list.Count, Is.EqualTo(1));
                
                list.PushBack(20);
                Assert.That(list.Count, Is.EqualTo(2));
                
                list.PushBack(30);
                Assert.That(list.Count, Is.EqualTo(3));
                
                list.PushBack(40);
                Assert.That(list.Count, Is.EqualTo(4));
                
                list.PushBack(50);
                Assert.That(list.Count, Is.EqualTo(5));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void PushBackWhenFullIncreasesCapacity()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushBack(10);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushBack(20);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushBack(30);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushBack(40);
                Assert.That(list.Capacity, Is.GreaterThan(3));
                
                list.PushBack(50);
                Assert.That(list.Capacity, Is.GreaterThan(3));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void PushBackReturnsIteratorToAddedNode()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                NativeLinkedListIterator it = list.PushBack(10);
                
                Assert.That(list.GetData(it), Is.EqualTo(10));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void PushFrontIncreasesCount()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                Assert.That(list.Count, Is.EqualTo(0));
                
                list.PushFront(10);
                Assert.That(list.Count, Is.EqualTo(1));
                
                list.PushFront(20);
                Assert.That(list.Count, Is.EqualTo(2));
                
                list.PushFront(30);
                Assert.That(list.Count, Is.EqualTo(3));
                
                list.PushFront(40);
                Assert.That(list.Count, Is.EqualTo(4));
                
                list.PushFront(50);
                Assert.That(list.Count, Is.EqualTo(5));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void PushFrontWhenFullIncreasesCapacity()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushFront(10);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushFront(20);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushFront(30);
                Assert.That(list.Capacity, Is.EqualTo(3));
                
                list.PushFront(40);
                Assert.That(list.Capacity, Is.GreaterThan(3));
                
                list.PushFront(50);
                Assert.That(list.Capacity, Is.GreaterThan(3));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void PushFrontReturnsIteratorToAddedNode()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                NativeLinkedListIterator it = list.PushFront(10);
                
                Assert.That(list.GetData(it), Is.EqualTo(10));
                
                AssertGeneralInvariants(list);
            }
        }
    
        [Test]
        public void GetHeadReturnsInvalidIteratorForEmptyList()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                NativeLinkedListIterator it = list.GetHead();
                
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
    
        [Test]
        public void GetTailReturnsInvalidIteratorForEmptyList()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                NativeLinkedListIterator it = list.GetTail();
                
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void GetNextTraversesListForward()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                list.PushBack(50);
                
                NativeLinkedListIterator it = list.GetHead();
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(10));
    
                it = list.GetNext(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(20));
                
                it = list.GetNext(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(30));
                
                it = list.GetNext(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(40));
                
                it = list.GetNext(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(50));
                
                it = list.GetNext(it);
                
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void GetPrevTraversesListBackward()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                list.PushBack(50);
                
                NativeLinkedListIterator it = list.GetTail();
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(50));
    
                it = list.GetPrev(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(40));
                
                it = list.GetPrev(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(30));
                
                it = list.GetPrev(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(20));
                
                it = list.GetPrev(it);
                
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(10));
                
                it = list.GetPrev(it);
                
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void SetDataSetsNodeData()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                list.PushBack(50);
                
                NativeLinkedListIterator it = list.GetHead();
                list.SetData(it, 100);
				Assert.That(list.GetData(it), Is.EqualTo(100));
                
                it = list.GetNext(it);
                list.SetData(it, 200);
				Assert.That(list.GetData(it), Is.EqualTo(200));
                
                it = list.GetNext(it);
                list.SetData(it, 300);
				Assert.That(list.GetData(it), Is.EqualTo(300));
                
                it = list.GetNext(it);
                list.SetData(it, 400);
				Assert.That(list.GetData(it), Is.EqualTo(400));
                
                it = list.GetNext(it);
                list.SetData(it, 500);
				Assert.That(list.GetData(it), Is.EqualTo(500));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveDoesNothingWhenIteratorIsInvalid()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);

                NativeLinkedListIterator invalid = NativeLinkedListIterator.MakeInvalid();
                NativeLinkedListIterator ret = list.Remove(invalid);
                
                Assert.That(list.IsValid(ret), Is.False);
				NativeLinkedListIterator it = list.GetHead();
				Assert.That(list.GetData(it), Is.EqualTo(10));
				it = list.GetNext(it);
				Assert.That(list.GetData(it), Is.EqualTo(20));
				it = list.GetNext(it);
				Assert.That(list.GetData(it), Is.EqualTo(30));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveDoesNothingWhenListIsEmpty()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                NativeLinkedListIterator it = list.GetHead();
                list.Remove(it);

                NativeLinkedListIterator ret = list.Remove(it);
                
                Assert.That(list.IsValid(ret), Is.False);
                Assert.That(list.Count, Is.EqualTo(0));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveOnlyNodeEmptiesList()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                
                NativeLinkedListIterator it = list.GetHead();
                list.Remove(it);

                Assert.That(list.IsValid(it), Is.False);
                Assert.That(list.IsValid(list.GetHead()), Is.False);
                Assert.That(list.IsValid(list.GetTail()), Is.False);
                Assert.That(list.Count, Is.EqualTo(0));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveHeadLeavesRemainingNodesReturnsNext()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                
                NativeLinkedListIterator next = list.Remove(list.GetHead());
                
                Assert.That(list.GetData(next), Is.EqualTo(20));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list.GetData(list.GetHead()), Is.EqualTo(20));
                Assert.That(list.GetData(list.GetTail()), Is.EqualTo(30));
                
                NativeLinkedListIterator it = list.GetHead();
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(20));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(30));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveHeadWhenNotFirstElementLeavesRemainingNodesReturnsNext()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                4,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                // array is now [10, 20, 30, 40]
                // head = 0, tail = 3
                
                list.Remove(list.GetHead());
                // array is now [40, 20, 30, _]
                // head = 1, tail = 0
                
                NativeLinkedListIterator next = list.Remove(list.GetHead());
                // array is now [40, 30, _, _]
                // head = 1, tail = 0
                
                Assert.That(list.GetData(next), Is.EqualTo(30));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list.GetData(list.GetHead()), Is.EqualTo(30));
                Assert.That(list.IsValid(list.GetPrev(list.GetHead())), Is.False);
                Assert.That(list.GetData(list.GetTail()), Is.EqualTo(40));
                Assert.That(list.IsValid(list.GetNext(list.GetTail())), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailLeavesRemainingNodesReturnsPrev()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                
                NativeLinkedListIterator prev = list.Remove(list.GetTail());
                
                Assert.That(list.GetData(prev), Is.EqualTo(20));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list.GetData(list.GetHead()), Is.EqualTo(10));
                Assert.That(list.GetData(list.GetTail()), Is.EqualTo(20));
                
                NativeLinkedListIterator it = list.GetHead();
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(10));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(20));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailWhenNotLastElementLeavesRemainingNodesReturnsPrev()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                4,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                // array is now [10, 20, 30, 40]
                // head = 0, tail = 3
                
                list.Remove(list.GetHead());
                // array is now [40, 20, 30, _]
                // head = 1, tail = 0
                
                NativeLinkedListIterator prev = list.Remove(list.GetTail());
                // array is now [30, 20, _, _]
                // head = 1, tail = 0
                
                Assert.That(list.GetData(prev), Is.EqualTo(30));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list.GetData(list.GetHead()), Is.EqualTo(20));
                Assert.That(list.IsValid(list.GetPrev(list.GetHead())), Is.False);
                Assert.That(list.GetData(list.GetTail()), Is.EqualTo(30));
                Assert.That(list.IsValid(list.GetNext(list.GetTail())), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveMiddleLeavesRemainingNodesReturnsPrev()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                
                NativeLinkedListIterator middle = list.GetNext(list.GetHead());
                NativeLinkedListIterator prev = list.Remove(middle);
                
                Assert.That(list.GetData(prev), Is.EqualTo(10));
                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(list.GetData(list.GetHead()), Is.EqualTo(10));
                Assert.That(list.GetData(list.GetTail()), Is.EqualTo(30));
                
                NativeLinkedListIterator it = list.GetHead();
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(10));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.True);
                Assert.That(list.GetData(it), Is.EqualTo(30));
                
                it = list.GetNext(it);
                Assert.That(list.IsValid(it), Is.False);
                
                AssertGeneralInvariants(list);
            }
        }

        [Test]
        public void ToArrayReturnsNodeDataInOrder()
        {
            using (NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);

                int[] arr = list.ToArray();
                
                Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));
                
                AssertGeneralInvariants(list);
            }
		}

		[Test]
		public void CopyToNativeArrayFillsGivenArray()
		{
			using (NativeLinkedList<int> list = new NativeLinkedList<int>(
				5,
				Allocator.Temp))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				list.PushBack(50);

				using (NativeArray<int> array = new NativeArray<int>(
					4,
					Allocator.Temp))
				{
					list.CopyToNativeArray(array, 2, 1, 3);

					Assert.That(array[0], Is.EqualTo(0));
					Assert.That(array[1], Is.EqualTo(30));
					Assert.That(array[2], Is.EqualTo(40));
					Assert.That(array[3], Is.EqualTo(50));

					AssertGeneralInvariants(list);
				}
			}
		}

        [Test]
        public void DisposeMakesIsCreatedReturnsFalse()
        {
            NativeLinkedList<int> list = new NativeLinkedList<int>(
                3,
                Allocator.Temp);
            bool isDisposed = false;
            try
            {
                Assert.That(list.IsCreated, Is.True);

                list.Dispose();
                isDisposed = true;

                Assert.That(list.IsCreated, Is.False);
                
                AssertGeneralInvariants(list);
            }
            finally
            {
                if (!isDisposed)
                {
                    list.Dispose();
                }
            }
        }
    }
}