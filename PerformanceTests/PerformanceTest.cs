using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using JacksonDunstan.NativeCollections;

public class PerformanceTest : MonoBehaviour
{
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

		NativeArray<int> sum = new NativeArray<int>(1, Allocator.Temp);

		NativeArray<int> array = new NativeArray<int>(
			size,
			Allocator.Temp);
		for (int i = 0; i < size; ++i)
		{
			array[i] = 1;
		}
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

		NativeList<int> list = new NativeList<int>(
			size,
			Allocator.Temp);
		for (int i = 0; i < size; ++i)
		{
			list.Add(1);
		}
		ListIterateJob listIterateJob = new ListIterateJob
		{
			List = list,
			Sum = sum
		};

		NativeLinkedList<int> linkedList = new NativeLinkedList<int>(
			size,
			Allocator.Temp);
		for (int i = 0; i < size; ++i)
		{
			linkedList.PushBack(1);
		}
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
		arrayIterateJob.Run();
		listIterateJob.Run();
		linkedListIterateJob.Run();
		arrayIterateJobParallelFor.Run(array.Length);
		linkedListIterateJobParallelFor.Run(linkedList.Length);

		var sw = new System.Diagnostics.Stopwatch();

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

		sum.Dispose();
		array.Dispose();
		list.Dispose();
		linkedList.Dispose();

		Debug.LogFormat(
			"Operation,Job Type,NativeArray Time,NativeList Time,NativeLinkedList Time\n" +
			"Iterate,Single,{0},{1},{2}\n" +
			"Iterate,ParallelFor,{3},n/a,{4}",
			arrayIterateTime,
			listIterateTime,
			linkedListIterateTime,
			arrayIterateParallelForTime,
			linkedListIterateParallelForTime);
	}

	// Update is called once per frame
	void Update()
	{

	}
}
