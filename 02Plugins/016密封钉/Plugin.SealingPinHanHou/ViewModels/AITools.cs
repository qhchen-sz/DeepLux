using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVDll;
using HV.Dialogs.Views;


namespace Plugin.SealingPinHanHou.ViewModels
{

    public class AI
    {
        public AI()
        {
            IsRunning = false;

        }
        public bool IsRunning { get; set; }
        public async Task<string> AiInit( string AiPath1,string AiPath2)
        {
            var view = LoadingView.Ins;
            view.LoadingShow("Ai初始化中");
            string error1 = "";
            string error2 = "";
            //AI_GpuSeg = new EVDll.AI_GpuSeg();
            //AI_GpuSeg.Init(AiPath, ref error);
            try
            {
                await Task.Run(() =>
                {
                    AI_GpuSeg = new EVDll.AI_GpuSeg();
                    Gpu_Detection =  new EVDll.AI_GpuALL.AI_Gpu_Detection(); 
                    AI_GpuSeg.Init(AiPath1, ref error1);
                    Gpu_Detection.Init(AiPath2, 2,ref error2);
                    if (error1 == "" && error2 =="")
                    {
                        IsRunning = true;
                    }
                    else
                    {
                        IsRunning = false;
                    }
                });
            }
            catch (Exception ex)
            {
                error1 = ex.Message;
                error2 = ex.Message;
                IsRunning = false;
            }
            finally
            {
                view.CloseWindows();
            }

            return error1+ error2;
        }
        public void AiClose( ref string erro)
        {
            if (AI_GpuSeg == null)
            {
                AI_GpuSeg = new EVDll.AI_GpuSeg();
            }
            else
            {
                AI_GpuSeg.Close();
            }
            if (Gpu_Detection == null)
            {
                AI_GpuSeg = new EVDll.AI_GpuSeg();
            }
            else
            {
                Gpu_Detection.Close();
            }
            IsRunning = false;
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
        public HalconDotNet.HImage AiRun(HalconDotNet.HImage hImage, ref string erro, double CONF_THRESHOLD = 0.5, double NMS_THRESHOLD = 0.5)
        {
            if (AI_GpuSeg == null)
                AI_GpuSeg = new EVDll.AI_GpuSeg();
            return AI_GpuSeg.Run(hImage, ref erro, CONF_THRESHOLD, NMS_THRESHOLD);
        }
        public EVDll.AI_GpuALL.AI_GpuALL.ResultList AiRunDetection(HalconDotNet.HImage hImage, ref string erro, double CONF_THRESHOLD = 0.5, double NMS_THRESHOLD = 0.5)
        {
            if (Gpu_Detection == null)
                Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
            
            return Gpu_Detection.Run(hImage, ref erro);
        }
        public EVDll.AI_CpuALL.AI_Cpu AI_Cpu;
        public EVDll.AI_GpuSeg AI_GpuSeg;
        public EVDll.AI_GpuALL.AI_Gpu_Detection Gpu_Detection;
        
    }
    public interface IResult { }

    public class HImageResult : IResult
    {
        public HalconDotNet.HImage Image { get; set; }
    }

    public class ResultListResult : IResult
    {
        public EVDll.AI_GpuALL.AI_GpuALL.ResultList ResultList { get; set; }
    }
}
