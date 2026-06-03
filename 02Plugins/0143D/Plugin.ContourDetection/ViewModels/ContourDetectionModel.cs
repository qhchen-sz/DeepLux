using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plugin.ContourDetection.ViewModels
{
    /// <summary>
    /// 轮廓检测算法模型 — 纯计算方法，与模块主体无关
    /// </summary>
    public static class ContourDetectionModel
    {
        /// <summary>
        /// 检测点特征值
        /// </summary>
        public static (double value, double row, double col) DetectPoint(ePointType pointType, HTuple zValues, HTuple pointY, HTuple pointX)
        {
            if (zValues.Length == 0) return (double.NaN, 0, 0);

            switch (pointType)
            {
                case ePointType.Z最大值:
                    return CalcZMax(zValues, pointY, pointX);
                case ePointType.Z最小值:
                    return CalcZMin(zValues, pointY, pointX);
                case ePointType.Z平均值:
                    return (CalcZMean(zValues), 0, 0);
                case ePointType.D平均值:
                    return (CalcDMean(zValues), 0, 0);
                case ePointType.拐点:
                    return CalcInflection(zValues, pointY, pointX);
                default:
                    return (0, 0, 0);
            }
        }

        /// <summary>
        /// 构建工作流分支
        /// </summary>
        public static (double value, double row, double col) ExeBuildWorkflow(WorkflowItem item, HTuple zValues, HTuple pointY, HTuple pointX)
        {
            switch (item.OperationName)
            {
                case "点":
                    return DetectPoint(item.PointType, zValues, pointY, pointX);
                case "线":
                    // TODO: 待后续实现
                    return (0, 0, 0);
                case "圆":
                    // TODO: 待后续实现
                    return (0, 0, 0);
                default:
                    return (0, 0, 0);
            }
        }

        /// <summary>
        /// 处理单条轮廓条带：根据条带几何参数从高度图提取数据，并运行完整工作流管线
        /// </summary>
        /// <param name="heightImage">高度图 HImage</param>
        /// <param name="stripCenterRow">条带中心 Row</param>
        /// <param name="stripCenterCol">条带中心 Col</param>
        /// <param name="stripPhi">条带方向角（弧度）</param>
        /// <param name="stripLength1">条带半宽度（沿 Width 方向）</param>
        /// <param name="stripHalfWidth">条带半高（沿 Length 方向）</param>
        /// <param name="workflowItems">启用的工作流项集合</param>
        /// <returns>
        ///   resultVal:  工作流管线输出的最终检测值
        ///   resultRow:  检测点 Row 坐标（若工作流未返回有效坐标，则回退为条带中心）
        ///   resultCol:  检测点 Col 坐标（同上）
        ///   pointY:     区域内点 Y 坐标数组
        ///   pointX:     区域内点 X 坐标数组
        ///   zValues:    区域内 Z 值数组
        ///   hasData:    区域内是否有数据点
        /// </returns>
        public static (double resultVal, double resultRow, double resultCol,
                       double[] pointY, double[] pointX, double[] zValues, bool hasData)
            ProcessSingleStrip(HImage heightImage,
                               double stripCenterRow, double stripCenterCol,
                               double stripPhi, double stripLength1, double stripHalfWidth,
                               IEnumerable<WorkflowItem> workflowItems)
        {
            HRegion subRegion = new HRegion();
            HImage reducedImage = null;
            HRegion domain = null;

            try
            {
                // 生成旋转矩形区域
                subRegion.GenRectangle2(stripCenterRow, stripCenterCol, stripPhi, stripLength1, stripHalfWidth);
                reducedImage = heightImage.ReduceDomain(subRegion);
                domain = reducedImage.GetDomain();
                domain.GetRegionPoints(out HTuple pointY, out HTuple pointX);

                if (pointY.Length == 0)
                {
                    return (double.NaN, stripCenterRow, stripCenterCol,
                            new double[0], new double[0], new double[0], false);
                }

                HTuple zValues = reducedImage.GetGrayval(pointY, pointX);

                // 运行工作流管线
                double resultVal = 0;
                double resultRow = stripCenterRow;
                double resultCol = stripCenterCol;
                bool workflowExecuted = false;

                foreach (var item in workflowItems)
                {
                    if (!item.m_enable) continue;

                    switch (item.Category)
                    {
                        case eWorkflowCategory.构建:
                            var (val, row, col) = ExeBuildWorkflow(item, zValues, pointY, pointX);
                            resultVal = val;
                            if (row != 0 || col != 0)
                            {
                                resultRow = row;
                                resultCol = col;
                            }
                            workflowExecuted = true;
                            break;
                        case eWorkflowCategory.测量:
                            // TODO: 待后续实现
                            break;
                        case eWorkflowCategory.计算:
                            // TODO: 待后续实现
                            break;
                    }
                }

                if (!workflowExecuted)
                {
                    return (0, stripCenterRow, stripCenterCol,
                            pointY.ToDArr(), pointX.ToDArr(), zValues.ToDArr(), true);
                }

                return (resultVal, resultRow, resultCol,
                        pointY.ToDArr(), pointX.ToDArr(), zValues.ToDArr(), true);
            }
            finally
            {
                subRegion?.Dispose();
                reducedImage?.Dispose();
                domain?.Dispose();
            }
        }

        /// <summary>
        /// Z最大值：取整个区域内Z最大值的坐标和Z值
        /// </summary>
        public static (double maxZ, double maxRow, double maxCol) CalcZMax(HTuple zValues, HTuple pointY, HTuple pointX)
        {
            if (zValues.Length == 0) return (double.NaN, 0, 0);

            double[] zArr = zValues.ToDArr();
            double[] yArr = pointY.ToDArr();
            double[] xArr = pointX.ToDArr();

            double maxVal = double.MinValue;
            double maxRow = 0, maxCol = 0;
            for (int i = 0; i < zArr.Length; i++)
            {
                if (zArr[i] > maxVal)
                {
                    maxVal = zArr[i];
                    maxRow = yArr[i];
                    maxCol = xArr[i];
                }
            }
            return (maxVal, maxRow, maxCol);
        }

        /// <summary>
        /// Z最小值：取整个区域内Z最小值的坐标和Z值
        /// </summary>
        public static (double minZ, double minRow, double minCol) CalcZMin(HTuple zValues, HTuple pointY, HTuple pointX)
        {
            if (zValues.Length == 0) return (double.NaN, 0, 0);

            double[] zArr = zValues.ToDArr();
            double[] yArr = pointY.ToDArr();
            double[] xArr = pointX.ToDArr();

            double minVal = double.MaxValue;
            double minRow = 0, minCol = 0;
            for (int i = 0; i < zArr.Length; i++)
            {
                if (zArr[i] < minVal)
                {
                    minVal = zArr[i];
                    minRow = yArr[i];
                    minCol = xArr[i];
                }
            }
            return (minVal, minRow, minCol);
        }

        /// <summary>
        /// Z平均值：所有Z值的均值
        /// </summary>
        public static double CalcZMean(HTuple zValues)
        {
            if (zValues.Length == 0) return 0;
            double sum = 0;
            double[] arr = zValues.ToDArr();
            for (int i = 0; i < arr.Length; i++)
            {
                sum += arr[i];
            }
            return sum / arr.Length;
        }

        /// <summary>
        /// D平均值（暂用Z平均值占位，待后续实现）
        /// </summary>
        public static double CalcDMean(HTuple zValues)
        {
            return CalcZMean(zValues);
        }

        /// <summary>
        /// 拐点检测（暂用Z最大值占位，待后续实现）
        /// </summary>
        public static (double value, double row, double col) CalcInflection(HTuple zValues, HTuple pointY, HTuple pointX)
        {
            return CalcZMax(zValues, pointY, pointX);
        }
    }
}
