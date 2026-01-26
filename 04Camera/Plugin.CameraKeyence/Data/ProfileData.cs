//----------------------------------------------------------------------------- 
// <copyright file="ProfileData.cs" company="KEYENCE">
//	 Copyright (c) 2019 KEYENCE CORPORATION. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------- 

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace Device.Keyence3DCameraDevice
{
	/// <summary>
	/// Thread-safe class for array storage
	/// </summary>
	public static class ThreadSafeBuffer
	{
		#region Constant
		private const int BatchFinalizeFlagBitCount = 16;
		#endregion

		#region Field
		/// <summary>Data buffer</summary>
		private static List<int[]>[] _buffer = new List<int[]>[NativeMethods.DeviceCount];
		/// <summary>Buffer for the amount of data</summary>
		private static uint[] _count = new uint[NativeMethods.DeviceCount];
		/// <summary>Object for exclusive control</summary>
		private static object[] _syncObject = new object[NativeMethods.DeviceCount];
		/// <summary>Callback function notification parameter</summary>
		private static uint[] _notify = new uint[NativeMethods.DeviceCount];
		/// <summary>Batch number</summary>
		private static int[] _batchNo = new int[NativeMethods.DeviceCount];
		#endregion

		#region Constructor

		/// <summary>
		/// Constructor
		/// </summary>
		static ThreadSafeBuffer()
		{
			for (int i = 0; i < NativeMethods.DeviceCount; i++)
			{
				_buffer[i] = new List<int[]>();
				_syncObject[i] = new object();
			}
		}
		#endregion

		#region Method
		/// <summary>
		/// Get buffer data count
		/// </summary>
		/// <returns>buffer data count</returns>
		public static int GetBufferDataCount(int index)
		{
			return _buffer[index].Count;
		}

		/// <summary>
		/// Element addition
		/// </summary>
		/// <param name="index">User information set when high-speed communication was initialized</param>
		/// <param name="value">Additional element</param>
		/// <param name="notify">Parameter for notification</param>
		public static void Add(int index, List<int[]> value, uint notify)
		{
			lock (_syncObject[index])
			{
				_buffer[index].AddRange(value);
				_count[index] += (uint)value.Count;
				_notify[index] |= notify;
				// Add the batch number if the batch has been finalized.
				if ((uint)(notify & (0x1 << BatchFinalizeFlagBitCount)) != 0) _batchNo[index]++;
			}
		}

		/// <summary>
		/// Clear elements.
		/// </summary>
		/// <param name="index">Device ID</param>
		public static void Clear(int index)
		{
			lock (_syncObject[index])
			{
				_buffer[index].Clear();
			}
		}

		/// <summary>
		/// Clear the buffer.
		/// </summary>
		/// <param name="index">Device ID</param>
		public static void ClearBuffer(int index)
		{
			Clear(index);
			ClearCount(index);
			_batchNo[index] = 0;
			ClearNotify(index);
		}

		/// <summary>
		/// Get element.
		/// </summary>
		/// <param name="index">Device ID</param>
		/// <param name="notify">Parameter for notification</param>
		/// <param name="batchNo">Batch number</param>
		/// <returns>Element</returns>
		public static List<int[]> Get(int index, out uint notify, out int batchNo)
		{
			List<int[]> value = new List<int[]>();
			lock (_syncObject[index])
			{
				value.AddRange(_buffer[index]);
				_buffer[index].Clear();
				notify = _notify[index];
				_notify[index] = 0;
				batchNo = _batchNo[index];
			}
			return value;
		}

		/// <summary>
		/// Add the count
		/// </summary>
		/// <param name="index">Device ID</param>
		/// <param name="count">Count</param>
		/// <param name="notify">Parameter for notification</param>
		internal static void AddCount(int index, uint count, uint notify)
		{
			lock (_syncObject[index])
			{
				_count[index] += count;
				_notify[index] |= notify;
				// Add the batch number if the batch has been finalized.
				if ((uint)(notify & (0x1 << BatchFinalizeFlagBitCount)) != 0) _batchNo[index]++;
			}
		}

		/// <summary>
		/// Get the count
		/// </summary>
		/// <param name="index">Device ID</param>
		/// <param name="notify">Parameter for notification</param>
		/// <param name="batchNo">Batch number</param>
		/// <returns></returns>
		internal static uint GetCount(int index, out uint notify, out int batchNo)
		{
			lock (_syncObject[index])
			{
				notify = _notify[index];
				_notify[index] = 0;
				batchNo = _batchNo[index];
				return _count[index];
			}
		}

		/// <summary>
		/// Clear the number of elements.
		/// </summary>
		/// <param name="index">Device ID</param>
		private static void ClearCount(int index)
		{
			lock (_syncObject[index])
			{
				_count[index] = 0;
			}
		}

		/// <summary>
		/// Clear notifications.
		/// </summary>
		/// <param name="index">Device ID</param>
		private static void ClearNotify(int index)
		{
			lock (_syncObject[index])
			{
				_notify[index] = 0;
			}
		}

		#endregion
	}
	/// <summary>
	/// Profile data class
	/// </summary>
	public class ProfileData
	{
		#region constant
		private const int LUMINANCE_OUTPUT_ON_VALUE = 1;
		public  const int MULTIPLE_VALUE_FOR_LUMINANCE_OUTPUT = 2; 
		#endregion

		#region Field
		/// <summary>
		/// Profile data
		/// </summary>
		private int[] _profData;

		/// <summary>
		/// Profile information
		/// </summary>
		private LJX8IF_PROFILE_INFO _profileInfo;

		#endregion

		#region Property
		/// <summary>
		/// Profile Data
		/// </summary>
		public int[] ProfData
		{
			get { return _profData; }
		}

		/// <summary>
		/// Profile Imformation
		/// </summary>
		public LJX8IF_PROFILE_INFO ProfInfo
		{
			get { return _profileInfo; }
		}
		 #endregion

		#region Method
		/// <summary>
		/// Constructor
		/// </summary>
		public ProfileData(int[] receiveBuffer, LJX8IF_PROFILE_INFO profileInfo)
		{
			SetData(receiveBuffer, profileInfo);
		}

		/// <summary>
		/// Constructor Overload
		/// </summary>
		/// <param name="receiveBuffer">Receive buffer</param>
		/// <param name="startIndex">Start position</param>
		/// <param name="profileInfo">Profile information</param>
		public ProfileData(int[] receiveBuffer, int startIndex, LJX8IF_PROFILE_INFO profileInfo)
		{
			int bufIntSize = CalculateDataSize(profileInfo);
			int[] bufIntArray = new int[bufIntSize];
			_profileInfo = profileInfo;

			Array.Copy(receiveBuffer, startIndex, bufIntArray, 0, bufIntSize);
			SetData(bufIntArray, profileInfo);
		}

		/// <summary>
		/// Set the members to the arguments.
		/// </summary>
		/// <param name="receiveBuffer">Receive buffer</param>
		/// <param name="profileInfo">Profile information</param>
		private void SetData(int[] receiveBuffer, LJX8IF_PROFILE_INFO profileInfo)
		{
			_profileInfo = profileInfo;

			// Extract the header.
			int headerSize = Utility.GetByteSize(Utility.TypeOfStructure.ProfileHeader) /  Marshal.SizeOf(typeof(int));
			int[] headerData = new int[headerSize];
			Array.Copy(receiveBuffer, 0, headerData, 0, headerSize);

			// Extract the footer.
			int footerSize = Utility.GetByteSize(Utility.TypeOfStructure.ProfileFooter) /  Marshal.SizeOf(typeof(int));
			int[] footerData = new int[footerSize];
			Array.Copy(receiveBuffer, receiveBuffer.Length - footerSize, footerData, 0, footerSize);

			// Extract the profile data.
			int profSize = receiveBuffer.Length - headerSize - footerSize;
			_profData = new int[profSize];
			Array.Copy(receiveBuffer, headerSize, _profData, 0, profSize);
		}

		/// <summary>
		/// Data size calculation
		/// </summary>
		/// <param name="profileInfo">Profile information</param>
		/// <returns>Profile data size</returns>
		public static int CalculateDataSize(LJX8IF_PROFILE_INFO profileInfo)
		{
			LJX8IF_PROFILE_HEADER header = new LJX8IF_PROFILE_HEADER();
			LJX8IF_PROFILE_FOOTER footer = new LJX8IF_PROFILE_FOOTER();

			int multipleValue = GetIsLuminanceOutput(profileInfo) ? MULTIPLE_VALUE_FOR_LUMINANCE_OUTPUT : 1;
			return profileInfo.nProfileDataCount * multipleValue + (Marshal.SizeOf(header) + Marshal.SizeOf(footer)) /  Marshal.SizeOf(typeof(int));
		}

		public static bool GetIsLuminanceOutput(LJX8IF_PROFILE_INFO profileInfo)
		{
			return profileInfo.byLuminanceOutput == LUMINANCE_OUTPUT_ON_VALUE;
		}

		/// <summary>
		/// Create the X-position string from the profile information.
		/// </summary>
		/// <param name="profileInfo">Profile information</param>
		/// <returns>X-position string</returns>
		public static string GetXPositionString(LJX8IF_PROFILE_INFO profileInfo)
		{
			StringBuilder sb = new StringBuilder();
			// Data position calculation
			double posX = profileInfo.lXStart;
			double deltaX = profileInfo.lXPitch;

			int singleProfileCount = profileInfo.nProfileDataCount;
			int dataCount = profileInfo.byProfileCount;

			for (int i = 0; i < dataCount; i++)
			{
				for (int j = 0; j < singleProfileCount; j++)
				{
					sb.AppendFormat("{0}\t", (posX + deltaX * j));
				}
			}
			return sb.ToString();
		}

		#endregion
	}
}
