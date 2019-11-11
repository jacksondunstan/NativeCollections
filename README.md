# Native Collections

A small library of native collections like `NativeArray<T>` suitable to be used with Unity 2018.1 or greater. No additional packages (e.g. ECS or Burst) are required.

# Getting Started

Clone or download this repository and copy the `JacksonDunstanNativeCollections` directory somewhere inside your Unity project's `Assets` directory.

# NativeLinkedList<T>

This is a doubly-linked list backed by parallel arrays. Here's how to use it:

```csharp
// Create an empty list with capacity for five nodes
NativeLinkedList<int> list = new NativeLinkedList<int>(5, Allocator.Temp);

// Add some nodes to the list
list.InsertAfter(list.Tail, 10);
list.InsertAfter(list.Tail, 20);
list.InsertAfter(list.Tail, 30);
list.InsertAfter(list.Tail, 40);
list.InsertAfter(list.Tail, 50);

// Iterate over the list
foreach (int val in list)
{
	Debug.Log(val);
}

// Remove a node from the middle
list.Remove(list.GetEnumeratorAtIndex(2));

// Insert a node into the middle
list.InsertBefore(list.Head.Next, 15);

// Sort the nodes so they can be accessed sequentially
list.SortNodeMemoryAddresses();

// Access the nodes sequentially
for (int i = 0; i < list.Length; ++i)
{
	Debug.Log(list[i]);
}

// Dispose the list's native memory
list.Dispose();
```

There is much more functionality available. See [the source](JacksonDunstanNativeCollections/NativeLinkedList.cs) for more.

To read about the making of this type, see this [article series](https://jacksondunstan.com/articles/4865).

# NativeChunkedList<T>

This is a dynamically-resizable list backed by arrays that store "chunks" of elements and an array of pointers to those chunks. Here's how to use it:

```csharp
// Create an empty list with capacity for 4096 elements in 1024 element chunks
NativeChunkedList<int> list = new NativeChunkedList<int>(
	1024,
	4096,
	Allocator.Temp);

// Add some elements to the list
list.Add(10);
list.Add(20);
list.Add(30);
list.Add(40);
list.Add(50);

// Iterate over the list
foreach (int val in list)
{
	Debug.Log(val);
}

// Remove an element from the middle
list.RemoveAt(2);

// Insert an element into the middle
list.Insert(1, 15);

// Access the elements sequentially
foreach (var e in list.Chunks)
{
	foreach (int val in e)
	{
		Debug.Log(val);
	}
}

// Dispose the list's native memory
list.Dispose();
```

There is much more functionality available. See [the source](JacksonDunstanNativeCollections/NativeChunkedList.cs) for more.

To read about the making of this type, see this [article series](https://jacksondunstan.com/articles/4963).

# NativeHashSet<T>

This is a collection of unique keys that aren't mapped to values. Here's how to use it:

```csharp
// Create an empty set
NativeHashSet<int> set = new NativeHashSet<int>(100, Allocator.Persistent);

// Add some keys
set.TryAdd(123);
set.TryAdd(456);

// Check for containment
set.Contains(123); // true
set.Contains(1000); // false

// Remove a key
set.Remove(123);
```

There is much more functionality available. See [the source](JacksonDunstanNativeCollections/NativeHashSet.cs) for more.

To read about the making of this type, see this [article](https://jacksondunstan.com/articles/5346).

# NativeArray2D<T>

This is a 2D version of `NativeArray<T>`. Here's how to use it:

```csharp
// Create a 2x3 empty array
NativeArray<int> array = new NativeArray<int>(2, 3, Allocator.Temp);

// Set elements of the array
array[0, 1] = 123;
array[1, 2] = 456;

// Get elements of the array
int val123 = array[0, 1];
int val456 = array[1, 2]; 

// Iterate over the array
foreach (int val in array)
{
	Debug.Log(val);
}

// Copy to a managed array
int[,] managed = new int[2, 3];
array.CopyTo(managed);
```

There is much more functionality available. See [the source](JacksonDunstanNativeCollections/NativeArray2D.cs) for more.

To read about the making of this type, see this [article](https://jacksondunstan.com/articles/5416).

# NativeIntPtr and NativeLongPtr

These are pointers to a single `int` or `long`, useful for counters among other purposes. Here's how to use `NativeIntPtr` (`NativeLongPtr` is identical):

```csharp
// Construct with the zero value
NativeIntPtr intPtr0 = new NativeIntPtr(Allocator.Temp);

// Construct with a custom value
NativeIntPtr intPtr = new NativeIntPtr(Allocator.Temp, 123);

// Read and write the value
intPtr.Value = 20;
Debug.Log("Value: " + intPtr.Value); // prints "Value: 20"

// Get a Parallel for use in an IJobParallelFor
NativeIntPtr.Parallel parallel = intPtr.GetParallel();

// Perform atomic writes on it
parallel.Increment();
parallel.Decrement();
parallel.Add(100);

// Dispose the native memory
intPtr0.Dispose();
intPtr.Dispose();
```

To read about the making of this type, see this [article](https://jacksondunstan.com/articles/4940).

# NativePerJobThreadIntPtr and NativePerJobThreadLongPtr

These are pointers to a single `int` or `long`. Compared to `NativeIntPtr` and `NativeLongPtr`, their `Parallel` versions are much faster to use in `ParallelFor` jobs but use more memory. Here's how to use `NativePerJobThreadIntPtr` (`NativePerJobThreadLongPtr` is identical):

```csharp
// Construct with the zero value
NativePerJobThreadIntPtr intPtr0 = new NativePerJobThreadIntPtr(Allocator.Temp);

// Construct with a custom value
NativePerJobThreadIntPtr intPtr = new NativePerJobThreadIntPtr(Allocator.Temp, 123);

// Read and write the value
intPtr.Value = 20;
Debug.Log("Value: " + intPtr.Value); // prints "Value: 20"

// Get a Parallel for use in an IJobParallelFor
NativePerJobThreadIntPtr.Parallel parallel = intPtr.GetParallel();

// Perform atomic writes on it
parallel.Increment();
parallel.Decrement();
parallel.Add(100);

// Dispose the native memory
intPtr0.Dispose();
intPtr.Dispose();
```

To read about the making of this type, see this [article](https://jacksondunstan.com/articles/4942).

# IJobParallelForRanged

This is a job type interface similar to `IJobParallelFor` for job types that want to operate on a range of indices from the batch rather than one index at a time. This is especially useful with `NativeChunkedList<T>` as its enumerable and enumerator types can iterate much more efficiently than with random access via the indexer. Here's how to use this job type interface:

```csharp
[BurstCompile]
struct ChunkedListIterateJobParallelFor : IJobParallelForRanged
{
	public NativeChunkedList<int> List;
	public NativePerJobThreadIntPtr.Parallel Sum;

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
				Sum.Add(chunk.Current);
			}
		}
	}
}
```

To read about the making of this type, see this [article](https://jacksondunstan.com/articles/4976).

# License

[MIT](LICENSE.txt)