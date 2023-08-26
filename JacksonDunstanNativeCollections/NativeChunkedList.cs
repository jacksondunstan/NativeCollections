//-----------------------------------------------------------------------
// <copyright file="NativeChunkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// One chunk of elements in the list
	/// </summary>
	internal unsafe struct NativeChunkedListChunk
	{
		/// <summary>
		/// Array of elements in this chunk. Length is always equal to
		/// <see cref="NativeChunkedListState.m_ChunkLength"/>.
		/// </summary>
		internal void* m_Values;
	}

	/// <summary>
	/// The state of a <see cref="NativeChunkedList{T}"/>. Shared among
	/// instances of the struct via a pointer to unmanaged memory. This has no
	/// type parameters, so it can be used by all list types.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct NativeChunkedListState
	{
		/// <summary>
		/// The number of elements in a chunk
		/// </summary>
		internal int m_ChunkLength;

		/// <summary>
		/// Array of chunks of elements. Length is <see cref="m_Capacity"/>.
		/// </summary>
		internal NativeChunkedListChunk* m_Chunks;

		/// <summary>
		/// Number of elements contained
		/// </summary>
		internal int m_Length;

		/// <summary>
		/// Number of elements that can be contained
		/// </summary>
		internal int m_Capacity;

		/// <summary>
		/// Number of chunks contained
		/// </summary>
		internal int m_ChunksLength;

		/// <summary>
		/// Number of chunks that can be contained
		/// </summary>
		internal int m_ChunksCapacity;

		/// <summary>
		/// Allocator used to create <see cref="m_Chunks"/> and
		/// <see cref="NativeChunkedListChunk.m_Values"/>.
		/// </summary>
		internal Allocator m_Allocator;
	}

	/// <summary>
	/// A dynamically-resizable list native collection that stores an array of
	/// pointers to arrays of "chunks" of elements.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of elements in the list. Must be blittable.
	/// </typeparam>
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("Length = {Length}. Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(NativeChunkedListDebugView<>))]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativeChunkedList<T>
		: IEnumerable<T>
		, IDisposable
#if CSHARP_7_3_OR_NEWER
		where T : unmanaged
#else
		where T : struct
#endif
	{
		/// <summary>
		/// An enumerable for chunks of <see cref="NativeChunkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct ChunksEnumerable
			: IEnumerable<IEnumerable<T>>
			, IDisposable
		{
			/// <summary>
			/// List whose chunks to enumerate
			/// </summary>
			private readonly NativeChunkedList<T> m_List;

			/// <summary>
			/// Index of the first chunk to enumerate
			/// </summary>
			private readonly int m_StartChunksIndex;

			/// <summary>
			/// Index of the element in the first chunk to enumerate
			/// </summary>
			private readonly int m_StartChunkIndex;

			/// <summary>
			/// Index of the last chunk to enumerate
			/// </summary>
			private readonly int m_EndChunksIndex;

			/// <summary>
			/// Index of the element in the last chunk to enumerate
			/// </summary>
			private readonly int m_EndChunkIndex;

			/// <summary>
			/// Create the enumerable to enumerate the chunks of a given list
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="list">
			/// List whose chunks to enumerate
			/// </param>
			/// 
			/// <param name="startChunksIndex">
			/// Index of the first chunk to enumerate
			/// </param>
			/// 
			/// <param name="startChunkIndex">
			/// Index of the element in the first chunk to enumerate
			/// </param>
			/// 
			/// <param name="endChunksIndex">
			/// Index of the last chunk to enumerate
			/// </param>
			/// 
			/// <param name="endChunkIndex">
			/// Index of the element in the last chunk to enumerate
			/// </param>
			internal ChunksEnumerable(
				NativeChunkedList<T> list,
				int startChunksIndex,
				int startChunkIndex,
				int endChunksIndex,
				int endChunkIndex)
			{
				m_List = list;
				m_StartChunksIndex = startChunksIndex;
				m_StartChunkIndex = startChunkIndex;
				m_EndChunksIndex = endChunksIndex;
				m_EndChunkIndex = endChunkIndex;
			}

			/// <summary>
			/// Get an enumerator to enumerate the chunks of the list passed to
			/// the constructor
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator to enumerate the chunks of the list passed to
			/// the constructor. It is currently just before the first chunk, so
			/// call <see cref="ChunkEnumerator.MoveNext"/> to advance it to the
			/// first chunk.
			/// </returns>
			public ChunksEnumerator GetEnumerator()
			{
				return new ChunksEnumerator(
					m_List,
					m_StartChunksIndex,
					m_StartChunkIndex,
					m_EndChunksIndex,
					m_EndChunkIndex);
			}

			/// <summary>
			/// Get an enumerator to enumerate the chunks of the list passed to
			/// the constructor
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator to enumerate the chunks of the list passed to
			/// the constructor. It is currently just before the first chunk, so
			/// call <see cref="ChunkEnumerator.MoveNext"/> to advance it to the
			/// first chunk.
			/// </returns>
			IEnumerator<IEnumerable<T>> IEnumerable<IEnumerable<T>>.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <summary>
			/// Get an enumerator to enumerate the chunks of the list passed to
			/// the constructor
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator to enumerate the chunks of the list passed to
			/// the constructor. It is currently just before the first chunk, so
			/// call <see cref="ChunkEnumerator.MoveNext"/> to advance it to the
			/// first chunk.
			/// </returns>
			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <summary>
			/// Dispose of this enumerable. This is a no-op.
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}
		}

		/// <summary>
		/// An enumerator for chunks of <see cref="NativeChunkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct ChunksEnumerator : IEnumerator<IEnumerable<T>>
		{
			/// <summary>
			/// List whose chunks to enumerate
			/// </summary>
			private readonly NativeChunkedList<T> m_List;

			/// <summary>
			/// Index of the current chunk
			/// </summary>
			private int m_Index;

			/// <summary>
			/// Index of the first chunk to enumerate
			/// </summary>
			private readonly int m_StartChunksIndex;

			/// <summary>
			/// Index of the element in the first chunk to enumerate
			/// </summary>
			private readonly int m_StartChunkIndex;

			/// <summary>
			/// Index of the last chunk to enumerate
			/// </summary>
			private readonly int m_EndChunksIndex;

			/// <summary>
			/// Index of the element in the last chunk to enumerate
			/// </summary>
			private readonly int m_EndChunkIndex;

			/// <summary>
			/// Create the enumerator to enumerate over the chunks of the given
			/// list. It is currently just before the first chunk, so call
			/// <see cref="MoveNext"/> to advance to the first chunk.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="list">
			/// List to enumerate
			/// </param>
			/// 
			/// <param name="startChunksIndex">
			/// Index of the first chunk to enumerate
			/// </param>
			/// 
			/// <param name="startChunkIndex">
			/// Index of the element in the first chunk to enumerate
			/// </param>
			/// 
			/// <param name="endChunksIndex">
			/// Index of the last chunk to enumerate
			/// </param>
			/// 
			/// <param name="endChunkIndex">
			/// Index of the element in the last chunk to enumerate
			/// </param>
			internal ChunksEnumerator(
				NativeChunkedList<T> list,
				int startChunksIndex,
				int startChunkIndex,
				int endChunksIndex,
				int endChunkIndex)
			{
				m_List = list;
				m_Index = startChunksIndex - 1;
				m_StartChunksIndex = startChunksIndex;
				m_StartChunkIndex = startChunkIndex;
				m_EndChunksIndex = endChunksIndex;
				m_EndChunkIndex = endChunkIndex;
			}

			/// <summary>
			/// Move the enumerator to the next chunk
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If the enumerator hasn't moved beyond the last chunk and it is
			/// therefore still safe to call <see cref="Current"/>.
			/// </returns>
			public bool MoveNext()
			{
				m_List.RequireReadAccess();

				m_Index++;
				return m_Index <= m_EndChunksIndex;
			}

			/// <summary>
			/// Get an enumerable for the current chunk
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// An enumerable for the current chunk
			/// </value>
			public ChunkEnumerable Current
			{
				get
				{
					m_List.RequireReadAccess();

					// Just one chunk
					NativeChunkedListState* state = m_List.m_State;
					void* chunk = state->m_Chunks[m_Index].m_Values;
					int startChunkIndex;
					int endChunkIndex;
					if (m_StartChunksIndex == m_EndChunksIndex)
					{
						startChunkIndex = m_StartChunkIndex;
						endChunkIndex = m_EndChunkIndex;
					}
					// Start chunk
					else if (m_Index == m_StartChunksIndex)
					{
						startChunkIndex = m_StartChunkIndex;
						endChunkIndex = state->m_ChunkLength - 1;
					}
					// End chunk
					else if (m_Index == m_EndChunksIndex)
					{
						startChunkIndex = 0;
						endChunkIndex = m_EndChunkIndex;
					}
					// Middle chunk
					else
					{
						startChunkIndex = 0;
						endChunkIndex = state->m_ChunkLength - 1;
					}

					// Create the enumerator
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					int baseIndex = state->m_ChunkLength * m_Index;
					return new ChunkEnumerable(
						m_List,
						chunk,
						startChunkIndex,
						endChunkIndex,
						baseIndex);
#else
					return new ChunkEnumerable(
						m_List,
						chunk,
						startChunkIndex,
						endChunkIndex);
#endif
				}
			}

			/// <summary>
			/// Get an enumerable for the current chunk
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// An enumerable for the current chunk
			/// </value>
			IEnumerable<T> IEnumerator<IEnumerable<T>>.Current
			{
				get
				{
					return Current;
				}
			}

			/// <summary>
			/// Get an enumerable for the current chunk
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// An enumerable for the current chunk
			/// </value>
			object IEnumerator.Current
			{
				get
				{
					return Current;
				}
			}

			/// <summary>
			/// Reset the enumerator to just before the first chunk
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Reset()
			{
				m_List.RequireReadAccess();

				m_Index = m_StartChunksIndex - 1;
			}

			/// <summary>
			/// Dispose of this enumerator. This is a no-op.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}

			/// <summary>
			/// Set whether both read and write access should be allowed for the
			/// enumerator's list. This is used for automated testing purposes
			/// only.
			/// </summary>
			/// 
			/// <param name="allowReadOrWriteAccess">
			/// If both read and write access should be allowed for the
			/// enumerator's list
			/// </param>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			public void TestUseOnlySetAllowReadAndWriteAccess(
				bool allowReadOrWriteAccess)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.SetAllowReadOrWriteAccess(
					m_List.m_Safety,
					allowReadOrWriteAccess);
#endif
			}
		}

		/// <summary>
		/// An enumerable for elements of a chunk of a
		/// <see cref="NativeChunkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct ChunkEnumerable
			: IEnumerable<T>
			, IDisposable
		{
			/// <summary>
			/// List whose chunk is being enumerated
			/// </summary>
			private readonly NativeChunkedList<T> m_List;

			/// <summary>
			/// Chunk whose elements are being enumerated
			/// </summary>
			private readonly void* m_Chunk;

			/// <summary>
			/// Index of the first element to enumerate
			/// </summary>
			private readonly int m_StartChunkIndex;

			/// <summary>
			/// Index of the last element to enumerate
			/// </summary>
			private readonly int m_EndChunkIndex;

			/// <summary>
			/// Overall list index of the first element of the chunk
			/// </summary>
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			private readonly int m_BaseIndex;
#endif

			/// <summary>
			/// Create the enumerable for a chunk
			/// </summary>
			/// 
			/// <param name="list">
			/// List whose chunk is being enumerated
			/// </param>
			/// 
			/// <param name="chunk">
			/// Chunk whose elements are being enumerated
			/// </param>
			/// 
			/// <param name="startChunkIndex">
			/// Index of the first element to enumerate
			/// </param>
			/// 
			/// <param name="endChunkIndex">
			/// Index of the last element to enumerate
			/// </param>
			/// 
			/// <param name="baseIndex">
			/// Overall list index of the first element of the chunk
			/// </param>
			internal ChunkEnumerable(
				NativeChunkedList<T> list,
				void* chunk,
				int startChunkIndex,
				int endChunkIndex
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				, int baseIndex
#endif
			)
			{
				m_List = list;
				m_Chunk = chunk;
				m_StartChunkIndex = startChunkIndex;
				m_EndChunkIndex = endChunkIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				m_BaseIndex = baseIndex;
#endif
			}

			/// <summary>
			/// Create an enumerator for the elements of the chunk
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator for the chunk
			/// </returns>
			public ChunkEnumerator GetEnumerator()
			{
				return new ChunkEnumerator(
					m_List,
					m_Chunk,
					m_StartChunkIndex,
					m_EndChunkIndex
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					, m_BaseIndex
#endif
				);
			}

			/// <summary>
			/// Create an enumerator for the elements of the chunk
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator for the chunk
			/// </returns>
			IEnumerator<T> IEnumerable<T>.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <summary>
			/// Create an enumerator for the elements of the chunk
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// An enumerator for the chunk
			/// </returns>
			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <summary>
			/// Dispose of this enumerable. This is a no-op.
			/// 
			/// This operation has no access requirements on the enumerable's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}
		}

		/// <summary>
		/// An enumerator for elements of a chunk of a
		/// <see cref="NativeChunkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct ChunkEnumerator : IEnumerator<T>
		{
			/// <summary>
			/// List whose chunk is being enumerated
			/// </summary>
			private NativeChunkedList<T> m_List;

			/// <summary>
			/// Chunk whose elements are being enumerated
			/// </summary>
			private readonly void* m_Chunk;

			/// <summary>
			/// Current index into the chunk's elements
			/// </summary>
			private int m_Index;

			/// <summary>
			/// Index of the first element to enumerate
			/// </summary>
			private readonly int m_StartChunkIndex;

			/// <summary>
			/// Index of the last element to enumerate
			/// </summary>
			private readonly int m_EndChunkIndex;

			/// <summary>
			/// Overall list index of the first element of the chunk
			/// </summary>
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			private readonly int m_BaseIndex;
#endif

			/// <summary>
			/// Create the enumerator for a chunk. It is initially just before
			/// the first element. Call <see cref="MoveNext"/> to advance to
			/// the first element.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="list">
			/// List whose chunk is being enumerated
			/// </param>
			/// 
			/// <param name="chunk">
			/// Chunk whose elements are being enumerated
			/// </param>
			/// 
			/// <param name="startChunkIndex">
			/// Index of the first element to enumerate
			/// </param>
			/// 
			/// <param name="endChunkIndex">
			/// Index of the last element to enumerate
			/// </param>
			/// 
			/// <param name="baseIndex">
			/// Overall list index of the first element of the chunk
			/// </param>
			internal ChunkEnumerator(
				NativeChunkedList<T> list,
				void* chunk,
				int startChunkIndex,
				int endChunkIndex
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				, int baseIndex
#endif
			)
			{
				m_List = list;
				m_Chunk = chunk;
				m_Index = startChunkIndex - 1;
				m_StartChunkIndex = startChunkIndex;
				m_EndChunkIndex = endChunkIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				m_BaseIndex = baseIndex;
#endif
			}

			/// <summary>
			/// Move to the next element of the chunk
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If there are more elements of the chunk to enumerate and it is
			/// therefore safe to call <see cref="Current"/> to read their
			/// values.
			/// </returns>
			public bool MoveNext()
			{
				m_Index++;
				return m_Index <= m_EndChunkIndex;
			}

			/// <summary>
			/// Get the current element of the chunk
			/// 
			/// This operation requires read access to the element for the 'get'
			/// and write access to the element for the 'set'
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The current element of the chunk
			/// </value>
			public T Current
			{
				get
				{
					m_List.RequireReadAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					m_List.RequireParallelForAccess(m_BaseIndex + m_Index);
#endif

					return UnsafeUtility.ReadArrayElement<T>(m_Chunk, m_Index);
				}

				[WriteAccessRequired]
				set
				{
					m_List.RequireWriteAccess();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					m_List.RequireParallelForAccess(m_BaseIndex + m_Index);
#endif

					UnsafeUtility.WriteArrayElement(m_Chunk, m_Index, value);
				}
			}

			/// <summary>
			/// Get the current element of the chunk
			/// 
			/// This operation requires read access to the element
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The current element of the chunk
			/// </value>
			object IEnumerator.Current
			{
				get
				{
					return Current;
				}
			}

			/// <summary>
			/// Reset to just before the first element
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Reset()
			{
				m_Index = m_StartChunkIndex - 1;
			}

			/// <summary>
			/// Dispose of this enumerator. This is a no-op.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}

			/// <summary>
			/// Set the ParallelFor safety check ranges of the list this
			/// enumerator is for. This is used for automated testing purposes
			/// only.
			/// </summary>
			/// 
			/// <param name="minIndex">
			/// The minimum index that can safely be accessed. This is zero
			/// outside of a job and in a regular, non-ParallelFor job but set
			/// higher by ParallelFor jobs.
			/// </param>
			/// 
			/// <param name="maxIndex">
			/// The maximum index that can safely be accessed. This is equal to
			/// (m_Length-1) outside of a job and in a regular, non-ParallelFor
			/// job but set lower by ParallelFor jobs.
			/// </param>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			public void TestUseOnlySetParallelForSafetyCheckRange(
				int minIndex = -1,
				int maxIndex = -1)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				m_List.m_MinIndex = minIndex;
				m_List.m_MaxIndex = maxIndex;
#endif
			}

			/// <summary>
			/// Set whether both read and write access should be allowed for the
			/// enumerator's list. This is used for automated testing purposes
			/// only.
			/// </summary>
			/// 
			/// <param name="allowReadOrWriteAccess">
			/// If both read and write access should be allowed for the
			/// enumerator's list
			/// </param>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			public void TestUseOnlySetAllowReadAndWriteAccess(
				bool allowReadOrWriteAccess)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.SetAllowReadOrWriteAccess(
					m_List.m_Safety,
					allowReadOrWriteAccess);
#endif
			}
		}

		/// <summary>
		/// An enumerator for <see cref="NativeChunkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<T>
		{
			/// <summary>
			/// Index of the element
			/// </summary>
			internal int m_Index;

			/// <summary>
			/// List to iterate
			/// </summary>
			internal NativeChunkedList<T> m_List;

			/// <summary>
			/// Create the enumerator for a particular element
			/// </summary>
			/// 
			/// <param name="list">
			/// List to iterate
			/// </param>
			internal Enumerator(NativeChunkedList<T> list)
			{
				m_Index = -1;
				m_List = list;
			}

			/// <summary>
			/// Move to the next element of the list.
			/// 
			/// This operation requires read access to the enumerator's
			/// associated list.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is before or at the end of the list
			/// </returns>
			public bool MoveNext()
			{
				m_List.RequireReadAccess();

				m_Index++;
				return m_Index < m_List.Length;
			}

			/// <summary>
			/// Reset the enumerator to just before the start of the list.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			/// 
			/// This operation is O(1).
			/// </summary>
			public void Reset()
			{
				m_Index = -1;
			}

			/// <summary>
			/// Get the element's value.
			/// 
			/// This operation requires read access to the element for the 'get'
			/// and write access to the element for the 'set'
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The element's value
			/// </value>
			public T Current
			{
				get
				{
					return m_List[m_Index];
				}

				[WriteAccessRequired]
				set
				{
					m_List[m_Index] = value;
				}
			}

			/// <summary>
			/// Get a element's value. Prefer using the generic version of
			/// <see cref="Current"/> as this will cause boxing when enumerating
			/// value type element value. This is provided only for
			/// compatibility with <see cref="IEnumerator"/>. As such, there is
			/// no 'set' for this non-generic property.
			/// 
			/// This operation requires read access to the element.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The element's value
			/// </value>
			object IEnumerator.Current
			{
				get
				{
					return Current;
				}
			}

			/// <summary>
			/// Dispose the enumerator. This operation has no effect and exists
			/// only to satisfy the requirements of <see cref="IDisposable"/>.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			/// 
			/// This operation is O(1).
			/// </summary>
			public void Dispose()
			{
			}

			/// <summary>
			/// Set the ParallelFor safety check ranges of the list this
			/// enumerator is for. This is used for automated testing purposes
			/// only.
			/// </summary>
			/// 
			/// <param name="minIndex">
			/// The minimum index that can safely be accessed. This is zero
			/// outside of a job and in a regular, non-ParallelFor job but set
			/// higher by ParallelFor jobs.
			/// </param>
			/// 
			/// <param name="maxIndex">
			/// The maximum index that can safely be accessed. This is equal to
			/// (m_Length-1) outside of a job and in a regular, non-ParallelFor
			/// job but set lower by ParallelFor jobs.
			/// </param>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			public void TestUseOnlySetParallelForSafetyCheckRange(
				int minIndex = -1,
				int maxIndex = -1)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				m_List.m_MinIndex = minIndex;
				m_List.m_MaxIndex = maxIndex;
#endif
			}

			/// <summary>
			/// Set whether both read and write access should be allowed for the
			/// enumerator's list. This is used for automated testing purposes
			/// only.
			/// </summary>
			/// 
			/// <param name="allowReadOrWriteAccess">
			/// If both read and write access should be allowed for the
			/// enumerator's list
			/// </param>
			[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
			public void TestUseOnlySetAllowReadAndWriteAccess(
				bool allowReadOrWriteAccess)
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.SetAllowReadOrWriteAccess(
					m_List.m_Safety,
					allowReadOrWriteAccess);
#endif
			}
		}

		/// <summary>
		/// State of the list or null if the list is created with the default
		/// constructor or <see cref="Dispose"/> has been called. This is shared
		/// among all instances of the list.
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		private NativeChunkedListState* m_State;

		// These fields are all required when safety checks are enabled
		// They must have these exact types, names, and order
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		/// <summary>
		/// Length of the list. Equal to the number of elements currently
		/// stored. This is set by ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// </summary>
		private int m_Length;

		/// <summary>
		/// The minimum index that can safely be accessed. This is zero outside
		/// of a job and in a regular, non-ParallelFor job but set higher by
		/// ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// 
		/// This field must immediately follow <see cref="m_Length"/>.
		/// </summary>
		private int m_MinIndex;

		/// <summary>
		/// The maximum index that can safely be accessed. This is equal to
		/// (m_Length-1) outside of a job and in a regular, non-ParallelFor job
		/// but set lower by ParallelFor jobs due to specifying
		/// [NativeContainerSupportsMinMaxWriteRestriction].
		/// 
		/// This field must immediately follow <see cref="m_MaxIndex"/>.
		/// </summary>
		private int m_MaxIndex;

		/// <summary>
		/// A handle to information about what operations can be safely
		/// performed on the list at any given time.
		/// </summary>
		private AtomicSafetyHandle m_Safety;

		/// <summary>
		/// A handle that can be used to tell if the list has been disposed yet
		/// or not, which allows for error-checking double disposal.
		/// </summary>
		[NativeSetClassTypeToNullOnSchedule]
		private DisposeSentinel m_DisposeSentinel;
#endif

		/// <summary>
		/// Create the list with an initial capacity. It initially has no
		/// elements.
		///
		/// This complexity of this operation is the allocator's allocation
		/// complexity plus O(N) where N is the given capacity.
		/// </summary>
		/// 
		/// <param name="chunkLength">
		/// Number of elements stored in one chunk. If less than one, one is
		/// used.
		/// </param>
		/// 
		/// <param name="capacity">
		/// Initial capacity. If less than four, four is used. Rounded up to the
		/// next multiple of the given number of elements per chunk.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with. Must be valid.
		/// </param>
		public NativeChunkedList(
			int chunkLength,
			int capacity,
			Allocator allocator)
		{
			// Require a valid allocator
			if (allocator <= Allocator.None)
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			RequireBlittable();

			// Insist on a minimum number of elements per chunk
			if (chunkLength < 1)
			{
				chunkLength = 1;
			}

			// Insist on a minimum capacity
			if (capacity < 4)
			{
				capacity = 4;
			}

			// Allocate the state. The chunks and capacity are initialized in
			// the Capacity property set call below. It is freed in Dispose().
			m_State = (NativeChunkedListState*)UnsafeUtility.Malloc(
				UnsafeUtility.SizeOf<NativeChunkedListState>(),
				UnsafeUtility.AlignOf<NativeChunkedListState>(),
				allocator);
			m_State->m_ChunkLength = chunkLength;
			m_State->m_Chunks = null;
			m_State->m_Length = 0;
			m_State->m_Capacity = 0;
			m_State->m_ChunksLength = 0;
			m_State->m_ChunksCapacity = 0;
			m_State->m_Allocator = allocator;

			// Initialize safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = -1;
			m_MinIndex = -1;
			m_MaxIndex = -1;
#if UNITY_2018_3_OR_NEWER
        	DisposeSentinel.Create(
				out m_Safety,
				out m_DisposeSentinel,
				0,
				allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif

			// Allocate enough space for the initial capacity
			Capacity = capacity;
		}

		/// <summary>
		/// Get or set the capacity of the list. Setting a higher capacity does
		/// not change the <see cref="Length"/> of the list. Setting a lower
		/// capacity removes the elements at the end.
		/// 
		/// This operation requires read access to the 'get' and write access
		/// for the 'set'.
		///
		/// This operation is O(N) where N is the number of elements, but N can
		/// be the number of elements in the added or removed chunks or even 1
		/// in the case that the capacity increases by less than one chunk.
		/// </summary>
		public int Capacity
		{
			get
			{
				RequireReadAccess();

				return m_State->m_Capacity;
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				RequireFullListSafetyCheckBounds();

				// Round the new capacity up to the next multiple of the number
				// of elements per chunk
				int chunkLength = m_State->m_ChunkLength;
				int newCapacity = (value + chunkLength - 1)
					/ chunkLength
					* chunkLength;

				// Number of chunks has increased
				int numOldChunks = m_State->m_ChunksLength;
				int numNewChunks = newCapacity / chunkLength;
				if (numNewChunks > numOldChunks)
				{
					// Need more chunks than we have available
					Allocator allocator = m_State->m_Allocator;
					NativeChunkedListChunk* chunks;
					int chunksCapacity = m_State->m_ChunksCapacity;
					if (numNewChunks > chunksCapacity)
					{
						// Compute the new chunks capacity
						// Try to double, but meet the minimum
						int grown = chunksCapacity * 2;
						int min = numNewChunks > 64 ? numNewChunks : 64;
						chunksCapacity = min > grown ? min : grown;

						// Allocate the new chunk pointers
						chunks = (NativeChunkedListChunk*)UnsafeUtility.Malloc(
							sizeof(NativeChunkedListChunk*) * chunksCapacity,
							sizeof(NativeChunkedListChunk*),
							allocator);

						// Copy the old chunk pointers to the new chunk pointers
						UnsafeUtility.MemCpy(
							chunks,
							m_State->m_Chunks,
							sizeof(NativeChunkedListChunk*) * numOldChunks);

						// Free the old chunk pointers
						UnsafeUtility.Free(
							m_State->m_Chunks,
							m_State->m_Allocator);

						// Use the new chunk pointers
						m_State->m_Chunks = chunks;
						m_State->m_ChunksCapacity = chunksCapacity;
					}
					// No new chunks are needed, so use the old chunks
					else
					{
						chunks = m_State->m_Chunks;
					}

					// Allocate the new chunks
					int size = chunkLength * UnsafeUtility.SizeOf<T>();
					int align = UnsafeUtility.AlignOf<T>();
					for (int i = numOldChunks; i < numNewChunks; ++i)
					{
						chunks[i].m_Values = UnsafeUtility.Malloc(
							size,
							align,
							allocator);
						UnsafeUtility.MemClear(
							chunks[i].m_Values,
							size);
					}

					// Store the new capacity and number of chunks in use
					m_State->m_Capacity = newCapacity;
					m_State->m_ChunksLength = numNewChunks;
				}
				// Capacity has decreased below the number of elements
				else if (value < m_State->m_Length)
				{
					// Number of chunks has decreased
					if (numNewChunks < numOldChunks)
					{
						// Free the unused chunks
						Allocator allocator = m_State->m_Allocator;
						for (int i = numNewChunks; i < numOldChunks; ++i)
						{
							UnsafeUtility.Free(
								m_State->m_Chunks[i].m_Values,
								allocator);
						}

						// Store the new capacity and number of chunks in use
						m_State->m_Capacity = newCapacity;
						m_State->m_ChunksLength = numNewChunks;
					}

					// Store the new length of all elements
					m_State->m_Length = value;
				}
			}
		}

		/// <summary>
		/// Get the number of elements currently in the list. This is always
		/// less than or equal to the <see cref="Capacity"/>.
		/// 
		/// This operation requires read access.
		///
		/// This operation is O(1).
		/// </summary>
		public int Length
		{
			get
			{
				RequireReadAccess();

				return m_State->m_Length;
			}
		}

		/// <summary>
		/// Get an enumerator to the element at index -1. It will refer to the
		/// first element after a call to <see cref="Enumerator.MoveNext()"/>.
		/// 
		/// This operation has no access requirements.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the element at index -1.
		/// </value>
		public Enumerator GetEnumerator()
		{
			return new Enumerator(this);
		}

		/// <summary>
		/// Get an enumerator to the element at index -1. It will refer to the
		/// first element after a call to <see cref="Enumerator.MoveNext()"/>.
		/// 
		/// This operation has no access requirements.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the element at index -1.
		/// </value>
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		/// <summary>
		/// Get an enumerator to the element at index -1. It will refer to the
		/// first element after a call to <see cref="Enumerator.MoveNext()"/>.
		/// 
		/// This operation has no access requirements.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the element at index -1.
		/// </value>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		/// <summary>
		/// Get an enumerator over the chunks starting at the chunk at index -1.
		/// It will refer to the first chunk after a call to
		/// <see cref="ChunksEnumerator.MoveNext()"/>.
		/// 
		/// This operation has no access requirements.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the chunk at index -1.
		/// </value>
		public ChunksEnumerable Chunks
		{
			get
			{
				return new ChunksEnumerable(
					this,
					0,
					0,
					m_State->m_ChunksLength - 1,
					(m_State->m_Length - 1) % m_State->m_ChunkLength);
			}
		}

		/// <summary>
		/// Get an enumerator over the chunks starting at the chunk before the
		/// element at the given starting index and the element before it in its
		/// own chunk. It will refer to the specified element after a call to
		/// <see cref="ChunksEnumerator.MoveNext()"/> and
		/// <see cref="ChunkEnumerator.MoveNext"/>. Enumeration will continue
		/// until the specified end index is reached.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="startIndex">
		/// Index to start enumerating at. Must be non-negative.
		/// </param>
		/// 
		/// <param name="endIndex">
		/// Index to end enumerating at. Must be less than the
		/// <see cref="Length"/>.
		/// </param>
		public ChunksEnumerable GetChunksEnumerable(
			int startIndex,
			int endIndex)
		{
			RequireReadAccess();
			int lastIndex = endIndex - 1;
			RequireIndicesInBounds(startIndex, lastIndex);

			return new ChunksEnumerable(
				this,
				startIndex / m_State->m_ChunkLength,
				startIndex % m_State->m_ChunkLength,
				lastIndex / m_State->m_ChunkLength,
				lastIndex % m_State->m_ChunkLength);
		}

		/// <summary>
		/// Index into the list to get or set an element
		/// 
		/// This operation requires read access to the element for 'get' and
		/// write access to the element for 'set'.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the element to get or set. Must be greater than or equal to
		/// zero and less than <see cref="Length"/>.
		/// </param>
		public T this[int index]
		{
			get
			{
				RequireReadAccess();
				RequireParallelForAccess(index);

				int chunkLength = m_State->m_ChunkLength;
				int chunkIndex = index / chunkLength;
				int chunkArrayIndex = index % chunkLength;
				void* chunkArray = m_State->m_Chunks[chunkIndex].m_Values;
				return UnsafeUtility.ReadArrayElement<T>(
					chunkArray,
					chunkArrayIndex);
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				RequireParallelForAccess(index);

				int chunkLength = m_State->m_ChunkLength;
				int chunkIndex = index / chunkLength;
				int chunkArrayIndex = index % chunkLength;
				void* chunkArray = m_State->m_Chunks[chunkIndex].m_Values;
				UnsafeUtility.WriteArrayElement(
					chunkArray,
					chunkArrayIndex,
					value);
			}
		}

		/// <summary>
		/// Add an element to the end of the list. Increases the
		/// <see cref="Capacity"/> if the list is full.
		/// 
		/// This operation requires write access to the element at the end of
		/// the list when the list isn't at full capacity and write access to
		/// the full list when it is at full capacity.
		///
		/// This operation is O(1) when the list isn't at full capacity and
		/// O(N) where N is the number of elements per chunk when the list is
		/// at full capacity.
		/// </summary>
		/// 
		/// <param name="element">
		/// Element to add
		/// </param>
		[WriteAccessRequired]
		public void Add(T element)
		{
			RequireWriteAccess();

			// Increase capacity if we're full
			int endIndex = m_State->m_Length;
			if (endIndex == m_State->m_Capacity)
			{
				Capacity = endIndex + 1;
			}

			// Add the element to the end
			this[endIndex] = element;

			// Set the new length to account for the added element
			m_State->m_Length = endIndex + 1;
		}

		/// <summary>
		/// Add elements of an array to the end of the list. Increases the
		/// <see cref="Capacity"/> if the list is too full to fit all the
		/// specified elements.
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(1) when the list isn't at full capacity and
		/// O(N) where N is the number of elements per chunk when the list is
		/// at full capacity.
		/// </summary>
		/// 
		/// <param name="array">
		/// Elements to add. Must not be null.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index to insert. Must be in bounds.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to insert. The sum of this and the given start
		/// index must be in bounds. Must be positive.
		/// </param>
		[WriteAccessRequired]
		public void AddRange(T[] array, int startIndex, int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);
			RequireValidRangeWithinArray(array, startIndex, length);

			// Increase capacity if full
			int oldLength = m_State->m_Length;
			int newLength = oldLength + length;
			int capacity = m_State->m_Capacity;
			if (newLength > capacity)
			{
				Capacity = newLength;
			}

			// Copy elements to the end
			for (int i = 0; i < length; ++i)
			{
				this[oldLength + i] = array[startIndex + i];
			}

			// Set the new length to account for the added elements
			m_State->m_Length = newLength;
		}

		/// <summary>
		/// Add elements of an array to the end of the list. Increases the
		/// <see cref="Capacity"/> if the list is too full to fit all the
		/// specified elements.
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(1) when the list isn't at full capacity and
		/// O(N) where N is the number of elements per chunk when the list is
		/// at full capacity.
		/// </summary>
		/// 
		/// <param name="array">
		/// Elements to add. Must have been created via a non-default
		/// constructor and not disposed. The specified elements must be
		/// readable.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index to insert. Must be in bounds.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to insert. The sum of this and the given start
		/// index must be in bounds. Must be positive.
		/// </param>
		[WriteAccessRequired]
		public void AddRange(NativeArray<T> array, int startIndex, int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireValidRangeWithinArray(array, startIndex, length);

			// Increase capacity if full
			int oldLength = m_State->m_Length;
			int newLength = oldLength + length;
			int capacity = m_State->m_Capacity;
			if (newLength > capacity)
			{
				Capacity = newLength;
			}

			// Copy elements to the end
			for (int i = 0; i < length; ++i)
			{
				this[oldLength + i] = array[startIndex + i];
			}

			// Set the new length to account for the added elements
			m_State->m_Length = newLength;
		}

		/// <summary>
		/// Insert an element into the list at a given index and shift all
		/// elements starting at the insertion point back. Increases the
		/// <see cref="Capacity"/> if the list is too full to fit the
		/// element.
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(N) where N is the number of elements in the list
		/// after the insertion point.
		/// </summary>
		/// 
		/// <param name="index">
		/// Index to insert at. Can be any value for an empty list.
		/// </param>
		/// 
		/// <param name="element">
		/// Element to insert
		/// </param>
		[WriteAccessRequired]
		public void Insert(int index, T element)
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			int endIndex = m_State->m_Length - 1;
			if (endIndex == -1)
			{
				Add(element);
			}
			else
			{
				// Duplicate the last element to grow the list by one
				Add(this[endIndex]);

				// Inserting at the end
				if (index == endIndex)
				{
					this[index] = element;
				}
				// Inserting before the end
				else
				{
					// Save the element at the insertion point
					T cur = this[index];

					// Insert the item
					this[index] = element;

					// Shift subsequent elements back by one
					index++;
					do
					{
						T next = this[index];
						this[index] = cur;
						cur = next;
						index++;
					}
					while (index < endIndex);
					this[index] = cur;
				}
			}
		}

		/// <summary>
		/// Remove the element at the given index and shift all subsequent
		/// elements forward by one index to fill the gap
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(N) where N is the number of elements after the
		/// given index.
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the element to remove
		/// </param>
		[WriteAccessRequired]
		public void RemoveAt(int index)
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Shift subsequent elements forward
			var newLength = m_State->m_Length - 1;
			for (int i = index; i < newLength; ++i)
			{
				this[i] = this[i + 1];
			}

			// Decrease the length to account for the removed element
			m_State->m_Length = newLength;
		}

		/// <summary>
		/// Remove the element at the given index by replacing it with the
		/// last element of the list.
		/// 
		/// This operation requires write access to the element at the specified
		/// index and the element at the end of the list.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the element to replace with the last element of the list
		/// </param>
		[WriteAccessRequired]
		public void RemoveAtSwapBack(int index)
		{
			RequireWriteAccess();
			RequireParallelForAccess(index);
			RequireParallelForAccess(m_State->m_Length - 1);

			// Replace the specified element with the last element 
			var newLength = m_State->m_Length - 1;
			this[index] = this[newLength];

			// Decrease the length to account for the removed element
			m_State->m_Length = newLength;
		}

		/// <summary>
		/// Remove all elements.
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(1).
		/// </summary>
		[WriteAccessRequired]
		public void Clear()
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			m_State->m_Length = 0;
		}

		/// <summary>
		/// Replace the list's elements with the elements of an array
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(N) where N is the number of elements in the
		/// given array. If the given array has more elements than the list's
		/// <see cref="Capacity"/>, this operation also includes the complexity
		/// of the allocator passed to the constructor.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to replace the list's elements with. Must not be null.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index to copy. Must be in bounds.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to copy. The sum of this and the given start
		/// index must be in bounds. Must be positive.
		/// </param>
		[WriteAccessRequired]
		public void CopyFrom(T[] array, int startIndex, int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);
			RequireValidRangeWithinArray(array, startIndex, length);

			// Grow to make room for the array
			if (length > m_State->m_Capacity)
			{
				Capacity = length;
			}

			// Copy the array's elements
			for (int dest = 0, src = startIndex; dest < length; ++dest, ++src)
			{
				this[dest] = array[src];
			}

			// The new length is the length of the copied array
			m_State->m_Length = array.Length;
		}

		/// <summary>
		/// Replace the list's elements with the elements of an array
		/// 
		/// This operation requires write access to the full list.
		///
		/// This operation is O(N) where N is the number of elements in the
		/// given array. If the given array has more elements than the list's
		/// <see cref="Capacity"/>, this operation also includes the complexity
		/// of the allocator passed to the constructor.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to replace the list's elements with. Must not be null.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index to copy. Must be in bounds.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to copy. The sum of this and the given start
		/// index must be in bounds. Must be positive.
		/// </param>
		[WriteAccessRequired]
		public void CopyFrom(NativeArray<T> array, int startIndex, int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireValidRangeWithinArray(array, startIndex, length);

			// Grow to make room for the array
			if (length > m_State->m_Capacity)
			{
				Capacity = length;
			}

			// Copy the array's elements
			for (int dest = 0, src = startIndex; dest < length; ++dest, ++src)
			{
				this[dest] = array[src];
			}

			// The new length is the length of the copied array
			m_State->m_Length = array.Length;
		}

		/// <summary>
		/// Copy elements to a managed array, which is optionally allocated.
		/// 
		/// This operation requires read access to the list and read access to
		/// the elements to copy.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy elements to. If null or less than the calculated length
		/// (see notes on that parameter) then a new array with the given length
		/// will be allocated.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// Index of the first element to copy. If negative or greater than or
		/// equal to the length of the list, zero is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified element values copied into it.
		/// </returns>
		public T[] ToArray(
			T[] array = null,
			int startIndex = 0,
			int destIndex = 0,
			int length = -1)
		{
			RequireReadAccess();

			// If length is invalid, copy the whole list
			if (length < 0 || length > m_State->m_Length)
			{
				length = m_State->m_Length;
			}

			// If the enumerator is invalid for this list, start at the head
			if (startIndex < 0 || startIndex >= m_State->m_Length)
			{
				startIndex = 0;
			}

			// If the given array is null or can't hold all the elements, allocate
			// a new one
			if (array == null || array.Length < length)
			{
				array = new T[length];
			}

			// If dest index is invalid, copy to the start
			if (destIndex < 0 || destIndex >= array.Length - length)
			{
				destIndex = 0;
			}

			// If there are any elements to copy
			if (length > 0)
			{
				RequireParallelForAccess(startIndex);
				RequireParallelForAccess(startIndex + length - 1);

				// Copy the elements' values to the array
				do
				{
					// Copy the element's value
					array[destIndex] = this[startIndex];

					// Go to the next element
					startIndex++;

					// Count the copy
					destIndex++;
					length--;
				}
				while (length > 0);
			}

			return array;
		}

		/// <summary>
		/// Copy elements to a <see cref="NativeArray{T}"/>.
		/// 
		/// This operation requires read access to the list and read access to
		/// the elements to copy. It also requires read access to the elements of
		/// the given array to write.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy elements to. If <see cref="NativeArray{T}.IsCreated"/>
		/// returns false for it or its <see cref="Length"/> is less than the
		/// calculated length (see notes on that parameter) then a new array
		/// with the given length will be allocated with the same allocator
		/// that this list uses.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// Index of the first element to copy. If negative or greater than or
		/// equal to the length of the list, zero is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified element values copied into it.
		/// </returns>
		public NativeArray<T> ToNativeArray(
			NativeArray<T> array = default(NativeArray<T>),
			int startIndex = 0,
			int destIndex = 0,
			int length = -1)
		{
			RequireReadAccess();

			// If length is invalid, copy the whole list
			if (length < 0 || length > m_State->m_Length)
			{
				length = m_State->m_Length;
			}

			// If the enumerator is invalid for this list, start at the head
			if (startIndex < 0 || startIndex >= m_State->m_Length)
			{
				startIndex = 0;
			}

			// If the given array is null or can't hold all the elements,
			// allocate a new one
			if (!array.IsCreated || array.Length < length)
			{
				// No need to clear the array since we're about to overwrite its
				// entire contents
				array = new NativeArray<T>(
					length,
					m_State->m_Allocator,
					NativeArrayOptions.UninitializedMemory);
			}

			// If dest index is invalid, copy to the start
			if (destIndex < 0 || destIndex >= array.Length - length)
			{
				destIndex = 0;
			}

			// If there are any elements to copy
			if (length > 0)
			{
				RequireParallelForAccess(startIndex);
				RequireParallelForAccess(startIndex + length - 1);

				// Copy the elements' values to the array
				do
				{
					// Copy the element's value
					array[destIndex] = this[startIndex];

					// Go to the next element
					startIndex++;

					// Count the copy
					destIndex++;
					length--;
				}
				while (length > 0);
			}

			return array;
		}

		/// <summary>
		/// Check if the underlying unmanaged memory has been created and not
		/// freed via a call to <see cref="Dispose"/>.
		/// 
		/// This operation has no access requirements.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// Initially true when a non-default constructor is called but
		/// initially false when the default constructor is used. After
		/// <see cref="Dispose"/> is called, this becomes false. Note that
		/// calling <see cref="Dispose"/> on one copy of a list doesn't result
		/// in this becoming false for all copies if it was true before. This
		/// property should <i>not</i> be used to check whether the list is
		/// usable, only to check whether it was <i>ever</i> usable.
		/// </value>
		public bool IsCreated
		{
			get
			{
				return m_State != null;
			}
		}

		/// <summary>
		/// Prepare the list for usage in a ParallelFor job
		/// 
		/// This operation requires write access to the full list.
		/// 
		/// This complexity of this operation is O(1)
		/// </summary>
		[WriteAccessRequired]
		public void PrepareForParallelForJob()
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = m_State->m_Length;
#endif
		}

		/// <summary>
		/// Release the list's unmanaged memory. Do not use it after this. Do
		/// not call <see cref="Dispose"/> on copies of the list either.
		/// 
		/// This operation requires write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		/// 
		/// This complexity of this operation is O(1) plus the allocator's
		/// deallocation complexity.
		/// </summary>
		[WriteAccessRequired]
		public void Dispose()
		{
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Make sure we're not double-disposing
#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
        	DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
			DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

			// Free the chunks and the chunk pointers
			for (int i = 0; i < m_State->m_ChunksLength; ++i)
			{
				UnsafeUtility.Free(
					m_State->m_Chunks[i].m_Values,
					m_State->m_Allocator);
			}
			UnsafeUtility.Free(
				m_State->m_Chunks,
				m_State->m_Allocator);
			m_State->m_Chunks = null;

			// Free the state
			UnsafeUtility.Free(m_State, m_State->m_Allocator);
			m_State = null;
		}

		/// <summary>
		/// Set the ParallelFor safety check ranges. This is used for automated
		/// testing purposes only.
		/// </summary>
		/// 
		/// <param name="minIndex">
		/// The minimum index that can safely be accessed. This is zero outside
		/// of a job and in a regular, non-ParallelFor job but set higher by
		/// ParallelFor jobs.
		/// </param>
		/// 
		/// <param name="maxIndex">
		/// The maximum index that can safely be accessed. This is equal to
		/// (m_Length-1) outside of a job and in a regular, non-ParallelFor job
		/// but set lower by ParallelFor jobs.
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		public void TestUseOnlySetParallelForSafetyCheckRange(
			int minIndex = -1,
			int maxIndex = -1)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_MinIndex = minIndex;
			m_MaxIndex = maxIndex;
#endif
		}

		/// <summary>
		/// Set whether both read and write access should be allowed. This is
		/// used for automated testing purposes only.
		/// </summary>
		/// 
		/// <param name="allowReadOrWriteAccess">
		/// If both read and write access should be allowed
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		public void TestUseOnlySetAllowReadAndWriteAccess(
			bool allowReadOrWriteAccess)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.SetAllowReadOrWriteAccess(
				m_Safety,
				allowReadOrWriteAccess);
#endif
		}

		/// <summary>
		/// Throw an exception if the given type parameter is not blittable
		/// </summary>
		private static void RequireBlittable()
		{
// No check is necessary because C# 7.3 uses `where T : unmanaged`
#if !CSHARP_7_3_OR_NEWER
			if (!UnsafeUtility.IsBlittable<T>())
			{
				throw new ArgumentException(
					"Type used in NativeChunkedList must be blittable");
			}
#endif
		}

		/// <summary>
		/// Throw an exception when an index is out of the safety check bounds:
		///   [m_MinIndex, m_MaxIndex]
		/// or m_Length doesn't equal m_State->m_Length and the list is being
		/// used by a ParallelFor job
		/// </summary>
		/// 
		/// <param name="index">
		/// Index that must be in the safety check bounds
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireParallelForAccess(int index)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (m_MinIndex != -1 || m_MaxIndex != -1)
			{
				// m_Length isn't synchronized
				if (m_Length != m_State->m_Length)
				{
					throw new IndexOutOfRangeException(
						"List can't be used in a ParallelFor job. Call " +
						"PrepareForParallelFor before executing the job.");
				}

				// The index is out of bounds
				if (index < m_MinIndex || index > m_MaxIndex)
				{
					throw new IndexOutOfRangeException(
						"Index is out of restricted " +
						"ParallelFor range in ReadWriteBuffer.\n" +
						"ReadWriteBuffers are restricted to only read and " +
						"write the node at the job index. You can " +
						"use double buffering strategies to avoid race " +
						"conditions due to reading and writing in parallel " +
						"to the same nodes from a ParallelFor job.");
				}
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the safety check bounds don't encompass the
		/// full list.
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireFullListSafetyCheckBounds()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (m_MinIndex != -1 || m_MaxIndex != -1)
			{
				throw new IndexOutOfRangeException(
					"This operation cannot be performed from a ParallelFor " +
					"job because exclusive access to the full list is " +
					"required to prevent errors. You can " +
					"use double buffering strategies to avoid race " +
					"conditions due to reading and writing in parallel " +
					"to the same elements from a ParallelFor job.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the list isn't readable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireReadAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the list isn't writable
		/// </summary>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireWriteAccess()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}

		/// <summary>
		/// Throw an exception if the given array is null
		/// </summary>
		/// 
		/// <param name="array">
		/// Array that must not be null
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireNonNullManagedArray(T[] array)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (array == null)
			{
				throw new ArgumentNullException(
					"array",
					"The given array must not be null.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the given range is invalid (i.e. it has a
		/// negative length) or isn't within the bounds of a given array
		/// </summary>
		/// 
		/// <param name="array">
		/// Array the range must be within bounds of. Must be usable.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index that must be in bounds
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements after the startIndex that must be in bounds
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireValidRangeWithinArray(
			NativeArray<T> array,
			int startIndex,
			int length)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (length < 0)
			{
				throw new IndexOutOfRangeException(
					"Invalid range length specified. Range lengths must be " +
					"non-negative");
			}
			if (startIndex < 0)
			{
				throw new IndexOutOfRangeException(
					"Invalid range start index specified. Range start " +
					"indices must be non-negative.");
			}
			if (startIndex + length > array.Length)
			{
				throw new IndexOutOfRangeException(
					"Invalid range end index specified for array. Range end " +
					"indices must be less than the array length.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the given range is invalid (i.e. it has a
		/// negative length) or isn't within the bounds of a given array
		/// </summary>
		/// 
		/// <param name="array">
		/// Array the range must be within bounds of. Must be usable.
		/// </param>
		/// 
		/// <param name="startIndex">
		/// First index that must be in bounds
		/// </param>
		/// 
		/// <param name="length">
		/// Number of elements after the startIndex that must be in bounds
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireValidRangeWithinArray(
			T[] array,
			int startIndex,
			int length)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (length < 0)
			{
				throw new IndexOutOfRangeException(
					"Invalid range length specified. Range lengths must be " +
					"non-negative");
			}
			if (startIndex < 0)
			{
				throw new IndexOutOfRangeException(
					"Invalid range start index specified. Range start " +
					"indices must be non-negative.");
			}
			if (startIndex + length > array.Length)
			{
				throw new IndexOutOfRangeException(
					"Invalid range end index specified for array. Range end " +
					"indices must be less than the array length.");
			}
#endif
		}

		/// <summary>
		/// Throw an exception if the given range is invalid for the list
		/// </summary>
		/// 
		/// <param name="startIndex">
		/// Start index that must be non-negative and less than or equal to
		/// the end index
		/// </param>
		/// 
		/// <param name="endIndex">
		/// End index that must be less than the length and greater than or
		/// equal to the start index
		/// </param>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private void RequireIndicesInBounds(int startIndex, int endIndex)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (startIndex < 0)
			{
				throw new IndexOutOfRangeException(
					"Invalid start index. It must be non-negative");
			}

			if (endIndex >= m_State->m_Length)
			{
				throw new IndexOutOfRangeException(
					"Invalid end index. It must be less than the length.");
			}

			if (startIndex > endIndex)
			{
				throw new IndexOutOfRangeException(
					"Invalid range. The start must be less than or equal to " +
					"the end.");
			}
#endif
		}
	}

	/// <summary>
	/// Provides a debugger view of <see cref="NativeChunkedList{T}"/>.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of elements in the list
	/// </typeparam>
	internal sealed class NativeChunkedListDebugView<T>
#if CSHARP_7_3_OR_NEWER
		where T : unmanaged
#else
		where T : struct
#endif
	{
		/// <summary>
		/// List to view
		/// </summary>
		private NativeChunkedList<T> m_List;

		/// <summary>
		/// Create the view for a given list
		/// </summary>
		/// 
		/// <param name="list">
		/// List to view
		/// </param>
		public NativeChunkedListDebugView(NativeChunkedList<T> list)
		{
			m_List = list;
		}

		/// <summary>
		/// Get a managed array version of the list's elements to be viewed in
		/// the debugger.
		/// </summary>
		public T[] Items
		{
			get
			{
				return m_List.ToArray();
			}
		}
	}
}