using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using JacksonDunstan.NativeCollections;
using UnityEditor;

public class PerformanceTest : MonoBehaviour
{
	// Initialize jobs

	[BurstCompile]
	struct ArraySetJob : IJob
	{
		public NativeArray<int> Array;

		public void Execute()
		{
			for (int i = 0; i < Array.Length; ++i)
			{
				Array[i] = 1;
			}
		}
	}

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
	struct LinkedListInsertAfterTailJob : IJob
	{
		public NativeLinkedList<int> LinkedList;
		public int NumNodesToInsert;

		public void Execute()
		{
			NativeLinkedList<int>.Enumerator e = LinkedList.Tail;
			for (int i = 0; i < NumNodesToInsert; ++i)
			{
				e = LinkedList.InsertAfter(e, 1);
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
		public NativeLinkedList<int> LinkedList;
		public NativeArray<int> Sum;

		public void Execute()
		{
			for (int i = 0; i < LinkedList.Length; ++i)
			{
				Sum[0] += LinkedList[i];
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
		public NativeLinkedList<int> LinkedList;
		public NativeArray<int> Sum;

		public void Execute(int index)
		{
			Sum[0] += LinkedList[index];
		}
	}

	// Use this for initialization
	void Start()
	{
		const int size = 10000000;

		// Create native collections
		NativeArray<int> sum = new NativeArray<int>(1, Allocator.Temp);
		NativeArray<int> array = new NativeArray<int>(
			size,
			Allocator.Temp);
		NativeList<int> list = new NativeList<int>(
			size,
			Allocator.Temp);
		NativeLinkedList<int> linkedList = new NativeLinkedList<int>(
			size,
			Allocator.Temp);

		// Create jobs
		ArraySetJob arraySetJob = new ArraySetJob
		{
			Array = array
		};
		ListAddJob listAddJob = new ListAddJob
		{
			List = list,
			NumElementsToAdd = size
		};
		LinkedListInsertAfterTailJob linkedListInsertAfterTailJob = new LinkedListInsertAfterTailJob
		{
			LinkedList = linkedList,
			NumNodesToInsert = size
		};
		ArrayIterateJob arrayIterateJob = new ArrayIterateJob
		{
			Array = array,
			Sum = sum
		};
		ArrayIterateJobParallelFor arrayIterateJobParallelFor = new ArrayIterateJobParallelFor
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
			LinkedList = linkedList,
			Sum = sum
		};
		LinkedListIterateJobParallelFor linkedListIterateJobParallelFor = new LinkedListIterateJobParallelFor
		{
			LinkedList = linkedList,
			Sum = sum
		};

		// Warm up the job system
		arraySetJob.Run();
		listAddJob.Run();
		linkedListInsertAfterTailJob.Run();
		arrayIterateJob.Run();
		listIterateJob.Run();
		linkedListIterateJob.Run();
		arrayIterateJobParallelFor.Run(array.Length);
		linkedListIterateJobParallelFor.Run(linkedList.Length);

		// Run initialize jobs

		var sw = new System.Diagnostics.Stopwatch();

		sw.Reset();
		sw.Start();
		arraySetJob.Run();
		long arraySetTime = sw.ElapsedMilliseconds;

		sw.Reset();
		sw.Start();
		listAddJob.Run();
		long listAddTime = sw.ElapsedMilliseconds;

		sw.Reset();
		sw.Start();
		linkedListInsertAfterTailJob.Run();
		long linkedListInsertAfterTailTime = sw.ElapsedMilliseconds;

		Debug.Log(linkedList.Length);

		// Run iterate jobs

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJob.Run();
		long arrayIterateTime = sw.ElapsedMilliseconds;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		listIterateJob.Run();
		long listIterateTime = sw.ElapsedMilliseconds;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJob.Run();
		long linkedListIterateTime = sw.ElapsedMilliseconds;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJobParallelFor.Run(array.Length);
		long arrayIterateParallelForTime = sw.ElapsedMilliseconds;

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJobParallelFor.Run(linkedList.Length);
		long linkedListIterateParallelForTime = sw.ElapsedMilliseconds;

		// Dispose native collections
		sum.Dispose();
		array.Dispose();
		list.Dispose();
		linkedList.Dispose();

		// Report results
		Debug.LogFormat(
			"Operation,Job Type,NativeArray Time,NativeList Time,NativeLinkedList Time\n" +
			"Set,Single,{0},n/a,n/a\n" +
			"Add/InsertAfter,Single,n/a,{1},{2}\n" +
			"Iterate,Single,{0},{1},{2}\n" +
			"Iterate,ParallelFor,{3},n/a,{4}",
			arraySetTime,
			listAddTime,
			linkedListInsertAfterTailTime,
			arrayIterateTime,
			listIterateTime,
			linkedListIterateTime,
			arrayIterateParallelForTime,
			linkedListIterateParallelForTime);

		// Quit
#if UNITY_EDITOR
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	// Update is called once per frame
	void Update()
	{

	}
}
