using DMSkin.Socket;
using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.GrabImage.Model;
using Plugin.ColorRecognition.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using System.Xml.Serialization;

namespace Plugin.ColorRecognition.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum,
        UpperLeftX,
        UpperLeftY,
        LowRightX,
        LowRightY,
    }

    public enum eOperateCommand
    {
        StartLearn,
        Edit,
        EndLearn,
        Cancel
    }
    #endregion

    [Category("检测识别")]
    [DisplayName("颜色识别")]
    [ModuleImageName("ColorRecognition")]
    [Serializable]
    public class ColorRecognitionViewModel : ModuleBase, IDisposable
    {
        #region 构造函数和析构函数
        public ColorRecognitionViewModel()
        {
            InitializeFields();
        }

        ~ColorRecognitionViewModel()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 释放托管资源
            }

            // 释放非托管资源
            if (_colorThresholdRegion != null && _colorThresholdRegion.IsInitialized())
            {
                _colorThresholdRegion.Dispose();
                _colorThresholdRegion = null;
            }

            if (_previewImage != null && _previewImage.IsInitialized())
            {
                _previewImage.Dispose();
                _previewImage = null;
            }

            if (_previewWindowH != null)
            {
                _previewWindowH = null;
            }
        }

        private void InitializeFields()
        {
            _roiList = new Dictionary<string, ROI>();
            _mathCoord = new Coord_Info();
            _mathCoord.Score = 0;
            _colorThresholdRegion = new HObject();
            _previewImage = new HObject();

            _IsTrained = false;
            LearningStatusText = "未学习";
            LearningStatusColor = "Gray";
        }
        #endregion

        #region 属性定义

        [NonSerialized]
        private Coord_Info _mathCoord = new Coord_Info();
        public Coord_Info MathCoord
        {
            get => _mathCoord;
            set => Set(ref _mathCoord, value);
        }

        [NonSerialized]
        private Dictionary<string, ROI> _roiList = new Dictionary<string, ROI>();
        public Dictionary<string, ROI> RoiList
        {
            get
            {
                if (_roiList == null) _roiList = new Dictionary<string, ROI>();
                return _roiList;
            }
            set => Set(ref _roiList, value);
        }

        private byte[] _colorThresholdRegionData;
        public byte[] ColorThresholdRegionData
        {
            get => _colorThresholdRegionData;
            set
            {
                if (_colorThresholdRegionData != value)
                {
                    _colorThresholdRegionData = value;
                    RaisePropertyChanged(nameof(ColorThresholdRegionData));
                    if (value != null && value.Length > 0)
                        _colorThresholdRegionNeedsDeserialize = true;
                }
            }
        }

        [NonSerialized]
        private HObject _colorThresholdRegion;
        [NonSerialized]
        private bool _colorThresholdRegionNeedsDeserialize = false;

        [XmlIgnore]
        public HObject ColorThresholdRegion
        {
            get
            {
                if ((_colorThresholdRegion == null || !_colorThresholdRegion.IsInitialized() || _colorThresholdRegionNeedsDeserialize)
                    && ColorThresholdRegionData != null && ColorThresholdRegionData.Length > 0)
                {
                    DeserializeColorThresholdRegion();
                    _colorThresholdRegionNeedsDeserialize = false;
                }

                // 若反序列化后仍无效，确保状态同步
                if (_colorThresholdRegion == null || !_colorThresholdRegion.IsInitialized())
                {
                    if (_IsTrained)
                    {
                        _IsTrained = false;
                        RaisePropertyChanged(nameof(IsTrained));
                        UpdateLearningStatus();
                    }
                }

                return _colorThresholdRegion;
            }
            set
            {
                if (_colorThresholdRegion != value)
                {
                    if (_colorThresholdRegion != null && _colorThresholdRegion.IsInitialized())
                        _colorThresholdRegion.Dispose();

                    _colorThresholdRegion = value;

                    if (value != null && value.IsInitialized())
                    {
                        SerializeColorThresholdRegion();
                        _IsTrained = true;
                        RaisePropertyChanged(nameof(IsTrained));
                    }
                    else
                    {
                        ColorThresholdRegionData = null;
                        _IsTrained = false;
                        RaisePropertyChanged(nameof(IsTrained));
                    }

                    UpdateLearningStatus();
                    RaisePropertyChanged(nameof(ColorThresholdRegion));
                }
            }
        }

        private bool _IsLearning = false;
        public bool IsLearning
        {
            get => _IsLearning;
            set => Set(ref _IsLearning, value);
        }

        private double _MinScore = 0.7;
        public double MinScore
        {
            get => _MinScore;
            set => Set(ref _MinScore, value);
        }

        private bool _ShowSearchRegion = true;
        public bool ShowSearchRegion
        {
            get => _ShowSearchRegion;
            set => Set(ref _ShowSearchRegion, value);
        }

        private bool _ShowResultContour = true;
        public bool ShowResultContour
        {
            get => _ShowResultContour;
            set => Set(ref _ShowResultContour, value);
        }

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get => _InputImageLinkText;
            set
            {
                if (_InputImageLinkText != value)
                {
                    _InputImageLinkText = value;
                    RaisePropertyChanged();
                    GetDispImage(InputImageLinkText);
                    if (DispImage != null && DispImage.IsInitialized())
                        ShowHRoi();
                }
            }
        }

        private bool _IsTrained = false;
        public bool IsTrained
        {
            get => _IsTrained;
            set
            {
                if (_IsTrained != value)
                {
                    _IsTrained = value;
                    RaisePropertyChanged(nameof(IsTrained));
                    UpdateLearningStatus();
                }
            }
        }

        private void UpdateLearningStatus()
        {
            if (_IsTrained)
            {
                LearningStatusText = "已学习";
                LearningStatusColor = "Green";
            }
            else
            {
                LearningStatusText = "未学习";
                LearningStatusColor = "Gray";
            }
            RaisePropertyChanged(nameof(LearningStatusText));
            RaisePropertyChanged(nameof(LearningStatusColor));
        }

        public double ScoreValue => MathCoord.Score;

        private Rectangle1Model _LearningRegion;
        public Rectangle1Model LearningRegion
        {
            get
            {
                if (_LearningRegion == null) _LearningRegion = new Rectangle1Model();
                return _LearningRegion;
            }
            set => Set(ref _LearningRegion, value);
        }

        private string _learningStatusText = "未学习";
        public string LearningStatusText
        {
            get => _learningStatusText;
            set => Set(ref _learningStatusText, value);
        }

        private string _learningStatusColor = "Gray";
        public string LearningStatusColor
        {
            get => _learningStatusColor;
            set => Set(ref _learningStatusColor, value);
        }

        [NonSerialized]
        private VMHWindowControl _previewWindowH;
        [Browsable(false)]
        public VMHWindowControl PreviewWindowH
        {
            get => _previewWindowH;
            set => Set(ref _previewWindowH, value);
        }

        private bool _hasPreviewImage = false;
        public bool HasPreviewImage
        {
            get => _hasPreviewImage;
            set => Set(ref _hasPreviewImage, value);
        }

        private byte[] _previewImageData;
        public byte[] PreviewImageData
        {
            get => _previewImageData;
            set => Set(ref _previewImageData, value);
        }

        [NonSerialized]
        private HObject _previewImage;
        [XmlIgnore]
        public HObject PreviewImage
        {
            get
            {
                if ((_previewImage == null || !_previewImage.IsInitialized())
                    && PreviewImageData != null && PreviewImageData.Length > 0)
                {
                    DeserializePreviewImage();
                }
                return _previewImage;
            }
            set
            {
                if (_previewImage != value)
                {
                    if (_previewImage != null && _previewImage.IsInitialized())
                        _previewImage.Dispose();

                    _previewImage = value;

                    if (value != null && value.IsInitialized())
                    {
                        SerializePreviewImage();
                        HasPreviewImage = true;
                    }
                    else
                    {
                        PreviewImageData = null;
                        HasPreviewImage = false;
                    }

                    RaisePropertyChanged(nameof(PreviewImage));
                    RaisePropertyChanged(nameof(HasPreviewImage));
                }
            }
        }

        private Rectangle1Model _Rectangle1SearchRegion;
        public Rectangle1Model Rectangle1SearchRegion
        {
            get
            {
                if (_Rectangle1SearchRegion == null)
                    _Rectangle1SearchRegion = new Rectangle1Model();
                return _Rectangle1SearchRegion;
            }
            set => Set(ref _Rectangle1SearchRegion, value);
        }

        #endregion

        #region 序列化和反序列化方法

        private void SerializeColorThresholdRegion()
        {
            try
            {
                if (_colorThresholdRegion != null && _colorThresholdRegion.IsInitialized())
                {
                    string tempFile = Path.GetTempFileName() + ".reg";
                    try
                    {
                        HOperatorSet.WriteRegion(_colorThresholdRegion, tempFile);
                        ColorThresholdRegionData = File.ReadAllBytes(tempFile);
                        Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色阈值区域序列化成功，数据大小: {ColorThresholdRegionData.Length} bytes", eMsgType.Info);
                    }
                    finally
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                    }
                }
                else
                {
                    Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色阈值区域未初始化，无法序列化", eMsgType.Warn);
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"序列化颜色阈值区域失败: {ex.Message}", eMsgType.Warn);
            }
        }

        private void DeserializeColorThresholdRegion()
        {
            try
            {
                if (ColorThresholdRegionData != null && ColorThresholdRegionData.Length > 0)
                {
                    string tempFile = Path.GetTempFileName() + ".reg";
                    try
                    {
                        File.WriteAllBytes(tempFile, ColorThresholdRegionData);
                        HOperatorSet.ReadRegion(out HObject newRegion, tempFile);

                        if (newRegion != null && newRegion.IsInitialized())
                        {
                            HOperatorSet.AreaCenter(newRegion, out HTuple area, out _, out _);
                            if (area.D > 0)
                            {
                                if (_colorThresholdRegion != null && _colorThresholdRegion.IsInitialized())
                                    _colorThresholdRegion.Dispose();

                                _colorThresholdRegion = newRegion;
                                _IsTrained = true;
                                Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色阈值区域反序列化成功，区域面积: {area.D}", eMsgType.Info);
                            }
                            else
                            {
                                newRegion.Dispose();
                                _colorThresholdRegion = null;
                                _IsTrained = false;
                                Logger.AddLog($"[{ModuleParam.ModuleName}] 反序列化区域面积为零，视为无效", eMsgType.Warn);
                            }
                        }
                        else
                        {
                            _colorThresholdRegion = null;
                            _IsTrained = false;
                            Logger.AddLog($"[{ModuleParam.ModuleName}] 反序列化失败：读取的区域对象无效", eMsgType.Warn);
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                    }
                }
                else
                {
                    Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色阈值区域数据为空，无法反序列化", eMsgType.Info);
                    _colorThresholdRegion = null;
                    _IsTrained = false;
                }

                RaisePropertyChanged(nameof(IsTrained));
                UpdateLearningStatus();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"[{ModuleParam.ModuleName}] 反序列化颜色阈值区域失败: {ex.Message}", eMsgType.Warn);
                _colorThresholdRegion = null;
                _IsTrained = false;
                RaisePropertyChanged(nameof(IsTrained));
                UpdateLearningStatus();
            }
        }

        private void SerializePreviewImage()
        {
            try
            {
                if (_previewImage != null && _previewImage.IsInitialized())
                {
                    string tempFile = Path.GetTempFileName() + ".bmp";
                    try
                    {
                        HOperatorSet.WriteImage(_previewImage, "bmp", 0, tempFile);
                        PreviewImageData = File.ReadAllBytes(tempFile);
                        HasPreviewImage = true;
                    }
                    finally
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"序列化预览图像失败: {ex.Message}", eMsgType.Warn);
            }
        }

        private void DeserializePreviewImage()
        {
            try
            {
                if (PreviewImageData != null && PreviewImageData.Length > 0)
                {
                    string tempFile = Path.GetTempFileName() + ".bmp";
                    try
                    {
                        File.WriteAllBytes(tempFile, PreviewImageData);
                        HOperatorSet.ReadImage(out _previewImage, tempFile);

                        if (_previewImage != null && _previewImage.IsInitialized())
                        {
                            _hasPreviewImage = true;
                            RaisePropertyChanged(nameof(HasPreviewImage));
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"反序列化预览图像失败: {ex.Message}", eMsgType.Warn);
                _previewImage = null;
                _hasPreviewImage = false;
                RaisePropertyChanged(nameof(HasPreviewImage));
            }
        }

        #endregion

        #region Override Methods

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0) return;
            if (InputImageLinkText == null)
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        // 确保颜色区域有效
        private bool EnsureColorRegionValid()
        {
            try
            {
                var region = ColorThresholdRegion; // 触发 getter
                if (region == null || !region.IsInitialized()) return false;
                HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
                return area.D > 0;
            }
            catch
            {
                return false;
            }
        }

        // 从 Rectangle1SearchRegion 创建搜索区域（不依赖 _roiList）
        private HObject GetSearchRegion(HImage image)
        {
            HObject searchRegion = null;
            try
            {
                if (Rectangle1SearchRegion != null &&
                    Rectangle1SearchRegion.Row1 < Rectangle1SearchRegion.Row2 &&
                    Rectangle1SearchRegion.Col1 < Rectangle1SearchRegion.Col2)
                {
                    searchRegion = new HRegion(
                        Rectangle1SearchRegion.Row1, Rectangle1SearchRegion.Col1,
                        Rectangle1SearchRegion.Row2, Rectangle1SearchRegion.Col2);
                }
                else
                {
                    HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                    double row1 = height.D * 0.1;
                    double col1 = width.D * 0.1;
                    double row2 = height.D * 0.9;
                    double col2 = width.D * 0.9;

                    row1 = Math.Max(0, row1);
                    col1 = Math.Max(0, col1);
                    row2 = Math.Min(height.D, row2);
                    col2 = Math.Min(width.D, col2);
                    if (row2 <= row1) row2 = row1 + 10;
                    if (col2 <= col1) col2 = col1 + 10;

                    searchRegion = new HRegion(row1, col1, row2, col2);
                    Logger.AddLog($"[{ModuleParam.ModuleName}] 创建默认搜索区域: ({row1}, {col1}, {row2}, {col2})", eMsgType.Info);
                }
                return searchRegion;
            }
            catch (Exception ex)
            {
                searchRegion?.Dispose();
                Logger.AddLog($"[{ModuleParam.ModuleName}] 创建搜索区域失败: {ex.Message}", eMsgType.Error);
                return null;
            }
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    Logger.AddLog($"流程执行[{ModuleParam.ModuleName}]失败：未链接图像源！", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    Logger.AddLog($"流程执行[{ModuleParam.ModuleName}]失败：图像未获取或未初始化！", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 1. 验证颜色区域
                if (!EnsureColorRegionValid())
                {
                    Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色阈值区域未初始化或无效，请先进行颜色学习！", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 2. 获取搜索区域
                using (HObject searchRegion = GetSearchRegion(DispImage))
                {
                    if (searchRegion == null || !searchRegion.IsInitialized())
                    {
                        Logger.AddLog($"[{ModuleParam.ModuleName}] 无法获取有效的搜索区域", eMsgType.Warn);
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    try
                    {
                        HOperatorSet.AreaCenter(searchRegion, out HTuple searchAreaTuple, out _, out _);
                        double totalSearchArea = searchAreaTuple.Length > 0 ? searchAreaTuple.D : 0;
                        if (totalSearchArea <= 0)
                        {
                            Logger.AddLog($"[{ModuleParam.ModuleName}] 搜索区域面积为零", eMsgType.Warn);
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }

                        HOperatorSet.Intersection(searchRegion, ColorThresholdRegion, out HObject resultRegion);

                        HOperatorSet.AreaCenter(resultRegion, out HTuple resultAreaTuple, out HTuple row, out HTuple col);
                        double resultArea = resultAreaTuple.Length > 0 ? resultAreaTuple.D : 0;

                        double score = resultArea / totalSearchArea;

                        if (score >= MinScore)
                        {
                            var tempCoord = MathCoord;
                            tempCoord.Score = score;
                            tempCoord.Status = true;
                            if (row.Length > 0 && col.Length > 0)
                            {
                                tempCoord.X = col[0].D;
                                tempCoord.Y = row[0].D;
                            }
                            MathCoord = tempCoord;
                            RaisePropertyChanged(nameof(ScoreValue));

                            if (ShowResultContour && resultRegion != null && resultRegion.IsInitialized())
                            {
                                ShowHRoi(new HRoi(
                                    ModuleParam.ModuleEncode,
                                    ModuleParam.ModuleName,
                                    "检测区域",
                                    HRoiType.检测结果,
                                    "green",
                                    new HObject(resultRegion)
                                ));
                            }

                            ChangeModuleRunStatus(eRunStatus.OK);
                            return true;
                        }
                        else
                        {
                            var tempCoord = MathCoord;
                            tempCoord.Status = false;
                            tempCoord.Score = 0;
                            MathCoord = tempCoord;
                            RaisePropertyChanged(nameof(ScoreValue));

                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }
                    }
                    finally
                    {
                        // resultRegion 已在 if 中可能传递给 ShowHRoi，不在此释放
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
                ChangeModuleRunStatus(eRunStatus.NG);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            AddOutputParam("X", "double", Math.Round(MathCoord.X, 3));
            AddOutputParam("Y", "double", Math.Round(MathCoord.Y, 3));
            AddOutputParam("Score", "double", Math.Round(MathCoord.Score, 3));
        }

        #endregion

        #region Commands

        [NonSerialized]
        private CommandBase _OperateCommand;
        [Browsable(false)]
        public CommandBase OperateCommand
        {
            get
            {
                if (_OperateCommand == null)
                {
                    _OperateCommand = new CommandBase(obj =>
                    {
                        try
                        {
                            switch ((eOperateCommand)obj)
                            {
                                case eOperateCommand.StartLearn:
                                    StartLearn();
                                    break;
                                case eOperateCommand.EndLearn:
                                    ExecuteEndLearn();
                                    break;
                                case eOperateCommand.Cancel:
                                    IsLearning = false;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.GetExceptionMsg(ex);
                        }
                    });
                }
                return _OperateCommand;
            }
        }

        private void StartLearn()
        {
            IsLearning = true;
            if (DispImage == null || !DispImage.IsInitialized())
            {
                MessageView.Ins.MessageBoxShow("图像不能为空！");
                return;
            }

            var view = ModuleView as ColorRecognitionView;
            if (view == null) return;

            view.mWindowH.HobjectToHimage(DispImage);

            string templetKey = ModuleParam.ModuleName + ROIDefine.Templet;

            if (_roiList == null) _roiList = new Dictionary<string, ROI>();

            if (_roiList.ContainsKey(templetKey))
            {
                var roi = (ROIRectangle1)_roiList[templetKey];
                view.mWindowH.WindowH.genRect1(
                    templetKey,
                    roi.row1,
                    roi.col1,
                    roi.row2,
                    roi.col2,
                    ref _roiList
                );
            }
            else
            {
                int height = (int)view.mWindowH.hv_imageHeight;
                int width = (int)view.mWindowH.hv_imageWidth;
                view.mWindowH.WindowH.genRect1(
                    templetKey,
                    height / 2 - 50,
                    width / 2 - 50,
                    height / 2 + 50,
                    width / 2 + 50,
                    ref _roiList
                );
            }

            RaisePropertyChanged(nameof(RoiList));
        }

        private void ExecuteEndLearn()
        {
            try
            {
                IsLearning = false;

                if (_roiList == null) _roiList = new Dictionary<string, ROI>();

                string key = ModuleParam.ModuleName + ROIDefine.Templet;
                if (!_roiList.TryGetValue(key, out ROI learnRoi))
                {
                    MessageView.Ins.MessageBoxShow("请先绘制学习区域！");
                    return;
                }

                HOperatorSet.CountChannels(DispImage, out HTuple channels);
                if (channels.D != 3)
                {
                    MessageView.Ins.MessageBoxShow("颜色学习需要彩色图像（RGB）！");
                    return;
                }

                HObject learnRegion = learnRoi.GetRegion();

                HOperatorSet.TransFromRgb(
                    DispImage, DispImage, DispImage,
                    out HObject fullH, out HObject fullS, out HObject fullV,
                    "hsv"
                );

                HOperatorSet.Intensity(learnRegion, fullH, out HTuple hMean, out HTuple hDev);
                HOperatorSet.Intensity(learnRegion, fullS, out HTuple sMean, out HTuple sDev);
                HOperatorSet.Intensity(learnRegion, fullV, out HTuple vMean, out HTuple vDev);

                double h = hMean.D;
                double s = sMean.D;
                double v = vMean.D;

                double hTol = 15;
                double sTol = 30;
                double vTol = 40;

                HOperatorSet.Threshold(fullH, out HObject hReg, h - hTol, h + hTol);
                HOperatorSet.Threshold(fullS, out HObject sReg, s - sTol, s + sTol);
                HOperatorSet.Threshold(fullV, out HObject vReg, v - vTol, v + vTol);

                HOperatorSet.Intersection(hReg, sReg, out HObject hsReg);
                HOperatorSet.Intersection(hsReg, vReg, out HObject colorRegion);

                ColorThresholdRegion = colorRegion.Clone();
                IsTrained = true;
                UpdateLearningStatus();

                CreatePreviewImage(learnRoi);
                RefreshMainWindow(learnRoi);

                MessageView.Ins.MessageBoxShow($"颜色学习完成！\n学习区域图像已显示在预览窗口。");

                learnRegion.Dispose();
                fullH.Dispose();
                fullS.Dispose();
                fullV.Dispose();
                hReg.Dispose();
                sReg.Dispose();
                vReg.Dispose();
                hsReg.Dispose();
                colorRegion.Dispose();
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
                MessageView.Ins.MessageBoxShow($"颜色学习失败：{ex.Message}");
            }
        }

        private void CreatePreviewImage(ROI learnRoi)
        {
            try
            {
                if (DispImage == null || !DispImage.IsInitialized() || learnRoi == null)
                {
                    HasPreviewImage = false;
                    return;
                }

                HObject learnRegion = learnRoi.GetRegion();
                HOperatorSet.ReduceDomain(DispImage, learnRegion, out HObject imageReduced);
                HOperatorSet.CropDomain(imageReduced, out HObject croppedImage);
                HOperatorSet.ZoomImageSize(croppedImage, out HObject previewImage, 240, 200, "constant");

                PreviewImage = previewImage;
                HasPreviewImage = true;
                UpdatePreviewWindow(previewImage);

                learnRegion.Dispose();
                imageReduced.Dispose();
                croppedImage.Dispose();

                RaisePropertyChanged(nameof(HasPreviewImage));
                RaisePropertyChanged(nameof(PreviewImage));
            }
            catch (Exception ex)
            {
                HasPreviewImage = false;
                Logger.AddLog($"创建预览图像失败: {ex.Message}", eMsgType.Warn);
            }
        }

        private void UpdatePreviewWindow(HObject previewImage)
        {
            try
            {
                var view = ModuleView as ColorRecognitionView;
                if (view == null || view.previewHost == null || previewImage == null || !previewImage.IsInitialized())
                    return;

                if (PreviewWindowH == null)
                {
                    PreviewWindowH = new VMHWindowControl();
                    PreviewWindowH.hControl.Width = 240;
                    PreviewWindowH.hControl.Height = 200;
                    if (view.previewHost.Child == null)
                        view.previewHost.Child = PreviewWindowH;
                }

                if (PreviewWindowH != null)
                {
                    PreviewWindowH.ClearWindow();
                    PreviewWindowH.HobjectToHimage(previewImage);

                    HOperatorSet.GetImageSize(previewImage, out HTuple width, out HTuple height);
                    if (width.D > 0 && height.D > 0)
                    {
                        double controlWidth = 240;
                        double controlHeight = 200;
                        double scaleX = controlWidth / width.D;
                        double scaleY = controlHeight / height.D;
                        double scale = Math.Min(scaleX, scaleY);
                        double scaledWidth = width.D * scale;
                        double scaledHeight = height.D * scale;
                        double offsetX = (controlWidth - scaledWidth) / 2.0;
                        double offsetY = (controlHeight - scaledHeight) / 2.0;

                        PreviewWindowH.hControl.HalconWindow.SetPart(
                            -offsetY,
                            -offsetX,
                            scaledHeight + offsetY,
                            scaledWidth + offsetX
                        );
                    }

                    HOperatorSet.CountChannels(previewImage, out HTuple channels);
                    if (channels.D == 3)
                    {
                        HImage colorImage = new HImage(previewImage);
                        PreviewWindowH.hControl.HalconWindow.DispColor(colorImage);
                    }
                    else if (channels.D == 1)
                    {
                        PreviewWindowH.hControl.HalconWindow.DispObj(previewImage);
                    }

                    PreviewWindowH.hControl.HalconWindow.FlushBuffer();
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"更新预览窗口失败: {ex.Message}", eMsgType.Warn);
            }
        }

        private void RefreshMainWindow(ROI learnRoi)
        {
            try
            {
                var view = ModuleView as ColorRecognitionView;
                if (view == null || view.mWindowH == null) return;

                view.mWindowH.HobjectToHimage(DispImage);
                ShowHRoi();

                if (ColorThresholdRegion != null && ColorThresholdRegion.IsInitialized())
                {
                    view.mWindowH.hControl.HalconWindow.SetColor("green");
                    view.mWindowH.hControl.HalconWindow.SetDraw("margin");
                    view.mWindowH.hControl.HalconWindow.DispObj(ColorThresholdRegion);
                }

                HObject learnRegionObj = learnRoi.GetRegion();
                view.mWindowH.hControl.HalconWindow.SetColor("yellow");
                view.mWindowH.hControl.HalconWindow.SetDraw("margin");
                view.mWindowH.hControl.HalconWindow.DispObj(learnRegionObj);
                learnRegionObj.Dispose();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"刷新主窗口显示失败: {ex.Message}", eMsgType.Error);
            }
        }

        [NonSerialized]
        private CommandBase _confirmCommand;
        [Browsable(false)]
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_confirmCommand == null)
                {
                    _confirmCommand = new CommandBase(obj =>
                    {
                        try
                        {
                            SaveConfig();
                            CloseView();
                            Logger.AddLog($"[{ModuleParam.ModuleName}] 配置已保存", eMsgType.Info);
                        }
                        catch (Exception ex)
                        {
                            Logger.GetExceptionMsg(ex);
                            MessageView.Ins.MessageBoxShow("保存配置时发生错误！");
                        }
                    });
                }
                return _confirmCommand;
            }
        }

        [NonSerialized]
        private CommandBase _ExecuteCommand;
        [Browsable(false)]
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase(_ => ExeModule());
                }
                return _ExecuteCommand;
            }
        }

        [NonSerialized]
        private CommandBase _LinkCommand;
        [Browsable(false)]
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    EventMgr.Ins
                        .GetEvent<VarChangedEvent>()
                        .Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase(obj =>
                    {
                        switch ((eLinkCommand)obj)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }

        #endregion

        #region UI Events

        public override void Loaded()
        {
            try
            {
                base.Loaded();

                // 主动触发颜色区域反序列化
                var _ = ColorThresholdRegion;

                if (_roiList == null) _roiList = new Dictionary<string, ROI>();

                // 从 Rectangle1SearchRegion 恢复搜索区域 ROI（用于 UI 编辑）
                string searchKey = ModuleParam.ModuleName + ROIDefine.Search;
                if (Rectangle1SearchRegion != null &&
                    Rectangle1SearchRegion.Row1 < Rectangle1SearchRegion.Row2 &&
                    Rectangle1SearchRegion.Col1 < Rectangle1SearchRegion.Col2)
                {
                    if (_roiList.ContainsKey(searchKey))
                    {
                        if (_roiList[searchKey] is ROIRectangle1 rectRoi)
                        {
                            rectRoi.row1 = Rectangle1SearchRegion.Row1;
                            rectRoi.col1 = Rectangle1SearchRegion.Col1;
                            rectRoi.row2 = Rectangle1SearchRegion.Row2;
                            rectRoi.col2 = Rectangle1SearchRegion.Col2;
                        }
                        else
                        {
                            _roiList[searchKey] = new ROIRectangle1(
                                Rectangle1SearchRegion.Row1,
                                Rectangle1SearchRegion.Col1,
                                Rectangle1SearchRegion.Row2,
                                Rectangle1SearchRegion.Col2);
                        }
                    }
                    else
                    {
                        _roiList.Add(searchKey, new ROIRectangle1(
                            Rectangle1SearchRegion.Row1,
                            Rectangle1SearchRegion.Col1,
                            Rectangle1SearchRegion.Row2,
                            Rectangle1SearchRegion.Col2));
                    }
                }

                var view = ModuleView as ColorRecognitionView;
                if (view != null)
                {
                    if (view.mWindowH == null)
                    {
                        view.mWindowH = new VMHWindowControl();
                        view.winFormHost.Child = view.mWindowH;
                    }

                    if (view.previewHost != null && view.previewHost.Child == null)
                    {
                        PreviewWindowH = new VMHWindowControl();
                        PreviewWindowH.hControl.Width = 240;
                        PreviewWindowH.hControl.Height = 200;
                        view.previewHost.Child = PreviewWindowH;
                    }

                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        GetDispImage(InputImageLinkText);
                    }

                    if (DispImage != null && DispImage.IsInitialized())
                    {
                        if (view.mWindowH != null)
                        {
                            view.mWindowH.DispObj(DispImage);

                            if (view.mWindowH.hControl != null)
                            {
                                view.mWindowH.hControl.MouseUp -= HControl_MouseUp;
                                view.mWindowH.hControl.MouseMove -= HControl_MouseMove;
                                view.mWindowH.hControl.MouseWheel -= HControl_MouseWheel;
                                view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                                view.mWindowH.hControl.MouseMove += HControl_MouseMove;
                                view.mWindowH.hControl.MouseWheel += HControl_MouseWheel;
                            }

                            ShowHRoi();
                        }

                        if (IsTrained)
                        {
                            try
                            {
                                if (ColorThresholdRegion != null && ColorThresholdRegion.IsInitialized())
                                {
                                    if (view.mWindowH?.hControl?.HalconWindow != null)
                                    {
                                        view.mWindowH.hControl.HalconWindow.SetColor("green");
                                        view.mWindowH.hControl.HalconWindow.SetDraw("margin");
                                        view.mWindowH.hControl.HalconWindow.DispObj(ColorThresholdRegion);
                                    }
                                }

                                if (HasPreviewImage && PreviewImage != null && PreviewImage.IsInitialized())
                                {
                                    UpdatePreviewWindow(PreviewImage);
                                }

                                Logger.AddLog($"[{ModuleParam.ModuleName}] 颜色识别模型已加载", eMsgType.Info);
                            }
                            catch (Exception ex)
                            {
                                Logger.AddLog($"显示已学习内容失败: {ex.Message}", eMsgType.Warn);
                            }
                        }
                    }
                    else
                    {
                        Logger.AddLog($"Loaded: 图像未初始化，无法显示", eMsgType.Warn);
                    }
                }
                else
                {
                    Logger.AddLog($"Loaded: 视图未找到", eMsgType.Warn);
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"Loaded 方法出错: {ex.Message}", eMsgType.Error);
                Logger.GetExceptionMsg(ex);
            }
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as ColorRecognitionView;
                if (view == null) return;
                view.mWindowH.WindowH.smallestActiveROI(out string name, out string index);
                if (!string.IsNullOrEmpty(index) && _roiList != null && _roiList.TryGetValue(index, out ROI roi))
                {
                    ShowHText();
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }

        private void HControl_MouseMove(object sender, MouseEventArgs e) => ShowHText();
        private void HControl_MouseWheel(object sender, MouseEventArgs e) => ShowHText();

        #endregion

        #region Helper Methods

        public override void ShowHRoi()
        {
            try
            {
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    GetDispImage(InputImageLinkText);
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        Logger.AddLog("显示ROI失败：图像未初始化", eMsgType.Warn);
                        return;
                    }
                }

                var view = ModuleView as ColorRecognitionView;
                VMHWindowControl mWindowH = null;
                bool dispSearch = true;

                if (view == null || view.IsClosed)
                {
                    dispSearch = false;
                    if (DispImage != null && DispImage.IsInitialized() && DispImage.DispViewID != null)
                    {
                        mWindowH = ViewDic.GetView(DispImage.DispViewID);
                    }
                }
                else
                {
                    mWindowH = view.mWindowH;
                    if (mWindowH != null)
                    {
                        mWindowH.ClearWindow();
                        if (DispImage != null && DispImage.IsInitialized())
                        {
                            mWindowH.Image = new RImage(DispImage);
                        }
                    }
                }

                if (mWindowH == null)
                {
                    Logger.AddLog("显示ROI失败：窗口控件未初始化", eMsgType.Warn);
                    return;
                }

                if (mWindowH.hControl == null || mWindowH.hControl.HalconWindow == null)
                {
                    Logger.AddLog("显示ROI失败：Halcon窗口未初始化", eMsgType.Warn);
                    return;
                }

                string searchKey = ModuleParam.ModuleName + ROIDefine.Search;
                if (dispSearch)
                {
                    if (_roiList == null) _roiList = new Dictionary<string, ROI>();

                    var tempRoiList = _roiList;

                    if (!tempRoiList.TryGetValue(searchKey, out ROI searchRoi))
                    {
                        if (mWindowH.hv_imageHeight <= 0 || mWindowH.hv_imageWidth <= 0)
                        {
                            Logger.AddLog("显示ROI失败：图像尺寸无效", eMsgType.Warn);
                            return;
                        }

                        mWindowH.WindowH.genRect1(
                            searchKey,
                            5,
                            5,
                            Math.Max(5, mWindowH.hv_imageHeight - 5),
                            Math.Max(5, mWindowH.hv_imageWidth - 5),
                            ref tempRoiList
                        );
                    }
                    else
                    {
                        var rect = (ROIRectangle1)searchRoi;
                        mWindowH.WindowH.genRect1(
                            searchKey,
                            rect.row1,
                            rect.col1,
                            rect.row2,
                            rect.col2,
                            ref tempRoiList
                        );
                    }

                    _roiList = tempRoiList;
                    RaisePropertyChanged(nameof(RoiList));
                }

                if (mHRoi != null)
                {
                    foreach (var roi in mHRoi.Where(r => r.ModuleName == ModuleParam.ModuleName))
                    {
                        if (roi == null) continue;

                        if (roi.roiType == HRoiType.文字显示)
                        {
                            var text = (HText)roi;
                            if (text.text != null && mWindowH.hControl?.HalconWindow != null)
                            {
                                ShowTool.SetFont(mWindowH.hControl.HalconWindow, text.size, "false", "false");
                                ShowTool.SetMsg(mWindowH.hControl.HalconWindow, text.text, "image", text.row, text.col, text.drawColor, "false");
                            }
                        }
                        else if (roi.hobject != null && roi.hobject.IsInitialized())
                        {
                            if (mWindowH.hControl?.HalconWindow != null)
                            {
                                mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"ShowHRoi 方法出错: {ex.Message}", eMsgType.Error);
                Logger.GetExceptionMsg(ex);
            }
        }

        private void ShowHText()
        {
            try
            {
                var view = ModuleView as ColorRecognitionView;
                if (view == null || _roiList == null || _roiList.Count == 0 || DispImage == null) return;

                string key = ModuleParam.ModuleName + ROIDefine.Search;
                if (!_roiList.TryGetValue(key, out ROI roi)) return;

                HTuple info = roi.GetModelData();
                if (info.Length >= 4)
                {
                    Rectangle1SearchRegion.Row1 = Math.Round(info.DArr[0], 0);
                    Rectangle1SearchRegion.Col1 = Math.Round(info.DArr[1], 0);
                    Rectangle1SearchRegion.Row2 = Math.Round(info.DArr[2], 0);
                    Rectangle1SearchRegion.Col2 = Math.Round(info.DArr[3], 0);

                    ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
                    ShowTool.SetMsg(view.mWindowH.hControl.HalconWindow, "搜索区域", "image", info.DArr[1] + 5, info.DArr[0] + 5, "cyan", "false");

                    if (IsLearning && _roiList.TryGetValue(ModuleParam.ModuleName + ROIDefine.Templet, out ROI learnRoi))
                    {
                        HTuple info1 = learnRoi.GetModelData();
                        if (info1.Length >= 2)
                        {
                            ShowTool.SetMsg(view.mWindowH.hControl.HalconWindow, "学习区域", "image", info1.DArr[1], info1.DArr[0], "yellow", "false");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"ShowHText 方法出错: {ex.Message}", eMsgType.Warn);
            }
        }

        private void CloseView()
        {
            try
            {
                if (ModuleView is System.Windows.Window window)
                {
                    window.DialogResult = true;
                    window.Close();
                }
                else if (this is ModuleBase moduleBase && moduleBase.ModuleView != null)
                {
                    var parentWindow = System.Windows.Window.GetWindow(moduleBase.ModuleView as System.Windows.DependencyObject);
                    if (parentWindow != null)
                    {
                        if (parentWindow.Owner != null)
                        {
                            parentWindow.DialogResult = true;
                        }
                        parentWindow.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (_roiList == null) _roiList = new Dictionary<string, ROI>();

                string searchKey = ModuleParam.ModuleName + ROIDefine.Search;
                if (_roiList.TryGetValue(searchKey, out ROI searchRoi))
                {
                    // 可选：保存到配置
                }

                // 确保颜色区域被序列化
                if (_colorThresholdRegion != null && _colorThresholdRegion.IsInitialized())
                {
                    SerializeColorThresholdRegion();
                }

                AddOutputParams();
                Logger.AddLog($"模块[{ModuleParam.ModuleName}]配置保存成功", eMsgType.Info);
            }
            catch (Exception ex)
            {
                throw new Exception($"保存配置失败: {ex.Message}", ex);
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            if (obj.SendName.EndsWith("InputImageLink"))
                InputImageLinkText = obj.LinkName;
        }

        #endregion
    }
}