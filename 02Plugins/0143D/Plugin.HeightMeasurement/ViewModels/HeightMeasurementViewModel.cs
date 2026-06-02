using EventMgrLib;
using HalconDotNet;
using Plugin.HeightMeasurement.Views;
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
using Newtonsoft.Json.Linq;

namespace Plugin.HeightMeasurement.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        RefRoiCenterX,
        RefRoiCenterY,
        RefRoiLength1,
        RefRoiLength2,
        RefRoiAngel,
        RefRoiLink,
        RefBasePlaneLink,
        MeasRoiCenterX,
        MeasRoiCenterY,
        MeasRoiLength1,
        MeasRoiLength2,
        MeasRoiAngel,
        MeasRoiLink,
        FilterM,
        FilterN,
    }

    public enum eMeasureMode
    {
        [EnumDescription("平均值")]
        Average,
        [EnumDescription("最大值")]
        Max,
        [EnumDescription("最小值")]
        Min,
    }

    public enum eFilterMode
    {
        [EnumDescription("全部")]
        All,
        [EnumDescription("剔除最大m个点,取n个点")]
        RemoveMaxMTakeN,
        [EnumDescription("剔除最小m个点,取n个点")]
        RemoveMinMTakeN,
        [EnumDescription("剔除最小m个点,剔除最大n个点")]
        RemoveMinMRemoveMaxN,
    }

    public enum eRefRoiSource
    {
        [EnumDescription("手动绘制")]
        Manual,
        [EnumDescription("链接区域")]
        LinkRegion,
        [EnumDescription("链接基准")]
        LinkBase,
    }

    public enum eMeasRoiSource
    {
        [EnumDescription("手动绘制")]
        Manual,
        [EnumDescription("链接区域")]
        LinkRegion,
    }
    #endregion

    [Category("3D")]
    [DisplayName("检测高度")]
    [ModuleImageName("HeightMeasurement")]
    [Serializable]
    public class HeightMeasurementViewModel : ModuleBase
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
            HImage sourceImage = null;
            HObject chObj = null;
            HObject chRealObj = null;
            HObject refPlaneObj = null;
            HObject diffObj = null;
            HRegion refRegion = null;
            HRegion measRegion = null;

            try
            {
                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (!IsOpenWindows)
                    GetDispImage(InputImageLinkText, true);

                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);

                GetHomMat2D();

                // 逆变换：拖动 ROI 后把图像坐标转回原始坐标
                if (DisenableAffine2d && HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    DisenableAffine2d = false;
                    if (RefRoiChangedFlag)
                    {
                        Aff.Affine2d(HomMat2D_Inverse, TempRefRoi, InitRefRoi);
                        RefRoiCenterX.Text = InitRefRoi.MidC.ToString();
                        RefRoiCenterY.Text = InitRefRoi.MidR.ToString();
                        RefRoiLength1.Text = InitRefRoi.Length1.ToString();
                        RefRoiLength2.Text = InitRefRoi.Length2.ToString();
                        RefRoiAngel.Text = InitRefRoi.Deg.ToString();
                    }
                    if (MeasRoiChangedFlag)
                    {
                        Aff.Affine2d(HomMat2D_Inverse, TempMeasRoi, InitMeasRoi);
                        MeasRoiCenterX.Text = InitMeasRoi.MidC.ToString();
                        MeasRoiCenterY.Text = InitMeasRoi.MidR.ToString();
                        MeasRoiLength1.Text = InitMeasRoi.Length1.ToString();
                        MeasRoiLength2.Text = InitMeasRoi.Length2.ToString();
                        MeasRoiAngel.Text = InitMeasRoi.Deg.ToString();
                    }
                }

                // 正变换：原始坐标 → 图像坐标
                UpdateRoiTransforms();

                refRegion = GetRefRoiRegion();
                measRegion = GetMeasRoiRegion();
                if (refRegion == null || !refRegion.IsInitialized() ||
                    measRegion == null || !measRegion.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 提取高度通道
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (1 > channelCount.I)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception($"目标通道 1 超出图像通道数 {channelCount.I}"));
                    return false;
                }
                HOperatorSet.AccessChannel(sourceImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // 获取图像尺寸
                HOperatorSet.GetImageSize(chRealObj, out HTuple width, out HTuple height);

                // ---- 计算断差图 ----
                if (RefRoiSource == eRefRoiSource.LinkBase)
                {
                    // 链接外部基准平面图像
                    if (string.IsNullOrEmpty(RefBasePlaneLinkText))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    var savedDisp = DispImage;
                    GetDispImage(RefBasePlaneLinkText, true);
                    if (DispImage == null || !DispImage.IsInitialized())
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    HImage linkedPlaneImg = new HImage(DispImage);
                    DispImage = savedDisp;
                    if (ModuleView != null && ModuleView.mWindowH != null)
                        ModuleView.mWindowH.HobjectToHimage(DispImage);

                    HOperatorSet.CountChannels(linkedPlaneImg, out HTuple pc);
                    int pCh = pc.I >= 1 ? 1 : 1;
                    HOperatorSet.AccessChannel(linkedPlaneImg, out HObject pChObj, pCh);
                    HOperatorSet.ConvertImageType(pChObj, out HObject pRealObj, "real");
                    HOperatorSet.FitSurfaceFirstOrder(
                        linkedPlaneImg.GetDomain(),
                        pRealObj,
                        "regression", 5, 0.1,
                        out HTuple pAlpha, out HTuple pBeta, out HTuple pGamma
                    );
                    HOperatorSet.AreaCenter(linkedPlaneImg.GetDomain(), out _, out HTuple refR, out HTuple refC);

                    // 生成与输入图像同尺寸的全图基准平面
                    HOperatorSet.GenImageSurfaceFirstOrder(out refPlaneObj, "real",
                        pAlpha, pBeta, pGamma, refR, refC, width, height);

                    // 全图断差 = 原始高度 - 基准平面高度
                    HOperatorSet.SubImage(chRealObj, refPlaneObj, out diffObj, 1.0, 0.0);

                    pChObj.Dispose();
                    pRealObj.Dispose();
                    linkedPlaneImg.Dispose();
                }
                else if (FitPlaneForRef)
                {
                    // 拟合基准平面
                    HOperatorSet.FitSurfaceFirstOrder(refRegion, chRealObj, "regression", 5, 0.1,
                        out HTuple alpha, out HTuple beta, out HTuple gamma);
                    HOperatorSet.AreaCenter(refRegion, out _, out HTuple refR, out HTuple refC);

                    // 生成全图基准平面
                    HOperatorSet.GenImageSurfaceFirstOrder(out refPlaneObj, "real",
                        alpha, beta, gamma, refR, refC, width, height);

                    // 全图断差 = 原始高度 - 基准平面高度
                    HOperatorSet.SubImage(chRealObj, refPlaneObj, out diffObj, 1.0, 0.0);
                }
                else
                {
                    // 基准区域平均高度
                    HOperatorSet.Intensity(refRegion, new HImage(chRealObj), out HTuple refMean, out _);
                    double baseHeight = refMean.D;

                    // 全图断差 = 原始高度 - 基准高度
                    HOperatorSet.ScaleImage(chRealObj, out diffObj, 1.0, -baseHeight);
                }

                // 提取测量区域内的断差值
                HOperatorSet.GetRegionPoints(measRegion, out HTuple measRows, out HTuple measCols);
                HOperatorSet.GetGrayval(diffObj, measRows, measCols, out HTuple diffValues);

                int totalPoints = diffValues.Length;
                if (totalPoints == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 重采样
                if (ResampleFactor > 1)
                {
                    HTuple sampled = new HTuple();
                    for (int i = 0; i < totalPoints; i += ResampleFactor)
                    {
                        sampled = sampled.TupleConcat(diffValues[i]);
                    }
                    diffValues = sampled;
                    totalPoints = diffValues.Length;
                }

                if (totalPoints == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // C# 排序与筛选
                double[] values = diffValues.ToDArr();
                Array.Sort(values); // 升序

                int m = 0, n = 0;
                if (FilterMode != eFilterMode.All)
                {
                    m = GetFilterCount(FilterM, totalPoints, FilterMIsPercent);
                    n = GetFilterCount(FilterN, totalPoints, FilterNIsPercent);
                    m = Math.Max(0, Math.Min(m, totalPoints));
                    n = Math.Max(0, Math.Min(n, totalPoints));
                }

                List<double> filtered = new List<double>();
                switch (FilterMode)
                {
                    case eFilterMode.All:
                        filtered.AddRange(values);
                        break;
                    case eFilterMode.RemoveMaxMTakeN:
                        // 升序数组，最大在末尾；剔除最大m个后，取接下来最大的n个
                        for (int i = totalPoints - 1 - m;
                             i >= totalPoints - 1 - m - n + 1 && i >= 0; i--)
                        {
                            filtered.Add(values[i]);
                        }
                        break;
                    case eFilterMode.RemoveMinMTakeN:
                        // 剔除最小m个后，取接下来最小的n个
                        for (int i = m; i < m + n && i < totalPoints; i++)
                        {
                            filtered.Add(values[i]);
                        }
                        break;
                    case eFilterMode.RemoveMinMRemoveMaxN:
                        // 剔除最小m个和最大n个
                        for (int i = m; i < totalPoints - n; i++)
                        {
                            filtered.Add(values[i]);
                        }
                        break;
                }

                if (filtered.Count == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 按模式统计
                switch (MeasureMode)
                {
                    case eMeasureMode.Average:
                        HeightDifference = filtered.Average();
                        break;
                    case eMeasureMode.Max:
                        HeightDifference = filtered.Max();
                        break;
                    case eMeasureMode.Min:
                        HeightDifference = filtered.Min();
                        break;
                }

                HeightDifference = Math.Round(HeightDifference, 6);

                ChangeModuleRunStatus(eRunStatus.OK);

                // ---- 显示 ----
                if (ShowRegion)
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        new HObject(refRegion)
                    ));
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "blue",
                        new HObject(measRegion)
                    ));

                    // 基准区域文字标注
                    ShowHRoi(new HText(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.文字显示,
                        "green",
                        "基准",
                        TranRefRoi.MidC,
                        TranRefRoi.MidR,
                        128
                    ));

                    // 测量区域文字标注
                    ShowHRoi(new HText(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.文字显示,
                        "blue",
                        "测量",
                        TranMeasRoi.MidC,
                        TranMeasRoi.MidR,
                        128
                    ));
                }

                HOperatorSet.AreaCenter(measRegion, out _, out HTuple measR, out HTuple measC);
                string resultText = $"断差:{HeightDifference:F4}";
                ShowHRoi(new HText(
                    ModuleParam.ModuleEncode,
                    ModuleParam.ModuleName,
                    ModuleParam.Remarks,
                    HRoiType.文字显示,
                    "green",
                    resultText,
                    measC.D,
                    measR.D - 30,
                    32
                ));

                ShowHRoi();
                InitRefRoiMethod();
                InitMeasRoiMethod();

                if (ModuleView is HeightMeasurementView view && view.mWindowH != null)
                {
                    HOperatorSet.SetLut(view.mWindowH.hControl.HalconWindow, "temperature");
                    view.mWindowH.WindowH._hWndControl.Repaint();
                }

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
                refRegion?.Dispose();
                measRegion?.Dispose();
                sourceImage?.Dispose();
                chObj?.Dispose();
                chRealObj?.Dispose();
                if (refPlaneObj != null) refPlaneObj.Dispose();
                if (diffObj != null) diffObj.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("断差", "double", HeightDifference);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region ROI helpers
        private HRegion GetRefRoiRegion()
        {
            if (RefRoiSource == eRefRoiSource.LinkRegion && !string.IsNullOrEmpty(RefRoiLinkText))
            {
                HRegion region = (HRegion)GetLinkValue(RefRoiLinkText);
                return region;
            }
            if (TranRefRoi.Length1 > 0 && TranRefRoi.Length2 > 0)
            {
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(TranRefRoi.MidR, TranRefRoi.MidC,
                    -TranRefRoi.Phi, TranRefRoi.Length1, TranRefRoi.Length2);
                return roiRegion;
            }
            return DispImage.GetDomain();
        }

        private HRegion GetMeasRoiRegion()
        {
            if (MeasRoiSource == eMeasRoiSource.LinkRegion && !string.IsNullOrEmpty(MeasRoiLinkText))
            {
                HRegion region = (HRegion)GetLinkValue(MeasRoiLinkText);
                return region;
            }
            if (TranMeasRoi.Length1 > 0 && TranMeasRoi.Length2 > 0)
            {
                HRegion roiRegion = new HRegion();
                roiRegion.GenRectangle2(TranMeasRoi.MidR, TranMeasRoi.MidC,
                    -TranMeasRoi.Phi, TranMeasRoi.Length1, TranMeasRoi.Length2);
                return roiRegion;
            }
            return DispImage.GetDomain();
        }

        private void UpdateRoiTransforms()
        {
            // 基准 ROI
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                InitRefRoi.MidC = Convert.ToDouble(GetLinkValue(RefRoiCenterX));
                InitRefRoi.MidR = Convert.ToDouble(GetLinkValue(RefRoiCenterY));
                InitRefRoi.Length1 = Convert.ToDouble(GetLinkValue(RefRoiLength1));
                InitRefRoi.Length2 = Convert.ToDouble(GetLinkValue(RefRoiLength2));
                InitRefRoi.Deg = Convert.ToDouble(GetLinkValue(RefRoiAngel));
                Aff.Affine2d(HomMat2D, InitRefRoi, TranRefRoi);
            }
            else
            {
                if (!RefRoiCenterX.Text.StartsWith("&"))
                    InitRefRoi.MidC = TranRefRoi.MidC = TempRefRoi.MidC;
                else
                    InitRefRoi.MidC = TranRefRoi.MidC = TempRefRoi.MidC = Convert.ToDouble(GetLinkValue(RefRoiCenterX));

                if (!RefRoiCenterY.Text.StartsWith("&"))
                    InitRefRoi.MidR = TranRefRoi.MidR = TempRefRoi.MidR;
                else
                    InitRefRoi.MidR = TranRefRoi.MidR = TempRefRoi.MidR = Convert.ToDouble(GetLinkValue(RefRoiCenterY));

                if (!RefRoiLength1.Text.StartsWith("&"))
                    InitRefRoi.Length1 = TranRefRoi.Length1 = TempRefRoi.Length1;
                else
                    InitRefRoi.Length1 = TranRefRoi.Length1 = TempRefRoi.Length1 = Convert.ToDouble(GetLinkValue(RefRoiLength1));

                if (!RefRoiLength2.Text.StartsWith("&"))
                    InitRefRoi.Length2 = TranRefRoi.Length2 = TempRefRoi.Length2;
                else
                    InitRefRoi.Length2 = TranRefRoi.Length2 = TempRefRoi.Length2 = Convert.ToDouble(GetLinkValue(RefRoiLength2));

                if (!RefRoiAngel.Text.StartsWith("&"))
                    InitRefRoi.Deg = TranRefRoi.Deg = TempRefRoi.Deg;
                else
                    InitRefRoi.Deg = TranRefRoi.Deg = TempRefRoi.Deg = Convert.ToDouble(GetLinkValue(RefRoiAngel));
            }

            // 测量 ROI
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                InitMeasRoi.MidC = Convert.ToDouble(GetLinkValue(MeasRoiCenterX));
                InitMeasRoi.MidR = Convert.ToDouble(GetLinkValue(MeasRoiCenterY));
                InitMeasRoi.Length1 = Convert.ToDouble(GetLinkValue(MeasRoiLength1));
                InitMeasRoi.Length2 = Convert.ToDouble(GetLinkValue(MeasRoiLength2));
                InitMeasRoi.Deg = Convert.ToDouble(GetLinkValue(MeasRoiAngel));
                Aff.Affine2d(HomMat2D, InitMeasRoi, TranMeasRoi);
            }
            else
            {
                if (!MeasRoiCenterX.Text.StartsWith("&"))
                    InitMeasRoi.MidC = TranMeasRoi.MidC = TempMeasRoi.MidC;
                else
                    InitMeasRoi.MidC = TranMeasRoi.MidC = TempMeasRoi.MidC = Convert.ToDouble(GetLinkValue(MeasRoiCenterX));

                if (!MeasRoiCenterY.Text.StartsWith("&"))
                    InitMeasRoi.MidR = TranMeasRoi.MidR = TempMeasRoi.MidR;
                else
                    InitMeasRoi.MidR = TranMeasRoi.MidR = TempMeasRoi.MidR = Convert.ToDouble(GetLinkValue(MeasRoiCenterY));

                if (!MeasRoiLength1.Text.StartsWith("&"))
                    InitMeasRoi.Length1 = TranMeasRoi.Length1 = TempMeasRoi.Length1;
                else
                    InitMeasRoi.Length1 = TranMeasRoi.Length1 = TempMeasRoi.Length1 = Convert.ToDouble(GetLinkValue(MeasRoiLength1));

                if (!MeasRoiLength2.Text.StartsWith("&"))
                    InitMeasRoi.Length2 = TranMeasRoi.Length2 = TempMeasRoi.Length2;
                else
                    InitMeasRoi.Length2 = TranMeasRoi.Length2 = TempMeasRoi.Length2 = Convert.ToDouble(GetLinkValue(MeasRoiLength2));

                if (!MeasRoiAngel.Text.StartsWith("&"))
                    InitMeasRoi.Deg = TranMeasRoi.Deg = TempMeasRoi.Deg;
                else
                    InitMeasRoi.Deg = TranMeasRoi.Deg = TempMeasRoi.Deg = Convert.ToDouble(GetLinkValue(MeasRoiAngel));
            }
        }

        public void InitRefRoiMethod()
        {
            var view = ModuleView as HeightMeasurementView;
            if (view == null) return;

            if (RefRoiSource == eRefRoiSource.LinkRegion && !string.IsNullOrEmpty(RefRoiLinkText))
            {
                HRegion region = (HRegion)GetLinkValue(RefRoiLinkText);
                if (region != null && region.IsInitialized())
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        new HObject(region)
                    ));
                    ShowHRoi();
                }
                return;
            }

            string roiName = ModuleParam.ModuleName + "RefROI";
            if (_RoiList == null)
                _RoiList = new Dictionary<string, ROI>();

            if (TranRefRoi.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genRect2(roiName, TranRefRoi.MidR, TranRefRoi.MidC,
                    TranRefRoi.Phi, TranRefRoi.Length1, TranRefRoi.Length2, ref _RoiList);
            }
            else if (DispImage != null && !_RoiList.ContainsKey(roiName))
            {
                DispImage.GetImageSize(out int w, out int h);
                view.mWindowH.WindowH.genRect2(roiName, h / 2.0, w / 4.0,
                    0, w / 8.0, h / 8.0, ref _RoiList);
                TranRefRoi.MidC = w / 4.0;
                TranRefRoi.MidR = h / 2.0;
                TranRefRoi.Length1 = w / 8.0;
                TranRefRoi.Length2 = h / 8.0;
                TranRefRoi.Deg = 0;

                RefRoiCenterX.Text = TranRefRoi.MidC.ToString();
                RefRoiCenterY.Text = TranRefRoi.MidR.ToString();
                RefRoiLength1.Text = TranRefRoi.Length1.ToString();
                RefRoiLength2.Text = TranRefRoi.Length2.ToString();
                RefRoiAngel.Text = TranRefRoi.Deg.ToString();
            }
            else if (DispImage != null && _RoiList.ContainsKey(roiName))
            {
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    view.mWindowH.WindowH.genRect2(roiName, TranRefRoi.MidR, TranRefRoi.MidC,
                        TranRefRoi.Phi, TranRefRoi.Length1, TranRefRoi.Length2, ref _RoiList);
                    Aff.Affine2d(HomMat2D_Inverse, TranRefRoi, InitRefRoi);
                    InitRefRoi.MidC = Math.Round(InitRefRoi.MidC, 3);
                    InitRefRoi.MidR = Math.Round(InitRefRoi.MidR, 3);
                    InitRefRoi.Length1 = Math.Round(InitRefRoi.Length1, 3);
                    InitRefRoi.Length2 = Math.Round(InitRefRoi.Length2, 3);
                    InitRefRoi.Deg = Math.Round(InitRefRoi.Deg, 3);
                    if (RefRoiChangedFlag)
                    {
                        RefRoiCenterX.Text = InitRefRoi.MidC.ToString();
                        RefRoiCenterY.Text = InitRefRoi.MidR.ToString();
                        RefRoiLength1.Text = InitRefRoi.Length1.ToString();
                        RefRoiLength2.Text = InitRefRoi.Length2.ToString();
                        RefRoiAngel.Text = InitRefRoi.Deg.ToString();
                    }
                }
                else
                {
                    view.mWindowH.WindowH.genRect2(roiName, InitRefRoi.MidR, InitRefRoi.MidC,
                        InitRefRoi.Phi, InitRefRoi.Length1, InitRefRoi.Length2, ref _RoiList);
                    if (RefRoiChangedFlag)
                    {
                        RefRoiCenterX.Text = InitRefRoi.MidC.ToString();
                        RefRoiCenterY.Text = InitRefRoi.MidR.ToString();
                        RefRoiLength1.Text = InitRefRoi.Length1.ToString();
                        RefRoiLength2.Text = InitRefRoi.Length2.ToString();
                        RefRoiAngel.Text = InitRefRoi.Deg.ToString();
                    }
                }
            }
        }

        public void InitMeasRoiMethod()
        {
            var view = ModuleView as HeightMeasurementView;
            if (view == null) return;

            if (MeasRoiSource == eMeasRoiSource.LinkRegion && !string.IsNullOrEmpty(MeasRoiLinkText))
            {
                HRegion region = (HRegion)GetLinkValue(MeasRoiLinkText);
                if (region != null && region.IsInitialized())
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "blue",
                        new HObject(region)
                    ));
                    ShowHRoi();
                }
                return;
            }

            string roiName = ModuleParam.ModuleName + "MeasROI";
            if (_RoiList == null)
                _RoiList = new Dictionary<string, ROI>();

            if (TranMeasRoi.FlagLineStyle != null)
            {
                view.mWindowH.WindowH.genRect2(roiName, TranMeasRoi.MidR, TranMeasRoi.MidC,
                    TranMeasRoi.Phi, TranMeasRoi.Length1, TranMeasRoi.Length2, ref _RoiList);
            }
            else if (DispImage != null && !_RoiList.ContainsKey(roiName))
            {
                DispImage.GetImageSize(out int w, out int h);
                view.mWindowH.WindowH.genRect2(roiName, h / 2.0, w * 3.0 / 4.0,
                    0, w / 8.0, h / 8.0, ref _RoiList);
                TranMeasRoi.MidC = w * 3.0 / 4.0;
                TranMeasRoi.MidR = h / 2.0;
                TranMeasRoi.Length1 = w / 8.0;
                TranMeasRoi.Length2 = h / 8.0;
                TranMeasRoi.Deg = 0;

                MeasRoiCenterX.Text = TranMeasRoi.MidC.ToString();
                MeasRoiCenterY.Text = TranMeasRoi.MidR.ToString();
                MeasRoiLength1.Text = TranMeasRoi.Length1.ToString();
                MeasRoiLength2.Text = TranMeasRoi.Length2.ToString();
                MeasRoiAngel.Text = TranMeasRoi.Deg.ToString();
            }
            else if (DispImage != null && _RoiList.ContainsKey(roiName))
            {
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    view.mWindowH.WindowH.genRect2(roiName, TranMeasRoi.MidR, TranMeasRoi.MidC,
                        TranMeasRoi.Phi, TranMeasRoi.Length1, TranMeasRoi.Length2, ref _RoiList);
                    Aff.Affine2d(HomMat2D_Inverse, TranMeasRoi, InitMeasRoi);
                    InitMeasRoi.MidC = Math.Round(InitMeasRoi.MidC, 3);
                    InitMeasRoi.MidR = Math.Round(InitMeasRoi.MidR, 3);
                    InitMeasRoi.Length1 = Math.Round(InitMeasRoi.Length1, 3);
                    InitMeasRoi.Length2 = Math.Round(InitMeasRoi.Length2, 3);
                    InitMeasRoi.Deg = Math.Round(InitMeasRoi.Deg, 3);
                    if (MeasRoiChangedFlag)
                    {
                        MeasRoiCenterX.Text = InitMeasRoi.MidC.ToString();
                        MeasRoiCenterY.Text = InitMeasRoi.MidR.ToString();
                        MeasRoiLength1.Text = InitMeasRoi.Length1.ToString();
                        MeasRoiLength2.Text = InitMeasRoi.Length2.ToString();
                        MeasRoiAngel.Text = InitMeasRoi.Deg.ToString();
                    }
                }
                else
                {
                    view.mWindowH.WindowH.genRect2(roiName, InitMeasRoi.MidR, InitMeasRoi.MidC,
                        InitMeasRoi.Phi, InitMeasRoi.Length1, InitMeasRoi.Length2, ref _RoiList);
                    if (MeasRoiChangedFlag)
                    {
                        MeasRoiCenterX.Text = InitMeasRoi.MidC.ToString();
                        MeasRoiCenterY.Text = InitMeasRoi.MidR.ToString();
                        MeasRoiLength1.Text = InitMeasRoi.Length1.ToString();
                        MeasRoiLength2.Text = InitMeasRoi.Length2.ToString();
                        MeasRoiAngel.Text = InitMeasRoi.Deg.ToString();
                    }
                }
            }
        }

        private int GetFilterCount(LinkVarModel linkVar, int total, bool isPercent)
        {
            double val = 0;
            double.TryParse(GetLinkValue(linkVar)?.ToString(), out val);
            if (isPercent)
                return (int)Math.Round(total * val / 100.0);
            return (int)val;
        }
        #endregion

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private string _RefRoiLinkText;
        public string RefRoiLinkText
        {
            get { return _RefRoiLinkText; }
            set { Set(ref _RefRoiLinkText, value); }
        }

        private string _MeasRoiLinkText;
        public string MeasRoiLinkText
        {
            get { return _MeasRoiLinkText; }
            set { Set(ref _MeasRoiLinkText, value); }
        }

        private string _RefBasePlaneLinkText;
        public string RefBasePlaneLinkText
        {
            get { return _RefBasePlaneLinkText; }
            set { Set(ref _RefBasePlaneLinkText, value); }
        }

        private eRefRoiSource _RefRoiSource = eRefRoiSource.Manual;
        public eRefRoiSource RefRoiSource
        {
            get { return _RefRoiSource; }
            set { Set(ref _RefRoiSource, value); }
        }

        private eMeasRoiSource _MeasRoiSource = eMeasRoiSource.Manual;
        public eMeasRoiSource MeasRoiSource
        {
            get { return _MeasRoiSource; }
            set { Set(ref _MeasRoiSource, value); }
        }

        // 基准 ROI 参数
        public LinkVarModel RefRoiCenterX { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel RefRoiCenterY { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel RefRoiLength1 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel RefRoiLength2 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel RefRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // 测量 ROI 参数
        public LinkVarModel MeasRoiCenterX { get; set; } = new LinkVarModel() { Text = "300" };
        public LinkVarModel MeasRoiCenterY { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel MeasRoiLength1 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel MeasRoiLength2 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel MeasRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // 几何对象
        public ROIRectangle2 InitRefRoi = new ROIRectangle2();
        public ROIRectangle2 TranRefRoi = new ROIRectangle2();
        public ROIRectangle2 TempRefRoi = new ROIRectangle2();
        public ROIRectangle2 InitMeasRoi = new ROIRectangle2();
        public ROIRectangle2 TranMeasRoi = new ROIRectangle2();
        public ROIRectangle2 TempMeasRoi = new ROIRectangle2();

        [NonSerialized] bool DisenableAffine2d = false;
        [NonSerialized] bool RefRoiChangedFlag = false;
        [NonSerialized] bool MeasRoiChangedFlag = false;

        [NonSerialized]
        private Dictionary<string, ROI> _RoiList;

        private eMeasureMode _MeasureMode = eMeasureMode.Average;
        public eMeasureMode MeasureMode
        {
            get { return _MeasureMode; }
            set { Set(ref _MeasureMode, value); }
        }

        private eFilterMode _FilterMode = eFilterMode.All;
        public eFilterMode FilterMode
        {
            get { return _FilterMode; }
            set { Set(ref _FilterMode, value); }
        }

        private bool _FitPlaneForRef = false;
        public bool FitPlaneForRef
        {
            get { return _FitPlaneForRef; }
            set { Set(ref _FitPlaneForRef, value); }
        }

        public LinkVarModel FilterM { get; set; } = new LinkVarModel() { Text = "3" };
        public LinkVarModel FilterN { get; set; } = new LinkVarModel() { Text = "5" };

        private bool _FilterMIsPercent = false;
        public bool FilterMIsPercent
        {
            get { return _FilterMIsPercent; }
            set { Set(ref _FilterMIsPercent, value); }
        }

        private bool _FilterNIsPercent = false;
        public bool FilterNIsPercent
        {
            get { return _FilterNIsPercent; }
            set { Set(ref _FilterNIsPercent, value); }
        }

        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private bool _SortOutput = false;
        public bool SortOutput
        {
            get { return _SortOutput; }
            set { Set(ref _SortOutput, value); }
        }

        private bool _EnableJudgment = false;
        public bool EnableJudgment
        {
            get { return _EnableJudgment; }
            set { Set(ref _EnableJudgment, value); }
        }

        private int _ResampleFactor = 1;
        public int ResampleFactor
        {
            get { return _ResampleFactor; }
            set { Set(ref _ResampleFactor, Math.Max(1, value)); }
        }

        private double _HeightDifference = -9999999.0;
        public double HeightDifference
        {
            get { return _HeightDifference; }
            set { Set(ref _HeightDifference, value); }
        }
        #endregion


        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as HeightMeasurementView;
            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }
            view.mWindowH.hControl.MouseUp += HControl_MouseUp;

            RefRoiCenterX.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiCenterY.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiLength1.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiLength2.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiAngel.TextChanged = new Action(() => { RefRoiChanged(); });

            MeasRoiCenterX.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiCenterY.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiLength1.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiLength2.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiAngel.TextChanged = new Action(() => { MeasRoiChanged(); });

            if (DispImage == null || !DispImage.IsInitialized())
            {
                SetDefaultLink();
                if (InputImageLinkText == null) return;
            }
            GetDispImage(InputImageLinkText, true);
            if (DispImage != null && DispImage.IsInitialized())
            {
                InitRefRoiMethod();
                InitMeasRoiMethod();
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
                        var view = ModuleView as HeightMeasurementView;
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

        private void RefRoiChanged()
        {
            if (RefRoiChangedFlag) return;
            InitRefRoi.MidC = Convert.ToDouble(GetLinkValue(RefRoiCenterX));
            InitRefRoi.MidR = Convert.ToDouble(GetLinkValue(RefRoiCenterY));
            InitRefRoi.Length1 = Convert.ToDouble(GetLinkValue(RefRoiLength1));
            InitRefRoi.Length2 = Convert.ToDouble(GetLinkValue(RefRoiLength2));
            InitRefRoi.Deg = Convert.ToDouble(GetLinkValue(RefRoiAngel));
            DisenableAffine2d = true;
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                Aff.Affine2d(HomMat2D, InitRefRoi, TempRefRoi);
            }
            else
            {
                TempRefRoi.MidC = InitRefRoi.MidC;
                TempRefRoi.MidR = InitRefRoi.MidR;
                TempRefRoi.Length1 = InitRefRoi.Length1;
                TempRefRoi.Length2 = InitRefRoi.Length2;
                TempRefRoi.Deg = InitRefRoi.Deg;
            }
            ExeModule();
            InitRefRoiMethod();
        }

        private void MeasRoiChanged()
        {
            if (MeasRoiChangedFlag) return;
            InitMeasRoi.MidC = Convert.ToDouble(GetLinkValue(MeasRoiCenterX));
            InitMeasRoi.MidR = Convert.ToDouble(GetLinkValue(MeasRoiCenterY));
            InitMeasRoi.Length1 = Convert.ToDouble(GetLinkValue(MeasRoiLength1));
            InitMeasRoi.Length2 = Convert.ToDouble(GetLinkValue(MeasRoiLength2));
            InitMeasRoi.Deg = Convert.ToDouble(GetLinkValue(MeasRoiAngel));
            DisenableAffine2d = true;
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                Aff.Affine2d(HomMat2D, InitMeasRoi, TempMeasRoi);
            }
            else
            {
                TempMeasRoi.MidC = InitMeasRoi.MidC;
                TempMeasRoi.MidR = InitMeasRoi.MidR;
                TempMeasRoi.Length1 = InitMeasRoi.Length1;
                TempMeasRoi.Length2 = InitMeasRoi.Length2;
                TempMeasRoi.Deg = InitMeasRoi.Deg;
            }
            ExeModule();
            InitMeasRoiMethod();
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as HeightMeasurementView;
            if (view == null || view.mWindowH == null) return;

            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            string refRoiName = ModuleParam.ModuleName + "RefROI";
            string measRoiName = ModuleParam.ModuleName + "MeasROI";

            if (roi is ROIRectangle2 rect2)
            {
                if (index == refRoiName)
                {
                    TempRefRoi.MidC = Math.Round(rect2.MidC, 3);
                    TempRefRoi.MidR = Math.Round(rect2.MidR, 3);
                    TempRefRoi.Length1 = Math.Round(rect2.Length1, 3);
                    TempRefRoi.Length2 = Math.Round(rect2.Length2, 3);
                    TempRefRoi.Deg = Math.Round(rect2.Deg, 3);
                    RefRoiChangedFlag = true;
                    DisenableAffine2d = true;
                    ExeModule();
                    InitRefRoiMethod();
                    RefRoiChangedFlag = false;
                }
                else if (index == measRoiName)
                {
                    TempMeasRoi.MidC = Math.Round(rect2.MidC, 3);
                    TempMeasRoi.MidR = Math.Round(rect2.MidR, 3);
                    TempMeasRoi.Length1 = Math.Round(rect2.Length1, 3);
                    TempMeasRoi.Length2 = Math.Round(rect2.Length2, 3);
                    TempMeasRoi.Deg = Math.Round(rect2.Deg, 3);
                    MeasRoiChangedFlag = true;
                    DisenableAffine2d = true;
                    ExeModule();
                    InitMeasRoiMethod();
                    MeasRoiChangedFlag = false;
                }
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
                case eLinkCommand.RefRoiCenterX:
                    RefRoiCenterX.Text = obj.LinkName;
                    break;
                case eLinkCommand.RefRoiCenterY:
                    RefRoiCenterY.Text = obj.LinkName;
                    break;
                case eLinkCommand.RefRoiLength1:
                    RefRoiLength1.Text = obj.LinkName;
                    break;
                case eLinkCommand.RefRoiLength2:
                    RefRoiLength2.Text = obj.LinkName;
                    break;
                case eLinkCommand.RefRoiAngel:
                    RefRoiAngel.Text = obj.LinkName;
                    break;
                case eLinkCommand.RefRoiLink:
                    RefRoiLinkText = obj.LinkName;
                    break;
                case eLinkCommand.RefBasePlaneLink:
                    RefBasePlaneLinkText = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiCenterX:
                    MeasRoiCenterX.Text = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiCenterY:
                    MeasRoiCenterY.Text = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiLength1:
                    MeasRoiLength1.Text = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiLength2:
                    MeasRoiLength2.Text = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiAngel:
                    MeasRoiAngel.Text = obj.LinkName;
                    break;
                case eLinkCommand.MeasRoiLink:
                    MeasRoiLinkText = obj.LinkName;
                    break;
                case eLinkCommand.FilterM:
                    FilterM.Text = obj.LinkName;
                    break;
                case eLinkCommand.FilterN:
                    FilterN.Text = obj.LinkName;
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
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.RefRoiCenterX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiCenterX");
                                break;
                            case eLinkCommand.RefRoiCenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiCenterY");
                                break;
                            case eLinkCommand.RefRoiLength1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiLength1");
                                break;
                            case eLinkCommand.RefRoiLength2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiLength2");
                                break;
                            case eLinkCommand.RefRoiAngel:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiAngel");
                                break;
                            case eLinkCommand.RefRoiLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefRoiLink");
                                break;
                            case eLinkCommand.RefBasePlaneLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},RefBasePlaneLink");
                                break;
                            case eLinkCommand.MeasRoiCenterX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiCenterX");
                                break;
                            case eLinkCommand.MeasRoiCenterY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiCenterY");
                                break;
                            case eLinkCommand.MeasRoiLength1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiLength1");
                                break;
                            case eLinkCommand.MeasRoiLength2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiLength2");
                                break;
                            case eLinkCommand.MeasRoiAngel:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiAngel");
                                break;
                            case eLinkCommand.MeasRoiLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},MeasRoiLink");
                                break;
                            case eLinkCommand.FilterM:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},FilterM");
                                break;
                            case eLinkCommand.FilterN:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},FilterN");
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["RefRoiLinkText"] = RefRoiLinkText ?? "";
            obj["MeasRoiLinkText"] = MeasRoiLinkText ?? "";
            obj["RefBasePlaneLinkText"] = RefBasePlaneLinkText ?? "";
            obj["RefRoiSource"] = (int)RefRoiSource;
            obj["MeasRoiSource"] = (int)MeasRoiSource;
            obj["MeasureMode"] = (int)MeasureMode;
            obj["FilterMode"] = (int)FilterMode;
            obj["FitPlaneForRef"] = FitPlaneForRef;
            obj["FilterMIsPercent"] = FilterMIsPercent;
            obj["FilterNIsPercent"] = FilterNIsPercent;
            obj["ShowRegion"] = ShowRegion;
            obj["SortOutput"] = SortOutput;
            obj["EnableJudgment"] = EnableJudgment;
            obj["ResampleFactor"] = ResampleFactor;
            obj["RefRoiCenterX"] = RefRoiCenterX?.Text ?? "";
            obj["RefRoiCenterY"] = RefRoiCenterY?.Text ?? "";
            obj["RefRoiLength1"] = RefRoiLength1?.Text ?? "";
            obj["RefRoiLength2"] = RefRoiLength2?.Text ?? "";
            obj["RefRoiAngel"] = RefRoiAngel?.Text ?? "";
            obj["MeasRoiCenterX"] = MeasRoiCenterX?.Text ?? "";
            obj["MeasRoiCenterY"] = MeasRoiCenterY?.Text ?? "";
            obj["MeasRoiLength1"] = MeasRoiLength1?.Text ?? "";
            obj["MeasRoiLength2"] = MeasRoiLength2?.Text ?? "";
            obj["MeasRoiAngel"] = MeasRoiAngel?.Text ?? "";
            obj["FilterM"] = FilterM?.Text ?? "";
            obj["FilterN"] = FilterN?.Text ?? "";
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
                if (obj["RefRoiLinkText"] != null) RefRoiLinkText = obj["RefRoiLinkText"].ToString();
                if (obj["MeasRoiLinkText"] != null) MeasRoiLinkText = obj["MeasRoiLinkText"].ToString();
                if (obj["RefBasePlaneLinkText"] != null) RefBasePlaneLinkText = obj["RefBasePlaneLinkText"].ToString();
                if (obj["RefRoiSource"] != null) RefRoiSource = (eRefRoiSource)obj["RefRoiSource"].Value<int>();
                if (obj["MeasRoiSource"] != null) MeasRoiSource = (eMeasRoiSource)obj["MeasRoiSource"].Value<int>();
                if (obj["MeasureMode"] != null) MeasureMode = (eMeasureMode)obj["MeasureMode"].Value<int>();
                if (obj["FilterMode"] != null) FilterMode = (eFilterMode)obj["FilterMode"].Value<int>();
                if (obj["FitPlaneForRef"] != null) FitPlaneForRef = obj["FitPlaneForRef"].Value<bool>();
                if (obj["FilterMIsPercent"] != null) FilterMIsPercent = obj["FilterMIsPercent"].Value<bool>();
                if (obj["FilterNIsPercent"] != null) FilterNIsPercent = obj["FilterNIsPercent"].Value<bool>();
                if (obj["ShowRegion"] != null) ShowRegion = obj["ShowRegion"].Value<bool>();
                if (obj["SortOutput"] != null) SortOutput = obj["SortOutput"].Value<bool>();
                if (obj["EnableJudgment"] != null) EnableJudgment = obj["EnableJudgment"].Value<bool>();
                if (obj["ResampleFactor"] != null) ResampleFactor = obj["ResampleFactor"].Value<int>();
                if (obj["RefRoiCenterX"] != null && RefRoiCenterX != null) RefRoiCenterX.Text = obj["RefRoiCenterX"].ToString();
                if (obj["RefRoiCenterY"] != null && RefRoiCenterY != null) RefRoiCenterY.Text = obj["RefRoiCenterY"].ToString();
                if (obj["RefRoiLength1"] != null && RefRoiLength1 != null) RefRoiLength1.Text = obj["RefRoiLength1"].ToString();
                if (obj["RefRoiLength2"] != null && RefRoiLength2 != null) RefRoiLength2.Text = obj["RefRoiLength2"].ToString();
                if (obj["RefRoiAngel"] != null && RefRoiAngel != null) RefRoiAngel.Text = obj["RefRoiAngel"].ToString();
                if (obj["MeasRoiCenterX"] != null && MeasRoiCenterX != null) MeasRoiCenterX.Text = obj["MeasRoiCenterX"].ToString();
                if (obj["MeasRoiCenterY"] != null && MeasRoiCenterY != null) MeasRoiCenterY.Text = obj["MeasRoiCenterY"].ToString();
                if (obj["MeasRoiLength1"] != null && MeasRoiLength1 != null) MeasRoiLength1.Text = obj["MeasRoiLength1"].ToString();
                if (obj["MeasRoiLength2"] != null && MeasRoiLength2 != null) MeasRoiLength2.Text = obj["MeasRoiLength2"].ToString();
                if (obj["MeasRoiAngel"] != null && MeasRoiAngel != null) MeasRoiAngel.Text = obj["MeasRoiAngel"].ToString();
                if (obj["FilterM"] != null && FilterM != null) FilterM.Text = obj["FilterM"].ToString();
                if (obj["FilterN"] != null && FilterN != null) FilterN.Text = obj["FilterN"].ToString();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"HeightMeasurementViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
