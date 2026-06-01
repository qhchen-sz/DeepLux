using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.MeasureLine.ViewModels
{
    public static class FindLineTools
    {
        /// <summary>
        /// 直线检测（基于Halcon计量模型）
        /// </summary>
        /// <param name="ho_Image">输入图像</param>
        /// <param name="ho_Line">输出直线轮廓</param>
        /// <param name="ho_Region">输出测量区域（卡尺点集轮廓）</param>
        /// <param name="hv_Row">检测矩形中心行</param>
        /// <param name="hv_Column">检测矩形中心列</param>
        /// <param name="hv_Phi">检测矩形角度</param>
        /// <param name="hv_Length1">矩形半长</param>
        /// <param name="hv_Length2">矩形半宽</param>
        /// <param name="hv_Threshold">边缘阈值</param>
        /// <param name="hv_MeasureNum">卡尺数量</param>
        /// <param name="hv_Transition">边缘极性</param>
        /// <param name="hv_Select">点筛选方式</param>
        /// <param name="hv_Score">最小分数</param>
        /// <param name="hv_RowBegin">输出直线起点行</param>
        /// <param name="hv_ColumnBegin">输出直线起点列</param>
        /// <param name="hv_RowEnd">输出直线终点行</param>
        /// <param name="hv_ColumnEnd">输出直线终点列</param>
        /// <param name="measureRows">输出测量点行坐标集合（实际边缘点）</param>
        /// <param name="measureCols">输出测量点列坐标集合</param>
        /// <returns>是否检测成功</returns>
        public static bool Find_HoLine(HObject ho_Image, out HObject ho_Line, out HObject ho_Region,
            HTuple hv_Row, HTuple hv_Column, HTuple hv_Phi, HTuple hv_Length1, HTuple hv_Length2,
            HTuple hv_Threshold, HTuple hv_MeasureNum, HTuple hv_Transition, HTuple hv_Select,
            HTuple hv_Score, out HTuple hv_RowBegin, out HTuple hv_ColumnBegin, out HTuple hv_RowEnd,
            out HTuple hv_ColumnEnd, out HTuple measureRows, out HTuple measureCols)
        {
            // Local iconic variables 
            HObject ho_Line1 = null;
            HObject ho_Polygons = null;

            // Local control variables 
            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_MetrologyHandle = new HTuple(), hv_BeginRow = new HTuple();
            HTuple hv_BeginCol = new HTuple(), hv_EndRow = new HTuple();
            HTuple hv_EndCol = new HTuple(), hv_Length = new HTuple();
            HTuple hv_Phi1 = new HTuple(), hv_RowBegin1 = new HTuple();
            HTuple hv_ColBegin1 = new HTuple(), hv_RowEnd1 = new HTuple();
            HTuple hv_ColEnd1 = new HTuple(), hv_Index1 = new HTuple();
            HTuple hv_Nr = new HTuple(), hv_Nc = new HTuple(), hv_Dist = new HTuple();
            HTuple hv_Exception = new HTuple();
            HTuple hv_Column_COPY_INP_TMP = new HTuple(hv_Column);
            HTuple hv_Row_COPY_INP_TMP = new HTuple(hv_Row);

            // Initialize output
            HOperatorSet.GenEmptyObj(out ho_Line);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_Line1);
            HOperatorSet.GenEmptyObj(out ho_Polygons);
            hv_RowBegin = new HTuple();
            hv_ColumnBegin = new HTuple();
            hv_RowEnd = new HTuple();
            hv_ColumnEnd = new HTuple();
            measureRows = new HTuple();
            measureCols = new HTuple();

            try
            {
                try
                {
                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);

                    // 创建计量模型
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    HOperatorSet.SetMetrologyModelImageSize(hv_MetrologyHandle, hv_Width, hv_Height);

                    ho_Line1.Dispose();
                    HOperatorSet.GenRectangle2ContourXld(out ho_Line1, hv_Row_COPY_INP_TMP, hv_Column_COPY_INP_TMP,
                        hv_Phi, 0, hv_Length2);
                    ho_Polygons.Dispose();
                    HOperatorSet.GenPolygonsXld(ho_Line1, out ho_Polygons, "ramer", 2);
                    hv_BeginRow.Dispose(); hv_BeginCol.Dispose(); hv_EndRow.Dispose(); hv_EndCol.Dispose(); hv_Length.Dispose(); hv_Phi1.Dispose();
                    HOperatorSet.GetLinesXld(ho_Polygons, out hv_BeginRow, out hv_BeginCol, out hv_EndRow,
                        out hv_EndCol, out hv_Length, out hv_Phi1);

                    hv_RowBegin1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_RowBegin1 = hv_BeginRow.TupleSelect(0);
                    }
                    hv_ColBegin1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ColBegin1 = hv_BeginCol.TupleSelect(0);
                    }
                    hv_RowEnd1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_RowEnd1 = hv_EndRow.TupleSelect(0);
                    }
                    hv_ColEnd1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ColEnd1 = hv_EndCol.TupleSelect(0);
                    }

                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectLineMeasure(hv_MetrologyHandle, hv_RowEnd1, hv_ColEnd1, hv_RowBegin1,
                        hv_ColBegin1, hv_Length1, 2, 1, hv_Threshold,
                        new HTuple(), new HTuple(), out hv_Index1);

                    // 设置卡尺个数和分数
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "num_measures", hv_MeasureNum);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "min_score", hv_Score);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_transition", hv_Transition);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_select", hv_Select);

                    // 应用计量模型（执行测量）
                    HOperatorSet.ApplyMetrologyModel(ho_Image, hv_MetrologyHandle);

                    // 获取测量结果直线轮廓
                    ho_Line.Dispose();
                    HOperatorSet.GetMetrologyObjectResultContour(out ho_Line, hv_MetrologyHandle, hv_Index1, 0, 1.5);

                    // 获取测量点（实际边缘点）—— 必须在 ApplyMetrologyModel 之后调用
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_Region, hv_MetrologyHandle,
                        hv_Index1, "all", out measureRows, out measureCols);

                    // 拟合直线（从直线轮廓获得精确端点）
                    hv_RowBegin.Dispose(); hv_ColumnBegin.Dispose(); hv_RowEnd.Dispose(); hv_ColumnEnd.Dispose();
                    hv_Nr.Dispose(); hv_Nc.Dispose(); hv_Dist.Dispose();
                    HOperatorSet.FitLineContourXld(ho_Line, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                        out hv_ColumnBegin, out hv_RowEnd, out hv_ColumnEnd, out hv_Nr, out hv_Nc, out hv_Dist);
                }
                catch (HalconException)
                {
                    return false;
                }
                return true;
            }
            catch (HalconException)
            {
                return false;
            }
            finally
            {
                ho_Line1?.Dispose();
                ho_Polygons?.Dispose();
                hv_Column_COPY_INP_TMP?.Dispose();
                hv_Row_COPY_INP_TMP?.Dispose();
                hv_Width?.Dispose();
                hv_Height?.Dispose();
                hv_MetrologyHandle?.Dispose();
                hv_BeginRow?.Dispose();
                hv_BeginCol?.Dispose();
                hv_EndRow?.Dispose();
                hv_EndCol?.Dispose();
                hv_Length?.Dispose();
                hv_Phi1?.Dispose();
                hv_RowBegin1?.Dispose();
                hv_ColBegin1?.Dispose();
                hv_RowEnd1?.Dispose();
                hv_ColEnd1?.Dispose();
                hv_Index1?.Dispose();
                hv_Nr?.Dispose();
                hv_Nc?.Dispose();
                hv_Dist?.Dispose();
                hv_Exception?.Dispose();
            }
        }
    }
}