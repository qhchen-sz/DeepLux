using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using HV.Common.Enums;
using HV.Common.Provide;

namespace Plugin.Envelope.ViewModels
{
    public static  class Algorithm
    {
        /// <summary>
        /// 寻边算法：基于AI分割标签图，计算熔点到顶盖距离、膜到顶盖距离。
        ///
        /// ============ 输入图像说明 ============
        /// ho_Image 是 AI 语义分割的标签图（已 ScaleImage 放大），不同灰度值代表不同区域：
        ///   灰度  60 → 顶盖（ho_Region1）
        ///   灰度 120 → 蓝膜/隔膜（ho_Region2）
        ///   灰度 180 → 热熔点（ho_Region3）
        ///
        /// ============ 算法整体流程 ============
        /// 第一步：从标签图中提取顶盖(60)、蓝膜(120)、热熔点(180)三个区域
        /// 第二步：从蓝膜区域中筛选出"隔膜"区域（排除与热熔点重叠的部分，保留与顶盖同列的部分）
        /// 第三步：根据位置参数(Left/Right/Center)选择目标隔膜，用卡尺测量顶盖到隔膜的边缘距离
        /// 第四步：以顶盖区域底边为初始位置，用计量模型精确拟合顶盖基准线（ho_Line1）
        /// 第五步：计算热熔点到顶盖线的垂直距离（输出 hv_DistanceRongDian）
        /// 第六步：遍历每个隔膜区域，计算其顶边中点到顶盖线的垂直距离（输出 hv_DistanceTopCover）
        ///          → 这就是"左膜到顶盖距离"/"右膜到顶盖距离"的来源
        /// ====================================
        /// </summary>
        public static void Find_RongDian(HObject ho_Image, out HObject ho_Line1, out HObject ho_Arrow,
            out HObject ho_Cross, HTuple hv_Location, out HTuple hv_DistanceRongDian, out HTuple hv_DistanceMo,
            out HObject ho_TopCoverArrow, out HTuple hv_DistanceTopCover, out HObject ho_RongDianArrow)
        {




            // Stack for temporary objects
            HObject[] OTemp = new HObject[20];

            // Local iconic variables

            // ---- 区域变量说明 ----
            // ho_Region1: 顶盖区域（灰度60），取最大面积的那个
            // ho_Region2: 隔膜区域（灰度120），经过多步筛选后的结果
            // ho_Region3: 热熔点区域（灰度180），取最大面积的那个
            // ho_Line1:  顶盖线 —— 在顶盖区域底边拟合出的水平直线，是测量"膜到顶盖距离"和"熔点到顶盖距离"的基准线
            HObject ho_Region1 = null, ho_Region3 = null, ho_ConnectedRegions = null;
            HObject ho_Region2 = null, ho_RegionDilation = null, ho_RegionDifference = null;
            HObject ho_RegionDilation1 = null, ho_Rectangle = null, ho_Cross1 = null;
            HObject ho_Arrow1 = null, ho_Arrow2 = null, ho_Cross2 = null;
            HObject ho_MeeasureRegion = null, ho_ArrowRongDian = null;

            // Local control variables

            // ---- 关键控制变量说明 ----
            // hv_DistanceRongDian: 熔点到顶盖线的垂直距离（最终输出 ResultValue1）
            // hv_DistanceMo:       顶盖到隔膜边缘的卡尺距离数组
            // hv_DistanceTopCover: 每个隔膜区域顶边中点到顶盖线的垂直距离数组
            //                       → Left模式取[0]=左膜距离，Right模式取[0]=右膜距离
            //                       → Center模式取[0]=左膜距离，取[最后]=右膜距离
            HTuple hv_Value = new HTuple(), hv_Width = new HTuple();
            HTuple hv_Height = new HTuple(), hv_Row = new HTuple();
            HTuple hv_Column = new HTuple(), hv_Row2 = new HTuple();
            HTuple hv_Row3 = new HTuple(), hv_Column2 = new HTuple();
            HTuple hv_Phi1 = new HTuple(), hv_Length11 = new HTuple();
            HTuple hv_Length21 = new HTuple(), hv_MeasureHandle = new HTuple();
            HTuple hv_RowEdge = new HTuple(), hv_ColumnEdge = new HTuple();
            HTuple hv_Amplitude = new HTuple(), hv_DistanceMo1 = new HTuple();
            HTuple hv_DistanceMo2 = new HTuple(), hv_Phi = new HTuple();
            HTuple hv_Length1 = new HTuple(), hv_Length2 = new HTuple();
            HTuple hv_Line1_X1 = new HTuple(), hv_Line1_Y1 = new HTuple();
            HTuple hv_Line1_X2 = new HTuple(), hv_Line1_Y2 = new HTuple();
            HTuple hv_MetrologyHandle = new HTuple(), hv_Index1 = new HTuple();
            HTuple hv_Row1 = new HTuple(), hv_Column1 = new HTuple();
            HTuple hv_RowBegin = new HTuple(), hv_ColBegin = new HTuple();
            HTuple hv_RowEnd = new HTuple(), hv_ColEnd = new HTuple();
            HTuple hv_Nr = new HTuple(), hv_Nc = new HTuple(), hv_Dist = new HTuple();
            HTuple hv_Area = new HTuple(), hv_Row4 = new HTuple();
            HTuple hv_Column3 = new HTuple(), hv_RowOver = new HTuple();
            HTuple hv_ColumnOver = new HTuple(), hv_IsOverlapping = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables
            HOperatorSet.GenEmptyObj(out ho_Line1);
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_Region3);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            HOperatorSet.GenEmptyObj(out ho_RegionDilation);
            HOperatorSet.GenEmptyObj(out ho_RegionDifference);
            HOperatorSet.GenEmptyObj(out ho_RegionDilation1);
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            HOperatorSet.GenEmptyObj(out ho_Arrow1);
            HOperatorSet.GenEmptyObj(out ho_Arrow2);
            HOperatorSet.GenEmptyObj(out ho_Cross2);
            HOperatorSet.GenEmptyObj(out ho_MeeasureRegion);
            HOperatorSet.GenEmptyObj(out ho_ArrowRongDian);
            HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
            HOperatorSet.GenEmptyObj(out ho_RongDianArrow);
            hv_DistanceRongDian = new HTuple();
            hv_DistanceMo = new HTuple();
            hv_DistanceTopCover = new HTuple();
            try
            {
                try
                {
                    // ============================================================
                    // 第一步：初始化输出变量
                    // ============================================================
                    ho_Line1.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Line1);
                    ho_Arrow.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Arrow);
                    ho_Cross.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Cross);
                    hv_DistanceRongDian.Dispose();
                    hv_DistanceRongDian = 0;
                    hv_DistanceMo.Dispose();
                    hv_DistanceMo = new HTuple();
                    hv_DistanceMo[0] = 0;
                    hv_DistanceMo[1] = 0;

                    // ============================================================
                    // 第二步：从标签图中提取三个核心区域
                    // ============================================================

                    // 2.1 提取顶盖区域：灰度=60 的像素 → ho_Region1
                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region1, 60, 60);
                    // 2.2 提取热熔点区域：灰度=180 的像素 → ho_Region3
                    ho_Region3.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region3, 180, 180);
                    // 2.3 顶盖区域可能有多个连通域，只取面积最大的那个
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions);
                    ho_Region1.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region1, "max_area", 70);

                    // ============================================================
                    // 第三步：从蓝膜区域(灰度120)中提取"隔膜"区域 → ho_Region2
                    //
                    // 为什么需要这一步？
                    //   灰度120的像素包含了蓝膜，但我们需要的是"隔膜"——
                    //   即位于顶盖正下方、与热熔点不重叠的那部分膜。
                    //
                    // 处理流程：
                    //   3.1 提取全部灰度120区域
                    //   3.2 将热熔点区域膨胀(100×500)，从灰度120区域中做差集
                    //       → 排除与热熔点重叠的膜部分
                    //   3.3 按高度筛选：只保留高度 > (最大高度/3) 的区域
                    //       → 排除细小的噪点
                    //   3.4 将顶盖区域横向膨胀成竖带(1×图像高度)，与筛选结果求交集
                    //       → 只保留与顶盖同列的膜区域（即"隔膜"）
                    // ============================================================

                    // 3.1 提取灰度120的全部区域
                    ho_Region2.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region2, 120, 120);
                    // 3.2 热熔点区域膨胀100行×500列，从灰度120区域中减去 → 排除热熔点重叠部分
                    ho_RegionDilation.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region3, out ho_RegionDilation, 100, 500);
                    ho_RegionDifference.Dispose();
                    HOperatorSet.Difference(ho_Region2, ho_RegionDilation, out ho_RegionDifference);
                    // 3.3 按高度筛选：计算所有区域的高度，只保留高度 > 最大高度/3 的区域
                    hv_Value.Dispose();
                    HOperatorSet.RegionFeatures(ho_RegionDifference, "height", out hv_Value);
                    ho_Region2.Dispose();
                    HOperatorSet.Connection(ho_RegionDifference, out ho_Region2);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.SelectShape(ho_Region2, out ExpTmpOutVar_0, "height", "and",
                            hv_Value / 3, "max");
                        ho_Region2.Dispose();
                        ho_Region2 = ExpTmpOutVar_0;
                    }
                    // 3.4 将顶盖横向膨胀成一条竖带(宽1×高=图像高度)，与筛选后的120区域求交集
                    //     结果：只有与顶盖位于同一列方向的膜区域被保留 → 这就是"隔膜"
                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    ho_RegionDilation1.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region1, out ho_RegionDilation1, 1, hv_Height);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.Intersection(ho_Region2, ho_RegionDilation1, out ExpTmpOutVar_0);
                        ho_Region2.Dispose();
                        ho_Region2 = ExpTmpOutVar_0;
                    }

                    // ============================================================
                    // 第四步：根据位置参数选择目标隔膜，并用卡尺测量顶盖到隔膜的边缘距离
                    //
                    // 三种模式：
                    //   Left:  选最左侧的隔膜 → 测量1个距离 → hv_DistanceMo[0]=左膜距离
                    //   Right: 选最右侧的隔膜 → 测量1个距离 → hv_DistanceMo[0]=右膜距离
                    //   Center:选左右两个隔膜 → 测量2个距离 → hv_DistanceMo[0]=左膜, [1]=右膜
                    //
                    // 卡尺测量原理：
                    //   以顶盖中心行(hv_Row)为起点、隔膜中心行(hv_Row2)为终点，
                    //   以隔膜列坐标为中心生成一个 Measure 矩形，
                    //   用 MeasurePos 提取该矩形内的边缘点，
                    //   两个边缘点之间的距离即为"顶盖到隔膜的边缘距离"
                    // ============================================================
                    if ((int)(new HTuple(hv_Location.TupleEqual("Left"))) != 0)
                    {
                        // ---- Left模式：按列坐标升序排列，取最左侧的隔膜区域 ----
                        {
                            HObject sortedRegions;
                            HOperatorSet.SortRegion(ho_Region2, out sortedRegions, "first_point",
                                "true", "column");
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectObj(sortedRegions, out ExpTmpOutVar_0, 1);
                            sortedRegions.Dispose();
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        // 获取顶盖的行坐标(hv_Row) 和 隔膜的列坐标(hv_Column)、行坐标(hv_Row2)
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        // 以顶盖中心和隔膜中心为对角，生成一个矩形测量区域（列方向±20像素容差）
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Rectangle.Dispose();
                            HOperatorSet.GenRectangle1(out ho_Rectangle,
                                hv_Row,                                    // 上边 = 顶盖行坐标
                                (hv_Column.TupleSelect(0)) - 20,          // 左边 = 隔膜列-20
                                hv_Row2.TupleSelect(0),                   // 下边 = 隔膜行坐标
                                (hv_Column.TupleSelect(0)) + 20);         // 右边 = 隔膜列+20
                        }
                        // 计算矩形的最小外接旋转矩形 → 得到卡尺测量矩形的位姿参数
                        hv_Row3.Dispose(); hv_Column2.Dispose(); hv_Phi1.Dispose(); hv_Length11.Dispose(); hv_Length21.Dispose();
                        HOperatorSet.SmallestRectangle2(ho_Rectangle, out hv_Row3, out hv_Column2,
                            out hv_Phi1, out hv_Length11, out hv_Length21);
                        // 生成卡尺测量矩形，用于提取边缘
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_MeasureHandle.Dispose();
                            HOperatorSet.GenMeasureRectangle2(hv_Row3, hv_Column2, -hv_Phi1, hv_Length11,
                                hv_Length21, hv_Width, hv_Height, "nearest_neighbor", out hv_MeasureHandle);
                        }
                        // MeasurePos: 在卡尺矩形内找1对边缘点(从亮到暗+从暗到亮)，边缘幅度阈值30
                        // 返回 hv_DistanceMo = 两个边缘点之间的距离（即顶盖到隔膜的边缘距离，单位：像素）
                        hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_DistanceMo.Dispose();
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 1, 15, "all", "all",
                            out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude, out hv_DistanceMo);
                        // 在找到的边缘点位置画十字标记
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Cross.Dispose();
                            HOperatorSet.GenCrossContourXld(out ho_Cross, hv_RowEdge, hv_ColumnEdge,
                                16, (new HTuple(45)).TupleRad());
                        }
                        // 在两个边缘点之间画箭头
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Arrow.Dispose();
                            gen_arrow_contour_xld(out ho_Arrow, hv_RowEdge.TupleSelect(0), hv_ColumnEdge.TupleSelect(
                                0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5, 5);
                        }
                    }
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Right"))) != 0)
                    {
                        // ---- Right模式：按列坐标降序排列，取最右侧的隔膜区域 ----
                        {
                            HObject sortedRegions;
                            HOperatorSet.SortRegion(ho_Region2, out sortedRegions, "first_point",
                                "false", "column");
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectObj(sortedRegions, out ExpTmpOutVar_0, 1);
                            sortedRegions.Dispose();
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        // 以下逻辑与 Left 模式完全相同，只是选的是最右侧的隔膜
                        // 获取顶盖的行坐标(hv_Row) 和 隔膜的行/列坐标
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        // 以顶盖中心和隔膜中心为对角，生成矩形测量区域
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Rectangle.Dispose();
                            HOperatorSet.GenRectangle1(out ho_Rectangle, hv_Row, (hv_Column.TupleSelect(
                                0)) - 20, hv_Row2.TupleSelect(0), (hv_Column.TupleSelect(0)) + 20);
                        }
                        hv_Row3.Dispose(); hv_Column2.Dispose(); hv_Phi1.Dispose(); hv_Length11.Dispose(); hv_Length21.Dispose();
                        HOperatorSet.SmallestRectangle2(ho_Rectangle, out hv_Row3, out hv_Column2,
                            out hv_Phi1, out hv_Length11, out hv_Length21);
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_MeasureHandle.Dispose();
                            HOperatorSet.GenMeasureRectangle2(hv_Row3, hv_Column2, -hv_Phi1, hv_Length11,
                                hv_Length21, hv_Width, hv_Height, "nearest_neighbor", out hv_MeasureHandle);
                        }
                        hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_DistanceMo.Dispose();
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 1, 15, "all", "all",
                            out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude, out hv_DistanceMo);
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Cross.Dispose();
                            HOperatorSet.GenCrossContourXld(out ho_Cross, hv_RowEdge, hv_ColumnEdge,
                                16, (new HTuple(45)).TupleRad());
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Arrow.Dispose();
                            gen_arrow_contour_xld(out ho_Arrow, hv_RowEdge.TupleSelect(0), hv_ColumnEdge.TupleSelect(
                                0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5, 5);
                        }
                    }
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                    {
                        // ---- Center模式：取面积>1500的隔膜区域（通常是左右两个），分别测量顶盖到隔膜的边缘距离 ----
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShape(ho_Region2, out ExpTmpOutVar_0, "area", "and",
                                1500, "max");
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        // 获取顶盖的行坐标(hv_Row) 和 隔膜的列/行坐标
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        // 只有当区域数量>=2时才进行测量（需要左右两个隔膜都存在）
                        if ((int)(new HTuple((new HTuple(hv_Column.TupleLength())).TupleGreaterEqual(
                            2))) != 0)
                        {

                            // ---- 测量第一个隔膜（通常是左侧） ----
                            // 同时对两个区域生成矩形：[顶盖行, 隔膜1列-20] → [隔膜1行, 隔膜1列+20]
                            //                        [顶盖行, 隔膜2列-20] → [隔膜2行, 隔膜2列+20]
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                ho_Rectangle.Dispose();
                                HOperatorSet.GenRectangle1(out ho_Rectangle, hv_Row.TupleConcat(hv_Row),
                                    (((hv_Column.TupleSelect(0)) - 20)).TupleConcat((hv_Column.TupleSelect(
                                    1)) - 20), ((hv_Row2.TupleSelect(0))).TupleConcat(hv_Row2.TupleSelect(
                                    1)), (((hv_Column.TupleSelect(0)) + 20)).TupleConcat((hv_Column.TupleSelect(
                                    1)) + 20));
                            }
                            hv_Row3.Dispose(); hv_Column2.Dispose(); hv_Phi1.Dispose(); hv_Length11.Dispose(); hv_Length21.Dispose();
                            HOperatorSet.SmallestRectangle2(ho_Rectangle, out hv_Row3, out hv_Column2,
                                out hv_Phi1, out hv_Length11, out hv_Length21);
                            // 对第一个隔膜生成卡尺，找3对边缘点
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_MeasureHandle.Dispose();
                                HOperatorSet.GenMeasureRectangle2(hv_Row3.TupleSelect(0), hv_Column2.TupleSelect(
                                    0), -(hv_Phi1.TupleSelect(0)), hv_Length11.TupleSelect(0), hv_Length21.TupleSelect(
                                    0), hv_Width, hv_Height, "nearest_neighbor", out hv_MeasureHandle);
                            }
                            hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_DistanceMo1.Dispose();
                            HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 3, 15, "all", "all",
                                out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude, out hv_DistanceMo1);
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                ho_Cross1.Dispose();
                                HOperatorSet.GenCrossContourXld(out ho_Cross1, hv_RowEdge, hv_ColumnEdge,
                                    16, (new HTuple(45)).TupleRad());
                            }
                            if ((int)(new HTuple(hv_DistanceMo1.TupleLength())) != 0)
                            {
                                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                {
                                    ho_Arrow1.Dispose();
                                    gen_arrow_contour_xld(out ho_Arrow1, hv_RowEdge.TupleSelect(0), hv_ColumnEdge.TupleSelect(
                                        0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5, 5);
                                }
                            }
                            else
                            {
                                ho_Arrow1.Dispose();
                                HOperatorSet.GenEmptyObj(out ho_Arrow1);
                                hv_DistanceMo1.Dispose();
                                hv_DistanceMo1 = 0;
                            }

                            // ---- 测量第二个隔膜（通常是右侧） ----
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_MeasureHandle.Dispose();
                                HOperatorSet.GenMeasureRectangle2(hv_Row3.TupleSelect(1), hv_Column2.TupleSelect(
                                    1), -(hv_Phi1.TupleSelect(1)), hv_Length11.TupleSelect(1), hv_Length21.TupleSelect(
                                    1), hv_Width, hv_Height, "nearest_neighbor", out hv_MeasureHandle);
                            }
                            hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_DistanceMo2.Dispose();
                            HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 3, 30, "all", "all",
                                out hv_RowEdge, out hv_ColumnEdge, out hv_Amplitude, out hv_DistanceMo2);
                            if ((int)(new HTuple(hv_DistanceMo2.TupleLength())) != 0)
                            {
                                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                {
                                    ho_Arrow2.Dispose();
                                    gen_arrow_contour_xld(out ho_Arrow2, hv_RowEdge.TupleSelect(0), hv_ColumnEdge.TupleSelect(
                                        0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5, 5);
                                }
                            }
                            else
                            {
                                ho_Arrow2.Dispose();
                                HOperatorSet.GenEmptyObj(out ho_Arrow2);
                                hv_DistanceMo2.Dispose();
                                hv_DistanceMo2 = 0;
                            }
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                ho_Cross2.Dispose();
                                HOperatorSet.GenCrossContourXld(out ho_Cross2, hv_RowEdge, hv_ColumnEdge,
                                    16, (new HTuple(45)).TupleRad());
                            }

                            // 合并左右两个隔膜的十字、箭头
                            ho_Cross.Dispose();
                            HOperatorSet.ConcatObj(ho_Cross1, ho_Cross2, out ho_Cross);
                            ho_Arrow.Dispose();
                            HOperatorSet.ConcatObj(ho_Arrow1, ho_Arrow2, out ho_Arrow);
                            hv_DistanceMo.Dispose();
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_DistanceMo = new HTuple();
                                hv_DistanceMo = hv_DistanceMo.TupleConcat(hv_DistanceMo1, hv_DistanceMo2);
                            }
                        }
                    }

                    // ============================================================
                    // 第五步：拟合顶盖线（ho_Line1），计算热熔点到顶盖线的距离
                    //
                    // 顶盖线 = 位于顶盖区域(ho_Region1=灰度60)底边的一条水平直线
                    // 拟合方法：
                    //   5.1 取热熔点区域(ho_Region3=灰度180)最大面积的连通域，用于后续距离计算
                    //   5.2 计算顶盖(ho_Region1)的最小外接矩形，取其底边作为顶盖线的初始位置
                    //       （顶盖线 Y = 顶盖中心行 + 顶盖半高，即顶盖矩形的下边）
                    //   5.3 用计量模型(Metrology Model)在该初始位置精确拟合直线
                    //   5.4 对拟合结果用 FitLineContourXld 做 Tukey 鲁棒直线拟合
                    //   5.5 计算热熔点中心到该直线的垂直距离 → hv_DistanceRongDian
                    // ============================================================

                    // 5.1 热熔点区域取最大面积连通域（用于后续计算熔点到顶盖线的距离）
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region3, out ho_ConnectedRegions);
                    ho_Region3.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region3, "max_area", 70);

                    // 5.2 计算顶盖(ho_Region1)的最小外接旋转矩形，得到中心(hv_Row, hv_Column)和半宽半高
                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                    HOperatorSet.SmallestRectangle2(ho_Region1, out hv_Row, out hv_Column, out hv_Phi,
                        out hv_Length1, out hv_Length2);
                    // 顶盖线的初始位置 = 顶盖外接矩形的底边：
                    //   左端点 = (顶盖列 - 半宽, 顶盖行 + 半高)
                    //   右端点 = (顶盖列 + 半宽, 顶盖行 + 半高)
                    // 即：取顶盖外接矩形最下方的水平边作为顶盖线的初始估计
                    hv_Line1_X1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_X1 = hv_Column - hv_Length1;    // 线左端点列坐标
                    }
                    hv_Line1_Y1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_Y1 = hv_Row + hv_Length2;        // 线左端点行坐标（顶盖底边）
                    }
                    hv_Line1_X2.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_X2 = hv_Column + hv_Length1;    // 线右端点列坐标
                    }
                    hv_Line1_Y2.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_Y2 = hv_Row + hv_Length2;        // 线右端点行坐标（与左端点同行）
                    }

                    // 5.3 用计量模型在初始线位置附近精确拟合直线边缘
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    hv_Index1.Dispose();
                    // 添加直线测量对象：测量矩形半宽20、半高5，边缘幅度阈值30
                    HOperatorSet.AddMetrologyObjectLineMeasure(hv_MetrologyHandle, hv_Line1_Y1,
                        hv_Line1_X1, hv_Line1_Y2, hv_Line1_X2, 20, 5, 1, 30, new HTuple(), new HTuple(),
                        out hv_Index1);
                    // 设置计量参数：30个测量矩形、最低分数0.3、只取第一个边缘、接受所有极性
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures", 30);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score", 0.3);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_select", "first");
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition", "all");
                    // 执行计量测量
                    HOperatorSet.ApplyMetrologyModel(ho_Image, hv_MetrologyHandle);
                    ho_MeeasureRegion.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_MeeasureRegion, hv_MetrologyHandle,
                        "all", "all", out hv_Row1, out hv_Column1);
                    // 将测量到的边缘点连成折线 → ho_Line1（顶盖线）
                    ho_Line1.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_Line1, hv_Row1, hv_Column1);
                    ho_Cross.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row1, hv_Column1, 6, 0.785398);

                    // 5.4 Tukey 鲁棒直线拟合 → 得到最终的直线参数
                    hv_RowBegin.Dispose(); hv_ColBegin.Dispose(); hv_RowEnd.Dispose(); hv_ColEnd.Dispose(); hv_Nr.Dispose(); hv_Nc.Dispose(); hv_Dist.Dispose();
                    HOperatorSet.FitLineContourXld(ho_Line1, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                        out hv_ColBegin, out hv_RowEnd, out hv_ColEnd, out hv_Nr, out hv_Nc,
                        out hv_Dist);

                    // 5.5 计算热熔点到顶盖线的垂直距离
                    // 热熔点的中心坐标(hv_Row4, hv_Column3)，即 ho_Region3 的中心
                    hv_Area.Dispose(); hv_Row4.Dispose(); hv_Column3.Dispose();
                    HOperatorSet.AreaCenter(ho_Region3, out hv_Area, out hv_Row4, out hv_Column3);
                    // 从热熔点中心向下画一条竖直线，与顶盖线求交点
                    hv_RowOver.Dispose(); hv_ColumnOver.Dispose(); hv_IsOverlapping.Dispose();
                    HOperatorSet.IntersectionLines(0, hv_Column3, hv_Height, hv_Column3, hv_RowBegin,
                        hv_ColBegin, hv_RowEnd, hv_ColEnd, out hv_RowOver, out hv_ColumnOver,
                        out hv_IsOverlapping);
                    // 画箭头：从热熔点中心 → 顶盖线上的交点
                    ho_RongDianArrow.Dispose();
                    gen_arrow_contour_xld(out ho_RongDianArrow, hv_Row4, hv_Column3, hv_RowOver,
                        hv_ColumnOver, 5, 5);
                    // DistancePl: 计算热熔点中心点到顶盖线的垂直距离 → 这就是"熔点到顶盖距离"
                    hv_DistanceRongDian.Dispose();
                    HOperatorSet.DistancePl(hv_Row4, hv_Column3, hv_RowBegin, hv_ColBegin, hv_RowEnd,
                        hv_ColEnd, out hv_DistanceRongDian);

                    // ============================================================
                    // 第六步：计算"膜到顶盖距离"（hv_DistanceTopCover）
                    //
                    // 这是"左膜到顶盖距离"和"右膜到顶盖距离"的核心计算：
                    //
                    // 对 ho_Region2（隔膜区域）中的每一个连通域：
                    //   6.1 计算该区域的最小外接旋转矩形
                    //   6.2 取矩形的"顶边中点"作为膜的顶点坐标：
                    //       顶边行 = 中心行 - 半高（即矩形最上边）
                    //       顶边列 = 中心列
                    //   6.3 从该顶点向下画一条竖直线，与第五步拟合出的顶盖线(ho_Line1)求交点
                    //   6.4 计算顶点到交点的垂直距离 DistancePl → 这就是该膜区域到顶盖的距离
                    //
                    // Left模式:  ho_Region2 只有1个（最左侧隔膜）
                    //           → hv_DistanceTopCover[0] = 左膜到顶盖距离
                    // Right模式: ho_Region2 只有1个（最右侧隔膜）
                    //           → hv_DistanceTopCover[0] = 右膜到顶盖距离
                    // Center模式: ho_Region2 有2个（左右两个隔膜）
                    //           → hv_DistanceTopCover[0] = 左膜到顶盖距离
                    //           → hv_DistanceTopCover[最后] = 右膜到顶盖距离
                    //
                    // 同时在图上画出箭头：从膜顶点 → 与顶盖线的交点
                    // ============================================================
                    {
                        HObject ho_Region2Conn, ho_SingleRegion, ho_TempArrow;
                        HTuple hv_NumRegions, hv_RR, hv_CC, hv_Phi2, hv_LL1, hv_LL2;
                        HTuple hv_TopRow, hv_TopCol, hv_RowO, hv_ColO, hv_IsO, hv_Dist2;

                        ho_TopCoverArrow.Dispose();
                        HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
                        hv_DistanceTopCover.Dispose();
                        hv_DistanceTopCover = new HTuple();

                        // 6.1 将隔膜区域拆分为独立连通域，逐个处理
                        HOperatorSet.Connection(ho_Region2, out ho_Region2Conn);
                        HOperatorSet.CountObj(ho_Region2Conn, out hv_NumRegions);

                        HTuple end = hv_NumRegions + 1;
                        for (int i = 1; i < end.I; i++)
                        {
                            HOperatorSet.SelectObj(ho_Region2Conn, out ho_SingleRegion, i);

                            // 6.2 计算当前膜区域的最小外接旋转矩形
                            //     hv_RR=中心行, hv_CC=中心列
                            //     hv_LL1=半宽, hv_LL2=半高
                            HOperatorSet.SmallestRectangle2(ho_SingleRegion, out hv_RR, out hv_CC,
                                out hv_Phi2, out hv_LL1, out hv_LL2);
                            // 顶边中点 = (中心行 - 半高, 中心列)
                            // 即矩形最上方那条边的中点
                            hv_TopRow = hv_RR - hv_LL2;
                            hv_TopCol = hv_CC;

                            // 6.3 从顶边中点向下画竖直线(从行=0到行=图像高度)，
                            //     与顶盖线 ho_Line1 求交点
                            HOperatorSet.IntersectionLines(0, hv_TopCol, hv_Height, hv_TopCol,
                                hv_RowBegin, hv_ColBegin, hv_RowEnd, hv_ColEnd,
                                out hv_RowO, out hv_ColO, out hv_IsO);

                            // 画箭头：膜顶点 → 交点（可视化用）
                            gen_arrow_contour_xld(out ho_TempArrow, hv_TopRow, hv_TopCol,
                                hv_RowO, hv_ColO, 5, 5);

                            // 6.4 计算膜顶点到顶盖线的垂直距离（点到直线的距离）
                            //     这个值 × 像素当量 = 最终的"膜到顶盖距离"
                            HOperatorSet.DistancePl(hv_TopRow, hv_TopCol,
                                hv_RowBegin, hv_ColBegin, hv_RowEnd, hv_ColEnd, out hv_Dist2);

                            // 将箭头累积到 ho_TopCoverArrow，距离累积到 hv_DistanceTopCover
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_TopCoverArrow, ho_TempArrow,
                                    out ExpTmpOutVar_0);
                                ho_TopCoverArrow.Dispose();
                                ho_TopCoverArrow = ExpTmpOutVar_0;
                            }
                            hv_DistanceTopCover = hv_DistanceTopCover.TupleConcat(hv_Dist2);

                            ho_SingleRegion.Dispose();
                            ho_TempArrow.Dispose();
                            hv_RR.Dispose(); hv_CC.Dispose(); hv_Phi2.Dispose();
                            hv_LL1.Dispose(); hv_LL2.Dispose();
                            hv_TopRow.Dispose(); hv_TopCol.Dispose();
                            hv_RowO.Dispose(); hv_ColO.Dispose(); hv_IsO.Dispose();
                            hv_Dist2.Dispose();
                        }
                        ho_Region2Conn.Dispose();
                        hv_NumRegions.Dispose();
                    }
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    Logger.AddLog("包膜算法模块计算距离异常", eMsgType.Error);

                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    ho_Line1.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Line1);
                    ho_Arrow.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Arrow);
                    ho_Cross.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Cross);
                    hv_DistanceRongDian.Dispose();
                    hv_DistanceRongDian = 0;
                    hv_DistanceMo.Dispose();
                    hv_DistanceMo = new HTuple();
                    hv_DistanceMo[0] = 0;
                    hv_DistanceMo[1] = 0;
                    ho_TopCoverArrow.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
                    ho_RongDianArrow.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_RongDianArrow);
                    hv_DistanceTopCover.Dispose();
                    hv_DistanceTopCover = new HTuple();

                }


                ho_Region1.Dispose();
                ho_Region3.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_Region2.Dispose();
                ho_RegionDilation.Dispose();
                ho_RegionDifference.Dispose();
                ho_RegionDilation1.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross1.Dispose();
                ho_Arrow1.Dispose();
                ho_Arrow2.Dispose();
                ho_Cross2.Dispose();
                ho_MeeasureRegion.Dispose();

                hv_Value.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_Row2.Dispose();
                hv_Row3.Dispose();
                hv_Column2.Dispose();
                hv_Phi1.Dispose();
                hv_Length11.Dispose();
                hv_Length21.Dispose();
                hv_MeasureHandle.Dispose();
                hv_RowEdge.Dispose();
                hv_ColumnEdge.Dispose();
                hv_Amplitude.Dispose();
                hv_DistanceMo1.Dispose();
                hv_DistanceMo2.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Line1_X1.Dispose();
                hv_Line1_Y1.Dispose();
                hv_Line1_X2.Dispose();
                hv_Line1_Y2.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_Index1.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_RowBegin.Dispose();
                hv_ColBegin.Dispose();
                hv_RowEnd.Dispose();
                hv_ColEnd.Dispose();
                hv_Nr.Dispose();
                hv_Nc.Dispose();
                hv_Dist.Dispose();
                hv_Area.Dispose();
                hv_Row4.Dispose();
                hv_Column3.Dispose();
                hv_RowOver.Dispose();
                hv_ColumnOver.Dispose();
                hv_IsOverlapping.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region1.Dispose();
                ho_Region3.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_Region2.Dispose();
                ho_RegionDilation.Dispose();
                ho_RegionDifference.Dispose();
                ho_RegionDilation1.Dispose();
                ho_Rectangle.Dispose();
                ho_Cross1.Dispose();
                ho_Arrow1.Dispose();
                ho_Arrow2.Dispose();
                ho_Cross2.Dispose();
                ho_MeeasureRegion.Dispose();
                ho_TopCoverArrow.Dispose();
                ho_RongDianArrow.Dispose();

                hv_Value.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_Row2.Dispose();
                hv_Row3.Dispose();
                hv_Column2.Dispose();
                hv_Phi1.Dispose();
                hv_Length11.Dispose();
                hv_Length21.Dispose();
                hv_MeasureHandle.Dispose();
                hv_RowEdge.Dispose();
                hv_ColumnEdge.Dispose();
                hv_Amplitude.Dispose();
                hv_DistanceMo1.Dispose();
                hv_DistanceMo2.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Line1_X1.Dispose();
                hv_Line1_Y1.Dispose();
                hv_Line1_X2.Dispose();
                hv_Line1_Y2.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_Index1.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_RowBegin.Dispose();
                hv_ColBegin.Dispose();
                hv_RowEnd.Dispose();
                hv_ColEnd.Dispose();
                hv_Nr.Dispose();
                hv_Nc.Dispose();
                hv_Dist.Dispose();
                hv_Area.Dispose();
                hv_Row4.Dispose();
                hv_Column3.Dispose();
                hv_RowOver.Dispose();
                hv_ColumnOver.Dispose();
                hv_IsOverlapping.Dispose();
                hv_Exception.Dispose();
                hv_DistanceTopCover.Dispose();

                throw HDevExpDefaultException;
            }
        }

        /// <summary>
        /// 计算图像中每个蓝膜区域（灰度值2）的面积
        /// </summary>
        /// <param name="ho_Image">AI 分割结果图像（已 ScaleImage 放大）</param>
        /// <param name="hv_Areas">每个蓝膜区域的面积数组</param>
        /// <param name="hv_Rows">每个蓝膜区域的行坐标</param>
        /// <param name="hv_Columns">每个蓝膜区域的列坐标</param>
        public static void CalcBlueMembraneAreas(HObject ho_Image, out HTuple hv_Areas,
            out HTuple hv_Rows, out HTuple hv_Columns)
        {
            HObject ho_BlueRegion = null, ho_ConnectedRegions = null;

            HOperatorSet.GenEmptyObj(out ho_BlueRegion);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            hv_Areas = new HTuple();
            hv_Rows = new HTuple();
            hv_Columns = new HTuple();

            try
            {
                ho_BlueRegion.Dispose();
                HOperatorSet.Threshold(ho_Image, out ho_BlueRegion, 120, 120);

                ho_ConnectedRegions.Dispose();
                HOperatorSet.Connection(ho_BlueRegion, out ho_ConnectedRegions);

                hv_Areas.Dispose();
                HOperatorSet.RegionFeatures(ho_ConnectedRegions, "area", out hv_Areas);
                hv_Rows.Dispose();
                HOperatorSet.RegionFeatures(ho_ConnectedRegions, "row", out hv_Rows);
                hv_Columns.Dispose();
                HOperatorSet.RegionFeatures(ho_ConnectedRegions, "column", out hv_Columns);

                ho_BlueRegion.Dispose();
                ho_ConnectedRegions.Dispose();
            }
            catch (HalconException)
            {
                ho_BlueRegion.Dispose();
                ho_ConnectedRegions.Dispose();
                hv_Areas = new HTuple();
                hv_Rows = new HTuple();
                hv_Columns = new HTuple();
            }
        }

        /// <summary>
        /// 过滤掉面积过小的蓝膜区域（灰度值2），返回清理后的标签图
        /// </summary>
        public static void FilterSmallBlueMembrane(HImage ho_Image, HTuple hv_MinArea,
            out HImage ho_FilteredImage)
        {
            HObject ho_AllRegion2 = null, ho_ConnectedRegions = null;
            HObject ho_LargeRegions = null, ho_SmallRegions = null;

            HOperatorSet.GenEmptyObj(out ho_AllRegion2);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_LargeRegions);
            HOperatorSet.GenEmptyObj(out ho_SmallRegions);
            ho_FilteredImage = null;

            try
            {
                // 1. 提取所有灰度=2的区域
                ho_AllRegion2.Dispose();
                HOperatorSet.Threshold(ho_Image, out ho_AllRegion2, 2.0, 2.0);

                // 2. 拆分为独立连通域
                ho_ConnectedRegions.Dispose();
                HOperatorSet.Connection(ho_AllRegion2, out ho_ConnectedRegions);

                // 3. 按面积筛选，保留大面积区域
                ho_LargeRegions.Dispose();
                HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_LargeRegions,
                    "area", "and", hv_MinArea, 99999999);

                // 4. 取差集得到小面积区域
                ho_SmallRegions.Dispose();
                HOperatorSet.Difference(ho_AllRegion2, ho_LargeRegions, out ho_SmallRegions);

                // 5. 用 PaintRegion 把小区域像素抹为 0
                HObject tempObj = new HImage(ho_Image);
                HOperatorSet.PaintRegion(ho_SmallRegions, tempObj,
                    out HObject resultObj, 0.0, "fill");
                ho_FilteredImage = new HImage(resultObj);
                tempObj.Dispose();
                resultObj.Dispose();

                ho_AllRegion2.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_LargeRegions.Dispose();
                ho_SmallRegions.Dispose();
            }
            catch (HalconException)
            {
                ho_AllRegion2.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_LargeRegions.Dispose();
                ho_SmallRegions.Dispose();
                ho_FilteredImage = new HImage(ho_Image);
            }
        }

        /// <summary>
        /// 过滤掉面积过小的顶盖区域（灰度值1），返回清理后的标签图
        /// 原理与 FilterSmallBlueMembrane 相同，只将阈值从 2.0 改为 1.0
        /// </summary>
        public static void FilterSmallTopCover(HImage ho_Image, HTuple hv_MinArea,
            out HImage ho_FilteredImage)
        {
            HObject ho_AllRegion1 = null, ho_ConnectedRegions = null;
            HObject ho_LargeRegions = null, ho_SmallRegions = null;

            HOperatorSet.GenEmptyObj(out ho_AllRegion1);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_LargeRegions);
            HOperatorSet.GenEmptyObj(out ho_SmallRegions);
            ho_FilteredImage = null;

            try
            {
                ho_AllRegion1.Dispose();
                HOperatorSet.Threshold(ho_Image, out ho_AllRegion1, 1.0, 1.0);

                ho_ConnectedRegions.Dispose();
                HOperatorSet.Connection(ho_AllRegion1, out ho_ConnectedRegions);

                ho_LargeRegions.Dispose();
                HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_LargeRegions,
                    "area", "and", hv_MinArea, 99999999);

                ho_SmallRegions.Dispose();
                HOperatorSet.Difference(ho_AllRegion1, ho_LargeRegions, out ho_SmallRegions);

                HObject tempObj = new HImage(ho_Image);
                HOperatorSet.PaintRegion(ho_SmallRegions, tempObj,
                    out HObject resultObj, 0.0, "fill");
                ho_FilteredImage = new HImage(resultObj);
                tempObj.Dispose();
                resultObj.Dispose();

                ho_AllRegion1.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_LargeRegions.Dispose();
                ho_SmallRegions.Dispose();
            }
            catch (HalconException)
            {
                ho_AllRegion1.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_LargeRegions.Dispose();
                ho_SmallRegions.Dispose();
                ho_FilteredImage = new HImage(ho_Image);
            }
        }

        public static void gen_arrow_contour_xld(out HObject ho_Arrow, HTuple hv_Row1, HTuple hv_Column1,
    HTuple hv_Row2, HTuple hv_Column2, HTuple hv_HeadLength, HTuple hv_HeadWidth)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_TempArrow = null;

            // Local control variables 

            HTuple hv_Length = new HTuple(), hv_ZeroLengthIndices = new HTuple();
            HTuple hv_DR = new HTuple(), hv_DC = new HTuple(), hv_HalfHeadWidth = new HTuple();
            HTuple hv_RowP1 = new HTuple(), hv_ColP1 = new HTuple();
            HTuple hv_RowP2 = new HTuple(), hv_ColP2 = new HTuple();
            HTuple hv_Index = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            HOperatorSet.GenEmptyObj(out ho_TempArrow);
            try
            {
                //This procedure generates arrow shaped XLD contours,
                //pointing from (Row1, Column1) to (Row2, Column2).
                //If starting and end point are identical, a contour consisting
                //of a single point is returned.
                //
                //input parameteres:
                //Row1, Column1: Coordinates of the arrows' starting points
                //Row2, Column2: Coordinates of the arrows' end points
                //HeadLength, HeadWidth: Size of the arrow heads in pixels
                //
                //output parameter:
                //Arrow: The resulting XLD contour
                //
                //The input tuples Row1, Column1, Row2, and Column2 have to be of
                //the same length.
                //HeadLength and HeadWidth either have to be of the same length as
                //Row1, Column1, Row2, and Column2 or have to be a single element.
                //If one of the above restrictions is violated, an error will occur.
                //
                //
                //Init
                ho_Arrow.Dispose();
                HOperatorSet.GenEmptyObj(out ho_Arrow);
                //
                //Calculate the arrow length
                hv_Length.Dispose();
                HOperatorSet.DistancePp(hv_Row1, hv_Column1, hv_Row2, hv_Column2, out hv_Length);
                //
                //Mark arrows with identical start and end point
                //(set Length to -1 to avoid division-by-zero exception)
                hv_ZeroLengthIndices.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_ZeroLengthIndices = hv_Length.TupleFind(
                        0);
                }
                if ((int)(new HTuple(hv_ZeroLengthIndices.TupleNotEqual(-1))) != 0)
                {
                    if (hv_Length == null)
                        hv_Length = new HTuple();
                    hv_Length[hv_ZeroLengthIndices] = -1;
                }
                //
                //Calculate auxiliary variables.
                hv_DR.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_DR = (1.0 * (hv_Row2 - hv_Row1)) / hv_Length;
                }
                hv_DC.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_DC = (1.0 * (hv_Column2 - hv_Column1)) / hv_Length;
                }
                hv_HalfHeadWidth.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_HalfHeadWidth = hv_HeadWidth / 2.0;
                }
                //
                //Calculate end points of the arrow head.
                hv_RowP1.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_RowP1 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) + (hv_HalfHeadWidth * hv_DC);
                }
                hv_ColP1.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_ColP1 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) - (hv_HalfHeadWidth * hv_DR);
                }
                hv_RowP2.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_RowP2 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) - (hv_HalfHeadWidth * hv_DC);
                }
                hv_ColP2.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_ColP2 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) + (hv_HalfHeadWidth * hv_DR);
                }
                //
                //Finally create output XLD contour for each input point pair
                for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Length.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
                {
                    if ((int)(new HTuple(((hv_Length.TupleSelect(hv_Index))).TupleEqual(-1))) != 0)
                    {
                        //Create_ single points for arrows with identical start and end point
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_TempArrow.Dispose();
                            HOperatorSet.GenContourPolygonXld(out ho_TempArrow, hv_Row1.TupleSelect(
                                hv_Index), hv_Column1.TupleSelect(hv_Index));
                        }
                    }
                    else
                    {
                        //Create arrow contour
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_TempArrow.Dispose();
                            HOperatorSet.GenContourPolygonXld(out ho_TempArrow, ((((((((((hv_Row1.TupleSelect(
                                hv_Index))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                                hv_RowP1.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)))).TupleConcat(
                                hv_RowP2.TupleSelect(hv_Index)))).TupleConcat(hv_Row2.TupleSelect(hv_Index)),
                                ((((((((((hv_Column1.TupleSelect(hv_Index))).TupleConcat(hv_Column2.TupleSelect(
                                hv_Index)))).TupleConcat(hv_ColP1.TupleSelect(hv_Index)))).TupleConcat(
                                hv_Column2.TupleSelect(hv_Index)))).TupleConcat(hv_ColP2.TupleSelect(
                                hv_Index)))).TupleConcat(hv_Column2.TupleSelect(hv_Index)));
                        }
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_Arrow, ho_TempArrow, out ExpTmpOutVar_0);
                        ho_Arrow.Dispose();
                        ho_Arrow = ExpTmpOutVar_0;
                    }
                }
                ho_TempArrow.Dispose();

                hv_Length.Dispose();
                hv_ZeroLengthIndices.Dispose();
                hv_DR.Dispose();
                hv_DC.Dispose();
                hv_HalfHeadWidth.Dispose();
                hv_RowP1.Dispose();
                hv_ColP1.Dispose();
                hv_RowP2.Dispose();
                hv_ColP2.Dispose();
                hv_Index.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_TempArrow.Dispose();

                hv_Length.Dispose();
                hv_ZeroLengthIndices.Dispose();
                hv_DR.Dispose();
                hv_DC.Dispose();
                hv_HalfHeadWidth.Dispose();
                hv_RowP1.Dispose();
                hv_ColP1.Dispose();
                hv_RowP2.Dispose();
                hv_ColP2.Dispose();
                hv_Index.Dispose();

                throw HDevExpDefaultException;
            }
        }

    }
}
