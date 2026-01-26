using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.Envelope.ViewModels
{
    public static  class Algorithm
    {
        public static void Find_RongDian(HObject ho_Image, out HObject ho_Line1, out HObject ho_Arrow,
            out HObject ho_Cross, HTuple hv_Location, out HTuple hv_DistanceRongDian, out HTuple hv_DistanceMo)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Region1 = null, ho_Region3 = null, ho_ConnectedRegions = null;
            HObject ho_Region2 = null, ho_RegionDilation = null, ho_RegionDifference = null;
            HObject ho_RegionDilation1 = null, ho_Rectangle = null, ho_Cross1 = null;
            HObject ho_Arrow1 = null, ho_Arrow2 = null, ho_Cross2 = null;
            HObject ho_MeeasureRegion = null, ho_ArrowRongDian = null;

            // Local control variables 

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
            hv_DistanceRongDian = new HTuple();
            hv_DistanceMo = new HTuple();
            try
            {
                try
                {
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

                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region1, 60, 60);
                    ho_Region3.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region3, 180, 180);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions);
                    ho_Region1.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region1, "max_area",
                        70);

                    ho_Region2.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region2, 120, 120);
                    ho_RegionDilation.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region3, out ho_RegionDilation, 100, 500);
                    ho_RegionDifference.Dispose();
                    HOperatorSet.Difference(ho_Region2, ho_RegionDilation, out ho_RegionDifference
                        );
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
                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    ho_RegionDilation1.Dispose();
                    HOperatorSet.DilationRectangle1(ho_Region1, out ho_RegionDilation1, 1, hv_Height);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.Intersection(ho_Region2, ho_RegionDilation1, out ExpTmpOutVar_0
                            );
                        ho_Region2.Dispose();
                        ho_Region2 = ExpTmpOutVar_0;
                    }
                    if ((int)(new HTuple(hv_Location.TupleEqual("Left"))) != 0)
                    {
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShapeStd(ho_Region2, out ExpTmpOutVar_0, "max_area",
                                70);
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Rectangle.Dispose();
                            HOperatorSet.GenRectangle1(out ho_Rectangle, hv_Row, (hv_Column.TupleSelect(
                                0)) - 5, hv_Row2.TupleSelect(0), (hv_Column.TupleSelect(0)) + 5);
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
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 1, 30, "all", "all",
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
                    else if ((int)(new HTuple(hv_Location.TupleEqual("Right"))) != 0)
                    {
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShapeStd(ho_Region2, out ExpTmpOutVar_0, "max_area",
                                70);
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            ho_Rectangle.Dispose();
                            HOperatorSet.GenRectangle1(out ho_Rectangle, hv_Row, (hv_Column.TupleSelect(
                                0)) - 5, hv_Row2.TupleSelect(0), (hv_Column.TupleSelect(0)) + 5);
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
                        HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 1, 30, "all", "all",
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
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShape(ho_Region2, out ExpTmpOutVar_0, "area", "and",
                                1500, "max");
                            ho_Region2.Dispose();
                            ho_Region2 = ExpTmpOutVar_0;
                        }
                        hv_Row.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region1, "row", out hv_Row);
                        hv_Column.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "column", out hv_Column);
                        hv_Row2.Dispose();
                        HOperatorSet.RegionFeatures(ho_Region2, "row", out hv_Row2);
                        if ((int)(new HTuple((new HTuple(hv_Column.TupleLength())).TupleGreaterEqual(
                            2))) != 0)
                        {


                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                ho_Rectangle.Dispose();
                                HOperatorSet.GenRectangle1(out ho_Rectangle, hv_Row.TupleConcat(hv_Row),
                                    (((hv_Column.TupleSelect(0)) - 5)).TupleConcat((hv_Column.TupleSelect(
                                    1)) - 5), ((hv_Row2.TupleSelect(0))).TupleConcat(hv_Row2.TupleSelect(
                                    1)), (((hv_Column.TupleSelect(0)) + 5)).TupleConcat((hv_Column.TupleSelect(
                                    1)) + 5));
                            }
                            hv_Row3.Dispose(); hv_Column2.Dispose(); hv_Phi1.Dispose(); hv_Length11.Dispose(); hv_Length21.Dispose();
                            HOperatorSet.SmallestRectangle2(ho_Rectangle, out hv_Row3, out hv_Column2,
                                out hv_Phi1, out hv_Length11, out hv_Length21);
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_MeasureHandle.Dispose();
                                HOperatorSet.GenMeasureRectangle2(hv_Row3.TupleSelect(0), hv_Column2.TupleSelect(
                                    0), -(hv_Phi1.TupleSelect(0)), hv_Length11.TupleSelect(0), hv_Length21.TupleSelect(
                                    0), hv_Width, hv_Height, "nearest_neighbor", out hv_MeasureHandle);
                            }
                            hv_RowEdge.Dispose(); hv_ColumnEdge.Dispose(); hv_Amplitude.Dispose(); hv_DistanceMo1.Dispose();
                            HOperatorSet.MeasurePos(ho_Image, hv_MeasureHandle, 3, 30, "all", "all",
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
                                        0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5,
                                        5);
                                }
                            }
                            else
                            {
                                ho_Arrow1.Dispose();
                                HOperatorSet.GenEmptyObj(out ho_Arrow1);
                                hv_DistanceMo1.Dispose();
                                hv_DistanceMo1 = 0;
                            }

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
                                        0), hv_RowEdge.TupleSelect(1), hv_ColumnEdge.TupleSelect(1), 5,
                                        5);
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


                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region3, out ho_ConnectedRegions);
                    ho_Region3.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_Region3, "max_area",
                        70);

                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                    HOperatorSet.SmallestRectangle2(ho_Region1, out hv_Row, out hv_Column, out hv_Phi,
                        out hv_Length1, out hv_Length2);
                    hv_Line1_X1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_X1 = hv_Column - hv_Length1;
                    }
                    hv_Line1_Y1.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_Y1 = hv_Row + hv_Length2;
                    }
                    hv_Line1_X2.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_X2 = hv_Column + hv_Length1;
                    }
                    hv_Line1_Y2.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Line1_Y2 = hv_Row + hv_Length2;
                    }

                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectLineMeasure(hv_MetrologyHandle, hv_Line1_Y1,
                        hv_Line1_X1, hv_Line1_Y2, hv_Line1_X2, 20, 5, 1, 30, new HTuple(), new HTuple(),
                        out hv_Index1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures",
                        30);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score",
                        0.3);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_select",
                        "first");
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition",
                        "all");
                    HOperatorSet.ApplyMetrologyModel(ho_Image, hv_MetrologyHandle);
                    ho_MeeasureRegion.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_MeeasureRegion, hv_MetrologyHandle,
                        "all", "all", out hv_Row1, out hv_Column1);
                    ho_Line1.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_Line1, hv_Row1, hv_Column1);
                    ho_Cross.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_Cross, hv_Row1, hv_Column1, 6, 0.785398);
                    //get_metrology_object_result_contour (Line1, MetrologyHandle, 0, 'all', 1.5)
                    hv_RowBegin.Dispose(); hv_ColBegin.Dispose(); hv_RowEnd.Dispose(); hv_ColEnd.Dispose(); hv_Nr.Dispose(); hv_Nc.Dispose(); hv_Dist.Dispose();
                    HOperatorSet.FitLineContourXld(ho_Line1, "tukey", -1, 0, 5, 2, out hv_RowBegin,
                        out hv_ColBegin, out hv_RowEnd, out hv_ColEnd, out hv_Nr, out hv_Nc,
                        out hv_Dist);
                    hv_Area.Dispose(); hv_Row4.Dispose(); hv_Column3.Dispose();
                    HOperatorSet.AreaCenter(ho_Region3, out hv_Area, out hv_Row4, out hv_Column3);
                    hv_RowOver.Dispose(); hv_ColumnOver.Dispose(); hv_IsOverlapping.Dispose();
                    HOperatorSet.IntersectionLines(0, hv_Column3, hv_Height, hv_Column3, hv_RowBegin,
                        hv_ColBegin, hv_RowEnd, hv_ColEnd, out hv_RowOver, out hv_ColumnOver,
                        out hv_IsOverlapping);
                    ho_ArrowRongDian.Dispose();
                    gen_arrow_contour_xld(out ho_ArrowRongDian, hv_Row4, hv_Column3, hv_RowOver,
                        hv_ColumnOver, 5, 5);
                    hv_DistanceRongDian.Dispose();
                    HOperatorSet.DistancePl(hv_Row4, hv_Column3, hv_RowBegin, hv_ColBegin, hv_RowEnd,
                        hv_ColEnd, out hv_DistanceRongDian);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ArrowRongDian, ho_Arrow, out ExpTmpOutVar_0);
                        ho_Arrow.Dispose();
                        ho_Arrow = ExpTmpOutVar_0;
                    }
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
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
                ho_ArrowRongDian.Dispose();

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
                ho_ArrowRongDian.Dispose();

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

                throw HDevExpDefaultException;
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
