using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using HV.Dialogs.Views;
using OpenCvSharp;
using YoloSDK;


namespace Plugin.Yolo.ViewModels
{
    [Serializable]
    public class AI
    {
        public eAiClass eAi;
        [NonSerialized]
        private Yolov11 yOLO;
        public AI()
        {
            IsRunning = false;
            eAi = eAiClass.无;
        }
        public bool IsRunning { get; set; }
        public async Task<string> AiInit(eAiClass AiClass, string AiPath)
        {
            var view = LoadingView.Ins;
            view.LoadingShow("Yolo初始化中");
            string error = "";

            try
            {
                
                await Task.Run(() =>
                {
                    List<string> customClasses = new List<string> { "baiban", "qipao", "zazhi", "liewen", "huahen" };
                    //yOLO = new Yolov11(AiPath,
                    //    inputSize: new int[2] { 1024, 1024 },
                    //        useCuda: true,
                    //        sliceOverlapRatio: 0f,
                    //        iouThreshold: 0.5f,
                    //        iosThreshold: 0.5f,
                    //        confidenceThreshold: new float[5] { 0.05f, 0.2f, 0.2f, 0.2f, 0.05f },
                    //        sliceHeight: 512,
                    //        sliceWidth: 512,
                    //        showLabel: true,
                    //        customClasses: customClasses,
                    //        batchSize: 2,
                    //        useEnhanceNMS: true
                    //    );
                    yOLO = new Yolov11(AiPath,
                            inputSize: new int[2] { 640, 640 },
                            useCuda: true,
                            sliceOverlapRatio: 0f,
                            iouThreshold: 0.5f,
                            iosThreshold: 0.5f,
                            confidenceThreshold: new float[5] { 0.05f, 0.1f, 0.05f, 0.1f, 0.25f },
                            sliceHeight: 256,
                            sliceWidth: 256,
                            showLabel: true,
                            customClasses: customClasses,
                            batchSize: 1,
                            useEnhanceNMS: true
                            );


                    IsRunning = true;
                    //switch (AiClass)
                    //{
                    //    case eAiClass.CPU:
                    //        AI_Cpu = new EVDll.AI_CpuALL.AI_Cpu();
                    //        AI_Cpu.Init(AiPath, ref error);
                    //        if (error == "")
                    //        {
                    //            IsRunning = true;
                    //            eAi = AiClass;
                    //        }
                    //        else
                    //        {
                    //            IsRunning = false;
                    //            eAi = eAiClass.无;
                    //        }
                    //        break;

                    //    case eAiClass.GPU分割旧:
                    //        AI_GpuSeg = new EVDll.AI_GpuSeg();
                    //        AI_GpuSeg.Init(AiPath, ref error);
                    //        if (error == "")
                    //        {
                    //            IsRunning = true;
                    //            eAi = AiClass;
                    //        }
                    //        else
                    //        {
                    //            IsRunning = false;
                    //            eAi = eAiClass.无;
                    //        }
                    //        break;
                    //    case eAiClass.GPU目标检测新:
                    //        Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu();
                    //        Gpu_Detection.Init(AiPath, 2,ref error);
                    //        if (error == "")
                    //        {
                    //            IsRunning = true;
                    //            eAi = AiClass;
                    //        }
                    //        else
                    //        {
                    //            IsRunning = false;
                    //            eAi = eAiClass.无;
                    //        }
                    //        break;
                    //    case eAiClass.GPU分类新:
                    //        Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu();
                    //        Gpu_Detection.Init(AiPath, 1, ref error);
                    //        if (error == "")
                    //        {
                    //            IsRunning = true;
                    //            eAi = AiClass;
                    //        }
                    //        else
                    //        {
                    //            IsRunning = false;
                    //            eAi = eAiClass.无;
                    //        }
                    //        break;
                    //    default:
                    //        error = "未知的AI类别";
                    //        break;
                    //}
                });
            }
            catch (Exception ex)
            {
                error = ex.Message;
                IsRunning = false;
                eAi = eAiClass.无;
            }
            finally
            {
                view.CloseWindows();
            }

            return error;
        }
        public void AiClose( ref string erro)
        {
            try
            {
                if (IsRunning && yOLO!=null)
                {

                    yOLO.CloseModel();
                    IsRunning=false;
                }

            }
            catch (Exception e)
            {
                
            }
        }
        //public T AiRun <T> (HalconDotNet.HImage hImage, ref string erro,double CONF_THRESHOLD = 0.5,double NMS_THRESHOLD = 0.5)
        //{
        //    HalconDotNet.HImage outputimage = new HalconDotNet.HImage();
        //    EVDll.AI_GpuALL.AI_GpuALL.ResultList resultList = new EVDll.AI_GpuALL.AI_GpuALL.ResultList();
        //    switch (eAi)
        //    {
        //        case eAiClass.CPU:

        //                if (AI_Cpu == null)
        //                    AI_Cpu = new EVDll.AI_CpuALL.AI_Cpu();
        //                //hImage.GetImageSize(out HalconDotNet.HTuple width, out HalconDotNet.HTuple height);
        //                outputimage = AI_Cpu.Run<HalconDotNet.HImage>(hImage, ref erro, CONF_THRESHOLD, NMS_THRESHOLD);
        //             return (T)(object)outputimage;
        //        case eAiClass.GPU分割旧:

        //            if (AI_GpuSeg == null)
        //                AI_GpuSeg = new EVDll.AI_GpuSeg();
        //            //hImage.GetImageSize(out HalconDotNet.HTuple width, out HalconDotNet.HTuple height);
        //            outputimage = AI_GpuSeg.Run(hImage, ref erro, CONF_THRESHOLD, NMS_THRESHOLD);
        //            return (T)(object)outputimage;
        //        case eAiClass.GPU目标检测新:
        //            if (Gpu_Detection == null)
        //                Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu();
        //            resultList = Gpu_Detection.Run(hImage, ref erro);
        //            return (T)(object)resultList;
        //        default:
        //            break;
        //    }
        //    return default(T);
        //}
        public List<DetectionResult> AiRun(HImage hImage, ref string erro, double CONF_THRESHOLD = 0.5, double NMS_THRESHOLD = 0.5)
        {
            List<DetectionResult> dL_RESULTs = new List<DetectionResult>();
            try
            {
                double tem;
                //Mat image = new Mat(@"D:\蜂巢\yoloSDK4.8(1)\yoloSDKTest\images\zidane.jpg");
                (dL_RESULTs, tem) = yOLO.DetectImage(HImageToMat(hImage));
                //dL_RESULTs = yOLO.RunSession(image);
            }
            catch (Exception)
            {

                
            }

            return dL_RESULTs;
        }
        public static Mat HImageToMat(HImage hImage)
        {
            IntPtr ptr = hImage.GetImagePointer1(out string type, out int width, out int height);

            if (type == "byte")
            {
                byte[] buffer = new byte[width * height];
                Marshal.Copy(ptr, buffer, 0, buffer.Length);

                // 固定数组，获取指针
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    IntPtr bufferPtr = handle.AddrOfPinnedObject();
                    return new Mat(height, width, MatType.CV_8UC1, bufferPtr);
                }
                finally
                {
                    handle.Free();
                }
            }
            else
            {
                hImage.GetImagePointer3(out IntPtr rPtr, out IntPtr gPtr, out IntPtr bPtr, out string _type, out int w, out int h);
                byte[] r = new byte[w * h];
                byte[] g = new byte[w * h];
                byte[] b = new byte[w * h];
                Marshal.Copy(rPtr, r, 0, r.Length);
                Marshal.Copy(gPtr, g, 0, g.Length);
                Marshal.Copy(bPtr, b, 0, b.Length);

                byte[] bgr = new byte[w * h * 3];
                for (int i = 0; i < w * h; i++)
                {
                    bgr[i * 3 + 0] = b[i];
                    bgr[i * 3 + 1] = g[i];
                    bgr[i * 3 + 2] = r[i];
                }

                var handle = GCHandle.Alloc(bgr, GCHandleType.Pinned);
                try
                {
                    IntPtr bgrPtr = handle.AddrOfPinnedObject();
                    return new Mat(h, w, MatType.CV_8UC3, bgrPtr);
                }
                finally
                {
                    handle.Free();
                }
            }
        }


    }

}
