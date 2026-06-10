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
    /// <summary>变量链接命令</summary>
    public enum eLinkCommand
    {
        InputImageLink,
    }

    /// <summary>
    /// 上采样插值方式（降采样→上采样流程中的上采样阶段）
    /// 降采样阶段固定使用 "constant"（最近邻），避免无效值扩散
    /// </summary>
    public enum eInterpolationMethod
    {
        [EnumDescription("线性邻插值")] Bilinear,
        [EnumDescription("最近邻插值")] NearestNeighbor,
        [EnumDescription("cubic B插值")] Bicubic,
        [EnumDescription("Catmull-Rom样条插值")] CatmullRom,
        [EnumDescription("natural cubic插值")] NaturalCubic,
    }

    /// <summary>缺陷检测类型：凹缺陷、凸缺陷、或两者都检</summary>
    public enum eDefectType
    {
        [EnumDescription("凹")] Concave,
        [EnumDescription("凸")] Convex,
        [EnumDescription("凹&凸")] Both,
    }

    /// <summary>
    /// 主窗口显示模式
    /// Original = 原始深度图
    /// Fitted = 拟合曲面伪彩色
    /// Difference = 残差图（原始-拟合）
    /// Detection = 残差图 + 缺陷区域标记
    /// </summary>
    public enum eDisplayMode
    {
        [EnumDescription("原始图像")] Original,
        [EnumDescription("拟合结果")] Fitted,
        [EnumDescription("差值结果")] Difference,
        [EnumDescription("检测结果")] Detection,
    }
    #endregion

    /// <summary>
    /// 顶盖焊检测插件 — 基于3D高度图检测焊接表面的凹/凸缺陷
    ///
    /// 算法原理：
    /// 1. 提取高度通道，按连通域分割成独立工件区域
    /// 2. 对每个工件区域做降采样→上采样（图像缩放），利用缩放过程中的插值平滑去除局部缺陷信号，得到光滑参考面
    /// 3. 原始高度图 - 光滑参考面 = 残差图（缺陷信号被凸显）
    /// 4. 在残差图上按凹/凸阈值分割出缺陷区域
    /// 5. 统计缺陷数量、面积、最大凹陷深度、最大凸起高度
    ///
    /// 注意：参考面构建方式为降采样+上采样（空间低通滤波），非多项式曲面拟合或高斯拟合
    /// 参考：顶盖（lid）焊接质量检测，如圆柱电池封口焊接
    /// </summary>
    [Category("3D")]
    [DisplayName("顶盖焊检测")]
    [ModuleImageName("LidWeldDetection")]
    [Serializable]
    public class LidWeldDetectionViewModel : ModuleBase
    {
        /// <summary>
        /// 运行时图像/区域缓存，用于切换显示模式时复用，避免重复计算
        /// NonSerialized：不参与方案保存，每次打开窗口需重新执行
        /// </summary>
        #region Runtime image caches
        [NonSerialized] private HImage _originalImage;          // 原始深度图缓存
        [NonSerialized] private HImage _fittedImage;            // 光滑参考面缓存（降采样→上采样结果）
        [NonSerialized] private HImage _diffImage;              // 残差图缓存（原始-拟合）
        [NonSerialized] private HRegion _defectRegion;          // 合并后的缺陷区域
        [NonSerialized] private HRegion _concaveRegion;         // 凹缺陷区域
        [NonSerialized] private HRegion _convexRegion;          // 凸缺陷区域
        [NonSerialized] private HRegion _workpieceContourRegion;// 工件轮廓线（黄色高亮）
        [NonSerialized] private HRegion _inspectionRegion;      // 实际检测区域（轮廓偏移后与有效区域取交集）
        #endregion

        /// <summary>自动链接到上游最后一个 HImage 类型的输出</summary>
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
            // 局部变量声明，finally 中统一释放
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
                // ===== 步骤0：清理旧缓存，重置输出变量 =====
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

                // ===== 步骤1：获取输入图像并缓存原始图（用于显示模式切换）=====
                GetDispImage(InputImageLinkText, true);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                sourceImage = new HImage(DispImage);
                _originalImage = new HImage(DispImage);

                // ===== 步骤2：提取高度通道（Ch1）并转为 real 浮点类型 =====
                // 3D 深度图可能为多通道（Ch1=高度, Ch2=亮度），统一取第1通道
                HOperatorSet.CountChannels(sourceImage, out HTuple channelCount);
                if (channelCount.I < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("图像无有效通道"));
                    return false;
                }
                HOperatorSet.AccessChannel(sourceImage, out chObj, 1);
                HOperatorSet.ConvertImageType(chObj, out chRealObj, "real");

                // ===== 步骤3：Z像素单位换算 =====
                // 原始高度值 × ResolutionZ → 实际物理高度
                // 例如原始值单位为 μm、希望以 mm 检测时，ResolutionZ = 0.001
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

                // ===== 步骤4：过滤无效数据 =====
                // Keyence 等传感器的高度图常用负哨兵值（如 -21474836）表示无效点
                // 通过 MinValidHeight/MaxValidHeight 参数设定有效高度范围
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

                // ===== 步骤5：构建检测区域（轮廓偏移后取交集）=====
                // 有效区域向内/外偏移 ContourOffset，排除边缘不可靠数据
                // 再与原始有效区域取交集，确保检测区域不超出有效数据范围
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

                // ===== 步骤6：构建工件轮廓线（仅用于显示，黄色边界）=====
                workpieceContourRegion = BuildWorkpieceContourRegion(offsetWorkpieceRegion);
                _workpieceContourRegion = workpieceContourRegion != null && workpieceContourRegion.IsInitialized() && workpieceContourRegion.Area > 0
                    ? new HRegion(workpieceContourRegion)
                    : null;

                // ===== 步骤7：按连通域分别构建光滑参考面 =====
                // 对有效区域中的每个独立连通域，先降采样(去噪)→再上采样(回原尺寸)，通过图像缩放插值构造光滑参考面
                // 降采样用 "constant" 避免无效值扩散（最近邻），上采样用用户选择的插值方式
                string componentInterpDown = "constant";
                string componentInterpUp = GetHalconInterpolationMode();
                fittedImage = BuildComponentFittedImage(realImage, validRegion, srcW, srcH, componentInterpDown, componentInterpUp);
                if (fittedImage == null || !fittedImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    Logger.GetExceptionMsg(new Exception("Failed to build fitted surface from valid regions"));
                    return false;
                }
                _fittedImage = new HImage(fittedImage);

                // ===== 步骤8：计算残差图 =====
                // 残差 = 原始高度 - 光滑参考面
                // 正值 → 实际高于参考面（凸起），负值 → 实际低于参考面（凹陷）
                HOperatorSet.ReduceDomain(realImage, validRegion, out HObject validRealObj);
                HImage validRealImage = new HImage(validRealObj);
                HOperatorSet.ReduceDomain(fittedImage, validRegion, out HObject validFittedObj);
                HImage validFittedImage = new HImage(validFittedObj);
                HOperatorSet.SubImage(validRealImage, validFittedImage, out diffObj, 1.0, 0.0);
                validRealImage.Dispose();
                validFittedImage.Dispose();
                diffImage = new HImage(diffObj);
                _diffImage = new HImage(diffObj);

                // ===== 步骤9：缺陷检测（凹/凸/双检）=====
                // 在残差图上按阈值分割，凹缺陷取负值区域，凸缺陷取正值区域
                // 检测区域限定在 inspectionRegion（排除工件边缘干扰）
                bool hasConcave = false;
                bool hasConvex = false;

                if (DefectType == eDefectType.Concave || DefectType == eDefectType.Both)
                {
                    // 凹缺陷检测：残差 < -ConcaveThreshold（高度低于参考面的区域）
                    // 差值越小凹陷越深，min 设为极负值确保不漏检，实际由阈值控制深度
                    HOperatorSet.Threshold(diffImage, out HObject concaveObj, -999999.0, -ConcaveThreshold);
                    // 取与检测区域的交集，排除工件边缘及轮廓偏移区域
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
                    // 凸缺陷检测：残差 > ConvexThreshold（高度高于参考面的区域）
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

                // ===== 步骤10：合并缺陷区域 =====
                // 支持单选凹、单选凸、或凹+凸合并
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

                // 保存到运行时缓存（用于后续显示模式切换和 ROI 叠加）
                _defectRegion = defectRegion != null && defectRegion.IsInitialized() ? new HRegion(defectRegion) : null;
                _concaveRegion = concaveRegion != null && concaveRegion.IsInitialized() ? new HRegion(concaveRegion) : null;
                _convexRegion = convexRegion != null && convexRegion.IsInitialized() ? new HRegion(convexRegion) : null;

                // ===== 步骤11：统计缺陷信息 =====
                // 缺陷数量（连通域个数）、总面积、最大凹陷深度/凸起高度
                CalculateDefectStats(diffImage, concaveRegion, convexRegion);
                HasDefect = DefectCount > 0;

                // ===== 步骤12：根据显示模式更新主窗口图像 =====
                UpdateDisplayImage();

                // ===== 步骤13：叠加显示工件轮廓（黄色边界线）=====
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

                // ===== 步骤14：叠加显示缺陷区域（凹=蓝色，凸=红色）=====
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

        /// <summary>
        /// 对有效区域做轮廓偏移
        /// 内缩(ContourOffset &lt; 0)：ErosionCircle 腐蚀，缩小检测区域，排除边缘毛刺
        /// 外扩(ContourOffset &gt; 0)：DilationCircle 膨胀，扩大检测区域
        /// 偏移结果为 0：直接复制原始区域
        /// 内部先 FillUp 填充孔洞，避免空心区域影响形态学操作
        /// </summary>
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

        /// <summary>
        /// 生成工件轮廓线（有效区域边界），取 inner 边界
        /// 先 FillUp 填充内部孔洞，避免内部无效点被当作工件外轮廓显示
        /// 用于主窗口黄色高亮叠加显示
        /// </summary>
        private HRegion BuildWorkpieceContourRegion(HRegion validRegion)
        {
            if (validRegion == null || !validRegion.IsInitialized() || validRegion.Area <= 0)
                return new HRegion();

            HObject filledObj = null;
            HObject boundaryObj = null;
            try
            {
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

        /// <summary>
        /// 统计缺陷信息
        /// 对凹/凸区域分别执行：
        /// 1. Connection 连通域分割 + CountObj 计数 → 缺陷数量
        /// 2. AreaCenter 求和面积 → 总缺陷面积
        /// 3. MinMaxGray 取残差极值 → 凹取 min(最大凹陷深度)、凸取 max(最大凸起高度)
        /// </summary>
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

        /// <summary>
        /// 按连通域构建光滑参考面（降采样→上采样，空间低通滤波）
        ///
        /// 原理：对每个独立工件区域，通过图像缩放过程中插值平滑去除局部缺陷信号
        /// 1. 取连通域的外接矩形，裁剪出局部图像
        /// 2. 矩形内但不在连通域内的无效像素，用该连通域的平均高度填充
        /// 3. 按 SampleRateX/Y 降采样 → 缩小图像，局部高频信息（缺陷、噪声）被平滑掉
        /// 4. 再上采样回原始尺寸 → 插值得到光滑参考面
        /// 5. 仅提取连通域内像素的值，写入画布（SetGrayval）
        /// 6. 最终用 ChangeDomain 限制画布 domain 到所有连通域的并集
        ///
        /// 注意：这不是多项式/高斯曲面拟合，本质是图像缩放插值的低通滤波效果
        /// interpDown: 降采样插值方式（固定 "constant" 最近邻，避免无效值扩散）
        /// interpUp: 上采样插值方式（用户可选 Bilinear/Bicubic 等）
        /// </summary>
        private HImage BuildComponentFittedImage(
            HImage realImage,
            HRegion validRegion,
            int srcW,
            int srcH,
            string interpDown,
            string interpUp)
        {
            // 拟合画布：与原图等大的全零 real 图像
            HObject fittedCanvasObj = null;
            // 有效区域按连通域分割后的各个独立区域
            HObject connectedObj = null;
            // 所有已拟合连通域的并集（最终用作画布 domain）
            HRegion fittedRegion = null;

            try
            {
                HOperatorSet.GenImageConst(out fittedCanvasObj, "real", srcW, srcH);
                HOperatorSet.Connection(validRegion, out connectedObj);
                HOperatorSet.CountObj(connectedObj, out HTuple componentCount);

                // 逐个连通域拟合
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
                        // 选出第 i 个连通域
                        HOperatorSet.SelectObj(connectedObj, out componentObj, i);
                        componentRegion = new HRegion(componentObj);

                        if (!componentRegion.IsInitialized() || componentRegion.Area <= 0)
                            continue;

                        // --- 获取外接矩形，裁剪出局部图像 ---
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

                        // 裁剪：ReduceDomain 限制到矩形区域 + CropPart 裁出独立图像
                        HOperatorSet.ReduceDomain(realImage, rectRegion, out localRealObj);
                        HOperatorSet.CropPart(localRealObj, out localCropObj, r1, c1, localW, localH);
                        localCropImage = new HImage(localCropObj);

                        // --- 填充无效像素：矩形内但不在连通域内的区域，填入该连通域的平均高度 ---
                        // 避免无效值（如0或极负值）在降采样时扩散到有效区域
                        HOperatorSet.Intensity(componentRegion, realImage, out HTuple meanHeight, out _);
                        HOperatorSet.Difference(rectRegion, componentRegion, out localInvalidObj);
                        localInvalidRegion = new HRegion(localInvalidObj);
                        if (localInvalidRegion.IsInitialized() && localInvalidRegion.Area > 0)
                        {
                            // MoveRegion 将无效区域平移到局部坐标系（原点 = r1, c1）
                            HOperatorSet.MoveRegion(localInvalidRegion, out movedInvalidObj, -r1, -c1);
                            movedInvalidRegion = new HRegion(movedInvalidObj);
                            // PaintRegion：将无效像素填充为平均高度值
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

                        // --- 降采样 → 上采样：构建光滑拟合曲面 ---
                        // 降采样比例 = 1/SampleRateX, 1/SampleRateY
                        int intervalX = Math.Max(1, SampleRateX);
                        int intervalY = Math.Max(1, SampleRateY);
                        int targetW = Math.Max(2, (int)Math.Ceiling((double)localW / intervalX));
                        int targetH = Math.Max(2, (int)Math.Ceiling((double)localH / intervalY));

                        // 降采样：原尺寸 → 小尺寸，去除局部高频信息（缺陷、噪声）
                        HOperatorSet.ZoomImageSize(localFilledObj, out sampledObj, targetW, targetH, interpDown);
                        // 上采样：小尺寸 → 原尺寸，插值得到光滑曲面
                        HOperatorSet.ZoomImageSize(sampledObj, out fittedLocalObj, localW, localH, interpUp);

                        // --- 仅提取连通域内像素的拟合值，写入画布 ---
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

        /// <summary>
        /// 按面积过滤缺陷区域
        /// 对输入 region 做 Connection 连通域分割，再 SelectShape 按面积筛选
        /// 保留 MinDefectArea ≤ 面积 ≤ MaxDefectArea 的连通域，过滤掉过小噪点和过大连片
        /// </summary>
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

        /// <summary>
        /// 根据当前 DisplayMode 更新主窗口显示的图像
        /// Original → 原始深度图
        /// Fitted → 光滑参考面伪彩色（real → byte 映射）
        /// Difference → 残差图伪彩色
        /// Detection → 残差图伪彩色（缺陷区域由 ShowHRoi 叠加标记）
        /// </summary>
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

        /// <summary>
        /// 将用户选择的 eInterpolationMethod 枚举映射为 Halcon 插值字符串
        /// 当前 CatmullRom 和 NaturalCubic 暂用 "bicubic" 替代，后续可通过 Halcon 自定义插值实现
        /// </summary>
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

        /// <summary>注册模块输出参数，供下游流程通过变量链接获取</summary>
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


        #region Properties - 输入/输出参数，与 Halcon 脚本面板绑定
        /// <summary>输入图像链接文本，格式如 &amp;模块名.变量名</summary>
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        /// <summary>Z 分辨率/像素单位换算系数，原始高度值 × ResolutionZ → 实际物理高度</summary>
        private double _ResolutionZ = 1.0;
        public double ResolutionZ
        {
            get { return _ResolutionZ; }
            set { Set(ref _ResolutionZ, value); }
        }

        /// <summary>有效高度下限，低于此值视为无效点（如传感器哨兵值）</summary>
        private double _MinValidHeight = 0.0;
        public double MinValidHeight
        {
            get { return _MinValidHeight; }
            set { Set(ref _MinValidHeight, value); }
        }

        /// <summary>有效高度上限，高于此值视为无效点</summary>
        private double _MaxValidHeight = 999999.0;
        public double MaxValidHeight
        {
            get { return _MaxValidHeight; }
            set { Set(ref _MaxValidHeight, value); }
        }

        /// <summary>X 方向降采样间隔（列方向），值越大平滑越强，最小为 2</summary>
        private int _SampleRateX = 10;
        public int SampleRateX
        {
            get { return _SampleRateX; }
            set { Set(ref _SampleRateX, Math.Max(2, value)); }
        }

        /// <summary>Y 方向降采样间隔（行方向），值越大平滑越强，最小为 2</summary>
        private int _SampleRateY = 80;
        public int SampleRateY
        {
            get { return _SampleRateY; }
            set { Set(ref _SampleRateY, Math.Max(2, value)); }
        }

        /// <summary>上采样插值方式，影响拟合曲面的光滑度</summary>
        private eInterpolationMethod _InterpolationMethod = eInterpolationMethod.Bilinear;
        public eInterpolationMethod InterpolationMethod
        {
            get { return _InterpolationMethod; }
            set { Set(ref _InterpolationMethod, value); }
        }

        /// <summary>缺陷检测类型：仅凹、仅凸、或凹凸都检</summary>
        private eDefectType _DefectType = eDefectType.Concave;
        public eDefectType DefectType
        {
            get { return _DefectType; }
            set { Set(ref _DefectType, value); }
        }

        /// <summary>凹缺陷深度阈值，残差 &lt; -阈值 的区域判定为凹陷</summary>
        private double _ConcaveThreshold = 0.20;
        public double ConcaveThreshold
        {
            get { return _ConcaveThreshold; }
            set { Set(ref _ConcaveThreshold, value); }
        }

        /// <summary>凸缺陷高度阈值，残差 &gt; 阈值 的区域判定为凸起</summary>
        private double _ConvexThreshold = 0.20;
        public double ConvexThreshold
        {
            get { return _ConvexThreshold; }
            set { Set(ref _ConvexThreshold, value); }
        }

        /// <summary>缺陷最小面积，低于此值的连通域被过滤</summary>
        private double _MinDefectArea = 10.0;
        public double MinDefectArea
        {
            get { return _MinDefectArea; }
            set { Set(ref _MinDefectArea, Math.Max(0, value)); }
        }

        /// <summary>缺陷最大面积，高于此值的连通域被过滤（排除大面积误检）</summary>
        private double _MaxDefectArea = 999999999.0;
        public double MaxDefectArea
        {
            get { return _MaxDefectArea; }
            set { Set(ref _MaxDefectArea, Math.Max(0, value)); }
        }

        /// <summary>是否在主窗口叠加显示缺陷区域（凹=蓝色，凸=红色）</summary>
        private bool _ShowDefectRegion = true;
        public bool ShowDefectRegion
        {
            get { return _ShowDefectRegion; }
            set { Set(ref _ShowDefectRegion, value); }
        }

        /// <summary>是否在主窗口叠加显示工件轮廓线（黄色边界）</summary>
        private bool _ShowWorkpieceContour = true;
        public bool ShowWorkpieceContour
        {
            get { return _ShowWorkpieceContour; }
            set { Set(ref _ShowWorkpieceContour, value); }
        }

        /// <summary>轮廓偏移量，正=外扩，负=内缩，用于排除工件边缘不可靠数据</summary>
        private double _ContourOffset = 0.0;
        public double ContourOffset
        {
            get { return _ContourOffset; }
            set { Set(ref _ContourOffset, value); }
        }

        /// <summary>主窗口显示模式，切换时自动刷新显示图像</summary>
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

        // ---- 输出结果（由 ExeModule 填充，供 AddOutputParams 注册）----
        /// <summary>是否存在缺陷（输出）</summary>
        private bool _HasDefect;
        public bool HasDefect
        {
            get { return _HasDefect; }
            set { Set(ref _HasDefect, value); }
        }

        /// <summary>缺陷数量（输出），各连通域按面积过滤后的计数</summary>
        private int _DefectCount;
        public int DefectCount
        {
            get { return _DefectCount; }
            set { Set(ref _DefectCount, value); }
        }

        /// <summary>缺陷总面积（输出），各连通域面积之和</summary>
        private double _DefectArea;
        public double DefectArea
        {
            get { return _DefectArea; }
            set { Set(ref _DefectArea, value); }
        }

        /// <summary>最大凹陷深度（输出），取自残差图凹区域最小值，取绝对值后为正</summary>
        private double _MaxConcaveDepth;
        public double MaxConcaveDepth
        {
            get { return _MaxConcaveDepth; }
            set { Set(ref _MaxConcaveDepth, value); }
        }

        /// <summary>最大凸起高度（输出），取自残差图凸区域最大值</summary>
        private double _MaxConvexHeight;
        public double MaxConvexHeight
        {
            get { return _MaxConvexHeight; }
            set { Set(ref _MaxConvexHeight, value); }
        }
        #endregion

        #region Commands - UI 交互命令绑定
        /// <summary>模块窗口加载完成回调，初始化 Halcon 图像控件并尝试自动链接输入</summary>
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

        /// <summary>变量链接变更回调，根据 SendName 尾部的命令类型更新对应属性</summary>
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

        /// <summary>链接命令：弹出变量链接窗口，选择输入图像来源</summary>
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

        /// <summary>执行命令：触发 ExeModule 完整检测流程</summary>
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

        /// <summary>确认命令：关闭模块视图窗口</summary>
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

        /// <summary>切换显示模式命令：在原始/拟合/差值/检测四种模式间切换</summary>
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

        #region Serialization - 方案保存/加载
        /// <summary>序列化模块参数到 JSON，保存方案时调用</summary>
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

        /// <summary>从 JSON 反序列化模块参数，加载方案时调用</summary>
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
