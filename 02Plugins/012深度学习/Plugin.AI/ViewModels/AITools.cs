using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVDll;
using HV.Dialogs.Views;


namespace Plugin.Ai.ViewModels
{

    public class AI
    {
        public eAiClass eAi;
        public AI()
        {
            IsRunning = false;
            eAi = eAiClass.无;
        }
        public bool IsRunning { get; set; }
        public async Task<string> AiInit(eAiClass AiClass, string AiPath)
        {
            var view = LoadingView.Ins;
            view.LoadingShow("Ai初始化中");
            string error = "";

            try
            {
                await Task.Run(() =>
                {
                    switch (AiClass)
                    {
                        case eAiClass.CPU:
                            AI_Cpu = new EVDll.AI_CpuALL.AI_Cpu();
                            AI_Cpu.Init(AiPath, ref error);
                            if (error == "")
                            {
                                IsRunning = true;
                                eAi = AiClass;
                            }
                            else
                            {
                                IsRunning = false;
                                eAi = eAiClass.无;
                            }
                            break;

                        case eAiClass.GPU分割旧:
                            AI_GpuSeg = new EVDll.AI_GpuSeg();
                            AI_GpuSeg.Init(AiPath, ref error);
                            if (error == "")
                            {
                                IsRunning = true;
                                eAi = AiClass;
                            }
                            else
                            {
                                IsRunning = false;
                                eAi = eAiClass.无;
                            }
                            break;
                        case eAiClass.GPU目标检测新:
                            AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                            AI_Gpu_Detection.Init(AiPath, 2,ref error);
                            if (error == "")
                            {
                                IsRunning = true;
                                eAi = AiClass;
                            }
                            else
                            {
                                IsRunning = false;
                                eAi = eAiClass.无;
                            }
                            break;
                        case eAiClass.GPU分类新:
                            AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                            AI_Gpu_Detection.Init(AiPath, 1, ref error);
                            if (error == "")
                            {
                                IsRunning = true;
                                eAi = AiClass;
                            }
                            else
                            {
                                IsRunning = false;
                                eAi = eAiClass.无;
                            }
                            break;
                        default:
                            error = "未知的AI类别";
                            break;
                    }
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
            switch (eAi)
            {
                case eAiClass.CPU:
                    if(AI_Cpu == null)
                    {
                        AI_Cpu = new EVDll.AI_CpuALL.AI_Cpu();
                        IsRunning = false;
                        return;
                    }
                    eAi = eAiClass.无;
                    AI_Cpu.Close();
                    IsRunning = false;
                    break;
                case eAiClass.GPU分割旧:
                    if (AI_GpuSeg == null)
                    {
                        AI_GpuSeg = new EVDll.AI_GpuSeg();
                        IsRunning = false;
                        return;
                    }
                    eAi = eAiClass.无;
                    AI_GpuSeg.Close();
                    IsRunning = false;
                    break;
                case eAiClass.GPU目标检测新:
                    if (AI_Gpu_Detection == null)
                    {
                        AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                        IsRunning = false;
                        return;
                    }
                    eAi = eAiClass.无;
                    AI_Gpu_Detection.Close();
                    IsRunning = false;
                    break;
                case eAiClass.GPU分类新:
                    if (AI_Gpu_Detection == null)
                    {
                        AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                        IsRunning = false;
                        return;
                    }
                    eAi = eAiClass.无;
                    AI_Gpu_Detection.Close();
                    IsRunning = false;
                    break;
                default:
                    break;
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
        public IResult AiRun(HalconDotNet.HImage hImage, ref string erro, double CONF_THRESHOLD = 0.5, double NMS_THRESHOLD = 0.5)
        {
            HImageResult outputimage = new HImageResult();
            ResultListResult resultList = new ResultListResult();
            switch (eAi)
            {
                case eAiClass.CPU:

                    if (AI_Cpu == null)
                        AI_Cpu = new EVDll.AI_CpuALL.AI_Cpu();
                    //hImage.GetImageSize(out HalconDotNet.HTuple width, out HalconDotNet.HTuple height);
                    outputimage.Image = AI_Cpu.Run<HalconDotNet.HImage>(hImage, ref erro, CONF_THRESHOLD, NMS_THRESHOLD);
                    return  outputimage;
                case eAiClass.GPU分割旧:

                    if (AI_GpuSeg == null)
                        AI_GpuSeg = new EVDll.AI_GpuSeg();
                    //hImage.GetImageSize(out HalconDotNet.HTuple width, out HalconDotNet.HTuple height);
                    outputimage.Image  = AI_GpuSeg.Run(hImage, ref erro, CONF_THRESHOLD, NMS_THRESHOLD);
                    return outputimage;
                case eAiClass.GPU目标检测新:
                    if (AI_Gpu_Detection == null)
                        AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                    resultList.ResultList = AI_Gpu_Detection.Run(hImage, ref erro);
                    return resultList;
                case eAiClass.GPU分类新:
                    if (AI_Gpu_Detection == null)
                        AI_Gpu_Detection = new EVDll.AI_GpuALL.AI_Gpu_Detection();
                    resultList.ResultList = AI_Gpu_Detection.Run(hImage, ref erro);
                    return resultList;
                default:
                    break;
            }
            return outputimage;
        }
        public EVDll.AI_CpuALL.AI_Cpu AI_Cpu;
        public EVDll.AI_GpuSeg AI_GpuSeg;
        public EVDll.AI_GpuALL.AI_Gpu_Detection AI_Gpu_Detection;
        
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
