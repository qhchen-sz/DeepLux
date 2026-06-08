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
    /// <summary>
    /// 链接命令枚举：定义模块所有可链接的外部变量，用于变量链接面板的交互。
    /// 每个值对应一个可被其他模块链接或从其他模块获取数据的参数。
    /// </summary>
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,      // 输入图像链接
        RefRoiCenterX,       // 基准ROI中心X
        RefRoiCenterY,       // 基准ROI中心Y
        RefRoiLength1,       // 基准ROI长边
        RefRoiLength2,       // 基准ROI短边
        RefRoiAngel,         // 基准ROI角度
        RefRoiLink,          // 基准ROI区域链接
        RefBasePlaneLink,    // 基准平面图像链接
        MeasRoiCenterX,      // 测量ROI中心X
        MeasRoiCenterY,      // 测量ROI中心Y
        MeasRoiLength1,      // 测量ROI长边
        MeasRoiLength2,      // 测量ROI短边
        MeasRoiAngel,        // 测量ROI角度
        MeasRoiLink,         // 测量ROI区域链接
        FilterM,             // 筛选参数m
        FilterN,             // 筛选参数n
    }

    /// <summary>
    /// 测量统计模式：对筛选后的断差值采用不同的统计方式。
    /// </summary>
    public enum eMeasureMode
    {
        [EnumDescription("平均值")]
        Average,
        [EnumDescription("最大值")]
        Max,
        [EnumDescription("最小值")]
        Min,
    }

    /// <summary>
    /// 筛选模式：对断差值进行排序后，按规则剔除异常点。
    /// 升序排序后，最小值在前，最大值在后。
    /// </summary>
    public enum eFilterMode
    {
        [EnumDescription("全部")]
        All,                          // 不筛选，保留所有点
        [EnumDescription("剔除最大m个点,取n个点")]
        RemoveMaxMTakeN,              // 剔除最大m个 → 取剩余中最大的n个
        [EnumDescription("剔除最小m个点,取n个点")]
        RemoveMinMTakeN,              // 剔除最小m个 → 取剩余中最小的n个
        [EnumDescription("剔除最小m个点,剔除最大n个点")]
        RemoveMinMRemoveMaxN,         // 剔除最小m个和最大n个 → 取中间部分
    }

    /// <summary>
    /// 基准区域来源：手动绘制 / 链接外部区域 / 链接外部基准平面图像。
    /// </summary>
    public enum eRefRoiSource
    {
        [EnumDescription("手动绘制")]
        Manual,
        [EnumDescription("链接区域")]
        LinkRegion,
        [EnumDescription("链接基准")]
        LinkBase,
    }

    /// <summary>
    /// 测量区域来源：手动绘制 / 链接外部区域。
    /// </summary>
    public enum eMeasRoiSource
    {
        [EnumDescription("手动绘制")]
        Manual,
        [EnumDescription("链接区域")]
        LinkRegion,
    }
    #endregion

    /// <summary>
    /// 高度测量（断差检测）模块。
    ///
    /// 功能概述：
    /// 1. 在3D高度图上定义"基准区域"和"测量区域"两个ROI
    /// 2. 计算基准面的参考高度（支持三种模式：外部基准平面 / 拟合平面 / 平均高度）
    /// 3. 提取测量区域内的断差值（实际高度 - 基准高度）
    /// 4. 对断差值进行重采样、排序、筛选，最终按模式（平均/最大/最小）统计断差
    /// 5. 支持仿射变换：当图像经过旋转/缩放/平移时，ROI坐标自动跟随
    /// 6. 支持窗口交互：在Halcon窗口中拖动ROI后自动重新执行并更新参数
    ///
    /// 典型应用场景：3D视觉中检测两个平面之间的高度差（如台阶高度、平面度偏差等）
    /// </summary>
    [Category("3D")]
    [DisplayName("检测高度")]
    [ModuleImageName("HeightMeasurement")]
    [Serializable]
    public class HeightMeasurementViewModel : ModuleBase
    {
        // ============================================================
        // 核心执行流程
        // ============================================================

        /// <summary>
        /// 设置默认输入图像链接：自动关联到流程中最后一个输出 HImage 的模块。
        /// </summary>
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

        /// <summary>
        /// 模块主执行方法。
        ///
        /// 处理流程：
        /// 1. 获取输入图像并提取高度通道（第1通道，real类型）
        /// 2. 逆变换：如果用户拖动了ROI，将图像坐标转回原始坐标
        /// 3. 正变换：根据原始坐标+仿射矩阵计算图像坐标
        /// 4. 生成基准/测量ROI区域，并裁剪到图像范围内
        /// 5. 计算断差图（三种基准模式）
        /// 6. 提取测量区域内的断差值
        /// 7. 重采样 → 排序 → 筛选 → 按模式统计
        /// 8. 显示结果区域和文字标注
        /// </summary>
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
            HRegion rawRefRegion = null;
            HRegion rawMeasRegion = null;
            HRegion imageRegion = null;

            try
            {
                // ---- 1. 获取输入图像 ----
                ClearRoiAndText();

                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 非窗口打开模式时主动获取图像；窗口模式已有DispImage
                if (!IsOpenWindows)
                    GetDispImage(InputImageLinkText, true);

                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);

                GetHomMat2D();

                // ---- 2. 逆变换：拖动ROI后把图像坐标转回原始坐标 ----
                // DisenableAffine2d 为 true 表示用户在窗口中拖动了ROI，需要逆变换
                if (DisenableAffine2d && HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    DisenableAffine2d = false;
                    if (RefRoiChangedFlag)
                    {
                        // TempRefRoi 存的是用户在图像坐标下拖动后的位置
                        // 通过逆变换还原为原始坐标 InitRefRoi
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

                // ---- 3. 正变换：原始坐标 → 图像坐标 ----
                UpdateRoiTransforms();

                // ---- 4. 提取高度通道（3D图像通常第1通道为高度） ----
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (1 > channelCount.I)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception($"目标通道 1 超出图像通道数 {channelCount.I}"));
                    return false;
                }
                HOperatorSet.AccessChannel(sourceImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // ---- 5. 生成ROI区域，并与图像边界求交集 ----
                HOperatorSet.GetImageSize(chRealObj, out HTuple width, out HTuple height);
                HOperatorSet.GenRectangle1(
                    out HObject imageRegionObj,
                    new HTuple(0),
                    new HTuple(0),
                    new HTuple(height.I - 1),
                    new HTuple(width.I - 1));
                imageRegion = new HRegion(imageRegionObj);

                // 获取原始ROI区域（可能超出图像范围）
                rawRefRegion = GetRefRoiRegion();
                rawMeasRegion = GetMeasRoiRegion();
                // 裁剪到图像范围内，防止GetGrayval越界
                refRegion = ClipRegionToImage(rawRefRegion, imageRegion);
                measRegion = ClipRegionToImage(rawMeasRegion, imageRegion);

                // 校验ROI有效性
                // LinkBase模式下基准来自外部平面图像，不需要refRegion
                bool needRefRegion = RefRoiSource != eRefRoiSource.LinkBase;
                bool refRegionInvalid = refRegion == null || !refRegion.IsInitialized() || refRegion.Area <= 0;
                bool measRegionInvalid = measRegion == null || !measRegion.IsInitialized() || measRegion.Area <= 0;
                if ((needRefRegion && refRegionInvalid) || measRegionInvalid)
                {
                    Logger.AddLog("检测高度 ROI 超出图像范围或有效区域为空。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // ---- 6. 计算断差图（三种基准模式） ----
                if (RefRoiSource == eRefRoiSource.LinkBase)
                {
                    // 模式A：链接外部基准平面图像
                    // 从另一个模块获取基准平面图像，拟合一次曲面后生成全图基准平面
                    // 断差图 = 原始高度图 - 基准平面图
                    if (string.IsNullOrEmpty(RefBasePlaneLinkText))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    // try-finally 确保即使中途失败，DispImage 也能恢复为原始图像
                    var savedDisp = DispImage;
                    HImage linkedPlaneImg = null;
                    try
                    {
                        GetDispImage(RefBasePlaneLinkText, true);
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
                        // 恢复原始图像的伪彩色显示
                        if (ModuleView != null && ModuleView.mWindowH != null)
                            ModuleView.mWindowH.HobjectToHimage(DispImage);
                    }

                    // 对基准平面图像拟合一次曲面，得到平面方程参数 α, β, γ
                    HOperatorSet.CountChannels(linkedPlaneImg, out HTuple pc);
                    int pCh = 1;
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
                    // 模式B：在基准区域内拟合平面，生成全图基准平面
                    // 适合基准面有一定倾斜角度的场景
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
                    // 模式C：取基准区域的平均高度作为恒定基准值
                    // 适合基准面大致水平的场景，计算更快
                    HOperatorSet.Intensity(refRegion, new HImage(chRealObj), out HTuple refMean, out _);
                    double baseHeight = refMean.D;

                    // 全图断差 = 原始高度 - 基准高度（整体偏移）
                    HOperatorSet.ScaleImage(chRealObj, out diffObj, 1.0, -baseHeight);
                }

                // ---- 7. 提取测量区域的断差值 ----
                HOperatorSet.GetRegionPoints(measRegion, out HTuple measRows, out HTuple measCols);
                HOperatorSet.GetGrayval(diffObj, measRows, measCols, out HTuple diffValues);

                int totalPoints = diffValues.Length;
                if (totalPoints == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // ---- 8. 重采样：每隔ResampleFactor个点取1个，降低计算量 ----
                if (ResampleFactor > 1)
                {
                    var sampledList = new List<double>();
                    for (int i = 0; i < totalPoints; i += ResampleFactor)
                    {
                        sampledList.Add(diffValues[i].D);
                    }
                    diffValues = new HTuple(sampledList.ToArray());
                    totalPoints = diffValues.Length;
                }

                if (totalPoints == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // ---- 9. 排序与筛选 ----
                double[] values = diffValues.ToDArr();
                Array.Sort(values); // 升序排列，values[0]=最小值，values[^1]=最大值

                // 解析筛选参数m和n（支持绝对值或百分比）
                int m = 0, n = 0;
                if (FilterMode != eFilterMode.All)
                {
                    m = GetFilterCount(FilterM, totalPoints, FilterMIsPercent);
                    n = GetFilterCount(FilterN, totalPoints, FilterNIsPercent);
                    // 确保m、n在有效范围内
                    m = Math.Max(0, Math.Min(m, totalPoints));
                    n = Math.Max(0, Math.Min(n, totalPoints));
                }

                List<double> filtered = new List<double>();
                switch (FilterMode)
                {
                    case eFilterMode.All:
                        // 不筛选，保留全部
                        filtered.AddRange(values);
                        break;
                    case eFilterMode.RemoveMaxMTakeN:
                        // 升序数组：剔除最大m个后，取前n大的值
                        for (int i = totalPoints - 1 - m;
                             i >= totalPoints - 1 - m - n + 1 && i >= 0; i--)
                        {
                            filtered.Add(values[i]);
                        }
                        break;
                    case eFilterMode.RemoveMinMTakeN:
                        // 升序数组：剔除最小m个后，取前n小的值
                        for (int i = m; i < m + n && i < totalPoints; i++)
                        {
                            filtered.Add(values[i]);
                        }
                        break;
                    case eFilterMode.RemoveMinMRemoveMaxN:
                        // 剔除最小m个和最大n个，保留中间部分
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

                // ---- 10. 按模式统计最终断差值 ----
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

                // ---- 11. 显示结果 ----
                if (ShowRegion)
                {
                    // 显示基准区域（绿色）—— LinkBase模式不显示refRegion
                    if (!refRegionInvalid)
                    {
                        ShowHRoi(new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "green",
                            new HObject(refRegion)
                        ));
                    }
                    // 显示测量区域（蓝色）
                    ShowHRoi(new HRoi(
                        ModuleParam.ModuleEncode,
                        ModuleParam.ModuleName,
                        ModuleParam.Remarks,
                        HRoiType.检测结果,
                        "blue",
                        new HObject(measRegion)
                    ));

                    // 基准区域文字标注
                    if (!refRegionInvalid)
                    {
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
                    }

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

                // 断差结果文字标注（显示在测量区域上方）
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
                // 释放所有 Halcon 对象，防止 GDI 资源泄漏
                refRegion?.Dispose();
                measRegion?.Dispose();
                // LinkRegion模式下 rawRefRegion 是外部链接的，不应释放
                if (RefRoiSource != eRefRoiSource.LinkRegion)
                    rawRefRegion?.Dispose();
                if (MeasRoiSource != eMeasRoiSource.LinkRegion)
                    rawMeasRegion?.Dispose();
                imageRegion?.Dispose();
                sourceImage?.Dispose();
                chObj?.Dispose();
                chRealObj?.Dispose();
                if (refPlaneObj != null) refPlaneObj.Dispose();
                if (diffObj != null) diffObj.Dispose();
            }
        }

        /// <summary>
        /// 注册输出参数：供后续模块链接使用。
        /// </summary>
        public override void AddOutputParams()
        {
            AddOutputParam("断差", "double", HeightDifference);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }


        // ============================================================
        // ROI 辅助方法
        // ============================================================

        #region ROI helpers

        /// <summary>
        /// 将ROI区域裁剪到图像边界内（求交集）。
        /// 用于防止 GetGrayval 时坐标越界导致程序崩溃。
        /// </summary>
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

        /// <summary>
        /// 获取基准ROI对应的 Halcon Region。
        ///
        /// 返回值优先级：
        /// 1. LinkRegion模式 → 返回链接的外部区域
        /// 2. 手动绘制有有效尺寸 → 根据 TranRefRoi 生成矩形区域
        /// 3. 兜底 → 返回整幅图像范围作为默认区域
        /// </summary>
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
            // 兜底：无有效ROI时返回整图区域
            if (DispImage == null || !DispImage.IsInitialized()) return null;
            return DispImage.GetDomain();
        }

        /// <summary>
        /// 获取测量ROI对应的 Halcon Region。逻辑同上。
        /// </summary>
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
            if (DispImage == null || !DispImage.IsInitialized()) return null;
            return DispImage.GetDomain();
        }

        /// <summary>
        /// 更新ROI坐标变换：将原始坐标（InitXxxRoi）通过仿射矩阵映射为图像坐标（TranXxxRoi）。
        ///
        /// 三种ROI状态的含义：
        /// - InitRefRoi / InitMeasRoi: 用户输入的原始坐标（不变）
        /// - TranRefRoi / TranMeasRoi: 仿射变换后的图像坐标（实际使用的坐标）
        /// - TempRefRoi / TempMeasRoi: 用户在窗口中拖动后的临时坐标
        ///
        /// 有仿射矩阵时：从链接值读取InitXxxRoi → 正变换 → TranXxxRoi
        /// 无仿射矩阵时：直接复制 InitXxxRoi 到 TranXxxRoi 和 TempXxxRoi
        /// </summary>
        private void UpdateRoiTransforms()
        {
            // ---- 基准 ROI 变换 ----
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                // 有仿射矩阵：从链接值获取原始坐标，再正向变换
                InitRefRoi.MidC = ResolveLinkValue(RefRoiCenterX);
                InitRefRoi.MidR = ResolveLinkValue(RefRoiCenterY);
                InitRefRoi.Length1 = ResolveLinkValue(RefRoiLength1);
                InitRefRoi.Length2 = ResolveLinkValue(RefRoiLength2);
                InitRefRoi.Deg = ResolveLinkValue(RefRoiAngel);
                Aff.Affine2d(HomMat2D, InitRefRoi, TranRefRoi);
            }
            else
            {
                // 无仿射矩阵：直接使用文本值
                // Text.StartsWith("&") 表示该值来自链接，否则为手动输入
                if (!RefRoiCenterX.Text.StartsWith("&"))
                    InitRefRoi.MidC = TranRefRoi.MidC = TempRefRoi.MidC;
                else
                    InitRefRoi.MidC = TranRefRoi.MidC = TempRefRoi.MidC = ResolveLinkValue(RefRoiCenterX);

                if (!RefRoiCenterY.Text.StartsWith("&"))
                    InitRefRoi.MidR = TranRefRoi.MidR = TempRefRoi.MidR;
                else
                    InitRefRoi.MidR = TranRefRoi.MidR = TempRefRoi.MidR = ResolveLinkValue(RefRoiCenterY);

                if (!RefRoiLength1.Text.StartsWith("&"))
                    InitRefRoi.Length1 = TranRefRoi.Length1 = TempRefRoi.Length1;
                else
                    InitRefRoi.Length1 = TranRefRoi.Length1 = TempRefRoi.Length1 = ResolveLinkValue(RefRoiLength1);

                if (!RefRoiLength2.Text.StartsWith("&"))
                    InitRefRoi.Length2 = TranRefRoi.Length2 = TempRefRoi.Length2;
                else
                    InitRefRoi.Length2 = TranRefRoi.Length2 = TempRefRoi.Length2 = ResolveLinkValue(RefRoiLength2);

                if (!RefRoiAngel.Text.StartsWith("&"))
                    InitRefRoi.Deg = TranRefRoi.Deg = TempRefRoi.Deg;
                else
                    InitRefRoi.Deg = TranRefRoi.Deg = TempRefRoi.Deg = ResolveLinkValue(RefRoiAngel);
            }

            // ---- 测量 ROI 变换（逻辑同上） ----
            if (HomMat2D != null && HomMat2D.Length > 0)
            {
                InitMeasRoi.MidC = ResolveLinkValue(MeasRoiCenterX);
                InitMeasRoi.MidR = ResolveLinkValue(MeasRoiCenterY);
                InitMeasRoi.Length1 = ResolveLinkValue(MeasRoiLength1);
                InitMeasRoi.Length2 = ResolveLinkValue(MeasRoiLength2);
                InitMeasRoi.Deg = ResolveLinkValue(MeasRoiAngel);
                Aff.Affine2d(HomMat2D, InitMeasRoi, TranMeasRoi);
            }
            else
            {
                if (!MeasRoiCenterX.Text.StartsWith("&"))
                    InitMeasRoi.MidC = TranMeasRoi.MidC = TempMeasRoi.MidC;
                else
                    InitMeasRoi.MidC = TranMeasRoi.MidC = TempMeasRoi.MidC = ResolveLinkValue(MeasRoiCenterX);

                if (!MeasRoiCenterY.Text.StartsWith("&"))
                    InitMeasRoi.MidR = TranMeasRoi.MidR = TempMeasRoi.MidR;
                else
                    InitMeasRoi.MidR = TranMeasRoi.MidR = TempMeasRoi.MidR = ResolveLinkValue(MeasRoiCenterY);

                if (!MeasRoiLength1.Text.StartsWith("&"))
                    InitMeasRoi.Length1 = TranMeasRoi.Length1 = TempMeasRoi.Length1;
                else
                    InitMeasRoi.Length1 = TranMeasRoi.Length1 = TempMeasRoi.Length1 = ResolveLinkValue(MeasRoiLength1);

                if (!MeasRoiLength2.Text.StartsWith("&"))
                    InitMeasRoi.Length2 = TranMeasRoi.Length2 = TempMeasRoi.Length2;
                else
                    InitMeasRoi.Length2 = TranMeasRoi.Length2 = TempMeasRoi.Length2 = ResolveLinkValue(MeasRoiLength2);

                if (!MeasRoiAngel.Text.StartsWith("&"))
                    InitMeasRoi.Deg = TranMeasRoi.Deg = TempMeasRoi.Deg;
                else
                    InitMeasRoi.Deg = TranMeasRoi.Deg = TempMeasRoi.Deg = ResolveLinkValue(MeasRoiAngel);
            }
        }

        /// <summary>
        /// 初始化/刷新基准ROI在 Halcon 窗口中的可视化矩形。
        ///
        /// 三种路径：
        /// 1. LinkRegion模式 → 直接显示链接的区域，不创建交互ROI
        /// 2. 首次创建（_RoiList中不存在）→ 根据图像尺寸生成默认位置的ROI
        /// 3. 已存在 → 根据当前变换后的坐标更新ROI位置
        ///    - 有逆变换矩阵时：用TranXxxRoi（图像坐标）创建，再逆变换回InitXxxRoi
        ///    - 无逆变换时：直接用InitXxxRoi创建
        /// </summary>
        public void InitRefRoiMethod()
        {
            var view = ModuleView as HeightMeasurementView;
            if (view == null) return;

            // 链接区域模式：直接显示链接的Region，不创建交互ROI
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
                // FlagLineStyle 不为 null 表示这是从序列化恢复的ROI，直接使用 TranRefRoi
                view.mWindowH.WindowH.genRect2(roiName, TranRefRoi.MidR, TranRefRoi.MidC,
                    TranRefRoi.Phi, TranRefRoi.Length1, TranRefRoi.Length2, ref _RoiList);
            }
            else if (DispImage != null && !_RoiList.ContainsKey(roiName))
            {
                // 首次创建：在图像左半区域生成默认位置的ROI
                DispImage.GetImageSize(out int w, out int h);
                view.mWindowH.WindowH.genRect2(roiName, h / 2.0, w / 4.0,
                    0, w / 8.0, h / 8.0, ref _RoiList);
                TranRefRoi.MidC = w / 4.0;
                TranRefRoi.MidR = h / 2.0;
                TranRefRoi.Length1 = w / 8.0;
                TranRefRoi.Length2 = h / 8.0;
                TranRefRoi.Deg = 0;

                // 同步到 UI 文本框
                RefRoiCenterX.Text = TranRefRoi.MidC.ToString();
                RefRoiCenterY.Text = TranRefRoi.MidR.ToString();
                RefRoiLength1.Text = TranRefRoi.Length1.ToString();
                RefRoiLength2.Text = TranRefRoi.Length2.ToString();
                RefRoiAngel.Text = TranRefRoi.Deg.ToString();
            }
            else if (DispImage != null && _RoiList.ContainsKey(roiName))
            {
                // ROI已存在：根据当前状态更新
                if (HomMat2D_Inverse != null && HomMat2D_Inverse.Length > 0)
                {
                    // 用图像坐标创建ROI，再逆变换还原原始坐标
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

        /// <summary>
        /// 初始化/刷新测量ROI在 Halcon 窗口中的可视化矩形。逻辑与 InitRefRoiMethod 对称。
        /// 区别：默认位置在图像右半区域（w * 3/4），颜色为蓝色。
        /// </summary>
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
                // 首次创建：在图像右半区域生成默认位置的ROI
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

        /// <summary>
        /// 安全地从链接变量中解析 double 值。
        /// 比 Convert.ToDouble 更安全：null 返回0，非数字格式记录错误日志后返回0。
        /// </summary>
        private double ResolveLinkValue(LinkVarModel linkVar)
        {
            var val = GetLinkValue(linkVar);
            if (val == null) return 0;
            if (double.TryParse(val.ToString(), out double result)) return result;
            Logger.AddLog($"链接值 '{val}' 无法解析为 double", eMsgType.Error);
            return 0;
        }

        /// <summary>
        /// 根据链接参数计算筛选个数（支持绝对值或百分比）。
        /// </summary>
        /// <param name="linkVar">链接变量</param>
        /// <param name="total">总点数</param>
        /// <param name="isPercent">true表示按百分比计算，false表示绝对值</param>
        private int GetFilterCount(LinkVarModel linkVar, int total, bool isPercent)
        {
            double val = 0;
            double.TryParse(GetLinkValue(linkVar)?.ToString(), out val);
            if (isPercent)
                return (int)Math.Round(total * val / 100.0);
            return (int)val;
        }
        #endregion

        // ============================================================
        // 模块属性
        // ============================================================

        #region Prop

        // ---- 图像链接 ----
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        // ---- 基准ROI链接 ----
        private string _RefRoiLinkText;
        public string RefRoiLinkText
        {
            get { return _RefRoiLinkText; }
            set { Set(ref _RefRoiLinkText, value); }
        }

        // ---- 测量ROI链接 ----
        private string _MeasRoiLinkText;
        public string MeasRoiLinkText
        {
            get { return _MeasRoiLinkText; }
            set { Set(ref _MeasRoiLinkText, value); }
        }

        // ---- 基准平面图像链接（LinkBase模式专用） ----
        private string _RefBasePlaneLinkText;
        public string RefBasePlaneLinkText
        {
            get { return _RefBasePlaneLinkText; }
            set { Set(ref _RefBasePlaneLinkText, value); }
        }

        // ---- 区域来源模式 ----
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

        // ---- 基准 ROI 参数（LinkVarModel 支持手动输入和变量链接双模式） ----
        public LinkVarModel RefRoiCenterX { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel RefRoiCenterY { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel RefRoiLength1 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel RefRoiLength2 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel RefRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // ---- 测量 ROI 参数 ----
        public LinkVarModel MeasRoiCenterX { get; set; } = new LinkVarModel() { Text = "300" };
        public LinkVarModel MeasRoiCenterY { get; set; } = new LinkVarModel() { Text = "100" };
        public LinkVarModel MeasRoiLength1 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel MeasRoiLength2 { get; set; } = new LinkVarModel() { Text = "50" };
        public LinkVarModel MeasRoiAngel { get; set; } = new LinkVarModel() { Text = "0" };

        // ---- ROI 几何对象（三种状态，详见 UpdateRoiTransforms 注释） ----
        public ROIRectangle2 InitRefRoi = new ROIRectangle2();   // 原始坐标
        public ROIRectangle2 TranRefRoi = new ROIRectangle2();   // 变换后的图像坐标
        public ROIRectangle2 TempRefRoi = new ROIRectangle2();   // 用户拖动后的临时坐标
        public ROIRectangle2 InitMeasRoi = new ROIRectangle2();
        public ROIRectangle2 TranMeasRoi = new ROIRectangle2();
        public ROIRectangle2 TempMeasRoi = new ROIRectangle2();

        // ---- 内部状态标记（不序列化） ----
        [NonSerialized] bool DisenableAffine2d = false;   // 是否需要执行逆变换
        [NonSerialized] bool RefRoiChangedFlag = false;   // 基准ROI被用户拖动标记
        [NonSerialized] bool MeasRoiChangedFlag = false;  // 测量ROI被用户拖动标记

        // ---- Halcon 窗口交互 ROI 容器（不序列化） ----
        [NonSerialized]
        private Dictionary<string, ROI> _RoiList;

        // ---- 测量统计模式 ----
        private eMeasureMode _MeasureMode = eMeasureMode.Average;
        public eMeasureMode MeasureMode
        {
            get { return _MeasureMode; }
            set { Set(ref _MeasureMode, value); }
        }

        // ---- 筛选模式 ----
        private eFilterMode _FilterMode = eFilterMode.All;
        public eFilterMode FilterMode
        {
            get { return _FilterMode; }
            set { Set(ref _FilterMode, value); }
        }

        // ---- 是否拟合基准平面 ----
        private bool _FitPlaneForRef = false;
        public bool FitPlaneForRef
        {
            get { return _FitPlaneForRef; }
            set { Set(ref _FitPlaneForRef, value); }
        }

        // ---- 筛选参数 m 和 n ----
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

        // ---- 是否显示检测区域 ----
        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        // ---- 重采样系数（>=1，间隔取点） ----
        private int _ResampleFactor = 1;
        public int ResampleFactor
        {
            get { return _ResampleFactor; }
            set { Set(ref _ResampleFactor, Math.Max(1, value)); }
        }

        // ---- 最终输出：断差值（默认 -9999999 表示未执行） ----
        private double _HeightDifference = -9999999.0;
        public double HeightDifference
        {
            get { return _HeightDifference; }
            set { Set(ref _HeightDifference, value); }
        }
        #endregion


        // ============================================================
        // WPF 命令与事件
        // ============================================================

        #region Command

        /// <summary>
        /// 模块加载时初始化 Halcon 窗口控件，并绑定 ROI 参数变化事件。
        /// </summary>
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
            // 订阅鼠标释放事件：用于检测用户拖动ROI完成
            view.mWindowH.hControl.MouseUp += HControl_MouseUp;

            // 绑定基准ROI参数变化 → 自动重新执行
            RefRoiCenterX.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiCenterY.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiLength1.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiLength2.TextChanged = new Action(() => { RefRoiChanged(); });
            RefRoiAngel.TextChanged = new Action(() => { RefRoiChanged(); });

            // 绑定测量ROI参数变化 → 自动重新执行
            MeasRoiCenterX.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiCenterY.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiLength1.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiLength2.TextChanged = new Action(() => { MeasRoiChanged(); });
            MeasRoiAngel.TextChanged = new Action(() => { MeasRoiChanged(); });

            // 加载图像并初始化ROI
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

        /// <summary>
        /// 执行按钮命令。
        /// </summary>
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

        /// <summary>
        /// 确定按钮命令：关闭窗口前取消鼠标事件订阅。
        /// </summary>
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

        /// <summary>
        /// 基准ROI参数变化回调：
        /// 从链接值重新读取 InitRefRoi，设置逆变换标记，重新执行并刷新窗口ROI。
        /// </summary>
        private void RefRoiChanged()
        {
            // 防止拖动ROI触发的逆变换过程中重复进入
            if (RefRoiChangedFlag) return;
            InitRefRoi.MidC = ResolveLinkValue(RefRoiCenterX);
            InitRefRoi.MidR = ResolveLinkValue(RefRoiCenterY);
            InitRefRoi.Length1 = ResolveLinkValue(RefRoiLength1);
            InitRefRoi.Length2 = ResolveLinkValue(RefRoiLength2);
            InitRefRoi.Deg = ResolveLinkValue(RefRoiAngel);
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

        /// <summary>
        /// 测量ROI参数变化回调（逻辑与 RefRoiChanged 对称）。
        /// </summary>
        private void MeasRoiChanged()
        {
            if (MeasRoiChangedFlag) return;
            InitMeasRoi.MidC = ResolveLinkValue(MeasRoiCenterX);
            InitMeasRoi.MidR = ResolveLinkValue(MeasRoiCenterY);
            InitMeasRoi.Length1 = ResolveLinkValue(MeasRoiLength1);
            InitMeasRoi.Length2 = ResolveLinkValue(MeasRoiLength2);
            InitMeasRoi.Deg = ResolveLinkValue(MeasRoiAngel);
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

        /// <summary>
        /// Halcon 窗口鼠标释放事件：检测用户是否拖动了基准/测量ROI。
        ///
        /// 流程：
        /// 1. 获取当前被拖动的ROI
        /// 2. 将ROI坐标写入 TempXxxRoi（图像坐标）
        /// 3. 设置 ChangedFlag = true（标记为用户拖动）
        /// 4. 重新执行 ExeModule（会走逆变换路径）
        /// 5. 刷新窗口ROI位置
        /// </summary>
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as HeightMeasurementView;
            if (view == null || view.mWindowH == null) return;

            // 获取当前激活的最小ROI（用户刚拖动的那个）
            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            string refRoiName = ModuleParam.ModuleName + "RefROI";
            string measRoiName = ModuleParam.ModuleName + "MeasROI";

            if (roi is ROIRectangle2 rect2)
            {
                if (index == refRoiName)
                {
                    // 记录拖动后的图像坐标到 TempRefRoi
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

        /// <summary>
        /// 变量链接变更回调：当外部模块的输出变量值变化时，更新对应的参数Text。
        ///
        /// 通过 SendName 中的命令枚举名判断哪个参数发生了变化。
        /// 例如 SendName = "{ModuleGuid},RefRoiCenterX" → 更新 RefRoiCenterX.Text
        /// </summary>
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            // SendName 格式: "{ModuleGuid},{LinkCommand名}"
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1]);
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

        /// <summary>
        /// 链接按钮命令：打开变量链接面板，允许用户选择要链接的模块变量。
        ///
        /// 每个 case 先调用 CommonMethods.GetModuleList 过滤出对应类型的模块列表，
        /// 然后发布 OpenVarLinkViewEvent 事件打开链接选择窗口。
        /// </summary>
        [NonSerialized]
        private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    // 订阅变量变更事件，过滤只接收本模块的通知
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

        // ============================================================
        // 序列化：保存/加载模块配置
        // ============================================================

        #region 序列化

        /// <summary>
        /// 序列化所有配置参数为 JSON 字符串。
        /// 先调用基类获取基础参数，再追加本模块特有参数。
        /// </summary>
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

        /// <summary>
        /// 从 JSON 字符串反序列化还原所有配置参数。
        /// 异常时记录错误日志，不中断程序。
        /// </summary>
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
