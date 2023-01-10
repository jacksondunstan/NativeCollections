//-----------------------------------------------------------------------
// <copyright file="TestNativeLongPtr.cs" company="Jackson Dunstan">
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
	/// Unit tests for <see cref="NativeLongPtr"/>
	/// </summary>
	public class TestNativeLongPtr
	{
		private static void AssertRequiresReadOrWriteAccess(
			NativeLongPtr intPtr,
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
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				Assert.That(intPtr.Value, Is.EqualTo(0));
			}
		}

		[Test]
		public void ConstructorSetsInitialValue()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp, 123))
			{
				Assert.That(intPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void ConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(
				() => new NativeLongPtr(Allocator.None),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void GetValueReturnsWhatSetValueSets()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				NativeLongPtr copy = intPtr;
				copy.Value = 123;

				Assert.That(intPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void GetValueRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				long value;
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => value = intPtr.Value);
			}
		}

		[Test]
		public void SetValueRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				NativeLongPtr copy = intPtr;
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => copy.Value = 123);
			}
		}

		[Test]
		public void ParallelIncrementIncrementsValue()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp, 123))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Increment();

				Assert.That(intPtr.Value, Is.EqualTo(124));
			}
		}

		[Test]
		public void ParallelIncrementRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					parallel.Increment);
			}
		}

		[Test]
		public void ParallelDecrementIncrementsValue()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp, 123))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Decrement();

				Assert.That(intPtr.Value, Is.EqualTo(122));
			}
		}

		[Test]
		public void ParallelDecrementRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					parallel.Decrement);
			}
		}

		[Test]
		public void ParallelAddOffsetsValue()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp, 123))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				parallel.Add(5);

				Assert.That(intPtr.Value, Is.EqualTo(128));

				parallel.Add(-15);

				Assert.That(intPtr.Value, Is.EqualTo(113));
			}
		}

		[Test]
		public void ParallelAddRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				NativeLongPtr.Parallel parallel = intPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					intPtr,
					() => parallel.Add(10));
			}
		}

		[Test]
		public void DisposeMakesUnusable()
		{
			NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp);
			intPtr.Dispose();
			Assert.That(
				() => intPtr.Value = 10,
				Throws.Exception);
		}

		[Test]
		public void DisposeRequiresReadAccess()
		{
			using (NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp))
			{
				AssertRequiresReadOrWriteAccess(
					intPtr,
					intPtr.Dispose);
			}
		}

		[Test]
		public void IsCreatedOnlyReturnsTrueBeforeDispose()
		{
			NativeLongPtr intPtr = new NativeLongPtr(Allocator.Temp);
			Assert.That(intPtr.IsCreated, Is.True);

			intPtr.Dispose();

			Assert.That(intPtr.IsCreated, Is.False);
		}

		private struct ParallelForTestJob : IJobParallelFor
		{
			public NativeArray<long> Array;
			public NativeLongPtr.Parallel Sum;

			public void Execute(int index)
			{
				Sum.Add(Array[index]);
			}
		}

		[Test]
		public void ParallelForJobCanUseParallelPtr()
		{
			using (NativeArray<long> array = new NativeArray<long>(
				3,
				Allocator.TempJob))
			{
				NativeArray<long> arrayCopy = array;
				arrayCopy[0] = 10;
				arrayCopy[1] = 20;
				arrayCopy[2] = 30;

				using (NativeLongPtr sum = new NativeLongPtr(
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
			public NativeLongPtr LongPtr;

			public void Execute()
			{
			}
		}

		[Test]
		public void CanDeallocateOnJobCompletion()
		{
			NativeLongPtr intPtr = new NativeLongPtr(Allocator.TempJob);
			var job = new DeallocateOnJobCompletionJob { LongPtr = intPtr };
			job.Run();

			Assert.That(
				() => intPtr.Value = 10,
				Throws.Exception);
		}
	}
}
