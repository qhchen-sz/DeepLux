using EventMgrLib;
using HalconDotNet;
using Plugin.Flatness.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;

namespace Plugin.Flatness.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        InitRoiCenterX,
        InitRoiCenterY,
        InitRoiLength1,
        InitRoiLength2,
        InitRoiAngel,
    }

    public enum eFitMethod
    {
        [EnumDescription("标准回归")]
        regression,
        [EnumDescription("Huber加权最小二乘")]
        huber,
        [EnumDescription("Tukey鲁棒估计")]
        tukey,
    }
    #endregion

    [Category("3D")]
    [DisplayName("平面度")]
    [ModuleImageName("Flatness")]
    [Serializable]
    public class FlatnessModel : ModuleBase
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
            HRegion domain = null;
            HImage diffImage = null;
            HObject planeImageObj = null;

            try
            {
                ClearRoiAndText();
                GetHomMat2D();

                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (!IsOpenWindows)
                {
                    GetDispImage(InputImageLinkText, true);
                }
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 逆变换：拖动 ROI 后把图像坐标转回原始坐标
                if (DisenableAffine2d && HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    DisenableAffine2d = false;
                    Aff.Affine2d(HomMat2D_Inverse, TempRoi, InitRoi);
                    if (InitRoiChanged_Flag)
                    {
                        InitRoiCenterX.Text = InitRoi.MidC.ToString();
                        InitRoiCenterY.Text = InitRoi.MidR.ToString();
                        InitRoiLength1.Text = InitRoi.Length1.ToString();
                        InitRoiLength2.Text = InitRoi.Length2.ToString();
                        InitRoiAngel.Text = InitRoi.Deg.ToString();
                    }
                }

                // 正变换：原始坐标 → 图像坐标
                if (HomMat2D != null && HomMat2D.Length > 0)
                {
                    InitRoi.MidC = Convert.ToDouble(GetLinkValue(InitRoiCenterX));
                    InitRoi.MidR = Convert.ToDouble(GetLinkValue(InitRoiCenterY));
                    InitRoi.Length1 = Convert.ToDouble(GetLinkValue(InitRoiLength1));
                    InitRoi.Length2 = Convert.ToDouble(GetLinkValue(InitRoiLength2));
                    InitRoi.Deg = Convert.ToDouble(GetLinkValue(InitRoiAngel));
                    Aff.Affine2d(HomMat2D, InitRoi, TranRoi);
                }
                else
                {
                    if (!InitRoiCenterX.Text.StartsWith("&"))
                        InitRoi.MidC = TranRoi.MidC = TempRoi.MidC;
                    else
                        InitRoi.MidC = TranRoi.MidC = TempRoi.MidC = Convert.ToDouble(GetLinkValue(InitRoiCenterX));

                    if (!InitRoiCenterY.Text.StartsWith("&"))
                        InitRoi.MidR = TranRoi.MidR = TempRoi.MidR;
                    else
                        InitRoi.MidR = TranRoi.MidR = TempRoi.MidR = Convert.ToDouble(GetLinkValue(InitRoiCenterY));

                    if (!InitRoiLength1.Text.StartsWith("&"))
                        InitRoi.Length1 = TranRoi.Length1 = TempRoi.Length1;
                    else
                        InitRoi.Length1 = TranRoi.Length1 = TempRoi.Length1 = Convert.ToDouble(GetLinkValue(InitRoiLength1));

                    if (!InitRoiLength2.Text.StartsWith("&"))
                        InitRoi.Length2 = TranRoi.Length2 = TempRoi.Length2;
                    else
                        InitRoi.Length2 = TranRoi.Length2 = TempRoi.Length2 = Convert.ToDouble(GetLinkValue(InitRoiLength2));

                    if (!InitRoiAngel.Text.StartsWith("&"))
                        InitRoi.Deg = TranRoi.Deg = TempRoi.Deg;
                    else
                        InitRoi.Deg = TranRoi.Deg = TempRoi.Deg = Convert.ToDouble(GetLinkValue(InitRoiAngel));
                }

                domain = GetRoiRegion();
                if (domain == null || !domain.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取图像通道数
                HOperatorSet.CountChannels(DispImage, out HTuple channelCount);
                int nChannels = channelCount.I;

                // ✅ 基恩士轮廓仪：固定用通道1（高度数据）
                int targetChannel = 1;
                if (targetChannel > nChannels)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception($"目标通道 {targetChannel} 超出图像通道数 {nChannels}"));
                    return false;
                }

                // 提取高度通道（Ch1）
                HOperatorSet.AccessChannel(DispImage, out HObject chObj, targetChannel);

                // 高度数据转 real 类型，确保数学运算精度
                HOperatorSet.ConvertImageType(chObj, out HObject chRealObj, "real");

                // 获取图像尺寸
                HOperatorSet.GetImageSize(chRealObj, out HTuple width, out HTuple height);

                // 拟合最佳平面（一阶）
                HOperatorSet.FitSurfaceFirstOrder(
                    domain,
                    chRealObj,
                    FitMethod.ToString(),
                    NumIterations,
                    ClippingFactor,
                    out HTuple alpha,
                    out HTuple beta,
                    out HTuple gamma
                );

                // 保存拟合参数
                Alpha = Math.Round(alpha.D, 6);
                Beta = Math.Round(beta.D, 6);
                Gamma = Math.Round(gamma.D, 6);

                // 生成拟合平面图像
                HOperatorSet.GenImageSurfaceFirstOrder(
                    out planeImageObj,
                    "real",
                    alpha,
                    beta,
                    gamma,
                    height / 2,
                    width / 2,
                    width,
                    height
                );

                // 相减得差分图像（原始高度 - 拟合平面）= 偏差
                HOperatorSet.SubImage(
                    chRealObj,
                    planeImageObj,
                    out HObject diffObj,
                    1.0,
                    0.0
                );
                diffImage = new HImage(diffObj);

                // 统计偏差：最小、最大、范围
                HOperatorSet.MinMaxGray(
                    domain,
                    diffImage,
                    0.0,
                    out HTuple minDev,
                    out HTuple maxDev,
                    out HTuple range
                );

                // 计算平面度 = 最大偏差 - 最小偏差
                Flatness = Math.Round(maxDev.D - minDev.D, 6);
                MaxDeviation = Math.Round(maxDev.D, 6);
                MinDeviation = Math.Round(minDev.D, 6);

                // 计算 RMS 误差（标准差）
                HOperatorSet.Intensity(
                    domain,
                    diffImage,
                    out HTuple meanDiff,
                    out HTuple deviation
                );
                FitError = Math.Round(deviation.D, 6);

                // 可选：Ch2 亮度通道做信号质量检查
                if (nChannels >= 2)
                {
                    HOperatorSet.AccessChannel(DispImage, out HObject ch2Obj, 2);
                    HOperatorSet.Intensity(domain, new HImage(ch2Obj), out HTuple meanIntensity, out _);
                    double avgBrightness = meanIntensity.D;
                    if (avgBrightness < 50)
                    {
                        Logger.GetExceptionMsg(new Exception($"Ch2 亮度偏低 ({avgBrightness:F1})，信号质量可能不佳"));
                    }
                    ch2Obj.Dispose();
                }

                // 显示偏差图（伪彩色）
                if (ShowDeviationMap && diffImage != null && diffImage.IsInitialized())
                {
                    double rangeVal = range.D;
                    if (rangeVal > 0)
                    {
                        HImage dispScaled = diffImage.ScaleImage(
                            255.0 / rangeVal,
                            128.0 - 128.0 * maxDev.D / rangeVal
                        );
                        DispImage = new RImage(dispScaled);
                    }
                }

                // 显示结果文字
                if (ShowResultPoint)
                {
                    HOperatorSet.AreaCenter(
                        domain,
                        out HTuple area,
                        out HTuple centerR,
                        out HTuple centerC
                    );
                    string text = $"平面度:{Flatness:F4}";
                    ShowHRoi(new HText(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.文字显示,
                        "green",
                        text,
                        centerC.D,
                        centerR.D - 30,
                        16
                    ));
                }

                // 显示 ROI 区域
                if (ShowRegion && domain != null && domain.IsInitialized())
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        new HObject(domain)
                    ));
                }

                ShowHRoi();
                InitRoiMethod();

                // ClearROI 会导致 real 类型深度图丢失颜色映射，重新设置 LUT
                if (ModuleView is FlatnessView view && view.mWindowH != null && IsOpenWindows)
                {
                    HOperatorSet.SetLut(view.mWindowH.hControl.HalconWindow, "temperature");
                    view.mWindowH.WindowH._hWndControl.Repaint();
                }

                // 释放临时对象
                chObj.Dispose();
                chRealObj.Dispose();

                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
            finally
            {
                domain?.Dispose();
                diffImage?.Dispose();
                if (planeImageObj != null)
                {
                    planeImageObj.Dispose();
                }
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("平面度", "double", Flatness);
            AddOutputParam("最大偏差", "double", MaxDeviation);
            AddOutputParam("最小偏差", "double", MinDeviation);
            AddOutputParam("RMS", "double", FitError);
            AddOutputParam("Alpha", "double", Alpha);
            AddOutputParam("Beta", "double", Beta);
            AddOutputParam("Gamma", "double", Gamma);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        /// <summary>获取ROI区域，若无效则返回图像完整domain</summary>
        private HRegion GetRoiRegion()
        {
            if (UseRoi && TranRoi.Length1 > 0 && TranRoi.Length2 > 0)
            {
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(TranRoi.MidR, TranRoi.MidC, -TranRoi.Phi, TranRoi.Length1, TranRoi.Length2);
                return roiRegion;
            }
            return DispImage.GetDomain();
        }

        public override void ShowHRoi()
        {
            base.ShowHRoi();
        }

        public void InitRoiMethod()
        {
            var view = ModuleView as FlatnessView;
            if (view == null) return;

            if (!UseRoi) return;

            string roiName = ModuleParam.ModuleName + "FlatnessROI";

            if (_RoiList == null)
                _RoiList = new Dictionary<string, ROI>();

            if (TranRoi.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genRect2(roiName, TranRoi.MidR, TranRoi.MidC,
                    TranRoi.Phi, TranRoi.Length1, TranRoi.Length2, ref _RoiList);
            }
            else if (DispImage != null && !_RoiList.ContainsKey(roiName))
            {
                DispImage.GetImageSize(out int width, out int height);
                view.mWindowH.WindowH.genRect2(roiName, height / 2.0, width / 2.0,
                    0, width / 4.0, height / 4.0, ref _RoiList);
                TranRoi.MidC = width / 2.0;
                TranRoi.MidR = height / 2.0;
                TranRoi.Length1 = width / 4.0;
                TranRoi.Length2 = height / 4.0;
                TranRoi.Deg = 0;
            }
            else if (DispImage != null && _RoiList.ContainsKey(roiName))
            {
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    view.mWindowH.WindowH.genRect2(roiName, TranRoi.MidR, TranRoi.MidC,
                        TranRoi.Phi, TranRoi.Length1, TranRoi.Length2, ref _RoiList);
                    Aff.Affine2d(HomMat2D_Inverse, TranRoi, InitRoi);
                    InitRoi.MidC = Math.Round(InitRoi.MidC, 3);
                    InitRoi.MidR = Math.Round(InitRoi.MidR, 3);
                    InitRoi.Length1 = Math.Round(InitRoi.Length1, 3);
                    InitRoi.Length2 = Math.Round(InitRoi.Length2, 3);
                    InitRoi.Deg = Math.Round(InitRoi.Deg, 3);
                    if (InitRoiChanged_Flag)
                    {
                        InitRoiCenterX.Text = InitRoi.MidC.ToString();
                        InitRoiCenterY.Text = InitRoi.MidR.ToString();
                        InitRoiLength1.Text = InitRoi.Length1.ToString();
                        InitRoiLength2.Text = InitRoi.Length2.ToString();
                        InitRoiAngel.Text = InitRoi.Deg.ToString();
                    }
                }
                else
                {
                    view.mWindowH.WindowH.genRect2(roiName, InitRoi.MidR, InitRoi.MidC,
                        InitRoi.Phi, InitRoi.Length1, InitRoi.Length2, ref _RoiList);
                    if (InitRoiChanged_Flag)
                    {
                        InitRoiCenterX.Text = InitRoi.MidC.ToString();
                        InitRoiCenterY.Text = InitRoi.MidR.ToString();
                        InitRoiLength1.Text = InitRoi.Length1.ToString();
                        InitRoiLength2.Text = InitRoi.Length2.ToString();
                        InitRoiAngel.Text = InitRoi.Deg.ToString();
                    }
                }
            }
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private eFitMethod _FitMethod = eFitMethod.regression;
        public eFitMethod FitMethod
        {
            get { return _FitMethod; }
            set { Set(ref _FitMethod, value); }
        }

        private bool _UseRoi = false;
        public bool UseRoi
        {
            get { return _UseRoi; }
            set
            {
                bool changed = _UseRoi != value;
                Set(ref _UseRoi, value);
                if (changed)
                {
                    if (value && DispImage != null && DispImage.IsInitialized())
                    {
                        double len1 = 0, len2 = 0;
                        double.TryParse(InitRoiLength1.Text, out len1);
                        double.TryParse(InitRoiLength2.Text, out len2);
                        if (len1 <= 0 || len2 <= 0)
                        {
                            DispImage.GetImageSize(out int width, out int height);
                            InitRoiCenterX.Text = (width / 2.0).ToString();
                            InitRoiCenterY.Text = (height / 2.0).ToString();
                            InitRoiAngel.Text = "0";
                            InitRoiLength1.Text = (width / 4.0).ToString();
                            InitRoiLength2.Text = (height / 4.0).ToString();
                        }
                    }
                    InitRoiMethod();
                }
            }
        }

        private int _NumIterations = 5;
        public int NumIterations
        {
            get { return _NumIterations; }
            set { Set(ref _NumIterations, value); }
        }

        private double _ClippingFactor = 0.1;
        public double ClippingFactor
        {
            get { return _ClippingFactor; }
            set { Set(ref _ClippingFactor, value); }
        }

        private bool _ShowDeviationMap = false;
        public bool ShowDeviationMap
        {
            get { return _ShowDeviationMap; }
            set { Set(ref _ShowDeviationMap, value); }
        }

        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private bool _ShowResultPoint = true;
        public bool ShowResultPoint
        {
            get { return _ShowResultPoint; }
            set { Set(ref _ShowResultPoint, value); }
        }

        private double _Flatness;
        public double Flatness
        {
            get { return _Flatness; }
            set { Set(ref _Flatness, value); }
        }

        private double _MaxDeviation;
        public double MaxDeviation
        {
            get { return _MaxDeviation; }
            set { Set(ref _MaxDeviation, value); }
        }

        private double _MinDeviation;
        public double MinDeviation
        {
            get { return _MinDeviation; }
            set { Set(ref _MinDeviation, value); }
        }

        private double _FitError;
        public double FitError
        {
            get { return _FitError; }
            set { Set(ref _FitError, value); }
        }

        private double _Alpha;
        public double Alpha
        {
            get { return _Alpha; }
            set { Set(ref _Alpha, value); }
        }

        private double _Beta;
        public double Beta
        {
            get { return _Beta; }
            set { Set(ref _Beta, value); }
        }

        private double _Gamma;
        public double Gamma
        {
            get { return _Gamma; }
            set { Set(ref _Gamma, value); }
        }

        // ROI 原始坐标输入（可链接变量）
        public LinkVarModel InitRoiCenterX { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel InitRoiCenterY { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel InitRoiLength1 { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel InitRoiLength2 { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel InitRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // 几何对象
        public ROIRectangle2 InitRoi = new ROIRectangle2();
        public ROIRectangle2 TranRoi = new ROIRectangle2();
        public ROIRectangle2 TempRoi = new ROIRectangle2();

        // 标志位
        [NonSerialized] bool DisenableAffine2d = false;
        [NonSerialized] bool InitRoiChanged_Flag = false;

        [NonSerialized]
        private Dictionary<string, ROI> _RoiList;
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as FlatnessView;
            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }
            view.mWindowH.hControl.MouseUp += HControl_MouseUp;

            InitRoiCenterX.TextChanged = new Action(() => { InitRoiChanged(); });
            InitRoiCenterY.TextChanged = new Action(() => { InitRoiChanged(); });
            InitRoiLength1.TextChanged = new Action(() => { InitRoiChanged(); });
            InitRoiLength2.TextChanged = new Action(() => { InitRoiChanged(); });
            InitRoiAngel.TextChanged = new Action(() => { InitRoiChanged(); });

            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText == null) return;
            }
            GetDispImage(InputImageLinkText, true);
            if (DispImage != null && DispImage.IsInitialized())
            {
                InitRoiMethod();
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
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
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
                        var view = ModuleView as FlatnessView;
                        if (view != null)
                        {
                            view.mWindowH.hControl.MouseUp -= HControl_MouseUp;
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        [NonSerialized]
        private CommandBase _DrawRoiCommand;
        public CommandBase DrawRoiCommand
        {
            get
            {
                if (_DrawRoiCommand == null)
                {
                    _DrawRoiCommand = new CommandBase((obj) =>
                    {
                        if (DispImage == null || !DispImage.IsInitialized()) return;

                        double len1 = 0, len2 = 0;
                        double.TryParse(InitRoiLength1.Text, out len1);
                        double.TryParse(InitRoiLength2.Text, out len2);
                        if (len1 <= 0 || len2 <= 0)
                        {
                            DispImage.GetImageSize(out int width, out int height);
                            InitRoiCenterX.Text = (width / 2.0).ToString();
                            InitRoiCenterY.Text = (height / 2.0).ToString();
                            InitRoiAngel.Text = "0";
                            InitRoiLength1.Text = (width / 4.0).ToString();
                            InitRoiLength2.Text = (height / 4.0).ToString();
                        }

                        InitRoiMethod();
                    });
                }
                return _DrawRoiCommand;
            }
        }

        private void InitRoiChanged()
        {
            if (InitRoiChanged_Flag) return;
            InitRoi.MidC = Convert.ToDouble(GetLinkValue(InitRoiCenterX));
            InitRoi.MidR = Convert.ToDouble(GetLinkValue(InitRoiCenterY));
            InitRoi.Length1 = Convert.ToDouble(GetLinkValue(InitRoiLength1));
            InitRoi.Length2 = Convert.ToDouble(GetLinkValue(InitRoiLength2));
            InitRoi.Deg = Convert.ToDouble(GetLinkValue(InitRoiAngel));
            DisenableAffine2d = true;
            ExeModule();
            InitRoiMethod();
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as FlatnessView;
            if (view == null || view.mWindowH == null) return;

            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            if (roi is ROIRectangle2 rect2)
            {
                TempRoi.MidC = Math.Round(rect2.MidC, 3);
                TempRoi.MidR = Math.Round(rect2.MidR, 3);
                TempRoi.Length1 = Math.Round(rect2.Length1, 3);
                TempRoi.Length2 = Math.Round(rect2.Length2, 3);
                TempRoi.Deg = Math.Round(rect2.Deg, 3);

                DisenableAffine2d = true;
                InitRoiChanged_Flag = true;
                ExeModule();
                InitRoiMethod();
                InitRoiChanged_Flag = false;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.InitRoiCenterX:
                    InitRoiCenterX.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitRoiCenterY:
                    InitRoiCenterY.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitRoiLength1:
                    InitRoiLength1.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitRoiLength2:
                    InitRoiLength2.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitRoiAngel:
                    InitRoiAngel.Text = obj.LinkName;
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
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged,
                        o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.InitRoiCenterX:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitRoiCenterX");
                                break;
                            case eLinkCommand.InitRoiCenterY:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitRoiCenterY");
                                break;
                            case eLinkCommand.InitRoiLength1:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitRoiLength1");
                                break;
                            case eLinkCommand.InitRoiLength2:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitRoiLength2");
                                break;
                            case eLinkCommand.InitRoiAngel:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitRoiAngel");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion
    }
}
