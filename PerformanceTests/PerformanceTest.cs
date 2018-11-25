using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using JacksonDunstan.NativeCollections;
using UnityEditor;

public class PerformanceTest : MonoBehaviour
{
	// Add jobs

	[BurstCompile]
	struct ListAddJob : IJob
	{
		public NativeList<int> List;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				List.Add(1);
			}
		}
	}

	[BurstCompile]
	struct LinkedListAddJob : IJob
	{
		public NativeLinkedList<int> List;
		public int NumNodesToInsert;

		public void Execute()
		{
			NativeLinkedList<int>.Enumerator e = List.Tail;
			for (int i = 0; i < NumNodesToInsert; ++i)
			{
				e = List.InsertAfter(e, 1);
			}
		}
	}

	[BurstCompile]
	struct ChunkedListAddJob : IJob
	{
		public NativeChunkedList<int> List;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				List.Add(1);
			}
		}
	}

	// Iterate jobs

	[BurstCompile]
	struct ArrayIterateJob : IJob
	{
		public NativeArray<int> Array;
		public NativeArray<int> Sum;

		public void Execute()
		{
			for (int i = 0; i < Array.Length; ++i)
			{
				Sum[0] += Array[i];
			}
		}
	}

	[BurstCompile]
	struct ListIterateJob : IJob
	{
		public NativeList<int> List;
		public NativeArray<int> Sum;

		public void Execute()
		{
			for (int i = 0; i < List.Length; ++i)
			{
				Sum[0] += List[i];
			}
		}
	}

	[BurstCompile]
	struct LinkedListIterateJob : IJob
	{
		public NativeLinkedList<int> List;
		public NativeArray<int> Sum;

		public void Execute()
		{
			for (int i = 0; i < List.Length; ++i)
			{
				Sum[0] += List[i];
			}
		}
	}

	[BurstCompile]
	struct ChunkedListIterateJob : IJob
	{
		public NativeChunkedList<int> List;
		public NativeArray<int> Sum;

		public void Execute()
		{
			for (
				var chunks = List.Chunks.GetEnumerator();
				chunks.MoveNext(); )
			{
				for (
					var chunk = chunks.Current.GetEnumerator();
					chunk.MoveNext(); )
				{
					Sum[0] += chunk.Current;
				}
			}
		}
	}

	// Iterate jobs (ParallelFor)

	[BurstCompile]
	struct ArrayIterateJobParallelFor : IJobParallelFor
	{
		public NativeArray<int> Array;
		public NativeArray<int> Sum;

		public void Execute(int index)
		{
			Sum[0] += Array[index];
		}
	}

	[BurstCompile]
	struct LinkedListIterateJobParallelFor : IJobParallelFor
	{
		public NativeLinkedList<int> List;
		public NativeArray<int> Sum;

		public void Execute(int index)
		{
			Sum[0] += List[index];
		}
	}

	[BurstCompile]
	struct ChunkedListIterateJobParallelFor : IJobParallelForRanged
	{
		public NativeChunkedList<int> List;
		public NativeArray<int> Sum;

		public void Execute(int startIndex, int endIndex)
		{
			for (
				var chunks = List.GetChunksEnumerable(startIndex, endIndex).GetEnumerator();
				chunks.MoveNext();)
			{
				for (
					var chunk = chunks.Current.GetEnumerator();
					chunk.MoveNext();)
				{
					Sum[0] += chunk.Current;
				}
			}
		}
	}

	// Insert jobs

	[BurstCompile]
	struct LinkedListInsertJob : IJob
	{
		public NativeLinkedList<int> LinkedList;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				LinkedList.InsertBefore(
					LinkedList.GetEnumeratorAtIndex(i / 2),
					1);
			}
		}
	}

	[BurstCompile]
	struct ChunkedListInsertJob : IJob
	{
		public NativeChunkedList<int> List;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				List.Insert(i / 2, 1);
			}
		}
	}

	// Remove jobs

	[BurstCompile]
	struct LinkedListRemoveJob : IJob
	{
		public NativeLinkedList<int> List;
		public int NumElementsToRemove;

		public void Execute()
		{
			for (int i = List.Length; i > 0; --i)
			{
				List.Remove(List.GetEnumeratorAtIndex(i / 2));
			}
		}
	}

	[BurstCompile]
	struct ChunkedListRemoveJob : IJob
	{
		public NativeChunkedList<int> List;
		public int NumElementsToRemove;

		public void Execute()
		{
			for (int i = List.Length; i > 0; --i)
			{
				List.RemoveAt(i / 2);
			}
		}
	}

	// NativeIntPtr and NativePerJobThreadIntPtr jobs

	[BurstCompile]
	struct NativeIntPtrParallelJob : IJobParallelFor
	{
		public NativeArray<int> Array;
		public NativeIntPtr.Parallel Sum;

		public void Execute(int index)
		{
			Sum.Add(Array[index]);
		}
	}

	[BurstCompile]
	struct NativePerJobThreadIntPtrParallelJob : IJobParallelFor
	{
		public NativeArray<int> Array;
		public NativePerJobThreadIntPtr.Parallel Sum;

		public void Execute(int index)
		{
			Sum.Add(Array[index]);
		}
	}

	// Warm up the job system

	static void WarmUpJobSystem()
	{
		// Create native collections

		NativeArray<int> sum = new NativeArray<int>(1, Allocator.Temp);
		NativeArray<int> array = new NativeArray<int>(
			4,
			Allocator.Temp);
		NativeList<int> list = new NativeList<int>(
			4,
			Allocator.Temp);
		NativeLinkedList<int> linkedList = new NativeLinkedList<int>(
			4,
			Allocator.Temp);
		NativeChunkedList<int> chunkedList = new NativeChunkedList<int>(
			4,
			4,
			Allocator.Temp);
		NativeIntPtr nativeIntPtr = new NativeIntPtr(Allocator.Temp);
		NativePerJobThreadIntPtr nativePerJobThreadIntPtr = new NativePerJobThreadIntPtr(
			Allocator.Temp);

		// Create jobs

		ListAddJob listAddJob = new ListAddJob
		{
			List = list
		};
		LinkedListAddJob linkedListAddJob = new LinkedListAddJob
		{
			List = linkedList
		};
		ChunkedListAddJob chunkedListAddJob = new ChunkedListAddJob
		{
			List = chunkedList
		};
		ArrayIterateJob arrayIterateJob = new ArrayIterateJob
		{
			Array = array,
			Sum = sum
		};
		ListIterateJob listIterateJob = new ListIterateJob
		{
			List = list,
			Sum = sum
		};
		LinkedListIterateJob linkedListIterateJob = new LinkedListIterateJob
		{
			List = linkedList,
			Sum = sum
		};
		ChunkedListIterateJob chunkedListIterateJob = new ChunkedListIterateJob
		{
			List = chunkedList,
			Sum = sum
		};
		ArrayIterateJobParallelFor arrayIterateJobParallelFor = new ArrayIterateJobParallelFor
		{
			Array = array,
			Sum = sum
		};
		LinkedListIterateJobParallelFor linkedListIterateJobParallelFor = new LinkedListIterateJobParallelFor
		{
			List = linkedList,
			Sum = sum
		};
		ChunkedListIterateJobParallelFor chunkedListIterateJobParallelFor = new ChunkedListIterateJobParallelFor
		{
			List = chunkedList,
			Sum = sum
		};
		LinkedListInsertJob linkedListInsertJob = new LinkedListInsertJob
		{
			LinkedList = linkedList
		};
		ChunkedListInsertJob chunkedListInsertJob = new ChunkedListInsertJob
		{
			List = chunkedList
		};
		LinkedListRemoveJob linkedListRemoveJob = new LinkedListRemoveJob
		{
			List = linkedList
		};
		ChunkedListRemoveJob chunkedListRemoveJob = new ChunkedListRemoveJob
		{
			List = chunkedList
		};
		NativeIntPtrParallelJob nativeIntPtrParallelJob = new NativeIntPtrParallelJob
		{
			Array = array,
			Sum = nativeIntPtr.GetParallel()
		};
		NativePerJobThreadIntPtrParallelJob nativePerJobThreadIntPtrParallelJob = new NativePerJobThreadIntPtrParallelJob
		{
			Array = array,
			Sum = nativePerJobThreadIntPtr.GetParallel()
		};

		// Run jobs

		listAddJob.Run();
		linkedListAddJob.Run();
		chunkedListAddJob.Run();
		arrayIterateJob.Run();
		listIterateJob.Run();
		linkedListIterateJob.Run();
		chunkedListIterateJob.Run();
		arrayIterateJobParallelFor.Run(array.Length);
		linkedListIterateJobParallelFor.Run(linkedList.Length);
		chunkedListIterateJobParallelFor.RunRanged(chunkedList.Length);
		list.Clear();
		linkedList.Clear();
		chunkedList.Clear();
		linkedListInsertJob.Run();
		chunkedListInsertJob.Run();
		linkedListRemoveJob.Run();
		chunkedListRemoveJob.Run();
		nativeIntPtrParallelJob.Run(array.Length);
		nativePerJobThreadIntPtrParallelJob.Run(array.Length);

		// Dispose native collections

		sum.Dispose();
		array.Dispose();
		list.Dispose();
		linkedList.Dispose();
		chunkedList.Dispose();
		nativeIntPtr.Dispose();
		nativePerJobThreadIntPtr.Dispose();
	}

	// Run the test

	void Start()
	{
		WarmUpJobSystem();

		const int size =
#if UNITY_EDITOR
			1000
#else
			10000
#endif
			;

		const int chunkSize = 1024;
		const int numElementsPerChunk = chunkSize / sizeof(int);

		// Create native collections

		NativeArray<int> sum = new NativeArray<int>(1, Allocator.Temp);
		NativeArray<int> array = new NativeArray<int>(
			size,
			Allocator.Temp);
		NativeList<int> list = new NativeList<int>(
			0,
			Allocator.Temp);
		NativeLinkedList<int> linkedList = new NativeLinkedList<int>(
			0,
			Allocator.Temp);
		NativeChunkedList<int> chunkedList = new NativeChunkedList<int>(
			numElementsPerChunk,
			0,
			Allocator.Temp);
		NativeIntPtr nativeIntPtr = new NativeIntPtr(Allocator.Temp);
		NativePerJobThreadIntPtr nativePerJobThreadIntPtr = new NativePerJobThreadIntPtr(
			Allocator.Temp);

		// Create jobs
		ListAddJob listAddJob = new ListAddJob
		{
			List = list,
			NumElementsToAdd = size
		};
		LinkedListAddJob linkedListAddJob = new LinkedListAddJob
		{
			List = linkedList,
			NumNodesToInsert = size
		};
		ChunkedListAddJob chunkedListAddJob = new ChunkedListAddJob
		{
			List = chunkedList,
			NumElementsToAdd = size
		};
		ArrayIterateJob arrayIterateJob = new ArrayIterateJob
		{
			Array = array,
			Sum = sum
		};
		ListIterateJob listIterateJob = new ListIterateJob
		{
			List = list,
			Sum = sum
		};
		LinkedListIterateJob linkedListIterateJob = new LinkedListIterateJob
		{
			List = linkedList,
			Sum = sum
		};
		ChunkedListIterateJob chunkedListIterateJob = new ChunkedListIterateJob
		{
			List = chunkedList,
			Sum = sum
		};
		ArrayIterateJobParallelFor arrayIterateJobParallelFor = new ArrayIterateJobParallelFor
		{
			Array = array,
			Sum = sum
		};
		LinkedListIterateJobParallelFor linkedListIterateJobParallelFor = new LinkedListIterateJobParallelFor
		{
			List = linkedList,
			Sum = sum
		};
		ChunkedListIterateJobParallelFor chunkedListIterateJobParallelFor = new ChunkedListIterateJobParallelFor
		{
			List = chunkedList,
			Sum = sum
		};
		LinkedListInsertJob linkedListInsertJob = new LinkedListInsertJob
		{
			LinkedList = linkedList,
			NumElementsToAdd = size
		};
		ChunkedListInsertJob chunkedListInsertJob = new ChunkedListInsertJob
		{
			List = chunkedList,
			NumElementsToAdd = size
		};
		LinkedListRemoveJob linkedListRemoveJob = new LinkedListRemoveJob
		{
			List = linkedList,
			NumElementsToRemove = size
		};
		ChunkedListRemoveJob chunkedListRemoveJob = new ChunkedListRemoveJob
		{
			List = chunkedList,
			NumElementsToRemove = size
		};
		NativeIntPtrParallelJob nativeIntPtrParallelJob = new NativeIntPtrParallelJob
		{
			Array = array,
			Sum = nativeIntPtr.GetParallel()
		};
		NativePerJobThreadIntPtrParallelJob nativePerJobThreadIntPtrParallelJob = new NativePerJobThreadIntPtrParallelJob
		{
			Array = array,
			Sum = nativePerJobThreadIntPtr.GetParallel()
		};

		// Run add jobs

		System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

		sw.Reset();
		sw.Start();
		listAddJob.Run();
		long listAddTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		linkedListAddJob.Run();
		long linkedListAddTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		chunkedListAddJob.Run();
		long chunkedListAddTicks = sw.ElapsedTicks;

		// Run iterate jobs

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJob.Run();
		long arrayIterateTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		listIterateJob.Run();
		long listIterateTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJob.Run();
		long linkedListIterateTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		chunkedListIterateJob.Run();
		long chunkedListIterateTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJobParallelFor.Run(size);
		long arrayIterateParallelForTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJobParallelFor.Run(size);
		long linkedListIterateParallelForTicks = sw.ElapsedTicks;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		chunkedListIterateJobParallelFor.RunRanged(size);
		long chunkedListIterateParallelForTicks = sw.ElapsedTicks;

		// Clear native collections

		list.Clear();
		linkedList.Clear();
		chunkedList.Clear();

		// Run insert jobs

		sw.Reset();
		sw.Start();
		linkedListInsertJob.Run();
		long linkedListInsertTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		chunkedListInsertJob.Run();
		long chunkedListInsertTicks = sw.ElapsedTicks;

		// Run remove jobs

		sw.Reset();
		sw.Start();
		linkedListRemoveJob.Run();
		long linkedListRemoveTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		chunkedListRemoveJob.Run();
		long chunkedListRemoveTicks = sw.ElapsedTicks;

		// Run NativeIntPtr and NativePerJobThreadIntPtr jobs

		sw.Reset();
		sw.Start();
		nativeIntPtrParallelJob.Run(size);
		long nativeIntPtrTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		nativePerJobThreadIntPtrParallelJob.Run(size);
		long nativePerJobThreadIntPtrTicks = sw.ElapsedTicks;

		// Report results
		Debug.Log(
			"Operation,Job Type,NativeArray,NativeList,NativeLinkedList,NativeChunkedList\n" +
			"Add,Single,"          + "n/a"                        + ","     + listAddTicks     + "," + linkedListAddTicks                + "," + chunkedListAddTicks                + "\n" +
			"Iterate,Single,"      + arrayIterateTicks            + ","     + listIterateTicks + "," + linkedListIterateTicks            + "," + chunkedListIterateTicks            + "\n" +
			"Iterate,ParallelFor," + arrayIterateParallelForTicks + ","     + "n/a"            + "," + linkedListIterateParallelForTicks + "," + chunkedListIterateParallelForTicks + "\n" +
			"Insert,Single,"       + "n/a"                        + ","     + "n/a"            + "," + linkedListInsertTicks             + "," + chunkedListInsertTicks             + "\n" +
			"Remove,Single,"       + "n/a"                        + ","     + "n/a"            + "," + linkedListRemoveTicks             + "," + chunkedListRemoveTicks);
		Debug.Log(
			"Operation,Job Type,NativeIntPtr,NativePerJobThreadIntPtr\n" +
			"Sum,ParallelFor," + nativeIntPtrTicks + "," + nativePerJobThreadIntPtrTicks);

		// Dispose native collections
		sum.Dispose();
		array.Dispose();
		list.Dispose();
		linkedList.Dispose();
		chunkedList.Dispose();
		nativeIntPtr.Dispose();
		nativePerJobThreadIntPtr.Dispose();

		// Quit
#if UNITY_EDITOR
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}
}
