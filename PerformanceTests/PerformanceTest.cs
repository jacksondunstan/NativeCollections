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

	// Insert jobs

	[BurstCompile]
	struct ArrayInsertJob : IJob
	{
		public NativeArray<int> Array;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				// If the array isn't empty
				if (i > 0)
				{
					// Duplicate the last element
					Array[i] = Array[i - 1];

					// Shift all elements back one
					for (int j = i - 2; j >= 0; --j)
					{
						Array[j + 1] = Array[j];
					}
				}

				// Insert the element
				Array[0] = 1;
			}
		}
	}

	[BurstCompile]
	struct ListInsertJob : IJob
	{
		public NativeList<int> List;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				// If the list isn't empty
				if (i > 0)
				{
					// Duplicate the last element to grow the list by one
					List.Add(List[List.Length - 1]);

					// Shift all elements back one
					for (int j = i - 2; j >= 0; --j)
					{
						List[j + 1] = List[j];
					}

					// Insert the element
					List[0] = 1;
				}
				// The list is empty. Just add the element.
				else
				{
					List.Add(1);
				}
			}
		}
	}

	[BurstCompile]
	struct LinkedListInsertJob : IJob
	{
		public NativeLinkedList<int> LinkedList;
		public int NumElementsToAdd;

		public void Execute()
		{
			for (int i = 0; i < NumElementsToAdd; ++i)
			{
				// Insert the element
				LinkedList.InsertBefore(LinkedList.Head, 1);
			}
		}
	}

	// Remove jobs

	[BurstCompile]
	struct ArrayRemoveJob : IJob
	{
		public NativeArray<int> Array;
		public int NumElementsToRemove;

		public void Execute()
		{
			for (int i = Array.Length; i > 0; --i)
			{
				// Shift all elements forward one starting at 'index'
				for (int j = 0; j < NumElementsToRemove - 1; ++j)
				{
					Array[j] = Array[j + 1];
				}
			}
		}
	}

	[BurstCompile]
	struct ListRemoveJob : IJob
	{
		public NativeList<int> List;
		public int NumElementsToRemove;

		public void Execute()
		{
			for (int i = List.Length; i > 0; --i)
			{
				// Shift all elements forward one starting at 'index'
				for (int j = 0; j < NumElementsToRemove - 1; ++j)
				{
					List[j] = List[j + 1];
				}
			}

			// Fix the length
			List.ResizeUninitialized(0);
		}
	}

	[BurstCompile]
	struct LinkedListRemoveJob : IJob
	{
		public NativeLinkedList<int> LinkedList;
		public int NumElementsToRemove;

		public void Execute()
		{
			for (int i = LinkedList.Length; i > 0; --i)
			{
				// Remove the head
				LinkedList.Remove(LinkedList.Head);
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

	// Run the test

	void Start()
	{
		const int size = 10000;

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
		NativeIntPtr nativeIntPtr = new NativeIntPtr(Allocator.Temp);
		NativePerJobThreadIntPtr nativePerJobThreadIntPtr = new NativePerJobThreadIntPtr(
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
		ArrayInsertJob arrayInsertJob = new ArrayInsertJob
		{
			Array = array,
			NumElementsToAdd = size
		};
		ListInsertJob listInsertJob = new ListInsertJob
		{
			List = list,
			NumElementsToAdd = size
		};
		LinkedListInsertJob linkedListInsertJob = new LinkedListInsertJob
		{
			LinkedList = linkedList,
			NumElementsToAdd = size
		};
		ArrayRemoveJob arrayRemoveJob = new ArrayRemoveJob
		{
			Array = array,
			NumElementsToRemove = size
		};
		ListRemoveJob listRemoveJob = new ListRemoveJob
		{
			List = list,
			NumElementsToRemove = size
		};
		LinkedListRemoveJob linkedListRemoveJob = new LinkedListRemoveJob
		{
			LinkedList = linkedList,
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

		// Warm up the job system
		arraySetJob.Run();
		listAddJob.Run();
		linkedListInsertAfterTailJob.Run();
		arrayIterateJob.Run();
		listIterateJob.Run();
		linkedListIterateJob.Run();
		arrayIterateJobParallelFor.Run(array.Length);
		linkedListIterateJobParallelFor.Run(linkedList.Length);
		list.Clear();
		linkedList.Clear();
		arrayInsertJob.Run();
		listInsertJob.Run();
		linkedListInsertJob.Run();
		arrayRemoveJob.Run();
		listRemoveJob.Run();
		linkedListRemoveJob.Run();
		nativeIntPtrParallelJob.Run(array.Length);
		nativePerJobThreadIntPtrParallelJob.Run(array.Length);

		// Run initialize jobs

		var sw = new System.Diagnostics.Stopwatch();

		sw.Reset();
		sw.Start();
		arraySetJob.Run();
		long arraySetTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		listAddJob.Run();
		long listAddTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		linkedListInsertAfterTailJob.Run();
		long linkedListInsertAfterTailTicks = sw.ElapsedTicks;

		Debug.Log(array.Length);
		Debug.Log(list.Length);
		Debug.Log(linkedList.Length);

		// Run iterate jobs

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJob.Run();
		long arrayIterateTicks = sw.ElapsedTicks;
		Debug.Log(sum[0]);

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		listIterateJob.Run();
		long listIterateTicks = sw.ElapsedTicks;
		Debug.Log(sum[0]);

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJob.Run();
		long linkedListIterateTicks = sw.ElapsedTicks;
		Debug.Log(sum[0]);

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		arrayIterateJobParallelFor.Run(array.Length);
		long arrayIterateParallelForTicks = sw.ElapsedTicks;
		Debug.Log(sum[0]);

		sum[0] = 0;
		sw.Reset();
		sw.Start();
		linkedListIterateJobParallelFor.Run(linkedList.Length);
		long linkedListIterateParallelForTicks = sw.ElapsedTicks;
		Debug.Log(sum[0]);

		// Clear native collections

		list.Clear();
		linkedList.Clear();

		// Run insert jobs

		sw.Reset();
		sw.Start();
		arrayInsertJob.Run();
		long arrayInsertTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		listInsertJob.Run();
		long listInsertTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		linkedListInsertJob.Run();
		long linkedListInsertTicks = sw.ElapsedTicks;

		// Run remove jobs

		sw.Reset();
		sw.Start();
		arrayRemoveJob.Run();
		long arrayRemoveTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		listRemoveJob.Run();
		long listRemoveTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		linkedListRemoveJob.Run();
		long linkedListRemoveTicks = sw.ElapsedTicks;

		// Run NativeIntPtr and NativePerJobThreadIntPtr jobs

		sw.Reset();
		sw.Start();
		nativeIntPtrParallelJob.Run(array.Length);
		long nativeIntPtrTicks = sw.ElapsedTicks;

		sw.Reset();
		sw.Start();
		nativePerJobThreadIntPtrParallelJob.Run(array.Length);
		long nativePerJobThreadIntPtrTicks = sw.ElapsedTicks;

		// Dispose native collections
		sum.Dispose();
		array.Dispose();
		list.Dispose();
		linkedList.Dispose();
		nativeIntPtr.Dispose();
		nativePerJobThreadIntPtr.Dispose();

		// Report results
		Debug.Log(
			"Operation,Job Type,NativeArray,NativeList,NativeLinkedList\n" +
			"Initialize,Single,"   + arraySetTicks                + ", "    + listAddTicks                      + "," + linkedListInsertAfterTailTicks    + "\n" +
			"Iterate,Single,"      + arrayIterateTicks            + ","     + listIterateTicks                  + "," + linkedListIterateTicks            + "\n" +
			"Iterate,ParallelFor," + arrayIterateParallelForTicks + ","     + "n/a"                             + "," + linkedListIterateParallelForTicks + "\n" +
			"Insert,Single,"       + arrayInsertTicks             + ","     + listInsertTicks                   + "," + linkedListInsertTicks             + "\n" +
			"Remove,Single,"       + arrayRemoveTicks             + ","     + listRemoveTicks                   + "," + linkedListRemoveTicks);
		Debug.Log(
			"Operation,Job Type,NativeIntPtr,NativePerJobThreadIntPtr\n" +
			"Sum,ParallelFor," + nativeIntPtrTicks + "," + nativePerJobThreadIntPtrTicks);

		// Quit
#if UNITY_EDITOR
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}
}
