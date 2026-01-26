using HV.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.CameraBopixel
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct BopixelCameraInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string CamName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SerialNO;

        public IntPtr CardHandle;
        public uint DeviceNum;

        // IsEmpty 和 operator== 方法在 C# 中通常不直接转换，而是通过其他方式实现。
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BopixelCameraInfoModel
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public BopixelCameraInfo[] Info;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Bopixel2DFrameInfo
    {
        public long OffsetX;
        public long OffsetY;
        public long Width;
        public long Height;
        public long Format;
        public ulong BufSize;
        public ulong Timestamp;
        public ulong FrameId;
    }
    public enum BopixelCameraErrorCode
    {
        OK = 0,
        NoCard = -2,
        CreateCardFail = -3,
        CreateCameraFail = -4,
        NullHandle = -5,
        NullObject = -6,
        NoDrive = -10
    }

    // 回调函数定义
    public delegate void BopixelCallBackFunc(ref Bopixel2DFrameInfo frameInfo, IntPtr pBase, ulong size);
    public  class BopixelCamera
    {
        public IntPtr Obj = IntPtr.Zero;
        public BopixelCameraInfoModel bopixelCameraInfoModel;
        public bool DeviceStatic;
        public BopixelCamera()
        {
            Obj = Create();
            bopixelCameraInfoModel = new BopixelCameraInfoModel();
        }
        public BopixelCameraErrorCode SelectCam()//获取相机
        {
            return (BopixelCameraErrorCode)EnumCameras(out bopixelCameraInfoModel);
        }
        public BopixelCameraErrorCode ConnectCam(BopixelCameraInfo info)
        {
            return (BopixelCameraErrorCode)ConnectDev( info, Obj);
        }
        public BopixelCameraErrorCode DisConnectCam()
        {
            return (BopixelCameraErrorCode)DisConnectDev(Obj);
        }
        public BopixelCameraErrorCode LoadDeviceSetting(string filePath)
        {
            return (BopixelCameraErrorCode)LoadDeviceSetting(filePath, Obj);
        }
        public BopixelCameraErrorCode LoadCameraSetting(string filePath)
        {
            return (BopixelCameraErrorCode)LoadCameraSetting(filePath, Obj);
        }
        public BopixelCameraErrorCode SetTriggerMode(eTrigMode mode)
        {
            return (BopixelCameraErrorCode)SetTriggerMode((int)mode, Obj);
        }
        public BopixelCameraErrorCode SetCallbackFunc(BopixelCallBackFunc func)
        {
            return (BopixelCameraErrorCode)SetCallbackFunc(func, Obj);
        }
        //public BopixelCameraErrorCode FreeimageBuffer(IntPtr intPtr)
        //{
        //    return (BopixelCameraErrorCode)FreeBuffer(intPtr);
        //}
        public BopixelCameraErrorCode StartGrabbing()
        {
            return (BopixelCameraErrorCode)StartGrabbing(Obj);
        }
        public BopixelCameraErrorCode StopGrabbing()
        {
            return (BopixelCameraErrorCode)StopGrabbing(Obj);
        }
        private const string DLL_NAME = "CameraBopixel.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Create();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Destroy(IntPtr objectHandle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EnumCameras(out BopixelCameraInfoModel cameras);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ConnectDev(BopixelCameraInfo info, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DisConnectDev(IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetGain(float value, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float GetGain(IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetExposureTime(float value, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float GetExposureTime(IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CaptureImage(bool byHand, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetOutPut(int index, int time, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LoadDeviceSetting(string filePath, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LoadCameraSetting(string filePath, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetTriggerMode(int mode, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetTriggerMode(IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetCallbackFunc(BopixelCallBackFunc func, IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeBuffer(IntPtr buffer);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int StartGrabbing(IntPtr obj);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int StopGrabbing(IntPtr obj);



    }
    

}

