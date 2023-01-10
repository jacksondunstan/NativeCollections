//-----------------------------------------------------------------------
// <copyright file="IJobParallelForRanged.cs" company="Jackson Dunstan">
//     Copyright (c) Jackson Dunstan. See LICENSE.md.
// </copyright>
//-----------------------------------------------------------------------

using System;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace JacksonDunstan.NativeCollections
{
	/// <summary>
	/// A ParallelFor job type that executes on ranges of indices
	/// </summary>
	[JobProducerType(typeof(IJobParallelForRangedExtensions.ParallelForJobStruct<>))]
	public interface IJobParallelForRanged
	{
		/// <summary>
		/// Execute on the given range of indices, inclusive of the start and
		/// exclusive of the end
		/// </summary>
		/// 
		/// <param name="startIndex">
		/// First index to execute on
		/// </param>
		/// 
		/// <param name="endIndex">
		/// One greater than the last index to execute on
		/// </param>
		void Execute(int startIndex, int endIndex);
	}

	/// <summary>
	/// Supporting functionality for <see cref="IJobParallelForRanged"/>
	/// </summary>
	public static class IJobParallelForRangedExtensions
	{
		/// <summary>
		/// Supporting functionality for <see cref="IJobParallelForRanged"/>
		/// </summary>
		internal struct ParallelForJobStruct<TJob>
			where TJob : struct, IJobParallelForRanged
		{
			/// <summary>
			/// Cached job type reflection data
			/// </summary>
			public static IntPtr jobReflectionData;

			/// <summary>
			/// Initialize the job type
			/// </summary>
			/// 
			/// <returns>
			/// Reflection data for the job type
			/// </returns>
			public static IntPtr Initialize()
			{
				if (jobReflectionData == IntPtr.Zero)
				{
					jobReflectionData = JobsUtility.CreateJobReflectionData(
						typeof(TJob),
#if UNITY_2020_2_OR_NEWER
						// Parameter removed in 2020.2
#else
						JobType.ParallelFor,
#endif
						(ExecuteJobFunction)Execute);
				}
				return jobReflectionData;
			}

			/// <summary>
			/// Delegate type for <see cref="Execute"/>
			/// </summary>
			public delegate void ExecuteJobFunction(
				ref TJob data,
				IntPtr additionalPtr,
				IntPtr bufferRangePatchData,
				ref JobRanges ranges,
				int jobIndex);

			/// <summary>
			/// Execute the job until there are no more work stealing ranges
			/// available to execute
			/// </summary>
			/// 
			/// <param name="jobData">
			/// The job to execute
			/// </param>
			/// 
			/// <param name="additionalPtr">
			/// TBD. Unused.
			/// </param>
			/// 
			/// <param name="bufferRangePatchData">
			/// TBD. Unused.
			/// </param>
			/// 
			/// <param name="ranges">
			/// Work stealing ranges to execute from
			/// </param>
			/// 
			/// <param name="jobIndex">
			/// Index of this job
			/// </param>
			public static unsafe void Execute(
				ref TJob jobData,
				IntPtr additionalPtr,
				IntPtr bufferRangePatchData,
				ref JobRanges ranges,
				int jobIndex)
			{
				int startIndex;
				int endIndex;
				while (JobsUtility.GetWorkStealingRange(
					ref ranges,
					jobIndex,
					out startIndex,
					out endIndex))
				{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
					JobsUtility.PatchBufferMinMaxRanges(
						bufferRangePatchData,
						UnsafeUtility.AddressOf(ref jobData),
						startIndex,
						endIndex - startIndex);
#endif
					jobData.Execute(startIndex, endIndex);
				}
			}
		}

		/// <summary>
		/// Run a job asynchronously
		/// </summary>
		/// 
		/// <param name="jobData">
		/// Job to run
		/// </param>
		/// 
		/// <param name="valuesLength">
		/// Length of the values to execute on.
		/// </param>
		///
		/// <param name="innerloopBatchCount">
		/// Number of job executions per batch
		/// </param>
		///
		/// <param name="dependsOn">
		/// Handle of the job that must be run before this job
		/// </param>
		/// 
		/// <returns>
		/// A handle to the created job
		/// </returns>
		/// 
		/// <typeparam name="T">
		/// Type of job to run
		/// </typeparam>
		unsafe public static JobHandle ScheduleRanged<T>(
			this T jobData,
			int valuesLength,
			int innerloopBatchCount,
			JobHandle dependsOn = new JobHandle())
			where T : struct, IJobParallelForRanged
		{
			var scheduleParams = new JobsUtility.JobScheduleParameters(
				UnsafeUtility.AddressOf(ref jobData),
				ParallelForJobStruct<T>.Initialize(),
				dependsOn,
#if UNITY_2020_2_OR_NEWER
				// Parameter renamed in 2020.2
				ScheduleMode.Parallel
#else
				ScheduleMode.Batched
#endif
			);
			return JobsUtility.ScheduleParallelFor(
				ref scheduleParams,
				valuesLength,
				innerloopBatchCount);
		}

		/// <summary>
		/// Run a job synchronously
		/// </summary>
		/// 
		/// <param name="jobData">
		/// Job to run
		/// </param>
		/// 
		/// <param name="valuesLength">
		/// Length of the values to execute on.
		/// </param>
		/// 
		/// <typeparam name="T">
		/// Type of job to run
		/// </typeparam>
		public static unsafe void RunRanged<T>(
			this T jobData,
			int valuesLength)
			where T : struct, IJobParallelForRanged
		{
			var scheduleParams = new JobsUtility.JobScheduleParameters(
				UnsafeUtility.AddressOf(ref jobData),
				ParallelForJobStruct<T>.Initialize(),
				new JobHandle(),
				ScheduleMode.Run);
			JobsUtility.ScheduleParallelFor(
				ref scheduleParams,
				valuesLength,
				valuesLength);
		}
	}
}
