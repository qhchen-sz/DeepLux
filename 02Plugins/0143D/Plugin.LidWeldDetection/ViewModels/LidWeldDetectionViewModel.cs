using EventMgrLib;
using HalconDotNet;
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
using Plugin.LidWeldDetection.Views;
using System;
using System.ComponentModel;
using System.Linq;
using VM.Halcon;
using VM.Halcon.Config;

namespace Plugin.LidWeldDetection.ViewModels
{
    #region enums
    public enum eLinkCommand
    {
        InputImageLink,
    }

    public enum eInterpolationMethod
    {
        [EnumDescription("线性邻插值")] Bilinear,
        [EnumDescription("最近邻插值")] NearestNeighbor,
        [EnumDescription("cubic B插值")] Bicubic,
        [EnumDescription("Catmull-Rom样条插值")] CatmullRom,
        [EnumDescription("natural cubic插值")] NaturalCubic,
    }

    public enum eDefectType
    {
        [EnumDescription("凹")] Concave,
        [EnumDescription("凸")] Convex,
        [EnumDescription("凹&凸")] Both,
    }

    public enum eDisplayMode
    {
        [EnumDescription("原始图像")] Original,
        [EnumDescription("拟合结果")] Fitted,
        [EnumDescription("差值结果")] Difference,
        [EnumDescription("检测结果")] Detection,
    }
    #endregion

    [Category("3D")]
    [DisplayName("顶盖焊检测")]
    [ModuleImageName("LidWeldDetection")]
    [Serializable]
    public class LidWeldDetectionViewModel : ModuleBase
    {
        #region Runtime image caches
        [NonSerialized] private HImage _originalImage;
        [NonSerialized] private HImage _fittedImage;
        [NonSerialized] private HImage _diffImage;
        [NonSerialized] private HRegion _defectRegion;
        [NonSerialized] private HRegion _concaveRegion;
        [NonSerialized] private HRegion _convexRegion;
        [NonSerialized] private HRegion _workpieceContourRegion;
        [NonSerialized] private HRegion _inspectionRegion;
        #endregion

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
            HImage realImage = null;
            HImage fittedImage = null;
            HObject diffObj = null;
            HImage diffImage = null;
            HRegion defectRegion = null;
            HRegion concaveRegion = null;
            HRegion convexRegion = null;
            HRegion validRegion = null;
            HRegion offsetWorkpieceRegion = null;
            HRegion inspectionRegion = null;
            HRegion workpieceContourRegion = null;

            try
            {
                // 释放旧的缓存图像
                _originalImage?.Dispose(); _originalImage = null;
                _fittedImage?.Dispose(); _fittedImage = null;
                _diffImage?.Dispose(); _diffImage = null;
                _defectRegion?.Dispose(); _defectRegion = null;
                _concaveRegion?.Dispose(); _concaveRegion = null;
                _convexRegion?.Dispose(); _convexRegion = null;
                _workpieceContourRegion?.Dispose(); _workpieceContourRegion = null;
                _inspectionRegion?.Dispose(); _inspectionRegion = null;
                HasDefect = false;
                DefectCount = 0;
                DefectArea = 0;
                MaxConcaveDepth = 0;
                MaxConvexHeight = 0;

                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // === 获取输入图像 ===
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);
                _originalImage = new HImage(DispImage);

                // 提取高度通道（通道1），转 real 类型
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (channelCount.I < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("图像无有效通道"));
                    return false;
                }
                HOperatorSet.AccessChannel(sourceImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // Z像素单位换算：原始高度值 × ResolutionZ → 实际高度。
                // 例如原始值单位为 um、希望以 mm 检测时，ResolutionZ 应设为 0.001。
                if (Math.Abs(ResolutionZ - 1.0) > 1e-12)
                {
                    HOperatorSet.ScaleImage(new HImage(chRealObj), out HObject scaledObj, ResolutionZ, 0.0);
                    chRealObj.Dispose();
                    chRealObj = scaledObj;
                }
                realImage = new HImage(chRealObj);

                // 获取原图尺寸
                HOperatorSet.GetImageSize(chRealObj, out HTuple width, out HTuple height);
                int srcW = width.I;
                int srcH = height.I;

                // === 过滤无效数据 ===
                // Keyence 高度图常用负哨兵值表示无效点，例如 -21474836。
                // 有效高度范围使用换算后的实际高度单位；默认把负高度过滤掉。
                // 如果现场确认 0 也是空点，可把最小有效高度调到 0.0001。
                double validMin = MinValidHeight;
                double validMax = Math.Max(validMin, MaxValidHeight);
                HOperatorSet.Threshold(chRealObj, out HObject validRegionObj, validMin, validMax);
                validRegion = new HRegion(validRegionObj);
                validRegionObj.Dispose();
                if (!validRegion.IsInitialized() || validRegion.Area <= 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("高度图没有有效高度区域"));
                    return false;
                }

                offsetWorkpieceRegion = BuildOffsetWorkpieceRegion(validRegion, ContourOffset);
                HOperatorSet.Intersection(offsetWorkpieceRegion, validRegion, out HObject inspectionRegionObj);
                inspectionRegion = new HRegion(inspectionRegionObj);
                inspectionRegionObj.Dispose();
                if (!inspectionRegion.IsInitialized() || inspectionRegion.Area <= 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("轮廓偏移后没有有效检测区域"));
                    return false;
                }

                _inspectionRegion = new HRegion(inspectionRegion);

                workpieceContourRegion = BuildWorkpieceContourRegion(offsetWorkpieceRegion);
                _workpieceContourRegion = workpieceContourRegion != null && workpieceContourRegion.IsInitialized() && workpieceContourRegion.Area > 0
                    ? new HRegion(workpieceContourRegion)
                    : null;

                // 按检测连通域分别生成拟合面，避免背景和不同工件段互相影响。
                string componentInterpDown = "constant";
                string componentInterpUp = GetHalconInterpolationMode();
                fittedImage = BuildComponentFittedImage(realImage, inspectionRegion, srcW, srcH, componentInterpDown, componentInterpUp);
                if (fittedImage == null || !fittedImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("Failed to build fitted surface from valid regions"));
                    return false;
                }
                _fittedImage = new HImage(fittedImage);

                // === 差值图 = 原始 - 拟合（只在检测区域计算）===
                HOperatorSet.ReduceDomain(realImage, inspectionRegion, out HObject validRealObj);
                HImage validRealImage = new HImage(validRealObj);
                HOperatorSet.ReduceDomain(fittedImage, inspectionRegion, out HObject validFittedObj);
                HImage validFittedImage = new HImage(validFittedObj);
                HOperatorSet.SubImage(validRealImage, validFittedImage, out diffObj, 1.0, 0.0);
                validRealImage.Dispose();
                validFittedImage.Dispose();
                diffImage = new HImage(diffObj);
                _diffImage = new HImage(diffObj);

                // === 缺陷检测 ===
                bool hasConcave = false;
                bool hasConvex = false;

                if (DefectType == eDefectType.Concave || DefectType == eDefectType.Both)
                {
                    // 凹缺陷：差值 < -凹陷阈值，且必须在检测区域内
                    HOperatorSet.Threshold(diffImage, out HObject concaveObj, -999999.0, -ConcaveThreshold);
                    HOperatorSet.Intersection(new HRegion(concaveObj), inspectionRegion, out HObject concaveValidObj);
                    concaveObj.Dispose();
                    HRegion rawConcaveRegion = new HRegion(concaveValidObj);
                    concaveValidObj.Dispose();
                    concaveRegion = FilterDefectRegionByArea(rawConcaveRegion);
                    rawConcaveRegion.Dispose();
                    if (concaveRegion.IsInitialized() && concaveRegion.Area > 0)
                        hasConcave = true;
                }

                if (DefectType == eDefectType.Convex || DefectType == eDefectType.Both)
                {
                    // 凸缺陷：差值 > 凸起阈值，且必须在检测区域内
                    HOperatorSet.Threshold(diffImage, out HObject convexObj, ConvexThreshold, 999999.0);
                    HOperatorSet.Intersection(new HRegion(convexObj), inspectionRegion, out HObject convexValidObj);
                    convexObj.Dispose();
                    HRegion rawConvexRegion = new HRegion(convexValidObj);
                    convexValidObj.Dispose();
                    convexRegion = FilterDefectRegionByArea(rawConvexRegion);
                    rawConvexRegion.Dispose();
                    if (convexRegion.IsInitialized() && convexRegion.Area > 0)
                        hasConvex = true;
                }

                // 合并缺陷区域
                if (DefectType == eDefectType.Both && hasConcave && hasConvex)
                {
                    HOperatorSet.Union2(concaveRegion, convexRegion, out HObject unionObj);
                    defectRegion = new HRegion(unionObj);
                    unionObj.Dispose();
                }
                else if (DefectType == eDefectType.Concave && hasConcave)
                {
                    defectRegion = new HRegion(concaveRegion);
                }
                else if (DefectType == eDefectType.Convex && hasConvex)
                {
                    defectRegion = new HRegion(convexRegion);
                }
                else
                {
                    defectRegion = new HRegion();
                }

                _defectRegion = defectRegion != null && defectRegion.IsInitialized() ? new HRegion(defectRegion) : null;
                _concaveRegion = concaveRegion != null && concaveRegion.IsInitialized() ? new HRegion(concaveRegion) : null;
                _convexRegion = convexRegion != null && convexRegion.IsInitialized() ? new HRegion(convexRegion) : null;

                // === 统计缺陷信息 ===
                CalculateDefectStats(diffImage, concaveRegion, convexRegion);
                HasDefect = DefectCount > 0;

                // === 根据显示模式更新 DispImage ===
                UpdateDisplayImage();

                // === 显示工件轮廓 ===
                if (ShowWorkpieceContour && _workpieceContourRegion != null && _workpieceContourRegion.IsInitialized() && _workpieceContourRegion.Area > 0)
                {
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测范围,
                        "yellow",
                        new HObject(_workpieceContourRegion)
                    ));
                }

                // === 显示缺陷区域 ===
                if (ShowDefectRegion)
                {
                    if (_concaveRegion != null && _concaveRegion.IsInitialized() && _concaveRegion.Area > 0)
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "blue",
                            new HObject(_concaveRegion)
                        ));
                    }
                    if (_convexRegion != null && _convexRegion.IsInitialized() && _convexRegion.Area > 0)
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "red",
                            new HObject(_convexRegion)
                        ));
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
            finally
            {
                sourceImage?.Dispose();
                chObj?.Dispose();
                chRealObj?.Dispose();
                realImage?.Dispose();
                fittedImage?.Dispose();
                diffObj?.Dispose();
                diffImage?.Dispose();
                validRegion?.Dispose();
                offsetWorkpieceRegion?.Dispose();
                inspectionRegion?.Dispose();
                workpieceContourRegion?.Dispose();
                // defectRegion, concaveRegion, convexRegion 已保存到 _xxxRegion，不在此处释放
            }
        }

        private HRegion BuildOffsetWorkpieceRegion(HRegion validRegion, double contourOffset)
        {
            if (validRegion == null || !validRegion.IsInitialized() || validRegion.Area <= 0)
                return new HRegion();

            HObject filledObj = null;
            HObject offsetObj = null;
            try
            {
                HOperatorSet.FillUp(validRegion, out filledObj);

                if (Math.Abs(contourOffset) < 1e-9)
                    HOperatorSet.CopyObj(filledObj, out offsetObj, 1, 1);
                else if (contourOffset < 0)
                    HOperatorSet.ErosionCircle(filledObj, out offsetObj, Math.Abs(contourOffset));
                else
                    HOperatorSet.DilationCircle(filledObj, out offsetObj, contourOffset);

                return new HRegion(offsetObj);
            }
            finally
            {
                filledObj?.Dispose();
                offsetObj?.Dispose();
            }
        }

        private HRegion BuildWorkpieceContourRegion(HRegion validRegion)
        {
            if (validRegion == null || !validRegion.IsInitialized() || validRegion.Area <= 0)
                return new HRegion();

            HObject filledObj = null;
            HObject boundaryObj = null;
            try
            {
                // 先填充有效区域内部小孔，避免内部无效点被显示成工件外轮廓。
                HOperatorSet.FillUp(validRegion, out filledObj);
                HOperatorSet.Boundary(filledObj, out boundaryObj, "inner");
                return new HRegion(boundaryObj);
            }
            finally
            {
                filledObj?.Dispose();
                boundaryObj?.Dispose();
            }
        }

        private void CalculateDefectStats(HImage diffImage, HRegion concaveRegion, HRegion convexRegion)
        {
            DefectCount = 0;
            DefectArea = 0;
            MaxConcaveDepth = 0;
            MaxConvexHeight = 0;

            // 凹缺陷统计
            if (concaveRegion != null && concaveRegion.IsInitialized() && concaveRegion.Area > 0)
            {
                HOperatorSet.Connection(concaveRegion, out HObject connectedConcave);
                HOperatorSet.CountObj(connectedConcave, out HTuple ccCount);
                HOperatorSet.AreaCenter(connectedConcave, out HTuple ccArea, out _, out _);
                HOperatorSet.MinMaxGray(concaveRegion, diffImage, 0, out HTuple minVal, out _, out _);

                DefectCount += ccCount.I;
                DefectArea += ccArea.D;
                if (minVal.D < 0)
                    MaxConcaveDepth = Math.Round(-minVal.D, 6);

                connectedConcave.Dispose();
            }

            // 凸缺陷统计
            if (convexRegion != null && convexRegion.IsInitialized() && convexRegion.Area > 0)
            {
                HOperatorSet.Connection(convexRegion, out HObject connectedConvex);
                HOperatorSet.CountObj(connectedConvex, out HTuple cvCount);
                HOperatorSet.AreaCenter(connectedConvex, out HTuple cvArea, out _, out _);
                HOperatorSet.MinMaxGray(convexRegion, diffImage, 0, out _, out HTuple maxVal, out _);

                DefectCount += cvCount.I;
                DefectArea += cvArea.D;
                MaxConvexHeight = Math.Round(maxVal.D, 6);

                connectedConvex.Dispose();
            }
        }

        private HImage BuildComponentFittedImage(
            HImage realImage,
            HRegion validRegion,
            int srcW,
            int srcH,
            string interpDown,
            string interpUp)
        {
            HObject fittedCanvasObj = null;
            HObject connectedObj = null;
            HRegion fittedRegion = null;

            try
            {
                HOperatorSet.GenImageConst(out fittedCanvasObj, "real", srcW, srcH);
                HOperatorSet.Connection(validRegion, out connectedObj);
                HOperatorSet.CountObj(connectedObj, out HTuple componentCount);

                for (int i = 1; i <= componentCount.I; i++)
                {
                    HObject componentObj = null;
                    HRegion componentRegion = null;
                    HObject rectObj = null;
                    HRegion rectRegion = null;
                    HObject localRealObj = null;
                    HObject localCropObj = null;
                    HImage localCropImage = null;
                    HObject localInvalidObj = null;
                    HRegion localInvalidRegion = null;
                    HObject movedInvalidObj = null;
                    HRegion movedInvalidRegion = null;
                    HObject localFilledObj = null;
                    HObject sampledObj = null;
                    HObject fittedLocalObj = null;

                    try
                    {
                        HOperatorSet.SelectObj(connectedObj, out componentObj, i);
                        componentRegion = new HRegion(componentObj);

                        if (!componentRegion.IsInitialized() || componentRegion.Area <= 0)
                            continue;

                        HOperatorSet.SmallestRectangle1(componentRegion,
                            out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);

                        int r1 = Math.Max(0, (int)Math.Floor(row1.D));
                        int c1 = Math.Max(0, (int)Math.Floor(col1.D));
                        int r2 = Math.Min(srcH - 1, (int)Math.Ceiling(row2.D));
                        int c2 = Math.Min(srcW - 1, (int)Math.Ceiling(col2.D));
                        int localW = Math.Max(1, c2 - c1 + 1);
                        int localH = Math.Max(1, r2 - r1 + 1);

                        HOperatorSet.GenRectangle1(out rectObj, r1, c1, r2, c2);
                        rectRegion = new HRegion(rectObj);

                        HOperatorSet.ReduceDomain(realImage, rectRegion, out localRealObj);
                        HOperatorSet.CropPart(localRealObj, out localCropObj, r1, c1, localW, localH);
                        localCropImage = new HImage(localCropObj);

                        HOperatorSet.Intensity(componentRegion, realImage, out HTuple meanHeight, out _);
                        HOperatorSet.Difference(rectRegion, componentRegion, out localInvalidObj);
                        localInvalidRegion = new HRegion(localInvalidObj);
                        if (localInvalidRegion.IsInitialized() && localInvalidRegion.Area > 0)
                        {
                            HOperatorSet.MoveRegion(localInvalidRegion, out movedInvalidObj, -r1, -c1);
                            movedInvalidRegion = new HRegion(movedInvalidObj);
                            HOperatorSet.PaintRegion(movedInvalidRegion, localCropImage, out localFilledObj, meanHeight, "fill");
                            movedInvalidRegion.Dispose();
                            movedInvalidRegion = null;
                            movedInvalidObj.Dispose();
                            movedInvalidObj = null;
                            localCropImage.Dispose();
                            localCropImage = null;
                        }
                        else
                        {
                            localFilledObj = localCropObj;
                            localCropObj = null;
                            localCropImage.Dispose();
                            localCropImage = null;
                        }

                        int intervalX = Math.Max(1, SampleRateX);
                        int intervalY = Math.Max(1, SampleRateY);
                        int targetW = Math.Max(2, (int)Math.Ceiling((double)localW / intervalX));
                        int targetH = Math.Max(2, (int)Math.Ceiling((double)localH / intervalY));

                        HOperatorSet.ZoomImageSize(localFilledObj, out sampledObj, targetW, targetH, interpDown);
                        HOperatorSet.ZoomImageSize(sampledObj, out fittedLocalObj, localW, localH, interpUp);
                        HOperatorSet.GetRegionPoints(componentRegion, out HTuple componentRows, out HTuple componentCols);
                        HOperatorSet.TupleSub(componentRows, r1, out HTuple localRows);
                        HOperatorSet.TupleSub(componentCols, c1, out HTuple localCols);
                        HOperatorSet.GetGrayval(fittedLocalObj, localRows, localCols, out HTuple fittedValues);
                        HOperatorSet.SetGrayval(fittedCanvasObj, componentRows, componentCols, fittedValues);

                        if (fittedRegion == null)
                        {
                            fittedRegion = new HRegion(componentRegion);
                        }
                        else
                        {
                            HOperatorSet.Union2(fittedRegion, componentRegion, out HObject mergedObj);
                            fittedRegion.Dispose();
                            fittedRegion = new HRegion(mergedObj);
                            mergedObj.Dispose();
                        }

                    }
                    finally
                    {
                        componentObj?.Dispose();
                        componentRegion?.Dispose();
                        rectObj?.Dispose();
                        rectRegion?.Dispose();
                        localRealObj?.Dispose();
                        localCropObj?.Dispose();
                        localCropImage?.Dispose();
                        localInvalidObj?.Dispose();
                        localInvalidRegion?.Dispose();
                        movedInvalidObj?.Dispose();
                        movedInvalidRegion?.Dispose();
                        localFilledObj?.Dispose();
                        sampledObj?.Dispose();
                        fittedLocalObj?.Dispose();
                    }
                }

                if (fittedRegion == null || !fittedRegion.IsInitialized() || fittedRegion.Area <= 0)
                    return null;

                HObject fittedObj = null;
                try
                {
                    HOperatorSet.ChangeDomain(fittedCanvasObj, fittedRegion, out fittedObj);
                    return new HImage(fittedObj);
                }
                finally
                {
                    fittedObj?.Dispose();
                }
            }
            finally
            {
                fittedCanvasObj?.Dispose();
                connectedObj?.Dispose();
                fittedRegion?.Dispose();
            }
        }

        private HRegion FilterDefectRegionByArea(HRegion region)
        {
            if (region == null || !region.IsInitialized() || region.Area <= 0)
                return new HRegion();

            HObject connected = null;
            HObject selected = null;
            try
            {
                HOperatorSet.Connection(region, out connected);
                double minArea = Math.Max(0, MinDefectArea);
                double maxArea = Math.Max(minArea, MaxDefectArea);
                HOperatorSet.SelectShape(connected, out selected, "area", "and", minArea, maxArea);
                return new HRegion(selected);
            }
            finally
            {
                connected?.Dispose();
                selected?.Dispose();
            }
        }

        private void UpdateDisplayImage()
        {
            try
            {
                switch (DisplayMode)
                {
                    case eDisplayMode.Original:
                        if (_originalImage != null && _originalImage.IsInitialized())
                            DispImage = new RImage(new HImage(_originalImage));
                        break;

                    case eDisplayMode.Fitted:
                        if (_fittedImage != null && _fittedImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_fittedImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;

                    case eDisplayMode.Difference:
                        if (_diffImage != null && _diffImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_diffImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;

                    case eDisplayMode.Detection:
                        // 检测结果：显示差值图伪彩色 + 缺陷标记
                        if (_diffImage != null && _diffImage.IsInitialized())
                        {
                            HImage disp = ScaleToByte(_diffImage);
                            if (disp != null) DispImage = new RImage(disp);
                        }
                        break;
                }

                if (ModuleView != null && ModuleView.mWindowH != null)
                    ModuleView.mWindowH.HobjectToHimage(DispImage);
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }

        /// <summary>将 real 图像缩放到 0-255 byte 范围用于显示</summary>
        private HImage ScaleToByte(HImage realImage)
        {
            try
            {
                HOperatorSet.MinMaxGray(realImage.GetDomain(), realImage, 0, out HTuple minVal, out HTuple maxVal, out HTuple range);
                if (range.D <= 0)
                {
                    HOperatorSet.ConvertImageType(realImage, out HObject byteObj, "byte");
                    return new HImage(byteObj);
                }
                HImage scaled = realImage.ScaleImage(255.0 / range.D, -minVal.D * 255.0 / range.D);
                HOperatorSet.ConvertImageType(scaled, out HObject byteObj2, "byte");
                scaled.Dispose();
                return new HImage(byteObj2);
            }
            catch
            {
                return null;
            }
        }

        private string GetHalconInterpolationMode()
        {
            switch (InterpolationMethod)
            {
                case eInterpolationMethod.NearestNeighbor: return "constant";
                case eInterpolationMethod.Bilinear: return "bilinear";
                case eInterpolationMethod.Bicubic: return "bicubic";
                case eInterpolationMethod.CatmullRom: return "bicubic"; // 占位，后续可扩展
                case eInterpolationMethod.NaturalCubic: return "bicubic"; // 占位，后续可扩展
                default: return "bilinear";
            }
        }

        public override void AddOutputParams()
        {
            AddOutputParam("工件轮廓", "HRegion", _workpieceContourRegion);
            AddOutputParam("检测区域", "HRegion", _inspectionRegion);
            AddOutputParam("缺陷区域", "HRegion", _defectRegion);
            AddOutputParam("是否有缺陷", "bool", HasDefect);
            AddOutputParam("缺陷数量", "int", DefectCount);
            AddOutputParam("缺陷面积", "double", DefectArea);
            AddOutputParam("最大凹陷深度", "double", MaxConcaveDepth);
            AddOutputParam("最大凸起高度", "double", MaxConvexHeight);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Properties
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        // 保留旧属性名 ResolutionZ，避免破坏已保存方案；界面显示为“Z像素单位”。
        private double _ResolutionZ = 1.0;
        public double ResolutionZ
        {
            get { return _ResolutionZ; }
            set { Set(ref _ResolutionZ, value); }
        }

        private double _MinValidHeight = 0.0;
        public double MinValidHeight
        {
            get { return _MinValidHeight; }
            set { Set(ref _MinValidHeight, value); }
        }

        private double _MaxValidHeight = 999999.0;
        public double MaxValidHeight
        {
            get { return _MaxValidHeight; }
            set { Set(ref _MaxValidHeight, value); }
        }

        private int _SampleRateX = 10;
        public int SampleRateX
        {
            get { return _SampleRateX; }
            set { Set(ref _SampleRateX, Math.Max(2, value)); }
        }

        private int _SampleRateY = 80;
        public int SampleRateY
        {
            get { return _SampleRateY; }
            set { Set(ref _SampleRateY, Math.Max(2, value)); }
        }

        private eInterpolationMethod _InterpolationMethod = eInterpolationMethod.Bilinear;
        public eInterpolationMethod InterpolationMethod
        {
            get { return _InterpolationMethod; }
            set { Set(ref _InterpolationMethod, value); }
        }

        private eDefectType _DefectType = eDefectType.Concave;
        public eDefectType DefectType
        {
            get { return _DefectType; }
            set { Set(ref _DefectType, value); }
        }

        private double _ConcaveThreshold = 0.20;
        public double ConcaveThreshold
        {
            get { return _ConcaveThreshold; }
            set { Set(ref _ConcaveThreshold, value); }
        }

        private double _ConvexThreshold = 0.20;
        public double ConvexThreshold
        {
            get { return _ConvexThreshold; }
            set { Set(ref _ConvexThreshold, value); }
        }

        private double _MinDefectArea = 10.0;
        public double MinDefectArea
        {
            get { return _MinDefectArea; }
            set { Set(ref _MinDefectArea, Math.Max(0, value)); }
        }

        private double _MaxDefectArea = 999999999.0;
        public double MaxDefectArea
        {
            get { return _MaxDefectArea; }
            set { Set(ref _MaxDefectArea, Math.Max(0, value)); }
        }

        private bool _ShowDefectRegion = true;
        public bool ShowDefectRegion
        {
            get { return _ShowDefectRegion; }
            set { Set(ref _ShowDefectRegion, value); }
        }

        private bool _ShowWorkpieceContour = true;
        public bool ShowWorkpieceContour
        {
            get { return _ShowWorkpieceContour; }
            set { Set(ref _ShowWorkpieceContour, value); }
        }

        private double _ContourOffset = 0.0;
        public double ContourOffset
        {
            get { return _ContourOffset; }
            set { Set(ref _ContourOffset, value); }
        }

        private eDisplayMode _DisplayMode = eDisplayMode.Original;
        public eDisplayMode DisplayMode
        {
            get { return _DisplayMode; }
            set
            {
                Set(ref _DisplayMode, value);
                UpdateDisplayImage();
            }
        }

        // 输出结果
        private bool _HasDefect;
        public bool HasDefect
        {
            get { return _HasDefect; }
            set { Set(ref _HasDefect, value); }
        }

        private int _DefectCount;
        public int DefectCount
        {
            get { return _DefectCount; }
            set { Set(ref _DefectCount, value); }
        }

        private double _DefectArea;
        public double DefectArea
        {
            get { return _DefectArea; }
            set { Set(ref _DefectArea, value); }
        }

        private double _MaxConcaveDepth;
        public double MaxConcaveDepth
        {
            get { return _MaxConcaveDepth; }
            set { Set(ref _MaxConcaveDepth, value); }
        }

        private double _MaxConvexHeight;
        public double MaxConvexHeight
        {
            get { return _MaxConvexHeight; }
            set { Set(ref _MaxConvexHeight, value); }
        }
        #endregion

        #region Commands
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as LidWeldDetectionView;
            if (view != null)
            {
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
                GetDispImage(InputImageLinkText, true);
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1]);
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
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

        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
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
                        var view = ModuleView as LidWeldDetectionView;
                        if (view != null) view.Close();
                    });
                }
                return _ConfirmCommand;
            }
        }

        [NonSerialized]
        private CommandBase _SwitchDisplayCommand;
        public CommandBase SwitchDisplayCommand
        {
            get
            {
                if (_SwitchDisplayCommand == null)
                {
                    _SwitchDisplayCommand = new CommandBase((obj) =>
                    {
                        if (obj is eDisplayMode mode)
                            DisplayMode = mode;
                    });
                }
                return _SwitchDisplayCommand;
            }
        }
        #endregion

        #region Serialization
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["ResolutionZ"] = ResolutionZ;
            obj["MinValidHeight"] = MinValidHeight;
            obj["MaxValidHeight"] = MaxValidHeight;
            obj["SampleRateX"] = SampleRateX;
            obj["SampleRateY"] = SampleRateY;
            obj["InterpolationMethod"] = (int)InterpolationMethod;
            obj["DefectType"] = (int)DefectType;
            obj["ConcaveThreshold"] = ConcaveThreshold;
            obj["ConvexThreshold"] = ConvexThreshold;
            obj["MinDefectArea"] = MinDefectArea;
            obj["MaxDefectArea"] = MaxDefectArea;
            obj["ShowDefectRegion"] = ShowDefectRegion;
            obj["ShowWorkpieceContour"] = ShowWorkpieceContour;
            obj["ContourOffset"] = ContourOffset;
            obj["DisplayMode"] = (int)DisplayMode;
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
                if (obj["ResolutionZ"] != null) ResolutionZ = obj["ResolutionZ"].Value<double>();
                if (obj["MinValidHeight"] != null) MinValidHeight = obj["MinValidHeight"].Value<double>();
                if (obj["MaxValidHeight"] != null) MaxValidHeight = obj["MaxValidHeight"].Value<double>();
                if (obj["SampleRateX"] != null) SampleRateX = obj["SampleRateX"].Value<int>();
                if (obj["SampleRateY"] != null) SampleRateY = obj["SampleRateY"].Value<int>();
                if (obj["InterpolationMethod"] != null) InterpolationMethod = (eInterpolationMethod)obj["InterpolationMethod"].Value<int>();
                if (obj["DefectType"] != null) DefectType = (eDefectType)obj["DefectType"].Value<int>();
                if (obj["ConcaveThreshold"] != null) ConcaveThreshold = obj["ConcaveThreshold"].Value<double>();
                if (obj["ConvexThreshold"] != null) ConvexThreshold = obj["ConvexThreshold"].Value<double>();
                if (obj["MinDefectArea"] != null) MinDefectArea = obj["MinDefectArea"].Value<double>();
                if (obj["MaxDefectArea"] != null) MaxDefectArea = obj["MaxDefectArea"].Value<double>();
                if (obj["ShowDefectRegion"] != null) ShowDefectRegion = obj["ShowDefectRegion"].Value<bool>();
                if (obj["ShowWorkpieceContour"] != null) ShowWorkpieceContour = obj["ShowWorkpieceContour"].Value<bool>();
                if (obj["ContourOffset"] != null) ContourOffset = obj["ContourOffset"].Value<double>();
                if (obj["DisplayMode"] != null) DisplayMode = (eDisplayMode)obj["DisplayMode"].Value<int>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"LidWeldDetectionViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
