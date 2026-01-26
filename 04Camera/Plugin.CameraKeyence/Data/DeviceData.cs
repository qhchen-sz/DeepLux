//----------------------------------------------------------------------------- 
// <copyright file="DeviceData.cs" company="KEYENCE">
//	 Copyright (c) 2019 KEYENCE CORPORATION. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------- 
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace Device.Keyence3DCameraDevice
{
	#region Enum
	/// <summary>Device communication state</summary>
	public enum DeviceStatus
	{
		NoConnection = 0,
		Ethernet,
		EthernetFast,
	};
	//该枚举用于存储值转化成um的换算系数计算

	public enum SensorType
    {
		LJ_X8020, //0.4um
		LJ_X8060,//0.8um
		LJ_X8080,//1.6
		LJ_X8200,//4
		LJ_X8400,//8
		LJ_X8900,//16
		LJ_V7020K_B,//0.4
		LJ_V7060K_B,//0.8
		LJ_V7080B,//1.6
		LJ_V7200B,//4
		LJ_V7300B ,//8
    }
	#endregion
	/// <summary>
	/// Utility class
	/// </summary>
	public static class Utility
	{
		#region Constant
		/// <summary>
		/// value for head temperature display
		/// </summary>
		private const int DivideValueForHeadTemperatureDisplay = 100;
		/// <summary>
		///  head temperature invalid value
		/// </summary>
		private const int HeadTemperatureInvalidValue = 0xFFFF;
		#endregion

		#region Enum
		/// <summary>
		/// Structure classification
		/// </summary>
		public enum TypeOfStructure
		{
			ProfileHeader,
			ProfileFooter,
		}
		#endregion

		#region Method

		#region Get the byte size

		/// <summary>
		/// Get the byte size of the structure.
		/// </summary>
		/// <param name="type">Structure whose byte size you want to get.</param>
		/// <returns>Byte size</returns>
		public static int GetByteSize(TypeOfStructure type)
		{
			switch (type)
			{
				case TypeOfStructure.ProfileHeader:
					LJX8IF_PROFILE_HEADER profileHeader = new LJX8IF_PROFILE_HEADER();
					return Marshal.SizeOf(profileHeader);

				case TypeOfStructure.ProfileFooter:
					LJX8IF_PROFILE_FOOTER profileFooter = new LJX8IF_PROFILE_FOOTER();
					return Marshal.SizeOf(profileFooter);
			}

			return 0;
		}
		#endregion

		#region Acquisition of log 

		/// <summary>
		/// Get the string for log output.
		/// </summary>
		/// <param name="profileInfo">profileInfo</param>
		/// <returns>String for log output</returns>
		public static StringBuilder ConvertProfileInfoToLogString(LJX8IF_PROFILE_INFO profileInfo)
		{
			StringBuilder sb = new StringBuilder();

			// Profile information of the profile obtained
			sb.AppendLine(string.Format(@"  Profile Data Num			: {0}", profileInfo.byProfileCount));
			string luminanceOutput = profileInfo.byLuminanceOutput == 0
				? @"OFF"
				: @"ON";
			sb.AppendLine(string.Format(@"  Luminance output			: {0}", luminanceOutput));
			sb.AppendLine(string.Format(@"  Profile Data Points			: {0}", profileInfo.nProfileDataCount));
			sb.AppendLine(string.Format(@"  X coordinate of the first point	: {0}", profileInfo.lXStart));
			sb.Append(string.Format(@"  X-direction interval		: {0}", profileInfo.lXPitch));

			return sb;
		}

		/// <summary>
		/// Get the string for log output.
		/// </summary>
		/// <param name="response">"Get batch profile" command response</param>
		/// <returns>String for log output</returns>
		public static StringBuilder ConvertBatchProfileResponseToLogString(LJX8IF_GET_BATCH_PROFILE_RESPONSE response)
		{
			StringBuilder sb = new StringBuilder();

			// Profile information of the profile obtained
			sb.AppendLine(string.Format(@"  CurrentBatchNo			: {0}", response.dwCurrentBatchNo));
			sb.AppendLine(string.Format(@"  CurrentBatchProfileCount		: {0}", response.dwCurrentBatchProfileCount));
			sb.AppendLine(string.Format(@"  OldestBatchNo			: {0}", response.dwOldestBatchNo));
			sb.AppendLine(string.Format(@"  OldestBatchProfileCount		: {0}", response.dwOldestBatchProfileCount));
			sb.AppendLine(string.Format(@"  GetBatchNo			: {0}", response.dwGetBatchNo));
			sb.AppendLine(string.Format(@"  GetBatchProfileCount		: {0}", response.dwGetBatchProfileCount));
			sb.AppendLine(string.Format(@"  GetBatchTopProfileNo		: {0}", response.dwGetBatchTopProfileNo));
			sb.AppendLine(string.Format(@"  GetProfileCount			: {0}", response.byGetProfileCount));
			sb.Append(string.Format(@"  CurrentBatchCommited		: {0}", response.byCurrentBatchCommited));

			return sb;
		}

		/// <summary>
		/// Get the string for log output.
		/// </summary>
		/// <param name="response">"Get profile" command response</param>
		/// <returns>String for log output</returns>
		public static StringBuilder ConvertProfileResponseToLogString(LJX8IF_GET_PROFILE_RESPONSE response)
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendLine(string.Format(@"  CurrentProfileNo	: {0}", response.dwCurrentProfileNo));
			sb.AppendLine(string.Format(@"  OldestProfileNo	: {0}", response.dwOldestProfileNo));
			sb.AppendLine(string.Format(@"  GetTopProfileNo	: {0}", response.dwGetTopProfileNo));
			sb.Append(string.Format(@"  GetProfileCount	: {0}", response.byGetProfileCount));

			return sb;
		}

		#endregion

		/// <summary>
		/// Get the string for log output.
		/// </summary>
		/// <param name="sensorTemperature">sensor Temperature</param>
		/// <param name="processorTemperature">processor Temperature</param>
		/// <param name="caseTemperature">case Temperature</param>
		/// <returns>String for log output</returns>
		public static StringBuilder ConvertHeadTemperatureLogString(short sensorTemperature, short processorTemperature, short caseTemperature)
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendLine(string.Format(@"  SensorTemperature	: {0}", GetTemperatureString(sensorTemperature)));
			sb.AppendLine(string.Format(@"  ProcessorTemperature	: {0}", GetTemperatureString(processorTemperature)));
			sb.Append(string.Format(@"  CaseTemperature		: {0}", GetTemperatureString(caseTemperature)));

			return sb;
		}

		private static string GetTemperatureString(short temperature)
		{
			if ((temperature & HeadTemperatureInvalidValue) == HeadTemperatureInvalidValue)
			{
				return "----";
			}
			return string.Format(@"{0} C", (decimal)temperature / DivideValueForHeadTemperatureDisplay);
		}
		#endregion
	}
	/// <summary>
	/// Constant class
	/// </summary>
	public static class Define
	{
		#region Constant

		public enum LjxHeadSamplingPeriod
		{
			LjxHeadSamplingPeriod10Hz = 0,
			LjxHeadSamplingPeriod20Hz,
			LjxHeadSamplingPeriod50Hz,
			LjxHeadSamplingPeriod100Hz,
			LjxHeadSamplingPeriod200Hz,
			LjxHeadSamplingPeriod500Hz,
			LjxHeadSamplingPeriod1KHz,
			LjxHeadSamplingPeriod2KHz,
			LjxHeadSamplingPeriod4KHz,
			LjxHeadSamplingPeriod8KHz,
			LjxHeadSamplingPeriod16KHz,
			LjxHeadSamplingPeriod1_5KHz,
			LjxHeadSamplingPeriod2_5KHz,
			LjxHeadSamplingPeriod3KHz,
			LjxHeadSamplingPeriod3_5KHz,
			LjxHeadSamplingPeriod4_5KHz,
			LjxHeadSamplingPeriod5KHz,
			LjxHeadSamplingPeriod6KHz,
			LjxHeadSamplingPeriod7KHz,
			LjxHeadSamplingPeriod12KHz,
		}

		public enum LuminanceOutput
		{
			LuminanceOutputOn,
			LuminanceOutputOff
		}

		/// <summary>
		/// Maximum amount of data for 1 profile
		/// </summary>
		public const int MaxProfileCount = LjxHeadMeasureRangeFull;
		public const float InvalidZValueum = -999.9999f;
		public const int CorrectZValue = 32768;
		//轮廓数据（高度数据），以0.01um为单位存储
		public const double ProfileUnit = 0.01f;
		/// <summary>
		/// Device ID (fixed to 0)
		/// </summary>
		public const int DeviceId = 0;

		/// <summary>
		/// Maximum profile count that store to buffer.
		/// </summary>
#if WIN64
		public const int BufferFullCount = 120000;
#else
		public const int BufferFullCount = 30000;
#endif
		// @Point
		//  32-bit architecture cannot allocate huge memory and the buffer limit is more strict.

		/// <summary>
		/// Measurement range X direction of LJ-X Head
		/// </summary>
		public const int LjxHeadMeasureRangeFull = 3200;
		public const int LjxHeadMeasureRangeThreeFourth = 2400;
		public const int LjxHeadMeasureRangeHalf = 1600;
		public const int LjxHeadMeasureRangeQuarter = 800;

		/// <summary>
		/// Light reception characteristic
		/// </summary>
		public const int ReceivedBinningOff = 1;
		public const int ReceivedBinningOn = 2;

		public const int ThinningXOff = 1;
		public const int ThinningX2 = 2;
		public const int ThinningX4 = 4;


		/// <summary>
		/// Measurement range X direction of LJ-V Head
		/// </summary>
		public const int MeasureRangeFull = 800;
		public const int MeasureRangeMiddle = 600;
		public const int MeasureRangeSmall = 400;
		#endregion
	}
	/// <summary>
	/// Device data class
	/// </summary>
	public class DeviceData
	{
		#region Field
		/// <summary>Connection state</summary>
		private DeviceStatus _status = DeviceStatus.NoConnection;

		#endregion

		#region Property
		/// <summary>
		/// Status property
		/// </summary>
		public DeviceStatus Status
		{ 
			get { return _status; }
			set
			{
				ProfileData.Clear();
				ProfileDataHighSpeed.Clear();
				SimpleArrayData.Clear();
				SimpleArrayDataHighSpeed.Clear();
				EthernetConfig = new LJX8IF_ETHERNET_CONFIG();
				_status = value; 
			} 
		}

		/// <summary>Ethernet settings</summary>
		public LJX8IF_ETHERNET_CONFIG EthernetConfig { get; set; }
		/// <summary>Profile data</summary>
		public List<ProfileData> ProfileData { get; }
		/// <summary>Profile data for high speed communication</summary>
		public List<ProfileData> ProfileDataHighSpeed { get; }
		/// <summary>Simple array data</summary>
		public ProfileSimpleArrayStore SimpleArrayData { get; }
		/// <summary>Simple array data for high speed communication</summary>
		public ProfileSimpleArrayStore SimpleArrayDataHighSpeed { get; }
		#endregion

		#region Constructor
		/// <summary>
		/// Constructor
		/// </summary>
		public DeviceData()
		{
			EthernetConfig = new LJX8IF_ETHERNET_CONFIG();
			ProfileData = new List<ProfileData>();
			ProfileDataHighSpeed = new List<ProfileData>();
			SimpleArrayData = new ProfileSimpleArrayStore(); ;
			SimpleArrayDataHighSpeed = new ProfileSimpleArrayStore(); ;
			var str = GetStatusString();
		}
		#endregion

		#region Method
		/// <summary>
		/// Connection status acquisition
		/// </summary>
		/// <returns>Connection status for display</returns>
		public string GetStatusString()
		{
			string status = _status.ToString();
			switch (_status)
			{			
			case DeviceStatus.Ethernet:
			case DeviceStatus.EthernetFast:
				status += string.Format("---{0}.{1}.{2}.{3}", EthernetConfig.abyIpAddress[0], EthernetConfig.abyIpAddress[1],
					EthernetConfig.abyIpAddress[2], EthernetConfig.abyIpAddress[3]);
				break;
			}
			return status;
		}
		#endregion
	}
	/// <summary>
	/// Object pinning class
	/// </summary>
	public sealed class PinnedObject : IDisposable
	{
		#region Field

		private GCHandle _handle;      // Garbage collector handle

		#endregion

		#region Property

		/// <summary>
		/// Get the address.
		/// </summary>
		public IntPtr Pointer
		{
			// Get the leading address of the current object that is pinned.
			get { return _handle.AddrOfPinnedObject(); }
		}

		#endregion

		#region Constructor

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="target">Target to protect from the garbage collector</param>
		public PinnedObject(object target)
		{
			// Pin the target to protect it from the garbage collector.
			_handle = GCHandle.Alloc(target, GCHandleType.Pinned);
		}

		#endregion

		#region Interface
		/// <summary>
		/// Interface
		/// </summary>
		public void Dispose()
		{
			_handle.Free();
			_handle = new GCHandle();
		}

		#endregion
	}
}
