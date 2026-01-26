using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.SealingPinHanHou.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        public static void Mfd_HanHou(HObject ho_Image, out HObject ho_Arrow, out HTuple hv_Pianwei,
            out HTuple hv_Distance)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_Region1, ho_Region2, ho_Region3;
            HObject ho_Region4, ho_RegionFillUp1, ho_RegionFillUp2;
            HObject ho_RegionDifference, ho_ImageCleared, ho_RegionFillUp;
            HObject ho_RegionFillUp4, ho_RegionDifference1, ho_RegionClosing;
            HObject ho_Contours;

            // Local control variables 

            HTuple hv_Row1 = new HTuple(), hv_Column1 = new HTuple();
            HTuple hv_Radius1 = new HTuple(), hv_Row2 = new HTuple();
            HTuple hv_Column2 = new HTuple(), hv_Radius2 = new HTuple();
            HTuple hv_MetrologyHandle = new HTuple(), hv_Index1 = new HTuple();
            HTuple hv_Index2 = new HTuple(), hv_Row = new HTuple();
            HTuple hv_Column = new HTuple(), hv_Sequence1 = new HTuple();
            HTuple hv_Sequence2 = new HTuple(), hv_fRow1 = new HTuple();
            HTuple hv_fColumn1 = new HTuple(), hv_fRow2 = new HTuple();
            HTuple hv_fColumn2 = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Arrow);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            HOperatorSet.GenEmptyObj(out ho_Region3);
            HOperatorSet.GenEmptyObj(out ho_Region4);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp1);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp2);
            HOperatorSet.GenEmptyObj(out ho_RegionDifference);
            HOperatorSet.GenEmptyObj(out ho_ImageCleared);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp4);
            HOperatorSet.GenEmptyObj(out ho_RegionDifference1);
            HOperatorSet.GenEmptyObj(out ho_RegionClosing);
            HOperatorSet.GenEmptyObj(out ho_Contours);
            hv_Pianwei = new HTuple();
            hv_Distance = new HTuple();
            ho_Region1.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region1, 1, 1);
            ho_Region2.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region2, 2, 255);
            ho_Region3.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region3, 3, 255);
            ho_Region4.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region4, 4, 4);


            ho_RegionFillUp1.Dispose();
            HOperatorSet.FillUp(ho_Region1, out ho_RegionFillUp1);
            ho_RegionFillUp2.Dispose();
            HOperatorSet.FillUp(ho_Region2, out ho_RegionFillUp2);
            ho_RegionDifference.Dispose();
            HOperatorSet.Difference(ho_RegionFillUp2, ho_RegionFillUp1, out ho_RegionDifference
                );
            hv_Pianwei.Dispose();
            HOperatorSet.RegionFeatures(ho_RegionDifference, "area", out hv_Pianwei);

            ho_ImageCleared.Dispose();
            HOperatorSet.GenImageProto(ho_Image, out ho_ImageCleared, 0);
            ho_RegionFillUp.Dispose();
            HOperatorSet.FillUp(ho_Region3, out ho_RegionFillUp);
            ho_RegionFillUp4.Dispose();
            HOperatorSet.FillUp(ho_Region4, out ho_RegionFillUp4);
            ho_RegionDifference1.Dispose();
            HOperatorSet.Difference(ho_RegionFillUp, ho_RegionFillUp4, out ho_RegionDifference1
                );
            ho_RegionClosing.Dispose();
            HOperatorSet.ClosingCircle(ho_RegionDifference1, out ho_RegionClosing, 1);
            {
                HObject ExpTmpOutVar_0;
                HOperatorSet.PaintRegion(ho_RegionClosing, ho_ImageCleared, out ExpTmpOutVar_0,
                    255, "fill");
                ho_ImageCleared.Dispose();
                ho_ImageCleared = ExpTmpOutVar_0;
            }
            hv_Row1.Dispose(); hv_Column1.Dispose(); hv_Radius1.Dispose();
            HOperatorSet.SmallestCircle(ho_Region3, out hv_Row1, out hv_Column1, out hv_Radius1);
            hv_Row2.Dispose(); hv_Column2.Dispose(); hv_Radius2.Dispose();
            HOperatorSet.SmallestCircle(ho_Region4, out hv_Row2, out hv_Column2, out hv_Radius2);
            hv_MetrologyHandle.Dispose();
            HOperatorSet.CreateMetrologyModel(out hv_MetrologyHandle);
            using (HDevDisposeHelper dh = new HDevDisposeHelper())
            {
                hv_Index1.Dispose();
                HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row2, hv_Column2,
                    (hv_Radius1 + hv_Radius2) / 2, 200, 5, 1, 50, (new HTuple("num_measures")).TupleConcat(
                    "min_score"), (new HTuple(20)).TupleConcat(0.1), out hv_Index1);
            }
            using (HDevDisposeHelper dh = new HDevDisposeHelper())
            {
                hv_Index2.Dispose();
                HOperatorSet.AddMetrologyObjectCircleMeasure(hv_MetrologyHandle, hv_Row2, hv_Column2,
                    (hv_Radius1 + hv_Radius2) / 2, 200, 5, 1, 50, (new HTuple("num_measures")).TupleConcat(
                    "min_score"), (new HTuple(20)).TupleConcat(0.1), out hv_Index2);
            }
            HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index1, "measure_select",
                "first");
            HOperatorSet.SetMetrologyObjectParam(hv_MetrologyHandle, hv_Index2, "measure_select",
                "last");
            //reduce_domain (ImageCleared, RegionClosing, ImageReduced)
            HOperatorSet.ApplyMetrologyModel(ho_ImageCleared, hv_MetrologyHandle);
            ho_Contours.Dispose(); hv_Row.Dispose(); hv_Column.Dispose();
            HOperatorSet.GetMetrologyObjectMeasures(out ho_Contours, hv_MetrologyHandle,
                "all", "all", out hv_Row, out hv_Column);
            using (HDevDisposeHelper dh = new HDevDisposeHelper())
            {
                hv_Sequence1.Dispose();
                HOperatorSet.TupleGenSequence(0, ((new HTuple(hv_Row.TupleLength())) / 2) - 1, 1,
                    out hv_Sequence1);
            }
            using (HDevDisposeHelper dh = new HDevDisposeHelper())
            {
                hv_Sequence2.Dispose();
                HOperatorSet.TupleGenSequence((new HTuple(hv_Row.TupleLength())) / 2, (new HTuple(hv_Row.TupleLength()
                    )) - 1, 1, out hv_Sequence2);
            }
            hv_fRow1.Dispose();
            HOperatorSet.TupleSelect(hv_Row, hv_Sequence1, out hv_fRow1);
            hv_fColumn1.Dispose();
            HOperatorSet.TupleSelect(hv_Column, hv_Sequence1, out hv_fColumn1);
            hv_fRow2.Dispose();
            HOperatorSet.TupleSelect(hv_Row, hv_Sequence2, out hv_fRow2);
            hv_fColumn2.Dispose();
            HOperatorSet.TupleSelect(hv_Column, hv_Sequence2, out hv_fColumn2);
            if (HDevWindowStack.IsOpen())
            {
                //dev_display (Image)
            }
            //gen_cross_contour_xld (Cross, [fRow1,fRow2], [fColumn1,fColumn2], 36, 0.785398)
            ho_Arrow.Dispose();
            HOperatorSet.TupleConcat(hv_fRow1, hv_fRow2, out HTuple fRow);
            HOperatorSet.TupleConcat(hv_fColumn1, hv_fColumn2, out HTuple fColumn);
            HOperatorSet.GenContourPolygonXld(out ho_Arrow, fRow, fColumn);
            hv_Distance.Dispose();
            HOperatorSet.DistancePp(hv_fRow1, hv_fColumn1, hv_fRow2, hv_fColumn2, out hv_Distance);
            ho_Region1.Dispose();
            ho_Region2.Dispose();
            ho_Region3.Dispose();
            ho_Region4.Dispose();
            ho_RegionFillUp1.Dispose();
            ho_RegionFillUp2.Dispose();
            ho_RegionDifference.Dispose();
            ho_ImageCleared.Dispose();
            ho_RegionFillUp.Dispose();
            ho_RegionFillUp4.Dispose();
            ho_RegionDifference1.Dispose();
            ho_RegionClosing.Dispose();
            ho_Contours.Dispose();

            hv_Row1.Dispose();
            hv_Column1.Dispose();
            hv_Radius1.Dispose();
            hv_Row2.Dispose();
            hv_Column2.Dispose();
            hv_Radius2.Dispose();
            hv_MetrologyHandle.Dispose();
            hv_Index1.Dispose();
            hv_Index2.Dispose();
            hv_Row.Dispose();
            hv_Column.Dispose();
            hv_Sequence1.Dispose();
            hv_Sequence2.Dispose();
            hv_fRow1.Dispose();
            hv_fColumn1.Dispose();
            hv_fRow2.Dispose();
            hv_fColumn2.Dispose();

            return;
        }
    }
}
