using HalconDotNet;
using MvCamCtrl.NET;
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
using Lmi3d.Zen;
using Lmi3d.GoSdk.Messages;
using Lmi3d.GoSdk;
using static Plugin.CameraLMI.LMICamera;

namespace Plugin.CameraLMI
{
    [Category("相机")]
    [DisplayName("LMI相机")]
    [Serializable]
    public class CameraLMI : CameraBase
    {
        [NonSerialized]
        private LMICamera MyCamera = new LMICamera();
        //public object ExtInfo
        //{
        //    set { CurDevice = (MyCamera.MV_CC_DEVICE_INFO)value; }
        //    get { return CurDevice; }
        //}
        // ch:用于保存图像的缓存 | en:Buffer for saving image
        //private UInt32 m_nBufSizeForSaveImage = 5120 * 5120 * 3 + 2048;
        //[NonSerialized]
        //private byte[] m_pBufForSaveImage = new byte[5120 * 5120 * 3 + 2048];

        public CameraLMI() : base() { }
        /// <summary>搜索相机</summary>
        public override List<CameraInfoModel> SearchCameras()
        {
            List<CameraInfoModel> mCamInfoList = new List<CameraInfoModel>();
            List<CamInfo> camInfoList = MyCamera.SelectCam();
            if (camInfoList .Count== 0)
            {
                MessageView.Ins.MessageBoxShow("查找设备失败", eMsgType.Warn);
                return mCamInfoList;
            }
           
            // ch:在窗体列表中显示设备名 | en:Display device name in the form list
            for (int i = 0; i < camInfoList.Count(); i++)
            {
                var temp = camInfoList[i];
                CameraInfoModel _camInfo = new CameraInfoModel()
                {
                    ExtInfo = temp,
                    CameraIP = "",
                    CamName = temp.Name,
                    Connected = false,
                    SerialNO = temp.ID.ToString(),
                    

                };
                


                mCamInfoList.Add(_camInfo);
            }
            return mCamInfoList;
        }

        /// <summary>连接相机</summary>
        public override void ConnectDev()
        {
            try
            {
                //base.ConnectDev();
                // 如果设备已经连接先断开
                //DisConnectDev();
                int nRet = -1;
                
                // ch:打开设备 | en:Open device
                if (MyCamera == null)
                {
                    MyCamera = new LMICamera();
                    if (null == MyCamera)
                    {
                        return;
                    }
                }
                if (null == ExtInfo) { return; }
                var curDevice = (CamInfo)ExtInfo;
                MyCamera.ConnectCam(curDevice);
                // ch:设置采集连续模式 | en:Set Continues Aquisition Mode
                //MyCamera.MV_CC_SetEnumValue_NET("AcquisitionMode", 2);// ch:工作在连续模式 | en:Acquisition On Continuous Mode
                // ch:注册回调函数 | en:Register image callback
                //ImageCallback = new BopixelCallBackFunc(ImageCallbackFunc);
                MyCamera.SetCallbackFunc(this);

                // ch:开启抓图 || en: start grab image
                //MyCamera.Start();

                Connected = true;
            }
            catch (Exception ex)
            {
                Connected = false;
            }
            base.ConnectDev();
        }
        /// <summary>断开相机</summary>
        public override void DisConnectDev()
        {
            if (Connected)
            {
                if (MyCamera.Close())
                {
                    Connected = false;
                }
            }
            base.DisConnectDev();
        }
        /// <summary>采集图像,是否手动采图</summary>
        public override bool CaptureImage(bool byHand)
        {
            try
            {
                
                if (byHand)
                {
                    if(Connected)
                        MyCamera.Start();

                }
                else
                {
                    if (Connected)
                        MyCamera.Start();
                }


                return true;
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
        public void GetSetting()
        {
            //int nRet = 0;
            //MyCamera.MVCC_ENUMVALUE stTriggerMode = new MyCamera.MVCC_ENUMVALUE();
            //MyCamera.MVCC_ENUMVALUE stTriggerSource = new MyCamera.MVCC_ENUMVALUE();
            //MyCamera.MVCC_ENUMVALUE stTriggerActivation = new MyCamera.MVCC_ENUMVALUE();
            //nRet = MyCamera.MV_CC_GetTriggerMode_NET(ref stTriggerMode);
            //nRet = MyCamera.MV_CC_GetTriggerSource_NET(ref stTriggerSource);
            //if (stTriggerMode.nCurValue == (uint)eTrigMode.内触发)
            //    TrigMode = eTrigMode.内触发;
            //else if (stTriggerSource.nCurValue == 7)//软触发
            //    TrigMode = eTrigMode.软触发;
            //else if (stTriggerSource.nCurValue == 0) //Line0 触发
            //{
            //    nRet += MyCamera.MV_CC_GetEnumValue_NET("TriggerActivation", ref stTriggerActivation);
            //    if (stTriggerActivation.nCurValue == 0)
            //        TrigMode = eTrigMode.上升沿;
            //    else
            //        TrigMode = eTrigMode.下降沿;
            //}

            //MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();
            //nRet = MyCamera.MV_CC_GetFloatValue_NET("ExposureTime", ref stParam);
            //ExposeTime = stParam.fCurValue;
            //MyCamera.MV_CC_GetFloatValue_NET("ResultingFrameRate", ref stParam);
            //Framerate = stParam.fCurValue.ToString();
            //base.GetSetting();
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
            if (Height > 100 & Height <= HeightMax)
            {

            }
        }
        /// <summary>设置图像高度 </summary>
        public override void SetImageHeight(int value)
        {
            try
            {
                MyCamera.SetImageHeight(value);
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
        public void ImageCallbackFunc(KObject data)
        {
            try
            {
                GoDataSet dataSet = (GoDataSet)data;
                HImage GrayImage = new HImage();
                HImage HeightImage = new HImage();
                for (UInt32 i = 0; i < dataSet.Count; i++)
                {
                    GoDataMsg dataObj = (GoDataMsg)dataSet.Get(i);
                    switch (dataObj.MessageType)
                    {
                        case GoDataMessageType.UniformSurface:
                            {
                                GoUniformSurfaceMsg surfaceMsg = (GoUniformSurfaceMsg)dataObj;
                                long width = surfaceMsg.Width;
                                long length = surfaceMsg.Length;
                                long bufferSize = width * length;
                                IntPtr bufferPointer = surfaceMsg.Data;

                                HImage hImage = new HImage();
                                HeightImage.GenImage1("int2", (int)surfaceMsg.Width, (int)surfaceMsg.Length, bufferPointer);
                                //hImage.WriteImage("tiff", 0, @".\himage.tiff");

                                //short[] ranges = new short[bufferSize];
                                //Marshal.Copy(bufferPointer, ranges, 0, ranges.Length);

                            }
                            break;
                        //case GoDataMessageType.SurfacePointCloud:
                        //    {
                        //        DataContext context = new DataContext();
                        //        GoSurfacePointCloudMsg surfaceMsg = (GoSurfacePointCloudMsg)dataObj;
                        //        context.xResolution = (double)surfaceMsg.XResolution / 1000000;
                        //        context.yResolution = (double)surfaceMsg.YResolution / 1000000;
                        //        context.zResolution = (double)surfaceMsg.ZResolution / 1000000;
                        //        context.xOffset = (double)surfaceMsg.XOffset / 1000;
                        //        context.yOffset = (double)surfaceMsg.YOffset / 1000;
                        //        context.zOffset = (double)surfaceMsg.ZOffset / 1000;
                        //        long surfacePointCount = surfaceMsg.Width * surfaceMsg.Length;
                        //        //Console.WriteLine("Surface Point Cloud received:");
                        //        //Console.WriteLine(" Buffer width: {0}", surfaceMsg.Width);
                        //        //Console.WriteLine(" Buffer length: {0}", surfaceMsg.Length);
                        //        GoPoints[] points = new GoPoints[surfacePointCount];
                        //        SurfacePoint[] surfaceBuffer = new SurfacePoint[surfacePointCount];
                        //        int structSize = Marshal.SizeOf(typeof(GoPoints));
                        //        IntPtr pointsPtr = surfaceMsg.Data;


                        //        HImage hImage = new HImage();
                        //        hImage.GenImage1("int2", (int)surfaceMsg.Width, (int)surfaceMsg.Length, pointsPtr);
                        //        hImage.WriteImage("tiff", 0, @".\himage.tiff");

                        //        for (UInt32 array = 0; array < surfacePointCount; ++array)
                        //        {
                        //            IntPtr incPtr = new IntPtr(pointsPtr.ToInt64() + array * structSize);
                        //            points[array] = (GoPoints)Marshal.PtrToStructure(incPtr, typeof(GoPoints));
                        //        }
                        //        for (UInt32 arrayIndex = 0; arrayIndex < surfacePointCount; ++arrayIndex)
                        //        {
                        //            if (points[arrayIndex].x != -32768)
                        //            {
                        //                surfaceBuffer[arrayIndex].x = context.xOffset + context.xResolution * points[arrayIndex].x;
                        //                surfaceBuffer[arrayIndex].y = context.yOffset + context.yResolution * points[arrayIndex].y;
                        //                surfaceBuffer[arrayIndex].z = context.zOffset + context.zResolution * points[arrayIndex].z;
                        //            }
                        //            else
                        //            {
                        //                surfaceBuffer[arrayIndex].x = -32768;
                        //                surfaceBuffer[arrayIndex].y = -32768;
                        //                surfaceBuffer[arrayIndex].z = -32768;
                        //            }
                        //        }

                        //    }
                        //    break;
                        case GoDataMessageType.SurfaceIntensity:
                            {
                                GoSurfaceIntensityMsg surfaceMsg = (GoSurfaceIntensityMsg)dataObj;
                                long width = surfaceMsg.Width;
                                long length = surfaceMsg.Length;
                                long bufferSize = width * length;
                                IntPtr bufferPointer = surfaceMsg.Data;
                                HImage hImage = new HImage();
                                GrayImage.GenImage1("byte", (int)width, (int)length, bufferPointer);
                                //hImage.WriteImage("jpeg", 0, @".\himage.jpg");

                            }
                            break;
                    }
                }
                HOperatorSet.Compose2(HeightImage, GrayImage, out HObject multImage);
                DispImage = new HImage(multImage);
                //DispImage = (HImage)multImage;
                //DispImage.GrayImage = GrayImage;
                EventWait.Set();
                //ImageGrab?.Invoke(Image);
                MyCamera.Stop();
            }
            catch (Exception ex)
            {
                EventWait.Set();
            }
        }
        //private Boolean IsMonoData(MyCamera.MvGvspPixelType enGvspPixelType)
        //{
        //    switch (enGvspPixelType)
        //    {
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
        //            return true;

        //        default:
        //            return false;
        //    }
        //}

        //private Boolean IsColorData(MyCamera.MvGvspPixelType enGvspPixelType)
        //{
        //    switch (enGvspPixelType)
        //    {
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR8:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG8:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
        //        case MyCamera.MvGvspPixelType.PixelType_Gvsp_YCBCR411_8_CBYYCRYY:
        //            return true;

        //        default:
        //            return false;
        //    }
        //}

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
        [OnDeserialized()] //反序列化之后

        internal void OnDeserializedMethod(StreamingContext context)
        {
            MyCamera = new LMICamera();
            if (SerialNo == null || SerialNo == "")
            {
                return;
            }
            //m_pBufForSaveImage = new byte[5120 * 5120 * 3 + 2048];
            //FindCBySN(SerialNo);
            //ConnectDev();
        }
        
    }
}
