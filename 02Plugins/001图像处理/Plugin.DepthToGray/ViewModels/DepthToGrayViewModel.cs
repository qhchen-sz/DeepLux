using EventMgrLib;
using HalconDotNet;
using Plugin.DepthToGray.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
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
using Newtonsoft.Json.Linq;

namespace Plugin.DepthToGray.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MinZValue,
        MaxZValue,
        MinGrayValue,
        MaxGrayValue,
        FilterInvalidValue,
        SubtractHeight,
    }
    #endregion

    [Category("图像处理")]
    [DisplayName("深度图转灰度")]
    [ModuleImageName("DepthToGray")]
    [Serializable]
    public class DepthToGrayViewModel : ModuleBase
    {
        #region ExeModule
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                // 获取输入图像
                if (InputImageLink == null || string.IsNullOrEmpty(InputImageLink.Text))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                HImage inputImage = GetLinkValue(InputImageLink) as HImage;
                if (inputImage == null || !inputImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 提取高度通道（2通道图：Ch1=高度，Ch2=亮度）
                HOperatorSet.CountChannels(inputImage, out HTuple channels);
                HImage heightImage;
                if (channels.I == 2)
                {
                    HOperatorSet.Decompose2(inputImage, out HObject ch1Obj, out HObject _);
                    heightImage = new HImage(ch1Obj);
                    ch1Obj.Dispose();
                }
                else if (channels.I > 2)
                {
                    HOperatorSet.AccessChannel(inputImage, out HObject ch1Obj, 1);
                    heightImage = new HImage(ch1Obj);
                    ch1Obj.Dispose();
                }
                else
                {
                    heightImage = new HImage(inputImage);
                }

                // 转 real 类型，避免整数运算截断导致二值化
                HOperatorSet.ConvertImageType(heightImage, out HObject realObj, "real");
                heightImage.Dispose();
                heightImage = new HImage(realObj);
                realObj.Dispose();

                // 解析过滤参数
                double filterValue = double.MinValue;
                if (!string.IsNullOrEmpty(FilterInvalidValue?.Text))
                {
                    double.TryParse(GetLinkValue(FilterInvalidValue)?.ToString(), out filterValue);
                }

                // 过滤无效值：记录有效区域，不缩减 domain
                HObject filterValidRegion = null;
                if (filterValue > double.MinValue)
                {
                    HOperatorSet.Threshold(heightImage, out filterValidRegion, filterValue, 99999);
                }

                // 整图作为工作图（不 ReduceDomain，保持完整 domain）
                HImage workImage = new HImage(heightImage);
                heightImage.Dispose();

                // 深度值 × Z分辨率 → 物理高度
                if (Math.Abs(ResolutionZ - 1.0) > 1e-12)
                {
                    HOperatorSet.ScaleImage(workImage, out HObject scaledObj, ResolutionZ, 0.0);
                    workImage.Dispose();
                    workImage = new HImage(scaledObj);
                    scaledObj.Dispose();
                }

                // 确定 Z 范围（仅统计有效区域）
                double minZ, maxZ;
                if (IsLimitValue)
                {
                    if (!double.TryParse(GetLinkValue(MinZValue)?.ToString(), out minZ)) minZ = 0;
                    if (!double.TryParse(GetLinkValue(MaxZValue)?.ToString(), out maxZ)) maxZ = 255;
                }
                else
                {
                    HTuple tMinZ, tMaxZ;
                    if (filterValidRegion != null)
                        HOperatorSet.MinMaxGray(filterValidRegion, workImage, 0, out tMinZ, out tMaxZ, out HTuple _);
                    else
                        HOperatorSet.MinMaxGray(workImage, workImage, 0, out tMinZ, out tMaxZ, out HTuple _);
                    minZ = tMinZ.D;
                    maxZ = tMaxZ.D;
                }

                string targetType = ResultImageType == "16U" ? "uint2" : "byte";

                double rangeZ = maxZ - minZ;
                if (Math.Abs(rangeZ) < 1e-10) rangeZ = 1;

                // minZ → 0, maxZ → 255, 线性映射（在整图上做）
                double mult = 255.0 / rangeZ;
                double add = -mult * minZ;
                HOperatorSet.ScaleImage(workImage, out HObject scaledImage, mult, add);

                // 无效区域涂黑
                if (filterValidRegion != null)
                {
                    HOperatorSet.GetImageSize(scaledImage, out HTuple imgW, out HTuple imgH);
                    HOperatorSet.GenRectangle1(out HObject fullRect, 0, 0, imgH - 1, imgW - 1);
                    HOperatorSet.Difference(fullRect, filterValidRegion, out HObject invalidRegion);
                    HOperatorSet.PaintRegion(invalidRegion, scaledImage, out HObject paintedImage, 0, "fill");
                    scaledImage.Dispose();
                    scaledImage = paintedImage;
                    fullRect.Dispose();
                    invalidRegion.Dispose();
                }

                // 生成输出图（PaintRegion 输出的已是完整 domain）
                HOperatorSet.ConvertImageType(scaledImage, out HObject resultImage, targetType);

                DispImage = new RImage(new HImage(resultImage));
                scaledImage.Dispose();
                resultImage.Dispose();
                ShowHRoi();
                ShowHistogram(workImage, filterValidRegion);
                ChangeModuleRunStatus(eRunStatus.OK);
                workImage.Dispose();
                filterValidRegion?.Dispose();
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

        #region AddOutputParams
        public override void AddOutputParams()
        {
            if (DispImage == null)
                DispImage = new RImage(new HImage("byte", 100, 100));
            if (!DispImage.IsInitialized())
                DispImage.GenEmptyObj();
            base.AddOutputParams();
            AddOutputParam("灰度图", "HImage", DispImage);
        }
        #endregion

        #region Prop
        private LinkVarModel _InputImageLink = new LinkVarModel();
        /// <summary>
        /// 输入深度图
        /// </summary>
        public LinkVarModel InputImageLink
        {
            get { return _InputImageLink; }
            set { _InputImageLink = value; RaisePropertyChanged(); }
        }

        private string _ResultImageType = "8U";
        /// <summary>
        /// 结果图像类型
        /// </summary>
        public string ResultImageType
        {
            get { return _ResultImageType; }
            set { _ResultImageType = value; RaisePropertyChanged(); }
        }

        [NonSerialized]
        private ObservableCollection<string> _ImageTypeList = new ObservableCollection<string>() { "8U", "16U" };
        /// <summary>
        /// 图像类型列表
        /// </summary>
        public ObservableCollection<string> ImageTypeList
        {
            get { return _ImageTypeList; }
            set { _ImageTypeList = value; RaisePropertyChanged(); }
        }

        private bool _IsLimitValue = false;
        /// <summary>
        /// 是否限制深度值范围
        /// </summary>
        public bool IsLimitValue
        {
            get { return _IsLimitValue; }
            set { _IsLimitValue = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _MinZValue = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 深度上最小Z值
        /// </summary>
        public LinkVarModel MinZValue
        {
            get { return _MinZValue; }
            set { _MinZValue = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _MaxZValue = new LinkVarModel() { Text = "255" };
        /// <summary>
        /// 深度上最大Z值
        /// </summary>
        public LinkVarModel MaxZValue
        {
            get { return _MaxZValue; }
            set { _MaxZValue = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _MinGrayValue = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 对应图像最小灰度值
        /// </summary>
        public LinkVarModel MinGrayValue
        {
            get { return _MinGrayValue; }
            set { _MinGrayValue = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _MaxGrayValue = new LinkVarModel() { Text = "255" };
        /// <summary>
        /// 对应图像最大灰度值
        /// </summary>
        public LinkVarModel MaxGrayValue
        {
            get { return _MaxGrayValue; }
            set { _MaxGrayValue = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _FilterInvalidValue = new LinkVarModel();
        /// <summary>
        /// 滤掉无效值（小于该值的像素被过滤）
        /// </summary>
        public LinkVarModel FilterInvalidValue
        {
            get { return _FilterInvalidValue; }
            set { _FilterInvalidValue = value; RaisePropertyChanged(); }
        }

        private double _ResolutionZ = 1.0;
        /// <summary>
        /// Z分辨率（像素当量，像素值 × ResolutionZ = 实际物理高度）
        /// </summary>
        public double ResolutionZ
        {
            get { return _ResolutionZ; }
            set { _ResolutionZ = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _SubtractHeight = new LinkVarModel();
        /// <summary>
        /// 减去高度值（映射前先减去该偏移值）
        /// </summary>
        public LinkVarModel SubtractHeight
        {
            get { return _SubtractHeight; }
            set { _SubtractHeight = value; RaisePropertyChanged(); }
        }

        private double _HistoMin;
        public double HistoMin
        {
            get { return _HistoMin; }
            set { _HistoMin = value; RaisePropertyChanged(); }
        }

        private double _HistoMax;
        public double HistoMax
        {
            get { return _HistoMax; }
            set { _HistoMax = value; RaisePropertyChanged(); }
        }

        private double _HistoMean;
        public double HistoMean
        {
            get { return _HistoMean; }
            set { _HistoMean = value; RaisePropertyChanged(); }
        }

        private double _HistoStdDev;
        public double HistoStdDev
        {
            get { return _HistoStdDev; }
            set { _HistoStdDev = value; RaisePropertyChanged(); }
        }

        [NonSerialized]
        private PointCollection _HistoPoints = new PointCollection();
        public PointCollection HistoPoints
        {
            get { return _HistoPoints; }
            set { _HistoPoints = value; RaisePropertyChanged(); }
        }

        [NonSerialized]
        private PointCollection _HistoFillPoints = new PointCollection();
        public PointCollection HistoFillPoints
        {
            get { return _HistoFillPoints; }
            set { _HistoFillPoints = value; RaisePropertyChanged(); }
        }

        private double _HistoMaxCount;
        public double HistoMaxCount
        {
            get { return _HistoMaxCount; }
            set { _HistoMaxCount = value; RaisePropertyChanged(); }
        }

        private double _HistoCenterX;
        public double HistoCenterX
        {
            get { return _HistoCenterX; }
            set { _HistoCenterX = value; RaisePropertyChanged(); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as DepthToGrayView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
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
                        var view = this.ModuleView as DepthToGrayView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLink.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "MinZValue":
                    MinZValue.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "MaxZValue":
                    MaxZValue.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "MinGrayValue":
                    MinGrayValue.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "MaxGrayValue":
                    MaxGrayValue.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "FilterInvalidValue":
                    FilterInvalidValue.Text = obj.LinkName;
                    ExeModule();
                    break;
                case "SubtractHeight":
                    SubtractHeight.Text = obj.LinkName;
                    ExeModule();
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
                            case eLinkCommand.MinZValue:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MinZValue");
                                break;
                            case eLinkCommand.MaxZValue:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MaxZValue");
                                break;
                            case eLinkCommand.MinGrayValue:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MinGrayValue");
                                break;
                            case eLinkCommand.MaxGrayValue:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},MaxGrayValue");
                                break;
                            case eLinkCommand.FilterInvalidValue:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},FilterInvalidValue");
                                break;
                            case eLinkCommand.SubtractHeight:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},SubtractHeight");
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion

        #region Method
        public override void ShowHRoi()
        {
            var imageSnapshot = DispImage;
            if (imageSnapshot == null || !imageSnapshot.IsInitialized())
                return;

            var view = ModuleView as DepthToGrayView;
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
                    if (mWindowH != null)
                    {
                        mWindowH.ClearWindow();
                        mWindowH.Image = new RImage(DispImage);

                        foreach (HRoi roi in DispImage.mHRoi)
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

        private void ShowHistogram(HImage inputImage, HObject validRegion = null)
        {
            try
            {
                // 1. 统计信息（仅统计有效区域）
                HTuple minVal, maxVal, meanVal, devVal;
                if (validRegion != null && validRegion.IsInitialized())
                {
                    HOperatorSet.MinMaxGray(validRegion, inputImage, 0, out minVal, out maxVal, out HTuple _);
                    HOperatorSet.Intensity(validRegion, inputImage, out meanVal, out devVal);
                }
                else
                {
                    HOperatorSet.MinMaxGray(inputImage, inputImage, 0, out minVal, out maxVal, out HTuple _);
                    HOperatorSet.Intensity(inputImage, inputImage, out meanVal, out devVal);
                }

                // inputImage 已经是物理高度（ExeModule里已乘过ResolutionZ），不再重复缩放
                HistoMin = minVal.D;
                HistoMax = maxVal.D;
                HistoMean = meanVal.D;
                HistoStdDev = devVal.D;

                // 2. 生成 byte 图像用于直方图统计（只对有效区域做，避免无效值压缩动态范围）
                HImage histoImage;
                if (validRegion != null && validRegion.IsInitialized())
                {
                    HOperatorSet.ReduceDomain(inputImage, validRegion, out HObject reducedObj);
                    histoImage = new HImage(reducedObj);
                    reducedObj.Dispose();
                }
                else
                {
                    histoImage = new HImage(inputImage);
                }

                double range = maxVal.D - minVal.D;
                if (Math.Abs(range) < 1e-10) range = 1;
                double mult = 255.0 / range;
                double add = -mult * minVal.D;
                HOperatorSet.ScaleImage(histoImage, out HObject normObj, mult, add);
                HOperatorSet.ConvertImageType(normObj, out HObject byteObj, "byte");
                HImage byteImage = new HImage(byteObj);
                normObj.Dispose();
                byteObj.Dispose();
                histoImage.Dispose();

                // 3. 统计直方图
                HOperatorSet.GetDomain(byteImage, out HObject domain);
                HOperatorSet.GrayHisto(domain, byteImage, out HTuple absHisto, out HTuple relHisto);
                domain.Dispose();
                byteImage.Dispose();

                // 4. 生成 WPF 折线点集（X轴以平均值为起点，半十字叉方式）
                int bins = absHisto.Length;
                double maxCount = absHisto.TupleMax().D;
                HistoMaxCount = maxCount;
                if (maxCount < 1) maxCount = 1;

                double canvasW = 330.0;
                double canvasH = 170.0;
                double centerX = canvasW / 2.0;
                double maxDev = Math.Max(Math.Abs(HistoMean - HistoMin), Math.Abs(HistoMax - HistoMean));
                if (maxDev < 1e-10) maxDev = 1;
                HistoCenterX = centerX;

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var linePoints = new PointCollection();
                    var fillPoints = new PointCollection();

                    for (int i = 0; i < bins; i++)
                    {
                        double binValue = HistoMin + i * (HistoMax - HistoMin) / (bins - 1);
                        double dev = binValue - HistoMean;
                        double x = centerX + dev / maxDev * centerX;
                        double y = canvasH - (absHisto[i].D / maxCount) * canvasH;
                        linePoints.Add(new System.Windows.Point(x, y));
                        fillPoints.Add(new System.Windows.Point(x, y));
                    }
                    // 填充区域：折线 + 底部两侧
                    fillPoints.Add(new System.Windows.Point(canvasW, canvasH));
                    fillPoints.Add(new System.Windows.Point(0, canvasH));

                    HistoPoints = linePoints;
                    HistoFillPoints = fillPoints;
                }));
            }
            catch (Exception ex)
            {
                Logger.AddLog($"高度分布统计异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLink"] = InputImageLink?.Text ?? "";
            obj["ResultImageType"] = ResultImageType ?? "8U";
            obj["IsLimitValue"] = IsLimitValue;
            obj["MinZValue"] = MinZValue?.Text ?? "0";
            obj["MaxZValue"] = MaxZValue?.Text ?? "255";
            obj["MinGrayValue"] = MinGrayValue?.Text ?? "0";
            obj["MaxGrayValue"] = MaxGrayValue?.Text ?? "255";
            obj["FilterInvalidValue"] = FilterInvalidValue?.Text ?? "";
            obj["ResolutionZ"] = ResolutionZ;
            obj["SubtractHeight"] = SubtractHeight?.Text ?? "";
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLink"] != null && InputImageLink != null)
                    InputImageLink.Text = obj["InputImageLink"].ToString();
                if (obj["ResultImageType"] != null)
                    ResultImageType = obj["ResultImageType"].ToString();
                if (obj["IsLimitValue"] != null)
                    IsLimitValue = obj["IsLimitValue"].Value<bool>();
                if (obj["MinZValue"] != null && MinZValue != null)
                    MinZValue.Text = obj["MinZValue"].ToString();
                if (obj["MaxZValue"] != null && MaxZValue != null)
                    MaxZValue.Text = obj["MaxZValue"].ToString();
                if (obj["MinGrayValue"] != null && MinGrayValue != null)
                    MinGrayValue.Text = obj["MinGrayValue"].ToString();
                if (obj["MaxGrayValue"] != null && MaxGrayValue != null)
                    MaxGrayValue.Text = obj["MaxGrayValue"].ToString();
                if (obj["FilterInvalidValue"] != null && FilterInvalidValue != null)
                    FilterInvalidValue.Text = obj["FilterInvalidValue"].ToString();
                if (obj["ResolutionZ"] != null)
                    ResolutionZ = obj["ResolutionZ"].Value<double>();
                if (obj["SubtractHeight"] != null && SubtractHeight != null)
                    SubtractHeight.Text = obj["SubtractHeight"].ToString();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"DepthToGrayViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
