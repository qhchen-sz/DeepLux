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
        public static bool Find_HoLine(HObject ho_Image, out HObject ho_Line, out HObject ho_Region,
HTuple hv_Row, HTuple hv_Column, HTuple hv_Phi, HTuple hv_Length1, HTuple hv_Length2,
HTuple hv_Threshold, HTuple hv_MeasureNum, HTuple hv_Transition, HTuple hv_Select,
HTuple hv_Score, out HTuple hv_RowBegin, out HTuple hv_ColumnBegin, out HTuple hv_RowEnd,
out HTuple hv_ColumnEnd)
        {




            // Local iconic variables 

            HObject ho_Line1 = null, ho_Polygons = null;

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

            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Line);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_Line1);
            HOperatorSet.GenEmptyObj(out ho_Polygons);
            hv_RowBegin = new HTuple();
            hv_ColumnBegin = new HTuple();
            hv_RowEnd = new HTuple();
            hv_ColumnEnd = new HTuple();
            try
            {
                try
                {
                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    //**创建卡尺ROI
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
                        hv_RowBegin1 = hv_BeginRow.TupleSelect(
                            0);
                    }
                    hv_ColBegin1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ColBegin1 = hv_BeginCol.TupleSelect(
                            0);
                    }
                    hv_RowEnd1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_RowEnd1 = hv_EndRow.TupleSelect(
                            0);
                    }
                    hv_ColEnd1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ColEnd1 = hv_EndCol.TupleSelect(
                            0);
                    }

                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectLineMeasure(hv_MetrologyHandle, hv_RowEnd1, hv_ColEnd1, hv_RowBegin1,
                        hv_ColBegin1,  hv_Length1, 2, 1, hv_Threshold,
                        new HTuple(), new HTuple(), out hv_Index1);
                    //                HOperatorSet.AddMetrologyObjectLineMeasure(hv_MetrologyHandle,  hv_RowEnd1, hv_ColEnd1, hv_RowBegin1,
                    //hv_ColBegin1, hv_Length1, 2, 1, hv_Threshold,
                    //new HTuple(), new HTuple(), out hv_Index1);


                    //**设置卡尺个数
                    //set_metrology_object_param (MetrologyHandle, 'all', 'rand_seed', 1)
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "num_measures",
                        hv_MeasureNum);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "min_score",
                        hv_Score);
                    //if(hv_Transition== "negative")
                    //    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_transition",
                    //    "positive");
                    //else if(hv_Transition == "positive")
                    //    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_transition",
                    //    "negative");
                    //else
                    //    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_transition",
                    //"all");
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_transition",
hv_Transition);
                    //if(hv_Select=="first")
                    //HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_select",
                    //    "last");
                    //else if(hv_Select == "last")
                    //    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_select",
                    //    "first");
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_select",
    hv_Select);
                    ho_Region.Dispose(); hv_Row_COPY_INP_TMP.Dispose(); hv_Column_COPY_INP_TMP.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_Region, hv_MetrologyHandle,
                        hv_Index1, "all", out hv_Row_COPY_INP_TMP, out hv_Column_COPY_INP_TMP);

                    HOperatorSet.ApplyMetrologyModel(ho_Image, hv_MetrologyHandle);
                    ho_Line.Dispose();
                    HOperatorSet.GetMetrologyObjectResultContour(out ho_Line, hv_MetrologyHandle,
                        hv_Index1, 0, 1.5);
                    hv_RowBegin.Dispose(); hv_ColumnBegin.Dispose(); hv_RowEnd.Dispose(); hv_ColumnEnd.Dispose(); hv_Nr.Dispose(); hv_Nc.Dispose(); hv_Dist.Dispose();
                    HOperatorSet.FitLineContourXld(ho_Line, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                        out hv_ColumnBegin, out hv_RowEnd, out hv_ColumnEnd, out hv_Nr, out hv_Nc,
                        out hv_Dist);

                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    return false;
                }

                ho_Line1.Dispose();
                ho_Polygons.Dispose();

                hv_Column_COPY_INP_TMP.Dispose();
                hv_Row_COPY_INP_TMP.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_BeginRow.Dispose();
                hv_BeginCol.Dispose();
                hv_EndRow.Dispose();
                hv_EndCol.Dispose();
                hv_Length.Dispose();
                hv_Phi1.Dispose();
                hv_RowBegin1.Dispose();
                hv_ColBegin1.Dispose();
                hv_RowEnd1.Dispose();
                hv_ColEnd1.Dispose();
                hv_Index1.Dispose();
                hv_Nr.Dispose();
                hv_Nc.Dispose();
                hv_Dist.Dispose();
                hv_Exception.Dispose();

                return true;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Line1.Dispose();
                ho_Polygons.Dispose();

                hv_Column_COPY_INP_TMP.Dispose();
                hv_Row_COPY_INP_TMP.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_MetrologyHandle.Dispose();
                hv_BeginRow.Dispose();
                hv_BeginCol.Dispose();
                hv_EndRow.Dispose();
                hv_EndCol.Dispose();
                hv_Length.Dispose();
                hv_Phi1.Dispose();
                hv_RowBegin1.Dispose();
                hv_ColBegin1.Dispose();
                hv_RowEnd1.Dispose();
                hv_ColEnd1.Dispose();
                hv_Index1.Dispose();
                hv_Nr.Dispose();
                hv_Nc.Dispose();
                hv_Dist.Dispose();
                hv_Exception.Dispose();
                return false;
                throw HDevExpDefaultException;

            }
        }//找线
    }
}
