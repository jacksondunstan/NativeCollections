//-----------------------------------------------------------------------
// <copyright file="TestNativePerJobThreadLongPtr.cs" company="Jackson Dunstan">
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
	/// Unit tests for <see cref="NativePerJobThreadLongPtr"/>
	/// </summary>
	public class TestNativePerJobThreadLongPtr
	{
		private static void AssertRequiresReadOrWriteAccess(
			NativePerJobThreadLongPtr longPtr,
			Action action)
		{
			longPtr.TestUseOnlySetAllowReadAndWriteAccess(false);
			try
			{
				Assert.That(
					() => action(),
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				longPtr.TestUseOnlySetAllowReadAndWriteAccess(true);
			}
		}

		[Test]
		public void ConstructorDefaultsValueToZero()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				Assert.That(longPtr.Value, Is.EqualTo(0));
			}
		}

		[Test]
		public void ConstructorSetsInitialValue()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp, 123))
			{
				Assert.That(longPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void ConstructorThrowsExceptionForInvalidAllocator()
		{
			Assert.That(
				() => new NativePerJobThreadLongPtr(Allocator.None),
				Throws.TypeOf<ArgumentException>());
		}

		[Test]
		public void GetValueReturnsWhatSetValueSets()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				NativePerJobThreadLongPtr copy = longPtr;
				copy.Value = 123;

				Assert.That(longPtr.Value, Is.EqualTo(123));
			}
		}

		[Test]
		public void GetValueRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				long value;
				AssertRequiresReadOrWriteAccess(
					longPtr,
					() => value = longPtr.Value);
			}
		}

		[Test]
		public void SetValueRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				NativePerJobThreadLongPtr copy = longPtr;
				AssertRequiresReadOrWriteAccess(
					longPtr,
					() => copy.Value = 123);
			}
		}

		[Test]
		public void ParallelIncrementIncrementsValue()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
			       Allocator.Temp,
			       123))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				parallel.Increment();

				Assert.That(longPtr.Value, Is.EqualTo(124));
			}
		}

		[Test]
		public void ParallelIncrementRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					longPtr,
					parallel.Increment);
			}
		}

		[Test]
		public void ParallelDecrementIncrementsValue()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp,
				123))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				parallel.Decrement();

				Assert.That(longPtr.Value, Is.EqualTo(122));
			}
		}

		[Test]
		public void ParallelDecrementRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					longPtr,
					parallel.Decrement);
			}
		}

		[Test]
		public void ParallelAddOffsetsValue()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp,
				123))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				parallel.Add(5);

				Assert.That(longPtr.Value, Is.EqualTo(128));

				parallel.Add(-15);

				Assert.That(longPtr.Value, Is.EqualTo(113));
			}
		}

		[Test]
		public void ParallelAddRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				NativePerJobThreadLongPtr.Parallel parallel = longPtr.GetParallel();
				AssertRequiresReadOrWriteAccess(
					longPtr,
					() => parallel.Add(10));
			}
		}

		[Test]
		public void DisposeMakesUnusable()
		{
			NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp);
			longPtr.Dispose();
			Assert.That(
				() => longPtr.Value = 10,
				Throws.Exception);
		}

		[Test]
		public void DisposeRequiresReadAccess()
		{
			using (NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp))
			{
				AssertRequiresReadOrWriteAccess(
					longPtr,
					longPtr.Dispose);
			}
		}

		[Test]
		public void IsCreatedOnlyReturnsTrueBeforeDispose()
		{
			NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.Temp);
			Assert.That(longPtr.IsCreated, Is.True);

			longPtr.Dispose();

			Assert.That(longPtr.IsCreated, Is.False);
		}

		private struct ParallelForTestJob : IJobParallelFor
		{
			public NativeArray<long> Array;
			public NativePerJobThreadLongPtr.Parallel Sum;

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

				using (NativePerJobThreadLongPtr sum = new NativePerJobThreadLongPtr(
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
			public NativePerJobThreadLongPtr LongPtr;

			public void Execute()
			{
			}
		}

		[Test]
		public void CanDeallocateOnJobCompletion()
		{
			NativePerJobThreadLongPtr longPtr = new NativePerJobThreadLongPtr(
				Allocator.TempJob);
			var job = new DeallocateOnJobCompletionJob { LongPtr = longPtr };
			job.Run();

			Assert.That(
				() => longPtr.Value = 10,
				Throws.Exception);
		}
	}
}
