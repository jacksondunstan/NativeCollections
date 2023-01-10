//-----------------------------------------------------------------------
// <copyright file="TestSharedDisposable.cs" company="Jackson Dunstan">
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
	/// Unit tests for <see cref="SharedDisposable{T}"/>
	/// </summary>
	public class TestSharedDisposable
	{
		private struct DisposeCallCounter : IDisposable
		{
			private static int NextId = 1;
			public int Id;
			
			public NativeArray<int> Num;

			public void AssignUniqueId()
			{
				Id = NextId++;
			}

			public void Dispose()
			{
				Num[0] = Num[0] + 1;
			}
		}
		
		private struct TestDisposable : IDisposable
		{
			public DisposeCallCounter Counter;

			public static TestDisposable Create()
			{
				return new TestDisposable
				{
					Counter =
					{
						Num = new NativeArray<int>(
							1,
							Allocator.TempJob)
					}
				};
			}

			public void Dispose()
			{
				Counter.Num.Dispose();
			}
		}
		
		private static void AssertRequiresReadOrWriteAccess(
			SharedDisposable<DisposeCallCounter> shared,
			Action action)
		{
			shared.TestUseOnlySetAllowReadAndWriteAccess(false);
			try
			{
				Assert.That(
					() => action(),
					Throws.TypeOf<InvalidOperationException>());
			}
			finally
			{
				shared.TestUseOnlySetAllowReadAndWriteAccess(true);
			}
		}

		[Test]
		public void ConstructorUsesGivenDisposable()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				disposable.Counter.AssignUniqueId();
				using (var shared = new SharedDisposable<DisposeCallCounter>(
					disposable.Counter,
					Allocator.TempJob))
				{
					Assert.That(shared.Value.Id, Is.EqualTo(disposable.Counter.Id));
				}
			}
		}

		[Test]
		public void ShareExtensionUsesGivenDisposable()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				disposable.Counter.AssignUniqueId();
				using (var shared = disposable.Counter.Share(Allocator.TempJob))
				{
					Assert.That(shared.Value.Id, Is.EqualTo(disposable.Counter.Id));
				}
			}
		}

		[Test]
		public void ConstructorThrowsExceptionForInvalidAllocator()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				Assert.That(
					() => new SharedDisposable<DisposeCallCounter>(
						disposable.Counter,
						Allocator.None),
					Throws.TypeOf<ArgumentException>());
			}
		}

		[Test]
		public void GetValueRequiresReadAccess()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				using (var shared = disposable.Counter.Share(Allocator.TempJob))
				{
					DisposeCallCounter val;
					AssertRequiresReadOrWriteAccess(
						shared,
						() => val = shared.Value);
				}
			}
		}

		[Test]
		public void RefIncrementsRefCountAndReturnsCopy()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				disposable.Counter.AssignUniqueId();
				using (var shared = disposable.Counter.Share(Allocator.TempJob))
				{
					using (var shared2 = shared.Ref())
					{
						Assert.That(
							shared2.Value.Id,
							Is.EqualTo(disposable.Counter.Id));
					}
				}
				Assert.That(disposable.Counter.Num[0], Is.EqualTo(1));
			}
		}

		[Test]
		public void DisposeDisposesDisposableAndMakesUnusable()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				var shared = disposable.Counter.Share(Allocator.TempJob);
				shared.Dispose();
				Assert.That(disposable.Counter.Num[0], Is.EqualTo(1));
				DisposeCallCounter val;
				Assert.That(
					() => val = shared.Value,
					Throws.Exception);
			}
		}

		[Test]
		public void DisposeRequiresReadAccess()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				using (var shared = disposable.Counter.Share(Allocator.TempJob))
				{
					AssertRequiresReadOrWriteAccess(
						shared,
						shared.Dispose);
				}
			}
		}

		[Test]
		public void IsCreatedOnlyReturnsTrueBeforeDispose()
		{
			using (TestDisposable disposable = TestDisposable.Create())
			{
				var shared = disposable.Counter.Share(Allocator.TempJob);
				Assert.That(shared.IsCreated, Is.True);

				shared.Dispose();

				Assert.That(shared.IsCreated, Is.False);
			}
		}

		private struct DeallocateOnJobCompletionJob : IJob
		{
			[DeallocateOnJobCompletion]
			public SharedDisposable<DisposeCallCounter> Shared;

			public void Execute()
			{
			}
		}

		[Test]
		public void CanDeallocateOnJobCompletion()
		{
			TestDisposable disposable = TestDisposable.Create();
			var shared = disposable.Counter.Share(Allocator.TempJob);
			var job = new DeallocateOnJobCompletionJob { Shared = shared };
			
			job.Run();
			Assert.That(
				() => disposable.Counter.Num[0],
				Throws.Exception);
		}
	}
}
