//-----------------------------------------------------------------------
// <copyright file="TestNativePerJobThreadIntPtr.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Unity.Collections;
using Unity.Jobs;
using NUnit.Framework;

namespace JacksonDunstan.NativeCollections.Tests
{
	/// <summary>
	/// Unit tests for <see cref="NativePerJobThreadIntPtr"/>
	/// </summary>
	public class TestNativePerJobThreadIntPtr
	{
		private static void AssertRequiresReadOrWriteAccess(
			NativePerJobThreadIntPtr intPtr,
			Action action)
		{
			intPtr.TestUseOnlySetAllowReadAndWriteAccess(false);
			try
			{
				Assert.That(
					() => action(),
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				intPtr.TestUseOnlySetAllowReadAndWriteAccess(true);
			}
		}

		[Test]
		public void ConstructorDefaultsValueToZero()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				Assert.That(intPtr.Value, Is.EqualTo(0));
			}
		}

		[Test]
		public void ConstructorSetsInitialValue()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp, 123))
			{
				Assert.That(intPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void ConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(
				() => new NativePerJobThreadIntPtr(Allocator.None),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void GetValueReturnsWhatSetValueSets()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				NativePerJobThreadIntPtr copy = intPtr;
				copy.Value = 123;

				Assert.That(intPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void GetValueRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				int value;
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => value = intPtr.Value);
			}
		}

		[Test]
		public void SetValueRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				NativePerJobThreadIntPtr copy = intPtr;
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => copy.Value = 123);
			}
		}

		[Test]
		public void ParallelIncrementIncrementsValue()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
			       Allocator.Temp,
			       123))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Increment();

				Assert.That(intPtr.Value, Is.EqualTo(124));
			}
		}

		[Test]
		public void ParallelIncrementRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					parallel.Increment);
			}
		}

		[Test]
		public void ParallelDecrementIncrementsValue()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp,
				123))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Decrement();

				Assert.That(intPtr.Value, Is.EqualTo(122));
			}
		}

		[Test]
		public void ParallelDecrementRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					parallel.Decrement);
			}
		}

		[Test]
		public void ParallelAddOffsetsValue()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp,
				123))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Add(5);

				Assert.That(intPtr.Value, Is.EqualTo(128));

				parallel.Add(-15);

				Assert.That(intPtr.Value, Is.EqualTo(113));
			}
		}

		[Test]
		public void ParallelAddRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => parallel.Add(10));
			}
		}

		[Test]
		public void DisposeMakesUnusable()
		{
			NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp);
			intPtr.Dispose();
			Assert.That(
				() => intPtr.Value = 10,
				Throws.Exception);
		}

		[Test]
		public void DisposeRequiresReadAccess()
		{
			using (NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp))
			{
				AssertRequiresReadOrWriteAccess(
					intPtr,
					intPtr.Dispose);
			}
		}

		[Test]
		public void IsCreatedOnlyReturnsTrueBeforeDispose()
		{
			NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.Temp);
			Assert.That(intPtr.IsCreated, Is.True);

			intPtr.Dispose();

			Assert.That(intPtr.IsCreated, Is.False);
		}

		private struct ParallelForTestJob : IJobParallelFor
		{
			public NativeArray<int> Array;
			public NativePerJobThreadIntPtr.Parallel Sum;

			public void Execute(int index)
			{
				Sum.Add(Array[index]);
			}
		}

		[Test]
		public void ParallelForJobCanUseParallelPtr()
		{
			using (NativeArray<int> array = new NativeArray<int>(
				3,
				Allocator.TempJob))
			{
				NativeArray<int> arrayCopy = array;
				arrayCopy[0] = 10;
				arrayCopy[1] = 20;
				arrayCopy[2] = 30;

				using (NativePerJobThreadIntPtr sum = new NativePerJobThreadIntPtr(
					Allocator.TempJob))
				{
					ParallelForTestJob job = new ParallelForTestJob
					{
						Array = array,
						Sum = sum.GetParallel()
					};
					job.Run(array.Length);

					Assert.That(sum.Value, Is.EqualTo(60));
				}
			}
		}

		private struct DeallocateOnJobCompletionJob : IJob
		{
			[DeallocateOnJobCompletion]
			public NativePerJobThreadIntPtr IntPtr;

			public void Execute()
			{
			}
		}

		[Test]
		public void CanDeallocateOnJobCompletion()
		{
			NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(
				Allocator.TempJob);
			var job = new DeallocateOnJobCompletionJob { IntPtr = intPtr };
			job.Run();

			Assert.That(
				() => intPtr.Value = 10,
				Throws.Exception);
		}
	}
}
