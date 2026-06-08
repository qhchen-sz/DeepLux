using System;
using System.Collections.Generic;
using HalconDotNet;
using HV.Common.Enums;
using HV.Common.Provide;

namespace Plugin.Envelope.ViewModels
{
    public static class Algorithm
    {
        /// <summary>
        /// 寻边算法：基于AI分割标签图，计算热熔点到顶盖距离、蓝膜到顶盖距离。
        ///
        /// ========== 输入 ==========
        /// ho_Image   : AI语义分割标签图，已做ScaleImage(60,0)。灰度60=顶盖, 120=蓝膜, 180=热熔点
        /// hv_Location: "Left" / "Right" / "Center"
        ///
        /// ========== 输出 ==========
        /// ho_LineLeft/Mid/Right      : 左/中/右顶盖下边缘拟合直线（蓝色显示）
        /// ho_ConnectLine             : 三条线首尾相连的折线（红色显示）
        /// ho_RegionLeftTop/MidTop/RightTop : 左/中/右顶盖子区域（粉色填充显示）
        /// hv_DistanceRongDian        : 热熔点中心→中顶盖线的垂直距离 → ResultValue1
        /// hv_DistanceTopCover        : 左膜上边中点→左顶盖线、右膜上边中点→右顶盖线的垂直距离 → ResultValue2/3
        /// ho_TopCoverArrow           : 膜上边→顶盖线的箭头集合（绿色）
        /// ho_RongDianArrow           : 热熔点中心→中顶盖线的箭头（蓝色）
        /// ho_DistanceLine            : 膜到顶盖的竖直线段（红色）
        /// ho_DistanceScale           : 距离水平标尺线（红色）
        /// ho_MembraneRegion          : 隔膜区域外接旋转矩形（SmallestRectangle2，绿色不填充）
        /// hv_MemTopRow/Col           : 隔膜外接矩形顶边中点坐标
        /// ================================================================
        /// </summary>
        public static void Find_RongDian(HObject ho_Image, out HObject ho_LineLeft, out HObject ho_LineMid,
            out HObject ho_LineRight, out HObject ho_ConnectLine,
            out HObject ho_RegionLeftTop, out HObject ho_RegionMidTop, out HObject ho_RegionRightTop,
            HTuple hv_Location,
            out HTuple hv_DistanceRongDian,
            out HObject ho_TopCoverArrow, out HTuple hv_DistanceTopCover, out HObject ho_RongDianArrow,
            out HObject ho_DistanceLine, out HObject ho_DistanceScale,
            out HObject ho_MembraneRegion, out HTuple hv_MemTopRow, out HTuple hv_MemTopCol)
        {
            // ========== 局部 HObject 变量 ==========
            // ho_Region1: 顶盖区域（灰度60），取最大连通域
            // ho_Region2: 隔膜区域（灰度120），经三步筛选（差集→高度过滤→顶盖竖带交集）
            // ho_Region3: 热熔点区域（灰度180），取最大连通域
            HObject ho_Region1 = null, ho_Region3 = null, ho_ConnectedRegions = null;
            HObject ho_Region2 = null, ho_RegionDilation = null, ho_RegionDifference = null;
            HObject ho_RegionDilation1 = null;
            // ho_Sorted        : 排序后的隔膜区域
            // ho_LeftMembrane  : 分离出的左蓝膜（Center模式使用）
            // ho_RightMembrane : 分离出的右蓝膜（Center模式使用）
            HObject ho_Sorted = null, ho_LeftMembrane = null, ho_RightMembrane = null;
            // ho_RectCutLeft/Mid/Right : 切割顶盖用的贯穿全图高度的矩形
            HObject ho_RectCutLeft = null, ho_RectCutMid = null, ho_RectCutRight = null;

            // ========== 局部 HTuple 变量 ==========
            // 图像尺寸和区域属性
            HTuple hv_Value = new HTuple(), hv_Width = new HTuple(), hv_Height = new HTuple();
            // hv_Row     : 顶盖外接矩形中心行（第六步用于fallback底边Y坐标）
            // hv_Phi     : 顶盖外接矩形角度（占位，不使用）
            // hv_Length1 : 顶盖外接矩形半宽（占位，不使用）
            // hv_Length2 : 顶盖外接矩形半高，hv_Row+hv_Length2=顶盖底边Y坐标
            HTuple hv_Row = new HTuple(), hv_Phi = new HTuple();
            HTuple hv_Length1 = new HTuple(), hv_Length2 = new HTuple();
            // 热熔点中心坐标和距离计算
            HTuple hv_Area = new HTuple(), hv_Row4 = new HTuple(), hv_Column3 = new HTuple();
            HTuple hv_RowOver = new HTuple(), hv_ColumnOver = new HTuple(), hv_IsOverlapping = new HTuple();
            HTuple hv_Exception = new HTuple();

            // 列坐标范围：左蓝膜/热熔点/右蓝膜各自的最左列(column1)和最右列(column2)
            // 这些是顶盖切三块的分割依据
            HTuple hv_LeftCol1 = new HTuple(), hv_LeftCol2 = new HTuple();
            HTuple hv_MidCol1 = new HTuple(), hv_MidCol2 = new HTuple();
            HTuple hv_RightCol1 = new HTuple(), hv_RightCol2 = new HTuple();

            // 三条顶盖下边缘直线的拟合参数（起止行列坐标）
            // Rb/Cb = RowBegin/ColBegin（起点）, Re/Ce = RowEnd/ColEnd（终点）
            HTuple hv_LeftRb = new HTuple(), hv_LeftCb = new HTuple();
            HTuple hv_LeftRe = new HTuple(), hv_LeftCe = new HTuple();
            HTuple hv_MidRb = new HTuple(), hv_MidCb = new HTuple();
            HTuple hv_MidRe = new HTuple(), hv_MidCe = new HTuple();
            HTuple hv_RightRb = new HTuple(), hv_RightCb = new HTuple();
            HTuple hv_RightRe = new HTuple(), hv_RightCe = new HTuple();
            // FitBottomEdge临时输出（面积检查用）
            HTuple hv_BotCenterRow = new HTuple(), hv_BotCenterCol = new HTuple();

            // ========== 初始化所有输出对象为空 ==========
            HOperatorSet.GenEmptyObj(out ho_LineLeft);
            HOperatorSet.GenEmptyObj(out ho_LineMid);
            HOperatorSet.GenEmptyObj(out ho_LineRight);
            HOperatorSet.GenEmptyObj(out ho_ConnectLine);
            HOperatorSet.GenEmptyObj(out ho_RegionLeftTop);
            HOperatorSet.GenEmptyObj(out ho_RegionMidTop);
            HOperatorSet.GenEmptyObj(out ho_RegionRightTop);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_Region3);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            HOperatorSet.GenEmptyObj(out ho_RegionDilation);
            HOperatorSet.GenEmptyObj(out ho_RegionDifference);
            HOperatorSet.GenEmptyObj(out ho_RegionDilation1);
            HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
            HOperatorSet.GenEmptyObj(out ho_RongDianArrow);
            HOperatorSet.GenEmptyObj(out ho_DistanceLine);
            HOperatorSet.GenEmptyObj(out ho_DistanceScale);
            HOperatorSet.GenEmptyObj(out ho_MembraneRegion);
            HOperatorSet.GenEmptyObj(out ho_Sorted);
            HOperatorSet.GenEmptyObj(out ho_LeftMembrane);
            HOperatorSet.GenEmptyObj(out ho_RightMembrane);
            HOperatorSet.GenEmptyObj(out ho_RectCutLeft);
            HOperatorSet.GenEmptyObj(out ho_RectCutMid);
            HOperatorSet.GenEmptyObj(out ho_RectCutRight);
            hv_DistanceRongDian = new HTuple();
            hv_DistanceTopCover = new HTuple();
            hv_MemTopRow = new HTuple();
            hv_MemTopCol = new HTuple();

            // 外层try：最终安全保障，任何Halcon异常都释放所有资源
            try
            {
                // 内层try：核心计算逻辑，异常时重置输出为安全默认值
                try
                {
                    // ============================================================
                    // 第一步：初始化所有输出变量为安全默认值（空对象/0值）
                    // 目的：确保异常时调用方读取输出也不会拿到损坏数据
                    // ============================================================
                    ho_LineLeft.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_LineLeft);
                    ho_LineMid.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_LineMid);
                    ho_LineRight.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_LineRight);
                    ho_ConnectLine.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_ConnectLine);
                    ho_RegionLeftTop.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_RegionLeftTop);
                    ho_RegionMidTop.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_RegionMidTop);
                    ho_RegionRightTop.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_RegionRightTop);
                    hv_DistanceRongDian.Dispose();
                    hv_DistanceRongDian = 0;

                    // ============================================================
                    // 第二步：从AI标签图中提取三个原始区域
                    // 输入是ScaleImage(60,0)后的标签图，故阈值分别为60/120/180
                    // ============================================================

                    // 2.1 顶盖：灰度精确等于60的像素
                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region1, 60, 60);

                    // 2.2 热熔点：灰度精确等于180的像素
                    ho_Region3.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region3, 180, 180);

                    // 2.3 顶盖只保留面积最大的连通域（排除AI分割碎片噪点）
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions);
                    ho_Region1.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region1, "max_area", 70);

                    // ============================================================
                    // 第三步：从蓝膜(灰度120)中筛选出真正的"隔膜" → ho_Region2
                    //
                    // 筛选逻辑：
                    //   3.1 提取全部灰度120区域
                    //   3.2 热熔点膨胀(半宽100列×半高500行)后做差集 → 排除与热熔点垂直重叠的膜
                    //   3.3 高度过滤：只保留高度 > 最大高度/3 的区域 → 排除细小噪点
                    //   3.4 顶盖膨胀成全图高度的竖带(半宽1行×半高=图高)后求交集
                    //       → 只保留列方向上与顶盖重叠的膜 → 这就是位于顶盖正下方的"隔膜"
                    // ============================================================

                    // 3.1 提取全部蓝膜（灰度120）
                    ho_Region2.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region2, 120, 120);

                    // 3.2 热熔点膨胀成(500行×100列)的矩形 → 从蓝膜中减去
                    //     行方向膨胀很大是为了确保热熔点上下附近的膜全部排除
                    ho_RegionDilation.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region3, out ho_RegionDilation, 100, 500);

                    ho_RegionDifference.Dispose();
                    HOperatorSet.Difference(ho_Region2, ho_RegionDilation, out ho_RegionDifference);

                    // 3.3 计算差集区域的高度，只保留高度 > 最大高度/3 的连通域
                    hv_Value.Dispose();
                    HOperatorSet.RegionFeatures(ho_RegionDifference, "height", out hv_Value);
                    ho_Region2.Dispose();
                    HOperatorSet.Connection(ho_RegionDifference, out ho_Region2);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HObject tmp;
                        HOperatorSet.SelectShape(ho_Region2, out tmp, "height", "and", hv_Value / 3, "max");
                        ho_Region2.Dispose();
                        ho_Region2 = tmp;
                    }

                    // 3.4 顶盖膨胀成全图高度竖带(半宽1行×半高=图像高度) → 与过滤后蓝膜求交集
                    //     结果：只有列方向上与顶盖有重叠的膜被保留 → 这才是"隔膜"
                    hv_Width.Dispose();
                    hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    ho_RegionDilation1.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region1, out ho_RegionDilation1, 1, hv_Height);
                    {
                        HObject tmp;
                        HOperatorSet.Intersection(ho_Region2, ho_RegionDilation1, out tmp);
                        ho_Region2.Dispose();
                        ho_Region2 = tmp;
                    }

                    // ============================================================
                    // 第四步：根据位置模式(Left/Right/Center)选择目标隔膜，并获取列坐标
                    //
                    // 每种模式完成以下工作：
                    //   ① 筛选目标隔膜 → 更新ho_Region2
                    //   ② 生成隔膜外接矩形(旋转矩形)用于UI显示 → ho_MembraneRegion
                    //   ③ 记录隔膜外接矩形顶边中点 → hv_MemTopRow/Col
                    //   ④ 获取列坐标范围 → hv_LeftCol1/Col2, hv_RightCol1/Col2
                    // ============================================================

                    if ((int)(new HTuple(hv_Location.TupleEqual("Left"))) != 0)
                    {
                        // ===== Left模式：取最左侧的一个隔膜 =====
                        {
                            // 按各区域首点列坐标升序排列
                            HObject sorted;
                            HOperatorSet.SortRegion(ho_Region2, out sorted, "first_point", "true", "column");
                            // 取排序后的第1个 → 最左侧隔膜
                            HObject tmp;
                            HOperatorSet.SelectObj(sorted, out tmp, 1);
                            sorted.Dispose();
                            ho_Region2.Dispose();
                            ho_Region2 = tmp;
                        }

                        // 生成隔膜的最小外接旋转矩形，用于UI显示
                        {
                            HTuple r, c, p, l1, l2;
                            HOperatorSet.SmallestRectangle2(ho_Region2, out r, out c, out p, out l1, out l2);
                            ho_MembraneRegion.Dispose();
                            HOperatorSet.GenRectangle2(out ho_MembraneRegion, r, c, p, l1, l2);
                            // 顶边中点行 = 旋转矩形中心行 − 半高（矩形最上边）
                            hv_MemTopRow.Dispose();
                            hv_MemTopRow = r - l2;
                            // 顶边中点列 = 旋转矩形中心列
                            hv_MemTopCol.Dispose();
                            hv_MemTopCol = c.Clone();
                            r.Dispose(); c.Dispose(); p.Dispose(); l1.Dispose(); l2.Dispose();
                        }

                        // 获取左蓝膜的列坐标范围 → 后续用于切割左顶盖
                        // column1=区域所有像素的最左列, column2=区域所有像素的最右列（不做外接矩形）
                        hv_LeftCol1.Dispose();
                        hv_LeftCol2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column1", out hv_LeftCol1);
                        HOperatorSet.RegionFeatures(ho_Region2, "column2", out hv_LeftCol2);
                    }
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Right"))) != 0)
                    {
                        // ===== Right模式：取最右侧的一个隔膜（降序排列） =====
                        {
                            HObject sorted;
                            HOperatorSet.SortRegion(ho_Region2, out sorted, "first_point", "false", "column");
                            HObject tmp;
                            HOperatorSet.SelectObj(sorted, out tmp, 1);
                            sorted.Dispose();
                            ho_Region2.Dispose();
                            ho_Region2 = tmp;
                        }

                        {
                            HTuple r, c, p, l1, l2;
                            HOperatorSet.SmallestRectangle2(ho_Region2, out r, out c, out p, out l1, out l2);
                            ho_MembraneRegion.Dispose();
                            HOperatorSet.GenRectangle2(out ho_MembraneRegion, r, c, p, l1, l2);
                            hv_MemTopRow.Dispose();
                            hv_MemTopRow = r - l2;
                            hv_MemTopCol.Dispose();
                            hv_MemTopCol = c.Clone();
                            r.Dispose(); c.Dispose(); p.Dispose(); l1.Dispose(); l2.Dispose();
                        }

                        hv_RightCol1.Dispose();
                        hv_RightCol2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column1", out hv_RightCol1);
                        HOperatorSet.RegionFeatures(ho_Region2, "column2", out hv_RightCol2);
                    }
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                    {
                        // ===== Center模式：保留面积>1500的隔膜（通常是左右两个） =====
                        {
                            HObject tmp;
                            HOperatorSet.SelectShape(ho_Region2, out tmp, "area", "and", 1500, "max");
                            ho_Region2.Dispose();
                            ho_Region2 = tmp;
                        }

                        {
                            HTuple r, c, p, l1, l2;
                            HOperatorSet.SmallestRectangle2(ho_Region2, out r, out c, out p, out l1, out l2);
                            ho_MembraneRegion.Dispose();
                            HOperatorSet.GenRectangle2(out ho_MembraneRegion, r, c, p, l1, l2);
                            hv_MemTopRow.Dispose();
                            hv_MemTopRow = r - l2;
                            hv_MemTopCol.Dispose();
                            hv_MemTopCol = c.Clone();
                            r.Dispose(); c.Dispose(); p.Dispose(); l1.Dispose(); l2.Dispose();
                        }

                        // 将两个隔膜分离为左蓝膜和右蓝膜，分别获取列坐标
                        {
                            HTuple num;
                            HOperatorSet.CountObj(ho_Region2, out num);
                            if ((int)(new HTuple(num.TupleGreaterEqual(2))) != 0)
                            {
                                // 按列坐标升序排列
                                ho_Sorted.Dispose();
                                HOperatorSet.SortRegion(ho_Region2, out ho_Sorted, "first_point", "true", "column");

                                // 取第1个 → 左蓝膜
                                ho_LeftMembrane.Dispose();
                                HOperatorSet.SelectObj(ho_Sorted, out ho_LeftMembrane, 1);

                                // 取最后一个 → 右蓝膜
                                ho_RightMembrane.Dispose();
                                HOperatorSet.SelectObj(ho_Sorted, out ho_RightMembrane, num);

                                // 左蓝膜列坐标
                                hv_LeftCol1.Dispose();
                                hv_LeftCol2.Dispose();
                                HOperatorSet.RegionFeatures(ho_LeftMembrane, "column1", out hv_LeftCol1);
                                HOperatorSet.RegionFeatures(ho_LeftMembrane, "column2", out hv_LeftCol2);

                                // 右蓝膜列坐标
                                hv_RightCol1.Dispose();
                                hv_RightCol2.Dispose();
                                HOperatorSet.RegionFeatures(ho_RightMembrane, "column1", out hv_RightCol1);
                                HOperatorSet.RegionFeatures(ho_RightMembrane, "column2", out hv_RightCol2);

                                num.Dispose();
                            }
                        }
                    }

                    // ============================================================
                    // 第五步：热熔点取最大面积 + 补充缺失侧列坐标 + 切割顶盖为三个子区域
                    //
                    // 切割策略：
                    //   用左蓝膜/热熔点/右蓝膜的列坐标范围，生成贯穿全图高的矩形，
                    //   与ho_Region1求交集 → 得到左/中/右三个顶盖子区域。
                    //   Left模式缺少右蓝膜 → 右顶盖列范围 = (midCol2+1, width-1)
                    //   Right模式缺少左蓝膜 → 左顶盖列范围 = (0, midCol1-1)
                    // ============================================================

                    // 5.1 热熔点取最大面积连通域 → 从此ho_Region3就是最终的热熔点了
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region3, out ho_ConnectedRegions);
                    ho_Region3.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region3, "max_area", 70);

                    // 5.2 获取热熔点列坐标范围（直接用column1/column2，不做外接矩形）
                    hv_MidCol1.Dispose();
                    hv_MidCol2.Dispose();
                    HOperatorSet.RegionFeatures(ho_Region3, "column1", out hv_MidCol1);
                    HOperatorSet.RegionFeatures(ho_Region3, "column2", out hv_MidCol2);

                    // 5.3 补充缺失侧的列坐标
                    if ((int)(new HTuple(hv_Location.TupleEqual("Left"))) != 0)
                    {
                        // Left模式无右蓝膜 → 右顶盖从热熔点右列+1到图像右边缘-1
                        hv_RightCol1.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_RightCol1 = hv_MidCol2 + 1;
                        }
                        hv_RightCol2.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_RightCol2 = hv_Width - 1;
                        }
                    }
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Right"))) != 0)
                    {
                        // Right模式无左蓝膜 → 左顶盖从图像左边缘(0)到热熔点左列-1
                        hv_LeftCol1.Dispose();
                        hv_LeftCol1 = 0;
                        hv_LeftCol2.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_LeftCol2 = hv_MidCol1 - 1;
                        }
                    }

                    // 5.4 用列坐标范围切割顶盖

                    // 左顶盖：矩形(0行, 左蓝膜最左列) → (Height-1行, 左蓝膜最右列) 与 ho_Region1 求交集
                    ho_RectCutLeft.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenRectangle1(out ho_RectCutLeft, 0, hv_LeftCol1, hv_Height - 1, hv_LeftCol2);
                    }
                    ho_RegionLeftTop.Dispose();
                    HOperatorSet.Intersection(ho_Region1, ho_RectCutLeft, out ho_RegionLeftTop);

                    // 中顶盖：矩形(0行, 热熔点最左列) → (Height-1行, 热熔点最右列) 与 ho_Region1 求交集
                    ho_RectCutMid.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenRectangle1(out ho_RectCutMid, 0, hv_MidCol1, hv_Height - 1, hv_MidCol2);
                    }
                    ho_RegionMidTop.Dispose();
                    HOperatorSet.Intersection(ho_Region1, ho_RectCutMid, out ho_RegionMidTop);

                    // 右顶盖：矩形(0行, 右蓝膜最左列) → (Height-1行, 右蓝膜最右列) 与 ho_Region1 求交集
                    ho_RectCutRight.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenRectangle1(out ho_RectCutRight, 0, hv_RightCol1, hv_Height - 1, hv_RightCol2);
                    }
                    ho_RegionRightTop.Dispose();
                    HOperatorSet.Intersection(ho_Region1, ho_RectCutRight, out ho_RegionRightTop);

                    // ============================================================
                    // 第六步：分别拟合三个顶盖区域的下边缘直线
                    //
                    // 方法：FitBottomEdge(子区域)
                    //   ① 取面积最大的连通域（多碎片时）
                    //   ② GenContourRegionXld("border") → 提取边界XLD轮廓
                    //   ③ 轮廓多条时取面积最大的那条
                    //   ④ GetContourXld → 拿所有轮廓点
                    //   ⑤ 逐列取底边：每列保留行坐标最大的轮廓点 → 精确下边缘
                    //   ⑥ 手算最小二乘 r = a*c + b → 直线方程
                    //   ⑦ 端点延伸到完整列范围 → 直线覆盖整个下边缘宽度
                    //
                    // fallback：子区域面积=0时，用顶盖整体外接矩形底边Y坐标 +
                    //          对应列范围生成一条水平线
                    // ============================================================

                    // 6.0 先算顶盖整体外接矩形，得到底边Y坐标 = hv_Row + hv_Length2，供空区域fallback用
                    hv_Row.Dispose();
                    hv_Phi.Dispose();
                    hv_Length1.Dispose();
                    hv_Length2.Dispose();
                    HTuple hv_Column;
                    HOperatorSet.SmallestRectangle2(ho_Region1, out hv_Row, out hv_Column,
                        out hv_Phi, out hv_Length1, out hv_Length2);
                    hv_Column.Dispose();

                    // 6.1 左顶盖下边缘拟合
                    {
                        HTuple a;
                        HOperatorSet.AreaCenter(ho_RegionLeftTop, out a, out hv_BotCenterRow, out hv_BotCenterCol);
                        if ((int)(new HTuple(a.TupleGreater(0))) != 0)
                        {
                            // 区域非空 → FitBottomEdge提取下边缘+拟合
                            FitBottomEdge(ho_RegionLeftTop, out hv_BotCenterRow, out hv_BotCenterCol,
                                out hv_LeftRb, out hv_LeftCb, out hv_LeftRe, out hv_LeftCe);
                        }
                        else
                        {
                            // 区域为空 → fallback水平线：底边Y坐标，列范围[leftCol1, leftCol2]
                            hv_LeftRb.Dispose();
                            hv_LeftCb.Dispose();
                            hv_LeftRe.Dispose();
                            hv_LeftCe.Dispose();
                            hv_LeftRb = hv_Row + hv_Length2;
                            hv_LeftCb = hv_LeftCol1;
                            hv_LeftRe = hv_Row + hv_Length2;
                            hv_LeftCe = hv_LeftCol2;
                        }
                        a.Dispose();
                    }

                    // 6.2 中顶盖下边缘拟合（同6.1）
                    {
                        HTuple a;
                        HOperatorSet.AreaCenter(ho_RegionMidTop, out a, out hv_BotCenterRow, out hv_BotCenterCol);
                        if ((int)(new HTuple(a.TupleGreater(0))) != 0)
                        {
                            FitBottomEdge(ho_RegionMidTop, out hv_BotCenterRow, out hv_BotCenterCol,
                                out hv_MidRb, out hv_MidCb, out hv_MidRe, out hv_MidCe);
                        }
                        else
                        {
                            hv_MidRb.Dispose();
                            hv_MidCb.Dispose();
                            hv_MidRe.Dispose();
                            hv_MidCe.Dispose();
                            hv_MidRb = hv_Row + hv_Length2;
                            hv_MidCb = hv_MidCol1;
                            hv_MidRe = hv_Row + hv_Length2;
                            hv_MidCe = hv_MidCol2;
                        }
                        a.Dispose();
                    }

                    // 6.3 右顶盖下边缘拟合（同6.1）
                    {
                        HTuple a;
                        HOperatorSet.AreaCenter(ho_RegionRightTop, out a, out hv_BotCenterRow, out hv_BotCenterCol);
                        if ((int)(new HTuple(a.TupleGreater(0))) != 0)
                        {
                            FitBottomEdge(ho_RegionRightTop, out hv_BotCenterRow, out hv_BotCenterCol,
                                out hv_RightRb, out hv_RightCb, out hv_RightRe, out hv_RightCe);
                        }
                        else
                        {
                            hv_RightRb.Dispose();
                            hv_RightCb.Dispose();
                            hv_RightRe.Dispose();
                            hv_RightCe.Dispose();
                            hv_RightRb = hv_Row + hv_Length2;
                            hv_RightCb = hv_RightCol1;
                            hv_RightRe = hv_Row + hv_Length2;
                            hv_RightCe = hv_RightCol2;
                        }
                        a.Dispose();
                    }

                    // 6.4 将拟合出的三条直线参数转为XLD轮廓 → 蓝色显示用
                    //     GenContourPolygonXld(起点, 终点) → 画一条直线段
                    ho_LineLeft.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenContourPolygonXld(out ho_LineLeft,
                            hv_LeftRb.TupleConcat(hv_LeftRe), hv_LeftCb.TupleConcat(hv_LeftCe));
                    }
                    ho_LineMid.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenContourPolygonXld(out ho_LineMid,
                            hv_MidRb.TupleConcat(hv_MidRe), hv_MidCb.TupleConcat(hv_MidCe));
                    }
                    ho_LineRight.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.GenContourPolygonXld(out ho_LineRight,
                            hv_RightRb.TupleConcat(hv_RightRe), hv_RightCb.TupleConcat(hv_RightCe));
                    }

                    // 6.5 红色连接线：左线右端点 → 中线左端点 → 中线右端点 → 右线左端点
                    //     先判断每条线的左右端点（按列坐标大小区分），再拼接成一条折线
                    {
                        HTuple cRow = new HTuple();
                        HTuple cCol = new HTuple();

                        // 左顶盖线：比较起点列和终点列，确定左端点和右端点
                        HTuple lLr, lLc, lRr, lRc;
                        if ((int)(new HTuple(hv_LeftCb.TupleLessEqual(hv_LeftCe))) != 0)
                        {
                            lLr = hv_LeftRb; lLc = hv_LeftCb;   // 左端点（列小）
                            lRr = hv_LeftRe; lRc = hv_LeftCe;   // 右端点（列大）
                        }
                        else
                        {
                            lLr = hv_LeftRe; lLc = hv_LeftCe;
                            lRr = hv_LeftRb; lRc = hv_LeftCb;
                        }

                        // 中顶盖线：判断左右端点
                        HTuple mLr, mLc, mRr, mRc;
                        if ((int)(new HTuple(hv_MidCb.TupleLessEqual(hv_MidCe))) != 0)
                        {
                            mLr = hv_MidRb; mLc = hv_MidCb;
                            mRr = hv_MidRe; mRc = hv_MidCe;
                        }
                        else
                        {
                            mLr = hv_MidRe; mLc = hv_MidCe;
                            mRr = hv_MidRb; mRc = hv_MidCb;
                        }

                        // 右顶盖线：判断左右端点
                        HTuple rLr, rLc, rRr, rRc;
                        if ((int)(new HTuple(hv_RightCb.TupleLessEqual(hv_RightCe))) != 0)
                        {
                            rLr = hv_RightRb; rLc = hv_RightCb;
                            rRr = hv_RightRe; rRc = hv_RightCe;
                        }
                        else
                        {
                            rLr = hv_RightRe; rLc = hv_RightCe;
                            rRr = hv_RightRb; rRc = hv_RightCb;
                        }

                        // 拼接：左线左端→左线右端→中线左端→中线右端→右线左端→右线右端（完整串起三条线）
                        cRow = cRow.TupleConcat(lLr).TupleConcat(lRr).TupleConcat(mLr).TupleConcat(mRr).TupleConcat(rLr).TupleConcat(rRr);
                        cCol = cCol.TupleConcat(lLc).TupleConcat(lRc).TupleConcat(mLc).TupleConcat(mRc).TupleConcat(rLc).TupleConcat(rRc);

                        ho_ConnectLine.Dispose();
                        HOperatorSet.GenContourPolygonXld(out ho_ConnectLine, cRow, cCol);

                        lLr.Dispose(); lLc.Dispose(); lRr.Dispose(); lRc.Dispose();
                        mLr.Dispose(); mLc.Dispose(); mRr.Dispose(); mRc.Dispose();
                        rLr.Dispose(); rLc.Dispose(); rRr.Dispose(); rRc.Dispose();
                        cRow.Dispose(); cCol.Dispose();
                    }

                    // ============================================================
                    // 第七步：计算热熔点中心 → 中顶盖线的垂直距离
                    //
                    // 方法：
                    //   ① AreaCenter(ho_Region3) → 热熔点几何中心(Row4, Col3)
                    //   ② 从热熔点中心向下画竖直线(行0→Height, 列Col3)
                    //      与中顶盖线求交点 → (RowOver, ColOver)
                    //   ③ DistancePl(热点中心, 中顶盖线) → 垂直像素距离
                    // ============================================================

                    // ⑦-1: 热熔点几何中心坐标
                    hv_Area.Dispose();
                    hv_Row4.Dispose();
                    hv_Column3.Dispose();
                    HOperatorSet.AreaCenter(ho_Region3, out hv_Area, out hv_Row4, out hv_Column3);

                    // ⑦-2: 竖直线(0,Col3)→(Height,Col3) 与 中顶盖线 的交点
                    hv_RowOver.Dispose();
                    hv_ColumnOver.Dispose();
                    hv_IsOverlapping.Dispose();
                    HOperatorSet.IntersectionLines(0, hv_Column3, hv_Height, hv_Column3,
                        hv_MidRb, hv_MidCb, hv_MidRe, hv_MidCe,
                        out hv_RowOver, out hv_ColumnOver, out hv_IsOverlapping);

                    // ⑦-3: 热熔点中心→交点的箭头（蓝色可视化）
                    ho_RongDianArrow.Dispose();
                    gen_arrow_contour_xld(out ho_RongDianArrow,
                        hv_Row4, hv_Column3, hv_RowOver, hv_ColumnOver, 5, 5);

                    // ⑦-4: 点到直线的垂直距离（像素值，调用方乘以SS转为mm）
                    hv_DistanceRongDian.Dispose();
                    HOperatorSet.DistancePl(hv_Row4, hv_Column3,
                        hv_MidRb, hv_MidCb, hv_MidRe, hv_MidCe,
                        out hv_DistanceRongDian);

                    // ============================================================
                    // 第八步：计算膜上边缘中点 → 对应顶盖线的垂直距离
                    //
                    // 方法：
                    //   ① FitTopEdge(膜区域) → (TopRow, TopCol) 膜上边缘拟合直线中心点
                    //   ② 从该点向下画竖直线 → 与对应顶盖线求交点
                    //   ③ DistancePl(膜顶点, 顶盖线) → 垂直距离
                    //
                    // 对应关系：
                    //   8.1 左蓝膜 → 左顶盖线（Left/Center模式）
                    //   8.2 右蓝膜 → 右顶盖线（Right/Center模式）
                    // ============================================================
                    {
                        HObject tmpArrow;
                        HTuple topRow, topCol, ro, co, iso, d2;

                        // 初始化可视化对象
                        ho_TopCoverArrow.Dispose();
                        HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
                        ho_DistanceLine.Dispose();
                        HOperatorSet.GenEmptyObj(out ho_DistanceLine);
                        ho_DistanceScale.Dispose();
                        HOperatorSet.GenEmptyObj(out ho_DistanceScale);
                        hv_DistanceTopCover.Dispose();
                        hv_DistanceTopCover = new HTuple();

                        // ========== 8.1 左蓝膜 → 左顶盖线 ==========
                        if ((int)(new HTuple(hv_Location.TupleEqual("Left"))) != 0 ||
                            (int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                        {
                            // Center模式用分离出的ho_LeftMembrane，Left模式用ho_Region2（已是最左）
                            HObject mem = ((int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                                ? ho_LeftMembrane : ho_Region2;

                            if (mem != null && mem.IsInitialized())
                            {
                                // 8.1-①: FitTopEdge → 膜的上边缘精确中点
                                FitTopEdge(mem, out topRow, out topCol);

                                // 8.1-②: 从中点向下画竖直线 → 与左顶盖线的交点
                                HOperatorSet.IntersectionLines(0, topCol, hv_Height, topCol,
                                    hv_LeftRb, hv_LeftCb, hv_LeftRe, hv_LeftCe,
                                    out ro, out co, out iso);

                                // 8.1-③: 箭头：膜顶点 → 交点
                                gen_arrow_contour_xld(out tmpArrow, topRow, topCol, ro, co, 5, 5);
                                {
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_TopCoverArrow, tmpArrow, out x);
                                    ho_TopCoverArrow.Dispose();
                                    ho_TopCoverArrow = x;
                                }

                                // 8.1-④: 距离线段：膜顶点 → 交点（红色竖线）
                                {
                                    HObject l;
                                    HOperatorSet.GenContourPolygonXld(out l,
                                        topRow.TupleConcat(ro), topCol.TupleConcat(co));
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_DistanceLine, l, out x);
                                    ho_DistanceLine.Dispose();
                                    ho_DistanceLine = x;
                                    l.Dispose();
                                }

                                // 8.1-⑤: DistancePl → 膜顶点到左顶盖线的垂直像素距离
                                HOperatorSet.DistancePl(topRow, topCol,
                                    hv_LeftRb, hv_LeftCb, hv_LeftRe, hv_LeftCe, out d2);

                                // 8.1-⑥: 水平标尺线（以膜顶点为中心，半长为距离/2）
                                {
                                    HObject s;
                                    HOperatorSet.GenContourPolygonXld(out s,
                                        topRow.TupleConcat(topRow),
                                        (topCol - (d2 / 2)).TupleConcat(topCol + (d2 / 2)));
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_DistanceScale, s, out x);
                                    ho_DistanceScale.Dispose();
                                    ho_DistanceScale = x;
                                    s.Dispose();
                                }

                                // 8.1-⑦: 追加距离值到数组
                                hv_DistanceTopCover = hv_DistanceTopCover.TupleConcat(d2);

                                tmpArrow.Dispose();
                                topRow.Dispose(); topCol.Dispose();
                                ro.Dispose(); co.Dispose(); iso.Dispose();
                                d2.Dispose();
                            }
                        }

                        // ========== 8.2 右蓝膜 → 右顶盖线 ==========
                        if ((int)(new HTuple(hv_Location.TupleEqual("Right"))) != 0 ||
                            (int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                        {
                            // Center模式用ho_RightMembrane，Right模式用ho_Region2
                            HObject mem = ((int)(new HTuple(hv_Location.TupleEqual("Center"))) != 0)
                                ? ho_RightMembrane : ho_Region2;

                            if (mem != null && mem.IsInitialized())
                            {
                                // 8.2-①: FitTopEdge → 膜的上边缘精确中点
                                FitTopEdge(mem, out topRow, out topCol);

                                // 8.2-②: 从中点向下画竖直线 → 与右顶盖线的交点
                                HOperatorSet.IntersectionLines(0, topCol, hv_Height, topCol,
                                    hv_RightRb, hv_RightCb, hv_RightRe, hv_RightCe,
                                    out ro, out co, out iso);

                                // 8.2-③: 箭头：膜顶点 → 交点
                                gen_arrow_contour_xld(out tmpArrow, topRow, topCol, ro, co, 5, 5);
                                {
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_TopCoverArrow, tmpArrow, out x);
                                    ho_TopCoverArrow.Dispose();
                                    ho_TopCoverArrow = x;
                                }

                                // 8.2-④: 距离线段
                                {
                                    HObject l;
                                    HOperatorSet.GenContourPolygonXld(out l,
                                        topRow.TupleConcat(ro), topCol.TupleConcat(co));
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_DistanceLine, l, out x);
                                    ho_DistanceLine.Dispose();
                                    ho_DistanceLine = x;
                                    l.Dispose();
                                }

                                // 8.2-⑤: DistancePl → 膜顶点到右顶盖线的垂直像素距离
                                HOperatorSet.DistancePl(topRow, topCol,
                                    hv_RightRb, hv_RightCb, hv_RightRe, hv_RightCe, out d2);

                                // 8.2-⑥: 水平标尺线
                                {
                                    HObject s;
                                    HOperatorSet.GenContourPolygonXld(out s,
                                        topRow.TupleConcat(topRow),
                                        (topCol - (d2 / 2)).TupleConcat(topCol + (d2 / 2)));
                                    HObject x;
                                    HOperatorSet.ConcatObj(ho_DistanceScale, s, out x);
                                    ho_DistanceScale.Dispose();
                                    ho_DistanceScale = x;
                                    s.Dispose();
                                }

                                // 8.2-⑦: 追加距离值
                                hv_DistanceTopCover = hv_DistanceTopCover.TupleConcat(d2);

                                tmpArrow.Dispose();
                                topRow.Dispose(); topCol.Dispose();
                                ro.Dispose(); co.Dispose(); iso.Dispose();
                                d2.Dispose();
                            }
                        }
                    }
                }
                catch (HalconException ex)
                {
                    // 内层catch：计算异常时将所有输出重置为安全空值/0
                    Logger.AddLog("包膜算法模块计算距离异常", eMsgType.Error);
                    ex.ToHTuple(out hv_Exception);
                    ho_LineLeft.Dispose(); HOperatorSet.GenEmptyObj(out ho_LineLeft);
                    ho_LineMid.Dispose(); HOperatorSet.GenEmptyObj(out ho_LineMid);
                    ho_LineRight.Dispose(); HOperatorSet.GenEmptyObj(out ho_LineRight);
                    ho_ConnectLine.Dispose(); HOperatorSet.GenEmptyObj(out ho_ConnectLine);
                    ho_RegionLeftTop.Dispose(); HOperatorSet.GenEmptyObj(out ho_RegionLeftTop);
                    ho_RegionMidTop.Dispose(); HOperatorSet.GenEmptyObj(out ho_RegionMidTop);
                    ho_RegionRightTop.Dispose(); HOperatorSet.GenEmptyObj(out ho_RegionRightTop);
                    hv_DistanceRongDian.Dispose(); hv_DistanceRongDian = 0;
                    ho_TopCoverArrow.Dispose(); HOperatorSet.GenEmptyObj(out ho_TopCoverArrow);
                    ho_RongDianArrow.Dispose(); HOperatorSet.GenEmptyObj(out ho_RongDianArrow);
                    ho_DistanceLine.Dispose(); HOperatorSet.GenEmptyObj(out ho_DistanceLine);
                    ho_DistanceScale.Dispose(); HOperatorSet.GenEmptyObj(out ho_DistanceScale);
                    ho_MembraneRegion.Dispose(); HOperatorSet.GenEmptyObj(out ho_MembraneRegion);
                    hv_DistanceTopCover.Dispose(); hv_DistanceTopCover = new HTuple();
                    hv_MemTopRow.Dispose(); hv_MemTopRow = new HTuple();
                    hv_MemTopCol.Dispose(); hv_MemTopCol = new HTuple();
                }

                // ========== 释放所有局部HObject和HTuple对象 ==========
                ho_Region1.Dispose(); ho_Region3.Dispose(); ho_ConnectedRegions.Dispose();
                ho_Region2.Dispose(); ho_RegionDilation.Dispose(); ho_RegionDifference.Dispose();
                ho_RegionDilation1.Dispose();
                ho_Sorted.Dispose(); ho_LeftMembrane.Dispose(); ho_RightMembrane.Dispose();
                ho_RectCutLeft.Dispose(); ho_RectCutMid.Dispose(); ho_RectCutRight.Dispose();
                hv_Value.Dispose(); hv_Width.Dispose(); hv_Height.Dispose();
                hv_Row.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                hv_Area.Dispose(); hv_Row4.Dispose(); hv_Column3.Dispose();
                hv_RowOver.Dispose(); hv_ColumnOver.Dispose(); hv_IsOverlapping.Dispose();
                hv_Exception.Dispose();
                hv_LeftCol1.Dispose(); hv_LeftCol2.Dispose();
                hv_MidCol1.Dispose(); hv_MidCol2.Dispose();
                hv_RightCol1.Dispose(); hv_RightCol2.Dispose();
                hv_LeftRb.Dispose(); hv_LeftCb.Dispose(); hv_LeftRe.Dispose(); hv_LeftCe.Dispose();
                hv_MidRb.Dispose(); hv_MidCb.Dispose(); hv_MidRe.Dispose(); hv_MidCe.Dispose();
                hv_RightRb.Dispose(); hv_RightCb.Dispose(); hv_RightRe.Dispose(); hv_RightCe.Dispose();
                hv_BotCenterRow.Dispose(); hv_BotCenterCol.Dispose();
                return;
            }
            catch (HalconException ex)
            {
                // 外层catch：最终安全保障，释放所有可能持有的资源后重新抛出异常
                ho_Region1.Dispose(); ho_Region3.Dispose(); ho_ConnectedRegions.Dispose();
                ho_Region2.Dispose(); ho_RegionDilation.Dispose(); ho_RegionDifference.Dispose();
                ho_RegionDilation1.Dispose();
                ho_TopCoverArrow.Dispose(); ho_RongDianArrow.Dispose();
                ho_DistanceLine.Dispose(); ho_DistanceScale.Dispose(); ho_MembraneRegion.Dispose();
                ho_LineLeft.Dispose(); ho_LineMid.Dispose(); ho_LineRight.Dispose();
                ho_ConnectLine.Dispose();
                ho_RegionLeftTop.Dispose(); ho_RegionMidTop.Dispose(); ho_RegionRightTop.Dispose();
                ho_Sorted.Dispose(); ho_LeftMembrane.Dispose(); ho_RightMembrane.Dispose();
                ho_RectCutLeft.Dispose(); ho_RectCutMid.Dispose(); ho_RectCutRight.Dispose();
                hv_Value.Dispose(); hv_Width.Dispose(); hv_Height.Dispose();
                hv_Row.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                hv_Area.Dispose(); hv_Row4.Dispose(); hv_Column3.Dispose();
                hv_RowOver.Dispose(); hv_ColumnOver.Dispose(); hv_IsOverlapping.Dispose();
                hv_Exception.Dispose(); hv_DistanceTopCover.Dispose();
                hv_MemTopRow.Dispose(); hv_MemTopCol.Dispose();
                hv_LeftCol1.Dispose(); hv_LeftCol2.Dispose();
                hv_MidCol1.Dispose(); hv_MidCol2.Dispose();
                hv_RightCol1.Dispose(); hv_RightCol2.Dispose();
                hv_LeftRb.Dispose(); hv_LeftCb.Dispose(); hv_LeftRe.Dispose(); hv_LeftCe.Dispose();
                hv_MidRb.Dispose(); hv_MidCb.Dispose(); hv_MidRe.Dispose(); hv_MidCe.Dispose();
                hv_RightRb.Dispose(); hv_RightCb.Dispose(); hv_RightRe.Dispose(); hv_RightCe.Dispose();
                hv_BotCenterRow.Dispose(); hv_BotCenterCol.Dispose();
                throw ex;
            }
        }

        // ========== FitTopEdge / FitBottomEdge ==========

        public static void FitTopEdge(HObject ho_Region, out HTuple hv_CenterRow, out HTuple hv_CenterCol)
        {
            // 区域多连通域时只取面积最大的
            {
                HTuple numObj;
                HOperatorSet.CountObj(ho_Region, out numObj);
                if (numObj.I == 0)
                {
                    hv_CenterRow = 0;
                    hv_CenterCol = 0;
                    numObj.Dispose();
                    return;
                }
                if (numObj.I > 1)
                {
                    HObject tmp;
                    HOperatorSet.SelectShapeStd(ho_Region, out tmp, "max_area", 70);
                    ho_Region.Dispose();
                    ho_Region = tmp;
                }
                numObj.Dispose();
            }

            // 生成区域边界XLD轮廓
            HObject contour;
            HOperatorSet.GenContourRegionXld(ho_Region, out contour, "border");

            // 轮廓有多条时取面积最大的（孔洞等）
            {
                HTuple numObj;
                HOperatorSet.CountObj(contour, out numObj);
                if (numObj.I > 1)
                {
                    HObject best = null;
                    HTuple maxArea = new HTuple(0);
                    for (int i = 1; i <= numObj.I; i++)
                    {
                        HObject cur;
                        HOperatorSet.SelectObj(contour, out cur, i);
                        HObject region;
                        HOperatorSet.GenRegionContourXld(cur, out region, "filled");
                        HTuple area;
                        HOperatorSet.AreaCenter(region, out area, out _, out _);
                        if (area.D > maxArea.D)
                        {
                            if (best != null)
                            {
                                best.Dispose();
                            }
                            best = cur;
                            maxArea.Dispose();
                            maxArea = area.Clone();
                        }
                        else
                        {
                            cur.Dispose();
                        }
                        region.Dispose();
                        area.Dispose();
                    }
                    contour.Dispose();
                    contour = best;
                    maxArea.Dispose();
                }
                numObj.Dispose();
            }

            // 拿所有轮廓点的坐标
            HTuple rows, cols;
            HOperatorSet.GetContourXld(contour, out rows, out cols);
            contour.Dispose();

            // 逐列取顶边：每列只保留行坐标最小的点 → 精确上边缘
            var colToRow = new Dictionary<int, double>();
            for (int i = 0; i < rows.Length; i++)
            {
                int c = (int)Math.Round(cols[i].D);
                double r = rows[i].D;
                if (!colToRow.ContainsKey(c) || r < colToRow[c])
                {
                    colToRow[c] = r;
                }
            }
            int minCol = int.MaxValue;
            int maxCol = int.MinValue;
            foreach (int c in colToRow.Keys)
            {
                if (c < minCol) minCol = c;
                if (c > maxCol) maxCol = c;
            }
            HTuple topRows = new HTuple();
            HTuple topCols = new HTuple();
            for (int c = minCol; c <= maxCol; c++)
            {
                if (colToRow.TryGetValue(c, out double r))
                {
                    topRows = topRows.TupleConcat(r);
                    topCols = topCols.TupleConcat(c);
                }
            }

            // 手算最小二乘：r = a*c + b，加除零保护
            int n = topRows.Length;
            double sumR = 0, sumC = 0, sumCR = 0, sumC2 = 0;
            for (int i = 0; i < n; i++)
            {
                double r = topRows[i].D;
                double c = topCols[i].D;
                sumR += r;
                sumC += c;
                sumCR += c * r;
                sumC2 += c * c;
            }
            double denom = n * sumC2 - sumC * sumC;
            double a, b;
            if (Math.Abs(denom) < 1e-10)
            {
                // 所有点在同一列，无法拟合斜线 → fallback水平线
                a = 0;
                b = sumR / n;
            }
            else
            {
                a = (n * sumCR - sumC * sumR) / denom;
                b = (sumR - a * sumC) / n;
            }

            // 取列范围中点作为拟合直线的中心点
            hv_CenterRow = a * ((minCol + maxCol) / 2.0) + b;
            hv_CenterCol = (minCol + maxCol) / 2.0;

            rows.Dispose();
            cols.Dispose();
            topRows.Dispose();
            topCols.Dispose();
        }

        public static void FitBottomEdge(HObject ho_Region, out HTuple hv_CenterRow, out HTuple hv_CenterCol,
            out HTuple hv_RowBegin, out HTuple hv_ColBegin, out HTuple hv_RowEnd, out HTuple hv_ColEnd)
        {
            // 区域多连通域时只取面积最大的
            {
                HTuple numObj;
                HOperatorSet.CountObj(ho_Region, out numObj);
                if (numObj.I == 0)
                {
                    hv_CenterRow = 0;
                    hv_CenterCol = 0;
                    hv_RowBegin = 0;
                    hv_ColBegin = 0;
                    hv_RowEnd = 0;
                    hv_ColEnd = 0;
                    numObj.Dispose();
                    return;
                }
                if (numObj.I > 1)
                {
                    HObject tmp;
                    HOperatorSet.SelectShapeStd(ho_Region, out tmp, "max_area", 70);
                    ho_Region.Dispose();
                    ho_Region = tmp;
                }
                numObj.Dispose();
            }

            // 生成区域边界XLD轮廓
            HObject contour;
            HOperatorSet.GenContourRegionXld(ho_Region, out contour, "border");

            // 轮廓有多条时取面积最大的
            {
                HTuple numObj;
                HOperatorSet.CountObj(contour, out numObj);
                if (numObj.I > 1)
                {
                    HObject best = null;
                    HTuple maxArea = new HTuple(0);
                    for (int i = 1; i <= numObj.I; i++)
                    {
                        HObject cur;
                        HOperatorSet.SelectObj(contour, out cur, i);
                        HObject region;
                        HOperatorSet.GenRegionContourXld(cur, out region, "filled");
                        HTuple area;
                        HOperatorSet.AreaCenter(region, out area, out _, out _);
                        if (area.D > maxArea.D)
                        {
                            if (best != null)
                            {
                                best.Dispose();
                            }
                            best = cur;
                            maxArea.Dispose();
                            maxArea = area.Clone();
                        }
                        else
                        {
                            cur.Dispose();
                        }
                        region.Dispose();
                        area.Dispose();
                    }
                    contour.Dispose();
                    contour = best;
                    maxArea.Dispose();
                }
                numObj.Dispose();
            }

            // 拿所有轮廓点的坐标
            HTuple rows, cols;
            HOperatorSet.GetContourXld(contour, out rows, out cols);
            contour.Dispose();

            // 逐列取底边：每列只保留行坐标最大的点 → 精确下边缘
            var colToRow = new Dictionary<int, double>();
            for (int i = 0; i < rows.Length; i++)
            {
                int c = (int)Math.Round(cols[i].D);
                double r = rows[i].D;
                if (!colToRow.ContainsKey(c) || r > colToRow[c])
                {
                    colToRow[c] = r;
                }
            }
            int minCol = int.MaxValue;
            int maxCol = int.MinValue;
            foreach (int c in colToRow.Keys)
            {
                if (c < minCol) minCol = c;
                if (c > maxCol) maxCol = c;
            }
            HTuple btmRows = new HTuple();
            HTuple btmCols = new HTuple();
            for (int c = minCol; c <= maxCol; c++)
            {
                if (colToRow.TryGetValue(c, out double r))
                {
                    btmRows = btmRows.TupleConcat(r);
                    btmCols = btmCols.TupleConcat(c);
                }
            }

            // 手算最小二乘：r = a*c + b，加除零保护
            int n = btmRows.Length;
            double sumR = 0, sumC = 0, sumCR = 0, sumC2 = 0;
            for (int i = 0; i < n; i++)
            {
                double r = btmRows[i].D;
                double c = btmCols[i].D;
                sumR += r;
                sumC += c;
                sumCR += c * r;
                sumC2 += c * c;
            }
            double denom = n * sumC2 - sumC * sumC;
            double a, b;
            if (Math.Abs(denom) < 1e-10)
            {
                a = 0;
                b = sumR / n;
            }
            else
            {
                a = (n * sumCR - sumC * sumR) / denom;
                b = (sumR - a * sumC) / n;
            }

            // 端点延伸到完整列范围
            HTuple minC = btmCols.TupleMin();
            HTuple maxC = btmCols.TupleMax();
            hv_ColBegin = minC.Clone();
            hv_RowBegin = a * minC.D + b;
            hv_ColEnd = maxC.Clone();
            hv_RowEnd = a * maxC.D + b;

            hv_CenterRow = (hv_RowBegin.D + hv_RowEnd.D) / 2.0;
            hv_CenterCol = (hv_ColBegin.D + hv_ColEnd.D) / 2.0;

            rows.Dispose();
            cols.Dispose();
            btmRows.Dispose();
            btmCols.Dispose();
            minC.Dispose();
            maxC.Dispose();
        }

        public static void CalcBlueMembraneAreas(HObject ho_Image, out HTuple hv_Areas,
            out HTuple hv_Rows, out HTuple hv_Columns)
        {
            HObject ho_BlueRegion = null, ho_ConnectedRegions = null;
            HOperatorSet.GenEmptyObj(out ho_BlueRegion); HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            hv_Areas = new HTuple(); hv_Rows = new HTuple(); hv_Columns = new HTuple();
            try
            {
                ho_BlueRegion.Dispose(); HOperatorSet.Threshold(ho_Image, out ho_BlueRegion, 105, 135);
                ho_ConnectedRegions.Dispose(); HOperatorSet.Connection(ho_BlueRegion, out ho_ConnectedRegions);
                hv_Areas.Dispose(); HOperatorSet.RegionFeatures(ho_ConnectedRegions, "area", out hv_Areas);
                hv_Rows.Dispose(); HOperatorSet.RegionFeatures(ho_ConnectedRegions, "row", out hv_Rows);
                hv_Columns.Dispose(); HOperatorSet.RegionFeatures(ho_ConnectedRegions, "column", out hv_Columns);
                ho_BlueRegion.Dispose(); ho_ConnectedRegions.Dispose();
            }
            catch (HalconException) { ho_BlueRegion.Dispose(); ho_ConnectedRegions.Dispose(); hv_Areas = new HTuple(); hv_Rows = new HTuple(); hv_Columns = new HTuple(); }
        }

        public static void FilterSmallBlueMembrane(HImage ho_Image, HTuple hv_MinArea, out HImage ho_FilteredImage)
        {
            HObject ho_AllRegion2 = null, ho_ConnectedRegions = null, ho_LargeRegions = null, ho_SmallRegions = null;
            HOperatorSet.GenEmptyObj(out ho_AllRegion2); HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_LargeRegions); HOperatorSet.GenEmptyObj(out ho_SmallRegions);
            ho_FilteredImage = null;
            try
            {
                ho_AllRegion2.Dispose(); HOperatorSet.Threshold(ho_Image, out ho_AllRegion2, 2.0, 2.0);
                ho_ConnectedRegions.Dispose(); HOperatorSet.Connection(ho_AllRegion2, out ho_ConnectedRegions);
                ho_LargeRegions.Dispose(); HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_LargeRegions, "area", "and", hv_MinArea, 99999999);
                ho_SmallRegions.Dispose(); HOperatorSet.Difference(ho_AllRegion2, ho_LargeRegions, out ho_SmallRegions);
                HObject t = new HImage(ho_Image);
                HOperatorSet.PaintRegion(ho_SmallRegions, t, out HObject r, 0.0, "fill");
                ho_FilteredImage = new HImage(r); t.Dispose(); r.Dispose();
                ho_AllRegion2.Dispose(); ho_ConnectedRegions.Dispose(); ho_LargeRegions.Dispose(); ho_SmallRegions.Dispose();
            }
            catch (HalconException) { ho_AllRegion2.Dispose(); ho_ConnectedRegions.Dispose(); ho_LargeRegions.Dispose(); ho_SmallRegions.Dispose(); ho_FilteredImage = new HImage(ho_Image); }
        }

        public static void FilterSmallTopCover(HImage ho_Image, HTuple hv_MinArea, out HImage ho_FilteredImage)
        {
            HObject ho_AllRegion1 = null, ho_ConnectedRegions = null, ho_LargeRegions = null, ho_SmallRegions = null;
            HOperatorSet.GenEmptyObj(out ho_AllRegion1); HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_LargeRegions); HOperatorSet.GenEmptyObj(out ho_SmallRegions);
            ho_FilteredImage = null;
            try
            {
                ho_AllRegion1.Dispose(); HOperatorSet.Threshold(ho_Image, out ho_AllRegion1, 1.0, 1.0);
                ho_ConnectedRegions.Dispose(); HOperatorSet.Connection(ho_AllRegion1, out ho_ConnectedRegions);
                ho_LargeRegions.Dispose(); HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_LargeRegions, "area", "and", hv_MinArea, 99999999);
                ho_SmallRegions.Dispose(); HOperatorSet.Difference(ho_AllRegion1, ho_LargeRegions, out ho_SmallRegions);
                HObject t = new HImage(ho_Image);
                HOperatorSet.PaintRegion(ho_SmallRegions, t, out HObject r, 0.0, "fill");
                ho_FilteredImage = new HImage(r); t.Dispose(); r.Dispose();
                ho_AllRegion1.Dispose(); ho_ConnectedRegions.Dispose(); ho_LargeRegions.Dispose(); ho_SmallRegions.Dispose();
            }
            catch (HalconException) { ho_AllRegion1.Dispose(); ho_ConnectedRegions.Dispose(); ho_LargeRegions.Dispose(); ho_SmallRegions.Dispose(); ho_FilteredImage = new HImage(ho_Image); }
        }

        public static void gen_arrow_contour_xld(out HObject ho_Arrow, HTuple hv_Row1, HTuple hv_Column1,
            HTuple hv_Row2, HTuple hv_Column2, HTuple hv_HeadLength, HTuple hv_HeadWidth)
        {
            HObject ho_TempArrow = null;
            HTuple hv_Length = new HTuple(), hv_ZeroLengthIndices = new HTuple();
            HTuple hv_DR = new HTuple(), hv_DC = new HTuple(), hv_HalfHeadWidth = new HTuple();
            HTuple hv_RowP1 = new HTuple(), hv_ColP1 = new HTuple(), hv_RowP2 = new HTuple(), hv_ColP2 = new HTuple();
            HTuple hv_Index = new HTuple();
            HOperatorSet.GenEmptyObj(out ho_Arrow); HOperatorSet.GenEmptyObj(out ho_TempArrow);
            try
            {
                ho_Arrow.Dispose(); HOperatorSet.GenEmptyObj(out ho_Arrow);
                hv_Length.Dispose(); HOperatorSet.DistancePp(hv_Row1, hv_Column1, hv_Row2, hv_Column2, out hv_Length);
                hv_ZeroLengthIndices.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_ZeroLengthIndices = hv_Length.TupleFind(0); }
                if ((int)(new HTuple(hv_ZeroLengthIndices.TupleNotEqual(-1))) != 0)
                { if (hv_Length == null) hv_Length = new HTuple(); hv_Length[hv_ZeroLengthIndices] = -1; }
                hv_DR.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_DR = (1.0 * (hv_Row2 - hv_Row1)) / hv_Length; }
                hv_DC.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_DC = (1.0 * (hv_Column2 - hv_Column1)) / hv_Length; }
                hv_HalfHeadWidth.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_HalfHeadWidth = hv_HeadWidth / 2.0; }
                hv_RowP1.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_RowP1 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) + (hv_HalfHeadWidth * hv_DC); }
                hv_ColP1.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_ColP1 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) - (hv_HalfHeadWidth * hv_DR); }
                hv_RowP2.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_RowP2 = (hv_Row1 + ((hv_Length - hv_HeadLength) * hv_DR)) - (hv_HalfHeadWidth * hv_DC); }
                hv_ColP2.Dispose(); using (HDevDisposeHelper dh = new HDevDisposeHelper()) { hv_ColP2 = (hv_Column1 + ((hv_Length - hv_HeadLength) * hv_DC)) + (hv_HalfHeadWidth * hv_DR); }
                for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_Length.TupleLength())) - 1); hv_Index = (int)hv_Index + 1)
                {
                    if ((int)(new HTuple(((hv_Length.TupleSelect(hv_Index))).TupleEqual(-1))) != 0)
                    { using (HDevDisposeHelper dh = new HDevDisposeHelper()) { ho_TempArrow.Dispose(); HOperatorSet.GenContourPolygonXld(out ho_TempArrow, hv_Row1.TupleSelect(hv_Index), hv_Column1.TupleSelect(hv_Index)); } }
                    else
                    { using (HDevDisposeHelper dh = new HDevDisposeHelper()) { ho_TempArrow.Dispose(); HOperatorSet.GenContourPolygonXld(out ho_TempArrow, hv_Row1.TupleSelect(hv_Index).TupleConcat(hv_Row2.TupleSelect(hv_Index)).TupleConcat(hv_RowP1.TupleSelect(hv_Index)).TupleConcat(hv_Row2.TupleSelect(hv_Index)).TupleConcat(hv_RowP2.TupleSelect(hv_Index)).TupleConcat(hv_Row2.TupleSelect(hv_Index)), hv_Column1.TupleSelect(hv_Index).TupleConcat(hv_Column2.TupleSelect(hv_Index)).TupleConcat(hv_ColP1.TupleSelect(hv_Index)).TupleConcat(hv_Column2.TupleSelect(hv_Index)).TupleConcat(hv_ColP2.TupleSelect(hv_Index)).TupleConcat(hv_Column2.TupleSelect(hv_Index))); } }
                    { HObject x; HOperatorSet.ConcatObj(ho_Arrow, ho_TempArrow, out x); ho_Arrow.Dispose(); ho_Arrow = x; }
                }
                ho_TempArrow.Dispose(); hv_Length.Dispose(); hv_ZeroLengthIndices.Dispose(); hv_DR.Dispose(); hv_DC.Dispose();
                hv_HalfHeadWidth.Dispose(); hv_RowP1.Dispose(); hv_ColP1.Dispose(); hv_RowP2.Dispose(); hv_ColP2.Dispose(); hv_Index.Dispose();
                return;
            }
            catch (HalconException ex) { ho_TempArrow.Dispose(); hv_Length.Dispose(); hv_ZeroLengthIndices.Dispose(); hv_DR.Dispose(); hv_DC.Dispose(); hv_HalfHeadWidth.Dispose(); hv_RowP1.Dispose(); hv_ColP1.Dispose(); hv_RowP2.Dispose(); hv_ColP2.Dispose(); hv_Index.Dispose(); throw ex; }
        }
    }
}
