using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.SealingPin.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        public static void FindSealingPinCircle(HObject ho_Image, out HObject ho_Cross, out HObject ho_FindRegions,
            out HObject ho_FindContours, out HTuple hv_CircleRow, out HTuple hv_CircleCol)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Region = null, ho_RegionFillUp = null;
            HObject ho_ConnectedRegions = null, ho_CircleRegion1 = null;
            HObject ho_ImageCleared = null, ho_ImageResult1 = null, ho_Contours1 = null;
            HObject ho_Contour1 = null, ho_Cross1 = null, ho_Region2 = null;
            HObject ho_CircleRegion2 = null, ho_ImageResult2 = null, ho_Contours2 = null;
            HObject ho_Contour2 = null, ho_Cross2 = null;

            // Local control variables 

            HTuple hv_MetrologyHandle = new HTuple(), hv_Number = new HTuple();
            HTuple hv_Row1 = new HTuple(), hv_Column1 = new HTuple();
            HTuple hv_Radius1 = new HTuple(), hv_Index1 = new HTuple();
            HTuple hv_Row = new HTuple(), hv_Column = new HTuple();
            HTuple hv_Radius = new HTuple(), hv_StartPhi = new HTuple();
            HTuple hv_EndPhi = new HTuple(), hv_PointOrder = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_FindRegions);
            HOperatorSet.GenEmptyObj(out ho_FindContours);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_CircleRegion1);
            HOperatorSet.GenEmptyObj(out ho_ImageCleared);
            HOperatorSet.GenEmptyObj(out ho_ImageResult1);
            HOperatorSet.GenEmptyObj(out ho_Contours1);
            HOperatorSet.GenEmptyObj(out ho_Contour1);
            HOperatorSet.GenEmptyObj(out ho_Cross1);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            HOperatorSet.GenEmptyObj(out ho_CircleRegion2);
            HOperatorSet.GenEmptyObj(out ho_ImageResult2);
            HOperatorSet.GenEmptyObj(out ho_Contours2);
            HOperatorSet.GenEmptyObj(out ho_Contour2);
            HOperatorSet.GenEmptyObj(out ho_Cross2);
            hv_CircleRow = new HTuple();
            hv_CircleCol = new HTuple();
            try
            {
                try
                {
                    hv_CircleRow.Dispose();
                    hv_CircleRow = new HTuple();
                    hv_CircleCol.Dispose();
                    hv_CircleCol = new HTuple();
                    ho_Cross.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Cross);
                    ho_FindRegions.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_FindRegions);
                    ho_FindContours.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_FindContours);

                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    ho_Region.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region, 1, 1);
                    ho_RegionFillUp.Dispose();
                    HOperatorSet.FillUp(ho_Region, out ho_RegionFillUp);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_RegionFillUp, out ho_ConnectedRegions);
                    ho_CircleRegion1.Dispose();
                    HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_CircleRegion1, "roundness",
                        "and", 0.9, 1);
                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_CircleRegion1, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleGreater(1))) != 0)
                    {
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShapeStd(ho_CircleRegion1, out ExpTmpOutVar_0, "max_area",
                                70);
                            ho_CircleRegion1.Dispose();
                            ho_CircleRegion1 = ExpTmpOutVar_0;
                        }
                    }
                    hv_Row1.Dispose(); hv_Column1.Dispose(); hv_Radius1.Dispose();
                    HOperatorSet.SmallestCircle(ho_CircleRegion1, out hv_Row1, out hv_Column1,
                        out hv_Radius1);
                    ho_ImageCleared.Dispose();
                    HOperatorSet.GenImageProto(ho_Image, out ho_ImageCleared, 0);
                    ho_ImageResult1.Dispose();
                    HOperatorSet.PaintRegion(ho_CircleRegion1, ho_ImageCleared, out ho_ImageResult1,
                        255, "fill");
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row1,
                        hv_Column1, hv_Radius1, 50, 5, 1, 30, new HTuple(), new HTuple(), out hv_Index1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures",
                        20);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score",
                        0.1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition",
                        "negative");
                    HOperatorSet.ApplyMetrologyModel(ho_ImageResult1, hv_MetrologyHandle);
                    ho_Contours1.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_Contours1, hv_MetrologyHandle,
                        "all", "all", out hv_Row1, out hv_Column1);
                    ho_Contour1.Dispose();
                    HOperatorSet.GetMetrologyObjectResultContour(out ho_Contour1, hv_MetrologyHandle,
                        0, "all", 1.5);
                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose(); hv_StartPhi.Dispose(); hv_EndPhi.Dispose(); hv_PointOrder.Dispose();
                    HOperatorSet.FitCircleContourXld(ho_Contour1, "algebraic", -1, 0, 0, 3, 2,
                        out hv_Row, out hv_Column, out hv_Radius, out hv_StartPhi, out hv_EndPhi,
                        out hv_PointOrder);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross1.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross1, hv_Row, hv_Column, 26, (new HTuple(45)).TupleRad()
                            );
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_Cross, ho_Cross1, out ExpTmpOutVar_0);
                        ho_Cross.Dispose();
                        ho_Cross = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_FindRegions, ho_Contours1, out ExpTmpOutVar_0);
                        ho_FindRegions.Dispose();
                        ho_FindRegions = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_FindContours, ho_Contour1, out ExpTmpOutVar_0);
                        ho_FindContours.Dispose();
                        ho_FindContours = ExpTmpOutVar_0;
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_CircleRow = hv_CircleRow.TupleConcat(
                                hv_Row);
                            hv_CircleRow.Dispose();
                            hv_CircleRow = ExpTmpLocalVar_CircleRow;
                        }
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_CircleCol = hv_CircleCol.TupleConcat(
                                hv_Column);
                            hv_CircleCol.Dispose();
                            hv_CircleCol = ExpTmpLocalVar_CircleCol;
                        }
                    }
                    ho_Region2.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region2, 2, 2);
                    ho_RegionFillUp.Dispose();
                    HOperatorSet.FillUp(ho_Region2, out ho_RegionFillUp);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_RegionFillUp, out ho_ConnectedRegions);
                    ho_CircleRegion2.Dispose();
                    HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_CircleRegion2, "roundness",
                        "and", 0.9, 1);
                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_CircleRegion2, out hv_Number);
                    if ((int)(new HTuple(hv_Number.TupleGreater(1))) != 0)
                    {
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.SelectShapeStd(ho_CircleRegion2, out ExpTmpOutVar_0, "max_area",
                                70);
                            ho_CircleRegion2.Dispose();
                            ho_CircleRegion2 = ExpTmpOutVar_0;
                        }
                    }
                    hv_Row1.Dispose(); hv_Column1.Dispose(); hv_Radius1.Dispose();
                    HOperatorSet.SmallestCircle(ho_CircleRegion2, out hv_Row1, out hv_Column1,
                        out hv_Radius1);
                    ho_ImageCleared.Dispose();
                    HOperatorSet.GenImageProto(ho_Image, out ho_ImageCleared, 0);
                    ho_ImageResult2.Dispose();
                    HOperatorSet.PaintRegion(ho_CircleRegion2, ho_ImageCleared, out ho_ImageResult2,
                        255, "fill");
                    hv_MetrologyHandle.Dispose();
                    HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
                    hv_Index1.Dispose();
                    HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row1,
                        hv_Column1, hv_Radius1, 50, 5, 1, 30, new HTuple(), new HTuple(), out hv_Index1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "num_measures",
                        20);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "min_score",
                        0.1);
                    HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, "all", "measure_transition",
                        "negative");
                    HOperatorSet.ApplyMetrologyModel(ho_ImageResult2, hv_MetrologyHandle);
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_Image, HDevWindowStack.GetActive());
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_Contour1, HDevWindowStack.GetActive());
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_Cross1, HDevWindowStack.GetActive());
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_Contours1, HDevWindowStack.GetActive());
                    }
                    ho_Contours2.Dispose(); hv_Row1.Dispose(); hv_Column1.Dispose();
                    HOperatorSet.GetMetrologyObjectMeasures(out ho_Contours2, hv_MetrologyHandle,
                        "all", "all", out hv_Row1, out hv_Column1);

                    ho_Contour2.Dispose();
                    HOperatorSet.GetMetrologyObjectResultContour(out ho_Contour2, hv_MetrologyHandle,
                        0, "all", 1.5);
                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Radius.Dispose(); hv_StartPhi.Dispose(); hv_EndPhi.Dispose(); hv_PointOrder.Dispose();
                    HOperatorSet.FitCircleContourXld(ho_Contour2, "algebraic", -1, 0, 0, 3, 2,
                        out hv_Row, out hv_Column, out hv_Radius, out hv_StartPhi, out hv_EndPhi,
                        out hv_PointOrder);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross2.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross2, hv_Row, hv_Column, 26, (new HTuple(45)).TupleRad()
                            );
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_Cross, ho_Cross2, out ExpTmpOutVar_0);
                        ho_Cross.Dispose();
                        ho_Cross = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_FindRegions, ho_Contours2, out ExpTmpOutVar_0);
                        ho_FindRegions.Dispose();
                        ho_FindRegions = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_FindContours, ho_Contour2, out ExpTmpOutVar_0);
                        ho_FindContours.Dispose();
                        ho_FindContours = ExpTmpOutVar_0;
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_CircleRow = hv_CircleRow.TupleConcat(
                                hv_Row);
                            hv_CircleRow.Dispose();
                            hv_CircleRow = ExpTmpLocalVar_CircleRow;
                        }
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_CircleCol = hv_CircleCol.TupleConcat(
                                hv_Column);
                            hv_CircleCol.Dispose();
                            hv_CircleCol = ExpTmpLocalVar_CircleCol;
                        }
                    }
                    ho_Region.Dispose();
                    ho_RegionFillUp.Dispose();
                    ho_ConnectedRegions.Dispose();
                    ho_CircleRegion1.Dispose();
                    ho_ImageCleared.Dispose();
                    ho_ImageResult1.Dispose();
                    ho_Contours1.Dispose();
                    ho_Contour1.Dispose();
                    ho_Cross1.Dispose();
                    ho_Region2.Dispose();
                    ho_CircleRegion2.Dispose();
                    ho_ImageResult2.Dispose();
                    ho_Contours2.Dispose();
                    ho_Contour2.Dispose();
                    ho_Cross2.Dispose();

                    hv_MetrologyHandle.Dispose();
                    hv_Number.Dispose();
                    hv_Row1.Dispose();
                    hv_Column1.Dispose();
                    hv_Radius1.Dispose();
                    hv_Index1.Dispose();
                    hv_Row.Dispose();
                    hv_Column.Dispose();
                    hv_Radius.Dispose();
                    hv_StartPhi.Dispose();
                    hv_EndPhi.Dispose();
                    hv_PointOrder.Dispose();
                    hv_Exception.Dispose();

                    return;
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                }

            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region.Dispose();
                ho_RegionFillUp.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_CircleRegion1.Dispose();
                ho_ImageCleared.Dispose();
                ho_ImageResult1.Dispose();
                ho_Contours1.Dispose();
                ho_Contour1.Dispose();
                ho_Cross1.Dispose();
                ho_Region2.Dispose();
                ho_CircleRegion2.Dispose();
                ho_ImageResult2.Dispose();
                ho_Contours2.Dispose();
                ho_Contour2.Dispose();
                ho_Cross2.Dispose();

                hv_MetrologyHandle.Dispose();
                hv_Number.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_Radius1.Dispose();
                hv_Index1.Dispose();
                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_Radius.Dispose();
                hv_StartPhi.Dispose();
                hv_EndPhi.Dispose();
                hv_PointOrder.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }
    }
}
