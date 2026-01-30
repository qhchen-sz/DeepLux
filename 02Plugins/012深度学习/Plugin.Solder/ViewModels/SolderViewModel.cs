using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.Solder.Views;
using Plugin.GrabImage.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using System.Windows.Media;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views.Dock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using EVDll;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Plugin.Solder.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        CenterY,
        Scale,//像素当量
    }
    public enum eOperateCommand
    {
        StartLearn,
        Edit,
        EndLearn,
        Cancel
    }
    public enum eLocationClass
    {
        Right,
        Left,
        Center
    }
    public enum eAiColor
    {
        red=0,
        blue = 1,
        green =2,
        cyan = 3,
        yellow = 4,
        coral = 5,
        orange = 6,
        pink = 7,

    }
    #endregion

    [Category("深度学习")]
    [DisplayName("极耳焊算法")]
    [ModuleImageName("Solder")]
    [Serializable]
    public class SolderViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }
            if (InputImageLinkText == null)
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }
        public override bool ExeModule()
        {
            Stopwatch.Restart();

            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText);
                string erro = "";
                if (ClassAi == null || !ClassAi.IsRunning)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                SolderViewModel aiViewModel = this;

                // 修复1: 正确初始化 OutRegion
                if (OutRegion == null || !OutRegion.IsInitialized())
                {
                    OutRegion = new HRegion();
                    OutRegion.GenEmptyObj();
                    Logger.AddLog("错误: AI返回结果无效", eMsgType.Info);
                }
                else
                {
                    OutRegion.GenEmptyObj(); // 清空现有内容而不是重新创建
                }
                HImage Result = ClassAi.AiRun(DispImage, ref erro, this.CONF_THRESHOLD, this.NMS_THRESHOLD);

                HRegion region1 = new HRegion();
                HRegion region2 = new HRegion();
               // HRegion region3 = new HRegion();
               // HRegion region4 = new HRegion();

                if (IsDispAiRegion)
                {
                 
                    region1 = Result.Threshold(1.0, 1.0);
                    region2 = Result.Threshold(2.0, 2.0);
                   // region3 = Result.Threshold(3.0, 3.0);
                  //  region4 = Result.Threshold(4.0, 4.0);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai1", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)2).ToString(), new HObject(region1),true));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai2", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)3).ToString(), new HObject(region2), true));
                   // ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai3", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)4).ToString(), new HObject(region3), true));
                   // ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "ai4", ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)5).ToString(), new HObject(region4), true));
                }
                else
                {
                    // 即使不显示也要生成region1用于输出
                    
                    region1 = Result.Threshold(1.0, 1.0);
                }
                // 修复：将region1设置为输出区域
                if (region1 != null && region1.IsInitialized())
                {
                    // 方法1：直接赋值（推荐）
                    OutRegion = new HRegion(region1);                                           
                }
                else
                {
                    OutRegion.GenEmptyObj();
                }



                Algorithm.jierhansuanfa(Result, out HObject ho_Region1, out HObject ho_Region2,out HObject ho_ho_CrossPreUpper, out HObject ho_ho_CrossPreLower, out HObject ho_ho_CrossFinalUpper,
               out HObject ho_ho_CrossFinalLower, out HObject ho_ho_CrossPreLeft, out HObject ho_ho_CrossPreRight,out HObject ho_ho_CrossFinalLeft, out HObject ho_ho_CrossFinalRight, out HObject ho_ho_UpperGapLine,
               out HObject ho_ho_LowerGapLine, out HObject ho_ho_LeftGapLine, out HObject ho_ho_RightGapLine, out HObject ho_ho_Preweld_Contour, out HObject ho_ho_Finalweld_Contour, out HTuple hv_Offset,
               out HTuple hv_UpperGaps, out HTuple hv_LowerGaps, out HTuple hv_RightGaps, out HTuple hv_LiftGaps,out HTuple hv_AreaP_Tuple, out HTuple hv_AreaF_Tuple, out HTuple hv_RowP_Tuple,
               out HTuple hv_ColumnP_Tuple, out HTuple hv_RowF_Tuple, out HTuple hv_ColumnF_Tuple, out HTuple hv_WidthP_Tuple, out HTuple hv_HeightP_Tuple, out HTuple hv_WidthF_Tuple,
               out HTuple hv_HeightF_Tuple);
                    //PianWei = 0;
                    //List<double> Distance1 = new List<double>();

                // 像素当量 
                double.TryParse(GetLinkValue(Scale).ToString(), out double SS);


                // 安全处理 AreaP_Tuple
                // 安全处理 AreaP_Tuple
                if (hv_AreaP_Tuple != null && hv_AreaP_Tuple.Length >= 2)
                {
                    try
                    {
                        // 使用 .D 属性获取 double 值
                        double area1 = hv_AreaP_Tuple[0].D; //储预焊区域面积
                        double area2 = hv_AreaP_Tuple[1].D;
                        double area3 = hv_AreaF_Tuple[0].D;//存储终焊区域面积
                        double area4 = hv_AreaF_Tuple[1].D;

                        // 直接赋值给类的属性
                        #region 中心坐标赋值
                        double RowP_Tuple1 = hv_RowP_Tuple[0].D;
                        double ColumnP_Tuple1 = hv_ColumnP_Tuple[0].D;
                        double RowF_Tuple1 = hv_RowF_Tuple[0].D;
                        double ColumnF_Tuple1 = hv_ColumnF_Tuple[0].D;

                        double RowP_Tuple2 = hv_RowP_Tuple[1].D;
                        double ColumnP_Tuple2 = hv_ColumnP_Tuple[1].D;
                        double RowF_Tuple2 = hv_RowF_Tuple[1].D;
                        double ColumnF_Tuple2 = hv_ColumnF_Tuple[1].D;
                        #endregion

                      

                        // 如果需要像素当量换算
                        area1 = area1 * SS * SS;  // 面积需要乘以比例的平方
                        area2 = area2 * SS * SS;
                        area3 = area3 * SS * SS;
                        area4 = area4 * SS * SS;

                        double WidthP1 = hv_WidthP_Tuple[0].D * SS;
                        double HeightP1 = hv_HeightP_Tuple[0].D * SS;
                        double WidthF1 = hv_WidthF_Tuple[0].D * SS;
                        double HeightF1 = hv_HeightF_Tuple[0].D * SS;

                        double WidthP2 = hv_WidthP_Tuple[1].D * SS;
                        double HeightP2 = hv_HeightP_Tuple[1].D * SS;
                        double WidthF2 = hv_WidthF_Tuple[1].D * SS;
                        double HeightF2 = hv_HeightF_Tuple[1].D * SS;

                        // 将计算后的值赋给类的属性
                        AreaP_Tuple1 = Math.Round(area1, 3);
                        AreaP_Tuple2 = Math.Round(area3, 3);
                        AreaF_Tuple1 = Math.Round(area3, 3);
                        AreaF_Tuple2 = Math.Round(area4, 3);

                        WidthP_Tuple1 = Math.Round(WidthP1, 3);
                        HeightP_Tuple1 = Math.Round(HeightP1, 3);
                        WidthF_Tuple1 = Math.Round(WidthF1, 3);
                        HeightF_Tuple1 = Math.Round(HeightF1, 3);

                        WidthP_Tuple2 = Math.Round(WidthP2, 3);
                        HeightP_Tuple2 = Math.Round(HeightP2, 3);
                        WidthF_Tuple2 = Math.Round(WidthF2, 3);
                        HeightF_Tuple2 = Math.Round(HeightF2, 3);

                        // 区域中心行坐标
                        RowP_Tuple1 = Math.Round(RowP_Tuple1, 3);
                        ColumnP_Tuple1 = Math.Round(ColumnP_Tuple1, 3);
                        RowF_Tuple1 = Math.Round(RowF_Tuple1, 3);
                        ColumnF_Tuple1 = Math.Round(ColumnF_Tuple1, 3);

                        RowP_Tuple2 = Math.Round(RowP_Tuple2, 3);
                        ColumnP_Tuple2 = Math.Round(ColumnP_Tuple2, 3);
                        RowF_Tuple2 = Math.Round(RowF_Tuple2, 3);
                        ColumnF_Tuple2 = Math.Round(ColumnF_Tuple2, 3);

                        // 将间隙数据赋值给属性
                        UpperGaps1 = Math.Round((hv_UpperGaps[0].D) * SS, 3);
                        UpperGaps2 = Math.Round((hv_UpperGaps[1].D) * SS, 3);
                        UpperGaps3 = Math.Round((hv_UpperGaps[2].D) * SS, 3);
                        LowerGaps1 = Math.Round((hv_LowerGaps[0].D) * SS, 3);
                        LowerGaps2 = Math.Round((hv_LowerGaps[1].D) * SS, 3);
                        LowerGaps3 = Math.Round((hv_LowerGaps[2].D) * SS, 3);

                        UpperGaps4 = Math.Round((hv_UpperGaps[3].D) * SS, 3);
                        UpperGaps5 = Math.Round((hv_UpperGaps[4].D) * SS, 3);
                        UpperGaps6 = Math.Round((hv_UpperGaps[5].D) * SS, 3);
                        LowerGaps4 = Math.Round((hv_LowerGaps[3].D) * SS, 3);
                        LowerGaps5 = Math.Round((hv_LowerGaps[4].D) * SS, 3);
                        LowerGaps6 = Math.Round((hv_LowerGaps[5].D) * SS, 3);
                    }
                    catch (Exception ex)
                    {
                        // 设置默认值
                        AreaP_Tuple1 = 0;
                        AreaP_Tuple2 = 0;
                        AreaF_Tuple1 = 0;
                        AreaF_Tuple2 = 0;

                        RowP_Tuple1 = 0;
                        ColumnP_Tuple1 = 0;
                        RowF_Tuple1 = 0;
                        ColumnF_Tuple1 = 0;
                        RowP_Tuple2 = 0;
                        ColumnP_Tuple2 = 0;
                        RowF_Tuple2 = 0;
                        ColumnF_Tuple2 = 0;

                        Logger.AddLog($"计算过程中出现异常: {ex.Message}", eMsgType.Error);
                    }
                }

                // 显示其他区域
                if (IsFindRegions)
                {
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 1, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_Preweld_Contour)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 2, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_Finalweld_Contour)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 3, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossPreUpper)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 4, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossPreLower)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 5, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossFinalUpper)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 6, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossFinalLower)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 7, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_UpperGapLine)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 8, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_LowerGapLine)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 9, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossPreLeft)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 10, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossPreRight)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 11, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossFinalLeft)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 12, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_CrossFinalRight)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 13, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_LeftGapLine)));
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + 14, ModuleParam.Remarks, HRoiType.检测结果, eAiColor.blue.ToString(), new HObject(ho_ho_RightGapLine)));

                }
                
                ShowHRoi();
                
                ChangeModuleRunStatus(eRunStatus.OK);
                // 清理资源
                if (OutputImage != null)
                {
                    OutputImage.Dispose();
                    OutputImage = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                OutRegion.Dispose();
                if (OutputImage != null)
                    OutputImage.Dispose();
                return false;
            }
            finally
            {
                if (OutputImage != null)
                {
                    OutputImage.Dispose();
                    OutputImage = null;
                }
            }
        }
        public override void InitModule()
        {
            base.Init();
            DeviceStatic = false;
            if (IsOpen)
            {

                string erro = "";
                if (ClassAi == null)
                    ClassAi = new AI();

                if (ClassAi.IsRunning)
                {
                    ClassAi.AiClose(ref erro);
                    DeviceStatic = false;
                }
                else
                {
                    erro = ClassAi.AiInit(this.AiPath).ToString();
                    DeviceStatic = true;
                }

                if (!string.IsNullOrEmpty(erro))
                {
                    // 处理错误，例如显示错误消息
                    //MessageBox.Show(erro, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            // 修复: 确保 OutRegion 有效
            if (OutRegion == null || !OutRegion.IsInitialized())
            {
                OutRegion = new HRegion();
                OutRegion.GenEmptyObj();
            }
            // 输出 region1 区域
            AddOutputParam("区域", "HRegion", OutRegion); 


            try
            {
                //区域面积
                AddOutputParam("预焊面积1", "double", AreaP_Tuple1);
                AddOutputParam("预焊面积2", "double", AreaP_Tuple2);
                AddOutputParam("终焊面积1", "double", AreaF_Tuple1);
                AddOutputParam("终焊面积2", "double", AreaF_Tuple2);

                //区域宽度 
                AddOutputParam("预焊1.宽度", "double", WidthP_Tuple1);
                AddOutputParam("预焊1.高度", "double", HeightP_Tuple1);
                AddOutputParam("终焊1.宽度", "double", WidthF_Tuple1);
                AddOutputParam("终焊1.高度", "double", HeightF_Tuple1);

                AddOutputParam("预焊2.宽度", "double", WidthP_Tuple2);
                AddOutputParam("预焊2.高度", "double", HeightP_Tuple2);
                AddOutputParam("终焊2.宽度", "double", WidthF_Tuple2);
                AddOutputParam("终焊2.高度", "double", HeightF_Tuple2);

                //中心坐标
                AddOutputParam("预焊1.X", "double", RowP_Tuple1);
                AddOutputParam("预焊1.Y", "double", ColumnP_Tuple1);
                AddOutputParam("终焊1.X", "double", RowF_Tuple1);
                AddOutputParam("终焊1.Y", "double", ColumnF_Tuple1);

                AddOutputParam("预焊2.X", "double", RowP_Tuple2);
                AddOutputParam("预焊2.Y", "double", ColumnP_Tuple2);
                AddOutputParam("终焊2.X", "double", RowF_Tuple2);
                AddOutputParam("终焊2.Y", "double", ColumnF_Tuple2);

                //间隙
                AddOutputParam("上端1.中间", "double", UpperGaps1);
                AddOutputParam("上端1.左测", "double", UpperGaps2);
                AddOutputParam("上端1.右侧", "double", UpperGaps3);
                AddOutputParam("下端1.中间", "double", LowerGaps1);
                AddOutputParam("下端1.左侧", "double", LowerGaps2);
                AddOutputParam("下端1.右侧", "double", LowerGaps3);

                AddOutputParam("上端2.中间", "double", UpperGaps4);
                AddOutputParam("上端2.左测", "double", UpperGaps5);
                AddOutputParam("上端2.右侧", "double", UpperGaps6);
                AddOutputParam("下端2.中间", "double", LowerGaps4);
                AddOutputParam("下端2.左侧", "double", LowerGaps5);
                AddOutputParam("下端2.右侧", "double", LowerGaps6);

            }
            catch (Exception)
            {

                // 设置默认输出值
                Logger.AddLog($"在添加输出参数时发生异常：", eMsgType.Error);
            }
        }

        #region Prop
        [NonSerialized]
        int ChannelNum = 1;
        [NonSerialized]
        private HRegion _ReduceRegion;
        [NonSerialized]
        public HImage OutputImage;
        private bool _DeviceStatic =false;
        private string _AiPath ="";
        private double cONF_THRESHOLD = 0.5, nMS_THRESHOLD = 0.5;
        private int _DefectNum = 4;
        private eLocationClass _SelectLocation = eLocationClass.Center;

        //区域面积
        #region
        private double _AreaP_Tuple1, _AreaP_Tuple2, _AreaF_Tuple1,_AreaF_Tuple2;
        public double AreaP_Tuple1
        {
            get { return _AreaP_Tuple1; }
            set { _AreaP_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double AreaP_Tuple2
        {
            get { return _AreaP_Tuple2; }
            set { _AreaP_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double AreaF_Tuple1
        {
            get { return _AreaF_Tuple1; }
            set { _AreaF_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double AreaF_Tuple2
        {
            get { return _AreaF_Tuple2; }
            set { _AreaF_Tuple2 = value; RaisePropertyChanged(); }
        }
        #endregion
        //区域区域宽度
        #region
        private double _WidthP_Tuple1, _HeightP_Tuple1, _WidthF_Tuple1, _HeightF_Tuple1, _WidthP_Tuple2, _HeightP_Tuple2, _WidthF_Tuple2, _HeightF_Tuple2;
        public double WidthP_Tuple1
        {
            get { return _WidthP_Tuple1; }
            set { _WidthP_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double HeightP_Tuple1
        {
            get { return _HeightP_Tuple1; }
            set { _HeightP_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double WidthF_Tuple1
        {
            get { return _WidthF_Tuple1; }
            set { _WidthF_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double HeightF_Tuple1
        {
            get { return _HeightF_Tuple1; }
            set { _HeightF_Tuple1 = value; RaisePropertyChanged(); }
        }

        public double WidthP_Tuple2
        {
            get { return _WidthP_Tuple2; }
            set { _WidthP_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double HeightP_Tuple2
        {
            get { return _HeightP_Tuple2; }
            set { _HeightP_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double WidthF_Tuple2
        {
            get { return _WidthF_Tuple2; }
            set { _WidthF_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double HeightF_Tuple2
        {
            get { return _HeightF_Tuple2; }
            set { _HeightF_Tuple2 = value; RaisePropertyChanged(); }
        }
        #endregion

        //输出区域中心行坐标
        #region
        private double _RowP_Tuple1, _ColumnP_Tuple1, _RowF_Tuple1, _ColumnF_Tuple1, _RowP_Tuple2, _ColumnP_Tuple2, _RowF_Tuple2, _ColumnF_Tuple2;

        public double RowP_Tuple1
        {
            get { return _RowP_Tuple1; }
            set { _RowP_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double ColumnP_Tuple1
        {
            get { return _ColumnP_Tuple1; }
            set { _ColumnP_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double RowF_Tuple1
        {
            get { return _RowF_Tuple1; }
            set { _RowF_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double ColumnF_Tuple1
        {
            get { return _ColumnF_Tuple1; }
            set { _ColumnF_Tuple1 = value; RaisePropertyChanged(); }
        }
        public double RowP_Tuple2
        {
            get { return _RowP_Tuple2; }
            set { _RowP_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double ColumnP_Tuple2
        {
            get { return _ColumnP_Tuple2; }
            set { _ColumnP_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double RowF_Tuple2
        {
            get { return _RowF_Tuple2; }
            set { _RowF_Tuple2 = value; RaisePropertyChanged(); }
        }
        public double ColumnF_Tuple2
        {
            get { return _ColumnF_Tuple2; }
            set { _ColumnF_Tuple2 = value; RaisePropertyChanged(); }
        }

        #endregion

        //间隙
        #region
        private double _UpperGaps1, _UpperGaps2, _UpperGaps3, _UpperGaps4, _UpperGaps5, _UpperGaps6;
        public double UpperGaps1
        {
            get { return _UpperGaps1; }
            set { _UpperGaps1 = value; RaisePropertyChanged(); }
        }
        public double UpperGaps2
        {
            get { return _UpperGaps2; }
            set { _UpperGaps2 = value; RaisePropertyChanged(); }
        }
        public double UpperGaps3
        {
            get { return _UpperGaps3; }
            set { _UpperGaps3 = value; RaisePropertyChanged(); }
        }
        public double UpperGaps4
        {
            get { return _UpperGaps4; }
            set { _UpperGaps4 = value; RaisePropertyChanged(); }
        }
        public double UpperGaps5
        {
            get { return _UpperGaps5; }
            set { _UpperGaps5 = value; RaisePropertyChanged(); }
        }
        public double UpperGaps6
        {
            get { return _UpperGaps6; }
            set { _UpperGaps6 = value; RaisePropertyChanged(); }
        }

        private double _LowerGaps1, _LowerGaps2, _LowerGaps3, _LowerGaps4, _LowerGaps5, _LowerGaps6;
        public double LowerGaps1
        {
            get { return _LowerGaps1; }
            set { _LowerGaps1 = value; RaisePropertyChanged(); }
        }
        public double LowerGaps2
        {
            get { return _LowerGaps2; }
            set { _LowerGaps2 = value; RaisePropertyChanged(); }
        }
        public double LowerGaps3
        {
            get { return _LowerGaps3; }
            set { _LowerGaps3 = value; RaisePropertyChanged(); }
        }
        public double LowerGaps4
        {
            get { return _LowerGaps4; }
            set { _LowerGaps4 = value; RaisePropertyChanged(); }
        }
        public double LowerGaps5
        {
            get { return _LowerGaps5; }
            set { _LowerGaps5 = value; RaisePropertyChanged(); }
        }
        public double LowerGaps6
        {
            get { return _LowerGaps6; }
            set { _LowerGaps6 = value; RaisePropertyChanged(); }
        }

        #endregion

        public int DefectNum
        {
            get { return _DefectNum; }
            set { Set(ref _DefectNum, value); }
        }
        public bool DeviceStatic
        {
            get { return _DeviceStatic; }
            set { Set(ref _DeviceStatic, value); }
        }
        public double CONF_THRESHOLD
        {
            get{return cONF_THRESHOLD; }
            set { Set(ref cONF_THRESHOLD, value); }
        }
        public double NMS_THRESHOLD
        {
            get { return nMS_THRESHOLD; }
            set { Set(ref nMS_THRESHOLD, value); }
        }
        public Array LocationSource { get; set; } = Enum.GetValues(typeof(eLocationClass));
        public eLocationClass SelectLocation
        {
            get { return _SelectLocation; }
            set { Set(ref _SelectLocation, value); }
        }

        public string AiPath
        {
            get { return _AiPath; }
            set { Set(ref _AiPath, value); }
        }

        private bool _IsOpen, _IsDispAiRegion = false;
        public bool IsDispAiRegion
        {
            get { return _IsDispAiRegion; }
            set { _IsDispAiRegion = value; RaisePropertyChanged(); }
        }
        public bool IsOpen
        {
            get { return _IsOpen; }
            set { _IsOpen = value; RaisePropertyChanged(); }
        }

        //新增判断显示
        private bool  _IsFindRegions= false;
        
        public bool IsFindRegions
        {
            get { return _IsFindRegions; }
            set { _IsFindRegions = value; RaisePropertyChanged(); }
        }
      


        public HRegion ReduceRegion
        {
            get
            {
                if (_ReduceRegion == null)
                {
                    _ReduceRegion = new HRegion();
                }
                return _ReduceRegion;
            }
            set { Set(ref _ReduceRegion, value); }
        }
        [NonSerialized]
        HRegion OutRegion = new HRegion();
        [NonSerialized]
        int Type = 0;
        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
    

        private bool _OutPutMaxArea = false;
        public bool OutPutMaxArea
        {
            get { return _OutPutMaxArea; }
            set { _OutPutMaxArea = value; ExeModule(); RaisePropertyChanged(); }

        }
        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        //初始化，
        private LinkVarModel _Scale = new LinkVarModel() { Value=1 };
        public LinkVarModel Scale
        {
            get { return _Scale; }
            set { _Scale = value; RaisePropertyChanged(); }
        }



        private int _DrawSize = 10;
        /// <summary>
        /// 涂抹尺寸
        /// </summary>
        public int DrawSize
        {
            get { return _DrawSize; }
            set { Set(ref _DrawSize, value, new Action(() => SetBurshRegion())); }
        }
        [NonSerialized]
        public HXLDCont contour_xld;
        [NonSerialized]
        HRegion finalRegion = new HRegion();
        [NonSerialized]
        HObject brushRegion = new HObject();
        #endregion
        #region//后处理参数
        private bool useScore = false,useID =false, useLimtHeight = false ,useLimtWidth =false;
        public bool UseLimtWidth
        {
            get { return useLimtWidth; }
            set { Set(ref useLimtWidth, value); }
        }
        public bool UseLimtHeight
        {
            get { return useLimtHeight; }
            set { Set(ref useLimtHeight, value); }
        }
        public bool UseID
        {
            get { return useID; }
            set { Set(ref useID, value); }
        }
        public bool UseScore
        {
            get { return useScore; }
            set { Set(ref useScore, value); }
        }
        private double score = 0;
        public double Score
        {
            get { return score; }
            set { Set(ref score, value); }
        }
        private int iD = 1;
        public int ID
        {
            get { return iD; }
            set { Set(ref iD, value); }
        }
        private double limtHeight = 9999,limtWidth=9999;
        public double LimtHeight
        {
            get { return limtHeight; }
            set { Set(ref limtHeight, value); }
        }
        public double LimtWidth
        {
            get { return limtWidth; }
            set { Set(ref limtWidth, value); }
        }
        
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as SolderView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (InputImageLinkText == null || InputImageLinkText =="")
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                view.mWindowH.hControl.MouseUp += HControl_MouseUp;

                GetDispImage(InputImageLinkText);
                //ExeModule();
                //view.mWindowH.DispObj(DispImage);
                //ImageChanged();
                //ThresholdChanged();
                

            }
        }
        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase((obj) =>
                    {
                        ExeModule();
                    });
                }
                return _ExecuteCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as SolderView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        [NonSerialized]
        private AI ClassAi;
        [NonSerialized]
        private CommandBase _AiInit;
        public CommandBase AiInit
        {
            get
            {
                if (_AiInit == null)
                {
                    _AiInit = new CommandBase((obj) =>
                    {
                        string erro = "";
                        if (ClassAi == null)
                            ClassAi = new AI();
                        
                        if (ClassAi.IsRunning)
                        {
                            ClassAi.AiClose(ref erro);
                            DeviceStatic = false;
                        }
                        else
                        {
                            erro = ClassAi.AiInit(this.AiPath).ToString();
                            DeviceStatic = true;
                        }
                        if (!string.IsNullOrEmpty(erro))
                        {
                            // 处理错误，例如显示错误消息
                            //MessageBox.Show(erro, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                return _AiInit;
            }
        }

        [NonSerialized]
        private CommandBase _OpenAiFolder;
        public CommandBase OpenAiFolder
        {
            get
            {
                if (_OpenAiFolder == null)
                {
                    _OpenAiFolder = new CommandBase((obj) =>
                    {
                        Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                        openFileDialog.Filter = "(*.xml,*.engine,*.hymson)|*.xml;*.engine*;*.hymson|All files(*.*)|*.*";
                        if ((bool)openFileDialog.ShowDialog())
                        {
                            AiPath = openFileDialog.FileName;
                        }
                    });
                }
                return _OpenAiFolder;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    if (InputImageLinkText == null) return;
                    GetDispImage(InputImageLinkText);
                    //ImageChanged();
                    break;
                case "Scale":
                    Scale.Text = obj.LinkName;
                    break;
                default:
                    break;
            }
        }
        [NonSerialized]
        private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.MathNum:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MathNumLink");
                                break;
                            case eLinkCommand.CenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CenterY");
                                break;
                            case eLinkCommand.Scale:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Scale");
                                break;
                            default:
                                break;
                        }

                    });
                }
                return _LinkCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ClearPaintCommand;
        public CommandBase ClearPaintCommand
        {
            get
            {
                if (_ClearPaintCommand == null)
                {
                    _ClearPaintCommand = new CommandBase((obj) =>
                    {
                        finalRegion.Dispose();
                        var view = ModuleView as SolderView;
                        view.mWindowH.HobjectToHimage(DispImage);
                        view.mWindowH.WindowH.DispHobject(contour_xld, "green");
                    });
                }
                return _ClearPaintCommand;
            }
        }
        #endregion

        #region Method
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as SolderView;
                if (view == null) return; ;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                view.mWindowH.DispObj(finalRegion);
                if (index.Length < 1) return;
                RoiList[index] = roi;
                //switch (SelectedROIType)
                //{
                //    case eDrawShape.矩形:
                //        Rectangle2Region = (ROIRectangle2)roi;
                //        Rectangle2Region.Length1 = Math.Round(Rectangle2Region.Length1, 2);
                //        Rectangle2Region.Length2 = Math.Round(Rectangle2Region.Length2, 2);
                //        Rectangle2Region.MidC = Math.Round(Rectangle2Region.MidC, 2);
                //        Rectangle2Region.MidR = Math.Round(Rectangle2Region.MidR, 2);
                //        ReduceRegion.GenRectangle2(Rectangle2Region.MidR, Rectangle2Region.MidC, -Rectangle2Region.Phi, Rectangle2Region.Length1, Rectangle2Region.Length2);
                //        break;
                //    case eDrawShape.圆形:
                //        CircleRegion = (ROICircle)roi;
                //        CircleRegion.CenterX = Math.Round(CircleRegion.CenterX, 2);
                //        CircleRegion.CenterY = Math.Round(CircleRegion.CenterY, 2);
                //        CircleRegion.Radius = Math.Round(CircleRegion.Radius, 2);
                //        ReduceRegion.GenCircle(CircleRegion.CenterY, CircleRegion.CenterX, CircleRegion.Radius);
                //        break;
                //    default:
                //        break;
                //}
                //ThresholdChanged();
            }
            catch (Exception ex)
            {
            }
        }
        [NonSerialized]
        VMHWindowControl mWindowH;
        private void ShowImage(HImage image)
        {
            var view = ModuleView as SolderView;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            mWindowH.ClearWindow();
            mWindowH.HobjectToHimage(image);
        }
        private void ShowRoi()
        {
            //var view = ModuleView as BlobView;
            bool dispDrawRoi = true;
            //if (view == null || view.IsClosed)
            //{
            //    dispDrawRoi = false;
            //}

            var view = ModuleView as SolderView;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispDrawRoi = false;
            }
            else
            {
                mWindowH = view.mWindowH;
            }


            //if (dispDrawRoi)
            //{
            //    switch (SelectedROIType)
            //    {
            //        case eDrawShape.矩形:
            //            mWindowH.WindowH.DispROI(ModuleParam.ModuleEncode + ModuleParam.ModuleName + ROIType.Rectangle2, Rectangle2Region);
            //            break;

            //        case eDrawShape.圆形:
            //            mWindowH.WindowH.DispROI(ModuleParam.ModuleEncode + ModuleParam.ModuleName + ROIType.Circle, CircleRegion);
            //            break;
            //        default:
            //            break;
            //    }
            //}

            if (ReduceRegion != null && ReduceRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(ReduceRegion)));
            }
            if (OutRegion != null && OutRegion.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.检测结果, "green", new HObject(OutRegion),true));
            }
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(mWindowH.hControl.HalconWindow, roiText.size, "false", "false");
                    ShowTool.SetMsg(mWindowH.hControl.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                }
            }

        }
        public void ShowImageAndRoi(HImage image)
        {
            ShowImage(image);
            ShowRoi();
        }
        private void SetBurshRegion()
        {
            HObject ho_temp_brush = new HObject();
            HTuple hv_Row1 = 10, hv_Column1 = 10, hv_Row2 = null, hv_Column2 = null;
            HTuple imageWidth, imageHeight;
            HImage image = new HImage(DispImage);
            image.GetImageSize(out imageWidth, out imageHeight);
            //switch (DrawShape)
            //{
            //    case eDrawShape.圆形:
            //        HOperatorSet.GenCircle(out ho_temp_brush, imageWidth / 2, imageHeight / 2, DrawSize);
            //        if (hv_Row1.D != 0)
            //        {
            //            brushRegion.Dispose();
            //            brushRegion = ho_temp_brush;
            //        }
            //        break;
            //    case eDrawShape.矩形:
            //        HOperatorSet.GenRectangle1(out ho_temp_brush, 0, 0, DrawSize, DrawSize);
            //        if (hv_Row1.D != 0)
            //        {
            //            brushRegion.Dispose();
            //            brushRegion = ho_temp_brush;
            //        }
            //        break;
            //    default:
            //        break;
            //}

        }




        #endregion
        
    }
}
