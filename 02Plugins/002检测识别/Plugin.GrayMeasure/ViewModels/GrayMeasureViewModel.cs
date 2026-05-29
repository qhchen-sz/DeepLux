using EventMgrLib;
using HalconDotNet;
using Plugin.GrayMeasure.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using Newtonsoft.Json.Linq;

namespace Plugin.GrayMeasure.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
    }

    [Category("检测识别")]
    [DisplayName("灰度测量")]
    [ModuleImageName("GrayMeasure")]
    [Serializable]
    public class GrayMeasureViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
                return;
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
                if (!IsOpenWindows)
                {
                    GetDispImage(InputImageLinkText);
                }
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                HImage grayImage = DispImage;
                HOperatorSet.CountChannels(DispImage, out HTuple channels);
                if (channels.I > 1)
                {
                    HOperatorSet.Rgb1ToGray(DispImage, out HObject grayObj);
                    grayImage = new HImage(grayObj);
                }

                List<double> grayValues = new List<double>();
                HObject displayCircles = null;
                HObject scopeRegion = null;

                if (RoiMode == eRoiMode.Circle)
                {
                    // ========== 环形ROI模式：单圆 + 圆周卡尺 ==========
                    HTuple metrologyHandle = null;

                    try
                    {
                        HOperatorSet.CreateMetrologyModel(out metrologyHandle);
                        HOperatorSet.GetImageSize(grayImage, out HTuple imgWidth, out HTuple imgHeight);
                        HOperatorSet.SetMetrologyModelImageSize(metrologyHandle, imgWidth, imgHeight);

                        HOperatorSet.AddMetrologyObjectCircleMeasure(metrologyHandle,
                            CenterY, CenterX, Radius,
                            Length1, Length2, 1.0, 30.0,
                            (new HTuple("start_phi")).TupleConcat("end_phi"),
                            (new HTuple(0.0)).TupleConcat(new HTuple(360).TupleRad()),
                            out HTuple metrologyIndex);

                        HOperatorSet.SetMetrologyObjectParam(metrologyHandle, "all", "num_measures", MeasNum);

                        HOperatorSet.GetMetrologyObjectMeasures(out scopeRegion, metrologyHandle,
                            "all", "all", out HTuple measureRows, out HTuple measureCols);

                        HOperatorSet.CountObj(scopeRegion, out HTuple numMeasures);

                        for (int i = 1; i <= numMeasures; i++)
                        {
                            HOperatorSet.SelectObj(scopeRegion, out HObject singleXld, i);
                            HOperatorSet.GenRegionContourXld(singleXld, out HObject singleRegion, "filled");

                            HOperatorSet.Intensity(singleRegion, grayImage, out HTuple meanGray, out HTuple deviation);
                            double grayVal = Math.Round(meanGray.D, 2);
                            grayValues.Add(grayVal);

                            if (ShowGrayValue)
                            {
                                HOperatorSet.AreaCenter(singleRegion, out HTuple area, out HTuple row, out HTuple col);
                                string text = $"{i}:{grayVal:F1}";
                                ShowHRoi(new HText(
                                    ModuleParam.ModuleEncode,
                                    $"{ModuleParam.ModuleName}_{i}",
                                    ModuleParam.Remarks,
                                    HRoiType.文字显示,
                                    "green",
                                    text,
                                    col.D,
                                    row.D,
                                    64
                                ));
                            }

                            singleXld.Dispose();
                            singleRegion.Dispose();
                        }
                    }
                    finally
                    {
                        if (metrologyHandle != null)
                            HOperatorSet.ClearMetrologyModel(metrologyHandle);
                    }

                    if (ShowMeasContour && scopeRegion != null && scopeRegion.IsInitialized())
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测范围,
                            "blue",
                            new HObject(scopeRegion)
                        ));
                    }
                    if (ShowCirclePath)
                    {
                        HOperatorSet.GenCircleContourXld(out displayCircles,
                            CenterY, CenterX, Radius, 0, 6.28318, "positive", 1);
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "cyan",
                            new HObject(displayCircles)
                        ));
                    }
                }
                else if (RoiMode == eRoiMode.CircleArray)
                {
                    // ========== 圆阵ROI模式：按行列生成多个圆，每个圆算灰度 ==========
                    HOperatorSet.GenEmptyObj(out HObject allCircleContours);
                    int circleIndex = 1;

                    for (int r = 0; r < ArrayRows; r++)
                    {
                        for (int c = 0; c < ArrayCols; c++)
                        {
                            double cy = CenterY + r * ArrayRowSpacing;
                            double cx = CenterX + c * ArrayColSpacing;

                            HRegion circleRegion = new HRegion();
                            circleRegion.GenCircle(cy, cx, Radius);

                            HOperatorSet.Intensity(circleRegion, grayImage, out HTuple meanGray, out HTuple deviation);
                            double grayVal = Math.Round(meanGray.D, 2);
                            grayValues.Add(grayVal);

                            if (ShowGrayValue)
                            {
                                string text = $"{circleIndex}:{grayVal:F1}";
                                ShowHRoi(new HText(
                                    ModuleParam.ModuleEncode,
                                    $"{ModuleParam.ModuleName}_{circleIndex}",
                                    ModuleParam.Remarks,
                                    HRoiType.文字显示,
                                    "green",
                                    text,
                                    cx,
                                    cy,
                                    64
                                ));
                            }
                            circleIndex++;

                            HOperatorSet.GenCircleContourXld(out HObject circleCont, cy, cx, Radius, 0, 6.28318, "positive", 1);
                            HOperatorSet.ConcatObj(allCircleContours, circleCont, out HObject temp);
                            allCircleContours.Dispose();
                            allCircleContours = temp;

                            circleRegion.Dispose();
                            circleCont.Dispose();
                        }
                    }

                    displayCircles = allCircleContours;
                    if (ShowCircleArray && displayCircles != null && displayCircles.IsInitialized())
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "cyan",
                            new HObject(displayCircles)
                        ));
                    }
                }
                else if (RoiMode == eRoiMode.RectArray)
                {
                    // ========== 矩形阵ROI模式：按行列生成多个矩形，每个矩形算灰度 ==========
                    HOperatorSet.GenEmptyObj(out HObject allRectContours);
                    int rectIndex = 1;

                    for (int r = 0; r < ArrayRows; r++)
                    {
                        for (int c = 0; c < ArrayCols; c++)
                        {
                            double ry = CenterY + r * ArrayRowSpacing;
                            double rx = CenterX + c * ArrayColSpacing;

                            HRegion rectRegion = new HRegion();
                            rectRegion.GenRectangle2(ry, rx, RectAngle, RectLength1, RectLength2);

                            HOperatorSet.Intensity(rectRegion, grayImage, out HTuple meanGray, out HTuple deviation);
                            double grayVal = Math.Round(meanGray.D, 2);
                            grayValues.Add(grayVal);

                            if (ShowGrayValue)
                            {
                                string text = $"{rectIndex}:{grayVal:F1}";
                                ShowHRoi(new HText(
                                    ModuleParam.ModuleEncode,
                                    $"{ModuleParam.ModuleName}_{rectIndex}",
                                    ModuleParam.Remarks,
                                    HRoiType.文字显示,
                                    "green",
                                    text,
                                    rx,
                                    ry,
                                    64
                                ));
                            }
                            rectIndex++;

                            HOperatorSet.GenRectangle2ContourXld(out HObject rectCont, ry, rx, RectAngle, RectLength1, RectLength2);
                            HOperatorSet.ConcatObj(allRectContours, rectCont, out HObject temp);
                            allRectContours.Dispose();
                            allRectContours = temp;

                            rectRegion.Dispose();
                            rectCont.Dispose();
                        }
                    }

                    HObject displayRects = allRectContours;
                    if (ShowCircleArray && displayRects != null && displayRects.IsInitialized())
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "cyan",
                            new HObject(displayRects)
                        ));
                    }
                }

                AverageGray = grayValues.Count > 0 ? Math.Round(grayValues.Average(), 2) : 0;
                GrayValueList = grayValues;
                MaxGray = grayValues.Count > 0 ? grayValues.Max() : 0;
                MinGray = grayValues.Count > 0 ? grayValues.Min() : 0;

                ShowHRoi();

                displayCircles?.Dispose();
                scopeRegion?.Dispose();

                if (channels.I > 1)
                    grayImage.Dispose();

                // 最大/最小灰度阈值检测
                bool checkOK = true;
                if (EnableMaxGrayCheck && grayValues.Any(v => v > MaxGrayThreshold))
                    checkOK = false;
                if (EnableMinGrayCheck && grayValues.Any(v => v < MinGrayThreshold))
                    checkOK = false;

                if (checkOK)
                {
                    ChangeModuleRunStatus(eRunStatus.OK);
                    return true;
                }
                else
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            ClearOutputParam();
            AddOutputParam("平均灰度", "double", AverageGray);
            AddOutputParam("最大灰度", "double", MaxGray);
            AddOutputParam("最小灰度", "double", MinGray);
            for (int i = 0; GrayValueList != null && i < GrayValueList.Count; i++)
            {
                AddOutputParam($"灰度值_{i + 1}", "double", GrayValueList[i]);
            }
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private eRoiMode _RoiMode = eRoiMode.Circle;
        public eRoiMode RoiMode
        {
            get { return _RoiMode; }
            set { Set(ref _RoiMode, value); }
        }

        private double _CenterX = 200;
        public double CenterX
        {
            get { return _CenterX; }
            set { Set(ref _CenterX, value); }
        }

        private double _CenterY = 200;
        public double CenterY
        {
            get { return _CenterY; }
            set { Set(ref _CenterY, value); }
        }

        private double _Radius = 100;
        public double Radius
        {
            get { return _Radius; }
            set { Set(ref _Radius, value); }
        }

        private double _Length1 = 30;
        public double Length1
        {
            get { return _Length1; }
            set { Set(ref _Length1, value); }
        }

        private double _Length2 = 10;
        public double Length2
        {
            get { return _Length2; }
            set { Set(ref _Length2, value); }
        }

        private int _MeasNum = 12;
        public int MeasNum
        {
            get { return _MeasNum; }
            set { Set(ref _MeasNum, value); }
        }

        // 圆阵参数
        private int _ArrayRows = 3;
        public int ArrayRows
        {
            get { return _ArrayRows; }
            set { Set(ref _ArrayRows, value); }
        }

        private int _ArrayCols = 3;
        public int ArrayCols
        {
            get { return _ArrayCols; }
            set { Set(ref _ArrayCols, value); }
        }

        private double _ArrayRowSpacing = 100;
        public double ArrayRowSpacing
        {
            get { return _ArrayRowSpacing; }
            set { Set(ref _ArrayRowSpacing, value); }
        }

        private double _ArrayColSpacing = 100;
        public double ArrayColSpacing
        {
            get { return _ArrayColSpacing; }
            set { Set(ref _ArrayColSpacing, value); }
        }

        private double _RectLength1 = 50;
        public double RectLength1
        {
            get { return _RectLength1; }
            set { Set(ref _RectLength1, value); }
        }

        private double _RectLength2 = 30;
        public double RectLength2
        {
            get { return _RectLength2; }
            set { Set(ref _RectLength2, value); }
        }

        private double _RectAngle = 0;
        public double RectAngle
        {
            get { return _RectAngle; }
            set { Set(ref _RectAngle, value); }
        }

        private double _AverageGray;
        public double AverageGray
        {
            get { return _AverageGray; }
            set { Set(ref _AverageGray, value); }
        }

        private double _MaxGray;
        public double MaxGray
        {
            get { return _MaxGray; }
            set { Set(ref _MaxGray, value); }
        }

        private double _MinGray;
        public double MinGray
        {
            get { return _MinGray; }
            set { Set(ref _MinGray, value); }
        }

        [NonSerialized]
        private List<double> _GrayValueList = new List<double>();
        public List<double> GrayValueList
        {
            get { return _GrayValueList; }
            set { Set(ref _GrayValueList, value); }
        }

        private bool _ShowMeasContour = true;
        public bool ShowMeasContour
        {
            get { return _ShowMeasContour; }
            set { Set(ref _ShowMeasContour, value); }
        }

        private bool _ShowGrayValue = true;
        public bool ShowGrayValue
        {
            get { return _ShowGrayValue; }
            set { Set(ref _ShowGrayValue, value); }
        }

        private bool _ShowCirclePath = true;
        public bool ShowCirclePath
        {
            get { return _ShowCirclePath; }
            set { Set(ref _ShowCirclePath, value); }
        }

        private bool _ShowCircleArray = true;
        public bool ShowCircleArray
        {
            get { return _ShowCircleArray; }
            set { Set(ref _ShowCircleArray, value); }
        }

        private bool _EnableMaxGrayCheck = false;
        public bool EnableMaxGrayCheck
        {
            get { return _EnableMaxGrayCheck; }
            set { Set(ref _EnableMaxGrayCheck, value); }
        }

        private double _MaxGrayThreshold = 255;
        public double MaxGrayThreshold
        {
            get { return _MaxGrayThreshold; }
            set { Set(ref _MaxGrayThreshold, value); }
        }

        private bool _EnableMinGrayCheck = false;
        public bool EnableMinGrayCheck
        {
            get { return _EnableMinGrayCheck; }
            set { Set(ref _EnableMinGrayCheck, value); }
        }

        private double _MinGrayThreshold = 0;
        public double MinGrayThreshold
        {
            get { return _MinGrayThreshold; }
            set { Set(ref _MinGrayThreshold, value); }
        }

        /// <summary> 交互式ROI列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        [NonSerialized]
        private ROICircle _roiCircle;
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as GrayMeasureView;
            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }
            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText == null) return;
            }
            GetDispImage(InputImageLinkText);
            if (DispImage != null && DispImage.IsInitialized())
            {
                view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                ShowHRoi();
                InitCircleMethod();
            }
        }

        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase((obj) =>
                    {
                        ExeModule();
                        InitCircleMethod();
                    });
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
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as GrayMeasureView;
                        if (view != null)
                            view.Close();
                    });
                return _ConfirmCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
            }
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                if (RoiMode != eRoiMode.Circle) return;  // 圆阵模式不响应拖动
                var view = ModuleView as GrayMeasureView;
                if (view == null) return;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length > 0)
                {
                    _roiCircle = roi as ROICircle;
                    if (_roiCircle != null)
                    {
                        CenterX = Math.Round(_roiCircle.CenterX, 3);
                        CenterY = Math.Round(_roiCircle.CenterY, 3);
                        Radius = Math.Round(_roiCircle.Radius, 3);
                        ExeModule();
                        InitCircleMethod();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void InitCircleMethod()
        {
            var view = ModuleView as GrayMeasureView;
            if (view == null) return;
            if (DispImage == null || !DispImage.IsInitialized()) return;

            // 先清掉旧的交互圆，避免模式切换时残留
            view.mWindowH.ClearROI();
            RoiList.Clear();

            // 重绘 mHRoi（卡尺区域、结果轮廓等），避免 ClearROI 把 ShowHRoi 画的内容也清掉
            ShowHRoi();

            if (RoiMode == eRoiMode.Circle)
            {
                if (ShowCirclePath)
                {
                    view.mWindowH.WindowH.genCircle(
                        ModuleParam.ModuleName,
                        CenterY,
                        CenterX,
                        Radius,
                        ref RoiList
                    );
                }
            }
            else if (RoiMode == eRoiMode.CircleArray && ShowCircleArray)
            {
                // 圆阵模式：为每个圆生成一个交互圆（不可拖动，因为 HControl_MouseUp 已跳过）
                for (int r = 0; r < ArrayRows; r++)
                {
                    for (int c = 0; c < ArrayCols; c++)
                    {
                        double cy = CenterY + r * ArrayRowSpacing;
                        double cx = CenterX + c * ArrayColSpacing;
                        string roiName = $"{ModuleParam.ModuleName}_{r}_{c}";
                        view.mWindowH.WindowH.genCircle(roiName, cy, cx, Radius, ref RoiList);
                    }
                }
            }
            else if (RoiMode == eRoiMode.RectArray && ShowCircleArray)
            {
                // 矩形阵模式：为每个矩形生成一个交互矩形（不可拖动，因为 HControl_MouseUp 已跳过）
                for (int r = 0; r < ArrayRows; r++)
                {
                    for (int c = 0; c < ArrayCols; c++)
                    {
                        double ry = CenterY + r * ArrayRowSpacing;
                        double rx = CenterX + c * ArrayColSpacing;
                        string roiName = $"{ModuleParam.ModuleName}_{r}_{c}";
                        view.mWindowH.WindowH.genRect2(roiName, ry, rx, RectAngle, RectLength1, RectLength2, ref RoiList);
                    }
                }
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
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged,
                        o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InputImageLink");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion
        #region Serialize
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["RoiMode"] = (int)RoiMode;
            obj["CenterX"] = CenterX;
            obj["CenterY"] = CenterY;
            obj["Radius"] = Radius;
            obj["Length1"] = Length1;
            obj["Length2"] = Length2;
            obj["MeasNum"] = MeasNum;
            obj["ArrayRows"] = ArrayRows;
            obj["ArrayCols"] = ArrayCols;
            obj["ArrayRowSpacing"] = ArrayRowSpacing;
            obj["ArrayColSpacing"] = ArrayColSpacing;
            obj["RectLength1"] = RectLength1;
            obj["RectLength2"] = RectLength2;
            obj["RectAngle"] = RectAngle;
            obj["ShowMeasContour"] = ShowMeasContour;
            obj["ShowGrayValue"] = ShowGrayValue;
            obj["ShowCirclePath"] = ShowCirclePath;
            obj["ShowCircleArray"] = ShowCircleArray;
            obj["EnableMaxGrayCheck"] = EnableMaxGrayCheck;
            obj["MaxGrayThreshold"] = MaxGrayThreshold;
            obj["EnableMinGrayCheck"] = EnableMinGrayCheck;
            obj["MinGrayThreshold"] = MinGrayThreshold;
            JArray roiArray = new JArray();
            foreach (var kvp in RoiList)
            {
                HTuple data = kvp.Value.GetModelData();
                JObject roiObj = new JObject
                {
                    ["Key"] = kvp.Key,
                    ["Type"] = kvp.Value.Type.ToString(),
                    ["Color"] = kvp.Value.Color,
                    ["ModelData"] = new JArray(data.ToDArr().Select(d => d))
                };
                roiArray.Add(roiObj);
            }
            obj["RoiList"] = roiArray;
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
                if (obj["RoiMode"] != null) RoiMode = (eRoiMode)obj["RoiMode"].Value<int>();
                if (obj["CenterX"] != null) CenterX = obj["CenterX"].Value<double>();
                if (obj["CenterY"] != null) CenterY = obj["CenterY"].Value<double>();
                if (obj["Radius"] != null) Radius = obj["Radius"].Value<double>();
                if (obj["Length1"] != null) Length1 = obj["Length1"].Value<double>();
                if (obj["Length2"] != null) Length2 = obj["Length2"].Value<double>();
                if (obj["MeasNum"] != null) MeasNum = obj["MeasNum"].Value<int>();
                if (obj["ArrayRows"] != null) ArrayRows = obj["ArrayRows"].Value<int>();
                if (obj["ArrayCols"] != null) ArrayCols = obj["ArrayCols"].Value<int>();
                if (obj["ArrayRowSpacing"] != null) ArrayRowSpacing = obj["ArrayRowSpacing"].Value<double>();
                if (obj["ArrayColSpacing"] != null) ArrayColSpacing = obj["ArrayColSpacing"].Value<double>();
                if (obj["RectLength1"] != null) RectLength1 = obj["RectLength1"].Value<double>();
                if (obj["RectLength2"] != null) RectLength2 = obj["RectLength2"].Value<double>();
                if (obj["RectAngle"] != null) RectAngle = obj["RectAngle"].Value<double>();
                if (obj["ShowMeasContour"] != null) ShowMeasContour = obj["ShowMeasContour"].Value<bool>();
                if (obj["ShowGrayValue"] != null) ShowGrayValue = obj["ShowGrayValue"].Value<bool>();
                if (obj["ShowCirclePath"] != null) ShowCirclePath = obj["ShowCirclePath"].Value<bool>();
                if (obj["ShowCircleArray"] != null) ShowCircleArray = obj["ShowCircleArray"].Value<bool>();
                if (obj["EnableMaxGrayCheck"] != null) EnableMaxGrayCheck = obj["EnableMaxGrayCheck"].Value<bool>();
                if (obj["MaxGrayThreshold"] != null) MaxGrayThreshold = obj["MaxGrayThreshold"].Value<double>();
                if (obj["EnableMinGrayCheck"] != null) EnableMinGrayCheck = obj["EnableMinGrayCheck"].Value<bool>();
                if (obj["MinGrayThreshold"] != null) MinGrayThreshold = obj["MinGrayThreshold"].Value<double>();
                if (obj["RoiList"] != null)
                {
                    RoiList.Clear();
                    foreach (JToken token in (JArray)obj["RoiList"])
                    {
                        string key = token["Key"]?.ToString();
                        string type = token["Type"]?.ToString();
                        string color = token["Color"]?.ToString() ?? "yellow";
                        JArray dataArr = (JArray)token["ModelData"];
                        if (string.IsNullOrEmpty(key) || dataArr == null)
                            continue;
                        double[] data = dataArr.Select(d => d.Value<double>()).ToArray();
                        ROI roi = CreateROIFromData(type, data);
                        if (roi != null)
                        {
                            roi.Color = color;
                            RoiList[key] = roi;
                        }
                    }
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"GrayMeasureViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        private ROI CreateROIFromData(string type, double[] data)
        {
            switch (type)
            {
                case "Circle":
                    if (data.Length >= 3)
                        return new ROICircle(data[0], data[1], data[2]);
                    break;
                case "Line":
                    if (data.Length >= 4)
                        return new ROILine(data[0], data[1], data[2], data[3]);
                    break;
                case "Rectangle1":
                    if (data.Length >= 4)
                        return new ROIRectangle1(data[0], data[1], data[2], data[3]);
                    break;
                case "Rectangle2":
                    if (data.Length >= 5)
                        return new ROIRectangle2(data[0], data[1], data[2], data[3], data[4]);
                    break;
            }
            return null;
        }
        #endregion

    }
}
