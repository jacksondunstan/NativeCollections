//-----------------------------------------------------------------------
// <copyright file="TestNativeChunkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
// </copyright>
//-----------------------------------------------------------------------

using NUnit.Framework;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace JacksonDunstan.NativeCollections.Tests
{
	/// <summary>
	/// Unit tests for <see cref="NativeChunkedList{T}"/> and
	/// <see cref="NativeChunkedList{T}.Enumerator"/>
	/// </summary>
	public class TestNativeChunkedList
	{
		struct NonBlittableStruct
		{
			public string String;
		}

		private static NativeChunkedList<int> CreateList(
			int numElementsPerChunk,
			int capacity)
		{
			return new NativeChunkedList<int>(
				numElementsPerChunk,
				capacity,
				Allocator.Temp);
		}

		private static NativeChunkedList<int> CreateListValues(
			params int[] values)
		{
			NativeChunkedList<int> list = new NativeChunkedList<int>(
				4,
				values.Length,
				Allocator.Temp);
			list.AddRange(values, 0, values.Length);
			return list;
		}

		private static NativeArray<int> CreateArrayValues(params int[] values)
		{
			return new NativeArray<int>(
				values,
				Allocator.Temp);
		}

		private static void AssertRequiresReadOrWriteAccess(
			NativeChunkedList<int> list,
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
			NativeChunkedList<int>.Enumerator enumerator,
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

		private static void AssertRequiresFullListAccess(
			NativeChunkedList<int> list,
			Action<NativeChunkedList<int>> action)
		{
			list.TestUseOnlySetParallelForSafetyCheckRange(1, list.Length-2);
			Assert.That(
				() => action(list),
				Throws.TypeOf<IndexOutOfRangeException>());
		}

		private static void AssertRequiresIndexInSafetyRange(
			NativeChunkedList<int> list,
			int minIndex,
			int maxIndex,
			Action<NativeChunkedList<int>> action)
		{
			list.TestUseOnlySetParallelForSafetyCheckRange(minIndex, maxIndex);
			Assert.That(
				() => action(list),
				Throws.TypeOf<IndexOutOfRangeException>());
		}

		private static void AssertRequiresNonNullArray(Action action)
		{
			Assert.That(
				() => action(),
				Throws.TypeOf<ArgumentNullException>());
		}

		private static void AssertRequiresValidArrayRange(Action action)
		{
			Assert.That(
				() => action(),
				Throws.TypeOf<IndexOutOfRangeException>());
		}

		private static void AddElements(
			NativeChunkedList<int> list,
			params int[] elements)
		{
			for (int i = 0; i < elements.Length; ++i)
			{
				list.Add(elements[i]);
			}
		}

		private static void AssertElements(
			NativeChunkedList<int> list,
			params int[] elements)
		{
			Assert.That(list.Length, Is.EqualTo(elements.Length));
			Assert.That(list.ToArray(), Is.EqualTo(elements));
		}

		[Test]
		public void ConstructorCreatesValidListForSingleChunk()
		{
			using (NativeChunkedList<int> list = CreateList(4, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void ConstructorCreatesValidListForMultipleChunks()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void ConstructorCreatesValidListForTooLowNumElementsPerChunk()
		{
			using (NativeChunkedList<int> list = CreateList(0, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void ConstructorCreatesValidListForTooLowCapacity()
		{
			using (NativeChunkedList<int> list = CreateList(4, 0))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void ConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(() =>
				new NativeChunkedList<int>(
					4,
					4,
					Allocator.None),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void ConstructorThrowsExceptionForNonBlittableType()
		{
			Assert.That(() =>
	            new NativeChunkedList<NonBlittableStruct>(
					4,
					4,
					Allocator.Temp),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void CapacityGetReturnsValuePassedToConstructor()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				Assert.That(list.Capacity, Is.EqualTo(4));
			}
		}

		[Test]
		public void CapacityGetRequiresReadAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				int capacity = 0;
				AssertRequiresReadOrWriteAccess(
					list,
					() => capacity = list.Capacity);
			}
		}

		[Test]
		public void CapacitySetDoesNothingForSameValue()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				NativeChunkedList<int> copy = list;
				AddElements(list, 10, 20, 30, 40);

				copy.Capacity = 4;

				Assert.That(list.Capacity, Is.EqualTo(4));
				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void CapacitySetRoundsUpToNextChunk()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				NativeChunkedList<int> copy = list;
				AddElements(list, 10, 20, 30, 40);

				copy.Capacity = 5;

				Assert.That(list.Capacity, Is.EqualTo(6));
				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void CapacitySetHandlesBigIncrease()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				NativeChunkedList<int> copy = list;
				AddElements(list, 10, 20, 30, 40);

				copy.Capacity = 400;

				Assert.That(list.Capacity, Is.EqualTo(400));
				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void CapacitySetShrinksLessThanOneChunk()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				NativeChunkedList<int> copy = list;
				AddElements(list, 10, 20, 30, 40);

				copy.Capacity = 3;

				Assert.That(list.Capacity, Is.EqualTo(4));
				AssertElements(list, 10, 20, 30);
			}
		}

		[Test]
		public void CapacitySetShrinksMoreThanOneChunk()
		{
			using (NativeChunkedList<int> list = CreateList(2, 6))
			{
				NativeChunkedList<int> copy = list;
				AddElements(list, 10, 20, 30, 40, 50, 60);

				copy.Capacity = 2;

				Assert.That(list.Capacity, Is.EqualTo(2));
				AssertElements(list, 10, 20);
			}
		}

		[Test]
		public void CapacitySetRequiresReadAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				NativeChunkedList<int> copy = list;
				AssertRequiresReadOrWriteAccess(
					list,
					() => copy.Capacity = 4);
			}
		}

		[Test]
		public void CapacitySetRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresFullListAccess(
					list,
					copy => copy.Capacity = 4);
			}
		}

		[Test]
		public void LengthGetRequiresReadAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				int length = 0;
				AssertRequiresReadOrWriteAccess(
					list,
					() => length = list.Length);
			}
		}

		[Test]
		public void GetEnumeratorReturnsManuallyUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				NativeChunkedList<int>.Enumerator e = list.GetEnumerator();

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(10));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(20));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(30));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(40));

				Assert.That(e.MoveNext(), Is.False);
			}
		}

		[Test]
		public void GetEnumeratorReturnsForeachUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				int index = 0;
				foreach (int element in list)
				{
					Assert.That(element, Is.EqualTo((index+1) * 10));
					index++;
				}
				Assert.That(index, Is.EqualTo(4));
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableReturnsManuallyUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				IEnumerator e = ((IEnumerable)list).GetEnumerator();

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(10));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(20));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(30));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(40));

				Assert.That(e.MoveNext(), Is.False);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableReturnsForeachUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				int index = 0;
				foreach (int element in (IEnumerable)list)
				{
					Assert.That(element, Is.EqualTo((index+1) * 10));
					index++;
				}
				Assert.That(index, Is.EqualTo(4));
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableTReturnsManuallyUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				IEnumerator<int> e = ((IEnumerable<int>)list).GetEnumerator();

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(10));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(20));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(30));

				Assert.That(e.MoveNext(), Is.True);
				Assert.That(e.Current, Is.EqualTo(40));

				Assert.That(e.MoveNext(), Is.False);
			}
		}

		[Test]
		public void GetEnumeratorIEnumerableTReturnsForeachUsableEnumerator()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				int index = 0;
				foreach (int element in (IEnumerable<int>)list)
				{
					Assert.That(element, Is.EqualTo((index+1) * 10));
					index++;
				}
				Assert.That(index, Is.EqualTo(4));
			}
		}

		[Test]
		public void IndexerGetReturnsCorrectElement()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				Assert.That(list[0], Is.EqualTo(10));
				Assert.That(list[1], Is.EqualTo(20));
				Assert.That(list[2], Is.EqualTo(30));
				Assert.That(list[3], Is.EqualTo(40));
			}
		}

		[Test]
		public void IndexerGetRequiresReadAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				int element = 0;
				AssertRequiresReadOrWriteAccess(
					list,
					() => element = list[0]);
			}
		}

		[Test]
		public void IndexerGetRequiresIndexInSafetyRange()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				int element = 0;
				AssertRequiresIndexInSafetyRange(
					list,
					1,
					1,
					copy => element = copy[0]);
			}
		}

		[Test]
		public void IndexerSetSetsCorrectElement()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 0, 0, 0, 0);
				NativeChunkedList<int> copy = list;

				copy[0] = 10;
				copy[1] = 20;
				copy[2] = 30;
				copy[3] = 40;

				AssertElements(list, 10, 20, 30, 40);
			}
		}

		[Test]
		public void IndexerSetRequiresReadAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 0, 0, 0, 0);
				NativeChunkedList<int> copy = list;

				AssertRequiresReadOrWriteAccess(
					list,
					() => copy[0] = 10);
			}
		}

		[Test]
		public void IndexerSetRequiresIndexInSafetyRange()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 0, 0, 0, 0);

				AssertRequiresIndexInSafetyRange(
					list,
					1,
					1,
					copy => copy[0] = 10);
			}
		}

		[Test]
		public void AddInsertsElementAtEnd()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				list.Add(10);
				AssertElements(list, 10);

				list.Add(20);
				AssertElements(list, 10, 20);

				list.Add(30);
				AssertElements(list, 10, 20, 30);

				list.Add(40);
				AssertElements(list, 10, 20, 30, 40);

				list.Add(50);
				AssertElements(list, 10, 20, 30, 40, 50);
			}
		}

		[Test]
		public void AddRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Add(10));
			}
		}

		[Test]
		public void AddRangeManagedInsertsAtEnd()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				list.AddRange(new[] { 10, 20, 30, 40, 50 }, 0, 5);

				Assert.That(list.Capacity, Is.EqualTo(6));
				AssertElements(list, 10, 20, 30, 40, 50);

				list.AddRange(new[] { 60, 70 }, 0, 2);

				Assert.That(list.Capacity, Is.EqualTo(8));
				AssertElements(list, 10, 20, 30, 40, 50, 60, 70);
			}
		}

		[Test]
		public void AddRangeManagedRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() =>  list.AddRange(new[] { 10, 20, 30, 40, 50 }, 0, 5));
			}
		}

		[Test]
		public void AddRangeManagedRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresFullListAccess(
					list,
					copy => copy.AddRange(new[] { 10, 20, 30, 40, 50 }, 0, 5));
			}
		}

		[Test]
		public void AddRangeManagedRequiresNonNullArray()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresNonNullArray(() => list.AddRange(null, 0, 0));
			}
		}

		[Test]
		public void AddRangeManagedRequiresValidRange()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AssertRequiresValidArrayRange(
					() => list.AddRange(new[] { 10, 20, 30, 40, 50 }, 0, -1));

				AssertRequiresValidArrayRange(
					() => list.AddRange(new[] { 10, 20, 30, 40, 50 }, -1, 0));

				AssertRequiresValidArrayRange(
					() => list.AddRange(new[] { 10, 20, 30, 40, 50 }, 2, 4));
			}
		}

		[Test]
		public void AddRangeNativeInsertsAtEnd()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 10, 20, 30, 40, 50 },
				Allocator.Temp))
			{
				using (NativeArray<int> arraySmall = new NativeArray<int>(
					new[] { 60, 70 },
					Allocator.Temp))
				{
					using (NativeChunkedList<int> list = CreateList(2, 4))
					{
						list.AddRange(array, 0, 5);

						Assert.That(list.Capacity, Is.EqualTo(6));
						AssertElements(list, 10, 20, 30, 40, 50);

						list.AddRange(arraySmall, 0, 2);

						Assert.That(list.Capacity, Is.EqualTo(8));
						AssertElements(list, 10, 20, 30, 40, 50, 60, 70);
					}
				}
			}
		}

		[Test]
		public void AddRangeNativeRequiresWriteAccess()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 10, 20, 30, 40, 50 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.AddRange(array, 0, 5));
				}
			}
		}

		[Test]
		public void AddRangeNativeRequiresFullListAccess()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 10, 20, 30, 40, 50 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AssertRequiresFullListAccess(
						list,
						copy => copy.AddRange(array, 0, 5));
				}
			}
		}

		[Test]
		public void AddRangeNativeRequiresNonNullArray()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 10, 20, 30, 40, 50 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AssertRequiresNonNullArray(() => list.AddRange(null, 0, 0));
				}
			}
		}

		[Test]
		public void AddRangeNativeRequiresValidRange()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 10, 20, 30, 40, 50 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AssertRequiresValidArrayRange(
						() => list.AddRange(array, 0, -1));

					AssertRequiresValidArrayRange(
						() => list.AddRange(array, -1, 0));

					AssertRequiresValidArrayRange(
						() => list.AddRange(array, 2, 4));
				}
			}
		}

		[Test]
		public void InsertShiftsElementsBack()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				list.Insert(0, 5);

				AssertElements(list, 5, 10, 20, 30, 40);
			}
		}

		[Test]
		public void InsertIntoEmptyListAddsElement()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				list.Insert(0, 10);

				AssertElements(list, 10);
			}
		}

		[Test]
		public void InsertAtTheEndOnlyShiftsLastElement()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				list.Insert(3, 50);

				AssertElements(list, 10, 20, 30, 50, 40);
			}
		}

		[Test]
		public void InsertRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Insert(0, 5));
			}
		}

		[Test]
		public void InsertRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				AssertRequiresFullListAccess(
					list,
					copy => copy.Insert(0, 5));
			}
		}

		[Test]
		public void RemoveAtShiftsElementsForward()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				list.RemoveAt(0);

				AssertElements(list, 20, 30, 40, 50);

				list.RemoveAt(3);

				AssertElements(list, 20, 30, 40);
			}
		}

		[Test]
		public void RemoveAtRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresReadOrWriteAccess(
					list,
					() => list.RemoveAt(0));
			}
		}

		[Test]
		public void RemoveAtRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresFullListAccess(
					list,
					copy => copy.RemoveAt(0));
			}
		}

		[Test]
		public void RemoveAtSwapBackReplacesWithLastElement()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				list.RemoveAtSwapBack(0);

				AssertElements(list, 50, 20, 30, 40);

				list.RemoveAtSwapBack(1);

				AssertElements(list, 50, 40, 30);

				list.RemoveAtSwapBack(2);

				AssertElements(list, 50, 40);
			}
		}

		[Test]
		public void RemoveAtSwapBackRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresReadOrWriteAccess(
					list,
					() => list.RemoveAtSwapBack(0));
			}
		}

		[Test]
		public void RemoveAtSwapBackRequiresElementAndBackInSafetyRange()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresIndexInSafetyRange(
					list,
					1,
					4,
					copy => copy.RemoveAtSwapBack(0));

				AssertRequiresIndexInSafetyRange(
					list,
					0,
					3,
					copy => copy.RemoveAtSwapBack(0));
			}
		}

		[Test]
		public void ClearRemovesAllElements()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				list.Clear();

				AssertElements(list);
				Assert.That(list.Capacity, Is.EqualTo(6));
			}
		}

		[Test]
		public void ClearRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Clear());
			}
		}

		[Test]
		public void ClearRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				AssertRequiresFullListAccess(
					list,
					copy => copy.Clear());
			}
		}

		[Test]
		public void CopyFromManagedReplacesWithoutGrowingCapacity()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40, 50);

				list.CopyFrom(new[] { 100, 200, 300, 400, 500 }, 0, 5);
				Assert.That(list.Capacity, Is.EqualTo(6));
				AssertElements(list, 100, 200, 300, 400, 500);

				list.CopyFrom(new[] { 1000, 2000 }, 0, 2);
				Assert.That(list.Capacity, Is.EqualTo(6));
				AssertElements(list, 1000, 2000);
			}
		}

		[Test]
		public void CopyFromManagedReplacesAndGrowsCapacity()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				list.CopyFrom(new[] { 100, 200, 300, 400, 500 }, 0, 5);
				Assert.That(list.Capacity, Is.EqualTo(6));
				AssertElements(list, 100, 200, 300, 400, 500);
			}
		}

		[Test]
		public void CopyFromManagedRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertRequiresReadOrWriteAccess(
					list,
					() => list.CopyFrom(new[] { 100, 200 }, 0, 2));
			}
		}

		[Test]
		public void CopyFromManagedRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertRequiresFullListAccess(
					list,
					copy => copy.CopyFrom(new[] { 100, 200 }, 0, 2));
			}
		}

		[Test]
		public void CopyFromManagedRequiresNonNullArray()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertRequiresNonNullArray(() => list.CopyFrom(null, 0, 0));
			}
		}

		[Test]
		public void CopyFromManagedRequiresValidRange()
		{
			using (NativeChunkedList<int> list = CreateList(2, 4))
			{
				AddElements(list, 10, 20, 30, 40);

				AssertRequiresValidArrayRange(
					() => list.CopyFrom(new[] { 100, 200, 300 }, -1, 0));

				AssertRequiresValidArrayRange(
					() => list.CopyFrom(new[] { 100, 200, 300 }, 0, -2));

				AssertRequiresValidArrayRange(
					() => list.CopyFrom(new[] { 100, 200, 300 }, 1, 3));
			}
		}

		[Test]
		public void CopyFromNativeReplacesWithoutGrowingCapacity()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200, 300, 400, 500 },
				Allocator.Temp))
			{
				using (NativeArray<int> arraySmall = new NativeArray<int>(
					new[] { 1000, 2000 },
					Allocator.Temp))
				{
					using (NativeChunkedList<int> list = CreateList(2, 4))
					{
						AddElements(list, 10, 20, 30, 40, 50);

						list.CopyFrom(array, 0, 5);
						Assert.That(list.Capacity, Is.EqualTo(6));
						AssertElements(list, 100, 200, 300, 400, 500);

						list.CopyFrom(arraySmall, 0, 2);
						Assert.That(list.Capacity, Is.EqualTo(6));
						AssertElements(list, 1000, 2000);
					}
				}
			}
		}

		[Test]
		public void CopyFromNativeReplacesAndGrowsCapacity()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200, 300, 400, 500 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AddElements(list, 10, 20, 30, 40);

					list.CopyFrom(array, 0, 5);
					Assert.That(list.Capacity, Is.EqualTo(6));
					AssertElements(list, 100, 200, 300, 400, 500);
				}
			}
		}

		[Test]
		public void CopyFromNativeRequiresWriteAccess()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AddElements(list, 10, 20, 30, 40);

					AssertRequiresReadOrWriteAccess(
						list,
						() => list.CopyFrom(array, 0, 2));
				}
			}
		}

		[Test]
		public void CopyFromNativeRequiresFullListAccess()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AddElements(list, 10, 20, 30, 40);

					AssertRequiresFullListAccess(
						list,
						copy => copy.CopyFrom(array, 0, 2));
				}
			}
		}

		[Test]
		public void CopyFromNativeRequiresNonNullArray()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AddElements(list, 10, 20, 30, 40);

					AssertRequiresNonNullArray(() => list.CopyFrom(null, 0, 0));
				}
			}
		}

		[Test]
		public void CopyFromNativeRequiresValidRange()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				new[] { 100, 200, 300 },
				Allocator.Temp))
			{
				using (NativeChunkedList<int> list = CreateList(2, 4))
				{
					AddElements(list, 10, 20, 30, 40);

					AssertRequiresValidArrayRange(
						() => list.CopyFrom(array, -1, 0));

					AssertRequiresValidArrayRange(
						() => list.CopyFrom(array, 0, -2));

					AssertRequiresValidArrayRange(
						() => list.CopyFrom(array, 1, 3));
				}
			}
		}

		[Test]
		public void ToArrayAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = list.ToArray();

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, 0, 0, -1);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, 0, 0, 4);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayCopiesFromStartWhenStartIndexIsInvalid()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = list.ToArray(null, -1, 0, 3);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayAllocatesArrayWhenNullIsPassed()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = list.ToArray(null, 0, 0, 3);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = new int[2];

				int[] ret = list.ToArray(arr, 0, 0, 3);

				Assert.That(arr, Is.EqualTo(new[] { 0, 0 }));
				Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = new int[3];

				list.ToArray(arr, 0, -1, 3);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
			}
		}

		[Test]
		public void ToArrayCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				int[] arr = new int[6];

				list.ToArray(arr, 0, 4, 3);

				Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30, 0, 0, 0 }));
			}
		}

		[Test]
		public void ToArrayWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);
				int[] array = new int[5];

				Assert.That(
					() => list.ToArray(array, 0, 0, 1),
					Throws.TypeOf<IndexOutOfRangeException>());
				list.TestUseOnlySetParallelForSafetyCheckRange(
					0,
					list.Length - 1);
			}
		}

		[Test]
		public void ToArrayWhenWriteOnlyThrowsException()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.ToArray());
			}
		}

		[Test]
		public void ToNativeArrayAllocatesAndCopiesWholeListByDefault()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArray())
				{
					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayUsesFullLengthWhenLengthIsNegative()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						0,
						0,
						-1);

					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayUsesFullLengthWhenLengthIsTooLarge()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						0,
						0,
						4);

					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesFromStartWhenStartIndexIsInvalid()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						-1,
						0,
						3);

					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
					Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayAllocatesArrayWhenInvalidArrayIsPassed()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = list.ToNativeArray(
					default(NativeArray<int>),
					0,
					0,
					3))
				{
					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayAllocatesArrayWhenTooShortIsPassed()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0))
				{
					using (NativeArray<int> ret = list.ToNativeArray(
						arr,
						0,
						0,
						3))
					{
						Assert.That(arr, Is.EqualTo(new[] { 0, 0 }));
						Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
					}
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesToStartWhenDestIndexIsNegative()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						0,
						-1,
						3);

					Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30 }));
					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayCopiesToStartWhenDestIndexIsTooLarge()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> arr = CreateArrayValues(0, 0, 0, 0, 0, 0))
				{
					NativeArray<int> ret = list.ToNativeArray(
						arr,
						0,
						4,
						3);

					Assert.That(ret, Is.EqualTo(new[] { 10, 20, 30, 0, 0, 0 }));
					Assert.That(arr, Is.EqualTo(new[] { 10, 20, 30, 0, 0, 0 }));
				}
			}
		}

		[Test]
		public void ToNativeArrayWhenOutOfSafetyCheckBoundsThrowsException()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30, 40))
			{
				using (NativeArray<int> array = CreateArrayValues(0, 0, 0, 0, 0))
				{
					list.TestUseOnlySetParallelForSafetyCheckRange(1, 2);

					Assert.That(
						() => list.ToNativeArray(array, 0, 0, 1),
						Throws.TypeOf<IndexOutOfRangeException>());
					list.TestUseOnlySetParallelForSafetyCheckRange(
						0,
						list.Length - 1);
				}
			}
		}

		[Test]
		public void ToNativeArrayWhenWriteOnlyThrowsException()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				using (NativeArray<int> array = CreateArrayValues(0, 0, 0, 0, 0))
				{
					AssertRequiresReadOrWriteAccess(
						list,
						() => list.ToNativeArray(array));
				}
			}
		}

		[Test]
		public void IsCreatedReturnsFalseAfterDefaultConstructor()
		{
			NativeChunkedList<int> list = new NativeChunkedList<int>();

			Assert.That(list.IsCreated, Is.False);
		}

		[Test]
		public void IsCreatedReturnsTrueAfterConstructor()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				Assert.That(list.IsCreated, Is.True);
			}
		}

		[Test]
		public void DisposeMakesIsCreatedReturnFalse()
		{
			NativeChunkedList<int> list = CreateListValues(10, 20, 30);
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
		public void DisposeRequiresWriteAccess()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				AssertRequiresReadOrWriteAccess(
					list,
					() => list.Dispose());
			}
		}

		[Test]
		public void DisposeRequiresFullListAccess()
		{
			using (NativeChunkedList<int> list = CreateListValues(10, 20, 30))
			{
				AssertRequiresFullListAccess(
					list,
					copy => copy.Dispose());
			}
		}
	}
}
