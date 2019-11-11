//-----------------------------------------------------------------------
// <copyright file="NativeLinkedList.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.txt.
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
	/// The state of a <see cref="NativeLinkedList{T}"/>. Shared among instances
	/// of the struct via a pointer to unmanaged memory. This has no type
	/// parameters, so it can be used by all list types.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct NativeLinkedListState
	{
		/// <summary>
		/// Each node's value. Indices correspond with nextIndexes.
		/// </summary>
		internal void* m_Values;

		/// <summary>
		/// Each node's next node index. Indices correspond with values.
		/// </summary>
		internal int* m_NextIndexes;

		/// <summary>
		/// Each node's previous node index. Indices correspond with values.
		/// </summary>
		internal int* m_PrevIndexes;

		/// <summary>
		/// Index of the first node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int m_HeadIndex;

		/// <summary>
		/// Index of the last node in the list or -1 if there are no nodes in
		/// the list
		/// </summary>
		internal int m_TailIndex;

		/// <summary>
		/// Number of nodes contained
		/// </summary>
		internal int m_Length;

		/// <summary>
		/// Number of nodes that can be contained
		/// </summary>
		internal int m_Capacity;

		/// <summary>
		/// Version of enumerators that are valid for this list. This starts at
		/// 1 and increases by one with each change that invalidates the list's
		/// enumerators.
		/// </summary>
		internal int m_Version;

		/// <summary>
		/// Allocator used to create the backing arrays
		/// </summary>
		internal Allocator m_Allocator;
	}

	/// <summary>
	/// A doubly-linked list native collection.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of nodes in the list. Must be blittable.
	/// </typeparam>
	[NativeContainer]
	[NativeContainerSupportsMinMaxWriteRestriction]
	[DebuggerDisplay("Length = {Length}. Capacity = {Capacity}")]
	[DebuggerTypeProxy(typeof(NativeLinkedListDebugView<>))]
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct NativeLinkedList<T>
		: IEnumerable<T>
		, IDisposable
#if CSHARP_7_3_OR_NEWER
		where T : unmanaged
#else
		where T : struct
#endif
	{
		/// <summary>
		/// An enumerator for <see cref="NativeLinkedList{T}"/>
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator
			: IEnumerator<T>
			, IEquatable<Enumerator>
		{
			/// <summary>
			/// Index of the node
			/// </summary>
			internal int m_Index;

			/// <summary>
			/// Version of the list that this enumerator is valid for
			/// </summary>
			internal readonly int m_Version;

			/// <summary>
			/// List to iterate
			/// </summary>
			internal NativeLinkedList<T> m_List;

			/// <summary>
			/// Create the enumerator for a particular node
			/// </summary>
			/// 
			/// <param name="list">
			/// List to iterate
			/// </param>
			/// 
			/// <param name="index">
			/// Index of the node. Out-of-bounds values are OK.
			/// </param>
			/// 
			/// <param name="version">
			/// Version of the list that this enumerator is valid for
			/// </param>
			internal Enumerator(
				NativeLinkedList<T> list,
				int index,
				int version)
			{
				m_Index = index;
				m_Version = version;
				m_List = list;
			}

			/// <summary>
			/// Get the list this enumerator is for.
			/// </summary>
			/// 
			/// <value>
			/// The list this enumerator is for. It is not necessarily usable.
			/// For example, if this enumerator was initialized with the default
			/// constructor then the list returned will not be usable.
			/// </value>
			public NativeLinkedList<T> List
			{
				get
				{
					return m_List;
				}
			}

			/// <summary>
			/// Get an enumerator to the next node.
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid, an enumerator to the next node or
			/// an invalid enumerator if at the tail node. If this enumerator is
			/// invalid but was constructed via the non-default constructor and
			/// the list it enumerates has not been disposed and has not
			/// invalidated this enumerator, an enumerator to the head node.
			/// Otherwise, an invalid enumerator.
			/// </returns>
			public Enumerator Next
			{
				get
				{
					m_List.RequireReadAccess();

					// The version matches
					if (m_Version == m_List.m_State->m_Version)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Return the next node
							m_List.RequireParallelForAccess(m_Index);
							return new Enumerator(
								m_List,
								m_List.m_State->m_NextIndexes[m_Index],
								m_Version);
						}

						// Not within the list. Return the head.
						return new Enumerator(
							m_List,
							m_List.m_State->m_HeadIndex,
							m_Version);
					}

					// No valid list
					return this;
				}
			}

			/// <summary>
			/// Get an enumerator to the previous node
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid, an enumerator to the previous node
			/// or an invalid enumerator if at the head node. If this enumerator
			/// is invalid but was constructed via the non-default constructor
			/// and the list it enumerates has not been disposed and has not
			/// invalidated this enumerator, an enumerator to the tail node.
			/// Otherwise, an invalid enumerator.
			/// </returns>
			public Enumerator Prev
			{
				get
				{
					m_List.RequireReadAccess();

					// The version matches
					if (m_Version == m_List.m_State->m_Version)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Return the previous node
							m_List.RequireParallelForAccess(m_Index);
							return new Enumerator(
								m_List,
								m_List.m_State->m_PrevIndexes[m_Index],
								m_Version);
						}

						// Not within the list. Return the tail.
						return new Enumerator(
							m_List,
							m_List.m_State->m_TailIndex,
							m_Version);
					}

					// No valid list
					return this;
				}
			}

			/// <summary>
			/// If this enumerator is valid, move to the next node or
			/// invalidate this enumerator if at the tail node. If this
			/// enumerator is invalid but was constructed via the non-default
			/// constructor and the list it enumerates has not been disposed and
			/// has not invalidated this enumerator, move to the head node.
			/// Otherwise, this function has no effect.
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MoveNext()
			{
				m_List.RequireReadAccess();

				// The version matches
				if (m_Version == m_List.m_State->m_Version)
				{
					// Still within the list. The enumerator is valid.
					if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
					{
						// Go to the next node
						m_List.RequireParallelForAccess(m_Index);
						m_Index = m_List.m_State->m_NextIndexes[m_Index];
						return m_Index >= 0;
					}

					// Not within the list. Go to the head.
					m_Index = m_List.m_State->m_HeadIndex;
					return true;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// If this enumerator is valid, move to the previous node or
			/// invalidate this enumerator if at the head node. If this
			/// enumerator is invalid but was constructed via the non-default
			/// constructor and the list it enumerates has not been disposed and
			/// has not invalidated this enumerator, move to the tail node.
			/// Otherwise, this function has no effect.
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MovePrev()
			{
				m_List.RequireReadAccess();

				// The version matches
				if (m_Version == m_List.m_State->m_Version)
				{
					// Still within the list. The enumerator is valid.
					if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
					{
						// Go to the previous node
						m_List.RequireParallelForAccess(m_Index);
						m_Index = m_List.m_State->m_PrevIndexes[m_Index];
						return m_Index >= 0;
					}

					// Not within the list. Go to the tail.
					m_Index = m_List.m_State->m_TailIndex;
					return true;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// If this enumerator is valid, move to the next node a given
			/// number of times or invalidate this enumerator if either at the
			/// tail node or the tail node is moved beyond while moving next. If
			/// this enumerator is invalid but was constructed via the
			/// non-default constructor and the list it enumerates has not been
			/// disposed and has not invalidated this enumerator, move to the
			/// head node. Otherwise, this function has no effect.
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(N) where N is the given number of moves.
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MoveNext(int numSteps)
			{
				m_List.RequireReadAccess();

				// The version matches
				if (m_Version == m_List.m_State->m_Version)
				{
					for (; numSteps > 0; numSteps--)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Go to the next node
							m_List.RequireParallelForAccess(m_Index);
							m_Index = m_List.m_State->m_NextIndexes[m_Index];
						}
						else
						{
							// Not within the list. Go to the tail.
							m_Index = m_List.m_State->m_HeadIndex;
						}
					}
					return m_Index >= 0;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// If this enumerator is valid, move to the previous node a given
			/// number of times or invalidate this enumerator if either at the
			/// head node or the head node moved beyond while moving previous.
			/// If this enumerator is invalid but was constructed via the
			/// non-default constructor and the list it enumerates has not been
			/// disposed and has not invalidated this enumerator, move to the
			/// tail node. Otherwise, this function has no effect.
			/// 
			/// This operation requires read access in general and access to the
			/// node if the enumerator is valid.
			///
			/// This operation is O(N) where N is the given number of moves.
			/// </summary>
			/// 
			/// <returns>
			/// If this enumerator is valid
			/// </returns>
			public bool MovePrev(int numSteps)
			{
				m_List.RequireReadAccess();

				// The version matches
				if (m_Version == m_List.m_State->m_Version)
				{
					for (; numSteps > 0; numSteps--)
					{
						// Still within the list. The enumerator is valid.
						if (m_Index >= 0 && m_Index < m_List.m_State->m_Length)
						{
							// Go to the previous node
							m_List.RequireParallelForAccess(m_Index);
							m_Index = m_List.m_State->m_PrevIndexes[m_Index];
						}
						else
						{
							// Not within the list. Go to the tail.
							m_Index = m_List.m_State->m_TailIndex;
						}
					}
					return m_Index >= 0;
				}

				// Already invalid
				return false;
			}

			/// <summary>
			/// Get the distance between this iterator and a given iterator that
			/// is further towards the tail.
			/// 
			/// This operation requires read access in general and access to all
			/// nodes between the node this enumerator refers to and the given
			/// enumerator, inclusive, if this enumerator is valid and the given
			/// enumerator is valid for the same list as this enumerator.
			///
			/// This operation is O(N) where N is the length of the list.
			/// </summary>
			/// 
			/// <param name="other">
			/// Enumerator to get the distance to. Must be valid for the same
			/// list this enumerator is for.
			/// </param>
			/// 
			/// <returns>
			/// The number of nodes between this enumerator and the given
			/// enumerator if both are valid for the same list and the given
			/// enumerator is either the same enumerator as this enumerator or
			/// is toward the tail compared to this enumerator. Otherwise, a
			/// negative value.
			/// </returns>
			public int GetDistance(Enumerator other)
			{
				m_List.RequireReadAccess();

				// Can't compare invalid enumerators or enumerators for
				// different lists
				if (!IsValid || !other.IsValidFor(m_List))
				{
					return -1;
				}

				// Keep moving next to find the given enumerator
				int distance = 0;
				int index = m_Index;
				do
				{
					// Reached the tail
					if (index < 0)
					{
						return -1;
					}

					// Reached the enumerator to find
					if (index == other.m_Index)
					{
						return distance;
					}

					// Move next
					m_List.RequireParallelForAccess(index);
					index = m_List.m_State->m_NextIndexes[index];
					distance++;
				}
				while (true);
			}

			/// <summary>
			/// Check if an enumerator is valid
			/// 
			/// This operation requires read access unless the enumerator was
			/// initialized with the default constructor.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// If the given enumerator is valid
			/// </returns>
			public bool IsValid
			{
				get
				{
					if (m_List.m_State == null)
					{
						return false;
					}

					m_List.RequireReadAccess();

					return m_Index >= 0
						&& m_Index < m_List.m_State->m_Length
						&& m_Version == m_List.m_State->m_Version;
				}
			}

			/// <summary>
			/// Check if an enumerator is valid for a given list
			/// 
			/// This operation requires read access unless the enumerator was
			/// initialized with the default constructor.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="list">
			/// List to check if the enumerator is valid for
			/// </param>
			/// 
			/// <returns>
			/// If the enumerator is valid for the given list
			/// </returns>
			public bool IsValidFor(NativeLinkedList<T> list)
			{
				if (m_List.m_State == null)
				{
					return false;
				}

				m_List.RequireReadAccess();

				return m_Index >= 0
					&& m_List.m_State == list.m_State
					&& m_Index < m_List.m_State->m_Length
					&& m_Version == m_List.m_State->m_Version;
			}

			/// <summary>
			/// Check if two enumerators refer to the same node lists.
			/// 
			/// This operation requires read access to both enumerators' lists
			/// unless either list was initialized with the default constructor.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="a">
			/// First enumerator to compare
			/// </param>
			/// 
			/// <param name="b">
			/// Second enumerator to compare
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerators refer to the same node and neither
			/// enumerator is invalid.
			/// </returns>
			public static bool operator ==(Enumerator a, Enumerator b)
			{
				// Enumerators without a valid list can't be equal
				if (a.m_List.m_State == null || b.m_List.m_State == null)
				{
					return false;
				}

				a.m_List.RequireReadAccess();
				b.m_List.RequireReadAccess();

				return a.IsValid
					&& b.IsValid
					&& a.m_Index == b.m_Index
					&& a.m_List.m_State == b.m_List.m_State;
			}

			/// <summary>
			/// Check if two enumerators refer to different nodes.
			/// 
			/// This operation requires read access to both enumerators' lists
			/// unless either list was initialized with the default constructor.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="a">
			/// First enumerator to compare
			/// </param>
			/// 
			/// <param name="b">
			/// Second enumerator to compare
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerators refer to different nodes or either
			/// enumerator is invalid.
			/// </returns>
			public static bool operator !=(Enumerator a, Enumerator b)
			{
				// Enumerators without a valid list can't be equal
				if (a.m_List.m_State == null || b.m_List.m_State == null)
				{
					return true;
				}

				a.m_List.RequireReadAccess();
				b.m_List.RequireReadAccess();

				return !a.IsValid
					|| !b.IsValid
					|| a.m_Index != b.m_Index
					|| a.m_List.m_State != b.m_List.m_State;
			}

			/// <summary>
			/// Check if this enumerator refer to the same node as another
			/// enumerator.
			/// 
			/// This operation requires read access to both enumerators' lists
			/// unless either list was initialized with the default constructor.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="e">
			/// Enumerator to compare with
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerator refers to the same node as this
			/// enumerator and is of the same type and neither enumerator is
			/// invalid.
			/// </returns>
			public bool Equals(Enumerator e)
			{
				return this == e;
			}

			/// <summary>
			/// Check if this enumerator refer to the same node as another
			/// enumerator.
			/// 
			/// This operation requires read access to both enumerators' lists
			/// unless either list was initialized with the default constructor.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <param name="obj">
			/// Enumerator to compare with
			/// </param>
			/// 
			/// <returns>
			/// If the given enumerator refers to the same node as this
			/// enumerator and is of the same type and neither enumerator is
			/// invalid.
			/// </returns>
			public override bool Equals(object obj)
			{
				return obj is Enumerator && this == (Enumerator)obj;
			}

			/// <summary>
			/// Get a hash code for this enumerator. If the enumerator is
			/// mutated such as by calling <see cref="MoveNext()"/>, the
			/// returned hash code will no longer match values returned by
			/// subsequent calls to this function.
			/// 
			/// This operation has no access requirements on the enumerator's
			/// associated list.
			/// 
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <returns>
			/// A hash code for this enumerator
			/// </returns>
			public override int GetHashCode()
			{
				// Suppress "non-readonly field" warning because we don't have a
				// readonly-only way to generate a hash code since Index is
				// mutable to comply with the IEnumerator interface.
#pragma warning disable RECS0025
				return m_Index;
#pragma warning restore RECS0025
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
			/// Reset the enumerator to the head of the list if it wasn't
			/// created using the default constructor.
			/// 
			/// This operation requires read access to the list.
			/// 
			/// This operation is O(1).
			/// </summary>
			public void Reset()
			{
				m_List.RequireReadAccess();

				m_Index = m_List.m_State->m_HeadIndex;
			}

			/// <summary>
			/// Get or set a node's value.
			/// 
			/// This operation requires read access to the node for 'get' and
			/// write access to the node for 'set'.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value
			/// </value>
			public T Value
			{
				get
				{
					m_List.RequireReadAccess();
					m_List.RequireParallelForAccess(m_Index);

					return UnsafeUtility.ReadArrayElement<T>(
						m_List.m_State->m_Values,
						m_Index);
				}

				[WriteAccessRequired]
				set
				{
					m_List.RequireWriteAccess();
					m_List.RequireParallelForAccess(m_Index);

					UnsafeUtility.WriteArrayElement(
						m_List.m_State->m_Values,
						m_Index,
						value);
				}
			}

			/// <summary>
			/// Get or set a node's value. This is provided only for
			/// compatibility with <see cref="IEnumerator{T}"/>. As such, there is
			/// no 'set' for this property.
			/// 
			/// This operation requires read access to the node.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value
			/// </value>
			public T Current
			{
				get
				{
					m_List.RequireReadAccess();
					m_List.RequireParallelForAccess(m_Index);

					return UnsafeUtility.ReadArrayElement<T>(
						m_List.m_State->m_Values,
						m_Index);
				}
			}

			/// <summary>
			/// Get a node's value. Prefer using the generic version of
			/// <see cref="Current"/> as this will cause boxing when enumerating
			/// value type node value. This is provided only for compatibility
			/// with <see cref="IEnumerator"/>. As such, there is no 'set' for
			/// this non-generic property.
			/// 
			/// This operation requires read access to the node.
			///
			/// This operation is O(1).
			/// </summary>
			/// 
			/// <value>
			/// The node's value or null if this enumerator is invalid.
			/// </value>
			object IEnumerator.Current
			{
				get
				{
					m_List.RequireReadAccess();
					m_List.RequireParallelForAccess(m_Index);

					return UnsafeUtility.ReadArrayElement<T>(
						m_List.m_State->m_Values,
						m_Index);
				}
			}
		}

		/// <summary>
		/// State of the list or null if the list is created with the default
		/// constructor or <see cref="Dispose"/> has been called. This is shared
		/// among all instances of the list.
		/// </summary>
		[NativeDisableUnsafePtrRestriction]
		private NativeLinkedListState* m_State;

		// These fields are all required when safety checks are enabled
		// They must have these exact types, names, and order
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		/// <summary>
		/// Length of the list. Equal to the number of nodes currently stored.
		/// This is set by ParallelFor jobs due to specifying
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
		/// This field must immediately follow <see cref="m_MinIndex"/>.
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
		/// Create the list with an initial capacity. It initially has no nodes.
		///
		/// This complexity of this operation is O(1) plus the allocator's
		/// allocation complexity.
		/// </summary>
		/// 
		/// <param name="capacity">
		/// Initial capacity. If less than four, four is used.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with. Must be valid.
		/// </param>
		public NativeLinkedList(int capacity, Allocator allocator)
		{
			// Require a valid allocator
			if (allocator <= Allocator.None)
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			RequireBlittable();

			// Insist on a minimum capacity
			if (capacity < 4)
			{
				capacity = 4;
			}

			// Allocate the state. It is freed in Dispose().
			m_State = (NativeLinkedListState*)UnsafeUtility.Malloc(
				sizeof(NativeLinkedListState),
				UnsafeUtility.AlignOf<NativeLinkedListState>(),
				allocator);

			// Create the backing arrays. There's no need to clear them since we
			// make no assumptions about the contents anyways.
			m_State->m_Values = UnsafeUtility.Malloc(
				UnsafeUtility.SizeOf<T>() * capacity,
				UnsafeUtility.AlignOf<T>(),
				allocator
			);
			m_State->m_NextIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			m_State->m_PrevIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);

			// Store the allocator for future allocation and deallocation
			// operations
			m_State->m_Allocator = allocator;

			// Initially empty with the given capacity
			m_State->m_Length = 0;
			m_State->m_Capacity = capacity;

			// The list is empty so there is no head or tail
			m_State->m_HeadIndex = -1;
			m_State->m_TailIndex = -1;

			// Version starts at one so that the default (0) is never used
			m_State->m_Version = 1;

			// Initialize safety ranges
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			m_Length = -1;
			m_MinIndex = -1;
			m_MaxIndex = -1;
#if UNITY_2018_3_OR_NEWER
        	DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif
		}

		/// <summary>
		/// Create the list with an initial capacity. It initially has no nodes.
		///
		/// This complexity of this operation is O(N) where N is the given
		/// length and additionally has the complexity of the given allocator's
		/// allocation operation.
		/// </summary>
		/// 
		/// <param name="capacity">
		/// Initial capacity. This is capped at a minimum of four and the given
		/// count.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to add. This is capped at a minimum of zero.
		/// </param>
		/// 
		/// <param name="allocator">
		/// Allocator to allocate unmanaged memory with. Must be valid.
		/// </param>
		public NativeLinkedList(int capacity, int length, Allocator allocator)
		{
			// Require a valid allocator
			if (allocator <= Allocator.None)
			{
				throw new ArgumentException(
					"Allocator must be Temp, TempJob or Persistent",
					"allocator");
			}

			RequireBlittable();

			// Insist on a non-negative length
			if (length < 0)
			{
				length = 0;
			}

			// Insist on a minimum capacity
			int requiredCapacity = Math.Max(4, length);
			if (capacity < requiredCapacity)
			{
				capacity = requiredCapacity;
			}

			// Allocate the state. It is freed in Dispose().
			m_State = (NativeLinkedListState*)UnsafeUtility.Malloc(
				sizeof(NativeLinkedListState),
				UnsafeUtility.AlignOf<NativeLinkedListState>(),
				allocator);

			// Create the backing arrays.
			int valuesSize = UnsafeUtility.SizeOf<T>() * capacity;
			m_State->m_Values = UnsafeUtility.Malloc(
				valuesSize,
				UnsafeUtility.AlignOf<T>(),
				allocator
			);
			m_State->m_NextIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);
			m_State->m_PrevIndexes = (int*)UnsafeUtility.Malloc(
				sizeof(int) * capacity,
				UnsafeUtility.AlignOf<int>(),
				allocator);

			// If there are any nodes to initialize
			int endIndex = length - 1;
			if (length > 0)
			{
				// Clear the node values to their defaults
				UnsafeUtility.MemClear(m_State->m_Values, valuesSize);

				// Initialize next pointers to the next index and the last next
				// pointer to an invalid index
				for (int i = 0; i < endIndex; ++i)
				{
					m_State->m_NextIndexes[i] = i + 1;
				}
				m_State->m_NextIndexes[endIndex] = -1;

				// Initialize prev pointers to the previous index and the first
				// prev pointer to an invalid index
				m_State->m_PrevIndexes[0] = -1;
				for (int i = 1; i < length; ++i)
				{
					m_State->m_PrevIndexes[i] = i - 1;
				}

				// The first node is the head and the last node is the tail
				m_State->m_HeadIndex = 0;
				m_State->m_TailIndex = endIndex;
			}
			else
			{
				// The list is empty so there is no head or tail
				m_State->m_HeadIndex = -1;
				m_State->m_TailIndex = -1;
			}

			// Store the allocator for future allocation and deallocation
			// operations
			m_State->m_Allocator = allocator;

			// Initially sized to the given count with the given capacity
			m_State->m_Length = length;
			m_State->m_Capacity = capacity;

			// Version starts at one so that the default (0) is never used
			m_State->m_Version = 1;

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
		}

		/// <summary>
		/// Get the capacity of the list. This is always greater than or equal
		/// to its <see cref="Length"/>.
		/// 
		/// This operation requires read access.
		///
		/// This operation is O(1).
		/// </summary>
		public int Capacity
		{
			get
			{
				RequireReadAccess();

				return m_State->m_Capacity;
			}
		}

		/// <summary>
		/// Get the number of nodes currently in the list. This is always less
		/// than or equal to the <see cref="Capacity"/>.
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
		/// Get an enumerator to the head of the list or an invalid enumerator
		/// if the list is empty.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the head of the list or an invalid enumerator if
		/// the list is empty.
		/// </value>
		public Enumerator Head
		{
			get
			{
				RequireReadAccess();

				return new Enumerator(
					this,
					m_State->m_HeadIndex,
					m_State->m_Version);
			}
		}

		/// <summary>
		/// Get an enumerator to the tailI of the list or an invalid enumerator
		/// if the list is empty.
		/// 
		/// This operation requires read access.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An enumerator to the tailI of the list or an invalid enumerator if
		/// the list is empty.
		/// </value>
		public Enumerator Tail
		{
			get
			{
				RequireReadAccess();

				return new Enumerator(
					this,
					m_State->m_TailIndex,
					m_State->m_Version);
			}
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		public Enumerator GetEnumerator()
		{
			RequireReadAccess();

			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			RequireReadAccess();

			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Get an invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <value>
		/// An invalid enumerator that will refer to the head of the list
		/// after a call to <see cref="Enumerator.MoveNext()"/> or
		/// <see cref="Enumerator.Next"/>.
		/// </value>
		IEnumerator IEnumerable.GetEnumerator()
		{
			RequireReadAccess();

			return new Enumerator(this, -1, m_State->m_Version);
		}

		/// <summary>
		/// Get an enumerator for the node at a given index.
		/// 
		/// This operation requires read access.
		/// 
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the node to get an enumerator for. If negative or greater
		/// than or equal to <see cref="Length"/>, the enumerator will be
		/// invalid.
		/// </param>
		/// 
		/// <returns>
		/// The enumerator at the given index. Note that this index is not the
		/// number of next pointers to follow from the head of the list but
		/// instead the index into the underlying node array. Do not use this
		/// after modifying the list until calling
		/// <see cref="SortNodeMemoryAddresses"/>. If the given index is
		/// negative or greater than or equal to <see cref="Length"/>, the
		/// enumerator will be invalid.
		/// </returns>
		public Enumerator GetEnumeratorAtIndex(int index)
		{
			RequireReadAccess();

			return new Enumerator(this, index, m_State->m_Version);
		}

		/// <summary>
		/// Index into the list as if it were an array. Do not use this after
		/// modifying the list until calling
		/// <see cref="SortNodeMemoryAddresses"/>.
		/// 
		/// This operation requires read access to the node for 'get' and write
		/// access to the node for 'set'.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="index">
		/// Index of the node to get or set. Must be greater than or equal to
		/// zero and less than <see cref="Length"/>.
		/// </param>
		public T this[int index]
		{
			get
			{
				RequireReadAccess();
				RequireParallelForAccess(index);

				return UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					index);
			}

			[WriteAccessRequired]
			set
			{
				RequireWriteAccess();
				RequireParallelForAccess(index);

				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					index,
					value);
			}
		}

		/// <summary>
		/// Insert a node after the node referred to by the given enumerator.
		/// This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(1) when the list has enough capacity to hold the
		/// inserted node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted node or an invalid enumerator if the
		/// given enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(Enumerator enumerator, T value)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty, so we don't know where to insert
				if (endIndex > 0)
				{
					return enumerator;
				}

				// Need room for one more node
				EnsureCapacity(1);

				// Insert at the beginning
				UnsafeUtility.WriteArrayElement(m_State->m_Values, 0, value);
				m_State->m_NextIndexes[0] = -1;
				m_State->m_PrevIndexes[0] = m_State->m_TailIndex;

				// The added node is now the head and tail
				m_State->m_HeadIndex = 0;
				m_State->m_TailIndex = 0;

				// Count the newly-added node
				m_State->m_Length = 1;

				// Return an enumerator to the added node
				return new Enumerator(this, 0, m_State->m_Version);
			}

			// Need room for one more node
			EnsureCapacity(1);

			// By adding C after B, we're changing from this:
			//   values:      [  A, B,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   values:      [  A, B,  D, C ]
			//   nextIndexes: [  1, 3, -1, 2 ]
			//   prevIndexes: [ -1, 0,  3, 1 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the value to insert at the end node
			UnsafeUtility.WriteArrayElement(m_State->m_Values, endIndex, value);

			// Point the end node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[endIndex] = insertNextIndex;

			// Point the end node's previous to the insert node
			m_State->m_PrevIndexes[endIndex] = enumerator.m_Index;

			// Point the insert node's next to the end node
			m_State->m_NextIndexes[enumerator.m_Index] = endIndex;

			// The list was empty, so the inserted node is both head and tail
			if (m_State->m_HeadIndex < 0)
			{
				m_State->m_HeadIndex = endIndex;
				m_State->m_TailIndex = endIndex;
			}
			// Point the next node's prev to the end node
			else if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = endIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the end node where we moved it
			else
			{
				m_State->m_TailIndex = endIndex;
			}

			// Count the newly-added node
			m_State->m_Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the nodes of a given list after the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given list.
		///
		/// This operation is O(N) where N is the length of the given list and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert. Must be readable.
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted head node or the given enumerator if
		/// the given list is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			list.RequireReadAccess();
			list.RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// list is empty so there's nothing to insert
				if (endIndex > 0 || list.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given list
				EnsureCapacity(list.Length);

				// Copy to the end
				CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted list's length is now the list's length
				m_State->m_Length = list.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the elements of a given array after the node referred to by
		/// the given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the length of the given array and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted head node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			NativeArray<T> array)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || array.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(array.Length);

				// Copy to the end
				CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = array.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(array.Length);

			// Insert the list at the end
			CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + array.Length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the elements of a given array after the node referred to by
		/// the given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(N) where N is the length of the given array and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. Must not be null.
		/// </param>
		/// 
		/// <returns>
		/// The given enumerator if invalid for this list or the given array is
		/// empty. Otherwise, an enumerator to the inserted head node.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(Enumerator enumerator, T[] array)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || array.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(array.Length);

				// Copy to the end
				CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = array.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given array
			EnsureCapacity(array.Length);

			// Insert the list at the end
			CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + array.Length;

			// The first inserted node
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of nodes after the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the node range of given list.
		///
		/// This operation is O(N) where N is the number of nodes to insert and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="start">
		/// Enumerator to the first node to insert. Must refer to the same node
		/// as the given end enumerator or be toward the head relative to it. If
		/// invalid for this list, this function has no effect.
		/// </param>
		/// 
		/// <param name="end">
		/// Enumerator to the last node to insert. Must refer to the same node
		/// as the given start enumerator or be toward the tail relative to it.
		/// If invalid for this list, this function has no effect.
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted start node or the given enumerator if
		/// either given enumerator is invalid for this list.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			Enumerator start,
			Enumerator end)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Enumerators must be for this list
			if (start.IsValidFor(this) && end.IsValidFor(this))
			{
				return enumerator;
			}

			// Compute how many nodes to insert
			int numInsertNodes = start.GetDistance(end) + 1;

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or
				// there's nothing to insert
				if (endIndex > 0 || numInsertNodes == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given list
				EnsureCapacity(numInsertNodes);

				// Copy to the end
				CopyToEnd(start, end, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted list's length is now the list's length
				m_State->m_Length = numInsertNodes;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(numInsertNodes);

			// Insert the list at the end
			CopyToEnd(start, end, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + numInsertNodes;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of the elements of a given array after the node
		/// referred to by the given enumerator. This doesn't invalidate any
		/// enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the given length and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
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
		/// 
		/// <returns>
		/// An enumerator to the inserted head node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			NativeArray<T> array,
			int startIndex,
			int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireValidRangeWithinArray(array, startIndex, length);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(length);

				// Copy to the end
				CopyToEnd(
					array,
					startIndex,
					length,
					out copiedHeadIndex,
					out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(length);

			// Insert the list at the end
			CopyToEnd(
				array,
				startIndex,
				length,
				out copiedHeadIndex,
				out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of the elements of a given array after the node
		/// referred to by the given enumerator. This doesn't invalidate any
		/// enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the given length and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
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
		/// 
		/// <returns>
		/// An enumerator to the inserted head node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertAfter(
			Enumerator enumerator,
			T[] array,
			int startIndex,
			int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);
			RequireValidRangeWithinArray(array, startIndex, length);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(length);

				// Copy to the end
				CopyToEnd(
					array,
					startIndex,
					length,
					out copiedHeadIndex,
					out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(length);

			// Insert the list at the end
			CopyToEnd(
				array,
				startIndex,
				length,
				out copiedHeadIndex,
				out copiedTailIndex);

			// Point the inserted tail node's next to the next node
			int insertNextIndex = m_State->m_NextIndexes[enumerator.m_Index];
			m_State->m_NextIndexes[copiedTailIndex] = insertNextIndex;

			// Point the inserted head node's previous to the insert node
			m_State->m_PrevIndexes[copiedHeadIndex] = enumerator.m_Index;

			// Point the insert node's next to the inserted head node
			m_State->m_NextIndexes[enumerator.m_Index] = copiedHeadIndex;

			// Point the next node's prev to the inserted tail node
			if (insertNextIndex >= 0)
			{
				m_State->m_PrevIndexes[insertNextIndex] = copiedTailIndex;
			}
			// The insert node was the tail, so update the tail index to
			// point to the inserted tail node where we moved it
			else
			{
				m_State->m_TailIndex = copiedTailIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + length;

			// The first inserted node 
			return new Enumerator(this, copiedHeadIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a node before the node referred to by the given enumerator.
		/// This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(1) when the list has enough capacity to hold the
		/// inserted node or O(N) plus the allocator's deallocation and
		/// allocation complexity when it doesn't.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="value">
		/// Value of the node to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted node or an invalid enumerator if the
		/// given enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(Enumerator enumerator, T value)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty, so we don't know where to insert
				if (endIndex > 0)
				{
					return enumerator;
				}

				// Need room for one more node
				EnsureCapacity(1);

				// Insert at the beginning
				UnsafeUtility.WriteArrayElement(m_State->m_Values, 0, value);
				m_State->m_NextIndexes[0] = -1;
				m_State->m_PrevIndexes[0] = m_State->m_TailIndex;

				// The added node is now the head and tail
				m_State->m_HeadIndex = 0;
				m_State->m_TailIndex = 0;

				// Count the newly-added node
				m_State->m_Length = 1;

				// Return an enumerator to the added node
				return new Enumerator(this, 0, m_State->m_Version);
			}

			// Need room for one more node
			EnsureCapacity(1);

			// By adding B before C, we're changing from this:
			//   values:      [  A, C,  D ]
			//   nextIndexes: [  1, 2, -1 ]
			//   prevIndexes: [ -1, 0,  1 ]
			// To this:
			//   values:      [  A, C,  D, B ]
			//   nextIndexes: [  3, 2, -1, 1 ]
			//   prevIndexes: [ -1, 3,  1, 0 ]
			// Terminology:
			//   "insert node": node to insert after (B)
			//   "next node": node previously next of the insert node (A)
			//   "prev node": node previously prev of the insert node (D)
			//   "end node": node just after the end of the nodes array (D + 1)

			// Set the value to insert at the end node
			UnsafeUtility.WriteArrayElement(m_State->m_Values, endIndex, value);

			// Point the end node's next to the insert node
			m_State->m_NextIndexes[endIndex] = enumerator.m_Index;

			// Point the end node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[endIndex] = insertPrevIndex;

			// Point the insert node's prev to the end node
			m_State->m_PrevIndexes[enumerator.m_Index] = endIndex;

			// Point the prev node's next to the end node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = endIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the end node where we moved it
			else
			{
				m_State->m_HeadIndex = endIndex;
			}

			// Count the newly-added node
			m_State->m_Length = endIndex + 1;

			// The inserted node 
			return new Enumerator(this, endIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the nodes of a given list before the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given list.
		///
		/// This operation is O(N) where N is the length of the given list and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="list">
		/// List whose nodes to insert
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given list is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			NativeLinkedList<T> list)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			list.RequireReadAccess();
			list.RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// list is empty so there's nothing to insert
				if (endIndex > 0 || list.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given list
				EnsureCapacity(list.Length);

				// Copy to the end
				CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted list's length is now the list's length
				m_State->m_Length = list.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(list.m_State->m_Length);

			// Insert the list at the end
			CopyToEnd(list, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + list.m_State->m_Length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the elements of a given array before the node referred to by
		/// the given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the length of the given array and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			NativeArray<T> array)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || array.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(array.Length);

				// Copy to the end
				CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = array.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(array.Length);

			// Insert the list at the end
			CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + array.Length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert the elements of a given array before the node referred to by
		/// the given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(N) where N is the length of the given array and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert before. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. Must not be null.
		/// </param>
		/// 
		/// <returns>
		/// The given enumerator if invalid for this list or the given array is
		/// empty. Otherwise, an enumerator to the inserted head node.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(Enumerator enumerator, T[] array)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || array.Length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(array.Length);

				// Copy to the end
				CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = array.Length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(array.Length);

			// Insert the list at the end
			CopyToEnd(array, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + array.Length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of nodes before the node referred to by the
		/// given enumerator. This doesn't invalidate any enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the node range of given list.
		///
		/// This operation is O(N) where N is the number of nodes to insert and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="start">
		/// Enumerator to the first node to insert. Must refer to the same node
		/// as the given end enumerator or be toward the head relative to it. If
		/// invalid for this list, this function has no effect.
		/// </param>
		/// 
		/// <param name="end">
		/// Enumerator to the last node to insert. Must refer to the same node
		/// as the given start enumerator or be toward the tail relative to it.
		/// If invalid for this list, this function has no effect.
		/// </param>
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given list is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			Enumerator start,
			Enumerator end)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Enumerators must be for this list
			if (start.IsValidFor(this) && end.IsValidFor(this))
			{
				return enumerator;
			}

			// Compute how many nodes to insert
			int numInsertNodes = start.GetDistance(end) + 1;

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// list is empty so there's nothing to insert
				if (endIndex > 0 || numInsertNodes == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given list
				EnsureCapacity(numInsertNodes);

				// Copy to the end
				CopyToEnd(start, end, out copiedHeadIndex, out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted list's length is now the list's length
				m_State->m_Length = numInsertNodes;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(numInsertNodes);

			// Insert the list at the end
			CopyToEnd(start, end, out copiedHeadIndex, out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + numInsertNodes;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of the elements of a given array after the node
		/// referred to by the given enumerator. This doesn't invalidate any
		/// enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the given length and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
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
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			NativeArray<T> array,
			int startIndex,
			int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireValidRangeWithinArray(array, startIndex, length);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(length);

				// Copy to the end
				CopyToEnd(
					array,
					startIndex,
					length,
					out copiedHeadIndex,
					out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(length);

			// Insert the list at the end
			CopyToEnd(
				array,
				startIndex,
				length,
				out copiedHeadIndex,
				out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Insert a range of the elements of a given array after the node
		/// referred to by the given enumerator. This doesn't invalidate any
		/// enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the full given array.
		///
		/// This operation is O(N) where N is the given length and
		/// additional complexity of the allocator's deallocation and allocation
		/// operations when the list doesn't have enough capacity to hold the
		/// inserted nodes.
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to insert after. If invalid for this list,
		/// this function has no effect.
		/// </param>
		/// 
		/// <param name="array">
		/// Array whose elements to insert. It must be readable.
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
		/// 
		/// <returns>
		/// An enumerator to the inserted tail node or the given enumerator if
		/// the given array is empty or an invalid enumerator if the given
		/// enumerator is invalid.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator InsertBefore(
			Enumerator enumerator,
			T[] array,
			int startIndex,
			int length)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);
			RequireValidRangeWithinArray(array, startIndex, length);

			// The enumerator is invalid for this list
			int endIndex = m_State->m_Length;
			int copiedHeadIndex;
			int copiedTailIndex;
			if (!enumerator.IsValidFor(this))
			{
				// The list isn't empty so we don't know where to insert or the
				// array is empty so there's nothing to insert
				if (endIndex > 0 || length == 0)
				{
					return enumerator;
				}

				// Need room for all the nodes in the given array
				EnsureCapacity(length);

				// Copy to the end
				CopyToEnd(
					array,
					startIndex,
					length,
					out copiedHeadIndex,
					out copiedTailIndex);

				// The added nodes are now the head and tail
				m_State->m_HeadIndex = copiedHeadIndex;
				m_State->m_TailIndex = copiedTailIndex;

				// The inserted array's length is now the list's length
				m_State->m_Length = length;

				// The first inserted node
				return new Enumerator(
					this,
					copiedHeadIndex,
					m_State->m_Version);
			}

			// Need room for all the nodes in the given list
			EnsureCapacity(length);

			// Insert the list at the end
			CopyToEnd(
				array,
				startIndex,
				length,
				out copiedHeadIndex,
				out copiedTailIndex);

			// Point the inserted tail node's next to the insert node
			m_State->m_NextIndexes[copiedTailIndex] = enumerator.m_Index;

			// Point the inserted head node's previous to the prev node
			int insertPrevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
			m_State->m_PrevIndexes[copiedHeadIndex] = insertPrevIndex;

			// Point the insert node's prev to the inserted tail node
			m_State->m_PrevIndexes[enumerator.m_Index] = copiedTailIndex;

			// Point the prev node's next to the inserted head node
			if (insertPrevIndex >= 0)
			{
				m_State->m_NextIndexes[insertPrevIndex] = copiedHeadIndex;
			}
			// The insert node was the head, so update the head index to
			// point to the inserted head node where we moved it
			else
			{
				m_State->m_HeadIndex = copiedHeadIndex;
			}

			// Count the newly-added nodes
			m_State->m_Length = endIndex + length;

			// The inserted tail node 
			return new Enumerator(this, copiedTailIndex, m_State->m_Version);
		}

		/// <summary>
		/// Remove a node. This invalidates all enumerators, including the given
		/// enumerator, if the given enumerator is valid. Note that the node's
		/// value is not cleared since it's blittable and therefore can't hold
		/// any managed reference that could be garbage-collected.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="enumerator">
		/// Enumerator to the node to remove. Is invalid for this list, this
		/// function has no effect. <see cref="Enumerator.IsValidFor"/>.
		/// </param>
		///
		/// <returns>
		/// An invalid enumerator if this is the only node in the list or the
		/// given enumerator is invalid, the next node if this is the head of
		/// the list, otherwise the previous node.
		/// </returns>
		[WriteAccessRequired]
		public Enumerator Remove(Enumerator enumerator)
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Can't remove invalid enumerators
			if (!enumerator.IsValidFor(this))
			{
				return enumerator;
			}

			int newLength;
			int retIndex;

			// Node to remove is the only node
			if (m_State->m_Length == 1)
			{
				m_State->m_HeadIndex = -1;
				m_State->m_TailIndex = -1;
				newLength = 0;
				retIndex = -1;
			}
			// There are at least two nodes in the list
			else
			{
				// Node to remove is the head
				if (enumerator.m_Index == m_State->m_HeadIndex)
				{
					// Move the head pointer forward one
					int nextIndex = m_State->m_NextIndexes[enumerator.m_Index];
					m_State->m_HeadIndex = nextIndex;

					// Make the new head's previous node be invalid
					m_State->m_PrevIndexes[nextIndex] = -1;

					// Return an enumerator to the new head
					retIndex = nextIndex;
				}
				// Node to remove is the tail
				else if (enumerator.m_Index == m_State->m_TailIndex)
				{
					// Move the tail pointer back one
					int prevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
					m_State->m_TailIndex = prevIndex;

					// Make the new tail's next node be invalid
					m_State->m_NextIndexes[prevIndex] = -1;

					// Return an enumerator to the new tail
					retIndex = prevIndex;
				}
				// Node to remove is an interior node.
				else
				{
					// Link the previous node to the next node and the next node
					// to the previous node
					int prevIndex = m_State->m_PrevIndexes[enumerator.m_Index];
					int nextIndex = m_State->m_NextIndexes[enumerator.m_Index];
					m_State->m_NextIndexes[prevIndex] = nextIndex;
					m_State->m_PrevIndexes[nextIndex] = prevIndex;

					// Return an enumerator to the previous node
					retIndex = prevIndex;
				}

				// Move the last node to where the node was removed from
				int lastIndex = m_State->m_Length - 1;
				if (enumerator.m_Index != lastIndex)
				{
					// Copy the last node to where the removed node was
					int lastNextIndex = m_State->m_NextIndexes[lastIndex];
					int lastPrevIndex = m_State->m_PrevIndexes[lastIndex];
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						enumerator.m_Index,
						UnsafeUtility.ReadArrayElement<T>(
							m_State->m_Values,
							lastIndex));
					m_State->m_NextIndexes[enumerator.m_Index] = lastNextIndex;
					m_State->m_PrevIndexes[enumerator.m_Index] = lastPrevIndex;

					// If the last node wasn't the tail, set its next node's
					// previous index to where the last node was moved to
					if (lastNextIndex >= 0)
					{
						m_State->m_PrevIndexes[lastNextIndex] = enumerator.m_Index;
					}

					// If the last node wasn't the head, set its previous node's
					// next index to where the last node was moved to
					if (lastPrevIndex >= 0)
					{
						m_State->m_NextIndexes[lastPrevIndex] = enumerator.m_Index;
					}

					// If the last node was the head, update the head index
					if (lastIndex == m_State->m_HeadIndex)
					{
						m_State->m_HeadIndex = enumerator.m_Index;
					}

					// If the last node was the tail, update the tail index
					if (lastIndex == m_State->m_TailIndex)
					{
						m_State->m_TailIndex = enumerator.m_Index;
					}

					// If the last node was the return, update the return
					if (lastIndex == retIndex)
					{
						retIndex = enumerator.m_Index;
					}
				}

				// Account for the removed node
				newLength = lastIndex;
			}

			// Set the new length
			m_State->m_Length = newLength;

			// Invalidate all enumerators
			m_State->m_Version++;

			// Return the appropriate enumerator
			return new Enumerator(this, retIndex, m_State->m_Version);
		}

		/// <summary>
		/// Remove all nodes. This invalidates all enumerators. Note that the
		/// nodes' values are not cleared since they're blittable and therefore
		/// can't hold any managed reference that could be garbage-collected.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(1).
		/// </summary>
		[WriteAccessRequired]
		public void Clear()
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Forget the list
			m_State->m_HeadIndex = -1;
			m_State->m_TailIndex = -1;
			m_State->m_Length = 0;
			m_State->m_Version++;
		}

		/// <summary>
		/// Swap the values of two nodes in the list. This does not invalidate
		/// any enumerators. This swaps the node values without changing any
		/// next and previous pointers. For example, if the list was
		/// like this:
		///   Node 0: { Value = A, Next = 1, Prev = -1 }
		///   Node 1: { Value = B, Next = 2, Prev = 0 }
		///   Node 2: { Value = C, Next = -1, Prev = 1 }
		///   Head: 0
		///   Tail: 2
		/// Then swapping A and C would result in a list like this:
		///   Node 0: { Value = C, Next = 1, Prev = -1 }
		///   Node 1: { Value = B, Next = 2, Prev = 0 }
		///   Node 2: { Value = A, Next = -1, Prev = 1 }
		///   Head: 0
		///   Tail: 2
		/// Not a list like this:
		///   Node 0: { Value = A, Next = -1, Prev = 1 }
		///   Node 1: { Value = B, Next = 0, Prev = 2 }
		///   Node 2: { Value = C, Next = 1, Prev = -1 }
		///   Head: 2
		///   Tail: 0
		/// This means that iterating the list using the indexer results in the
		/// expected sequence where node values are swapped:
		///   Index 0: C
		///   Index 1: B
		///   Index 2: A
		/// Rather than the sequence not being swapped:
		///   Index 0: A
		///   Index 1: B
		///   Index 2: C
		/// So there's no need to call
		/// <see cref="SortNodeMemoryAddresses"/> just because this function was
		/// called in order to use the indexer.
		/// 
		/// This operation requires read and write access to the list plus read
		/// and write access to the nodes the enumerators refer to if they are
		/// both valid for this list and don't refer to the same node.
		///
		/// This operation is O(1).
		/// </summary>
		/// 
		/// <param name="a">
		/// Enumerator to the first node to swap. If invalid or for another
		/// list, this function has no effect.
		/// </param>
		/// 
		/// <param name="b">
		/// Enumerator to the second node to swap. If invalid or for another
		/// list, this function has no effect.
		/// </param>
		[WriteAccessRequired]
		public void Swap(Enumerator a, Enumerator b)
		{
			RequireReadAccess();
			RequireWriteAccess();

			// Both enumerators must be valid for this list or we can't swap
			// If the enumerators refer to the same node, don't swap because
			// swapping will have no effect.
			if (a.IsValidFor(this)
				&& b.IsValidFor(this)
				&& a.m_Index != b.m_Index)
			{
				RequireParallelForAccess(a.m_Index);
				RequireParallelForAccess(b.m_Index);

				// Read A to temp
				T temp = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					a.m_Index);

				// Write B to A
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					a.m_Index,
					UnsafeUtility.ReadArrayElement<T>(
						m_State->m_Values,
						b.m_Index));

				// Write temp (A's old value) to B
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					b.m_Index,
					temp);
			}
		}

		/// <summary>
		/// Reorder the list such that its order is preserved but the nodes are
		/// laid out sequentially in memory. This allows for indexing into the
		/// list after a call to <see cref="Remove"/>. This invalidates all
		/// enumerators.
		/// 
		/// This operation requires read and write access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(N) where N is the length of the list.
		/// </summary>
		[WriteAccessRequired]
		public void SortNodeMemoryAddresses()
		{
			RequireReadAccess();
			RequireWriteAccess();
			RequireFullListSafetyCheckBounds();

			// Swap the value for the head with the value for the first element,
			// then the the node after the head that to the second element,
			// until the tail is reached
			for (int curIndex = m_State->m_HeadIndex, startIndex = 0;
				curIndex >= 0;
				curIndex = m_State->m_NextIndexes[curIndex], startIndex++)
			{
				// Never swap backwards. The part of the array up to startIndex
				// is already in order.
				if (curIndex > startIndex)
				{
					T startValue = UnsafeUtility.ReadArrayElement<T>(
						m_State->m_Values,
						startIndex);
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						startIndex,
						UnsafeUtility.ReadArrayElement<T>(
							m_State->m_Values,
							curIndex));
					UnsafeUtility.WriteArrayElement(
						m_State->m_Values,
						curIndex,
						startValue);
				}
			}

			int endIndex = m_State->m_Length - 1;

			// Set all the next pointers to point to the next index now that
			// the values are sequential. The last one points to null.
			for (int i = 0; i <= endIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[endIndex] = -1;

			// Set all the prev pointers to point to the prev index now that
			// the values are sequential
			for (int i = 0; i <= endIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}

			// The head is now at the beginning and the tail is now at the end
			m_State->m_HeadIndex = 0;
			m_State->m_TailIndex = endIndex;

			// Invalidate all enumerators
			m_State->m_Version++;
		}

		/// <summary>
		/// Copy nodes to a managed array, which is optionally allocated.
		/// 
		/// This operation requires read access to the list and read access to
		/// the nodes to copy.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If null or less than the calculated length
		/// (see notes on that parameter) then a new array with the given length
		/// will be allocated.
		/// </param>
		/// 
		/// <param name="srcEnumerator">
		/// Enumerator to the first node to copy. If invalid or for another
		/// list, the head is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified node values copied into it.
		/// </returns>
		public T[] ToArray(
			T[] array = null,
			Enumerator srcEnumerator = default(Enumerator),
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
			if (!srcEnumerator.IsValidFor(this))
			{
				srcEnumerator.m_Index = m_State->m_HeadIndex;
			}

			// If the given array is null or can't hold all the nodes, allocate
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

			// Copy the nodes' values to the array
			while (length > 0)
			{
				// Copy the node's value
				RequireParallelForAccess(srcEnumerator.m_Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcEnumerator.m_Index);

				// Go to the next node
				srcEnumerator.m_Index = m_State->m_NextIndexes[srcEnumerator.m_Index];

				// Count the copy
				destIndex++;
				length--;
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes to the start of a managed array.
		/// 
		/// This operation requires read access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(N) where N is the length of the list.
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. Must not be null and must be at least as
		/// long as the list.
		/// </param>
		public void ToArrayFull(T[] array)
		{
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);

			// Traverse the list copying the nodes' values to the array
			for (int srcIndex = m_State->m_HeadIndex, destIndex = 0;
				srcIndex >= 0;
				srcIndex = m_State->m_NextIndexes[srcIndex], destIndex++)
			{
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcIndex);
			}
		}

		/// <summary>
		/// Copy nodes to a managed array in the reverse order in which they
		/// are stored in this list, which is optionally allocated.
		/// 
		/// This operation requires read access to the list and read access to
		/// the nodes to copy.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If null or less than the calculated length
		/// (see notes on that parameter) then a new array with the given length
		/// will be allocated.
		/// </param>
		/// 
		/// <param name="srcEnumerator">
		/// Enumerator to the first node to copy. If invalid or for another
		/// list, the tail is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified node values copied into it.
		/// </returns>
		public T[] ToArrayReverse(
			T[] array = null,
			Enumerator srcEnumerator = default(Enumerator),
			int destIndex = 0,
			int length = -1)
		{
			RequireReadAccess();

			// If length is invalid, copy the whole list
			if (length < 0 || length > m_State->m_Length)
			{
				length = m_State->m_Length;
			}

			// If the enumerator is invalid for this list, start at the tail
			if (!srcEnumerator.IsValidFor(this))
			{
				srcEnumerator.m_Index = m_State->m_TailIndex;
			}

			// If the given array is null or can't hold all the nodes, allocate
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

			// Copy the nodes' values to the array
			while (length > 0)
			{
				// Copy the node's value
				RequireParallelForAccess(srcEnumerator.m_Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcEnumerator.m_Index);

				// Go to the previous node
				srcEnumerator.m_Index = m_State->m_PrevIndexes[srcEnumerator.m_Index];

				// Count the copy
				destIndex++;
				length--;
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes in the reverse order in which they
		/// are stored in this list to the start of a managed array.
		/// 
		/// This operation requires read access to the full list and
		/// is therefore not suitable for use from a ParallelFor job.
		///
		/// This operation is O(N) where N is the length of the list.
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. Must not be null and must be at least as
		/// long as the list.
		/// </param>
		public void ToArrayFullReverse(T[] array)
		{
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();
			RequireNonNullManagedArray(array);

			// Traverse the list copying the nodes' values to the array
			for (int srcIndex = m_State->m_TailIndex, destIndex = 0;
				srcIndex >= 0;
				srcIndex = m_State->m_PrevIndexes[srcIndex], destIndex++)
			{
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcIndex);
			}
		}

		/// <summary>
		/// Copy nodes to a <see cref="NativeArray{T}"/>.
		/// 
		/// This operation requires read access to the list and read access to
		/// the nodes to copy. It also requires read access to the elements of
		/// the given array to write.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If <see cref="NativeArray{T}.IsCreated"/>
		/// returns false for it or its <see cref="Length"/> is less than the
		/// calculated length (see notes on that parameter) then a new array
		/// with the given length will be allocated with the same allocator
		/// that this list uses.
		/// </param>
		/// 
		/// <param name="srcEnumerator">
		/// Enumerator to the first node to copy. If invalid or for another
		/// list, the head is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified node values copied into it.
		/// </returns>
		public NativeArray<T> ToNativeArray(
			NativeArray<T> array = default(NativeArray<T>),
			Enumerator srcEnumerator = default(Enumerator),
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
			if (!srcEnumerator.IsValidFor(this))
			{
				srcEnumerator.m_Index = m_State->m_HeadIndex;
			}

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
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

			// Copy the nodes' values to the array
			while (length > 0)
			{
				// Copy the node's value
				RequireParallelForAccess(srcEnumerator.m_Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcEnumerator.m_Index);

				// Go to the next node
				srcEnumerator.m_Index = m_State->m_NextIndexes[srcEnumerator.m_Index];

				// Count the copy
				destIndex++;
				length--;
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes in this list to the start of a
		/// <see cref="NativeArray{T}"/>.
		/// 
		/// This operation requires read access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires write access to the first <see cref="Length"/> elements of
		/// the given array.
		///
		/// This operation is O(N).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. It must be writable and it must be at least
		/// as long as the list.
		/// </param>
		public void ToNativeArrayFull(NativeArray<T> array)
		{
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();

			// Traverse the list copying the nodes' values to the array
			for (int srcIndex = m_State->m_HeadIndex, destIndex = 0;
				srcIndex >= 0;
				srcIndex = m_State->m_NextIndexes[srcIndex], destIndex++)
			{
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcIndex);
			}
		}

		/// <summary>
		/// Copy nodes to a <see cref="NativeArray{T}"/> in the reverse order
		/// in which they are stored in the list.
		/// 
		/// This operation requires read access to the list and read access to
		/// the nodes to copy. It also requires write access to the elements of
		/// the given array to copy to.
		///
		/// This operation is O(N) where N is the calculated length (see notes
		/// on that parameter).
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. If <see cref="NativeArray{T}.IsCreated"/>
		/// returns false for it or its <see cref="Length"/> is less than the
		/// calculated length (see notes on that parameter) then a new array
		/// with the given length will be allocated with the same allocator
		/// that this list uses.
		/// </param>
		/// 
		/// <param name="srcEnumerator">
		/// Enumerator to the first node to copy. If invalid or for another
		/// list, the tail is used.
		/// </param>
		/// 
		/// <param name="destIndex">
		/// Index to start copying into. If negative or greater than or equal to
		/// the array (created or passed) length minus the calculated length
		/// (see notes on that parameter), zero is used.
		/// </param>
		/// 
		/// <param name="length">
		/// Number of nodes to copy. If negative or greater than the list's
		/// <see cref="Length"/>, the list's <see cref="Length"/> is used.
		/// </param>
		/// 
		/// <returns>
		/// Either the given non-null array or a newly-allocated array with the
		/// specified node values copied into it.
		/// </returns>
		public NativeArray<T> ToNativeArrayReverse(
			NativeArray<T> array = default(NativeArray<T>),
			Enumerator srcEnumerator = default(Enumerator),
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
			if (!srcEnumerator.IsValidFor(this))
			{
				srcEnumerator.m_Index = m_State->m_TailIndex;
			}

			// If the given array is null or can't hold all the nodes, allocate
			// a new one
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

			// Copy the nodes' values to the array
			while (length > 0)
			{
				// Copy the node's value
				RequireParallelForAccess(srcEnumerator.m_Index);
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcEnumerator.m_Index);

				// Go to the previous node
				srcEnumerator.m_Index = m_State->m_PrevIndexes[srcEnumerator.m_Index];

				// Count the copy
				destIndex++;
				length--;
			}

			return array;
		}

		/// <summary>
		/// Copy all nodes in the reverse order in which they
		/// are stored in this list to the start of a
		/// <see cref="NativeArray{T}"/>.
		/// 
		/// This operation requires read access to the full list and
		/// is therefore not suitable for use from a ParallelFor job. It also
		/// requires read access to the elements of the given array to write.
		///
		/// This operation is O(N) where N is the length of the list.
		/// </summary>
		///
		/// <param name="array">
		/// Array to copy nodes to. It must be writable and  and it must be at
		/// least as long as the list.
		/// </param>
		public void ToNativeArrayFullReverse(NativeArray<T> array)
		{
			RequireReadAccess();
			RequireFullListSafetyCheckBounds();

			// Traverse the list copying the nodes' values to the array
			for (int srcIndex = m_State->m_TailIndex, destIndex = 0;
				srcIndex >= 0;
				srcIndex = m_State->m_PrevIndexes[srcIndex], destIndex++)
			{
				array[destIndex] = UnsafeUtility.ReadArrayElement<T>(
					m_State->m_Values,
					srcIndex);
			}
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

			// Free the state's contents
			UnsafeUtility.Free(
				m_State->m_Values,
				m_State->m_Allocator);
			m_State->m_Values = null;
			UnsafeUtility.Free(
				m_State->m_NextIndexes,
				m_State->m_Allocator);
			m_State->m_NextIndexes = null;
			UnsafeUtility.Free(
				m_State->m_PrevIndexes,
				m_State->m_Allocator);
			m_State->m_PrevIndexes = null;

			// Forget the list
			m_State->m_HeadIndex = -1;
			m_State->m_TailIndex = -1;
			m_State->m_Length = 0;
			m_State->m_Capacity = 0;

			// Invalidate all enumerators
			m_State->m_Version++;

			// Free the state itself
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
		/// Copy all the nodes of a list to the end of the arrays. The list must
		/// already have sufficient capacity to hold all the nodes of the list
		/// to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next list->m_State->m_Length -1
		/// nodes. Read access to the full given array is also required.
		/// </summary>
		/// 
		/// <param name="list">
		/// List to copy. Must not be empty.
		/// </param>
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			NativeLinkedList<T> list,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Copy the list's node values at the end. Copying with stride is
			// the same way NativeSlice<T> copies.
			int endIndex = m_State->m_Length;
			int sizeofT = UnsafeUtility.SizeOf<T>();
			UnsafeUtility.MemCpyStride(
				(byte*)m_State->m_Values + sizeofT * endIndex,
				sizeofT,
				list.m_State->m_Values,
				sizeofT,
				sizeofT,
				list.m_State->m_Length);

			// Copy the list's next and prev pointers at the end, offset for
			// the new location at the end of this list.
			for (int i = 0; i < list.m_State->m_Length; ++i)
			{
				m_State->m_NextIndexes[endIndex + i]
					= list.m_State->m_NextIndexes[i] + endIndex;
			}
			for (int i = 0; i < list.m_State->m_Length; ++i)
			{
				m_State->m_PrevIndexes[endIndex + i]
					= list.m_State->m_PrevIndexes[i] + endIndex;
			}

			// Re-set the list's head and tail pointers since we offset them
			copiedHeadIndex = endIndex + list.m_State->m_HeadIndex;
			copiedTailIndex = endIndex + list.m_State->m_TailIndex;
			m_State->m_NextIndexes[copiedTailIndex] = -1;
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
		}

		/// <summary>
		/// Copy a range of nodes of a list to the end of the arrays. The list
		/// must already have sufficient capacity to hold all the nodes to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next nodes to copy. Read access to
		/// the full given array is also required.
		/// </summary>
		/// 
		/// <param name="start">
		/// Enumerator to the first node to copy
		/// </param>
		/// 
		/// <param name="end">
		/// Enumerator to the last node to copy. Must be towards the tail
		/// relative to the given start enumerator.
		/// </param>
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the given start node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the given end node was copied to
		/// </param>
		private void CopyToEnd(
			Enumerator start,
			Enumerator end,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Copy the list's node values at the end
			int endIndex = m_State->m_Length;
			int srcIndex = start.m_Index;
			int destIndex = endIndex;
			do
			{
				// Copy the node value
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					destIndex,
					UnsafeUtility.ReadArrayElement<T>(
						start.m_List.m_State->m_Values,
						srcIndex));

				// Stop if this was the last node to copy
				if (srcIndex == end.m_Index)
				{
					break;
				}

				// Move to the next node to copy
				srcIndex = start.m_List.m_State->m_NextIndexes[srcIndex];
				destIndex++;
			}
			while (true);

			// "Return" the copied head and tail indices
			copiedHeadIndex = endIndex;
			copiedTailIndex = destIndex;

			// Initialize next indices to point to the next node
			for (int i = copiedHeadIndex; i < copiedTailIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[copiedTailIndex] = -1;

			// Initialize prev indices to point to the previous node
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
			for (int i = copiedHeadIndex + 1; i <= copiedTailIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}
		}

		/// <summary>
		/// Copy all the nodes of an array to the end of the arrays. The list
		/// must already have sufficient capacity to hold all the nodes of the
		/// array to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next list->m_State->m_Length -1
		/// nodes. Read access to the full given array is also required.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to copy. Must not be empty.
		/// </param>
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			NativeArray<T> array,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Compute the indices of the head and tail of the copied portion of
			// the list
			int endIndex = m_State->m_Length;
			copiedHeadIndex = endIndex;
			copiedTailIndex = endIndex + array.Length - 1;

			// Copy the array's elements to the end. Copying with stride is
			// the same way NativeSlice<T> copies.
			int sizeofT = UnsafeUtility.SizeOf<T>();
			UnsafeUtility.MemCpyStride(
				(byte*)m_State->m_Values + sizeofT * endIndex,
				sizeofT,
				array.GetUnsafeReadOnlyPtr(),
				sizeofT,
				sizeofT,
				array.Length);

			// Initialize next pointers to the next index and the last next
			// pointer to an invalid index
			for (int i = copiedHeadIndex; i <= copiedTailIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[copiedTailIndex] = -1;

			// Initialize prev pointers to the previous index and the first
			// prev pointer to an invalid index
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
			for (int i = copiedHeadIndex + 1; i <= copiedTailIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}
		}

		/// <summary>
		/// Copy all the nodes of an array to the end of the arrays. The list
		/// must already have sufficient capacity to hold all the nodes of the
		/// array to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next list->m_State->m_Length -1
		/// nodes. Read access to the full given array is also required.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to copy. Must not be empty.
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
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			NativeArray<T> array,
			int startIndex,
			int length,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Compute the indices of the head and tail of the copied portion of
			// the list
			int endIndex = m_State->m_Length;
			copiedHeadIndex = endIndex;
			copiedTailIndex = endIndex + length - 1;

			// Copy the array's elements to the end. Copying with stride is
			// the same way NativeSlice<T> copies.
			int sizeofT = UnsafeUtility.SizeOf<T>();
			UnsafeUtility.MemCpyStride(
				(byte*)m_State->m_Values + sizeofT * endIndex,
				sizeofT,
				(byte*)array.GetUnsafeReadOnlyPtr() + sizeofT * startIndex,
				sizeofT,
				sizeofT,
				length);

			// Initialize next pointers to the next index and the last next
			// pointer to an invalid index
			for (int i = copiedHeadIndex; i <= copiedTailIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[copiedTailIndex] = -1;

			// Initialize prev pointers to the previous index and the first
			// prev pointer to an invalid index
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
			for (int i = copiedHeadIndex + 1; i <= copiedTailIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}
		}

		/// <summary>
		/// Copy all the nodes of an array to the end of the arrays. The list
		/// must already have sufficient capacity to hold all the nodes of the
		/// array to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next list->m_State->m_Length -1
		/// nodes. Read access to the full given array is also required.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to copy. Must not be empty.
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
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			T[] array,
			int startIndex,
			int length,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Compute the indices of the head and tail of the copied portion of
			// the list
			int endIndex = m_State->m_Length;
			copiedHeadIndex = endIndex;
			copiedTailIndex = endIndex + length - 1;

			// Copy the array's elements to the end
			for (int destIndex = copiedHeadIndex, srcIndex = startIndex;
				 destIndex <= copiedTailIndex;
				 ++destIndex, ++srcIndex)
			{
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					destIndex,
					array[srcIndex]);
			}

			// Initialize next pointers to the next index and the last next
			// pointer to an invalid index
			for (int i = copiedHeadIndex; i <= copiedTailIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[copiedTailIndex] = -1;

			// Initialize prev pointers to the previous index and the first
			// prev pointer to an invalid index
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
			for (int i = copiedHeadIndex + 1; i <= copiedTailIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}
		}

		/// <summary>
		/// Copy all the nodes of a list to the end of the arrays. The list must
		/// already have sufficient capacity to hold all the nodes of the list
		/// to copy.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the portion of the list starting at
		/// m_State->m_Length as well as the next list->m_State->m_Length -1
		/// nodes.
		/// </summary>
		/// 
		/// <param name="array">
		/// Array to copy. Must not be empty.
		/// </param>
		/// 
		/// <param name="copiedHeadIndex">
		/// Index that the list's head node was copied to
		/// </param>
		/// 
		/// <param name="copiedTailIndex">
		/// Index that the list's tail node was copied to
		/// </param>
		private void CopyToEnd(
			T[] array,
			out int copiedHeadIndex,
			out int copiedTailIndex)
		{
			// Compute the indices of the head and tail of the copied portion of
			// the list
			int endIndex = m_State->m_Length;
			copiedHeadIndex = endIndex;
			copiedTailIndex = endIndex + array.Length - 1;

			// Copy the array's elements to the end
			for (int destIndex = copiedHeadIndex, srcIndex = 0;
				 destIndex <= copiedTailIndex;
				 ++destIndex, ++srcIndex)
			{
				UnsafeUtility.WriteArrayElement(
					m_State->m_Values,
					destIndex,
					array[srcIndex]);
			}

			// Initialize next pointers to the next index and the last next
			// pointer to an invalid index
			for (int i = copiedHeadIndex; i <= copiedTailIndex; ++i)
			{
				m_State->m_NextIndexes[i] = i + 1;
			}
			m_State->m_NextIndexes[copiedTailIndex] = -1;

			// Initialize prev pointers to the previous index and the first
			// prev pointer to an invalid index
			m_State->m_PrevIndexes[copiedHeadIndex] = -1;
			for (int i = copiedHeadIndex + 1; i <= copiedTailIndex; ++i)
			{
				m_State->m_PrevIndexes[i] = i - 1;
			}
		}

		/// <summary>
		/// Ensure that the capacity of the list is sufficient to store a given
		/// number of new nodes.
		/// 
		/// This operation requires read and write access to the list and read
		/// and write access to the full list if there is insufficient capacity.
		/// </summary>
		/// 
		/// <param name="numNewNodes">
		/// Number of new nodes that must be stored.
		/// </param>
		private void EnsureCapacity(int numNewNodes)
		{
			if (m_State->m_Length + numNewNodes > m_State->m_Capacity)
			{
				// The new capacity must be at least double to avoid excessive
				// and expensive capacity increase operations.
				int newCapacity = Math.Max(
					m_State->m_Length * 2,
					m_State->m_Length + numNewNodes);

				// Resize values
				int sizeofT = UnsafeUtility.SizeOf<T>();
				void* newvalues = UnsafeUtility.Malloc(
					newCapacity * sizeofT,
					UnsafeUtility.AlignOf<T>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpyStride(
					newvalues,
					sizeofT,
					m_State->m_Values,
					sizeofT,
					sizeofT,
					m_State->m_Length);
				UnsafeUtility.Free(
					m_State->m_Values,
					m_State->m_Allocator);
				m_State->m_Values = newvalues;

				// Resize nextIndexes
				int* newNextIndexes = (int*)UnsafeUtility.Malloc(
					newCapacity * sizeof(int),
					UnsafeUtility.AlignOf<int>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpy(
					newNextIndexes,
					m_State->m_NextIndexes,
					m_State->m_Length * sizeof(int));
				UnsafeUtility.Free(
					m_State->m_NextIndexes,
					m_State->m_Allocator);
				m_State->m_NextIndexes = newNextIndexes;

				// Resize prevIndexes
				int* newPrevIndexes = (int*)UnsafeUtility.Malloc(
					newCapacity * sizeof(int),
					UnsafeUtility.AlignOf<int>(),
					m_State->m_Allocator);
				UnsafeUtility.MemCpy(
					newPrevIndexes,
					m_State->m_PrevIndexes,
					m_State->m_Length * sizeof(int));
				UnsafeUtility.Free(
					m_State->m_PrevIndexes,
					m_State->m_Allocator);
				m_State->m_PrevIndexes = newPrevIndexes;

				m_State->m_Capacity = newCapacity;
			}
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
					"Type used in NativeLinkedList<{0}> must be blittable");
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
						"Index is out of restricted ParallelFor range in " +
						"ReadWriteBuffer. ReadWriteBuffers are restricted to " +
						"only read and write the node at the job index. You " +
						"can  use double buffering strategies to avoid race " +
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
	}
	
	/// <summary>
	/// Provides a debugger view of <see cref="NativeLinkedList{T}"/>.
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of nodes in the list
	/// </typeparam>
	internal sealed class NativeLinkedListDebugView<T>
#if CSHARP_7_3_OR_NEWER
		where T : unmanaged
#else
		where T : struct
#endif
	{
		/// <summary>
		/// List to view
		/// </summary>
		private NativeLinkedList<T> m_List;
	
		/// <summary>
		/// Create the view for a given list
		/// </summary>
		/// 
		/// <param name="list">
		/// List to view
		/// </param>
		public NativeLinkedListDebugView(NativeLinkedList<T> list)
		{
			m_List = list;
		}
	
		/// <summary>
		/// Get a managed array version of the list's nodes to be viewed in the
		/// debugger.
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