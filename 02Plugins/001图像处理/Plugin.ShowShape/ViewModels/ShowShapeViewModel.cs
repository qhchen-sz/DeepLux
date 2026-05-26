using EventMgrLib;
using HalconDotNet;
using Plugin.ShowShape.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views.Dock;
using Newtonsoft.Json.Linq;

namespace Plugin.ShowShape.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        LineStartX,
        LineStartY,
        LineEndX,
        LineEndY,
        CircleCenterX,
        CircleCenterY,
        CircleRadius,
        Rect1Row1,
        Rect1Col1,
        Rect1Row2,
        Rect1Col2,
        Rect2MidR,
        Rect2MidC,
        Rect2Phi,
        Rect2Length1,
        Rect2Length2,
        InputRoiLink,
    }

    public enum eShapeType
    {
        直线,
        圆,
        矩形1,
        矩形2
    }
    #endregion

    #region ShapeParams
    [Serializable]
    public class LineParams : NotifyPropertyBase
    {
        public int Index { get; set; }
        public eShapeType ShapeType { get { return eShapeType.直线; } }
        private LinkVarModel _StartX = new LinkVarModel() { Text = "0" };
        public LinkVarModel StartX
        {
            get { return _StartX; }
            set { _StartX = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _StartY = new LinkVarModel() { Text = "0" };
        public LinkVarModel StartY
        {
            get { return _StartY; }
            set { _StartY = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _EndX = new LinkVarModel() { Text = "100" };
        public LinkVarModel EndX
        {
            get { return _EndX; }
            set { _EndX = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _EndY = new LinkVarModel() { Text = "100" };
        public LinkVarModel EndY
        {
            get { return _EndY; }
            set { _EndY = value; RaisePropertyChanged(); }
        }
        private string _Color = "green";
        public string Color
        {
            get { return _Color; }
            set { _Color = value; RaisePropertyChanged(); }
        }
        private bool _IsFill = false;
        public bool IsFill
        {
            get { return _IsFill; }
            set { _IsFill = value; RaisePropertyChanged(); }
        }
        [NonSerialized]
        public ROILine OutRoi;
        public CommandBase LinkCommand { get; set; }
    }

    [Serializable]
    public class CircleParams : NotifyPropertyBase
    {
        public int Index { get; set; }
        public eShapeType ShapeType { get { return eShapeType.圆; } }
        private LinkVarModel _CenterX = new LinkVarModel() { Text = "100" };
        public LinkVarModel CenterX
        {
            get { return _CenterX; }
            set { _CenterX = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _CenterY = new LinkVarModel() { Text = "100" };
        public LinkVarModel CenterY
        {
            get { return _CenterY; }
            set { _CenterY = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Radius = new LinkVarModel() { Text = "50" };
        public LinkVarModel Radius
        {
            get { return _Radius; }
            set { _Radius = value; RaisePropertyChanged(); }
        }
        private string _Color = "green";
        public string Color
        {
            get { return _Color; }
            set { _Color = value; RaisePropertyChanged(); }
        }
        private bool _IsFill = false;
        public bool IsFill
        {
            get { return _IsFill; }
            set { _IsFill = value; RaisePropertyChanged(); }
        }
        [NonSerialized]
        public ROICircle OutRoi;
        public CommandBase LinkCommand { get; set; }
    }

    [Serializable]
    public class Rect1Params : NotifyPropertyBase
    {
        public int Index { get; set; }
        public eShapeType ShapeType { get { return eShapeType.矩形1; } }
        private LinkVarModel _Row1 = new LinkVarModel() { Text = "50" };
        public LinkVarModel Row1
        {
            get { return _Row1; }
            set { _Row1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Col1 = new LinkVarModel() { Text = "50" };
        public LinkVarModel Col1
        {
            get { return _Col1; }
            set { _Col1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Row2 = new LinkVarModel() { Text = "150" };
        public LinkVarModel Row2
        {
            get { return _Row2; }
            set { _Row2 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Col2 = new LinkVarModel() { Text = "150" };
        public LinkVarModel Col2
        {
            get { return _Col2; }
            set { _Col2 = value; RaisePropertyChanged(); }
        }
        private string _Color = "green";
        public string Color
        {
            get { return _Color; }
            set { _Color = value; RaisePropertyChanged(); }
        }
        private bool _IsFill = false;
        public bool IsFill
        {
            get { return _IsFill; }
            set { _IsFill = value; RaisePropertyChanged(); }
        }
        [NonSerialized]
        public ROIRectangle1 OutRoi;
        public CommandBase LinkCommand { get; set; }
    }

    [Serializable]
    public class Rect2Params : NotifyPropertyBase
    {
        public int Index { get; set; }
        public eShapeType ShapeType { get { return eShapeType.矩形2; } }
        private LinkVarModel _MidR = new LinkVarModel() { Text = "100" };
        public LinkVarModel MidR
        {
            get { return _MidR; }
            set { _MidR = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _MidC = new LinkVarModel() { Text = "100" };
        public LinkVarModel MidC
        {
            get { return _MidC; }
            set { _MidC = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Phi = new LinkVarModel() { Text = "0" };
        public LinkVarModel Phi
        {
            get { return _Phi; }
            set { _Phi = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Length1 = new LinkVarModel() { Text = "50" };
        public LinkVarModel Length1
        {
            get { return _Length1; }
            set { _Length1 = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _Length2 = new LinkVarModel() { Text = "30" };
        public LinkVarModel Length2
        {
            get { return _Length2; }
            set { _Length2 = value; RaisePropertyChanged(); }
        }
        private string _Color = "green";
        public string Color
        {
            get { return _Color; }
            set { _Color = value; RaisePropertyChanged(); }
        }
        private bool _IsFill = false;
        public bool IsFill
        {
            get { return _IsFill; }
            set { _IsFill = value; RaisePropertyChanged(); }
        }
        [NonSerialized]
        public ROIRectangle2 OutRoi;
        public CommandBase LinkCommand { get; set; }
    }

    [Serializable]
    public class RoiParams : NotifyPropertyBase
    {
        public int Index { get; set; }
        private LinkVarModel _InputRoi = new LinkVarModel();
        public LinkVarModel InputRoi
        {
            get { return _InputRoi; }
            set { _InputRoi = value; RaisePropertyChanged(); }
        }
        private string _RoiColor = "green";
        public string RoiColor
        {
            get { return _RoiColor; }
            set { _RoiColor = value; RaisePropertyChanged(); }
        }
        private bool _IsFill = false;
        public bool IsFill
        {
            get { return _IsFill; }
            set { _IsFill = value; RaisePropertyChanged(); }
        }
        public CommandBase LinkCommand { get; set; }
    }
    #endregion

    [Category("图像处理")]
    [DisplayName("形状显示")]
    [ModuleImageName("ShowShape")]
    [Serializable]
    public class ShowShapeViewModel : ModuleBase
    {
        #region ExeModule
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                ClearRoiAndText();

                // 获取输入图像（可选）
                if (!string.IsNullOrEmpty(InputImageLinkText))
                {
                    GetDispImage(InputImageLinkText, true);
                }
                else
                {
                    // 无输入图像时创建一个空白图像作为画布
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        DispImage = new RImage(new HImage("byte", 500, 500));
                    }
                }

                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 1. 绘制手动输入的直线
                foreach (var item in LineParams)
                {
                    try
                    {
                        double sx = Convert.ToDouble(GetLinkValue(item.StartX));
                        double sy = Convert.ToDouble(GetLinkValue(item.StartY));
                        double ex = Convert.ToDouble(GetLinkValue(item.EndX));
                        double ey = Convert.ToDouble(GetLinkValue(item.EndY));

                        item.OutRoi = new ROILine();
                        item.OutRoi.CreateLine(sy, sx, ey, ex);

                        HObject hObj = new HObject(item.OutRoi.GetRegion());
                        if (hObj != null && hObj.IsInitialized())
                        {
                            ShowHRoi(new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName + "_line_" + item.Index,
                                ModuleParam.Remarks,
                                HRoiType.检测结果,
                                item.Color,
                                hObj,
                                item.IsFill));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"ShowShape 直线[{item.Index}] 绘制失败: {ex.Message}", eMsgType.Error);
                    }
                }

                // 2. 绘制手动输入的圆
                foreach (var item in CircleParams)
                {
                    try
                    {
                        double cx = Convert.ToDouble(GetLinkValue(item.CenterX));
                        double cy = Convert.ToDouble(GetLinkValue(item.CenterY));
                        double r = Convert.ToDouble(GetLinkValue(item.Radius));

                        item.OutRoi = new ROICircle();
                        item.OutRoi.CreateCircle(cy, cx, r);

                        HObject hObj = new HObject(item.OutRoi.GetRegion());
                        if (hObj != null && hObj.IsInitialized())
                        {
                            ShowHRoi(new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName + "_circle_" + item.Index,
                                ModuleParam.Remarks,
                                HRoiType.检测结果,
                                item.Color,
                                hObj,
                                item.IsFill));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"ShowShape 圆[{item.Index}] 绘制失败: {ex.Message}", eMsgType.Error);
                    }
                }

                // 3. 绘制手动输入的矩形1
                foreach (var item in Rect1Params)
                {
                    try
                    {
                        double r1 = Convert.ToDouble(GetLinkValue(item.Row1));
                        double c1 = Convert.ToDouble(GetLinkValue(item.Col1));
                        double r2 = Convert.ToDouble(GetLinkValue(item.Row2));
                        double c2 = Convert.ToDouble(GetLinkValue(item.Col2));

                        item.OutRoi = new ROIRectangle1();
                        item.OutRoi.CreateRectangle1(r1, c1, r2, c2);

                        HObject hObj = new HObject(item.OutRoi.GetRegion());
                        if (hObj != null && hObj.IsInitialized())
                        {
                            ShowHRoi(new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName + "_rect1_" + item.Index,
                                ModuleParam.Remarks,
                                HRoiType.检测结果,
                                item.Color,
                                hObj,
                                item.IsFill));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"ShowShape 矩形1[{item.Index}] 绘制失败: {ex.Message}", eMsgType.Error);
                    }
                }

                // 4. 绘制手动输入的矩形2
                foreach (var item in Rect2Params)
                {
                    try
                    {
                        double mr = Convert.ToDouble(GetLinkValue(item.MidR));
                        double mc = Convert.ToDouble(GetLinkValue(item.MidC));
                        double phi = Convert.ToDouble(GetLinkValue(item.Phi));
                        double len1 = Convert.ToDouble(GetLinkValue(item.Length1));
                        double len2 = Convert.ToDouble(GetLinkValue(item.Length2));

                        item.OutRoi = new ROIRectangle2();
                        item.OutRoi.CreateRectangle2(mr, mc, phi, len1, len2);

                        HObject hObj = new HObject(item.OutRoi.GetRegion());
                        if (hObj != null && hObj.IsInitialized())
                        {
                            ShowHRoi(new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName + "_rect2_" + item.Index,
                                ModuleParam.Remarks,
                                HRoiType.检测结果,
                                item.Color,
                                hObj,
                                item.IsFill));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"ShowShape 矩形2[{item.Index}] 绘制失败: {ex.Message}", eMsgType.Error);
                    }
                }

                // 5. 显示引用的上游 ROI
                for (int i = 0; i < RoiParam.Count; i++)
                {
                    if (RoiParam[i].InputRoi == null || string.IsNullOrEmpty(RoiParam[i].InputRoi.Text))
                        continue;
                    try
                    {
                        var obj = GetLinkValue(RoiParam[i].InputRoi);
                        if (obj != null)
                        {
                            HObject hObj = null;
                            if (obj is HRegion region)
                                hObj = new HObject(region);
                            else if (obj is HObject ho)
                                hObj = ho;
                            else if (obj is HXLDCont xld)
                                hObj = new HObject(xld);
                            else if (obj is ROILine roiLine)
                                hObj = new HObject(roiLine.GetRegion());
                            else if (obj is ROICircle roiCircle)
                                hObj = new HObject(roiCircle.GetRegion());
                            else if (obj is ROICircularArc roiArc)
                                hObj = new HObject(roiArc.GetRegion());
                            else if (obj is ROIRectangle1 roiRect1)
                                hObj = new HObject(roiRect1.GetRegion());
                            else if (obj is ROIRectangle2 roiRect2)
                                hObj = new HObject(roiRect2.GetRegion());

                            if (hObj != null && hObj.IsInitialized())
                            {
                                ShowHRoi(new HRoi(
                                    ModuleParam.ModuleEncode,
                                    ModuleParam.ModuleName + "_roi_" + i,
                                    ModuleParam.Remarks,
                                    HRoiType.检测结果,
                                    RoiParam[i].RoiColor,
                                    hObj,
                                    RoiParam[i].IsFill));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"ShowShape ROI[{i}] 显示失败: {ex.Message}", eMsgType.Error);
                    }
                }

                ShowHRoi();
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }
        #endregion

        #region OutputParams
        public override void AddOutputParams()
        {
            if (DispImage == null)
                DispImage = new RImage(new HImage("byte", 500, 500));
            if (!DispImage.IsInitialized())
                DispImage.GenEmptyObj();

            base.AddOutputParams();

            // 输出手动绘制的形状对象
            for (int i = 0; i < LineParams.Count; i++)
                if (LineParams[i].OutRoi != null)
                    AddOutputParam($"直线{i}", "object", LineParams[i].OutRoi);

            for (int i = 0; i < CircleParams.Count; i++)
                if (CircleParams[i].OutRoi != null)
                    AddOutputParam($"圆{i}", "object", CircleParams[i].OutRoi);

            for (int i = 0; i < Rect1Params.Count; i++)
                if (Rect1Params[i].OutRoi != null)
                    AddOutputParam($"矩形1_{i}", "object", Rect1Params[i].OutRoi);

            for (int i = 0; i < Rect2Params.Count; i++)
                if (Rect2Params[i].OutRoi != null)
                    AddOutputParam($"矩形2_{i}", "object", Rect2Params[i].OutRoi);

            AddOutputParam("图像", "HImage", DispImage);
        }
        #endregion

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                if (!string.IsNullOrEmpty(value))
                    GetDispImage(value);
            }
        }

        private ObservableCollection<LineParams> _LineParams = new ObservableCollection<LineParams>();
        public ObservableCollection<LineParams> LineParams
        {
            get { return _LineParams; }
            set { _LineParams = value; RaisePropertyChanged(); }
        }
        private int _nSelectLineIndex = -1;
        public int nSelectLineIndex
        {
            get { return _nSelectLineIndex; }
            set { Set(ref _nSelectLineIndex, value); }
        }

        private ObservableCollection<CircleParams> _CircleParams = new ObservableCollection<CircleParams>();
        public ObservableCollection<CircleParams> CircleParams
        {
            get { return _CircleParams; }
            set { _CircleParams = value; RaisePropertyChanged(); }
        }
        private int _nSelectCircleIndex = -1;
        public int nSelectCircleIndex
        {
            get { return _nSelectCircleIndex; }
            set { Set(ref _nSelectCircleIndex, value); }
        }

        private ObservableCollection<Rect1Params> _Rect1Params = new ObservableCollection<Rect1Params>();
        public ObservableCollection<Rect1Params> Rect1Params
        {
            get { return _Rect1Params; }
            set { _Rect1Params = value; RaisePropertyChanged(); }
        }
        private int _nSelectRect1Index = -1;
        public int nSelectRect1Index
        {
            get { return _nSelectRect1Index; }
            set { Set(ref _nSelectRect1Index, value); }
        }

        private ObservableCollection<Rect2Params> _Rect2Params = new ObservableCollection<Rect2Params>();
        public ObservableCollection<Rect2Params> Rect2Params
        {
            get { return _Rect2Params; }
            set { _Rect2Params = value; RaisePropertyChanged(); }
        }
        private int _nSelectRect2Index = -1;
        public int nSelectRect2Index
        {
            get { return _nSelectRect2Index; }
            set { Set(ref _nSelectRect2Index, value); }
        }

        private ObservableCollection<RoiParams> _RoiParam = new ObservableCollection<RoiParams>();
        public ObservableCollection<RoiParams> RoiParam
        {
            get { return _RoiParam; }
            set { _RoiParam = value; RaisePropertyChanged(); }
        }
        private int _nSelectRoiIndex = -1;
        public int nSelectRoiIndex
        {
            get { return _nSelectRoiIndex; }
            set { Set(ref _nSelectRoiIndex, value); }
        }

        private bool _ShowResultRoi = true;
        public bool ShowResultRoi
        {
            get { return _ShowResultRoi; }
            set { Set(ref _ShowResultRoi, value); }
        }
        private bool _ShowImage = true;
        public bool ShowImage
        {
            get { return _ShowImage; }
            set { Set(ref _ShowImage, value); }
        }
        private bool _ShowOkLog;
        public bool ShowOkLog
        {
            get { return _ShowOkLog; }
            set { Set(ref _ShowOkLog, value); }
        }
        private bool _ShowNgLog;
        public bool ShowNgLog
        {
            get { return _ShowNgLog; }
            set { Set(ref _ShowNgLog, value); }
        }
        #endregion

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["nSelectLineIndex"] = nSelectLineIndex;
            obj["nSelectCircleIndex"] = nSelectCircleIndex;
            obj["nSelectRect1Index"] = nSelectRect1Index;
            obj["nSelectRect2Index"] = nSelectRect2Index;
            obj["nSelectRoiIndex"] = nSelectRoiIndex;
            obj["ShowResultRoi"] = ShowResultRoi;
            obj["ShowImage"] = ShowImage;
            obj["ShowOkLog"] = ShowOkLog;
            obj["ShowNgLog"] = ShowNgLog;
            JArray lineArr = new JArray();
            if (LineParams != null)
            {
                foreach (var item in LineParams)
                {
                    JObject itemObj = new JObject();
                    itemObj["Index"] = item.Index;
                    itemObj["StartX"] = item.StartX?.Text ?? "0";
                    itemObj["StartY"] = item.StartY?.Text ?? "0";
                    itemObj["EndX"] = item.EndX?.Text ?? "100";
                    itemObj["EndY"] = item.EndY?.Text ?? "100";
                    itemObj["Color"] = item.Color ?? "green";
                    itemObj["IsFill"] = item.IsFill;
                    lineArr.Add(itemObj);
                }
            }
            obj["LineParams"] = lineArr;
            JArray circleArr = new JArray();
            if (CircleParams != null)
            {
                foreach (var item in CircleParams)
                {
                    JObject itemObj = new JObject();
                    itemObj["Index"] = item.Index;
                    itemObj["CenterX"] = item.CenterX?.Text ?? "100";
                    itemObj["CenterY"] = item.CenterY?.Text ?? "100";
                    itemObj["Radius"] = item.Radius?.Text ?? "50";
                    itemObj["Color"] = item.Color ?? "green";
                    itemObj["IsFill"] = item.IsFill;
                    circleArr.Add(itemObj);
                }
            }
            obj["CircleParams"] = circleArr;
            JArray rect1Arr = new JArray();
            if (Rect1Params != null)
            {
                foreach (var item in Rect1Params)
                {
                    JObject itemObj = new JObject();
                    itemObj["Index"] = item.Index;
                    itemObj["Row1"] = item.Row1?.Text ?? "50";
                    itemObj["Col1"] = item.Col1?.Text ?? "50";
                    itemObj["Row2"] = item.Row2?.Text ?? "150";
                    itemObj["Col2"] = item.Col2?.Text ?? "150";
                    itemObj["Color"] = item.Color ?? "green";
                    itemObj["IsFill"] = item.IsFill;
                    rect1Arr.Add(itemObj);
                }
            }
            obj["Rect1Params"] = rect1Arr;
            JArray rect2Arr = new JArray();
            if (Rect2Params != null)
            {
                foreach (var item in Rect2Params)
                {
                    JObject itemObj = new JObject();
                    itemObj["Index"] = item.Index;
                    itemObj["MidR"] = item.MidR?.Text ?? "100";
                    itemObj["MidC"] = item.MidC?.Text ?? "100";
                    itemObj["Phi"] = item.Phi?.Text ?? "0";
                    itemObj["Length1"] = item.Length1?.Text ?? "50";
                    itemObj["Length2"] = item.Length2?.Text ?? "30";
                    itemObj["Color"] = item.Color ?? "green";
                    itemObj["IsFill"] = item.IsFill;
                    rect2Arr.Add(itemObj);
                }
            }
            obj["Rect2Params"] = rect2Arr;
            JArray roiArr = new JArray();
            if (RoiParam != null)
            {
                foreach (var item in RoiParam)
                {
                    JObject itemObj = new JObject();
                    itemObj["Index"] = item.Index;
                    itemObj["InputRoi"] = item.InputRoi?.Text ?? "";
                    itemObj["RoiColor"] = item.RoiColor ?? "green";
                    itemObj["IsFill"] = item.IsFill;
                    roiArr.Add(itemObj);
                }
            }
            obj["RoiParam"] = roiArr;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["nSelectLineIndex"] != null) nSelectLineIndex = obj["nSelectLineIndex"].Value<int>();
                if (obj["nSelectCircleIndex"] != null) nSelectCircleIndex = obj["nSelectCircleIndex"].Value<int>();
                if (obj["nSelectRect1Index"] != null) nSelectRect1Index = obj["nSelectRect1Index"].Value<int>();
                if (obj["nSelectRect2Index"] != null) nSelectRect2Index = obj["nSelectRect2Index"].Value<int>();
                if (obj["nSelectRoiIndex"] != null) nSelectRoiIndex = obj["nSelectRoiIndex"].Value<int>();
                if (obj["ShowResultRoi"] != null) ShowResultRoi = obj["ShowResultRoi"].Value<bool>();
                if (obj["ShowImage"] != null) ShowImage = obj["ShowImage"].Value<bool>();
                if (obj["ShowOkLog"] != null) ShowOkLog = obj["ShowOkLog"].Value<bool>();
                if (obj["ShowNgLog"] != null) ShowNgLog = obj["ShowNgLog"].Value<bool>();
                if (obj["LineParams"] != null)
                {
                    JArray arr = (JArray)obj["LineParams"];
                    LineParams.Clear();
                    foreach (var item in arr)
                    {
                        LineParams.Add(new LineParams()
                        {
                            Index = item["Index"]?.Value<int>() ?? 0,
                            Color = item["Color"]?.ToString() ?? "green",
                            IsFill = item["IsFill"]?.Value<bool>() ?? false,
                            LinkCommand = LinkCommand
                        });
                        var lp = LineParams.Last();
                        if (item["StartX"] != null && lp.StartX != null) lp.StartX.Text = item["StartX"].ToString();
                        if (item["StartY"] != null && lp.StartY != null) lp.StartY.Text = item["StartY"].ToString();
                        if (item["EndX"] != null && lp.EndX != null) lp.EndX.Text = item["EndX"].ToString();
                        if (item["EndY"] != null && lp.EndY != null) lp.EndY.Text = item["EndY"].ToString();
                    }
                }
                if (obj["CircleParams"] != null)
                {
                    JArray arr = (JArray)obj["CircleParams"];
                    CircleParams.Clear();
                    foreach (var item in arr)
                    {
                        CircleParams.Add(new CircleParams()
                        {
                            Index = item["Index"]?.Value<int>() ?? 0,
                            Color = item["Color"]?.ToString() ?? "green",
                            IsFill = item["IsFill"]?.Value<bool>() ?? false,
                            LinkCommand = LinkCommand
                        });
                        var cp = CircleParams.Last();
                        if (item["CenterX"] != null && cp.CenterX != null) cp.CenterX.Text = item["CenterX"].ToString();
                        if (item["CenterY"] != null && cp.CenterY != null) cp.CenterY.Text = item["CenterY"].ToString();
                        if (item["Radius"] != null && cp.Radius != null) cp.Radius.Text = item["Radius"].ToString();
                    }
                }
                if (obj["Rect1Params"] != null)
                {
                    JArray arr = (JArray)obj["Rect1Params"];
                    Rect1Params.Clear();
                    foreach (var item in arr)
                    {
                        Rect1Params.Add(new Rect1Params()
                        {
                            Index = item["Index"]?.Value<int>() ?? 0,
                            Color = item["Color"]?.ToString() ?? "green",
                            IsFill = item["IsFill"]?.Value<bool>() ?? false,
                            LinkCommand = LinkCommand
                        });
                        var rp = Rect1Params.Last();
                        if (item["Row1"] != null && rp.Row1 != null) rp.Row1.Text = item["Row1"].ToString();
                        if (item["Col1"] != null && rp.Col1 != null) rp.Col1.Text = item["Col1"].ToString();
                        if (item["Row2"] != null && rp.Row2 != null) rp.Row2.Text = item["Row2"].ToString();
                        if (item["Col2"] != null && rp.Col2 != null) rp.Col2.Text = item["Col2"].ToString();
                    }
                }
                if (obj["Rect2Params"] != null)
                {
                    JArray arr = (JArray)obj["Rect2Params"];
                    Rect2Params.Clear();
                    foreach (var item in arr)
                    {
                        Rect2Params.Add(new Rect2Params()
                        {
                            Index = item["Index"]?.Value<int>() ?? 0,
                            Color = item["Color"]?.ToString() ?? "green",
                            IsFill = item["IsFill"]?.Value<bool>() ?? false,
                            LinkCommand = LinkCommand
                        });
                        var rp = Rect2Params.Last();
                        if (item["MidR"] != null && rp.MidR != null) rp.MidR.Text = item["MidR"].ToString();
                        if (item["MidC"] != null && rp.MidC != null) rp.MidC.Text = item["MidC"].ToString();
                        if (item["Phi"] != null && rp.Phi != null) rp.Phi.Text = item["Phi"].ToString();
                        if (item["Length1"] != null && rp.Length1 != null) rp.Length1.Text = item["Length1"].ToString();
                        if (item["Length2"] != null && rp.Length2 != null) rp.Length2.Text = item["Length2"].ToString();
                    }
                }
                if (obj["RoiParam"] != null)
                {
                    JArray arr = (JArray)obj["RoiParam"];
                    RoiParam.Clear();
                    foreach (var item in arr)
                    {
                        RoiParam.Add(new RoiParams()
                        {
                            Index = item["Index"]?.Value<int>() ?? 0,
                            RoiColor = item["RoiColor"]?.ToString() ?? "green",
                            IsFill = item["IsFill"]?.Value<bool>() ?? false,
                            LinkCommand = LinkCommand
                        });
                        var rp = RoiParam.Last();
                        if (item["InputRoi"] != null && rp.InputRoi != null) rp.InputRoi.Text = item["InputRoi"].ToString();
                    }
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"ShowShapeViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as ShowShapeView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                foreach (var item in LineParams) item.LinkCommand = LinkCommand;
                foreach (var item in CircleParams) item.LinkCommand = LinkCommand;
                foreach (var item in Rect1Params) item.LinkCommand = LinkCommand;
                foreach (var item in Rect2Params) item.LinkCommand = LinkCommand;
                foreach (var item in RoiParam) item.LinkCommand = LinkCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            string cmd = obj.SendName.Split(',')[1];
            switch (cmd)
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "LineStartX":
                    if (nSelectLineIndex >= 0 && nSelectLineIndex < LineParams.Count)
                        LineParams[nSelectLineIndex].StartX.Text = obj.LinkName;
                    break;
                case "LineStartY":
                    if (nSelectLineIndex >= 0 && nSelectLineIndex < LineParams.Count)
                        LineParams[nSelectLineIndex].StartY.Text = obj.LinkName;
                    break;
                case "LineEndX":
                    if (nSelectLineIndex >= 0 && nSelectLineIndex < LineParams.Count)
                        LineParams[nSelectLineIndex].EndX.Text = obj.LinkName;
                    break;
                case "LineEndY":
                    if (nSelectLineIndex >= 0 && nSelectLineIndex < LineParams.Count)
                        LineParams[nSelectLineIndex].EndY.Text = obj.LinkName;
                    break;
                case "CircleCenterX":
                    if (nSelectCircleIndex >= 0 && nSelectCircleIndex < CircleParams.Count)
                        CircleParams[nSelectCircleIndex].CenterX.Text = obj.LinkName;
                    break;
                case "CircleCenterY":
                    if (nSelectCircleIndex >= 0 && nSelectCircleIndex < CircleParams.Count)
                        CircleParams[nSelectCircleIndex].CenterY.Text = obj.LinkName;
                    break;
                case "CircleRadius":
                    if (nSelectCircleIndex >= 0 && nSelectCircleIndex < CircleParams.Count)
                        CircleParams[nSelectCircleIndex].Radius.Text = obj.LinkName;
                    break;
                case "Rect1Row1":
                    if (nSelectRect1Index >= 0 && nSelectRect1Index < Rect1Params.Count)
                        Rect1Params[nSelectRect1Index].Row1.Text = obj.LinkName;
                    break;
                case "Rect1Col1":
                    if (nSelectRect1Index >= 0 && nSelectRect1Index < Rect1Params.Count)
                        Rect1Params[nSelectRect1Index].Col1.Text = obj.LinkName;
                    break;
                case "Rect1Row2":
                    if (nSelectRect1Index >= 0 && nSelectRect1Index < Rect1Params.Count)
                        Rect1Params[nSelectRect1Index].Row2.Text = obj.LinkName;
                    break;
                case "Rect1Col2":
                    if (nSelectRect1Index >= 0 && nSelectRect1Index < Rect1Params.Count)
                        Rect1Params[nSelectRect1Index].Col2.Text = obj.LinkName;
                    break;
                case "Rect2MidR":
                    if (nSelectRect2Index >= 0 && nSelectRect2Index < Rect2Params.Count)
                        Rect2Params[nSelectRect2Index].MidR.Text = obj.LinkName;
                    break;
                case "Rect2MidC":
                    if (nSelectRect2Index >= 0 && nSelectRect2Index < Rect2Params.Count)
                        Rect2Params[nSelectRect2Index].MidC.Text = obj.LinkName;
                    break;
                case "Rect2Phi":
                    if (nSelectRect2Index >= 0 && nSelectRect2Index < Rect2Params.Count)
                        Rect2Params[nSelectRect2Index].Phi.Text = obj.LinkName;
                    break;
                case "Rect2Length1":
                    if (nSelectRect2Index >= 0 && nSelectRect2Index < Rect2Params.Count)
                        Rect2Params[nSelectRect2Index].Length1.Text = obj.LinkName;
                    break;
                case "Rect2Length2":
                    if (nSelectRect2Index >= 0 && nSelectRect2Index < Rect2Params.Count)
                        Rect2Params[nSelectRect2Index].Length2.Text = obj.LinkName;
                    break;
                case "InputRoiLink":
                    if (nSelectRoiIndex >= 0 && nSelectRoiIndex < RoiParam.Count)
                        RoiParam[nSelectRoiIndex].InputRoi.Text = obj.LinkName;
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
                            case eLinkCommand.LineStartX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LineStartX");
                                break;
                            case eLinkCommand.LineStartY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LineStartY");
                                break;
                            case eLinkCommand.LineEndX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LineEndX");
                                break;
                            case eLinkCommand.LineEndY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LineEndY");
                                break;
                            case eLinkCommand.CircleCenterX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CircleCenterX");
                                break;
                            case eLinkCommand.CircleCenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CircleCenterY");
                                break;
                            case eLinkCommand.CircleRadius:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CircleRadius");
                                break;
                            case eLinkCommand.Rect1Row1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Row1");
                                break;
                            case eLinkCommand.Rect1Col1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Col1");
                                break;
                            case eLinkCommand.Rect1Row2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Row2");
                                break;
                            case eLinkCommand.Rect1Col2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect1Col2");
                                break;
                            case eLinkCommand.Rect2MidR:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2MidR");
                                break;
                            case eLinkCommand.Rect2MidC:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2MidC");
                                break;
                            case eLinkCommand.Rect2Phi:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2Phi");
                                break;
                            case eLinkCommand.Rect2Length1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2Length1");
                                break;
                            case eLinkCommand.Rect2Length2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Rect2Length2");
                                break;
                            case eLinkCommand.InputRoiLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion,HObject,HXLDCont,object");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRoiLink");
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
        private CommandBase _ShapeOperateCommand;
        public CommandBase ShapeOperateCommand
        {
            get
            {
                if (_ShapeOperateCommand == null)
                {
                    _ShapeOperateCommand = new CommandBase((obj) =>
                    {
                        string[] parts = obj.ToString().Split(',');
                        string action = parts[0];
                        string type = parts[1];
                        switch (action)
                        {
                            case "Add":
                                switch (type)
                                {
                                    case "Line":
                                        LineParams.Add(new LineParams()
                                        {
                                            Index = LineParams.Count,
                                            LinkCommand = LinkCommand
                                        });
                                        break;
                                    case "Circle":
                                        CircleParams.Add(new CircleParams()
                                        {
                                            Index = CircleParams.Count,
                                            LinkCommand = LinkCommand
                                        });
                                        break;
                                    case "Rect1":
                                        Rect1Params.Add(new Rect1Params()
                                        {
                                            Index = Rect1Params.Count,
                                            LinkCommand = LinkCommand
                                        });
                                        break;
                                    case "Rect2":
                                        Rect2Params.Add(new Rect2Params()
                                        {
                                            Index = Rect2Params.Count,
                                            LinkCommand = LinkCommand
                                        });
                                        break;
                                }
                                break;
                            case "Delete":
                                switch (type)
                                {
                                    case "Line":
                                        if (nSelectLineIndex < 0) return;
                                        LineParams.RemoveAt(nSelectLineIndex);
                                        break;
                                    case "Circle":
                                        if (nSelectCircleIndex < 0) return;
                                        CircleParams.RemoveAt(nSelectCircleIndex);
                                        break;
                                    case "Rect1":
                                        if (nSelectRect1Index < 0) return;
                                        Rect1Params.RemoveAt(nSelectRect1Index);
                                        break;
                                    case "Rect2":
                                        if (nSelectRect2Index < 0) return;
                                        Rect2Params.RemoveAt(nSelectRect2Index);
                                        break;
                                }
                                break;
                        }
                    });
                }
                return _ShapeOperateCommand;
            }
        }

        [NonSerialized]
        private CommandBase _RoiOperateCommand;
        public CommandBase RoiOperateCommand
        {
            get
            {
                if (_RoiOperateCommand == null)
                {
                    _RoiOperateCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "Add":
                                RoiParam.Add(new RoiParams()
                                {
                                    Index = RoiParam.Count,
                                    LinkCommand = LinkCommand
                                });
                                break;
                            case "Delete":
                                if (nSelectRoiIndex < 0) return;
                                RoiParam.RemoveAt(nSelectRoiIndex);
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _RoiOperateCommand;
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
                        var view = this.ModuleView as ShowShapeView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        #endregion

        #region ShowHRoi
        public override void ShowHRoi()
        {
            var imageSnapshot = DispImage;
            if (imageSnapshot == null || !imageSnapshot.IsInitialized())
                return;

            var view = ModuleView as ShowShapeView;
            VMHWindowControl mWindowH =
                (view == null || view.IsClosed)
                ? ViewDic.GetView(DispViewID)
                : view.mWindowH;

            if (mWindowH == null || mWindowH.IsDisposed)
                return;

            mWindowH.BeginInvoke(new Action(() =>
            {
                lock (HWndCtrl._displayLock)
                {
                    int windowsW = 594;
                    int windowsH = 583;
                    HTuple width = new HTuple(), height = new HTuple();

                    if (DispImage != null && DispImage.IsInitialized())
                    {
                        HOperatorSet.GetImageSize(DispImage, out width, out height);
                        windowsW = mWindowH.hControl.Width;
                        windowsH = mWindowH.hControl.Height;
                    }

                    if (mWindowH != null)
                    {
                        mWindowH.ClearWindow();
                        mWindowH.Image = new RImage(DispImage);

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
                                HText hText = new HText(roiText.drawColor, roiText.text, roiText.row, roiText.col, (int)roiText.size);
                                mWindowH.WindowH.DispText(hText);
                            }
                            else
                            {
                                mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                            }
                        }
                    }
                }
            }));
        }
        #endregion
    }
}
