using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Models;
using Device.Keyence3DCameraDevice;
using System.Threading;

namespace Plugin.CameraKeyence
{
    [Category("相机")]
    [DisplayName("基恩士相机")]
    [Serializable]
    public class CameraKeyence : CameraBase
    {
        [NonSerialized]
        private static bool isDllInit = false;
        [NonSerialized]
        private DeviceData deviceData;
        [NonSerialized]
        private HighSpeedDataCallBack highSpeedDataCallBack;
        [NonSerialized]
        private DeviceData[] _deviceData = new DeviceData[NativeMethods.DeviceCount];
        //[NonSerialized]
        //private HighSpeedDataCallBackForSimpleArray highSpeedDataCallbackForSimpleArray;
        [NonSerialized]
        public static bool IsManmual = false;
        //[NonSerialized]
        //private CancellationTokenSource _grabCancellationSource;
        [NonSerialized]
        private bool _isDataReceived;

        [NonSerialized]
        LJX8IF_HIGH_SPEED_PRE_START_REQUEST request;
        [NonSerialized]
        LJX8IF_PROFILE_INFO profileInfo;
        [NonSerialized]
        LJX8IF_ETHERNET_CONFIG ethernetConfig;
        private int KeyenceHeight = 0;
        private bool IsMeasure = true;
        public bool _isGrabbing = false;
        public bool Start = false, Stop = true;
        private bool GetImage = false;
        public CameraKeyence() : base() { }
        /// <summary>搜索相机</summary>
        public override List<CameraInfoModel> SearchCameras()
        {
            List<CameraInfoModel> mCamInfoList = new List<CameraInfoModel>();
            var camInfoList = Enum.GetValues(typeof(SensorType));
            if (camInfoList.Length== 0)
            {
                MessageView.Ins.MessageBoxShow("查找设备失败", eMsgType.Warn);
                return mCamInfoList;
            }

            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            foreach (var item in camInfoList)
            {
                CameraInfoModel _camInfo = new CameraInfoModel()
                {
                    ExtInfo = item,
                    CameraIP = "192.168.200.1",
                    CamName = item.ToString(),
                    Connected = false,
                    SerialNO = item.ToString(),


                };

                mCamInfoList.Add(_camInfo);
            }

            return mCamInfoList;
        }
        public override void CamSetPara()
        {
            SetExposureTime(ExposeTime);
            SetImageHeight(ImageHeight);
            SetGain(Gain);
        }

        /// <summary>连接相机</summary>
        public override void ConnectDev()
        {
            try
            {
                GetImage = false;
                if (!isDllInit)
                {
                    NativeMethods.LJX8IF_Initialize();
                    deviceData = new DeviceData();

                    highSpeedDataCallBack = HighSpeedDataCallbackFunc;

                    isDllInit = true;
                }
                for (int i = 0; i < NativeMethods.DeviceCount; i++)
                {
                    if (_deviceData==null)
                    {
                        _deviceData = new DeviceData[NativeMethods.DeviceCount];
                    }
                    _deviceData[i] = new DeviceData();
                    _deviceData[i].Status = DeviceStatus.NoConnection;

                }


                if (Connected)
                {
                    DisConnectDev();
                }
                if (CameraIP.Split('.').Length != 4)
                {
                    Connected = false;
                }

                //IP及端口参数
                ethernetConfig = new LJX8IF_ETHERNET_CONFIG
                {
                    abyIpAddress = new byte[]
                    {
                        Convert.ToByte(CameraIP.Split('.')[0]),
                        Convert.ToByte(CameraIP.Split('.')[1]),
                        Convert.ToByte(CameraIP.Split('.')[2]),
                        Convert.ToByte(CameraIP.Split('.')[3])
                    },    // IP address
                    wPortNo = 24691,                                 // Port number
                };
                int.TryParse(SerialNo,out int tem);
                int errCode = NativeMethods.LJX8IF_EthernetOpen(tem, ref ethernetConfig);
                if (errCode == (int)Rc.Ok)
                {
                    Connected = true;
                    CamGetPara();
                    //初始化高速数据通信/SimpleArray
                    //errCode = NativeMethods.LJX8IF_InitializeHighSpeedDataCommunication(DeviceId, ref ethernetConfig, HighSpeedPort, highSpeedDataCallBack, YLineNum, (uint)DeviceId);
                    //highSpeedDataCallbackForSimpleArray = HighSpeedDataCallbackFuncForSimpleArray;
                    //errCode = NativeMethods.LJX8IF_InitializeHighSpeedDataCommunicationSimpleArray(tem, ref ethernetConfig, 24692, highSpeedDataCallbackForSimpleArray, (uint)ImageHeight, (uint)tem);
                    if (errCode == (int)Rc.Ok)
                    {

                        //if (!isDllInit)
                        //{
                        //    //NativeMethods.LJX8IF_Initialize();
                        //    deviceData = new DeviceData();

                        //    highSpeedDataCallBack = HighSpeedDataCallbackFunc;

                        //    isDllInit = true;
                        //}
                        //预开始
                        //request = new LJX8IF_HIGH_SPEED_PRE_START_REQUEST { bySendPosition = Convert.ToByte(2) };
                        ////轮廓信息
                        //profileInfo = new LJX8IF_PROFILE_INFO();
                        //errCode = NativeMethods.LJX8IF_PreStartHighSpeedDataCommunication(tem, ref request, ref profileInfo);
                        //if (errCode == (int)Rc.Ok)
                        //{

                        //}
                        //else
                        //{

                        //}
                        ////开始
                        //errCode = NativeMethods.LJX8IF_StartHighSpeedDataCommunication(tem);
                        //if (errCode == (int)Rc.Ok)
                        //{
                        //    //LogModule.Log.Info("设备高速通信开始成功");
                        //}
                        //else
                        //{
                        //    //LogModule.Log.Error("设备高速通信开始失败：" + ((Rc)errCode).ToString());
                        //}
                    }
                    else
                    {
                        //LogModule.Log.Error("设备高速通信初始化失败：" + ((Rc)errCode).ToString());
                    }

                }
                else
                {
                    //打开失败
                    Connected = false;
                    Logger.AddLog("打开设备出错：" + ((Rc)errCode).ToString(), msgType: eMsgType.Error);
                    //LogModule.Log.Error("打开设备出错：" + ((Rc)errCode).ToString());
                }

            }
            catch (Exception ex)
            {
                Connected = false;
                Logger.AddLog("打开设备出错：" + ex.ToString(), msgType: eMsgType.Error);
            }
            base.ConnectDev();
        }
        /// <summary>断开相机</summary>
        public override void DisConnectDev()
        {
            if (Connected)
            {
                int.TryParse(SerialNo, out int tem);
                GetImage = false;
                NativeMethods.LJX8IF_StopHighSpeedDataCommunication(tem);
                NativeMethods.LJX8IF_FinalizeHighSpeedDataCommunication(tem);
                NativeMethods.LJX8IF_CommunicationClose(tem);
                Connected = false;
            }
            base.DisConnectDev();
        }
        /// <summary>采集图像,是否手动采图</summary>
        //public override bool CaptureImage(bool byHand)
        //{
        //    try
        //    {
        //        if (HImages == null)
        //            HImages = new Queue<HImage>();
        //        if (Connected)
        //        {
        //            if (_isGrabbing)
        //            {
        //                StopGrab();
        //            }
        //        }

        //        if (!byHand)
        //        {
        //            //return false;
        //            if (HImages.Count == 0)
        //                StartGrab();
        //            WaitImage();
        //                StopGrab();
        //        }
        //        else
        //        {
        //            if (HImages.Count == 0)
        //                StartGrab();
        //            WaitImage(6000);
        //            StopGrab();
        //        }




        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.GetExceptionMsg(ex);
        //        return false;
        //    }
        //}
        [NonSerialized]
        private Thread _captureThread;
        [NonSerialized]
        private  object _threadLock = new object();
        [NonSerialized]
        private volatile bool _isCaptureRunning = false;
        public override bool CaptureImage(bool byHand)
        {
            if (_threadLock == null) _threadLock =  new object();
            lock (_threadLock)
            {
                if (_isCaptureRunning)
                {
                    // 如果已经在运行，等待完成或强制停止
                    if (_captureThread != null && _captureThread.IsAlive)
                    {
                        _captureThread.Join(2000); // 等待2秒
                        if (_captureThread.IsAlive)
                        {
                            _captureThread.Abort(); // 强制终止（谨慎使用）
                        }
                    }
                }

                _isCaptureRunning = true;

                // 使用新线程执行采集
                bool result = false;
                var captureThread = new Thread(() =>
                {
                    try
                    {
                        result = ThreadSafeCaptureImage(byHand);
                    }
                    catch (ThreadAbortException)
                    {
                        // 线程被中止，正常处理
                        Thread.ResetAbort();
                    }
                    catch (Exception ex)
                    {
                        Logger.GetExceptionMsg(new Exception($"采集线程异常: {ex.Message}"));
                        result = false;
                    }
                    finally
                    {
                        _isCaptureRunning = false;
                    }
                })
                {
                    Name = "ImageCaptureThread",
                    IsBackground = true // 设置为后台线程，主程序退出时自动结束
                };

                captureThread.Start();
                _captureThread = captureThread;

                // 等待线程完成
                if (captureThread.Join(10000)) // 10秒超时
                {
                    return result;
                }
                else
                {
                    Logger.GetExceptionMsg(new TimeoutException("图像采集线程超时"));
                    return false;
                }
            }
        }
        private bool ThreadSafeCaptureImage(bool byHand)
        {
            try
            {
                if (HImages == null)
                    HImages = new Queue<HImage>();

                // 检查相机连接状态
                if (!Connected)
                {
                    Logger.GetExceptionMsg(new Exception("相机未连接"));
                    return false;
                }

                // 停止正在进行的采集
                if (_isGrabbing)
                {
                    StopGrab();
                    Thread.Sleep(100); // 给相机一些时间停止
                }

                // 执行采集
                if (!byHand)
                {
                    if (HImages.Count == 0)
                        StartGrab();
                    WaitImage();
                    StopGrab();
                }
                else
                {
                    if (HImages.Count == 0)
                        StartGrab();
                    WaitImage(DelayTime);
                    StopGrab();
                }

                return HImages.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }
        public override bool SetOutPut(int lineIndex, int time)
        {
            ////Strobe输出
            //nRet = MV_CC_SetEnumValue(handle, "LineSelector", 1);
            ////0:Line0 1:Line1 2:Line2 
            //nRet = MV_CC_SetEnumValue(handle, "LineMode", 8);//仅LineSelector为line2时需要特意设置，其他输出不需要
            //                                                 //0:Input 1:Output 8:Strobe 
            //int DurationValue = 0, DelayValue = 0, PreDelayValue = 0;//us
            //nRet = MV_CC_SetIntValue(handle, "StrobeLineDuration", DurationValue);
            ////strobe持续时间，设置为0，持续时间就是曝光时间，设置其他值，就是其他值时间
            //nRet = MV_CC_SetIntValue(handle, "StrobeLineDelay", DelayValue);//strobe延时，从曝光开始，延时多久输出
            //nRet = MV_CC_SetIntValue(handle, "StrobeLinePreDelay", PreDelayValue);//strobe提前输出，曝光延后开始
            //                                                                      //--------------------------------------------------------------------------------------------------
            //nRet = MV_CC_SetBoolValue(handle, "StrobeEnable", TRUE);/
            //int nRet = 0;
            //if (lineIndex != 1 && lineIndex != 2)
            //    return false;
            //nRet += MyCamera.MV_CC_SetEnumValue_NET("LineSelector", (uint)lineIndex);//1:Line1 2:Line2 
            ////if (lineIndex == 2)
            ////    nRet += MyCamera.MV_CC_SetEnumValue_NET("LineMode", 8);//0:Input 1:Output 8:Strobe //仅LineSelector为line2时需要特意设置，其他输出不需要
            //nRet += MyCamera.MV_CC_SetIntValue_NET("StrobeLineDuration", (uint)time); //strobe持续时间，设置为0，持续时间就是曝光时间，设置其他值，就是其他值时间
            ////nRet += MyCamera.MV_CC_SetBoolValue_NET("StrobeEnable", true); //Strobe使能
            //nRet += MyCamera.MV_CC_SetCommandValue_NET("LineTriggerSoftware");//触发输出
            //if (nRet != MyCamera.MV_OK)
            //{
            //    //ShowErrorMsg("Set CaptureImage Time Fail!", nRet);
            //    return false;
            //}
            return true;
        }
        /// <summary>未使用</summary>
        public override void LoadSetting(string filePath)
        {
            //if (File.Exists(filePath))
            //    MyCamera.LoadCameraSetting(filePath);
        }
        /// <summary>未使用</summary>
        public override void SaveSetting(string filePath)
        {
            //MyCamera.MV_CC_FeatureSave_NET(filePath);
        }
        /// <summary> 相机设置</summary>
        public override void SetSetting()
        {
            //int nRet = 0;
            ////设置采集模式
            //SetTriggerMode(TrigMode);
            ////设置曝光时间
            //SetExposureTime(ExposeTime);
            ////nRet = MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", ExposeTime);
            //SetGain((long)Gain);
            ////置帧率
            //nRet = MyCamera.MV_CC_SetFloatValue_NET("AcquisitionFrameRate", float.Parse(Framerate));
            //设置ip
            //apiReturn = myApi.Gige_Camera_setIPAddress(m_Handle, uint.Parse(_UniqueLabel), uint.Parse(_DevDirExt));

        }

        public override void CamGetPara() 
        {
            GetSetting();
        }
        public void GetSetting()
        {
            if (!Connected)
                return;
            LJX8IF_TARGET_SETTING targetSetting = new LJX8IF_TARGET_SETTING()
            {
                byCategory = 0,
                byItem = 10,
                byTarget1 = 0,
                byTarget2 = 0,
                byTarget3 = 0,
                byTarget4 = 0,
                byType = 16,
                reserve = 0,
            };
            int.TryParse(SerialNo, out int tem);
            byte[] bytes = new byte[2];
            PinnedObject pin = new PinnedObject(bytes);
            int rc = NativeMethods.LJX8IF_GetSetting(tem, 1, targetSetting, pin.Pointer, 2);
            KeyenceHeight = (bytes[1] << 8) | bytes[0];
            ImageHeight = KeyenceHeight;
            //触发模式
            targetSetting.byItem = 1;
            rc = NativeMethods.LJX8IF_GetSetting(tem, 1, targetSetting, pin.Pointer, 1);
            switch(bytes[0].ToString())
            {
                case "0":
                    TrigMode3D = e3DTrigMode.连续触发;
                    break;
                case "1":
                    TrigMode3D = e3DTrigMode.外部触发;
                    break;
                case "2":
                    TrigMode3D = e3DTrigMode.编码器触发;
                    break;

            }
            //批处理开关
            targetSetting.byItem = 3;
            rc = NativeMethods.LJX8IF_GetSetting(tem, 1, targetSetting, pin.Pointer, 1);
            switch (bytes[0].ToString())
            {
                case "0":
                    IsMeasure = false;
                    break;
                case "1":
                    IsMeasure = true;
                    break;

            }
            
        }
        public void StopGrab()
        {
            try
            {
                Start = true;
                Stop = false;
                int.TryParse(SerialNo, out int tem);
                int ERRO = 0;
                if (IsMeasure)
                {
                    ERRO += NativeMethods.LJX8IF_StopMeasure(tem);
                }
                    
                ERRO += NativeMethods.LJX8IF_ClearMemory(tem);
                ERRO += NativeMethods.LJX8IF_StopHighSpeedDataCommunication(tem);
                ERRO += NativeMethods.LJX8IF_FinalizeHighSpeedDataCommunication(tem);
                _isGrabbing = false;
            }
            catch (Exception ex)
            {
                Logger.AddLog("基恩士:" + ex.Message, eMsgType.Error);
            }

        }
        public void StartGrab(bool isManmual = false)
        {
            try
            {
                Start = false;
                Stop = true;
                int.TryParse(SerialNo, out int tem);
                CameraBase.IsStop = false;
                IsManmual = isManmual;
                //NativeMethods.LJX8IF_Trigger(DeviceId);
                //NativeMethods.LJX8IF_StopMeasure(tem);
                int ret = 0;
                //ret = NativeMethods.LJX8IF_StartHighSpeedDataCommunication(tem);
                //预开始
                //request = new LJX8IF_HIGH_SPEED_PRE_START_REQUEST { bySendPosition = Convert.ToByte(2) };
                ////轮廓信息
                //profileInfo = new LJX8IF_PROFILE_INFO();
                //int errCode = NativeMethods.LJX8IF_PreStartHighSpeedDataCommunication(tem, ref request, ref profileInfo);
                //开始
                GetImage = false;
                highSpeedDataCallBack = HighSpeedDataCallbackFunc;
                int errCode = NativeMethods.LJX8IF_InitializeHighSpeedDataCommunication(tem, ref ethernetConfig, 24692, highSpeedDataCallBack, (uint)ImageHeight, (uint)tem);
                //highSpeedDataCallbackForSimpleArray = HighSpeedDataCallbackFuncForSimpleArray;
                //int errCode = NativeMethods.LJX8IF_InitializeHighSpeedDataCommunicationSimpleArray(tem, ref ethernetConfig, 24692, highSpeedDataCallbackForSimpleArray, (uint)ImageHeight, (uint)tem);
                request = new LJX8IF_HIGH_SPEED_PRE_START_REQUEST { bySendPosition = Convert.ToByte(2) };
                //轮廓信息
                profileInfo = new LJX8IF_PROFILE_INFO();
                errCode += NativeMethods.LJX8IF_PreStartHighSpeedDataCommunication(tem, ref request, ref profileInfo);
                errCode += NativeMethods.LJX8IF_StartHighSpeedDataCommunication(tem);
                if (IsMeasure)//是否批处理
                {
                    errCode += NativeMethods.LJX8IF_StartMeasure(tem);
                }
                
                //_grabCancellationSource = new CancellationTokenSource(5000);
                //Task.Run(() => MonitorGrabTimeout(_grabCancellationSource.Token), _grabCancellationSource.Token);
                if (errCode == (int)Rc.Ok)
                {
                    Logger.AddLog("开始测量成功", eMsgType.Info);
                    _isGrabbing = true;
                }
                else
                {
                    Logger.AddLog("开始测量失败：" + ((Rc)errCode).ToString(), eMsgType.Error);
                    _isGrabbing = false;
                }
            }
            catch (Exception e)
            {
                Logger.AddLog("基恩士：" + e.Message.ToString(), eMsgType.Error);
                _isGrabbing = false;
            }
        }
        public override void CameraChanged(ChangType changTyp)
        {
            try
            {
                switch (changTyp)
                {
                    case ChangType.增益:
                        SetGain((long)Gain);
                        break;
                    case ChangType.曝光:
                        SetExposureTime((long)ExposeTime);
                        break;
                    case ChangType.宽度:
                        SetWidth();
                        break;
                    case ChangType.高度:
                        SetHeight();
                        break;
                    case ChangType.触发:
                        SetTriggerMode(TrigMode);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog("LMI:" + ex.Message, eMsgType.Error);
            }
        }
        /// <summary>设置宽度</summary>
        public void SetWidth()
        {
            if (Width > 100 & Width <= WidthMax)
            {

            }
        }
        /// <summary>设置高度</summary>
        public void SetHeight()
        {
            if (Height > 50 & Height <= HeightMax)
            {
                LJX8IF_TARGET_SETTING targetSetting = new LJX8IF_TARGET_SETTING()
                {
                    byCategory=0,
                    byItem=10,
                    byTarget1=0,
                    byTarget2=0,
                    byTarget3=0,
                    byTarget4=0,
                    byType=16,
                    reserve = 0,
                };
                int.TryParse(SerialNo, out int tem);
                byte[] bytes = new byte[2];
                bytes[0] = (byte)(Height & 0xFF);
                bytes[1] = (byte)((Height >> 8) & 0xFF);
                PinnedObject pin = new PinnedObject(bytes);
                uint error = 0;
                int rc = NativeMethods.LJX8IF_SetSetting(tem, 1, targetSetting, pin.Pointer, 2, ref error);
            }
        }
        /// <summary>设置图像高度 </summary>
        public override void SetImageHeight(int value)
        {
            try
            {
                if (KeyenceHeight == value)
                    return;
                if (value > 50 & value <= HeightMax)
                {
                    LJX8IF_TARGET_SETTING targetSetting = new LJX8IF_TARGET_SETTING()
                    {
                        byCategory = 0,
                        byItem = 10,
                        byTarget1 = 0,
                        byTarget2 = 0,
                        byTarget3 = 0,
                        byTarget4 = 0,
                        byType = 16,
                        reserve = 0,
                    };
                    int.TryParse(SerialNo, out int tem);
                    byte[] bytes = new byte[2];
                    bytes[0] = (byte)(value & 0xFF);
                    bytes[1] = (byte)((value >> 8) & 0xFF);
                    PinnedObject pin = new PinnedObject(bytes);
                    uint error = 0;
                    int rc = NativeMethods.LJX8IF_SetSetting(tem, 1, targetSetting, pin.Pointer, 2, ref error);
                    if (rc == 0)
                        KeyenceHeight = value;
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog("基恩士:" + ex.Message, eMsgType.Error);
            }
        }

        public override bool StopCaptureImage()
        {
            StopGrab();
            return true;
        }
        /// <summary>设置增益 </summary>
        public override void SetGain(float value)
        {
            try
            {
                //Gain = value;
                //int nRet = MyCamera.MV_CC_SetFloatValue_NET("Gain", value);
                //if (nRet != MyCamera.MV_OK)
                //{
                //    Logger.AddLog("LMI:" + "Gain Err", eMsgType.Error);
                //}
            }
            catch (Exception ex)
            {
                Logger.AddLog("LMI:" + ex.Message, eMsgType.Error);
            }
        }
        /// <summary>设置曝光</summary>
        public override void SetExposureTime(float value)
        {
            ExposeTime = value;
            //int nRet = MyCamera.MV_CC_SetFloatValue_NET("ExposureTime", value);
            //if (nRet != MyCamera.MV_OK)
            //{
            //    Logger.AddLog("LMI:" + "ExposureTime Err", eMsgType.Error);
            //}
        }
        /// <summary>设置触发</summary>
        public override bool SetTriggerMode(eTrigMode mode)
        {
            return true;
        }
        #region 相机事件
        /// <summary>采集回调</summary>
        private void HighSpeedDataCallbackFunc(IntPtr buffer, uint size, uint count, uint notify, uint user)
        {
            // @Point
            // Take care to only implement storing profile data in a thread save buffer in the callback function.
            // As the thread used to call the callback function is the same as the thread used to receive data,
            // the processing time of the callback function affects the speed at which data is received,
            // and may stop communication from being performed properly in some environments.
            if ((notify != 0) && (notify & 0x10000) != 0) return;
            if (count == 0) return;
            if (GetImage) return;
            uint profileSize = (uint)(size / Marshal.SizeOf(typeof(int)));
            List<int[]> receiveBuffer = new List<int[]>();
            int[] bufferArray = new int[(int)(profileSize * count)];
            Marshal.Copy(buffer, bufferArray, 0, (int)(profileSize * count));
            // Profile data retention
            float[] height = new float[(profileSize - 7) / 2 * count];
            float[] gray = new float[(profileSize - 7) / 2 * count];

            for (int i = 0; i < count; i++)
            {
                // 当前剖面在目标数组中的起始位置
                long targetStart = i * (profileSize - 7) / 2;

                // 源数据起始位置
                long heightStart = i * profileSize + 6;
                long grayStart = heightStart + (profileSize - 7) / 2; // 修正：灰度数据在高度数据之后

                // 复制高度数据（整个剖面）
                Array.Copy(
                    sourceArray: bufferArray,
                    sourceIndex: heightStart,
                    destinationArray: height,
                    destinationIndex: targetStart,
                    length: (profileSize - 7) / 2);

                // 复制灰度数据（整个剖面）
                Array.Copy(
                    sourceArray: bufferArray,
                    sourceIndex: grayStart,
                    destinationArray: gray,
                    destinationIndex: targetStart,
                    length: (profileSize - 7) / 2);
            }
            var pointer1 = Marshal.UnsafeAddrOfPinnedArrayElement(height, 0);
            var pointer2 = Marshal.UnsafeAddrOfPinnedArrayElement(gray, 0);
            HImage hImage1 = new HImage("real", (int)(profileSize - 7) / 2, (int)count, pointer1);
            //hImage1.MinMaxGray(hImage1, 0, out HTuple min, out HTuple max, out HTuple Range);
            //HRegion region = hImage1.Threshold(min + 1, max);
            hImage1 = hImage1.ScaleImage(0.00001, 0);
            //hImage1 = hImage1.ReduceDomain(region);

            HImage hImage2 = new HImage("real", (int)(profileSize - 7) / 2, (int)count, pointer2);
            hImage2.MinMaxGray(hImage2, 0, out HTuple min, out HTuple max, out HTuple Range);
            hImage2.GetDomain().GetRegionPoints(out HTuple rows, out HTuple cols);
            HTuple Grayval = hImage2.GetGrayval(rows, cols);
            if (Range.D != 0)
            {
                HTuple Grayvalbyte = min + Grayval * (255 / Range);
                hImage2 = hImage2.ConvertImageType("byte");
                hImage2.SetGrayval(rows, cols, Grayvalbyte);
            }
            hImage1 = hImage1.Compose2(hImage2);
            //HOperatorSet.WriteImage(hImage1, "tiff", 0, @"C:\Users\Administrator\Desktop\ai\CS\1.tiff");
            if (HImages == null)
                HImages = new Queue<HImage>();
            HImages.Enqueue(hImage1);
            GetImage = true;
            //if (ThreadSafeBuffer.GetBufferDataCount((int)user) + receiveBuffer.Count < Define.WriteDataSize)
            //{-
            //    ThreadSafeBuffer.Add((int)user, receiveBuffer, notify);
            //}
            //else
            //{
            //    _isBufferFull[(int)user] = true;
            //}
        }
        private void HighSpeedDataCallbackFuncForSimpleArray(IntPtr profileHeaderArray, IntPtr heightProfileArray, IntPtr luminanceProfileArray, uint luminanceEnable, uint profileDataCount, uint count, uint notify, uint user)
        {
            ushort zUnit = 0;
            int.TryParse(SerialNo, out int tem);
            NativeMethods.LJX8IF_GetZUnitSimpleArray(tem, ref zUnit);
            try
            {
                if ((notify != 0) && (notify & 0x10000) != 0) return;
                if (count == 0 || count!=ImageHeight) return;
                //if (_imageAvailable[user] == 1) return;
                var syncObject = new object();
                lock (syncObject)
                {
                    int bufferSize = (int)profileDataCount * (int)count;
                    float[] pointsZ = new float[bufferSize];
                    ushort[] buffer = new ushort[bufferSize];
                    ushort[] lBuffer = new ushort[bufferSize];
                    CopyUShort(heightProfileArray, buffer, bufferSize);

                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < profileDataCount; j++)
                        {
                            int index = (i * (int)profileDataCount) + j;
                            if (buffer[index] == 0)
                            {
                                pointsZ[index] = (float)Math.Min(-65535, Define.InvalidZValueum); // 确保不超过 ushort 最大值
                            }
                            else
                            {
                                // 计算并确保结果在 ushort 范围内
                                double tempValue =  ((buffer[index] - 32768.0) * GetZConvertFactorum(Remarks)) / 1000;
                                pointsZ[index] = (float)tempValue;
                            }


                            //pointsZ[index] = (float)(buffer[index] == 0 ? Define.InvalidZValueum : (buffer[index] - Define.CorrectZValue) * GetZConvertFactorum(Remarks) / 1000);//无符号整型转化um再转化为mm
                        }
                    }
                    var pointer = Marshal.UnsafeAddrOfPinnedArrayElement(pointsZ, 0);
                    HTuple min, max, Range;
                    HImage hImage = new HImage();
                    hImage = new HImage("real", (int)profileDataCount,(int)count, pointer);
                    //hImage = hImage.ConvertImageType("real");
                    //hImage.MinMaxGray(hImage, 0, out  min, out  max, out  Range);
                    //HRegion region = hImage.Threshold(min + 1, max);
                    //hImage = hImage.ReduceDomain(region);
                    //region.GetRegionPoints(out HTuple rows, out HTuple cols);
                    //HTuple Grayval = hImage.GetGrayval(rows, cols);
                    //Grayval = ((Grayval  - 32768.0) * GetZConvertFactorum(Remarks)) /1000;
                    //hImage.SetGrayval(rows, cols, Grayval);

                    //double scale = GetZConvertFactorum(Remarks);
                    //hImage = hImage.ScaleImage(scale, -32768.0);
                    //HeightImage = new Mat((int)count, (int)profileDataCount, MatType.CV_16U, pointer);


                    //Mat heightQue = new Mat(bitmaphei);
                    //ICogImage m = new CogImage16Range(bitmaphei);
                    //Dispatcher d=new Dispatcher();
                    //this.device_Keyence3DCameraDeviceView.myRecordDisplay.showimage(heightQue);
                    //this.display.Image = heightQue;
                    //if (IsManmual)
                    //HImageListQuque.Enqueue(HeightImage);
                    HImage hImage1 = new HImage();
                    if (luminanceEnable == 1)
                    {
                        //ImageContext context = new ImageContext();
                        //Heightcontext.xResolution = 1.0f;//转化为mm。
                        //Heightcontext.yResolution = 1.0f;
                        //Heightcontext.zResolution = 1.0f;
                        //Heightcontext.xOffset = 0.0f;//接收到点云数据X向补偿值单位为um，转化为mm
                        //Heightcontext.yOffset = 0.0f;//接收到点云数据Y向补偿值单位为um，转化为mm
                        //Heightcontext.zOffset = 0.0f;//接收到点云数据Z向补偿值单位为um，转化为mm
                        //亮度值,取值范围是0-1023
                        CopyUShort(luminanceProfileArray, lBuffer, bufferSize);
                        //测试保存数据
                        //Save2Tiff(outputFilePath4, lBuffer.ToList(), (int)count, (int)profileDataCount);
                        var pionter1 = Marshal.UnsafeAddrOfPinnedArrayElement(lBuffer, 0);
                        
                        hImage1 = new HImage("uint2", (int)profileDataCount, (int)count, pionter1);
                        hImage1.MinMaxGray(hImage1, 0, out    min, out  max, out  Range);
                        hImage1.GetDomain().GetRegionPoints(out HTuple rows, out HTuple cols);
                        HTuple Grayval = hImage1.GetGrayval(rows, cols);
                        if (Range.D != 0)
                        {
                            HTuple Grayvalbyte = min + Grayval * (255 / Range);
                            hImage1 = hImage1.ConvertImageType("byte");
                            hImage1.SetGrayval(rows, cols, Grayvalbyte);
                        }

                        hImage = hImage.Compose2(hImage1);


                        //HOperatorSet.WriteImage(hImage, "tiff", 0, @"C:\Users\Administrator\Desktop\ai\CS\1.tiff");



                    }
                    HImage hImage2 = new HImage();
                    hImage2 = hImage.Compose2(hImage1);
                    if (HImages == null)
                        HImages = new Queue<HImage>();
                    HImages.Enqueue(hImage2);

                    //int errCode = NativeMethods.LJX8IF_StopHighSpeedDataCommunication(tem);
                    // 标记已接收到数据，以防止超时  
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                System.Windows.Application.Current.Dispatcher.Invoke(() => { Logger.GetExceptionMsg(ex); });

            }
        }
        private static void CopyUShort(IntPtr source, ushort[] destination, int length)
        {
            var gch = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(destination, 0);
                var bytesToCopy = Marshal.SizeOf(destination[0]) * length;

                NativeMethods.CopyMemory(ptr, source, (UIntPtr)bytesToCopy);
            }
            finally
            {
                gch.Free();
            }
        }
        //private async Task MonitorGrabTimeout(CancellationToken token)
        //{
        //    try
        //    {
        //        await Task.Delay(5000, token); // 等待超时时间  
        //        int.TryParse(SerialNo, out int tem);
        //        if (!_isDataReceived && !_grabCancellationSource.IsCancellationRequested)
        //        {
        //            // 超时逻辑，例如取消操作或记录日志  
        //            Logger.AddLog("基恩士:采集超时" , eMsgType.Error);
        //            Start = true;
        //            Stop = false;
        //            _isGrabbing = false;
        //            NativeMethods.LJX8IF_StopMeasure(tem);
        //            int errCode = NativeMethods.LJX8IF_ClearMemory(tem);
        //            // 这里可以添加额外的超时处理逻辑，比如重置状态、抛出异常等  
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        // 操作被取消，通常不需要额外处理  
        //    }
        //}
        private static float GetZConvertFactorum(string sensorType)
        {
            float factor;
            switch (sensorType)
            {
                case"LJ_X8020":
                    factor = 0.4f;
                    break;
                case "LJ_X8060":
                    factor = 0.8f;
                    break;
                case "LJ_X8080":
                    factor = 1.6f;
                    break;
                case "LJ_X8200":
                    factor = 4.0f;
                    break;
                case "LJ_X8400":
                    factor = 8.0f;
                    break;
                case "LJ_X8900":
                    factor = 16.0f;
                    break;
                case "LJ_V7020K_B":
                    factor = 0.4f;
                    break;
                case "LJ_V7060K_B":
                    factor = 0.8f;
                    break;
                case "LJ_V7080B":
                    factor = 1.6f;
                    break;
                case "LJ_V7200B":
                    factor = 4.0f;
                    break;
                case "LJ_V7300B":
                    factor = 8.0f;
                    break;
                default:
                    factor = 0.0f;
                    break;
            }
            return factor;
        }
        /// <summary>断开连接时触发</summary>
        private void OnConnectionLost(object sender, EventArgs e)
        {
            // Close the MyCamera object.
            DisConnectDev();
        }
        #endregion
        
        //[OnSerializing()] 序列化之前
        //[OnSerialized()] 序列化之后
        //[OnDeserializing()] 反序列化之前
       
        public void FindCBySN(string Ctemp)
        {
            List<CameraInfoModel> mCamInfoList = new List<CameraInfoModel>();
            var camInfoList = Enum.GetValues(typeof(SensorType));
            // ch:创建设备列表 | en:Create Device List
            foreach (var item in camInfoList)
            {
                CameraInfoModel _camInfo = new CameraInfoModel()
                {
                    ExtInfo = item,
                    CameraIP = "192.168.200.1",
                    CamName = item.ToString(),
                    Connected = false,
                    SerialNO = item.ToString(),


                };

                mCamInfoList.Add(_camInfo);
            }
            for (int i = 0; i < mCamInfoList.Count; i++)
            {

                if (Ctemp == mCamInfoList[i].SerialNO)//判断是否等于指定相机序号
                {
                    ExtInfo = mCamInfoList[i].ExtInfo;
                    return;
                }
            }

            MessageView.Ins.MessageBoxShow("没有找当前到设备,请确认相机是否连接好!", eMsgType.Warn);
            return;
        }
        [OnDeserialized()] //反序列化之后
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (SerialNo == null || SerialNo == "")
            {
                return;
            }
            //FindCBySN(SerialNo);
            ConnectDev();

        }
        
    }
}
