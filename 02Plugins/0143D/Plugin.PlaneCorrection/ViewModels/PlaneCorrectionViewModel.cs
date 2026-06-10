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
using Newtonsoft.Json.Linq;

namespace Plugin.PlaneCorrection.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        PlaneImageLink,
        InitNx,
        InitNy,
        InitNz,
        InitD,
        InitRoiCenterX,
        InitRoiCenterY,
        InitRoiLength1,
        InitRoiLength2,
        InitRoiAngel,
        InitTranslateZ,
        InitResolutionX,
        InitResolutionY,
        InitResolutionZ,
    }

    public enum ePlaneMode
    {
        PlaneImage,
        Manual,
    }

    public enum eCorrectionMode
    {
        Quick,
        Projection,
        PointToPlaneDistance,
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

            // HALCON 图像/区域对象需要手动释放。这里统一在 finally 里释放，
            // 但 zCorrectedObj 成功交给 CorrectedImage 后不能释放，否则输出图像句柄会失效。
            HRegion domain = null;
            HRegion rawDomain = null;
            HRegion imageRegion = null;
            HImage linkedPlaneImg = null;
            HObject chObj = null;
            HObject chRealObj = null;
            HObject currentPlaneObj = null;
            HObject refPlaneObj = null;
            HObject residualObj = null;
            HObject refWithOffsetObj = null;
            HObject zCorrectedObj = null;
            HObject zDiffToRefObj = null;
            HObject distanceObj = null;
            HObject imageXObj = null;
            HObject imageYObj = null;
            HObject scaledXObj = null;
            HObject scaledYObj = null;
            HObject scaledZObj = null;
            HObject xTransObj = null;
            HObject yTransObj = null;
            HObject zProjectionPhysicalObj = null;
            HObject displayDomainObj = null;
            HTuple sourceObjectModel3D = null;
            HTuple transformedObjectModel3D = null;
            bool correctedImageAssigned = false;
            bool displayDomainAssigned = false;

            try
            {
                // 清理上一次运行的显示结果，并获取平台当前的二维仿射变换矩阵。
                // 如果流程前面做过定位/坐标变换，ROI 会通过 HomMat2D 映射到当前图像坐标。
                ClearRoiAndText();
                GetHomMat2D();

                // 读取输入高度图。当前插件固定取第 1 通道作为 Z 高度通道。
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

                double refAlpha = 0.0;
                double refBeta = 0.0;
                double refGamma = 0.0;
                double refCenterR = 0.0;
                double refCenterC = 0.0;
                double refNx = 0.0;
                double refNy = 0.0;
                double refNz = 1.0;
                double refD = 0.0;

                // 逆变换 ROI：当用户在显示窗口拖动 ROI 后，把显示坐标反算回原始 ROI 参数。
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

                // 正变换 ROI：把界面里的原始 ROI 参数转换到当前图像坐标。
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

                // 获取检测 ROI，并裁剪到图像有效范围内。
                // 这样 ROI 画出图像边界时不会在后续拟合、取值、统计时抛 HALCON 越界异常。
                rawDomain = GetRoiRegion();
                if (rawDomain == null || !rawDomain.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 构造整幅图像的有效区域，用于和 ROI 求交集。
                DispImage.GetImageSize(out int width, out int height);
                HOperatorSet.GenRectangle1(
                    out HObject imageRegionObj,
                    new HTuple(0),
                    new HTuple(0),
                    new HTuple(height - 1),
                    new HTuple(width - 1));
                imageRegion = new HRegion(imageRegionObj);
                domain = ClipRegionToImage(rawDomain, imageRegion);
                if (domain == null || !domain.IsInitialized() || domain.Area <= 0)
                {
                    Logger.AddLog("平面校正 ROI 超出图像范围或有效区域为空。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 检查输入图像是否至少有 1 个通道。
                HOperatorSet.CountChannels(DispImage, out HTuple channelCount);
                int nChannels = channelCount.I;

                // 提取高度通道并转为 real 类型，避免整型高度图参与平面运算时精度丢失。
                int targetChannel = 1;
                if (targetChannel > nChannels)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception($"目标通道 {targetChannel} 超出图像通道数 {nChannels}"));
                    return false;
                }

                HOperatorSet.AccessChannel(DispImage, out chObj, targetChannel);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");
                HOperatorSet.AreaCenter(domain, out _, out HTuple roiR, out HTuple roiC);
                double resolutionX = Convert.ToDouble(GetLinkValue(InitResolutionX));
                double resolutionY = Convert.ToDouble(GetLinkValue(InitResolutionY));
                double resolutionZ = Convert.ToDouble(GetLinkValue(InitResolutionZ));
                if (resolutionX <= 0 || resolutionY <= 0 || resolutionZ <= 0)
                {
                    Logger.AddLog("X/Y/Z 分辨率必须大于 0。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 拟合当前工件平面。
                // HALCON 一阶面公式：
                // z = Alpha * (row - row0) + Beta * (col - col0) + Gamma
                // 这里 row0/col0 使用 ROI 中心，所以 Gamma 就是当前平面在 ROI 中心的高度。
                HOperatorSet.FitSurfaceFirstOrder(
                    domain,
                    chRealObj,
                    "regression",
                    5,
                    0.1,
                    out HTuple inAlphaT,
                    out HTuple inBetaT,
                    out HTuple inGammaT
                );
                double inGamma = inGammaT.D;

                // 生成“当前拟合平面图”，尺寸与输入高度图一致。
                // 后面用 原始高度图 - 当前拟合平面图 得到局部残差/缺陷形貌。
                HOperatorSet.GenImageSurfaceFirstOrder(
                    out currentPlaneObj,
                    "real",
                    inAlphaT,
                    inBetaT,
                    inGammaT,
                    roiR,
                    roiC,
                    width,
                    height
                );

                if (PlaneMode == ePlaneMode.Manual)
                {
                    // 手动基准平面使用标准平面方程：
                    // Nx * x + Ny * y + Nz * z = D
                    // 其中 x 对应 HALCON 的 column，y 对应 row，z 对应高度。
                    double nxRef = Convert.ToDouble(GetLinkValue(InitNx));
                    double nyRef = Convert.ToDouble(GetLinkValue(InitNy));
                    double nzRef = Convert.ToDouble(GetLinkValue(InitNz));
                    double dRef = Convert.ToDouble(GetLinkValue(InitD));
                    double normalLen = Math.Sqrt(nxRef * nxRef + nyRef * nyRef + nzRef * nzRef);
                    if (normalLen <= 1e-12 || Math.Abs(nzRef) <= 1e-12)
                    {
                        Logger.AddLog("基准平面参数无效，Nz 不能为 0，法向量长度不能为 0。", eMsgType.Warn);
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    // 转成 HALCON 的一阶面参数：
                    // z = (-Ny / Nz) * row + (-Nx / Nz) * col + D / Nz
                    // 因为中心点使用 (0,0)，所以 Gamma = D / Nz。
                    refAlpha = -nyRef * resolutionY / (nzRef * resolutionZ);
                    refBeta = -nxRef * resolutionX / (nzRef * resolutionZ);
                    refGamma = dRef / (nzRef * resolutionZ);
                    refCenterR = 0.0;
                    refCenterC = 0.0;
                    refNx = nxRef / normalLen;
                    refNy = nyRef / normalLen;
                    refNz = nzRef / normalLen;
                    refD = dRef / normalLen;
                    EnsureNormalUp(ref refNx, ref refNy, ref refNz, ref refD);
                }
                else
                {
                    // 图像基准模式：读取一张基准平面高度图，并拟合它的整体平面姿态。
                    // 后续只使用拟合出来的基准平面，不直接把整张基准图叠到结果里，
                    // 这样可以避免基准图上的噪声、局部缺陷进入校正图。
                    if (string.IsNullOrEmpty(PlaneImageLinkText))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    var savedDisp = DispImage;
                    try
                    {
                        GetDispImage(PlaneImageLinkText, true);
                        if (DispImage == null || !DispImage.IsInitialized())
                        {
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }
                        linkedPlaneImg = new HImage(DispImage);
                    }
                    finally
                    {
                        DispImage = savedDisp;
                        if (ModuleView != null && ModuleView.mWindowH != null)
                            ModuleView.mWindowH.HobjectToHimage(DispImage);
                    }

                    HObject pChObj = null;
                    HObject pRealObj = null;
                    HRegion linkedDomain = null;
                    try
                    {
                        HOperatorSet.CountChannels(linkedPlaneImg, out HTuple pc);
                        if (pc.I < 1)
                        {
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }

                        HOperatorSet.AccessChannel(linkedPlaneImg, out pChObj, 1);
                        HOperatorSet.ConvertImageType(pChObj, out pRealObj, "real");
                        linkedDomain = linkedPlaneImg.GetDomain();
                        HOperatorSet.FitSurfaceFirstOrder(
                            linkedDomain,
                            pRealObj,
                            "regression",
                            5,
                            0.1,
                            out HTuple pAlpha,
                            out HTuple pBeta,
                            out HTuple pGamma
                        );
                        HOperatorSet.AreaCenter(linkedDomain, out _, out HTuple refR, out HTuple refC);

                        // 保存基准平面的 HALCON 一阶面参数和拟合中心。
                        // 后面用这些参数生成与当前输入图同尺寸的基准平面图。
                        refAlpha = pAlpha.D;
                        refBeta = pBeta.D;
                        refGamma = pGamma.D;
                        refCenterR = refR.D;
                        refCenterC = refC.D;
                        ConvertSurfaceToStandardPlane(
                            refAlpha,
                            refBeta,
                            refGamma,
                            refCenterR,
                            refCenterC,
                            resolutionX,
                            resolutionY,
                            resolutionZ,
                            out refNx,
                            out refNy,
                            out refNz,
                            out refD);
                    }
                    finally
                    {
                        linkedDomain?.Dispose();
                        pChObj?.Dispose();
                        pRealObj?.Dispose();
                    }
                }

                // 生成“基准平面图”，尺寸与当前输入高度图一致。
                // 它代表校正后的目标姿态：当前工件面最终要与这个平面平行。
                HOperatorSet.GenImageSurfaceFirstOrder(
                    out refPlaneObj,
                    "real",
                    refAlpha,
                    refBeta,
                    refGamma,
                    refCenterR,
                    refCenterC,
                    width,
                    height
                );

                // 计算当前平面相对基准平面的高度差，参考点使用当前 ROI 中心。
                // 因为当前平面的拟合中心就是 ROI 中心，所以当前面中心高度 = inGamma。
                // 这个 heightOffset 会被加回校正图，保证距离/高度差不被抹掉。
                double zInCenter = inGamma;
                double zRefCenter = refAlpha * (roiR.D - refCenterR) +
                                    refBeta * (roiC.D - refCenterC) +
                                    refGamma;
                double heightOffset = zInCenter - zRefCenter;
                HeightDistance = Math.Round(heightOffset, 6);

                // 三种校正方式共用当前残差图：
                // residualObj = 原始高度图 - 当前拟合平面
                // 它表示当前工件上的局部凸起、凹陷和缺陷形貌。
                HOperatorSet.SubImage(chRealObj, currentPlaneObj, out residualObj, 1.0, 0.0);
                // “启用平移”对应界面里的平移设置。
                // 这里先只读取平移量，不马上参与各模式计算；
                // 三种模式先生成不带平移的结果，后面再统一对结果做最终平移。
                double translateZ = IsTranslateEnabled ? Convert.ToDouble(GetLinkValue(InitTranslateZ)) : 0.0;

                if (CorrectionMode == eCorrectionMode.Quick)
                {
                    // 快速校正：只在高度图上做 Z 趋势补偿。
                    // 结果姿态与基准平面平行，并保留当前相对基准的高度差。
                    // 这里先只加 heightOffset，不加 translateZ；
                    // translateZ 会在三种模式结果生成后统一作为最终平移处理。
                    HOperatorSet.ScaleImage(refPlaneObj, out refWithOffsetObj, 1.0, heightOffset);
                    HOperatorSet.AddImage(residualObj, refWithOffsetObj, out zCorrectedObj, 1.0, 0.0);
                }
                else if (CorrectionMode == eCorrectionMode.PointToPlaneDistance)
                {
                    // 点到面距离图：输出每个点到基准平面的法向距离。
                    // 这不是一张“校正后姿态图”，而是最适合平面度、凸起/凹陷检测的距离图。
                    // 这里先输出不带平移的基础距离图；
                    // translateZ 会在后面按 refNz 折算成法向距离偏移。
                    HOperatorSet.SubImage(chRealObj, refPlaneObj, out zDiffToRefObj, 1.0, 0.0);
                    HOperatorSet.ScaleImage(zDiffToRefObj, out distanceObj, refNz, 0.0);
                    zCorrectedObj = distanceObj;
                }
                else
                {
                    // 投影校正：把高度图转成真实 XYZ 点云，按 3D 几何把当前平面法向旋到基准平面法向，
                    // 再投影回高度图。该模式必须使用正确的 X/Y/Z 分辨率。
                    ConvertSurfaceToStandardPlane(
                        inAlphaT.D,
                        inBetaT.D,
                        inGammaT.D,
                        roiR.D,
                        roiC.D,
                        resolutionX,
                        resolutionY,
                        resolutionZ,
                        out double curNx,
                        out double curNy,
                        out double curNz,
                        out _);

                    HOperatorSet.GenImageSurfaceFirstOrder(out imageXObj, "real", 0, 1, 0, 0, 0, width, height);
                    HOperatorSet.GenImageSurfaceFirstOrder(out imageYObj, "real", 1, 0, 0, 0, 0, width, height);
                    HOperatorSet.ScaleImage(imageXObj, out scaledXObj, resolutionX, 0.0);
                    HOperatorSet.ScaleImage(imageYObj, out scaledYObj, resolutionY, 0.0);
                    HOperatorSet.ScaleImage(chRealObj, out scaledZObj, resolutionZ, 0.0);
                    HOperatorSet.XyzToObjectModel3d(scaledXObj, scaledYObj, scaledZObj, out sourceObjectModel3D);

                    double centerX = roiC.D * resolutionX;
                    double centerY = roiR.D * resolutionY;
                    double centerZ = zInCenter * resolutionZ;

                    HTuple homMat3D = BuildPlaneAlignmentHomMat3D(
                        curNx,
                        curNy,
                        curNz,
                        refNx,
                        refNy,
                        refNz,
                        centerX,
                        centerY,
                        centerZ);
                    HOperatorSet.AffineTransObjectModel3d(sourceObjectModel3D, homMat3D, out transformedObjectModel3D);
                    ObjectModel3dToXyzImage(
                        transformedObjectModel3D,
                        out xTransObj,
                        out yTransObj,
                        out zProjectionPhysicalObj);
                    HOperatorSet.ScaleImage(
                        zProjectionPhysicalObj,
                        out zCorrectedObj,
                        1.0 / resolutionZ,
                        // 这里只做单位换算，不加平移；
                        // translateZ 会在三种模式结果生成后统一作为最终平移处理。
                        0.0);
                }

                // 最终平移：先完成校正/距离图计算，再移动输出结果。
                // 快速校正、投影校正输出的是高度图，所以直接加 translateZ。
                // 点到面距离图输出的是到基准平面的法向距离；
                // Z 方向平移 translateZ 对法向距离的影响是 translateZ * refNz。
                double finalTranslate = CorrectionMode == eCorrectionMode.PointToPlaneDistance
                    ? translateZ * refNz
                    : translateZ;
                if (Math.Abs(finalTranslate) > 1e-12)
                {
                    HObject unshiftedCorrectedObj = zCorrectedObj;
                    HOperatorSet.ScaleImage(unshiftedCorrectedObj, out HObject shiftedCorrectedObj, 1.0, finalTranslate);
                    zCorrectedObj = shiftedCorrectedObj;

                    if (object.ReferenceEquals(distanceObj, unshiftedCorrectedObj))
                        distanceObj = null;
                    unshiftedCorrectedObj?.Dispose();
                }

                // 输出校正后的高度图，供后续 3D 测量插件继续使用。
                var correctedImage = new HImage(zCorrectedObj);
                CorrectedImage = new RImage(correctedImage);
                correctedImageAssigned = true;

                // 平面度/RMS 统计局部残差。
                // 点到面距离图直接统计距离图；其他校正方式统计 residualObj，避免基准平面斜率污染结果。
                HObject statsObj = CorrectionMode == eCorrectionMode.PointToPlaneDistance ? zCorrectedObj : residualObj;
                HOperatorSet.MinMaxGray(
                    domain,
                    statsObj,
                    0.0,
                    out HTuple minDev,
                    out HTuple maxDev,
                    out _
                );

                MaxDeviation = Math.Round(maxDev.D, 6);
                MinDeviation = Math.Round(minDev.D, 6);
                Flatness = Math.Round(maxDev.D - minDev.D, 6);

                HOperatorSet.Intensity(
                    domain,
                    statsObj,
                    out HTuple meanDiff,
                    out HTuple deviation
                );
                RmsError = Math.Round(deviation.D, 6);

                // 显示校正后的高度图。这里只做 0~255 的可视化缩放，
                // 不影响 CorrectedImage 输出的真实高度值。
                if (ShowCorrectedImage)
                {
                    HOperatorSet.MinMaxGray(
                        domain,
                        correctedImage,
                        0.0,
                        out HTuple dispMin,
                        out _,
                        out HTuple dispRange
                    );
                    double rangeVal = dispRange.D;
                    if (rangeVal > 0)
                    {
                        HImage dispScaled = correctedImage.ScaleImage(
                            255.0 / rangeVal,
                            -dispMin.D * 255.0 / rangeVal
                        );
                        DispImage = new RImage(dispScaled);
                    }
                }

                // 显示本次平面校正后的残差统计结果。
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

                // 显示参与平面拟合的 ROI 区域。
                if (ShowRegion && domain != null && domain.IsInitialized())
                {
                    HOperatorSet.CopyObj(domain, out displayDomainObj, 1, -1);
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "green",
                        displayDomainObj
                    ));
                    displayDomainAssigned = true;
                }

                ShowHRoi();
                InitRoiMethod();

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
                // 释放本次运行创建的临时 HALCON 对象。
                // zCorrectedObj 成功交给 CorrectedImage 后不能释放，否则下游输出图像会失效。
                domain?.Dispose();
                rawDomain?.Dispose();
                imageRegion?.Dispose();
                linkedPlaneImg?.Dispose();
                chObj?.Dispose();
                chRealObj?.Dispose();
                currentPlaneObj?.Dispose();
                refPlaneObj?.Dispose();
                residualObj?.Dispose();
                refWithOffsetObj?.Dispose();
                zDiffToRefObj?.Dispose();
                if (!object.ReferenceEquals(distanceObj, zCorrectedObj))
                    distanceObj?.Dispose();
                imageXObj?.Dispose();
                imageYObj?.Dispose();
                scaledXObj?.Dispose();
                scaledYObj?.Dispose();
                scaledZObj?.Dispose();
                xTransObj?.Dispose();
                yTransObj?.Dispose();
                zProjectionPhysicalObj?.Dispose();
                if (!displayDomainAssigned)
                    displayDomainObj?.Dispose();
                if (sourceObjectModel3D != null)
                    HOperatorSet.ClearObjectModel3d(sourceObjectModel3D);
                if (transformedObjectModel3D != null)
                    HOperatorSet.ClearObjectModel3d(transformedObjectModel3D);
                if (!correctedImageAssigned)
                    zCorrectedObj?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("校正后图像", "HImage", CorrectedImage);
            AddOutputParam("平面度", "double", Flatness);
            AddOutputParam("最大偏差", "double", MaxDeviation);
            AddOutputParam("最小偏差", "double", MinDeviation);
            AddOutputParam("RMS", "double", RmsError);
            AddOutputParam("高度距离", "double", HeightDistance);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

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

        private HRegion ClipRegionToImage(HRegion region, HRegion imageRegion)
        {
            if (region == null || !region.IsInitialized() ||
                imageRegion == null || !imageRegion.IsInitialized())
            {
                return null;
            }

            HOperatorSet.Intersection(region, imageRegion, out HObject clippedObj);
            return new HRegion(clippedObj);
        }

        private void ConvertSurfaceToStandardPlane(
            double alpha,
            double beta,
            double gamma,
            double centerR,
            double centerC,
            double resolutionX,
            double resolutionY,
            double resolutionZ,
            out double nx,
            out double ny,
            out double nz,
            out double d)
        {
            // HALCON 高度图一阶面：
            // zPixel = alpha * (row - centerR) + beta * (col - centerC) + gamma
            // 真实坐标：
            // X = col * resolutionX, Y = row * resolutionY, Z = zPixel * resolutionZ
            // 转成标准平面方程：nx * X + ny * Y + nz * Z = d
            nx = -beta * resolutionZ / resolutionX;
            ny = -alpha * resolutionZ / resolutionY;
            nz = 1.0;
            d = resolutionZ * (gamma - beta * centerC - alpha * centerR);

            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len <= 1e-12)
            {
                nx = 0.0;
                ny = 0.0;
                nz = 1.0;
                d = 0.0;
                return;
            }

            nx /= len;
            ny /= len;
            nz /= len;
            d /= len;
            EnsureNormalUp(ref nx, ref ny, ref nz, ref d);
        }

        private void ObjectModel3dToXyzImage(
            HTuple objectModel3D,
            out HObject xImage,
            out HObject yImage,
            out HObject zImage)
        {
            // XyzToObjectModel3d 生成的点云带有 xyz_map 映射。
            // from_xyz_map 会把变换后的 XYZ 点写回原始高度图网格，CamParam/Pose 会被忽略。
            HOperatorSet.ObjectModel3dToXyz(
                out xImage,
                out yImage,
                out zImage,
                objectModel3D,
                "from_xyz_map",
                new HTuple(),
                new HTuple());
        }

        private void EnsureNormalUp(ref double nx, ref double ny, ref double nz, ref double d)
        {
            if (nz >= 0)
                return;

            nx = -nx;
            ny = -ny;
            nz = -nz;
            d = -d;
        }

        private HTuple BuildPlaneAlignmentHomMat3D(
            double curNx,
            double curNy,
            double curNz,
            double refNx,
            double refNy,
            double refNz,
            double centerX,
            double centerY,
            double centerZ)
        {
            NormalizeVector(ref curNx, ref curNy, ref curNz);
            NormalizeVector(ref refNx, ref refNy, ref refNz);

            double vx = curNy * refNz - curNz * refNy;
            double vy = curNz * refNx - curNx * refNz;
            double vz = curNx * refNy - curNy * refNx;
            double s = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            double c = curNx * refNx + curNy * refNy + curNz * refNz;

            double[,] r;
            if (s < 1e-12)
            {
                if (c >= 0)
                {
                    r = new double[,]
                    {
                        { 1.0, 0.0, 0.0 },
                        { 0.0, 1.0, 0.0 },
                        { 0.0, 0.0, 1.0 }
                    };
                }
                else
                {
                    GetPerpendicularAxis(curNx, curNy, curNz, out vx, out vy, out vz);
                    r = BuildAxisAngleRotation(vx, vy, vz, Math.PI);
                }
            }
            else
            {
                vx /= s;
                vy /= s;
                vz /= s;
                double angle = Math.Atan2(s, c);
                r = BuildAxisAngleRotation(vx, vy, vz, angle);
            }

            double tx = centerX - (r[0, 0] * centerX + r[0, 1] * centerY + r[0, 2] * centerZ);
            double ty = centerY - (r[1, 0] * centerX + r[1, 1] * centerY + r[1, 2] * centerZ);
            double tz = centerZ - (r[2, 0] * centerX + r[2, 1] * centerY + r[2, 2] * centerZ);

            // HALCON HomMat3D 12 参数：3x3 旋转矩阵 + 平移列。
            return new HTuple(new double[]
            {
                r[0, 0], r[0, 1], r[0, 2], tx,
                r[1, 0], r[1, 1], r[1, 2], ty,
                r[2, 0], r[2, 1], r[2, 2], tz
            });
        }

        private void NormalizeVector(ref double x, ref double y, ref double z)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (len <= 1e-12)
            {
                x = 0.0;
                y = 0.0;
                z = 1.0;
                return;
            }

            x /= len;
            y /= len;
            z /= len;
        }

        private void GetPerpendicularAxis(double x, double y, double z, out double ax, out double ay, out double az)
        {
            if (Math.Abs(x) < Math.Abs(y))
            {
                ax = 0.0;
                ay = -z;
                az = y;
            }
            else
            {
                ax = -z;
                ay = 0.0;
                az = x;
            }

            NormalizeVector(ref ax, ref ay, ref az);
        }

        private double[,] BuildAxisAngleRotation(double ax, double ay, double az, double angle)
        {
            NormalizeVector(ref ax, ref ay, ref az);
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            double t = 1.0 - c;

            return new double[,]
            {
                { t * ax * ax + c, t * ax * ay - s * az, t * ax * az + s * ay },
                { t * ax * ay + s * az, t * ay * ay + c, t * ay * az - s * ax },
                { t * ax * az - s * ay, t * ay * az + s * ax, t * az * az + c }
            };
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

        private eCorrectionMode _CorrectionMode = eCorrectionMode.Quick;
        public eCorrectionMode CorrectionMode
        {
            get { return _CorrectionMode; }
            set { Set(ref _CorrectionMode, value); }
        }

        // 界面“启用平移”开关：只控制是否把 InitTranslateZ 加到最终输出结果，
        // 不参与平面拟合、不改变 ROI、不改变当前平面到基准平面的旋转角度。
        private bool _IsTranslateEnabled = false;
        public bool IsTranslateEnabled
        {
            get { return _IsTranslateEnabled; }
            set { Set(ref _IsTranslateEnabled, value); }
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

        private double _HeightDistance;
        public double HeightDistance
        {
            get { return _HeightDistance; }
            set { Set(ref _HeightDistance, value); }
        }

        [NonSerialized]
        private RImage _CorrectedImage;
        public RImage CorrectedImage
        {
            get { return _CorrectedImage; }
            set { Set(ref _CorrectedImage, value); }
        }

        // ROI 原始坐标输入
        public LinkVarModel InitNx { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitNy { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitNz { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel InitD { get; set; } = new LinkVarModel() { Text = "0" };
        // 界面“平移Z”：按输入高度图单位填写。只有 IsTranslateEnabled 为 true 时才会生效。
        public LinkVarModel InitTranslateZ { get; set; } = new LinkVarModel() { Text = "0" };
        public LinkVarModel InitResolutionX { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel InitResolutionY { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel InitResolutionZ { get; set; } = new LinkVarModel() { Text = "1" };
        public LinkVarModel InitRoiCenterX { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiCenterY { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiLength1 { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiLength2 { get; set; } = new LinkVarModel() { Text = "10" };
        public LinkVarModel InitRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // 几何对象
        public ROIRectangle2 InitRoi = new ROIRectangle2();
        public ROIRectangle2 TranRoi = new ROIRectangle2();
        public ROIRectangle2 TempRoi = new ROIRectangle2();

        // 标志位
        bool DisenableAffine2d = false;
        bool InitRoiChanged_Flag = false;

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
            InitNx.TextChanged = new Action(() => { InitRoiChanged(); });
            InitNy.TextChanged = new Action(() => { InitRoiChanged(); });
            InitNz.TextChanged = new Action(() => { InitRoiChanged(); });
            InitD.TextChanged = new Action(() => { InitRoiChanged(); });
            InitTranslateZ.TextChanged = new Action(() => { InitRoiChanged(); });
            InitResolutionX.TextChanged = new Action(() => { InitRoiChanged(); });
            InitResolutionY.TextChanged = new Action(() => { InitRoiChanged(); });
            InitResolutionZ.TextChanged = new Action(() => { InitRoiChanged(); });

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
                case eLinkCommand.InitNx:
                    InitNx.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitNy:
                    InitNy.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitNz:
                    InitNz.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitD:
                    InitD.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitTranslateZ:
                    InitTranslateZ.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitResolutionX:
                    InitResolutionX.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitResolutionY:
                    InitResolutionY.Text = obj.LinkName;
                    break;
                case eLinkCommand.InitResolutionZ:
                    InitResolutionZ.Text = obj.LinkName;
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
                            case eLinkCommand.InitNx:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitNx");
                                break;
                            case eLinkCommand.InitNy:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitNy");
                                break;
                            case eLinkCommand.InitNz:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitNz");
                                break;
                            case eLinkCommand.InitD:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitD");
                                break;
                            case eLinkCommand.InitTranslateZ:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitTranslateZ");
                                break;
                            case eLinkCommand.InitResolutionX:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitResolutionX");
                                break;
                            case eLinkCommand.InitResolutionY:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitResolutionY");
                                break;
                            case eLinkCommand.InitResolutionZ:
                                CommonMethods.GetModuleList(ModuleParam,
                                    VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>()
                                    .Publish($"{ModuleGuid},InitResolutionZ");
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

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["PlaneImageLinkText"] = PlaneImageLinkText ?? "";
            obj["PlaneMode"] = (int)PlaneMode;
            obj["CorrectionMode"] = (int)CorrectionMode;
            obj["IsTranslateEnabled"] = IsTranslateEnabled;
            obj["UseRoi"] = UseRoi;
            obj["ShowCorrectedImage"] = ShowCorrectedImage;
            obj["ShowRegion"] = ShowRegion;
            obj["ShowResultPoint"] = ShowResultPoint;
            obj["InitNx"] = InitNx?.Text ?? "";
            obj["InitNy"] = InitNy?.Text ?? "";
            obj["InitNz"] = InitNz?.Text ?? "";
            obj["InitD"] = InitD?.Text ?? "";
            obj["InitTranslateZ"] = InitTranslateZ?.Text ?? "";
            obj["InitResolutionX"] = InitResolutionX?.Text ?? "";
            obj["InitResolutionY"] = InitResolutionY?.Text ?? "";
            obj["InitResolutionZ"] = InitResolutionZ?.Text ?? "";
            obj["InitRoiCenterX"] = InitRoiCenterX?.Text ?? "";
            obj["InitRoiCenterY"] = InitRoiCenterY?.Text ?? "";
            obj["InitRoiLength1"] = InitRoiLength1?.Text ?? "";
            obj["InitRoiLength2"] = InitRoiLength2?.Text ?? "";
            obj["InitRoiAngel"] = InitRoiAngel?.Text ?? "";
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
                if (obj["PlaneImageLinkText"] != null) PlaneImageLinkText = obj["PlaneImageLinkText"].ToString();
                if (obj["PlaneMode"] != null) PlaneMode = (ePlaneMode)obj["PlaneMode"].Value<int>();
                if (obj["CorrectionMode"] != null) CorrectionMode = (eCorrectionMode)obj["CorrectionMode"].Value<int>();
                if (obj["IsTranslateEnabled"] != null) IsTranslateEnabled = obj["IsTranslateEnabled"].Value<bool>();
                if (obj["UseRoi"] != null) UseRoi = obj["UseRoi"].Value<bool>();
                if (obj["ShowCorrectedImage"] != null) ShowCorrectedImage = obj["ShowCorrectedImage"].Value<bool>();
                if (obj["ShowRegion"] != null) ShowRegion = obj["ShowRegion"].Value<bool>();
                if (obj["ShowResultPoint"] != null) ShowResultPoint = obj["ShowResultPoint"].Value<bool>();
                if (obj["InitNx"] != null && InitNx != null) InitNx.Text = obj["InitNx"].ToString();
                if (obj["InitNy"] != null && InitNy != null) InitNy.Text = obj["InitNy"].ToString();
                if (obj["InitNz"] != null && InitNz != null) InitNz.Text = obj["InitNz"].ToString();
                if (obj["InitD"] != null && InitD != null) InitD.Text = obj["InitD"].ToString();
                if (obj["InitTranslateZ"] != null && InitTranslateZ != null) InitTranslateZ.Text = obj["InitTranslateZ"].ToString();
                if (obj["InitResolutionX"] != null && InitResolutionX != null) InitResolutionX.Text = obj["InitResolutionX"].ToString();
                if (obj["InitResolutionY"] != null && InitResolutionY != null) InitResolutionY.Text = obj["InitResolutionY"].ToString();
                if (obj["InitResolutionZ"] != null && InitResolutionZ != null) InitResolutionZ.Text = obj["InitResolutionZ"].ToString();
                if (obj["InitRoiCenterX"] != null && InitRoiCenterX != null) InitRoiCenterX.Text = obj["InitRoiCenterX"].ToString();
                if (obj["InitRoiCenterY"] != null && InitRoiCenterY != null) InitRoiCenterY.Text = obj["InitRoiCenterY"].ToString();
                if (obj["InitRoiLength1"] != null && InitRoiLength1 != null) InitRoiLength1.Text = obj["InitRoiLength1"].ToString();
                if (obj["InitRoiLength2"] != null && InitRoiLength2 != null) InitRoiLength2.Text = obj["InitRoiLength2"].ToString();
                if (obj["InitRoiAngel"] != null && InitRoiAngel != null) InitRoiAngel.Text = obj["InitRoiAngel"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"PlaneCorrectionViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
