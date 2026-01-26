using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.EnvelopeTieJiao.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        // Local procedures 
        public static void FindTieJiaoSize(HObject ho_Image, out HObject ho_Lines, out HObject ho_Cross,
            out HTuple hv_DistanceH, out HTuple hv_DistanceW)
        {



            // Local iconic variables 

            HObject ho_Region = null, ho_ConnectedRegions = null;
            HObject ho_SelectedRegions = null, ho_Rectangle = null, ho_Rectangle2 = null;
            HObject ho_Partitioned = null, ho_Partitioned2 = null, ho_ArrowAll = null;
            HObject ho_Contours = null, ho_ObjectSelected = null, ho_ObjectSelected2 = null;
            HObject ho_Cross1H = null, ho_Cross2H = null, ho_ArrowH = null;
            HObject ho_Cross1W = null, ho_Cross2W = null, ho_ArrowW = null;
            HObject ho_Crosstemp1 = null, ho_Crosstemp2 = null;

            // Local control variables 

            HTuple hv_Row = new HTuple(), hv_Column = new HTuple();
            HTuple hv_Phi = new HTuple(), hv_Length1 = new HTuple();
            HTuple hv_Length2 = new HTuple(), hv_Width = new HTuple();
            HTuple hv_Height = new HTuple(), hv_Number = new HTuple();
            HTuple hv_Number2 = new HTuple(), hv_RowEdgeFirstH = new HTuple();
            HTuple hv_ColumnEdgeFirstH = new HTuple(), hv_RowEdgeSecondH = new HTuple();
            HTuple hv_ColumnEdgeSecondH = new HTuple(), hv_RowEdgeFirstW = new HTuple();
            HTuple hv_ColumnEdgeFirstW = new HTuple(), hv_RowEdgeSecondW = new HTuple();
            HTuple hv_ColumnEdgeSecondW = new HTuple(), hv_Min = new HTuple();
            HTuple hv_Index = new HTuple(), hv_Row1 = new HTuple();
            HTuple hv_Column1 = new HTuple(), hv_IsOverlapping = new HTuple();
            HTuple hv_Row2 = new HTuple(), hv_Column2 = new HTuple();
            HTuple hv_Phi2 = new HTuple(), hv_Length12 = new HTuple();
            HTuple hv_Length22 = new HTuple(), hv_Row1_2 = new HTuple();
            HTuple hv_Column1_2 = new HTuple(), hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Lines);
            HOperatorSet.GenEmptyObj(out ho_Cross);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_Rectangle);
            HOperatorSet.GenEmptyObj(out ho_Rectangle2);
            HOperatorSet.GenEmptyObj(out ho_Partitioned);
            HOperatorSet.GenEmptyObj(out ho_Partitioned2);
            HOperatorSet.GenEmptyObj(out ho_ArrowAll);
            HOperatorSet.GenEmptyObj(out ho_Contours);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelected);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelected2);
            HOperatorSet.GenEmptyObj(out ho_Cross1H);
            HOperatorSet.GenEmptyObj(out ho_Cross2H);
            HOperatorSet.GenEmptyObj(out ho_ArrowH);
            HOperatorSet.GenEmptyObj(out ho_Cross1W);
            HOperatorSet.GenEmptyObj(out ho_Cross2W);
            HOperatorSet.GenEmptyObj(out ho_ArrowW);
            HOperatorSet.GenEmptyObj(out ho_Crosstemp1);
            HOperatorSet.GenEmptyObj(out ho_Crosstemp2);
            hv_DistanceH = new HTuple();
            hv_DistanceW = new HTuple();
            try
            {
                try
                {
                    ho_Cross.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Cross);
                    ho_Lines.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Lines);
                    hv_DistanceH.Dispose();
                    hv_DistanceH = new HTuple();
                    hv_DistanceW.Dispose();
                    hv_DistanceW = new HTuple();
                    ho_Region.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region, 1, 255);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Region, out ho_ConnectedRegions);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_SelectedRegions,
                        "max_area", 70);


                    hv_Row.Dispose(); hv_Column.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                    HOperatorSet.SmallestRectangle2(ho_SelectedRegions, out hv_Row, out hv_Column,
                        out hv_Phi, out hv_Length1, out hv_Length2);

                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Rectangle.Dispose();
                        HOperatorSet.GenRectangle2(out ho_Rectangle, hv_Row, hv_Column, 0, 400, (hv_Height / 2) - 10);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Rectangle2.Dispose();
                        HOperatorSet.GenRectangle2(out ho_Rectangle2, hv_Row, hv_Column, 0, (hv_Width / 2) - 10,
                            400);
                    }
                    ho_Partitioned.Dispose();
                    HOperatorSet.PartitionRectangle(ho_Rectangle, out ho_Partitioned, 50, hv_Height);
                    ho_Partitioned2.Dispose();
                    HOperatorSet.PartitionRectangle(ho_Rectangle2, out ho_Partitioned2, hv_Width,
                        50);
                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_Partitioned, out hv_Number);
                    hv_Number2.Dispose();
                    HOperatorSet.CountObj(ho_Partitioned2, out hv_Number2);

                    hv_RowEdgeFirstH.Dispose();
                    hv_RowEdgeFirstH = new HTuple();
                    hv_ColumnEdgeFirstH.Dispose();
                    hv_ColumnEdgeFirstH = new HTuple();
                    hv_RowEdgeSecondH.Dispose();
                    hv_RowEdgeSecondH = new HTuple();
                    hv_ColumnEdgeSecondH.Dispose();
                    hv_ColumnEdgeSecondH = new HTuple();

                    hv_RowEdgeFirstW.Dispose();
                    hv_RowEdgeFirstW = new HTuple();
                    hv_ColumnEdgeFirstW.Dispose();
                    hv_ColumnEdgeFirstW = new HTuple();
                    hv_RowEdgeSecondW.Dispose();
                    hv_RowEdgeSecondW = new HTuple();
                    hv_ColumnEdgeSecondW.Dispose();
                    hv_ColumnEdgeSecondW = new HTuple();
                    ho_ArrowAll.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_ArrowAll);
                    ho_Contours.Dispose();
                    HOperatorSet.GenContourRegionXld(ho_SelectedRegions, out ho_Contours, "border");
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Min.Dispose();
                        HOperatorSet.TupleMin(hv_Number.TupleConcat(hv_Number2), out hv_Min);
                    }
                    HTuple end_val33 = hv_Min;
                    HTuple step_val33 = 1;
                    for (hv_Index = 1; hv_Index.Continue(end_val33, step_val33); hv_Index = hv_Index.TupleAdd(step_val33))
                    {
                        //*高度测量
                        ho_ObjectSelected.Dispose();
                        HOperatorSet.SelectObj(ho_Partitioned, out ho_ObjectSelected, hv_Index);
                        hv_Row.Dispose(); hv_Column.Dispose(); hv_Phi.Dispose(); hv_Length1.Dispose(); hv_Length2.Dispose();
                        HOperatorSet.SmallestRectangle2(ho_ObjectSelected, out hv_Row, out hv_Column,
                            out hv_Phi, out hv_Length1, out hv_Length2);
                        hv_Row1.Dispose(); hv_Column1.Dispose(); hv_IsOverlapping.Dispose();
                        HOperatorSet.IntersectionLineContourXld(ho_Contours, 0, hv_Column, hv_Height,
                            hv_Column, out hv_Row1, out hv_Column1, out hv_IsOverlapping);

                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_RowEdgeFirstH = hv_RowEdgeFirstH.TupleConcat(
                                    hv_Row1.TupleSelect(0));
                                hv_RowEdgeFirstH.Dispose();
                                hv_RowEdgeFirstH = ExpTmpLocalVar_RowEdgeFirstH;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_ColumnEdgeFirstH = hv_ColumnEdgeFirstH.TupleConcat(
                                    hv_Column1.TupleSelect(0));
                                hv_ColumnEdgeFirstH.Dispose();
                                hv_ColumnEdgeFirstH = ExpTmpLocalVar_ColumnEdgeFirstH;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_RowEdgeSecondH = hv_RowEdgeSecondH.TupleConcat(
                                    hv_Row1.TupleSelect((new HTuple(hv_Row1.TupleLength())) - 1));
                                hv_RowEdgeSecondH.Dispose();
                                hv_RowEdgeSecondH = ExpTmpLocalVar_RowEdgeSecondH;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_ColumnEdgeSecondH = hv_ColumnEdgeSecondH.TupleConcat(
                                    hv_Column1.TupleSelect((new HTuple(hv_Row1.TupleLength())) - 1));
                                hv_ColumnEdgeSecondH.Dispose();
                                hv_ColumnEdgeSecondH = ExpTmpLocalVar_ColumnEdgeSecondH;
                            }
                        }

                        //*宽度测量
                        ho_ObjectSelected2.Dispose();
                        HOperatorSet.SelectObj(ho_Partitioned2, out ho_ObjectSelected2, hv_Index);
                        hv_Row2.Dispose(); hv_Column2.Dispose(); hv_Phi2.Dispose(); hv_Length12.Dispose(); hv_Length22.Dispose();
                        HOperatorSet.SmallestRectangle2(ho_ObjectSelected2, out hv_Row2, out hv_Column2,
                            out hv_Phi2, out hv_Length12, out hv_Length22);
                        hv_Row1_2.Dispose(); hv_Column1_2.Dispose(); hv_IsOverlapping.Dispose();
                        HOperatorSet.IntersectionLineContourXld(ho_Contours, hv_Row2, 0, hv_Row2,
                            hv_Width, out hv_Row1_2, out hv_Column1_2, out hv_IsOverlapping);

                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_RowEdgeFirstW = hv_RowEdgeFirstW.TupleConcat(
                                    hv_Row1_2.TupleSelect(0));
                                hv_RowEdgeFirstW.Dispose();
                                hv_RowEdgeFirstW = ExpTmpLocalVar_RowEdgeFirstW;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_ColumnEdgeFirstW = hv_ColumnEdgeFirstW.TupleConcat(
                                    hv_Column1_2.TupleSelect(0));
                                hv_ColumnEdgeFirstW.Dispose();
                                hv_ColumnEdgeFirstW = ExpTmpLocalVar_ColumnEdgeFirstW;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_RowEdgeSecondW = hv_RowEdgeSecondW.TupleConcat(
                                    hv_Row1_2.TupleSelect((new HTuple(hv_Row1_2.TupleLength())) - 1));
                                hv_RowEdgeSecondW.Dispose();
                                hv_RowEdgeSecondW = ExpTmpLocalVar_RowEdgeSecondW;
                            }
                        }
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            {
                                HTuple
                                  ExpTmpLocalVar_ColumnEdgeSecondW = hv_ColumnEdgeSecondW.TupleConcat(
                                    hv_Column1_2.TupleSelect((new HTuple(hv_Row1_2.TupleLength())) - 1));
                                hv_ColumnEdgeSecondW.Dispose();
                                hv_ColumnEdgeSecondW = ExpTmpLocalVar_ColumnEdgeSecondW;
                            }
                        }
                    }

                    hv_DistanceH.Dispose();
                    HOperatorSet.DistancePp(hv_RowEdgeFirstH, hv_ColumnEdgeFirstH, hv_RowEdgeSecondH,
                        hv_ColumnEdgeSecondH, out hv_DistanceH);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross1H.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross1H, hv_RowEdgeFirstH, hv_ColumnEdgeFirstH,
                            26, (new HTuple(45)).TupleRad());
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross2H.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross2H, hv_RowEdgeSecondH, hv_ColumnEdgeSecondH,
                            26, (new HTuple(45)).TupleRad());
                    }
                    ho_ArrowH.Dispose();
                    gen_arrow_contour_xld(out ho_ArrowH, hv_RowEdgeFirstH, hv_ColumnEdgeFirstH,
                        hv_RowEdgeSecondH, hv_ColumnEdgeSecondH, 5, 5);
                    hv_DistanceW.Dispose();
                    HOperatorSet.DistancePp(hv_RowEdgeFirstW, hv_ColumnEdgeFirstW, hv_RowEdgeSecondW,
                        hv_ColumnEdgeSecondW, out hv_DistanceW);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross1W.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross1W, hv_RowEdgeFirstW, hv_ColumnEdgeFirstW,
                            26, (new HTuple(45)).TupleRad());
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Cross2W.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_Cross2W, hv_RowEdgeSecondW, hv_ColumnEdgeSecondW,
                            26, (new HTuple(45)).TupleRad());
                    }
                    ho_ArrowW.Dispose();
                    gen_arrow_contour_xld(out ho_ArrowW, hv_RowEdgeFirstW, hv_ColumnEdgeFirstW,
                        hv_RowEdgeSecondW, hv_ColumnEdgeSecondW, 5, 5);

                    ho_Lines.Dispose();
                    HOperatorSet.ConcatObj(ho_ArrowW, ho_ArrowH, out ho_Lines);
                    ho_Crosstemp1.Dispose();
                    HOperatorSet.ConcatObj(ho_Cross1H, ho_Cross2H, out ho_Crosstemp1);
                    ho_Crosstemp2.Dispose();
                    HOperatorSet.ConcatObj(ho_Cross1W, ho_Cross2W, out ho_Crosstemp2);
                    ho_Cross.Dispose();
                    HOperatorSet.ConcatObj(ho_Crosstemp1, ho_Crosstemp2, out ho_Cross);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    throw new HalconException(hv_Exception);
                }

                ho_Region.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();
                ho_Rectangle.Dispose();
                ho_Rectangle2.Dispose();
                ho_Partitioned.Dispose();
                ho_Partitioned2.Dispose();
                ho_ArrowAll.Dispose();
                ho_Contours.Dispose();
                ho_ObjectSelected.Dispose();
                ho_ObjectSelected2.Dispose();
                ho_Cross1H.Dispose();
                ho_Cross2H.Dispose();
                ho_ArrowH.Dispose();
                ho_Cross1W.Dispose();
                ho_Cross2W.Dispose();
                ho_ArrowW.Dispose();
                ho_Crosstemp1.Dispose();
                ho_Crosstemp2.Dispose();

                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_Number.Dispose();
                hv_Number2.Dispose();
                hv_RowEdgeFirstH.Dispose();
                hv_ColumnEdgeFirstH.Dispose();
                hv_RowEdgeSecondH.Dispose();
                hv_ColumnEdgeSecondH.Dispose();
                hv_RowEdgeFirstW.Dispose();
                hv_ColumnEdgeFirstW.Dispose();
                hv_RowEdgeSecondW.Dispose();
                hv_ColumnEdgeSecondW.Dispose();
                hv_Min.Dispose();
                hv_Index.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_IsOverlapping.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Phi2.Dispose();
                hv_Length12.Dispose();
                hv_Length22.Dispose();
                hv_Row1_2.Dispose();
                hv_Column1_2.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Region.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();
                ho_Rectangle.Dispose();
                ho_Rectangle2.Dispose();
                ho_Partitioned.Dispose();
                ho_Partitioned2.Dispose();
                ho_ArrowAll.Dispose();
                ho_Contours.Dispose();
                ho_ObjectSelected.Dispose();
                ho_ObjectSelected2.Dispose();
                ho_Cross1H.Dispose();
                ho_Cross2H.Dispose();
                ho_ArrowH.Dispose();
                ho_Cross1W.Dispose();
                ho_Cross2W.Dispose();
                ho_ArrowW.Dispose();
                ho_Crosstemp1.Dispose();
                ho_Crosstemp2.Dispose();

                hv_Row.Dispose();
                hv_Column.Dispose();
                hv_Phi.Dispose();
                hv_Length1.Dispose();
                hv_Length2.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_Number.Dispose();
                hv_Number2.Dispose();
                hv_RowEdgeFirstH.Dispose();
                hv_ColumnEdgeFirstH.Dispose();
                hv_RowEdgeSecondH.Dispose();
                hv_ColumnEdgeSecondH.Dispose();
                hv_RowEdgeFirstW.Dispose();
                hv_ColumnEdgeFirstW.Dispose();
                hv_RowEdgeSecondW.Dispose();
                hv_ColumnEdgeSecondW.Dispose();
                hv_Min.Dispose();
                hv_Index.Dispose();
                hv_Row1.Dispose();
                hv_Column1.Dispose();
                hv_IsOverlapping.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Phi2.Dispose();
                hv_Length12.Dispose();
                hv_Length22.Dispose();
                hv_Row1_2.Dispose();
                hv_Column1_2.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }


        public static void FindTieJiao(HImage hImage,out HObject Lines,out HObject Cross,out double DistanceH,out double DistanceW)
        {
            Lines = new HObject(); Lines.GenEmptyObj();
            Cross = new HObject(); Cross.GenEmptyObj();
            HRegion region1 = new HRegion(); region1.GenEmptyObj();
            HRegion region2 = new HRegion(); region2.GenEmptyObj();
            HXLDCont Contours = new HXLDCont(); Contours.GenEmptyObj();
            DistanceH = 0;
            DistanceW = 0;
            hImage.Threshold(1.0, 1.0).Connection().SelectShapeStd("max_area", 70).SmallestRectangle2(out double Row, out double Column,
                        out double Phi, out double Length1, out double Length2);
            Contours = hImage.Threshold(1.0, 1.0).Connection().SelectShapeStd("max_area", 70).GenContourRegionXld("border");
            hImage.GetImageSize(out int width,out int height);
            region1.GenRectangle2(Row, Column, 0, 400, height/2 - 10);
            region1 = region1.PartitionRectangle(50, height - 10);
            region2.GenRectangle2(Row, Column, 0, width/2 - 10,400);
            region2 = region2.PartitionRectangle(width-10, 50);
            int min = Math.Min(region1.CountObj(), region2.CountObj());
            int countH = 0 ,countW = 0;
            for (int i = 1; i <= min; i++)
            {
                region1.SelectObj(i).SmallestRectangle2(out double HRow, out double HColumn, out double Hphi, out double HLength1, out double HLength2);
                HOperatorSet.IntersectionLineContourXld(Contours, 0, HColumn, height, HColumn, out HTuple Row1, out HTuple Column1, out HTuple IsOverlapping);
                if (Row1.Length >= 2)
                {
                    int t1 = Row1.Length-1;
                    HOperatorSet.DistancePp(Row1[0], Column1[0], Row1[t1], Column1[t1], out HTuple distance1);
                    DistanceH += distance1;
                    countH++;
                    gen_arrow_contour_xld(out HObject ho_ArrowH, Row1[0], Column1[0], Row1[t1], Column1[t1], 5, 5);
                    HOperatorSet.GenCrossContourXld(out HObject HCross, Row1, Column1, 26, (new HTuple(45)).TupleRad());
                    Lines = Lines.ConcatObj(ho_ArrowH);
                    Cross = Cross.ConcatObj(HCross);
                }
                //region2.WriteObject(@"C:\Users\Administrator\Desktop\ai\tiejiao_2025_11_14_jmyw_openvino_model\region.hobj");
                region2.SelectObj(i).SmallestRectangle2(out double WRow, out double WColumn, out double Wphi, out double WLength1, out double WLength2);
                HOperatorSet.IntersectionLineContourXld(Contours, WRow, 0, WRow, width, out HTuple Row2, out HTuple Column2, out HTuple IsOverlapping2);
                if (Row2.Length >= 2)
                {
                    int t2 = Row2.Length - 1;
                    HOperatorSet.DistancePp(Row2[0], Column2[0], Row2[t2], Column2[t2], out HTuple distance2);
                    DistanceW += distance2;
                    countW++;
                    gen_arrow_contour_xld(out HObject ho_ArrowW, Row2[0], Column2[0], Row2[t2], Column2[t2], 5, 5);
                    HOperatorSet.GenCrossContourXld(out HObject WCross, Row2, Column2, 26, (new HTuple(45)).TupleRad());
                    Lines = Lines.ConcatObj(ho_ArrowW);
                    Cross = Cross.ConcatObj(WCross);
                }

                DistanceH = DistanceH / countH;
                DistanceW = DistanceW/ countW;
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
