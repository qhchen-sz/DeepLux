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
        public static (double value, double row, double col) DetectPoint(ePointType pointType, HTuple zValues, HTuple pointY, HTuple pointX, InflectionParams inflectionParams = null)
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
                    return CalcSchmittInflection(zValues, pointY, pointX, inflectionParams);
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
                    return DetectPoint(item.PointType, zValues, pointY, pointX, item.InflectionParams);
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
        /// 拐点检测（差分法）：沿图像宽度方向（Col）分析高度剖面，检测Z值突变位置
        /// </summary>
        /// <param name="zValues">Z值数组</param>
        /// <param name="pointY">点Y坐标数组</param>
        /// <param name="pointX">点X坐标数组</param>
        /// <param name="inflectionParams">拐点参数（形状/选择/灵敏度），为null时使用默认值</param>
        public static (double value, double row, double col) CalcInflection(HTuple zValues, HTuple pointY, HTuple pointX, InflectionParams inflectionParams)
        {
            if (zValues.Length < 2) return (double.NaN, 0, 0);

            double[] zArr = zValues.ToDArr();
            double[] yArr = pointY.ToDArr();
            double[] xArr = pointX.ToDArr();

            int n = zArr.Length;

            // 使用默认参数
            eEdgeShape shape = inflectionParams?.Shape ?? eEdgeShape.上升沿;
            eEdgeSelect select = inflectionParams?.Select ?? eEdgeSelect.第一个;
            double sensitivity = inflectionParams?.Sensitivity ?? 0.1;

            // 按Col升序排序，形成从左到右的高度剖面
            int[] indices = Enumerable.Range(0, n).ToArray();
            Array.Sort(indices, (a, b) => xArr[a].CompareTo(xArr[b]));

            double[] sortedX = indices.Select(i => xArr[i]).ToArray();
            double[] sortedY = indices.Select(i => yArr[i]).ToArray();
            double[] sortedZ = indices.Select(i => zArr[i]).ToArray();

            // 遍历相邻点差分，查找满足条件的拐点
            int targetIdx = -1;
            for (int i = 0; i < n - 1; i++)
            {
                double diff = sortedZ[i + 1] - sortedZ[i];
                bool isMatch = shape == eEdgeShape.上升沿
                    ? diff > sensitivity
                    : diff < -sensitivity;

                if (isMatch)
                {
                    targetIdx = i;
                    if (select == eEdgeSelect.第一个)
                    {
                        break; // 找到第一个即停止
                    }
                    // 最后一个：继续遍历，保留最后一个匹配
                }
            }

            if (targetIdx < 0)
                return (double.NaN, 0, 0);

            double resultZ = sortedZ[targetIdx];
            double resultRow = sortedY[targetIdx];
            double resultCol = sortedX[targetIdx];
            return (resultZ, resultRow, resultCol);
        }

        /// <summary>
        /// 拐点检测（施密特触发器/迟滞比较器）：沿图像宽度方向（Col）分析高度剖面，
        /// 使用双阈值迟滞状态机检测上升沿/下降沿穿越位置。
        /// 灵敏度参数通过 InflectionParams.Sensitivity（int，1~100）映射为 0.01~1.0。
        /// sensitivity=0.01 时最灵敏（迟滞带极窄），sensitivity=1.0 时最不灵敏（迟滞带覆盖全量程）。
        /// </summary>
        public static (double value, double row, double col) CalcSchmittInflection(
            HTuple zValues, HTuple pointY, HTuple pointX, InflectionParams inflectionParams)
        {
            if (zValues.Length < 2) return (double.NaN, 0, 0);

            double[] zArr = zValues.ToDArr();
            double[] yArr = pointY.ToDArr();
            double[] xArr = pointX.ToDArr();
            int n = zArr.Length;

            // 解析参数
            eEdgeShape shape = inflectionParams?.Shape ?? eEdgeShape.上升沿;
            eEdgeSelect select = inflectionParams?.Select ?? eEdgeSelect.第一个;
            double sensitivity = inflectionParams?.Sensitivity ?? 0.1;
            if (sensitivity <= 0) sensitivity = 0.1;
            if (sensitivity > 1) sensitivity = 1.0;

            // 按 Col 升序排序，形成从左到右的高度剖面
            int[] indices = Enumerable.Range(0, n).ToArray();
            Array.Sort(indices, (a, b) => xArr[a].CompareTo(xArr[b]));

            double[] sortedX = new double[n];
            double[] sortedY = new double[n];
            double[] sortedZ = new double[n];
            for (int i = 0; i < n; i++)
            {
                int idx = indices[i];
                sortedX[i] = xArr[idx];
                sortedY[i] = yArr[idx];
                sortedZ[i] = zArr[idx];
            }

            // ---- 调试：导出原始点云数据（x y z） ----
            using (var sw = System.IO.File.CreateText(@"D:\ContourDebug_Schmitt.txt"))
            {
                for (int i = 0; i < n; i++)
                    sw.WriteLine($"{sortedX[i]:F8} {sortedY[i]:F8} {sortedZ[i]:F8}");
            }
            // ---- 调试结束 ----

            // 按 Col 聚合：同一 Col 位置的多个点取 Z 均值，消除多行数据的影响
            List<double> aggCols = new List<double>();
            List<double> aggRows = new List<double>();
            List<double> aggZ = new List<double>();
            {
                int j = 0;
                while (j < n)
                {
                    int colKey = (int)Math.Round(sortedX[j]);
                    double sumZ = 0, sumY = 0;
                    int count = 0;
                    while (j < n && (int)Math.Round(sortedX[j]) == colKey)
                    {
                        if (!double.IsNaN(sortedZ[j]))
                        {
                            sumZ += sortedZ[j];
                            sumY += sortedY[j];
                            count++;
                        }
                        j++;
                    }
                    if (count > 0)
                    {
                        aggCols.Add(colKey);
                        aggRows.Add((sumY) / count);
                        aggZ.Add(sumZ / count);
                    }
                }
            }

            int m = aggZ.Count;
            if (m < 2) return (double.NaN, 0, 0);

            double[] profileX = aggCols.ToArray();
            double[] profileY = aggRows.ToArray();
            double[] profileZ = aggZ.ToArray();

            // 计算有效 Z 范围
            double zMin = profileZ[0], zMax = profileZ[0];
            for (int i = 1; i < m; i++)
            {
                if (profileZ[i] < zMin) zMin = profileZ[i];
                if (profileZ[i] > zMax) zMax = profileZ[i];
            }
            if (zMin >= zMax) return (double.NaN, 0, 0); // Z 值无变化

            double zRange = zMax - zMin;
            double mid = (zMax + zMin) / 2.0;

            // 根据灵敏度计算双阈值
            // sensitivity=0 → 两阈值=mid（最灵敏，任何穿越都触发）
            // sensitivity=1 → highThreshold=zMax, lowThreshold=zMin（最不灵敏）
            double highThreshold = mid + sensitivity * zRange / 2.0;
            double lowThreshold = mid - sensitivity * zRange / 2.0;

            // 状态机遍历聚合后的 1D 剖面
            const int LOW = 0, HIGH = 1;
            int state = LOW;
            List<int> risingEdges = new List<int>();
            List<int> fallingEdges = new List<int>();

            for (int i = 0; i < m; i++)
            {
                if (state == LOW && profileZ[i] >= highThreshold)
                {
                    state = HIGH;
                    risingEdges.Add(i);
                }
                else if (state == HIGH && profileZ[i] <= lowThreshold)
                {
                    state = LOW;
                    fallingEdges.Add(i);
                }
            }

            // 根据 Shape 和 Select 筛选
            List<int> candidates = (shape == eEdgeShape.上升沿) ? risingEdges : fallingEdges;

            if (candidates.Count == 0)
                return (double.NaN, 0, 0);

            int targetIdx = (select == eEdgeSelect.第一个)
                ? candidates[0]
                : candidates[candidates.Count - 1];

            return (profileZ[targetIdx], profileY[targetIdx], profileX[targetIdx]);
        }

        /// <summary>
        /// 按 Col 聚合：同一 Col 位置的多个点取 Z 均值和 Row 均值，消除多行数据的影响
        /// </summary>
        public static (double[] aggCols, double[] aggRows, double[] aggZ) AggregateByCol(
            double[] pointX, double[] pointY, double[] zValues)
        {
            int n = pointX.Length;
            if (n == 0) return (new double[0], new double[0], new double[0]);

            // 按 Col 排序
            int[] indices = Enumerable.Range(0, n).ToArray();
            Array.Sort(indices, (a, b) => pointX[a].CompareTo(pointX[b]));

            List<double> aggCols = new List<double>();
            List<double> aggRows = new List<double>();
            List<double> aggZ = new List<double>();

            int j = 0;
            while (j < n)
            {
                int colKey = (int)Math.Round(pointX[indices[j]]);
                double sumZ = 0, sumY = 0;
                int count = 0;
                while (j < n && (int)Math.Round(pointX[indices[j]]) == colKey)
                {
                    if (!double.IsNaN(zValues[indices[j]]))
                    {
                        sumZ += zValues[indices[j]];
                        sumY += pointY[indices[j]];
                        count++;
                    }
                    j++;
                }
                if (count > 0)
                {
                    aggCols.Add(colKey);
                    aggRows.Add(sumY / count);
                    aggZ.Add(sumZ / count);
                }
            }

            return (aggCols.ToArray(), aggRows.ToArray(), aggZ.ToArray());
        }
    }
}
