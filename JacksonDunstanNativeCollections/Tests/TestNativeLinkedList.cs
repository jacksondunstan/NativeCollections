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
		private static NativeLinkedList<int> CreateNativeLinkedList(int capacity)
		{
			return new NativeLinkedList<int>(capacity, Allocator.Temp);
		}

		// Create a list of int with the given capacity
		private static NativeLinkedList<int> CreateNativeLinkedList(params int[] values)
		{
			Assert.That(values, Is.Not.Null);
			NativeLinkedList<int> list = new NativeLinkedList<int>(
				values.Length,
				Allocator.Temp);
			for (int i = 0; i < values.Length; ++i)
			{
				list.InsertAfter(list.Tail, values[i]);
			}
			return list;
		}

		// Create an array of int with the given length
		private static NativeArray<int> CreateNativeArray(params int[] values)
		{
			Assert.That(values, Is.Not.Null);
			NativeArray<int> array = new NativeArray<int>(
				values.Length,
				Allocator.Temp);
			for (int i = 0; i < values.Length; ++i)
			{
				array[i] = values[i];
			}
			return array;
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

		private unsafe static void AssertSameNativeArrays(
			NativeArray<int> a,
			NativeArray<int> b)
		{
			// We need at least one element to perform this test
			if (a.Length == 0 || b.Length == 0)
			{
				Assert.Fail("Can't test empty arrays");
			}

			// Must have the same first element
			Assert.That(b[0], Is.EqualTo(a[0]));

			// Get the first element of A and modify it
			int aOriginal = a[0];
			int aModified = aOriginal + 1;
			a[0] = aModified;

			// Get the first element of B. It should have changed.
			int bModified = b[0];

			// Reset the first element of A
			a[0] = aOriginal;

			// Make sure that B was modified when A was
			Assert.That(bModified, Is.EqualTo(aModified));
		}

		private static void AssertNotSameNativeArrays(
			NativeArray<int> a,
			NativeArray<int> b)
		{
			// We need at least one element to perform this test
			if (a.Length == 0 || b.Length == 0)
			{
				Assert.Fail("Can't test empty arrays");
			}

			// Get the first element of A and B. If the same, increment to
			// modify it. If different, modify by using B's element.
			int aOriginal = a[0];
			int bOriginal = b[0];
			int aModified = aOriginal == bOriginal ? aOriginal + 1 : bOriginal;
			a[0] = aModified;

			// Get the first element of B. It should have changed.
			int bModified = b[0];

			// Reset the first element of A
			a[0] = aOriginal;

			// Make sure that B was modified when A was
			Assert.That(bModified, Is.EqualTo(aModified));
		}

		private static void AssertListValues(
			NativeLinkedList<int> list,
			params int[] values)
		{
			Assert.That(values, Is.Not.Null);
			Assert.That(list.ToArray(), Is.EqualTo(values));
		}

		[Test]
		public void EmptyConstructorEnforcesMinimumCapacity()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(1))
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
				AssertListValues(list, 0, 0, 0, 0);
				AssertGeneralInvariants(list);
			}
		}
        
		[Test]
		public void InsertAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(5))
			{
				list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator insert = list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 40);
				list.InsertAfter(list.Tail, 50);

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(insert, 30);

				Assert.That(ret.Current, Is.EqualTo(30));
				AssertListValues(list, 10, 20, 30, 40, 50);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Tail,
					50);

				Assert.That(ret.Current, Is.EqualTo(50));
				Assert.That(list.Tail.Current, Is.EqualTo(50));
				AssertListValues(list, 10, 20, 30, 40, 50);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					e,
					50);

				Assert.That(ret.Current, Is.EqualTo(50));
				Assert.That(list.Head.Current, Is.EqualTo(50));
				Assert.That(list.Tail.Current, Is.EqualTo(50));
				AssertListValues(list, 50);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertNativeLinkedListAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(30, 40, 50))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Head.Next,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(30));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20))
			{
				NativeLinkedList<int>.Enumerator insert = list.InsertAfter(list.Tail, 30);

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						insert,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeArrayAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(30, 40, 50))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Head.Next,
						insertArray);

					Assert.That(ret.Current, Is.EqualTo(30));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						insertArray);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				using (NativeArray<int> array = CreateNativeArray(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						array);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertManagedArrayAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Head.Next,
					new [] { 30, 40, 50 });

				Assert.That(ret.Current, Is.EqualTo(30));
				AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Tail,
					new [] { 40, 50, 60 });

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 10, 20, 30, 40, 50, 60);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Tail,
					new[] { 40, 50, 60 });

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Head.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 40, 50, 60);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertBeforeInsertsNodeBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 40, 50))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Tail.Prev,
					30);

				Assert.That(ret.Current, Is.EqualTo(30));
				AssertListValues(list, 10, 20, 30, 40, 50);
				Assert.That(
					list.ToArrayReverse(),
					Is.EqualTo(new[] { 50, 40, 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(20, 30, 40, 50))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Head,
					10);

				Assert.That(ret.Current, Is.EqualTo(10));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				AssertListValues(list, 10, 20, 30, 40, 50);
				Assert.That(
					list.ToArrayReverse(),
					Is.EqualTo(new[] { 50, 40, 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					e,
					50);

				Assert.That(ret.Current, Is.EqualTo(50));
				Assert.That(list.Head.Current, Is.EqualTo(50));
				Assert.That(list.Tail.Current, Is.EqualTo(50));
				AssertListValues(list, 50);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertNativeLinkedListBeforeInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(20, 30, 40))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head.Next,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(list.Head.Current, Is.EqualTo(10));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Tail,
						insertList);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeArrayBeforeInsertsListBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(20, 30, 40))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head.Next,
						insertArray);

					Assert.That(ret.Current, Is.EqualTo(40));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				using (NativeArray<int> array = CreateNativeArray(40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Tail,
						array);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertManagedArrayBeforeInsertsNodeBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Head.Next,
					new [] { 20, 30, 40 });

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(
					list,
					Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Head,
					new [] { 10, 20, 30 });

				Assert.That(ret.Current, Is.EqualTo(30));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(
					list,
					Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.RemoveAll(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Tail,
					new[] { 40, 50, 60 });

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Head.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 40, 50, 60);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IndexerGetReturnsWhatIsSet()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int> copy = list;
				copy[1] = 200;
				int ret = copy[1];

				Assert.That(ret, Is.EqualTo(200));

				AssertGeneralInvariants(copy);
			}
		}

		[Test]
		public void GetEnumeratorAtIndexReturnsEnumeratorForTheGivenIndex()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);

				NativeLinkedList<int>.Enumerator e = list.GetEnumeratorAtIndex(1);

				Assert.That(e, Is.EqualTo(e20));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void SortNodeMemoryAddressesPreservesOrderButSortsMemory()
		{
			using (NativeLinkedList<int> list = new NativeLinkedList<int>(
				3,
				Allocator.Temp))
			{
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
				list.Remove(e10);
				// values = [ 30, 20, _ ]
				//             t   h
				NativeLinkedList<int>.Enumerator e5 = list.InsertAfter(list.Tail, 5);
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
				AssertListValues(list, 20, 30, 5);

				AssertGeneralInvariants(list);
			}
		}
    
        [Test]
        public void GetHeadReturnsInvalidEnumeratorForEmptyList()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e = list.Head;
                
                Assert.That(e.IsValid, Is.False);
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void GetHeadReturnsEnumeratorForFirstNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				int[] expected = { 10, 20, 30, 40 };
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				IEnumerable<int> query = from val in list
					where val > 10 && val < 40
					select val * 100;

				Assert.That(query, Is.EqualTo(new[] { 2000, 3000 }));

				AssertGeneralInvariants(list);
			}
		}
    
        [Test]
        public void GetTailReturnsInvalidEnumeratorForEmptyList()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e = list.Tail;
                
                Assert.That(e.IsValid, Is.False);
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void GetTailReturnsEnumeratorForLastNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				Assert.That(e.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetListReturnsListEnumeratorIsFor()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int> ret = list.Head.List;

				Assert.That(ret.Head == list.Head, Is.True);

				AssertGeneralInvariants(list);
			}
		}
        
        [Test]
        public void MoveNextMakesEnumeratorReferToNextNode()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
            {
                NativeLinkedList<int>.Enumerator e = list.Head;
                
                e.MoveNext();
                
                Assert.That(e.Current, Is.EqualTo(20));
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void MoveNextAtTailInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Remove(list.Tail); // invalidate the enumerator

				e.MoveNext();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextWhenPreviouslyValidMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 40);
				e.MoveNext();

				e.MoveNext();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextNumStepsMakesEnumeratorReferToNextNextNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				e.MoveNext(2);

				Assert.That(e.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextNumStepsBeyondTailInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);

				e.MoveNext(2);

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextNumStepsWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Remove(list.Tail); // invalidate the enumerator

				e.MoveNext(2);

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MoveNextNumStepsWhenPreviouslyValidMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 40);
				e.MoveNext();

				e.MoveNext(2);

				Assert.That(e.Current, Is.EqualTo(20));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextReturnsEnumeratorToNextNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.Current, Is.EqualTo(20));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextAtTailReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextWithInvalidEnumeratorReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Remove(list.Tail); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetNextWhenPreviouslyValidReturnsHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 40);
				e.MoveNext();

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}
        
        [Test]
		public void MovePrevMakesEnumeratorReferToPreviousNode()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
            {
                NativeLinkedList<int>.Enumerator e = list.Tail;
                
                e.MovePrev();
                
                Assert.That(e.Current, Is.EqualTo(30));
                
                AssertGeneralInvariants(list);
            }
        }

		[Test]
		public void MovePrevAtHeadInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				e.MovePrev();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				list.Remove(list.Head); // invalidate the enumerator

				e.MovePrev();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevWhenPreviouslyValidMakesEnumeratorReferToTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);
				e.MovePrev();

				e.MovePrev();

				Assert.That(e.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevNumStepsMakesEnumeratorReferToPreviousNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				e.MovePrev(2);

				Assert.That(e.Current, Is.EqualTo(20));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevNumStepsAtHeadInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);

				e.MovePrev(2);

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevNumStepsWithInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				list.Remove(list.Head); // invalidate the enumerator

				e.MovePrev(2);

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void MovePrevNumStepsWhenPreviouslyValidMakesEnumeratorReferToTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);
				e.MovePrev();

				e.MovePrev(2);

				Assert.That(e.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevReturnsEnumeratorToPreviousNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevAtHeadReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevWithInvalidEnumeratorReturnsInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				list.Remove(list.Head); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetPrevWhenPreviouslyValidReturnsTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);
				e.MovePrev();

				NativeLinkedList<int>.Enumerator ret = e.Prev;

				Assert.That(ret.Current, Is.EqualTo(40));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidReturnsFalseForDefaultEnumerator()
		{
			NativeLinkedList<int>.Enumerator e = default(NativeLinkedList<int>.Enumerator);

			Assert.That(e.IsValid, Is.False);
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsTrueForSameNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a == b, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = default(NativeLinkedList<int>.Enumerator);

				Assert.That(e == invalid, Is.False);
				Assert.That(invalid == e, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a == b, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualityOperatorReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30, 40))
				{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a.Equals((object)b), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = default(NativeLinkedList<int>.Enumerator);

				Assert.That(e.Equals((object)invalid), Is.False);
				Assert.That(invalid.Equals((object)e), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a.Equals((object)b), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorEqualsReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30, 40))
				{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;

				Assert.That(a.Equals("not an enumerator"), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsTrueForSameNode()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a.Equals(b), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = default(NativeLinkedList<int>.Enumerator);

				Assert.That(e.Equals(invalid), Is.False);
				Assert.That(invalid.Equals(e), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a.Equals(b), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGenericEqualsReturnsFalseForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30, 40))
				{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = list.Head;

				Assert.That(a != b, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueWhenOneNodeIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				NativeLinkedList<int>.Enumerator invalid = default(NativeLinkedList<int>.Enumerator);

				Assert.That(e != invalid, Is.True);
				Assert.That(invalid != e, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator a = list.Head;
				NativeLinkedList<int>.Enumerator b = a.Next;

				Assert.That(a != b, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorInequalityOperatorReturnsTrueForDifferentLists()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30, 40))
				{
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator a = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator b = list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);

				int aHashCode = a.GetHashCode();
				int bHashCode = b.GetHashCode();

				Assert.That(aHashCode, Is.Not.EqualTo(bHashCode));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorDisposeDoesNotThrowException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);

				Assert.That(() => e.Dispose(), Throws.Nothing);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ResetMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);

				e.Reset();

				Assert.That(e.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ResetInvalidEnumeratorKeepsEnumeratorInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Remove(list.Tail); // invalidate the enumerator

				e.Reset();

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetValueReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);
				list.InsertAfter(list.Tail, 50);

				Assert.That(e.Value, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetCurrentReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 30);
				list.InsertAfter(list.Tail, 40);
				list.InsertAfter(list.Tail, 50);

				Assert.That(e.Current, Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidReturnsTrueForValidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.IsValid, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidReturnsFalseWhenIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.Prev.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidReturnsFalseWhenIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Tail.Next.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidReturnsFalseAfterEnumeratorsAreInvalidated()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
			{
				list.InsertAfter(list.Tail, 10);
				list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 30);

				list.Remove(list.Head); // invalidate enumerators

				Assert.That(e.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsTrueForValidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.IsValidFor(list), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsFalseWhenIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.Prev.IsValidFor(list), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsFalseWhenIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Tail.Next.IsValidFor(list), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsFalseForDetaultEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					default(NativeLinkedList<int>.Enumerator).IsValidFor(list),
					Is.False);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsFalseAfterEnumeratorsAreInvalidated()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				list.Remove(list.Head); // invalidate enumerators

				Assert.That(e.IsValidFor(list), Is.False);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IsValidForReturnsFalseForOtherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(10, 20, 30))
				{
					Assert.That(otherList.Tail.IsValidFor(list), Is.False);
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void GetIEnumeratorCurrentReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				Assert.That(
					((IEnumerator)list.Head.Next.Next).Current,
					Is.EqualTo(30));

				AssertGeneralInvariants(list);
			}
		}

        [Test]
        public void SetValueSetsNodeValue()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
            {
                NativeLinkedList<int>.Enumerator e = list.Head.Next.Next;
                
				e.Value = 100;

				Assert.That(e.Value, Is.EqualTo(100));
                
                AssertGeneralInvariants(list);
            }
		}

        [Test]
        public void RemoveDoesNothingWhenEnumeratorIsInvalid()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);

                NativeLinkedList<int>.Enumerator invalid = default(NativeLinkedList<int>.Enumerator);
                NativeLinkedList<int>.Enumerator ret = list.Remove(invalid);
                
                Assert.That(ret.IsValid, Is.False);
				Assert.That(e10.IsValid, Is.True);
				Assert.That(e20.IsValid, Is.True);
				Assert.That(e30.IsValid, Is.True);
				AssertListValues(list, 10, 20, 30);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveOnlyNodeEmptiesList()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10))
            {
				NativeLinkedList<int>.Enumerator e = list.Head;
                
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
                
                NativeLinkedList<int>.Enumerator next = list.Remove(list.Head);
                
                Assert.That(next.Current, Is.EqualTo(20));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(20));
				Assert.That(list.Tail.Current, Is.EqualTo(30));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				AssertListValues(list, 20, 30);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveHeadWhenNotFirstElementLeavesRemainingNodesReturnsNext()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(4))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
				NativeLinkedList<int>.Enumerator e40 = list.InsertAfter(list.Tail, 40);
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
				AssertListValues(list, 30, 40);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
                
                NativeLinkedList<int>.Enumerator prev = list.Remove(list.Tail);
                
                Assert.That(prev.Current, Is.EqualTo(20));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(list.Tail.Current, Is.EqualTo(20));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				AssertListValues(list, 10, 20);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveTailWhenNotLastElementLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(4))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
				NativeLinkedList<int>.Enumerator e40 = list.InsertAfter(list.Tail, 40);
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
				AssertListValues(list, 20, 30);
                
                AssertGeneralInvariants(list);
            }
        }
        
        [Test]
        public void RemoveMiddleLeavesRemainingNodesReturnsPrev()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(3))
            {
				NativeLinkedList<int>.Enumerator e10 = list.InsertAfter(list.Tail, 10);
				NativeLinkedList<int>.Enumerator e20 = list.InsertAfter(list.Tail, 20);
				NativeLinkedList<int>.Enumerator e30 = list.InsertAfter(list.Tail, 30);
                
                NativeLinkedList<int>.Enumerator prev = list.Remove(e20);
                
                Assert.That(prev.Current, Is.EqualTo(10));
                Assert.That(list.Length, Is.EqualTo(2));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(list.Tail.Current, Is.EqualTo(30));
				Assert.That(e10.IsValid, Is.False);
				Assert.That(e20.IsValid, Is.False);
				Assert.That(e30.IsValid, Is.False);
				AssertListValues(list, 10, 30);
                
                AssertGeneralInvariants(list);
            }
		}

		[Test]
		public void RemoveAllRemovesAllNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.RemoveAll();

				AssertListValues(list);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void SwapSwapsNodeValuesAndDoesNotChangeNextAndPrevPointers()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.Swap(list.Head, list.Tail);

				Assert.That(list[0], Is.EqualTo(30));
				Assert.That(list[1], Is.EqualTo(20));
				Assert.That(list[2], Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void SwapDoesNothingWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.Swap(default(NativeLinkedList<int>.Enumerator), list.Tail);

				AssertListValues(list, 10, 20, 30);

				list.Swap(list.Head, default(NativeLinkedList<int>.Enumerator));

				AssertListValues(list, 10, 20, 30);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void SwapDoesNothingWhenEnumeratorIsForAnotherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(100, 200, 300))
				{
					list.Swap(otherList.Head, list.Tail);

					AssertListValues(list, 10, 20, 30);

					list.Swap(list.Head, otherList.Tail);

					AssertListValues(list, 10, 20, 30);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(otherList);
				}
			}
		}

        [Test]
        public void ToArrayAllocatesAndCopiesWholeListByDefault()
        {
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
            {
                int[] arr = list.ToArray();
                
                Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));
                
                AssertGeneralInvariants(list);
            }
		}

		[Test]
		public void ToArrayUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, list.Head, 0, -1);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, list.Head, 0, 4);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayCopiesFromStartWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = list.ToArray(null, list.Head.Prev, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayCopiesFromStartWhenEnumeratorIsForOtherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(100, 200, 300))
				{
					int[] arr = list.ToArray(null, otherList.Head, 0, 3);

					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(otherList);
				}
			}
		}

		[Test]
		public void ToArrayAllocatesArrayWhenNullIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = list.ToArray(null, list.Head, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[2];

				int[] ret = list.ToArray(arr, list.Head, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 0, 0 }));
				Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, list.Head, -1, 3);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[6];

				list.ToArray(arr, list.Head, 4, 3);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayFullCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[6];

				list.ToArrayFull(arr);

				Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = list.ToArrayReverse();

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArrayReverse(arr, list.Tail, 0, -1);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArrayReverse(arr, list.Tail, 0, 4);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseCopiesFromEndWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = list.ToArrayReverse(null, list.Tail.Next, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseCopiesFromEndWhenEnumeratorIsForOtherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(100, 200, 300))
				{
					int[] arr = list.ToArrayReverse(null, otherList.Tail, 0, 3);

					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(otherList);
				}
			}
		}

		[Test]
		public void ToArrayReverseAllocatesArrayWhenNullIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = list.ToArrayReverse(null, list.Tail, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[2];

				int[] ret = list.ToArrayReverse(arr, list.Tail, 0, 3);

				Assert.That(arr, Is.EqualTo(new [] { 0, 0 }));
				Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArrayReverse(arr, list.Tail, -1, 3);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayReverseCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[6];

				list.ToArrayReverse(arr, list.Tail, 4, 3);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayFullReverseCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int[] arr = new int[6];

				list.ToArrayFullReverse(arr);

				Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToNativeArrayAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArray())
				{
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head,
						0,
						-1);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head,
						0,
						4);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesFromStartWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head.Prev,
						0,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesFromStartWhenEnumeratorIsForOtherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(100, 200, 300))
				{
					using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
					{
						NativeArray<int> ret = list.ToNativeArray(
							arr,
							otherList.Head,
							0,
							3);

						AssertSameNativeArrays(arr, ret);
						Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));
						Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

						AssertGeneralInvariants(list);
						AssertGeneralInvariants(otherList);
					}
				}
			}
		}

		[Test]
		public void ToNativeArrayAllocatesArrayWhenInvalidArrayIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArray(
					default(NativeArray<int>),
					list.Head,
					0,
					3))
				{
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0))
				{
					using (NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head,
						0,
						3))
					{
						AssertNotSameNativeArrays(arr, ret);
						Assert.That(arr, Is.EqualTo(new [] { 0, 0 }));
						Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));

						AssertGeneralInvariants(list);
					}
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head,
						-1,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30 }));
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						list.Head,
						4,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(ret, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));
					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.ToNativeArrayFull(arr);

					Assert.That(arr, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArrayReverse())
				{
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail,
						0,
						-1);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));
					Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail,
						0,
						4);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));
					Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseCopiesFromStartWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail.Next,
						0,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));
					Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseCopiesFromStartWhenEnumeratorIsForOtherList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(100, 200, 300))
				{
					using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
					{
						NativeArray<int> ret = list.ToNativeArrayReverse(
							arr,
							otherList.Tail,
							0,
							3);

						AssertSameNativeArrays(arr, ret);
						Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));
						Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

						AssertGeneralInvariants(list);
						AssertGeneralInvariants(otherList);
					}
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseAllocatesArrayWhenInvalidArrayIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArrayReverse(
					default(NativeArray<int>),
					list.Tail,
					0,
					3))
				{
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0))
				{
					using (NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail,
						0,
						3))
					{
						AssertNotSameNativeArrays(arr, ret);
						Assert.That(arr, Is.EqualTo(new [] { 0, 0 }));
						Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));

						AssertGeneralInvariants(list);
					}
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail,
						-1,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10 }));
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						arr,
						list.Tail,
						4,
						3);

					AssertSameNativeArrays(arr, ret);
					Assert.That(ret, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));
					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullReverseCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.ToNativeArrayFullReverse(arr);

					Assert.That(arr, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

        [Test]
        public void DisposeMakesIsCreatedReturnsFalse()
        {
			NativeLinkedList<int> list = CreateNativeLinkedList(3);
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> sum = CreateNativeArray(0))
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> sum = CreateNativeArray(0))
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