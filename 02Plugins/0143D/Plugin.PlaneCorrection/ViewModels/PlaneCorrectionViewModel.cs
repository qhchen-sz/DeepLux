using EventMgrLib;
using HalconDotNet;
using Plugin.PlaneCorrection.Views;
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
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;

namespace Plugin.PlaneCorrection.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        PlaneImageLink,
        InitAlpha,
        InitBeta,
        InitGamma,
        InitRoiCenterX,
        InitRoiCenterY,
        InitRoiLength1,
        InitRoiLength2,
        InitRoiAngel,
    }

    public enum ePlaneMode
    {
        PlaneImage,
        Manual,
    }
    #endregion

    [Category("3D")]
    [DisplayName("平面校正")]
    [ModuleImageName("PlaneCorrection")]
    [Serializable]
    public class PlaneCorrectionViewModel : ModuleBase
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
            HTuple om3D = null;
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

                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取基准平面参数
                double refAlpha, refBeta, refGamma;
                HImage linkedPlaneImg = null;

                if (PlaneMode == ePlaneMode.Manual)
                {
                    refAlpha = Convert.ToDouble(GetLinkValue(InitAlpha));
                    refBeta = Convert.ToDouble(GetLinkValue(InitBeta));
                    refGamma = Convert.ToDouble(GetLinkValue(InitGamma));
                }
                else
                {
                    if (string.IsNullOrEmpty(PlaneImageLinkText))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    var savedDisp = DispImage;
                    GetDispImage(PlaneImageLinkText, true);
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    linkedPlaneImg = new HImage(DispImage);
                    DispImage = savedDisp;
                    // 同步恢复窗口显示图像
                    if (ModuleView != null && ModuleView.mWindowH != null)
                        ModuleView.mWindowH.HobjectToHimage(DispImage);

                    HOperatorSet.CountChannels(linkedPlaneImg, out HTuple pc);
                    int pCh = pc.I >= 1 ? 1 : 1;
                    HOperatorSet.AccessChannel(linkedPlaneImg, out HObject pChObj, pCh);
                    HOperatorSet.ConvertImageType(pChObj, out HObject pRealObj, "real");
                    HOperatorSet.FitSurfaceFirstOrder(
                        linkedPlaneImg.GetDomain(),
                        pRealObj,
                        "regression",
                        5,
                        0.1,
                        out HTuple pAlpha,
                        out HTuple pBeta,
                        out HTuple pGamma
                    );
                    refAlpha = pAlpha.D;
                    refBeta = pBeta.D;
                    refGamma = pGamma.D;
                    pChObj.Dispose();
                    pRealObj.Dispose();
                }

                // 逆变换 ROI
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

                // 正变换 ROI
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
                    InitRoi.MidC = TranRoi.MidC = TempRoi.MidC = Convert.ToDouble(GetLinkValue(InitRoiCenterX));
                    InitRoi.MidR = TranRoi.MidR = TempRoi.MidR = Convert.ToDouble(GetLinkValue(InitRoiCenterY));
                    InitRoi.Length1 = TranRoi.Length1 = TempRoi.Length1 = Convert.ToDouble(GetLinkValue(InitRoiLength1));
                    InitRoi.Length2 = TranRoi.Length2 = TempRoi.Length2 = Convert.ToDouble(GetLinkValue(InitRoiLength2));
                    InitRoi.Deg = TranRoi.Deg = TempRoi.Deg = Convert.ToDouble(GetLinkValue(InitRoiAngel));
                }

                domain = GetRoiRegion();
                if (domain == null || !domain.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取图像尺寸
                DispImage.GetImageSize(out int width, out int height);

                // 获取通道数
                HOperatorSet.CountChannels(DispImage, out HTuple channelCount);
                int nChannels = channelCount.I;

                // 提取高度通道（Ch1）
                int targetChannel = 1;
                if (targetChannel > nChannels)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception($"目标通道 {targetChannel} 超出图像通道数 {nChannels}"));
                    return false;
                }

                HOperatorSet.AccessChannel(DispImage, out HObject chObj, targetChannel);
                HOperatorSet.ConvertImageType(chObj, out HObject chRealObj, "real");

                double cx = width / 2.0;
                double cy = height / 2.0;

                // 生成 X、Y 坐标图（像素斜坡），用于构建 3D 点云
                HOperatorSet.GenImageSurfaceFirstOrder(out HObject xImage, "real", 0, 1, 0, cy, cx, width, height);
                HOperatorSet.GenImageSurfaceFirstOrder(out HObject yImage, "real", 1, 0, 0, cy, cx, width, height);

                // 参考平面法向量: n = (-β, -α, 1) / sqrt(α² + β² + 1)
                double normVal = Math.Sqrt(refAlpha * refAlpha + refBeta * refBeta + 1.0);
                double nx = -refBeta / normVal;
                double ny = -refAlpha / normVal;
                double nz = 1.0 / normVal;

                // 旋转轴: k = n × (0,0,1) = (ny, -nx, 0)
                // 旋转角: θ = acos(n·(0,0,1)) = acos(nz)
                double rx = ny;
                double ry = -nx;
                double angle = Math.Acos(nz);

                // 转为 3D 对象模型
                HOperatorSet.XyzToObjectModel3d(xImage, yImage, chRealObj, out om3D);

                if (Math.Abs(angle) > 1e-10)
                {
                    // 轴角 → 四元数 → 旋转矩阵
                    HOperatorSet.AxisAngleToQuat(rx, ry, 0, angle, out HTuple quat);
                    HOperatorSet.QuatToHomMat3d(quat, out HTuple rotMat);

                    // 组合变换：先平移到图像中心，旋转，再平移回来
                    HOperatorSet.HomMat3dIdentity(out HTuple homMat3D);
                    HOperatorSet.HomMat3dTranslate(homMat3D, -cx, -cy, 0, out HTuple toOrigin);
                    HOperatorSet.HomMat3dCompose(rotMat, toOrigin, out HTuple afterRot);
                    HOperatorSet.HomMat3dIdentity(out HTuple id2);
                    HOperatorSet.HomMat3dTranslate(id2, cx, cy, 0, out HTuple toCenter);
                    HOperatorSet.HomMat3dCompose(toCenter, afterRot, out HTuple transform);

                    HOperatorSet.RigidTransObjectModel3d(om3D, transform, out HTuple transformedOM);
                    om3D = transformedOM;
                }

                // 转回 XYZ 图像，其中 Z 图是校正后的高度
                HOperatorSet.ObjectModel3dToXyz(out HObject xTr, out HObject yTr, out HObject zTrans, om3D, "area", 0, -1);

                // 基准平面图像：优先用链接的，否则从参数生成
                if (linkedPlaneImg != null && linkedPlaneImg.IsInitialized())
                {
                    planeImageObj = linkedPlaneImg;
                }
                else
                {
                    HOperatorSet.GenImageSurfaceFirstOrder(
                        out planeImageObj,
                        "real",
                        refAlpha,
                        refBeta,
                        refGamma,
                        cy,
                        cx,
                        width,
                        height
                    );
                }

                // 旋转后基准面在中心处的高度
                double planeCenterHeight = refAlpha * cy + refBeta * cx + refGamma;
                HOperatorSet.ScaleImage(zTrans, out HObject zCorrected, 1.0, -planeCenterHeight);

                var correctedImage = new HImage(zCorrected);
                CorrectedImage = new RImage(correctedImage);

                // 统计校正后的偏差
                HOperatorSet.MinMaxGray(
                    domain,
                    correctedImage,
                    0.0,
                    out HTuple minDev,
                    out HTuple maxDev,
                    out HTuple range
                );

                MaxDeviation = Math.Round(maxDev.D, 6);
                MinDeviation = Math.Round(minDev.D, 6);
                Flatness = Math.Round(maxDev.D - minDev.D, 6);

                // RMS 误差
                HOperatorSet.Intensity(
                    domain,
                    correctedImage,
                    out HTuple meanDiff,
                    out HTuple deviation
                );
                RmsError = Math.Round(deviation.D, 6);

                // 显示
                if (ShowCorrectedImage)
                {
                    double rangeVal = range.D;
                    if (rangeVal > 0)
                    {
                        HImage dispScaled = correctedImage.ScaleImage(
                            255.0 / rangeVal,
                            128.0 - 128.0 * maxDev.D / rangeVal
                        );
                        DispImage = new RImage(dispScaled);
                    }
                }

                // 释放临时图像
                xImage.Dispose();
                yImage.Dispose();
                xTr.Dispose();
                yTr.Dispose();

                // 显示结果文字
                if (ShowResultPoint)
                {
                    HOperatorSet.AreaCenter(
                        domain,
                        out HTuple area,
                        out HTuple centerR,
                        out HTuple centerC
                    );
                    string text = $"校正后平面度:{Flatness:F4}  RMS:{RmsError:F4}";
                    ShowHRoi(new HText(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.文字显示,
                        "green",
                        text,
                        centerC.D,
                        centerR.D - 25,
                        14
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
                if (om3D != null)
                {
                    HOperatorSet.ClearObjectModel3d(om3D);
                }
                if (planeImageObj != null)
                {
                    planeImageObj.Dispose();
                }
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("校正后图像", "object", CorrectedImage);
            AddOutputParam("平面度", "double", Flatness);
            AddOutputParam("最大偏差", "double", MaxDeviation);
            AddOutputParam("最小偏差", "double", MinDeviation);
            AddOutputParam("RMS", "double", RmsError);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        private HRegion GetRoiRegion()
        {
            if (UseRoi && TranRoi.Length1 > 0 && TranRoi.Length2 > 0)
            {
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(TranRoi.MidR, TranRoi.MidC, TranRoi.Phi, TranRoi.Length1, TranRoi.Length2);
                return roiRegion;
            }
            return DispImage.GetDomain();
        }

        public void InitRoiMethod()
        {
            var view = ModuleView as PlaneCorrectionView;
            if (view == null) return;

            if (!UseRoi) return;

            string roiName = ModuleParam.ModuleName + "PlaneCorrectionROI";

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

        private string _PlaneImageLinkText;
        public string PlaneImageLinkText
        {
            get { return _PlaneImageLinkText; }
            set { Set(ref _PlaneImageLinkText, value); }
        }

        private ePlaneMode _PlaneMode = ePlaneMode.PlaneImage;
        public ePlaneMode PlaneMode
        {
            get { return _PlaneMode; }
            set { Set(ref _PlaneMode, value); }
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
                    InitRoiMethod();
                }
            }
        }

        private bool _ShowCorrectedImage = true;
        public bool ShowCorrectedImage
        {
            get { return _ShowCorrectedImage; }
            set { Set(ref _ShowCorrectedImage, value); }
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

        private double _RmsError;
        public double RmsError
        {
            get { return _RmsError; }
            set { Set(ref _RmsError, value); }
        }

        [NonSerialized]
        private RImage _CorrectedImage;
        public RImage CorrectedImage
        {
            get { return _CorrectedImage; }
            set { Set(ref _CorrectedImage, value); }
        }

        // ROI 原始坐标输入
        public LinkVarModel InitAlpha { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitBeta { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitGamma { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitRoiCenterX { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiCenterY { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiLength1 { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiLength2 { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // 几何对象
        [NonSerialized] public ROIRectangle2 InitRoi = new ROIRectangle2();
        [NonSerialized] public ROIRectangle2 TranRoi = new ROIRectangle2();
        [NonSerialized] public ROIRectangle2 TempRoi = new ROIRectangle2();

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
            var view = ModuleView as PlaneCorrectionView;
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
            InitAlpha.TextChanged = new Action(() => { InitRoiChanged(); });
            InitBeta.TextChanged = new Action(() => { InitRoiChanged(); });
            InitGamma.TextChanged = new Action(() => { InitRoiChanged(); });

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
                        var view = ModuleView as PlaneCorrectionView;
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
            var view = ModuleView as PlaneCorrectionView;
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
                case eLinkCommand.PlaneImageLink:
                    PlaneImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.InitAlpha:
                    InitAlpha.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitBeta:
                    InitBeta.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitGamma:
                    InitGamma.Text = obj.LinkName;
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
                            case eLinkCommand.PlaneImageLink:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},PlaneImageLink");
                                break;
                            case eLinkCommand.InitAlpha:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitAlpha");
                                break;
                            case eLinkCommand.InitBeta:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitBeta");
                                break;
                            case eLinkCommand.InitGamma:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitGamma");
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
