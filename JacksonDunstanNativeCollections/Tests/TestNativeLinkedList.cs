//-----------------------------------------------------------------------
// <copyright file="TestNativeLinkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
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
		struct NonBlittableStruct
		{
// Ignore warning of unused field
#pragma warning disable CS0649
			public string String;
#pragma warning restore CS0649
		}

		// Create an empty list of int with the given capacity
		private static NativeLinkedList<int> CreateEmptyNativeLinkedList(
			int capacity)
		{
			return new NativeLinkedList<int>(capacity, Allocator.Temp);
		}

		// Create a list of int with the given node values
		private static NativeLinkedList<int> CreateNativeLinkedList(
			params int[] values)
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

		// Create a list of int with the given node values
		private static NativeLinkedList<int> CreateNativeLinkedList(
			Allocator allocator,
			params int[] values)
		{
			Assert.That(values, Is.Not.Null);
			NativeLinkedList<int> list = new NativeLinkedList<int>(
				values.Length,
				allocator);
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

		// Create an array of int with the given length
		private static NativeArray<int> CreateNativeArray(
			Allocator allocator,
			params int[] values)
		{
			Assert.That(values, Is.Not.Null);
			NativeArray<int> array = new NativeArray<int>(
				values.Length,
				allocator);
			for (int i = 0; i < values.Length; ++i)
			{
				array[i] = values[i];
			}
			return array;
		}

		// Assert general invariants of the type
		private static void AssertGeneralInvariants(NativeLinkedList<int> list)
		{
			// Reset this copy's safety check range
			list.TestUseOnlySetParallelForSafetyCheckRange();

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

		private static void AssertRequiresReadOrWriteAccess(
			NativeLinkedList<int> list,
			Action action)
		{
			list.TestUseOnlySetAllowReadAndWriteAccess(false);
			try
			{
				Assert.That(
					() => action(),
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				list.TestUseOnlySetAllowReadAndWriteAccess(true);
			}
		}

		private static void AssertRequiresReadOrWriteAccess(
			NativeLinkedList<int>.Enumerator enumerator,
			Action action)
		{
			enumerator.TestUseOnlySetAllowReadAndWriteAccess(false);
			try
			{
				Assert.That(
					() => action(),
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				enumerator.TestUseOnlySetAllowReadAndWriteAccess(true);
			}
		}

		[Test]
		public void EmptyConstructorEnforcesMinimumCapacity()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(1))
			{
				Assert.That(list.Capacity, Is.EqualTo(4));
			}
		}

// No test is necessary because C# 7.3 uses `where T : unmanaged`
#if !CSHARP_7_3_OR_NEWER
		[Test]
		public void EmptyConstructorThrowsExceptionIfTypeParamIsNotBlittable()
		{
			Assert.That(
				() => new NativeLinkedList<NonBlittableStruct>(4, Allocator.Temp),
				Throws.TypeOf<ArgumentException>());
		}
#endif

		[Test]
		public void EmptyConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(
				() => new NativeLinkedList<int>(4, Allocator.None),
				Throws.TypeOf<ArgumentException>());
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

// No test is necessary because C# 7.3 uses `where T : unmanaged`
#if !CSHARP_7_3_OR_NEWER
		[Test]
		public void DefaultValuesConstructorThrowsExceptionIfTypeParamIsNotBlittable()
		{
			Assert.That(
				() => new NativeLinkedList<NonBlittableStruct>(6, 4, Allocator.Temp),
				Throws.TypeOf<ArgumentException>());
		}
#endif

		[Test]
		public void DefaultValuesConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(
				() => new NativeLinkedList<int>(4, 4, Allocator.None),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void InsertAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(5))
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => list.InsertAfter(list.Head, 20),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertAfter(list.Head, 100));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertNativeLinkedListAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeLinkedList<int> insertList = CreateEmptyNativeLinkedList(1))
				{
					// List to insert into doesn't have full list bounds
					insertList.InsertAfter(insertList.Tail, 20);
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.InsertAfter(list.Head, insertList),
						Throws.TypeOf<IndexOutOfRangeException>());

					// List to insert doesn't have full list bounds
					list.TestUseOnlySetParallelForSafetyCheckRange();
					insertList.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.InsertAfter(list.Head, insertList),
						Throws.TypeOf<IndexOutOfRangeException>());

					insertList.TestUseOnlySetParallelForSafetyCheckRange();
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> insertList = CreateEmptyNativeLinkedList(1))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertAfter(list.Head, insertList));
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Head.Next,
						insertList.Head.Next.Next,
						insertList.Head.Next.Next.Next.Next);

					Assert.That(ret.Current, Is.EqualTo(30));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20))
			{
				NativeLinkedList<int>.Enumerator insert = list.InsertAfter(list.Tail, 30);

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						insert,
						insertList.Head.Next.Next.Next,
						insertList.Head.Next.Next.Next.Next.Next);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						insertList.Head.Next.Next.Next,
						insertList.Head.Next.Next.Next.Next.Next);

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
		public void InsertNativeLinkedListRangeAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

					Assert.That(
						() => list.InsertAfter(
							list.Head,
							insertList.Head,
							insertList.Head),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertAfter(
							list.Head,
							insertList.Head,
							insertList.Tail));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertNativeArrayAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

					Assert.That(
						() => list.InsertAfter(list.Head, array),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeArrayAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertAfter(list.Head, array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Head.Next,
						insertArray,
						2,
						3);

					Assert.That(ret.Current, Is.EqualTo(30));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArraRangeAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						insertArray,
						3,
						3);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				using (NativeArray<int> array = CreateNativeArray(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
						list.Tail,
						array,
						3,
						3);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

					Assert.That(
						() => list.InsertAfter(list.Head, array, 0, 1),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertAfter(list.Head, array, 0, 1));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertManagedArrayAfterThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.InsertAfter(list.Tail, null),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.InsertAfter(list.Head, new [] { 20 }),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertManagedArrayAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertAfter(list.Head, new [] { 20 }));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeAfterInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 60, 70))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Head.Next,
					new[] { 10, 20, 30, 40, 50, 60, 70 },
					2,
					3);

				Assert.That(ret.Current, Is.EqualTo(30));
				AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeAfterTailUpdatesTail()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Tail,
					new[] { 10, 20, 30, 40, 50, 60, 70 },
					3,
					3);

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 10, 20, 30, 40, 50, 60);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeAfterInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertAfter(
					list.Tail,
					new[] { 10, 20, 30, 40, 50, 60, 70 },
					3,
					3);

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Head.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 40, 50, 60);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArraRangeAfterThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.InsertAfter(list.Tail, null, 0, 1),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeAfterWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.InsertAfter(list.Head, new[] { 20 }, 0, 1),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertManagedArrayRangeAfterWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertAfter(list.Head, new [] { 20 }, 0, 1));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(2, 3);

				Assert.That(
					() => list.InsertBefore(list.Tail, 40),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertBefore(list.Head, 100));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertNativeLinkedListBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 50))
			{
				using (NativeLinkedList<int> insertList = CreateEmptyNativeLinkedList(20))
				{
					// List to insert into doesn't have full list bounds
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.InsertBefore(list.Tail, insertList),
						Throws.TypeOf<IndexOutOfRangeException>());

					// List to insert doesn't have full list bounds
					list.TestUseOnlySetParallelForSafetyCheckRange();
					insertList.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.InsertBefore(list.Tail, insertList),
						Throws.TypeOf<IndexOutOfRangeException>());

					insertList.TestUseOnlySetParallelForSafetyCheckRange();
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 20, 30, 40, 50, 60))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertBefore(list.Head, insertList));
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeBeforeInsertsNodeAfterGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 20, 30, 40, 50, 60))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head.Next,
						insertList.Head.Next.Next,
						insertList.Head.Next.Next.Next.Next);

					Assert.That(ret.Current, Is.EqualTo(40));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(0, 10, 20, 30, 40, 50))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head,
						insertList.Head.Next,
						insertList.Head.Next.Next.Next);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(list.Head.Current, Is.EqualTo(10));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Tail,
						insertList.Head.Next.Next.Next,
						insertList.Head.Next.Next.Next.Next.Next);

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
		public void InsertNativeLinkedListRangeBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 3);

					Assert.That(
						() => list.InsertBefore(list.Tail, insertList.Head, insertList.Head),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeLinkedListRangeBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> insertList = CreateNativeLinkedList(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertBefore(
							list.Head,
							insertList.Head,
							insertList.Head));
					AssertGeneralInvariants(list);
					AssertGeneralInvariants(insertList);
				}
			}
		}

		[Test]
		public void InsertNativeArrayBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head,
						insertArray);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(list.Head.Current, Is.EqualTo(10));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

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
				list.Clear(); // invalidate the enumerator

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
		public void InsertNativeArrayBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 3);

					Assert.That(
						() => list.InsertBefore(list.Tail, array),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeArrayBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertBefore(list.Head, array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeBeforeInsertsListBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head.Next,
						insertArray,
						1,
						3);

					Assert.That(ret.Current, Is.EqualTo(40));
					AssertListValues(list, 10, 20, 30, 40, 50, 60, 70);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				using (NativeArray<int> insertArray = CreateNativeArray(0, 10, 20, 30, 40))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Head,
						insertArray,
						1,
						3);

					Assert.That(ret.Current, Is.EqualTo(30));
					Assert.That(list.Head.Current, Is.EqualTo(10));
					AssertListValues(list, 10, 20, 30, 40, 50, 60);

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				using (NativeArray<int> array = CreateNativeArray(10, 20, 30, 40, 50, 60, 70))
				{
					NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
						list.Tail,
						array,
						3,
						3);

					Assert.That(ret.Current, Is.EqualTo(40));
					Assert.That(list.Head.Current, Is.EqualTo(40));
					Assert.That(list.Tail.Current, Is.EqualTo(60));
					AssertListValues(list, 40, 50, 60);
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 3);

					Assert.That(
						() => list.InsertBefore(list.Tail, array, 0, 1),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void InsertNativeArrayRangeBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(20))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.InsertBefore(list.Head, array, 0, 1));
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
				list.Clear(); // invalidate the enumerator

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
		public void InsertManagedArrayBeforeThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.InsertBefore(list.Tail, null),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 3);

				Assert.That(
					() => list.InsertBefore(list.Tail, new[] { 20 }),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertManagedArrayBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertBefore(list.Head, new [] { 100 }));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeInsertsNodeBeforeGivenEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 50, 60, 70))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Head.Next,
					new[] { 10, 20, 30, 40, 50, 60, 70 },
					1,
					3);

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(
					list,
					Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeHeadUpdatesHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(40, 50, 60))
			{
				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Head,
					new[] { 0, 10, 20, 30, 40 },
					1,
					3);

				Assert.That(ret.Current, Is.EqualTo(30));
				Assert.That(list.Head.Current, Is.EqualTo(10));
				Assert.That(
					list,
					Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60 }));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeInsertsIntoEmptyList()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				list.Clear(); // invalidate the enumerator

				NativeLinkedList<int>.Enumerator ret = list.InsertBefore(
					list.Tail,
					new[] { 10, 20, 30, 40, 50, 60, 70 },
					3,
					3);

				Assert.That(ret.Current, Is.EqualTo(40));
				Assert.That(list.Head.Current, Is.EqualTo(40));
				Assert.That(list.Tail.Current, Is.EqualTo(60));
				AssertListValues(list, 40, 50, 60);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.InsertBefore(list.Tail, null, 0, 1),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 3);

				Assert.That(
					() => list.InsertBefore(list.Tail, new[] { 20 }, 0, 1),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void InsertManagedArrayRangeBeforeWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.InsertBefore(list.Head, new [] { 100 }, 0, 1));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IndexerGetReturnsValueAtGivenIndex()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list[1], Is.EqualTo(20));
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void IndexerGetCannotReadOutOfParallelForSafetyBounds()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(() => list[2], Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void IndexerGetWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				int val;

				AssertRequiresReadOrWriteAccess(
					list,
					() => val = list[0]);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IndexerSetChangesValueAtGivenIndex()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				// Need to make a copy because we can't change a 'using' var
				NativeLinkedList<int> copy = list;

				copy[1] = 200;

				Assert.That(list.Head.Next.Value, Is.EqualTo(200));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IndexerSetCannotWritOutOfParallelForSafetyBounds()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				// Need to make a copy because we can't change a 'using' var
				NativeLinkedList<int> copy = list;
				copy.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(
					() => copy[2] = 200,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void IndexerSetWhenReadOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				// Need to make a copy because we can't change a 'using' var
				NativeLinkedList<int> copy = list;

				AssertRequiresReadOrWriteAccess(
					copy,
					() => copy[0] = 100);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorAtIndexReturnsEnumeratorForTheGivenIndex()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void GetEnumeratorAtIndexWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = list.GetEnumeratorAtIndex(1));
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
				//			 t   h
				NativeLinkedList<int>.Enumerator e5 = list.InsertAfter(list.Tail, 5);
				// values = [ 30, 20, 5 ]
				//				 h   t

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
		public void SortNodeMemoryAddressesPreservesOrderButSortsMemoryRemoveAndInsertIntermediateNode()
		{
			using (var list = new NativeLinkedList<int>(7, Allocator.Temp))
			{
				var e00 = list.InsertAfter(list.Tail, 0);
				var e10 = list.InsertAfter(list.Tail, 10);
				var e20 = list.InsertAfter(list.Tail, 20);
				var e30 = list.InsertAfter(list.Tail, 30);
				var e40 = list.InsertAfter(list.Tail, 40);
				var e50 = list.InsertAfter(list.Tail, 50);
				var e60 = list.InsertAfter(list.Tail, 60);
				// values = [ h-0, 10, 20, 30, 40, 50, 60-t ]

				list.Remove(e30);
				// values = [ h-0, 10, 20, 60-t, 40, 50, _ ]

				list.SortNodeMemoryAddresses();
				// values = [ h-0, 10, 20, 40, 50, 60-t, _ ]

				// Same as removing e20 but the enumerator is invalid at this point.
				list.Remove(list.GetEnumeratorAtIndex(2));
				// values = [ h-0, 10, 60-t, 40, 50, _, _ ]

				list.SortNodeMemoryAddresses();
				// values = [ h-0, 10, 40, 50, 60-t, _, _ ]

				Assert.That(list[0], Is.EqualTo(0));
				Assert.That(list[1], Is.EqualTo(10));
				Assert.That(list[2], Is.EqualTo(40));
				Assert.That(list[3], Is.EqualTo(50));
				Assert.That(list[4], Is.EqualTo(60));
				Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
				Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
				Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(40));
				Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(50));
				Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(60));
				AssertListValues(list, 0, 10, 40, 50, 60);

				var e15 = list.InsertAfter(list.GetEnumeratorAtIndex(1), 15);
				// values = [ h-0, 10, 40, 50, 60-t, 15, _ ]

				Assert.That(list[0], Is.EqualTo(0));
				Assert.That(list[1], Is.EqualTo(10));
				Assert.That(list[2], Is.EqualTo(40));
				Assert.That(list[3], Is.EqualTo(50));
				Assert.That(list[4], Is.EqualTo(60));
				Assert.That(list[5], Is.EqualTo(15));
				Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
				Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
				Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(40));
				Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(50));
				Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(60));
				Assert.That(list.GetEnumeratorAtIndex(5).Current, Is.EqualTo(15));
				AssertListValues(list, 0, 10, 15, 40, 50, 60);

				list.SortNodeMemoryAddresses();
				// values = [ h-0, 10, 15, 40, 50, 60-t, _ ]

				Assert.That(list[0], Is.EqualTo(0));
				Assert.That(list[1], Is.EqualTo(10));
				Assert.That(list[2], Is.EqualTo(15));
				Assert.That(list[3], Is.EqualTo(40));
				Assert.That(list[4], Is.EqualTo(50));
				Assert.That(list[5], Is.EqualTo(60));
				Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
				Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
				Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(15));
				Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(40));
				Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(50));
				Assert.That(list.GetEnumeratorAtIndex(5).Current, Is.EqualTo(60));
				AssertListValues(list, 0, 10, 15, 40, 50, 60);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void SortNodeMemoryAddressesPreservesOrderButSortsMemoryRemoveIntermediateNode()
		{
			using var list = new NativeLinkedList<int>(7, Allocator.Temp);
			var e00 = list.InsertAfter(list.Tail, 0);
			var e10 = list.InsertAfter(list.Tail, 10);
			var e20 = list.InsertAfter(list.Tail, 20);
			var e30 = list.InsertAfter(list.Tail, 30);
			var e40 = list.InsertAfter(list.Tail, 40);
			var e50 = list.InsertAfter(list.Tail, 50);
			var e60 = list.InsertAfter(list.Tail, 60);
			// values = [ h-0, 10, 20, 30, 40, 50, 60-t ]

			AssertListValues(list, 0, 10, 20, 30, 40, 50, 60);

			list.Remove(e30);
			// values = [ h-0, 10, 20, 60-t, 40, 50, _ ]

			Assert.That(list[0], Is.EqualTo(0));
			Assert.That(list[1], Is.EqualTo(10));
			Assert.That(list[2], Is.EqualTo(20));
			Assert.That(list[3], Is.EqualTo(60));
			Assert.That(list[4], Is.EqualTo(40));
			Assert.That(list[5], Is.EqualTo(50));
			Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
			Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
			Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(20));
			Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(60));
			Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(40));
			Assert.That(list.GetEnumeratorAtIndex(5).Current, Is.EqualTo(50));
			AssertListValues(list, 0, 10, 20, 40, 50, 60);

			list.SortNodeMemoryAddresses();
			// values = [ h-0, 10, 20, 40, 50, 60-t, _ ]

			Assert.That(list[0], Is.EqualTo(0));
			Assert.That(list[1], Is.EqualTo(10));
			Assert.That(list[2], Is.EqualTo(20));
			Assert.That(list[3], Is.EqualTo(40));
			Assert.That(list[4], Is.EqualTo(50));
			Assert.That(list[5], Is.EqualTo(60));
			Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
			Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
			Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(20));
			Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(40));
			Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(50));
			Assert.That(list.GetEnumeratorAtIndex(5).Current, Is.EqualTo(60));
			AssertListValues(list, 0, 10, 20, 40, 50, 60);

			list.Remove(list.GetEnumeratorAtIndex(2));
			// values = [ h-0, 10, 60-t, 40, 50, _, _ ]

			Assert.That(list[0], Is.EqualTo(0));
			Assert.That(list[1], Is.EqualTo(10));
			Assert.That(list[2], Is.EqualTo(60));
			Assert.That(list[3], Is.EqualTo(40));
			Assert.That(list[4], Is.EqualTo(50));
			Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
			Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
			Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(60));
			Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(40));
			Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(50));
			AssertListValues(list, 0, 10, 40, 50, 60);

			var e70 = list.InsertAfter(list.Tail, 70);
			// values = [ h-0, 10, 60, 40, 50, 70-t, _ ]

			var e80 = list.InsertAfter(list.Tail, 80);
			// values = [ h-0, 10, 60, 40, 50, 70, 80-t ]

			list.SortNodeMemoryAddresses();
			// values = [ h-0, 10, 40, 50, 60, 70, 80-t ]

			Assert.That(list[0], Is.EqualTo(0));
			Assert.That(list[1], Is.EqualTo(10));
			Assert.That(list[2], Is.EqualTo(40));
			Assert.That(list[3], Is.EqualTo(50));
			Assert.That(list[4], Is.EqualTo(60));
			Assert.That(list[5], Is.EqualTo(70));
			Assert.That(list[6], Is.EqualTo(80));
			Assert.That(list.GetEnumeratorAtIndex(0).Current, Is.EqualTo(0));
			Assert.That(list.GetEnumeratorAtIndex(1).Current, Is.EqualTo(10));
			Assert.That(list.GetEnumeratorAtIndex(2).Current, Is.EqualTo(40));
			Assert.That(list.GetEnumeratorAtIndex(3).Current, Is.EqualTo(50));
			Assert.That(list.GetEnumeratorAtIndex(4).Current, Is.EqualTo(60));
			Assert.That(list.GetEnumeratorAtIndex(5).Current, Is.EqualTo(70));
			Assert.That(list.GetEnumeratorAtIndex(6).Current, Is.EqualTo(80));
			AssertListValues(list, 0, 10, 40, 50, 60, 70, 80);

			AssertGeneralInvariants(list);
		}

		[Test]
		public void SortNodeMemoryAddressesWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.SortNodeMemoryAddresses(),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void SortNodeMemoryAddressesWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.SortNodeMemoryAddresses());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetCapacityReturnsGreaterValueThanLength()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(4))
			{
				list.InsertAfter(list.Tail, 10);

				Assert.That(list.Capacity, Is.GreaterThan(list.Length));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetCapacityWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				int val;

				AssertRequiresReadOrWriteAccess(
					list,
					() => val = list.Capacity);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetLengthReturnsSmallerValueThanCapacity()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(4))
			{
				list.InsertAfter(list.Tail, 10);

				Assert.That(list.Length, Is.LessThan(list.Capacity));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetLengthWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				int val;

				AssertRequiresReadOrWriteAccess(
					list,
					() => val = list.Length);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetHeadReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				
				Assert.That(e.IsValid, Is.False);
				
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetHeadWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = list.Head);
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void GetEnumeratorWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = list.GetEnumerator());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableVersionReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void GetEnumeratorIEnumerableVersionWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = (NativeLinkedList<int>.Enumerator)((IEnumerable)list).GetEnumerator());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableTVersionReturnsInvalidEnumeratorForEmptyList()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void GetEnumeratorIEnumerableTVersionWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = (NativeLinkedList<int>.Enumerator)((IEnumerable<int>)list).GetEnumerator());
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void GetTailWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					list,
					() => e = list.Tail);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetListReturnsListEnumeratorIsFor()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				NativeLinkedList<int> ret = list.Head.List;

				Assert.That(ret.Head == list.Head, Is.True);

				AssertGeneralInvariants(list);
			}
		}
		
		[Test]
		public void EnumeratorMoveNextMakesEnumeratorReferToNextNode()
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
		public void EnumeratorMoveNextAtTailInvalidatesEnumerator()
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
		public void EnumeratorMoveNextWithInvalidEnumeratorKeepsEnumeratorInvalid()
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
		public void EnumeratorMoveNextWhenPreviouslyValidMakesEnumeratorReferToHead()
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
		public void EnumeratorMoveNextWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				e.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(
					() => e.MoveNext(),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMoveNextNumStepsMakesEnumeratorReferToNextNextNode()
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
		public void EnumeratorMoveNextNumStepsBeyondTailInvalidatesEnumerator()
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
		public void EnumeratorMoveNextNumStepsWithInvalidEnumeratorKeepsEnumeratorInvalid()
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
		public void EnumeratorMoveNextNumStepsWhenPreviouslyValidMakesEnumeratorReferToHead()
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
		public void EnumeratorMoveNextNumStepsWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				e.TestUseOnlySetParallelForSafetyCheckRange(0, 0);

				Assert.That(
					() => e.MoveNext(2),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMoveNextNumStepsWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.MoveNext());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMoveNextWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.MoveNext());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetNextReturnsEnumeratorToNextNode()
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
		public void EnumeratorGetNextAtTailReturnsInvalidEnumerator()
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
		public void EnumeratorGetNextWithInvalidEnumeratorReturnsInvalidEnumerator()
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
		public void EnumeratorGetNextWhenPreviouslyValidReturnsHead()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.InsertAfter(list.Tail, 40);
				e.MoveNext();

				NativeLinkedList<int>.Enumerator ret = e.Next;

				Assert.That(ret.Current, Is.EqualTo(10));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetNextWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				e.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(
					() => e.Next,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetNextWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e = e.Next);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMovePrevMakesEnumeratorReferToPreviousNode()
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
		public void EnumeratorMovePrevAtHeadInvalidatesEnumerator()
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
		public void EnumeratorMovePrevWithInvalidEnumeratorKeepsEnumeratorInvalid()
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
		public void EnumeratorMovePrevWhenPreviouslyValidMakesEnumeratorReferToTail()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorMovePrevWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				e.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(
					() => e.MovePrev(),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMovePrevWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.MovePrev());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMovePrevNumStepsMakesEnumeratorReferToPreviousNode()
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
		public void EnumeratorMovePrevNumStepsAtHeadInvalidatesEnumerator()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorMovePrevNumStepsWithInvalidEnumeratorKeepsEnumeratorInvalid()
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
		public void EnumeratorMovePrevNumStepsWhenPreviouslyValidMakesEnumeratorReferToTail()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorMovePrevNumStepsWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				e.TestUseOnlySetParallelForSafetyCheckRange(2, 2);

				Assert.That(
					() => e.MovePrev(2),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorMovePrevNumStepsWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.MovePrev(1));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetPrevReturnsEnumeratorToPreviousNode()
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
		public void EnumeratorGetPrevAtHeadReturnsInvalidEnumerator()
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
		public void EnumeratorGetPrevWithInvalidEnumeratorReturnsInvalidEnumerator()
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
		public void EnumeratorGetPrevWhenPreviouslyValidReturnsTail()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorGetPrevWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;
				e.TestUseOnlySetParallelForSafetyCheckRange(0, 1);

				Assert.That(
					() => e.Prev,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetPrevWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Tail;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e = e.Prev);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidReturnsFalseForDefaultEnumerator()
		{
			NativeLinkedList<int>.Enumerator e = default(NativeLinkedList<int>.Enumerator);

			Assert.That(e.IsValid, Is.False);
		}

		[Test]
		public void EnumeratorIsValidWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				bool valid = false;

				AssertRequiresReadOrWriteAccess(
					e,
					() => valid = e.IsValid);
				AssertGeneralInvariants(list);
			}
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
		public void EnumeratorEqualityOperatorWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator eA = listA.Head;
					NativeLinkedList<int>.Enumerator eB = listB.Head;
					bool isEqual = false;

					AssertRequiresReadOrWriteAccess(
						eA,
						() => isEqual = eA == listB.Head);
					AssertRequiresReadOrWriteAccess(
						eB,
						() => isEqual = eB == listA.Head);
					Assert.That(isEqual, Is.False);
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
		public void EnumeratorEqualsWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator eA = listA.Head;
					NativeLinkedList<int>.Enumerator eB = listB.Head;
					bool isEqual = false;

					AssertRequiresReadOrWriteAccess(
						eA,
						() => isEqual = eA.Equals((object)listB.Head));
					AssertRequiresReadOrWriteAccess(
						eB,
						() => isEqual = eB.Equals((object)listA.Head));
					Assert.That(isEqual, Is.False);
					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
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
		public void EnumeratorGenericEqualsWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator eA = listA.Head;
					NativeLinkedList<int>.Enumerator eB = listB.Head;
					bool isEqual = false;

					AssertRequiresReadOrWriteAccess(
						eA,
						() => isEqual = eA.Equals(listB.Head));
					AssertRequiresReadOrWriteAccess(
						eB,
						() => isEqual = eB.Equals(listA.Head));
					Assert.That(isEqual, Is.False);
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
		public void EnumeratorInequalityOperatorWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> listA = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> listB = CreateNativeLinkedList(10, 20, 30))
				{
					NativeLinkedList<int>.Enumerator eA = listA.Head;
					NativeLinkedList<int>.Enumerator eB = listB.Head;
					bool isNotEqual = false;

					AssertRequiresReadOrWriteAccess(
						eA,
						() => isNotEqual = eA != listB.Head);
					AssertRequiresReadOrWriteAccess(
						eB,
						() => isNotEqual = eB != listA.Head);
					Assert.That(isNotEqual, Is.False);
					AssertGeneralInvariants(listA);
					AssertGeneralInvariants(listB);
				}
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsZeroForSameEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int distance = list.Head.GetDistance(list.Head);

				Assert.That(distance, Is.EqualTo(0));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsNegativeForInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int distance = list.Head.Prev.GetDistance(list.Head);

				Assert.That(distance, Is.LessThan(0));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsNegativeGivenInvalidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int distance = list.Head.GetDistance(list.Head.Prev);

				Assert.That(distance, Is.LessThan(0));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsNegativeForDifferentLists()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeLinkedList<int> otherList = CreateNativeLinkedList(10, 20, 30))
				{
					int distance = list.Head.GetDistance(otherList.Head);

					Assert.That(distance, Is.LessThan(0));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsNegativeForBehindEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int distance = list.Tail.GetDistance(list.Head);

				Assert.That(distance, Is.LessThan(0));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceReturnsPositiveForAheadEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				int distance = list.Head.GetDistance(list.Tail);

				Assert.That(distance, Is.EqualTo(2));

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator start = list.Head;
				start.TestUseOnlySetParallelForSafetyCheckRange(0, 0);

				Assert.That(
					() => start.GetDistance(list.Tail),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetDistanceWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.GetDistance(list.Tail));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetHashCodeReturnsDifferentValuesForDifferentNodes()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorResetMakesEnumeratorReferToHead()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorResetInvalidEnumeratorKeepsEnumeratorInvalid()
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
		public void EnumeratorResetWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.Reset());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetValueReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorGetValueWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				e.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => e.Value,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetValueWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				int val;

				AssertRequiresReadOrWriteAccess(
					e,
					() => val = e.Value);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetCurrentReturnsNodeValue()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorGetCurrentWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				e.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => e.Current,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetCurrentWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				int val;

				AssertRequiresReadOrWriteAccess(
					e,
					() => val = e.Current);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidReturnsTrueForValidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.IsValid, Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidReturnsFalseWhenIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.Prev.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidReturnsFalseWhenIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Tail.Next.IsValid, Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidReturnsFalseAfterEnumeratorsAreInvalidated()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void EnumeratorIsValidForReturnsTrueForValidEnumerator()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.IsValidFor(list), Is.True);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidForReturnsFalseWhenIndexIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Head.Prev.IsValidFor(list), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidForReturnsFalseWhenIndexIsTooLarge()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(list.Tail.Next.IsValidFor(list), Is.False);

				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorIsValidForReturnsFalseForDetaultEnumerator()
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
		public void EnumeratorIsValidForReturnsFalseAfterEnumeratorsAreInvalidated()
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
		public void EnumeratorIsValidForReturnsFalseForOtherList()
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
		public void EnumeratorIsValidForWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.IsValidFor(list));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetIEnumeratorCurrentReturnsNodeValue()
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
		public void EnumeratorGetIEnumeratorCurrentWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				e.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => ((IEnumerator)e).Current,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorGetIEnumeratorCurrentWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				object val;

				AssertRequiresReadOrWriteAccess(
					e,
					() => val = ((IEnumerator)e).Current);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorSetValueSetsNodeValue()
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
		public void EnumeratorSetValueWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;
				e.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => e.Value = 100,
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void EnumeratorSetValueWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				NativeLinkedList<int>.Enumerator e = list.Head;

				AssertRequiresReadOrWriteAccess(
					e,
					() => e.Value = 100);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void RemoveDoesNothingWhenEnumeratorIsInvalid()
		{
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(10))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(4))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(4))
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
			using (NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3))
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
		public void RemoveWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.Remove(list.Head),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void RemoveWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Remove(list.Head));
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ClearRemovesAllNodes()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.Clear();

				AssertListValues(list);
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ClearWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.Clear(),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void ClearWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Clear());
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
		public void SwapWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

				Assert.That(
					() => list.Swap(list.Head, list.Tail),
					Throws.TypeOf<IndexOutOfRangeException>());
				Assert.That(
					() => list.Swap(list.Tail, list.Head),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void SwapWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Swap(list.Head, list.Tail));
				AssertGeneralInvariants(list);
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
		public void ToArrayWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);
				int[] array = new int[5];

				Assert.That(
					() => list.ToArray(array, list.Head, 0, 1),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void ToArrayWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.ToArray());
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
		public void ToArrayFullThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.ToArrayFull(null),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayFullWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.ToArrayFull(new int[list.Length]),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void ToArrayFullWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.ToArrayFull(new int[list.Length]));
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
		public void ToArrayReverseWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);
				int[] array = new int[5];

				Assert.That(
					() => list.ToArrayReverse(array, list.Head, 0, 1),
					Throws.TypeOf<IndexOutOfRangeException>());
				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void ToArrayReverseWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.ToArrayReverse());
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
		public void ToArrayFullReverseThrowsWhenArrayIsNull()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				Assert.That(
					() => list.ToArrayFullReverse(null),
					Throws.TypeOf<ArgumentNullException>());
				AssertGeneralInvariants(list);
			}
		}

		[Test]
		public void ToArrayFulReverseWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.ToArrayFullReverse(new int[list.Length]),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void ToArrayFullReverseWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.ToArrayFullReverse(new int[list.Length]));
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
		public void ToNativeArrayWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.ToNativeArray(array, list.Head, 0, 1),
						Throws.TypeOf<IndexOutOfRangeException>());
					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void ToNativeArrayWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.ToNativeArray(array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.ToNativeArrayFull(array);

					Assert.That(array, Is.EqualTo(new [] { 10, 20, 30, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

					Assert.That(
						() => list.ToNativeArrayFull(array),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void ToNativeArrayFullWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.ToNativeArrayFull(array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = list.ToNativeArrayReverse())
				{
					Assert.That(array, Is.EqualTo(new [] { 30, 20, 10 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArrayReverse(
						array,
						list.Tail,
						0,
						-1);

					AssertSameNativeArrays(array, ret);
					Assert.That(array, Is.EqualTo(new [] { 30, 20, 10 }));
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
		public void ToNativeArrayReverseWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.ToNativeArrayReverse(array, list.Head, 0, 1),
						Throws.TypeOf<IndexOutOfRangeException>());
					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void ToNativeArrayReverseWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.ToNativeArrayReverse(array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullReverseCopiesListToStartOfArray()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.ToNativeArrayFullReverse(array);

					Assert.That(array, Is.EqualTo(new [] { 30, 20, 10, 0, 0, 0 }));

					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void ToNativeArrayFullReverseWithoutFullListSafetyCheckBoundsThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 30, 40, 50))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0, 0))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

					Assert.That(
						() => list.ToNativeArrayFullReverse(array),
						Throws.TypeOf<IndexOutOfRangeException>());

					AssertGeneralInvariants(list);
					list.TestUseOnlySetParallelForSafetyCheckRange();
				}
			}
		}

		[Test]
		public void ToNativeArraFullReverseWhenWriteOnlyThrowsException()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				using (NativeArray<int> array = CreateNativeArray(0, 0, 0, 0, 0))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.ToNativeArrayFullReverse(array));
					AssertGeneralInvariants(list);
				}
			}
		}

		[Test]
		public void PrepareForParallelForJobRequiresWriteAccess()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.PrepareForParallelForJob());
			}
		}

		[Test]
		public void PrepareForParallelForJobRequiresFullListAccess()
		{
			using (NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

				Assert.That(
					() => list.PrepareForParallelForJob(),
					Throws.TypeOf<IndexOutOfRangeException>());

				AssertGeneralInvariants(list);
				list.TestUseOnlySetParallelForSafetyCheckRange();
			}
		}

		[Test]
		public void DisposeMakesIsCreatedReturnFalse()
		{
			NativeLinkedList<int> list = CreateEmptyNativeLinkedList(3);
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

		[Test]
		public void DisposeWithoutFullListSafetyCheckBoundsThrowsException()
		{
			NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40);
			list.TestUseOnlySetParallelForSafetyCheckRange(0, 2);

			Assert.That(
				() => list.Dispose(),
				Throws.TypeOf<IndexOutOfRangeException>());
			AssertGeneralInvariants(list);

			list.TestUseOnlySetParallelForSafetyCheckRange();
			list.Dispose();
		}

		[Test]
		public void DisposeWhenReadOnlyThrowsException()
		{
			NativeLinkedList<int> list = CreateNativeLinkedList(10, 20, 30, 40);
			list.TestUseOnlySetAllowReadAndWriteAccess(false);

			AssertRequiresReadOrWriteAccess(
				list,
				() => list.Dispose());
			AssertGeneralInvariants(list);

			list.TestUseOnlySetAllowReadAndWriteAccess(true);
			list.Dispose();
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(
				Allocator.TempJob,
				10,
				20,
				30))
			{
				using (NativeArray<int> sum = CreateNativeArray(
					Allocator.TempJob,
					0))
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
			using (NativeLinkedList<int> list = CreateNativeLinkedList(
				Allocator.TempJob,
				10,
				20,
				30))
			{
				using (NativeArray<int> sum = CreateNativeArray(
					Allocator.TempJob,
					0))
				{
					list.PrepareForParallelForJob();
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