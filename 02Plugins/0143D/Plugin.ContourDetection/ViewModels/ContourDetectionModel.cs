using HalconDotNet;
using System;

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
