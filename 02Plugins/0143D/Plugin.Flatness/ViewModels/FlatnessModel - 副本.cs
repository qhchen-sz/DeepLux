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
    }

    public enum eFitMethod
    {
        [EnumDescription("标准回归")]
        regression,
        [EnumDescription("加权最小二乘")]
        least_squares,
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
            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                HRegion domain = GetRoiRegion();

                domain.FitSurfaceFirstOrder(DispImage, FitMethod.ToString(), NumIterations, ClippingFactor, out HTuple surfaceHandle, out HTuple fitError);

                HOperatorSet.GetImageSize(DispImage, out HTuple width, out HTuple height);

                // 从拟合结果中提取平面参数
                //HOperatorSet.GetSurfaceMatchingResult(surfaceHandle, "alpha", 0, out HTuple alpha);
                //HOperatorSet.GetSurfaceMatchingResult(surfaceHandle, "beta", 0, out HTuple beta);
                //HOperatorSet.GetSurfaceMatchingResult(surfaceHandle, "gamma", 0, out HTuple gamma);

                // ✅ 正确：直接从 surfaceHandle HTuple 中提取（索引 0,1,2 对应 α,β,γ）
                HTuple alpha = surfaceHandle[0];   // X 方向倾斜系数
                HTuple beta = surfaceHandle[1];   // Y 方向倾斜系数
                HTuple gamma = surfaceHandle[2];   // 高度截距

                Alpha = Math.Round(alpha.D, 6);
                Beta = Math.Round(beta.D, 6);
                Gamma = Math.Round(gamma.D, 6);

                HOperatorSet.GenImageSurfaceFirstOrder(out HObject planeImageObj, "real", alpha, beta, gamma, height / 2, width / 2, width, height);
                HImage planeImage = new HImage(planeImageObj);
                HImage diffImage = DispImage.SubImage(planeImage, (HTuple)1, (HTuple)0);
                planeImage.Dispose();

                HOperatorSet.MinMaxGray(domain, diffImage, 0,
                    out HTuple minDev, out HTuple maxDev, out HTuple range);

                Flatness = Math.Round(maxDev.D - minDev.D, 6);
                MaxDeviation = Math.Round(maxDev.D, 6);
                MinDeviation = Math.Round(minDev.D, 6);
                RMS = Math.Round(fitError.D, 6);

                if (ShowDeviationMap)
                {
                    double rangeVal = range.D;
                    if (rangeVal > 0)
                    {
                        HImage dispDiff = diffImage.ScaleImage(255.0 / rangeVal, 128.0 - 128.0 * maxDev.D / rangeVal);
                        DispImage = new RImage(dispDiff);
                    }
                }

                if (ShowResultPoint)
                {
                    HOperatorSet.AreaCenter(domain, out HTuple area, out HTuple centerR, out HTuple centerC);
                    string text = $"平面度:{Flatness:F4}";
                    ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                        ModuleParam.Remarks, HRoiType.文字显示, "green", text,
                        centerC.D, centerR.D - 30, 16));
                }

                if (ShowRegion && domain != null && domain.IsInitialized())
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                        ModuleParam.Remarks, HRoiType.检测结果, "green",
                        new HObject(domain)));

                ShowHRoi();
                diffImage.Dispose();
                domain.Dispose();
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

        public override void AddOutputParams()
        {
            AddOutputParam("平面度", "double", Flatness);
            AddOutputParam("最大偏差", "double", MaxDeviation);
            AddOutputParam("最小偏差", "double", MinDeviation);
            AddOutputParam("RMS", "double", RMS);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        /// <summary>获取ROI区域，若无效则返回图像完整domain</summary>
        private HRegion GetRoiRegion()
        {
            if (RoiLen1 > 0 && RoiLen2 > 0)
            {
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(RoiMidR, RoiMidC, RoiPhi, RoiLen1, RoiLen2);
                return roiRegion;
            }
            return DispImage.GetDomain();
        }

        public override void ShowHRoi()
        {
            base.ShowHRoi();

            if (RoiLen1 <= 0 || RoiLen2 <= 0) return;

            var view = ModuleView as FlatnessView;
            if (view == null || view.mWindowH == null) return;

            if (_RoiList == null)
                _RoiList = new Dictionary<string, ROI>();

            string roiName = ModuleParam.ModuleName + "FlatnessROI";
            view.mWindowH.WindowH.genRect2(roiName, RoiMidR, RoiMidC,
                -RoiPhi, RoiLen1, RoiLen2, ref _RoiList);
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

        private bool _ShowDeviationMap = true;
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

        private double _RMS;
        public double RMS
        {
            get { return _RMS; }
            set { Set(ref _RMS, value); }
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

        private double _RoiMidR;
        public double RoiMidR
        {
            get { return _RoiMidR; }
            set { Set(ref _RoiMidR, value); }
        }

        private double _RoiMidC;
        public double RoiMidC
        {
            get { return _RoiMidC; }
            set { Set(ref _RoiMidC, value); }
        }

        private double _RoiPhi;
        public double RoiPhi
        {
            get { return _RoiPhi; }
            set { Set(ref _RoiPhi, value); }
        }

        private double _RoiLen1;
        public double RoiLen1
        {
            get { return _RoiLen1; }
            set { Set(ref _RoiLen1, value); }
        }

        private double _RoiLen2;
        public double RoiLen2
        {
            get { return _RoiLen2; }
            set { Set(ref _RoiLen2, value); }
        }

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
            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText == null) return;
            }
            GetDispImage(InputImageLinkText, true);
            if (DispImage != null && DispImage.IsInitialized())
            {
                ShowHRoi();
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
                            view.mWindowH.DrawModel = false;
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

                        DispImage.GetImageSize(out int width, out int height);
                        if (RoiLen1 <= 0 || RoiLen2 <= 0)
                        {
                            RoiMidR = height / 2.0;
                            RoiMidC = width / 2.0;
                            RoiPhi = 0;
                            RoiLen1 = width / 4.0;
                            RoiLen2 = height / 4.0;
                        }

                        var view = ModuleView as FlatnessView;
                        if (view != null && view.mWindowH != null)
                            view.mWindowH.DrawModel = true;

                        ShowHRoi();
                    });
                }
                return _DrawRoiCommand;
            }
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as FlatnessView;
            if (view == null || view.mWindowH == null) return;

            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            if (roi is ROIRectangle2 rect2)
            {
                RoiMidR = Math.Round(rect2.MidR, 3);
                RoiMidC = Math.Round(rect2.MidC, 3);
                RoiPhi = Math.Round(rect2.Phi, 3);
                RoiLen1 = Math.Round(rect2.Length1, 3);
                RoiLen2 = Math.Round(rect2.Length2, 3);

                view.mWindowH.DrawModel = false;
                ExeModule();
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
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion
    }
}
