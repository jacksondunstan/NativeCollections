//-----------------------------------------------------------------------
// <copyright file="TestNativeLinkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace JacksonDunstan.NativeCollections.Tests
{
    /// <summary>
    /// Unit tests for <see cref="NativeLinkedList{T}"/> and
	/// <see cref="NativeLinkedList{T}.Enumerator"/>
    /// </summary>
    public class TestNativeLinkedList
    {
		// Create a list of int with the given capacity
		private static NativeLinkedList<int> CreateList(int capacity)
		{
			return new NativeLinkedList<int>(capacity, Allocator.Temp);
		}

        // Assert general invariants of the type
        private static void AssertGeneralInvariants(NativeLinkedList<int> list)
        {
            // Length and capacity can't be negative
            Assert.That(list.Length, Is.GreaterThanOrEqualTo(0));
            Assert.That(list.Capacity, Is.GreaterThanOrEqualTo(0));
            
            // Length <= Capacity
            Assert.That(list.Length, Is.LessThanOrEqualTo(list.Capacity));
            
            // Either head and tail are both valid or both invalid
			if (list.Head.IsValid && !list.Tail.IsValid)
            {
                Assert.Fail("Head is valid but Tail is invalid");
            }
			if (!list.Head.IsValid && list.Tail.IsValid)
            {
                Assert.Fail("Tail is valid but Head is invalid");
            }
            
            // If not empty, head and tail must be valid. Otherwise, head and
            // tail must be invalid.
            Assert.That(
                list.Length > 0
					&& list.Head.IsValid
					&& list.Tail.IsValid
                || list.Length == 0
					&& !list.Head.IsValid
					&& !list.Tail.IsValid,
                Is.True);
            
            // Forward iteration must cover exactly Length steps and match the
            // array returned by ToArray.
            int[] values = list.ToArray();
            int numForwardIterations = 0;
			for (NativeLinkedList<int>.Enumerator e = list.Head;
                e.IsValid;
				e.MoveNext())
            {
                Assert.That(
                    values[numForwardIterations],
					Is.EqualTo(e.Current));
                numForwardIterations++;
            }
            Assert.That(numForwardIterations, Is.EqualTo(list.Length));
            
            // Backward iteration must cover exactly Length steps and match the
            // array returned by ToArray.
            int numBackwardIterations = 0;
			for (NativeLinkedList<int>.Enumerator e = list.Tail;
                e.IsValid;
                e.MovePrev())
            {
                Assert.That(
                    values[list.Length - 1 - numBackwardIterations],
                    Is.EqualTo(e.Current));
                numBackwardIterations++;
            }
            Assert.That(numBackwardIterations, Is.EqualTo(list.Length));
            
            // Forward and backward iteration must take the same number of steps
            Assert.That(
                numBackwardIterations,
                Is.EqualTo(numForwardIterations));
        }

		[Test]
		public void EmptyConstructorEnforcesMinimumCapacity()
		{
			using (NativeLinkedList<int> list = CreateList(1))
			{
				Assert.That(list.Capacity, Is.EqualTo(4));
			}
		}

		[Test]
		public void DefaultValuesConstructorEnforcesMinimumCapacityAndLength()
		{
			using (NativeLinkedList<int> list = new NativeLinkedList<int>(
				1,
				-1,
				Allocator.Temp))
			{
				Assert.That(list.Length, Is.EqualTo(0));
				Assert.That(list.Capacity, Is.EqualTo(4));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void DefaultValuesConstructorSetsAllValuesToDefault()
		{
			using (NativeLinkedList<int> list = new NativeLinkedList<int>(
				6,
				4,
				Allocator.Temp))
			{
				Assert.That(list.Length, Is.EqualTo(4));
				Assert.That(list.Capacity, Is.EqualTo(6));
				Assert.That(list, Is.EqualTo(new [] { 0, 0, 0, 0 }));
				AssertGeneralInvariants(list);
			}
		}
        
        [Test]
        public void PushBackIncreasesLengthAndCapacityReturnsAddedNodeEnumerator()
        {
            using (NativeLinkedList<int> list = CreateList(4))
            {
                Assert.That(list.Length, Is.EqualTo(0));
				Assert.That(list.Capacity, Is.EqualTo(4));
                
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
                Assert.That(list.Length, Is.EqualTo(1));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e10.Current, Is.EqualTo(10));
                
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e20.Current, Is.EqualTo(20));
                
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
                Assert.That(list.Length, Is.EqualTo(3));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e30.Current, Is.EqualTo(30));
                
				NativeLinkedList<int>.Enumerator e40 = list.PushBack(40);
                Assert.That(list.Length, Is.EqualTo(4));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e40.Current, Is.EqualTo(40));
                
				NativeLinkedList<int>.Enumerator e50 = list.PushBack(50);
				Assert.That(list.Length, Is.EqualTo(5));
				Assert.That(list.Capacity, Is.GreaterThan(4));
				Assert.That(e50.Current, Is.EqualTo(50));

                AssertGeneralInvariants(list);
            }
        }
        
		[Test]
		public void PushListBackIncreasesLengthAndCapacityReturnsHeadEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(4))
			{
				using (NativeLinkedList<int> pushList = CreateList(2))
				{
					pushList.PushBack(10);
					pushList.PushBack(20);

					NativeLinkedList<int>.Enumerator e = list.PushBack(pushList);

					Assert.That(list.Length, Is.EqualTo(2));
					Assert.That(list.Capacity, Is.EqualTo(4));
					Assert.That(e.Current, Is.EqualTo(10));
				}

				using (NativeLinkedList<int> pushList = CreateList(3))
				{
					pushList.PushBack(30);
					pushList.PushBack(40);
					pushList.PushBack(50);

					NativeLinkedList<int>.Enumerator e = list.PushBack(pushList);

					Assert.That(list.Length, Is.EqualTo(5));
					Assert.That(list.Capacity, Is.GreaterThan(4));
					Assert.That(e.Current, Is.EqualTo(30));
				}

				AssertGeneralInvariants(list);
			}
		}

        [Test]
        public void PushFrontIncreasesLengthAndCapacityReturnsAddedNodeEnumerator()
        {
			using (NativeLinkedList<int> list = CreateList(4))
            {
                Assert.That(list.Length, Is.EqualTo(0));
				Assert.That(list.Capacity, Is.EqualTo(4));

				NativeLinkedList<int>.Enumerator e10 = list.PushFront(10);
                Assert.That(list.Length, Is.EqualTo(1));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e10.Current, Is.EqualTo(10));

				NativeLinkedList<int>.Enumerator e20 = list.PushFront(20);
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e20.Current, Is.EqualTo(20));

				NativeLinkedList<int>.Enumerator e30 = list.PushFront(30);
                Assert.That(list.Length, Is.EqualTo(3));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e30.Current, Is.EqualTo(30));

				NativeLinkedList<int>.Enumerator e40 = list.PushFront(40);
                Assert.That(list.Length, Is.EqualTo(4));
				Assert.That(list.Capacity, Is.EqualTo(4));
				Assert.That(e40.Current, Is.EqualTo(40));

				NativeLinkedList<int>.Enumerator e50 = list.PushFront(50);
                Assert.That(list.Length, Is.EqualTo(5));
				Assert.That(list.Capacity, Is.GreaterThan(4));
				Assert.That(e50.Current, Is.EqualTo(50));

                AssertGeneralInvariants(list);
            }
        }
        
		[Test]
		public void InsertAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(20);
				list.PushBack(40);
				list.PushBack(50);

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(insert, 30);

				Assert.That(ret.Current, Is.EqualTo(30));
				Assert.That(
					list.ToArray(),
					Is.EqualTo(new[] { 10, 20, 30, 40, 50 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(40);

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(insert, 50);

				Assert.That(ret.Current, Is.EqualTo(50));
				Assert.That(list.Tail.Current, Is.EqualTo(50));
				Assert.That(
					list.ToArray(),
					Is.EqualTo(new[] { 10, 20, 30, 40, 50 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertListAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(20);
				list.PushBack(60);
				list.PushBack(70);

				using (NativeLinkedList<int> insertList = CreateList(3))
				{
					insertList.PushBack(30);
					insertList.PushBack(40);
					insertList.PushBack(50);

					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						insert,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(
						list.ToArray(),
						Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertListAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				list.PushBack(20);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(30);

				using (NativeLinkedList<int> insertList = CreateList(3))
				{
					insertList.PushBack(40);
					insertList.PushBack(50);
					insertList.PushBack(60);

					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						insert,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					Assert.That(
						list.ToArray(),
						Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertBeforeInsertsNodeBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				list.PushBack(20);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(40);
				list.PushBack(50);

				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(insert, 30);

				Assert.That(ret.Current, Is.EqualTo(30));
				Assert.That(
					list.ToArray(),
					Is.EqualTo(new[] { 10, 20, 30, 40, 50 }));
				Assert.That(
					list.ToArrayReverse(),
					Is.EqualTo(new[] { 50, 40, 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				NativeLinkedList<int>.Enumerator insert = list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				list.PushBack(50);

				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(insert, 10);

				Assert.That(ret.Current, Is.EqualTo(10));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(
					list.ToArray(),
					Is.EqualTo(new[] { 10, 20, 30, 40, 50 }));
				Assert.That(
					list.ToArrayReverse(),
					Is.EqualTo(new[] { 50, 40, 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertListBeforeInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				list.PushBack(10);
				NativeLinkedList<int>.Enumerator insert = list.PushBack(50);
				list.PushBack(60);
				list.PushBack(70);

				using (NativeLinkedList<int> insertList = CreateList(3))
				{
					insertList.PushBack(20);
					insertList.PushBack(30);
					insertList.PushBack(40);

					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						insert,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(
						list.ToArray(),
						Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertListBeforeTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateList(5))
			{
				NativeLinkedList<int>.Enumerator insert = list.PushBack(40);
				list.PushBack(50);
				list.PushBack(60);

				using (NativeLinkedList<int> insertList = CreateList(3))
				{
					insertList.PushBack(10);
					insertList.PushBack(20);
					insertList.PushBack(30);

					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						insert,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					Assert.That(
						list.ToArray(),
						Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void IndexerGetReturnsWhatIsSet()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);

				NativeLinkedList<int> copy = list;
				copy[1] = 200;
				int ret = copy[1];

				Assert.That(ret, Is.EqualTo(200));

				AssertGeneralInvariants(copy);
			}
		}

		[Test]
		public void SortNodeMemoryAddressesPreservesOrderButSortsMemory()
		{
			using (NativeLinkedList<int> list = new NativeLinkedList<int>(
				3,
				Allocator.Temp))
			{
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
				list.Remove(e10);
				// values = [ 30, 20, _ ]
				//             t   h
				NativeLinkedList<int>.Enumerator e5 = list.PushBack(5);
				// values = [ 30, 20, 5 ]
				//                 h   t

				list.SortNodeMemoryAddresses();

				Assert.That(list[0], Is.EqualTo(20));
				Assert.That(list[1], Is.EqualTo(30));
				Assert.That(list[2], Is.EqualTo(5));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(e5.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new [] { 20, 30, 5 }));

				AssertGeneralInvariants(list);
			}
		}
    
        [Test]
        public void GetHeadReturnsInvalidEnumeratorForEmptyList()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e = list.Head;
                
                Assert.That(e.IsValid, Is.False);
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void GetHeadReturnsEnumeratorForFirstNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Head;

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorReturnsEnumeratorThatMovesNextToFirstNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableVersionReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = (NativeLinkedList<int>.Enumerator)((IEnumerable)list).GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableVersionReturnsEnumeratorForFirstNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = (NativeLinkedList<int>.Enumerator)((IEnumerable)list).GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableTVersionReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = (NativeLinkedList<int>.Enumerator)((IEnumerable<int>)list).GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableTVersionReturnsEnumeratorForFirstNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = (NativeLinkedList<int>.Enumerator)((IEnumerable<int>)list).GetEnumerator();

				Assert.That(e.IsValid, Is.False);

				e.MoveNext();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ForeachTraversesList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				int[] expected = new [] { 10, 20, 30, 40 };
				int index = 0;
				foreach (int val in list)
				{
					Assert.That(val, Is.EqualTo(expected[index]));
					index++;
				}

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void LinqIsUsableWithNativeLinkedList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				IEnumerable<int> query = from val in list
					where val > 10 && val < 40
					select val * 100;

				Assert.That(query, Is.EqualTo(new [] { 2000, 3000 }));

				AssertGeneralInvariants(list);
			}
		}
    
        [Test]
        public void GetTailReturnsInvalidEnumeratorForEmptyList()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e = list.Tail;
                
                Assert.That(e.IsValid, Is.False);
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void GetTailReturnsEnumeratorForLastNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Tail;

				Assert.That(e.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}
        
        [Test]
        public void MoveNextMakesEnumeratorReferToNextNode()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Head;
                
                e.MoveNext();
                
                Assert.That(e.Current, Is.EqualTo(20));
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void MoveNextAtTailInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Tail;

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextWhenPreviouslyValidMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				NativeLinkedList<int>.Enumerator e = list.PushBack(40);
				e.MoveNext();

				e.MoveNext();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextReturnsEnumeratorToNextNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Head;

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.Current, Is.EqualTo(20));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextAtTailReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Tail;

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextWithInvalidEnumeratorReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextWhenPreviouslyValidReturnsHead()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				NativeLinkedList<int>.Enumerator e = list.PushBack(40);
				e.MoveNext();

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}
        
        [Test]
		public void MovePrevMakesEnumeratorReferToPreviousNode()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
                list.PushBack(10);
                list.PushBack(20);
                list.PushBack(30);
                list.PushBack(40);
                NativeLinkedList<int>.Enumerator e = list.Tail;
                
                e.MovePrev();
                
                Assert.That(e.Current, Is.EqualTo(30));
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void MovePrevAtHeadInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Head;

				e.MovePrev();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

				e.MovePrev();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevWhenPreviouslyValidMakesEnumeratorReferToTail()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				e.MovePrev();

				e.MovePrev();

				Assert.That(e.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevReturnsEnumeratorToPreviousNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Tail;

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevAtHeadReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = list.Head;

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevWithInvalidEnumeratorReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevWhenPreviouslyValidReturnsTail()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);
				e.MovePrev();

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MakeInvalidReturnsInvalidEnumerator()
		{
			NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

			Assert.That(e.IsValid, Is.False);
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsTrueForSameNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a == b, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = NativeLinkedList<int>.Enumerator.MakeInvalid();

				Assert.That(e == invalid, Is.False);
				Assert.That(invalid == e, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a == b, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				using (NativeLinkedList<int> listB = CreateList(3))
				{
					listB.PushBack(10);
					listB.PushBack(20);
					listB.PushBack(30);
					listB.PushBack(40);

					NativeLinkedList<int>.Enumerator a = listA.Head;
					NativeLinkedList<int>.Enumerator b = listB.Head;

					Assert.That(a == b, Is.False);

					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsTrueForSameNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a.Equals((object)b), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = NativeLinkedList<int>.Enumerator.MakeInvalid();

				Assert.That(e.Equals((object)invalid), Is.False);
				Assert.That(invalid.Equals((object)e), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a.Equals((object)b), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				using (NativeLinkedList<int> listB = CreateList(3))
				{
					listB.PushBack(10);
					listB.PushBack(20);
					listB.PushBack(30);
					listB.PushBack(40);

					NativeLinkedList<int>.Enumerator a = listA.Head;
					NativeLinkedList<int>.Enumerator b = listB.Head;

					Assert.That(a.Equals((object)b), Is.False);

					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseForNonEnumeratorType()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;

				Assert.That(a.Equals("not an enumerator"), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsTrueForSameNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a.Equals(b), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = NativeLinkedList<int>.Enumerator.MakeInvalid();

				Assert.That(e.Equals(invalid), Is.False);
				Assert.That(invalid.Equals(e), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a.Equals(b), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				using (NativeLinkedList<int> listB = CreateList(3))
				{
					listB.PushBack(10);
					listB.PushBack(20);
					listB.PushBack(30);
					listB.PushBack(40);

					NativeLinkedList<int>.Enumerator a = listA.Head;
					NativeLinkedList<int>.Enumerator b = listB.Head;

					Assert.That(a.Equals(b), Is.False);

					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsFalseForSameNode()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a != b, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = NativeLinkedList<int>.Enumerator.MakeInvalid();

				Assert.That(e != invalid, Is.True);
				Assert.That(invalid != e, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);
				list.PushBack(40);

				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a != b, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				using (NativeLinkedList<int> listB = CreateList(3))
				{
					listB.PushBack(10);
					listB.PushBack(20);
					listB.PushBack(30);
					listB.PushBack(40);

					NativeLinkedList<int>.Enumerator a = listA.Head;
					NativeLinkedList<int>.Enumerator b = listB.Head;

					Assert.That(a != b, Is.True);

					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
			}
		}

		[Test]
		public void EnumeratorGetHashCodeReturnsDifferentValuesForDifferentNodes()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator a = listA.PushBack(10);
				NativeLinkedList<int>.Enumerator b = listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				int aHashCode = a.GetHashCode();
				int bHashCode = b.GetHashCode();

				Assert.That(aHashCode, Is.Not.EqualTo(bHashCode));

				AssertGeneralInvariants(listA);
			}
		}

		[Test]
		public void EnumeratorDisposeDoesNotThrowException()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				NativeLinkedList<int>.Enumerator e = listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);

				Assert.That(() => e.Dispose(), Throws.Nothing);

				AssertGeneralInvariants(listA);
			}
		}

		[Test]
		public void ResetMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				NativeLinkedList<int>.Enumerator e = listA.PushBack(30);
				listA.PushBack(40);

				e.Reset();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(listA);
			}
		}

		[Test]
		public void ResetInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> listA = CreateList(3))
			{
				listA.PushBack(10);
				listA.PushBack(20);
				listA.PushBack(30);
				listA.PushBack(40);
				NativeLinkedList<int>.Enumerator e = NativeLinkedList<int>.Enumerator.MakeInvalid();

				e.Reset();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(listA);
			}
		}

		[Test]
		public void GetValueReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				NativeLinkedList<int>.Enumerator e = list.PushBack(30);
				list.PushBack(40);
				list.PushBack(50);

				Assert.That(e.Value, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetCurrentReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				NativeLinkedList<int>.Enumerator e = list.PushBack(30);
				list.PushBack(40);
				list.PushBack(50);

				Assert.That(e.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetIEnumeratorCurrentReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				NativeLinkedList<int>.Enumerator e = list.PushBack(30);
				list.PushBack(40);
				list.PushBack(50);

				Assert.That(((IEnumerator)e).Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

        [Test]
        public void SetValueSetsNodeValue()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
                list.PushBack(10);
                list.PushBack(20);
				NativeLinkedList<int>.Enumerator e = list.PushBack(30);
                list.PushBack(40);
                list.PushBack(50);
                
				e.Value = 100;

				Assert.That(e.Value, Is.EqualTo(100));
                
                AssertGeneralInvariants(list);
            }
		}

        [Test]
        public void RemoveDoesNothingWhenEnumeratorIsInvalid()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);

                NativeLinkedList<int>.Enumerator invalid = NativeLinkedList<int>.Enumerator.MakeInvalid();
                NativeLinkedList<int>.Enumerator ret = list.Remove(invalid);
                
                Assert.That(ret.IsValid, Is.False);
				Assert.That(e10.IsValid, Is.True);
				Assert.That(e20.IsValid, Is.True);
				Assert.That(e30.IsValid, Is.True);
				Assert.That(list.ToArray(), Is.EqualTo(new [] { 10, 20, 30 }));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveOnlyNodeEmptiesList()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e = list.PushBack(10);
                
                list.Remove(e);

                Assert.That(e.IsValid, Is.False);
				Assert.That(list.Head.IsValid, Is.False);
				Assert.That(list.Tail.IsValid, Is.False);
                Assert.That(list.Length, Is.EqualTo(0));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveHeadLeavesRemainingNodesReturnsNext()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
                
                NativeLinkedList<int>.Enumerator next = list.Remove(list.Head);
                
                Assert.That(next.Current, Is.EqualTo(20));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(20));
				Assert.That(list.Tail.Current, Is.EqualTo(30));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new [] { 20, 30 }));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveHeadWhenNotFirstElementLeavesRemainingNodesReturnsNext()
        {
			using (NativeLinkedList<int> list = CreateList(4))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
				NativeLinkedList<int>.Enumerator e40 = list.PushBack(40);
                // array is now [10, 20, 30, 40]
                // head = 0, tail = 3
                
                list.Remove(list.Head);
                // array is now [40, 20, 30, _]
                // head = 1, tail = 0
                
                NativeLinkedList<int>.Enumerator next = list.Remove(list.Head);
                // array is now [40, 30, _, _]
                // head = 1, tail = 0
                
                Assert.That(next.Current, Is.EqualTo(30));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(30));
				Assert.That(list.Head.Prev.IsValid, Is.False);
				Assert.That(list.Tail.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Next.IsValid, Is.False);
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(e40.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new[] { 30, 40 }));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
                
                NativeLinkedList<int>.Enumerator prev = list.Remove(list.Tail);
                
                Assert.That(prev.Current, Is.EqualTo(20));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(list.Tail.Current, Is.EqualTo(20));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new[] { 10, 20 }));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailWhenNotLastElementLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateList(4))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
				NativeLinkedList<int>.Enumerator e40 = list.PushBack(40);
                // array is now [10, 20, 30, 40]
                // head = 0, tail = 3
                
                list.Remove(list.Head);
                // array is now [40, 20, 30, _]
                // head = 1, tail = 0
                
                NativeLinkedList<int>.Enumerator prev = list.Remove(list.Tail);
                // array is now [30, 20, _, _]
                // head = 1, tail = 0
                
                Assert.That(prev.Current, Is.EqualTo(30));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(20));
				Assert.That(list.Head.Prev.IsValid, Is.False);
				Assert.That(list.Tail.Current, Is.EqualTo(30));
				Assert.That(list.Tail.Next.IsValid, Is.False);
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(e40.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new[] { 20, 30 }));
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveMiddleLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.PushBack(10);
				NativeLinkedList<int>.Enumerator e20 = list.PushBack(20);
				NativeLinkedList<int>.Enumerator e30 = list.PushBack(30);
                
                NativeLinkedList<int>.Enumerator prev = list.Remove(e20);
                
                Assert.That(prev.Current, Is.EqualTo(10));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(list.Tail.Current, Is.EqualTo(30));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				Assert.That(list.ToArray(), Is.EqualTo(new[] { 10, 30 }));
                
                AssertGeneralInvariants(list);
            }
        }

        [Test]
        public void ToArrayReturnsNodeValueInOrder()
        {
			using (NativeLinkedList<int> list = CreateList(3))
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
		public void ToArrayReverseReturnsNodeValueInReverseOrder()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);

				int[] arr = list.ToArrayReverse();

				Assert.That(arr, Is.EqualTo(new[] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void CopyToNativeArrayFillsGivenArray()
		{
			using (NativeLinkedList<int> list = CreateList(5))
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
					list.CopyToNativeArray(array, list.Head.Next.Next, 1, 3);

					Assert.That(array[0], Is.EqualTo(0));
					Assert.That(array[1], Is.EqualTo(30));
					Assert.That(array[2], Is.EqualTo(40));
					Assert.That(array[3], Is.EqualTo(50));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void CopyToNativeArrayReverseFillsGivenArrayWithReverseOrder()
		{
			using (NativeLinkedList<int> list = CreateList(5))
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
					list.CopyToNativeArrayReverse(
						array,
						list.Tail.Prev.Prev,
						1,
						3);

					Assert.That(array[0], Is.EqualTo(0));
					Assert.That(array[1], Is.EqualTo(30));
					Assert.That(array[2], Is.EqualTo(20));
					Assert.That(array[3], Is.EqualTo(10));

					AssertGeneralInvariants(list);
				}
			}
		}

        [Test]
        public void DisposeMakesIsCreatedReturnsFalse()
        {
			NativeLinkedList<int> list = CreateList(3);
            bool isDisposed = false;
            try
            {
                Assert.That(list.IsCreated, Is.True);

                list.Dispose();
                isDisposed = true;

                Assert.That(list.IsCreated, Is.False);
            }
            finally
            {
                if (!isDisposed)
                {
                    list.Dispose();
                }
            }
        }

		private struct TestJob : IJob
		{
			public NativeLinkedList<int> List;
			public NativeArray<int> Sum;

			public void Execute()
			{
				for (NativeLinkedList<int>.Enumerator e = List.Head;
					 e.IsValid;
				     e.MoveNext())
				{
					Sum[0] += e.Current;
				}
			}
		}

		[Test]
		public void JobCanIterateOverLinkedList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);

				using (NativeArray<int> sum = new NativeArray<int>(
					1,
					Allocator.Temp))
				{
					TestJob job = new TestJob { List = list, Sum = sum };
					job.Run();

					Assert.That(sum[0], Is.EqualTo(60));
				}

				AssertGeneralInvariants(list);
			}
		}

		private struct ParallelForTestJob : IJobParallelFor
		{
			public NativeLinkedList<int> List;
			public NativeArray<int> Sum;

			public void Execute(int index)
			{
				Sum[0] += List[index];
			}
		}

		[Test]
		public void ParallelForJobCanIterateOverLinkedList()
		{
			using (NativeLinkedList<int> list = CreateList(3))
			{
				list.PushBack(10);
				list.PushBack(20);
				list.PushBack(30);

				using (NativeArray<int> sum = new NativeArray<int>(
					1,
					Allocator.Temp))
				{
					ParallelForTestJob job = new ParallelForTestJob
					{
						List = list,
						Sum = sum
					};
					job.Run(list.Length);

					Assert.That(sum[0], Is.EqualTo(60));
				}

				AssertGeneralInvariants(list);
			}
		}
    }
}