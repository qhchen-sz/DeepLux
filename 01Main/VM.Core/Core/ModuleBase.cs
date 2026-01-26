using HalconDotNet;
using MahApps.Metro.Controls;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VM.Halcon;
using VM.Halcon.Config;
using
   HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Models;
using HV.Services;
using HV.Views.Dock;

namespace HV.Core
{
    [Serializable]
    public abstract class ModuleBase : NotifyPropertyBase
    {
        #region Prop
        public Guid ModuleGuid = Guid.NewGuid();

        [NonSerialized]
        public bool ClosedView = false;

        [NonSerialized]
        private RImage _DispImage;
        public RImage DispImage
        {
            get { return _DispImage; }
            set { _DispImage = value; }
        }
        [NonSerialized]
        private RImage _HeightImage;
        public RImage HeightImage
        {
            get { return _DispImage; }
            set { _DispImage = value; }
        }

        /// <summary>显示的ROI</summary>
        public List<HRoi> mHRoi = new List<HRoi>();

        /// <summary>取消等待流程 </summary>
        [NonSerialized]
        public bool CancelWait = false;

        [NonSerialized]
        private bool _NotWaitMotionFinish = false;

        /// <summary>不等待运动完成</summary>
        [Browsable(false)]
        public bool NotWaitMotionFinish
        {
            get { return _NotWaitMotionFinish; }
            set { Set(ref _NotWaitMotionFinish, value); }
        }

        /// <summary>
        /// 模板坐标
        /// </summary>
        public Coord_Info ModeCoord = new Coord_Info();

        /// <summary>
        /// 匹配坐标
        /// </summary>
        [NonSerialized]
        public Coord_Info MathCoord = new Coord_Info();

        //2D仿射矩阵
        [NonSerialized]
        public HTuple HomMat2D = new HTuple();

        //2D仿射矩阵反转
        [NonSerialized]
        public HTuple HomMat2D_Inverse = new HTuple();
        private ModuleParam _ModuleParam;

        /// <summary>
        /// 模块参数
        /// </summary>
        public ModuleParam ModuleParam
        {
            get
            {
                if (_ModuleParam == null)
                {
                    _ModuleParam = new ModuleParam();
                }
                return _ModuleParam;
            }
            set { _ModuleParam = value; }
        }

        [field: NonSerialized()]
        public ModuleViewBase ModuleView { get; set; }

        [NonSerialized]
        private Stopwatch _Stopwatch;
        public Stopwatch Stopwatch
        {
            get
            {
                if (_Stopwatch == null)
                {
                    _Stopwatch = new Stopwatch();
                }
                return _Stopwatch;
            }
            set { _Stopwatch = value; }
        }

        [NonSerialized]
        private Project _Prj;
        public Project Prj
        {
            get
            {
                if (_Prj == null)
                {
                    _Prj = Solution.Ins.GetProjectById(ModuleParam.ProjectID);
                }
                return _Prj;
            }
            set { _Prj = value; }
        }
        private int _TimeOut = 5000;

        /// <summary>
        /// 超时时间ms
        /// </summary>
        public int TimeOut
        {
            get { return _TimeOut; }
            set { Set(ref _TimeOut, value); }
        }
        private bool _IsFillDisp = true;

        /// <summary>
        /// 是否填充显示区域
        /// </summary>
        public bool IsFillDisp
        {
            get { return _IsFillDisp; }
            set { Set(ref _IsFillDisp, value); }
        }

        public List<string> CanvasList { get; set; } =
            new List<string>()
            {
                "图像窗口1",
                "图像窗口2",
                "图像窗口3",
                "图像窗口4",
                "图像窗口5",
                "图像窗口6",
                "图像窗口7",
                "图像窗口8",
                "图像窗口9",
            };
        private int _DispViewID = 0;

        /// <summary>
        /// 窗口ID
        /// </summary>
        public int DispViewID
        {
            get { return _DispViewID; }
            set
            {
                Set(
                    ref _DispViewID,
                    value,
                    new Action(() =>
                    {
                        if (DispImage != null)
                        {
                            DispImage.DispViewID = _DispViewID;
                        }
                    })
                );
            }
        }

        private string _TimeText;

        /// <summary>
        /// 时间片段模块文本
        /// </summary>
        public string TimeText
        {
            get { return _TimeText; }
            set { Set(ref _TimeText, value); }
        }
        private DateTime _DateTime;

        /// <summary>
        /// 时间片段模块时间
        /// </summary>
        public DateTime DateTime
        {
            get { return _DateTime; }
            set { _DateTime = value; }
        }

        #endregion
        #region Method
        /*private void InitRect1Changed()
        {
            if (InitLineChanged_Flag == true) return;
            InitRectangle1.row1 = Convert.ToDouble(GetLinkValue(Rect1Y1));
            InitRectangle1.col1 = Convert.ToDouble(GetLinkValue(Rect1X1));
            InitRectangle1.row2 = Convert.ToDouble(GetLinkValue(Rect1Y2));
            InitRectangle1.col2 = Convert.ToDouble(GetLinkValue(Rect1X2));
            ShowHRoi();
            //DisenableAffine2d = true;
            //if (roiLine != null)
            //{
            //    if (DisenableAffine2d && HomMat2D != null && HomMat2D.Length > 0)
            //    {
            //        Aff.Affine2d(HomMat2D, InitLine, TempLine);
            //        if (InitLineChanged_Flag)
            //        {
            //            roiLine.StartX = TempLine.StartX;
            //            roiLine.StartY = TempLine.StartY;
            //            roiLine.EndX = TempLine.EndX;
            //            roiLine.EndY = TempLine.EndY;
            //        }
            //    }
            //    else
            //    {
            //        roiLine.StartX = InitLine.StartX;
            //        roiLine.StartY = InitLine.StartY;
            //        roiLine.EndX = InitLine.EndX;
            //        roiLine.EndY = InitLine.EndY;
            //        TempLine.StartX = InitLine.StartX;
            //        TempLine.StartY = InitLine.StartY;
            //        TempLine.EndX = InitLine.EndX;
            //        TempLine.EndY = InitLine.EndY;
            //    }
            //    ExeModule();
            //    InitLineMethod();
            //}
        }*/
        /*ROILine roiLine;*/
        /*private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as MeasureGapView;
                if (view == null) return;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length < 1) return;
                ROIRectangle1 Rect1 = new ROIRectangle1();
                RoiList[index] = roi;
                
                    ROIRectangle1 rectangle1 = (ROIRectangle1)roi;
                    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                    {
                        HRegion region = rectangle1.GetRegion();
                        region = region.AffineTransRegion(new HHomMat2D(HomMat2D_Inverse), "nearest_neighbor");
                        region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
                        Rect1.row1 = Math.Round((double)row1, 3);
                        Rect1.col1 = Math.Round((double)column1, 3);
                        Rect1.row2 = Math.Round((double)row2, 3);
                        Rect1.col2 = Math.Round((double)column2, 3);
                    
                    }
                    else
                    {
                        Rect1.row1 = Math.Round(rectangle1.row1, 3);
                        Rect1.col1 = Math.Round(rectangle1.col1, 3);
                        Rect1.row2 = Math.Round(rectangle1.row2, 3);
                        Rect1.col2 = Math.Round(rectangle1.col2, 3);
                        
                    }
                    if (!Rect1X1.Text.StartsWith("&"))
                    {
                        Rect1X1.Text = Rect1.col1.ToString();
                    }
                    if (!Rect1Y1.Text.StartsWith("&"))
                    {
                    Rect1Y1.Text = Rect1.row1.ToString();
                    }
                    if (!Rect1X2.Text.StartsWith("&"))
                    {
                    Rect1X2.Text = Rect1.col2.ToString();
                    }
                    if (!Rect1Y2.Text.StartsWith("&"))
                    {
                    Rect1Y2.Text = Rect1.row2.ToString();
                    }


                    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, rectangle1.row1, rectangle1.col1, rectangle1.row2, rectangle1.col2, ref RoiList);

                IsManMual = true;
                ExeModule();
                IsManMual = false;
                //roiLine = roi as ROILine;
                //    if (roiLine != null)
                //    {
                //        TempLine.StartX = Math.Round(roiLine.StartX, 3);
                //        TempLine.StartY = Math.Round(roiLine.StartY, 3);
                //        TempLine.EndX = Math.Round(roiLine.EndX, 3);
                //        TempLine.EndY = Math.Round(roiLine.EndY, 3);
                //        DisenableAffine2d = true;
                //        InitLineChanged_Flag = true;
                //        ExeModule();
                //        InitLineMethod();
                //        InitLineChanged_Flag = false;
                //   }

            }
            catch (Exception ex)
            {
            }
        }*/
        /*public void InitRect1Method(bool isDisp = false)
        {
            var view = ModuleView as MeasureGapView;
            if (view == null) return;
            VMHWindowControl mWindowH;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            ClearRoiAndText();
            if(!isDisp)
                mWindowH.ClearROI();
            if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Rectangle1))
            {
                ROIRectangle1 ROIRect1 = (ROIRectangle1)RoiList[ModuleParam.ModuleName + ROIDefine.Rectangle1];
                if ( HomMat2D != null && HomMat2D.Length > 0)
                {
                    HRegion region = new HRegion();
                    region.GenRectangle1(
                        Convert.ToDouble(GetLinkValue(Rect1Y1)),
                        Convert.ToDouble(GetLinkValue(Rect1X1)),
                        Convert.ToDouble(GetLinkValue(Rect1X2)),
                        Convert.ToDouble(GetLinkValue(Rect1Y2)));
                    region = region.AffineTransRegion(new HHomMat2D(HomMat2D), "nearest_neighbor");
                    region.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
                    if (region.Area.I != 0)
                    {
                        mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1,
                            row1,
                            column1,
                            row2,
                            column2,
                            ref RoiList);
                    }
                    else
                    {
                        //mWindowH.WindowH.genRect2(ModuleParam.ModuleName + ROIDefine.Rectangle2, 200, 200, 0, 30, 30, ref RoiList);
                    }
                }
                else
                {
                    mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, Convert.ToDouble(GetLinkValue(Rect1Y1)), Convert.ToDouble(GetLinkValue(Rect1X1)), Convert.ToDouble(GetLinkValue(Rect1Y2)), Convert.ToDouble(GetLinkValue(Rect1X2)), ref RoiList);
                }
            }
            else
            {
                mWindowH.WindowH.genRect1(ModuleParam.ModuleName + ROIDefine.Rectangle1, 200, 200, 230, 230, ref RoiList);
            }

            //if (TranLine.FlagLineStyle != null)
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartX, TranLine.StartY, TranLine.EndX, TranLine.EndY, ref RoiList);
            //}
            //else if (DispImage != null && !RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageHeight / 4, view.mWindowH.hv_imageWidth / 2, ref RoiList);
            //    TranLine.StartX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.StartY = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndX = view.mWindowH.hv_imageHeight / 4;
            //    TranLine.EndY = view.mWindowH.hv_imageWidth / 4;
            //}
            //else if (DispImage != null && RoiList.ContainsKey(ModuleParam.ModuleName))
            //{
            //    if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, TranLine.StartY, TranLine.StartX, TranLine.EndY, TranLine.EndX, ref RoiList);
            //        Aff.Affine2d(HomMat2D_Inverse, TranLine, InitLine);
            //        InitLine.StartX = Math.Round(InitLine.StartX, 3);
            //        InitLine.StartY = Math.Round(InitLine.StartY, 3);
            //        InitLine.EndX = Math.Round(InitLine.EndX, 3);
            //        InitLine.EndY = Math.Round(InitLine.EndY, 3);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //    else
            //    {
            //        view.mWindowH.WindowH.genRect1(ModuleParam.ModuleName, InitLine.StartY, InitLine.StartX, InitLine.EndY, InitLine.EndX, ref RoiList);
            //        if (InitLineChanged_Flag)
            //        {
            //            InitLineStartX.Text = InitLine.StartX.ToString();
            //            InitLineStartY.Text = InitLine.StartY.ToString();
            //            InitLineEndX.Text = InitLine.EndX.ToString();
            //            InitLineEndY.Text = InitLine.EndY.ToString();
            //        }
            //    }
            //}
        }*/
        /// <summary>
        /// 执行模块
        /// </summary>
        /// <returns></returns>
        public abstract bool ExeModule();

        /// <summary>
        /// 加载视图
        /// </summary>
        public virtual void Loaded()
        {
            if (ModuleView != null)
            {
                ModuleView.IsClosed = false;
            }
        }

        public virtual void CloseView()
        {
            if (ModuleView != null)
            {
                ModuleView.Close();
            }
        }

        public virtual void GetHomMat2D()
        {
            int index = Prj.ModuleList.IndexOf(this);
            int value = 0;
            for (int i = index - 1; i >= 0; i--)
            {
                if (Prj.ModuleList[i].ModuleParam.ModuleName.StartsWith("坐标补正结束"))
                {
                    value += 1;
                }
                if (Prj.ModuleList[i].ModuleParam.ModuleName.StartsWith("坐标补正开始"))
                {
                    value -= 1;
                    if (value < 0)
                    {
                        HomMat2D = Prj.ModuleList[i].HomMat2D;
                        HomMat2D_Inverse = Prj.ModuleList[i].HomMat2D_Inverse;
                        return;
                    }
                }
            }
        }

        public virtual void InitModule()
        {
            AddOutputParams();
        }

        /// <summary>
        /// 添加模块输出参数
        /// </summary>
        /// <returns></returns>
        public virtual void AddOutputParams()
        {
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        public virtual void SetDefaultLink() { }

        public virtual void CompileScript() { }

        public object GetLinkValue(string var)
        {
            object result = null;
            if (var.StartsWith("&"))
            {
                if (var.Contains("["))
                {
                    string[] array = var.Split(new char[] { '[' });
                    string text = array[1].Split(new char[] { ']' })[0];
                    if (text == "i")
                    {
                        text = "0";
                        int num = this.Prj.ModuleList.IndexOf(this);
                        for (int i = num - 1; i >= 0; i--)
                        {
                            if (this.Prj.ModuleList[i].ModuleParam.ModuleName.StartsWith("循环开始"))
                            {
                                text = this.Prj.ModuleList[i].ModuleParam.pIndex.ToString();
                                if (text == "-1")
                                {
                                    text = "0";
                                }
                            }
                        }
                    }
                    string dataType = this.Prj.GetParamByName(array[0]).DataType;
                    string text2 = dataType;
                    string text3 = text2;
                    if (text3 != null)
                    {
                        switch (text3.Length)
                        {
                            case 3:
                                if (text3 == "int")
                                {
                                    result = Convert.ToInt32(this.Prj.GetParamByName(var).Value);
                                }
                                break;
                            case 4:
                                if (text3 == "bool")
                                {
                                    result = this.Prj.GetParamByName(var).Value;
                                }
                                break;
                            case 5:
                                if (text3 == "int[]")
                                {
                                    List<int> list = (List<int>)this.GetLinkValue(array[0]);
                                    if (Convert.ToInt32(text) + 1 > list.Count)
                                    {
                                        return -1;
                                    }
                                    result = list[Convert.ToInt32(text)];
                                }
                                break;
                            case 6:
                            {
                                char c = text3[0];
                                if (c != 'b')
                                {
                                    if (c != 'd')
                                    {
                                        if (c == 's')
                                        {
                                            if (text3 == "string")
                                            {
                                                result = this.Prj
                                                    .GetParamByName(var)
                                                    .Value.ToString();
                                            }
                                        }
                                    }
                                    else if (text3 == "double")
                                    {
                                        result = Convert.ToDouble(
                                            this.Prj.GetParamByName(var).Value
                                        );
                                    }
                                }
                                else if (text3 == "bool[]")
                                {
                                    List<bool> list2 = (List<bool>)this.GetLinkValue(array[0]);
                                    if (Convert.ToInt32(text) + 1 > list2.Count)
                                    {
                                        return -1;
                                    }
                                    result = list2[Convert.ToInt32(text)];
                                }
                                break;
                            }
                            case 8:
                            {
                                char c = text3[0];
                                if (c != 'd')
                                {
                                    if (c == 's')
                                    {
                                        if (text3 == "string[]")
                                        {
                                            List<string> list3 =
                                                (List<string>)this.GetLinkValue(array[0]);
                                            if (Convert.ToInt32(text) + 1 > list3.Count)
                                            {
                                                return -1;
                                            }
                                            result = list3[Convert.ToInt32(text)];
                                        }
                                    }
                                }
                                else if (text3 == "double[]")
                                {
                                    List<double> list4 = (List<double>)this.GetLinkValue(array[0]);
                                    if (Convert.ToInt32(text) + 1 > list4.Count)
                                    {
                                        return -1;
                                    }
                                    result = list4[Convert.ToInt32(text)];
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    result = this.Prj.GetParamByName(var).Value;
                }
            }
            else
            {
                result = var;
            }
            return result;
        }

        // Token: 0x06000DF6 RID: 3574 RVA: 0x00039724 File Offset: 0x00037924
        public object GetLinkValue(LinkVarModel var)
        {
            object result = null;
            if (string.IsNullOrEmpty(var.Text) || !var.Text.StartsWith("&"))
            {
                result = var.Value;
            }
            else if (var.Text.Contains("["))
            {
                string[] array = var.Text.Split(new char[] { '[' });
                string text = array[1].Split(new char[] { ']' })[0];
                if (text == "i")
                {
                    text = "0";
                    int num = this.Prj.ModuleList.IndexOf(this);
                    for (int i = num - 1; i >= 0; i--)
                    {
                        if (this.Prj.ModuleList[i].ModuleParam.ModuleName.StartsWith("循环开始"))
                        {
                            text = this.Prj.ModuleList[i].ModuleParam.pIndex.ToString();
                            if (text == "-1")
                            {
                                text = "0";
                            }
                        }
                    }
                }
                string dataType = this.Prj.GetParamByName(array[0]).DataType;
                string text2 = dataType;
                string text3 = text2;
                if (text3 != null)
                {
                    switch (text3.Length)
                    {
                        case 3:
                            if (text3 == "int")
                            {
                                result = Convert.ToInt32(this.Prj.GetParamByName(var.Text).Value);
                            }
                            break;
                        case 4:
                            if (text3 == "bool")
                            {
                                result = this.Prj.GetParamByName(var.Text).Value;
                            }
                            break;
                        case 5:
                            if (text3 == "int[]")
                            {
                                List<int> list = (List<int>)this.GetLinkValue(array[0]);
                                if (Convert.ToInt32(text) + 1 > list.Count)
                                {
                                    return -1;
                                }
                                result = list[Convert.ToInt32(text)];
                            }
                            break;
                        case 6:
                        {
                            char c = text3[0];
                            if (c != 'b')
                            {
                                if (c != 'd')
                                {
                                    if (c == 's')
                                    {
                                        if (text3 == "string")
                                        {
                                            result = this.Prj
                                                .GetParamByName(var.Text)
                                                .Value.ToString();
                                        }
                                    }
                                }
                                else if (text3 == "double")
                                {
                                    result = Convert.ToDouble(
                                        this.Prj.GetParamByName(var.Text).Value
                                    );
                                }
                            }
                            else if (text3 == "bool[]")
                            {
                                List<bool> list2 = (List<bool>)this.GetLinkValue(array[0]);
                                if (Convert.ToInt32(text) + 1 > list2.Count)
                                {
                                    return -1;
                                }
                                result = list2[Convert.ToInt32(text)];
                            }
                            break;
                        }
                        case 8:
                        {
                            char c = text3[0];
                            if (c != 'd')
                            {
                                if (c == 's')
                                {
                                    if (text3 == "string[]")
                                    {
                                        List<string> list3 =
                                            (List<string>)this.GetLinkValue(array[0]);
                                        if (Convert.ToInt32(text) + 1 > list3.Count)
                                        {
                                            return -1;
                                        }
                                        result = list3[Convert.ToInt32(text)];
                                    }
                                }
                            }
                            else if (text3 == "double[]")
                            {
                                List<double> list4 = (List<double>)this.GetLinkValue(array[0]);
                                if (Convert.ToInt32(text) + 1 > list4.Count)
                                {
                                    return -1;
                                }
                                result = list4[Convert.ToInt32(text)];
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                result = this.Prj.GetParamByName(var.Text).Value;
            }
            return result;
        }

        public void GetDispImage(string imageLinkText, bool isDispImageAtView = false)
        {
            if (imageLinkText == null)
                return;
            VarModel var = Prj.GetParamByName(imageLinkText);
            if (var == null)
            {
                DispImage = null;
                return;
            }
                
            object image = var.Value;
            //HOperatorSet.GetImageSize((HObject)image, out HTuple hTuple11, out HTuple hTuple22);
            if (image == null)
                return;
            if (image is RImage rImage)
            {
                DispImage = rImage;
                //DispImage.GetImageSize(out HTuple hTuple, out HTuple hTuple1);
            }
            else if (image is HImage)
            {
                DispImage = new RImage((HObject)image);
                //DispImage.GetImageSize(out HTuple hTuple, out HTuple hTuple1);
            }
            if (DispImage != null && DispImage.IsInitialized())
            {
                if (
                    ModuleView != null
                    && ModuleView.mWindowH != null
                    && ModuleView.IsClosed == false
                )
                {
                    //ModuleView.mWindowH.Image = DispImage;
                    ModuleView.mWindowH.HobjectToHimage(DispImage);
                }
                if (isDispImageAtView)
                {
                    DispImage.DispViewID = DispViewID;
                }
                else
                {
                    DispViewID = DispImage.DispViewID;
                }
                //if (ModuleView == null || ModuleView.IsClosed)
                //{
                //    ViewDic.GetView(DispImage.DispViewID).HobjectToHimage(DispImage);
                //}
            }
        }

        /// <summary>
        /// 输出变量
        /// </summary>
        protected void AddOutputParam(string varName, string varType, object obj, string note = "")
        {
            this.Prj.AddOutputParam(this.ModuleParam, varName, varType, obj, note);
        }

        protected void ClearOutputParam()
        {
            this.Prj.ClearOutputParam(this.ModuleParam);
        }
        protected void ChangeModuleRunStatus(eRunStatus runStatus)
        {
            if(ModuleParam.Status != eRunStatus.Disable)
            {
                ModuleParam.Status = runStatus;
            }
            Stopwatch.Stop();
            ModuleParam.ElapsedTime = Stopwatch.ElapsedMilliseconds;
            if (ModuleParam.PluginName != "条件分支" && ModuleParam.PluginName != "延时工具")
            {
                if (runStatus == eRunStatus.OK)
                {
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块成功，耗时{ModuleParam.ElapsedTime}ms."
                    );
                }
                else
                {
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        DispImage = new RImage();
                        DispImage.ReadImage($"{FilePaths.ConfigFilePath}Background.bmp");
                    }
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，耗时{ModuleParam.ElapsedTime}ms.",
                        eMsgType.Warn
                    );
                }
            }
                
               
            

            AddOutputParams();
        }

        public virtual void DeleteModule() { }

        public virtual void Init() { }

        public virtual void ShowHRoi()
        {
            VMHWindowControl mWindowH;
            if (ModuleView == null || ModuleView.IsClosed)
            {
                return;
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = ModuleView.mWindowH;
                if (mWindowH != null)
                {
                    mWindowH.ClearROI();
                    //mWindowH.Image = new HImage(DispImage);
                }
            }
            //List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            mWindowH.DispText.Clear();
            foreach (HRoi roi in mHRoi)
            {

                if (roi.roiType == HRoiType.文字显示)
                {


                    HText roiText = (HText)roi;
                    ShowTool.SetFont(
                    mWindowH.hControl.HalconWindow,
                    roiText.size,
                    "false",
                    "false"
                );
                    //HTuple htWinHandle = mWindowH.hControl.HalconWindow;
                    //mWindowH.DispText.Add(roiText.text);
                    //mWindowH.hControl.HalconWindow.DispText( roiText.text, "image", roiText.col, roiText.row, roiText.drawColor, "box", "false");

                    ShowTool.SetMsg(
                        mWindowH.hControl.HalconWindow,
                        roiText.text,
                        "image",
                        roiText.row,
                        roiText.col,
                        roiText.drawColor,
                        "false"
                    );
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                }
            }
        }

        public void ClearRoiAndText()
        {
            mHRoi.Clear();
        }

        public double GetDouble(string varName)
        {
            return Convert.ToDouble(Prj.GetParamByName(varName).Value);
        }

        public int GetInt(string varName)
        {
            return Convert.ToInt32(Prj.GetParamByName(varName).Value);
        }

        public bool GetBool(string varName)
        {
            return Convert.ToBoolean(Prj.GetParamByName(varName).Value);
        }

        public string GetString(string varName)
        {
            return Convert.ToString(Prj.GetParamByName(varName).Value);
        }

        public void SetDouble(string varName ,double Value)
        {
            Prj.GetParamByName(varName).Value = Value;
        }
        public void SetInt(string varName, int Value)
        {
            Prj.GetParamByName(varName).Value = Value;
        }
        public void SetBool(string varName, bool Value)
        {
            Prj.GetParamByName(varName).Value = Value;
        }
        public void SetString(string varName, string Value)
        {
            Prj.GetParamByName(varName).Value = Value;
        }
        #region ROI显示
        /// <summary>显示Roi</summary>
        public void ShowHRoi(HRoi ROI)
        {
            try
            {
                int index = mHRoi.FindIndex(
                    e => e.roiType == ROI.roiType && e.ModuleName == ROI.ModuleName
                );
                if (ROI.fors == true)
                {
                    mHRoi.Add(ROI);
                    return;
                }
                if (index > -1)
                    mHRoi[index] = ROI;
                else
                    mHRoi.Add(ROI);
                //DispImage.mHRoi = mHRoi;
                int index2 = DispImage.mHRoi.FindIndex(
    e => e.roiType == ROI.roiType && e.ModuleName == ROI.ModuleName
);
                if (ROI.fors == true)
                {
                    DispImage.mHRoi.Add(ROI);
                    return;
                }
                if (index2 > -1)
                    DispImage.mHRoi[index2] = ROI;
                else
                    DispImage.mHRoi.Add(ROI);

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        #endregion
        #endregion
    }
}
