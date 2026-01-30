using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.Solder.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        public static void jierhansuanfa(HObject ho_Image, out HObject ho_Region1, out HObject ho_Region2,
            out HObject ho_ho_CrossPreUpper, out HObject ho_ho_CrossPreLower, out HObject ho_ho_CrossFinalUpper,
            out HObject ho_ho_CrossFinalLower, out HObject ho_ho_CrossPreLeft, out HObject ho_ho_CrossPreRight,
            out HObject ho_ho_CrossFinalLeft, out HObject ho_ho_CrossFinalRight, out HObject ho_ho_UpperGapLine,
            out HObject ho_ho_LowerGapLine, out HObject ho_ho_LeftGapLine, out HObject ho_ho_RightGapLine,
            out HObject ho_ho_Preweld_Contour, out HObject ho_ho_Finalweld_Contour, out HTuple hv_Offset,
            out HTuple hv_UpperGaps, out HTuple hv_LowerGaps, out HTuple hv_RightGaps, out HTuple hv_LiftGaps,
            out HTuple hv_AreaP_Tuple, out HTuple hv_AreaF_Tuple, out HTuple hv_RowP_Tuple,
            out HTuple hv_ColumnP_Tuple, out HTuple hv_RowF_Tuple, out HTuple hv_ColumnF_Tuple,
            out HTuple hv_WidthP_Tuple, out HTuple hv_HeightP_Tuple, out HTuple hv_WidthF_Tuple,
            out HTuple hv_HeightF_Tuple)
        {



            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_ConnectedRegions1, ho_RegionFillUp1;
            HObject ho_AreaFilteredRegions1, ho_SortedRegions1, ho_ConnectedRegions2;
            HObject ho_RegionFillUp2, ho_SortedRegions2, ho_Preweld_Region = null;
            HObject ho_Finalweld_Region = null, ho_Preweld_Contour_single = null;
            HObject ho_Finalweld_Contour_single = null, ho_LineXLD = null;
            HObject ho_CrossPreUpper_single = null, ho_CrossPreLower_single = null;
            HObject ho_CrossFinalUpper_single = null, ho_CrossFinalLower_single = null;
            HObject ho_UpperGapLine_single = null, ho_LowerGapLine_single = null;
            HObject ho_HorizontalLineXLD = null, ho_CrossPreLeft_single = null;
            HObject ho_CrossPreRight_single = null, ho_CrossFinalLeft_single = null;
            HObject ho_CrossFinalRight_single = null, ho_LeftGapLine_single = null;
            HObject ho_RightGapLine_single = null;

            // Local control variables 

            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_Preweld_Num = new HTuple(), hv_Finalweld_Num = new HTuple();
            HTuple hv_Num_Pairs = new HTuple(), hv_i = new HTuple();
            HTuple hv_AreaP = new HTuple(), hv_RowP = new HTuple();
            HTuple hv_ColumnP = new HTuple(), hv_RowP_min = new HTuple();
            HTuple hv_ColumnP_min = new HTuple(), hv_RowP_max = new HTuple();
            HTuple hv_ColumnP_max = new HTuple(), hv_AreaF = new HTuple();
            HTuple hv_RowF = new HTuple(), hv_ColumnF = new HTuple();
            HTuple hv_RowF_min = new HTuple(), hv_ColumnF_min = new HTuple();
            HTuple hv_RowF_max = new HTuple(), hv_ColumnF_max = new HTuple();
            HTuple hv_WidthP = new HTuple(), hv_HeightP = new HTuple();
            HTuple hv_WidthF = new HTuple(), hv_HeightF = new HTuple();
            HTuple hv_VerticalCol_Center = new HTuple(), hv_VerticalCol_Left = new HTuple();
            HTuple hv_VerticalCol_Right = new HTuple(), hv_LineStartRow = new HTuple();
            HTuple hv_LineEndRow = new HTuple(), hv_VerticalCols = new HTuple();
            HTuple hv_LineColors = new HTuple(), hv_line_idx = new HTuple();
            HTuple hv_CurrentCol = new HTuple(), hv_PreIntersectionRows = new HTuple();
            HTuple hv_PreIntersectionCols = new HTuple(), hv_PreIsOverlapping = new HTuple();
            HTuple hv_FinalIntersectionRows = new HTuple(), hv_FinalIntersectionCols = new HTuple();
            HTuple hv_FinalIsOverlapping = new HTuple(), hv_CurrentUpperGap = new HTuple();
            HTuple hv_CurrentLowerGap = new HTuple(), hv_PreRowsSorted = new HTuple();
            HTuple hv_PreUpperRow = new HTuple(), hv_PreLowerRow = new HTuple();
            HTuple hv_FinalRowsSorted = new HTuple(), hv_FinalUpperRow = new HTuple();
            HTuple hv_FinalLowerRow = new HTuple(), hv_HorizontalRow = new HTuple();
            HTuple hv_HorizontalCol_Start = new HTuple(), hv_HorizontalCol_End = new HTuple();
            HTuple hv_PreHorizontalRows = new HTuple(), hv_PreHorizontalCols = new HTuple();
            HTuple hv_PreHorizontalOverlapping = new HTuple(), hv_FinalHorizontalRows = new HTuple();
            HTuple hv_FinalHorizontalCols = new HTuple(), hv_FinalHorizontalOverlapping = new HTuple();
            HTuple hv_CurrentLeftGap = new HTuple(), hv_CurrentRightGap = new HTuple();
            HTuple hv_PreHorizontalIndices = new HTuple(), hv_PreLeftCol = new HTuple();
            HTuple hv_PreRightCol = new HTuple(), hv_FinalHorizontalIndices = new HTuple();
            HTuple hv_FinalLeftCol = new HTuple(), hv_FinalRightCol = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreUpper);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreLower);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalUpper);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalLower);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreLeft);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreRight);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalLeft);
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalRight);
            HOperatorSet.GenEmptyObj(out ho_ho_UpperGapLine);
            HOperatorSet.GenEmptyObj(out ho_ho_LowerGapLine);
            HOperatorSet.GenEmptyObj(out ho_ho_LeftGapLine);
            HOperatorSet.GenEmptyObj(out ho_ho_RightGapLine);
            HOperatorSet.GenEmptyObj(out ho_ho_Preweld_Contour);
            HOperatorSet.GenEmptyObj(out ho_ho_Finalweld_Contour);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions1);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp1);
            HOperatorSet.GenEmptyObj(out ho_AreaFilteredRegions1);
            HOperatorSet.GenEmptyObj(out ho_SortedRegions1);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions2);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp2);
            HOperatorSet.GenEmptyObj(out ho_SortedRegions2);
            HOperatorSet.GenEmptyObj(out ho_Preweld_Region);
            HOperatorSet.GenEmptyObj(out ho_Finalweld_Region);
            HOperatorSet.GenEmptyObj(out ho_Preweld_Contour_single);
            HOperatorSet.GenEmptyObj(out ho_Finalweld_Contour_single);
            HOperatorSet.GenEmptyObj(out ho_LineXLD);
            HOperatorSet.GenEmptyObj(out ho_CrossPreUpper_single);
            HOperatorSet.GenEmptyObj(out ho_CrossPreLower_single);
            HOperatorSet.GenEmptyObj(out ho_CrossFinalUpper_single);
            HOperatorSet.GenEmptyObj(out ho_CrossFinalLower_single);
            HOperatorSet.GenEmptyObj(out ho_UpperGapLine_single);
            HOperatorSet.GenEmptyObj(out ho_LowerGapLine_single);
            HOperatorSet.GenEmptyObj(out ho_HorizontalLineXLD);
            HOperatorSet.GenEmptyObj(out ho_CrossPreLeft_single);
            HOperatorSet.GenEmptyObj(out ho_CrossPreRight_single);
            HOperatorSet.GenEmptyObj(out ho_CrossFinalLeft_single);
            HOperatorSet.GenEmptyObj(out ho_CrossFinalRight_single);
            HOperatorSet.GenEmptyObj(out ho_LeftGapLine_single);
            HOperatorSet.GenEmptyObj(out ho_RightGapLine_single);
            hv_Offset = new HTuple();
            hv_UpperGaps = new HTuple();
            hv_LowerGaps = new HTuple();
            hv_RightGaps = new HTuple();
            hv_LiftGaps = new HTuple();
            hv_AreaP_Tuple = new HTuple();
            hv_AreaF_Tuple = new HTuple();
            hv_RowP_Tuple = new HTuple();
            hv_ColumnP_Tuple = new HTuple();
            hv_RowF_Tuple = new HTuple();
            hv_ColumnF_Tuple = new HTuple();
            hv_WidthP_Tuple = new HTuple();
            hv_HeightP_Tuple = new HTuple();
            hv_WidthF_Tuple = new HTuple();
            hv_HeightF_Tuple = new HTuple();
            //预焊区域
            hv_Width.Dispose(); hv_Height.Dispose();
            HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
            ho_Region1.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region1, 1, 1);
            ho_ConnectedRegions1.Dispose();
            HOperatorSet.Connection(ho_Region1, out ho_ConnectedRegions1);
            ho_RegionFillUp1.Dispose();
            HOperatorSet.FillUp(ho_ConnectedRegions1, out ho_RegionFillUp1);
            ho_AreaFilteredRegions1.Dispose();
            HOperatorSet.SelectShape(ho_RegionFillUp1, out ho_AreaFilteredRegions1, "area",
                "and", 50000, 99999999);
            ho_SortedRegions1.Dispose();
            HOperatorSet.SortRegion(ho_AreaFilteredRegions1, out ho_SortedRegions1, "first_point",
                "true", "row");
            hv_Preweld_Num.Dispose();
            HOperatorSet.CountObj(ho_SortedRegions1, out hv_Preweld_Num);

            //终焊区域
            ho_Region2.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region2, 2, 2);
            ho_ConnectedRegions2.Dispose();
            HOperatorSet.Connection(ho_Region2, out ho_ConnectedRegions2);
            ho_RegionFillUp2.Dispose();
            HOperatorSet.FillUp(ho_ConnectedRegions2, out ho_RegionFillUp2);
            ho_Region2.Dispose();
            HOperatorSet.SelectShape(ho_RegionFillUp2, out ho_Region2, "area", "and", 10000,
                99999999);
            ho_SortedRegions2.Dispose();
            HOperatorSet.SortRegion(ho_Region2, out ho_SortedRegions2, "first_point", "true",
                "row");
            hv_Finalweld_Num.Dispose();
            HOperatorSet.CountObj(ho_SortedRegions2, out hv_Finalweld_Num);

            //设置左右平移距离变量
            hv_Offset.Dispose();
            hv_Offset = 300;

            //确定要检测的区域对数量
            hv_Num_Pairs.Dispose();
            using (HDevDisposeHelper dh = new HDevDisposeHelper())
            {
                hv_Num_Pairs = ((((hv_Preweld_Num.TupleConcat(
                    hv_Finalweld_Num))).TupleConcat(2))).TupleMin();
            }

            //初始化输出元组
            hv_UpperGaps.Dispose();
            hv_UpperGaps = new HTuple();
            hv_LowerGaps.Dispose();
            hv_LowerGaps = new HTuple();
            hv_RightGaps.Dispose();
            hv_RightGaps = new HTuple();
            hv_LiftGaps.Dispose();
            hv_LiftGaps = new HTuple();

            hv_AreaP_Tuple.Dispose();
            hv_AreaP_Tuple = new HTuple();
            hv_AreaF_Tuple.Dispose();
            hv_AreaF_Tuple = new HTuple();
            hv_RowP_Tuple.Dispose();
            hv_RowP_Tuple = new HTuple();
            hv_ColumnP_Tuple.Dispose();
            hv_ColumnP_Tuple = new HTuple();
            hv_RowF_Tuple.Dispose();
            hv_RowF_Tuple = new HTuple();
            hv_ColumnF_Tuple.Dispose();
            hv_ColumnF_Tuple = new HTuple();
            hv_WidthP_Tuple.Dispose();
            hv_WidthP_Tuple = new HTuple();
            hv_HeightP_Tuple.Dispose();
            hv_HeightP_Tuple = new HTuple();
            hv_WidthF_Tuple.Dispose();
            hv_WidthF_Tuple = new HTuple();
            hv_HeightF_Tuple.Dispose();
            hv_HeightF_Tuple = new HTuple();

            //初始化HObject数组（使用空对象）
            ho_ho_CrossPreUpper.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreUpper);
            ho_ho_CrossPreLower.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreLower);
            ho_ho_CrossFinalUpper.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalUpper);
            ho_ho_CrossFinalLower.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalLower);
            ho_ho_CrossPreLeft.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreLeft);
            ho_ho_CrossPreRight.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossPreRight);
            ho_ho_CrossFinalLeft.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalLeft);
            ho_ho_CrossFinalRight.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_CrossFinalRight);
            ho_ho_UpperGapLine.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_UpperGapLine);
            ho_ho_LowerGapLine.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_LowerGapLine);
            ho_ho_LeftGapLine.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_LeftGapLine);
            ho_ho_RightGapLine.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_RightGapLine);
            ho_ho_Preweld_Contour.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_Preweld_Contour);
            ho_ho_Finalweld_Contour.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ho_Finalweld_Contour);

            //循环处理每个区域对
            HTuple end_val57 = hv_Num_Pairs;
            HTuple step_val57 = 1;
            for (hv_i = 1; hv_i.Continue(end_val57, step_val57); hv_i = hv_i.TupleAdd(step_val57))
            {
                //选择对应区域
                ho_Preweld_Region.Dispose();
                HOperatorSet.SelectObj(ho_SortedRegions1, out ho_Preweld_Region, hv_i);
                ho_Finalweld_Region.Dispose();
                HOperatorSet.SelectObj(ho_SortedRegions2, out ho_Finalweld_Region, hv_i);

                //显示区域
                if (HDevWindowStack.IsOpen())
                {
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), ((new HTuple("yellow")).TupleConcat(
                            "cyan")).TupleSelect(hv_i - 1));
                    }
                }
                if (HDevWindowStack.IsOpen())
                {
                    HOperatorSet.DispObj(ho_Preweld_Region, HDevWindowStack.GetActive());
                }
                if (HDevWindowStack.IsOpen())
                {
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), ((new HTuple("green")).TupleConcat(
                            "magenta")).TupleSelect(hv_i - 1));
                    }
                }
                if (HDevWindowStack.IsOpen())
                {
                    HOperatorSet.DispObj(ho_Finalweld_Region, HDevWindowStack.GetActive());
                }

                //获取区域边界和中心点
                hv_AreaP.Dispose(); hv_RowP.Dispose(); hv_ColumnP.Dispose();
                HOperatorSet.AreaCenter(ho_Preweld_Region, out hv_AreaP, out hv_RowP, out hv_ColumnP);
                hv_RowP_min.Dispose(); hv_ColumnP_min.Dispose(); hv_RowP_max.Dispose(); hv_ColumnP_max.Dispose();
                HOperatorSet.SmallestRectangle1(ho_Preweld_Region, out hv_RowP_min, out hv_ColumnP_min,
                    out hv_RowP_max, out hv_ColumnP_max);

                hv_AreaF.Dispose(); hv_RowF.Dispose(); hv_ColumnF.Dispose();
                HOperatorSet.AreaCenter(ho_Finalweld_Region, out hv_AreaF, out hv_RowF, out hv_ColumnF);
                hv_RowF_min.Dispose(); hv_ColumnF_min.Dispose(); hv_RowF_max.Dispose(); hv_ColumnF_max.Dispose();
                HOperatorSet.SmallestRectangle1(ho_Finalweld_Region, out hv_RowF_min, out hv_ColumnF_min,
                    out hv_RowF_max, out hv_ColumnF_max);

                //计算预焊区域外接矩形的宽度和高度
                hv_WidthP.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_WidthP = hv_ColumnP_max - hv_ColumnP_min;
                }
                hv_HeightP.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_HeightP = hv_RowP_max - hv_RowP_min;
                }

                //计算终焊区域外接矩形的宽度和高度
                hv_WidthF.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_WidthF = hv_ColumnF_max - hv_ColumnF_min;
                }
                hv_HeightF.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_HeightF = hv_RowF_max - hv_RowF_min;
                }

                //将尺寸数据添加到元组数组
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_WidthP_Tuple = hv_WidthP_Tuple.TupleConcat(
                            hv_WidthP);
                        hv_WidthP_Tuple.Dispose();
                        hv_WidthP_Tuple = ExpTmpLocalVar_WidthP_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_HeightP_Tuple = hv_HeightP_Tuple.TupleConcat(
                            hv_HeightP);
                        hv_HeightP_Tuple.Dispose();
                        hv_HeightP_Tuple = ExpTmpLocalVar_HeightP_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_WidthF_Tuple = hv_WidthF_Tuple.TupleConcat(
                            hv_WidthF);
                        hv_WidthF_Tuple.Dispose();
                        hv_WidthF_Tuple = ExpTmpLocalVar_WidthF_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_HeightF_Tuple = hv_HeightF_Tuple.TupleConcat(
                            hv_HeightF);
                        hv_HeightF_Tuple.Dispose();
                        hv_HeightF_Tuple = ExpTmpLocalVar_HeightF_Tuple;
                    }
                }

                //将数据添加到元组数组
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_AreaP_Tuple = hv_AreaP_Tuple.TupleConcat(
                            hv_AreaP);
                        hv_AreaP_Tuple.Dispose();
                        hv_AreaP_Tuple = ExpTmpLocalVar_AreaP_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_AreaF_Tuple = hv_AreaF_Tuple.TupleConcat(
                            hv_AreaF);
                        hv_AreaF_Tuple.Dispose();
                        hv_AreaF_Tuple = ExpTmpLocalVar_AreaF_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_RowP_Tuple = hv_RowP_Tuple.TupleConcat(
                            hv_RowP);
                        hv_RowP_Tuple.Dispose();
                        hv_RowP_Tuple = ExpTmpLocalVar_RowP_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_ColumnP_Tuple = hv_ColumnP_Tuple.TupleConcat(
                            hv_ColumnP);
                        hv_ColumnP_Tuple.Dispose();
                        hv_ColumnP_Tuple = ExpTmpLocalVar_ColumnP_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_RowF_Tuple = hv_RowF_Tuple.TupleConcat(
                            hv_RowF);
                        hv_RowF_Tuple.Dispose();
                        hv_RowF_Tuple = ExpTmpLocalVar_RowF_Tuple;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_ColumnF_Tuple = hv_ColumnF_Tuple.TupleConcat(
                            hv_ColumnF);
                        hv_ColumnF_Tuple.Dispose();
                        hv_ColumnF_Tuple = ExpTmpLocalVar_ColumnF_Tuple;
                    }
                }

                //=== 垂直线法：以终焊中心列为准 ===
                hv_VerticalCol_Center.Dispose();
                hv_VerticalCol_Center = new HTuple(hv_ColumnF);
                hv_VerticalCol_Left.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_VerticalCol_Left = hv_ColumnF - hv_Offset;
                }
                hv_VerticalCol_Right.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_VerticalCol_Right = hv_ColumnF + hv_Offset;
                }

                hv_LineStartRow.Dispose();
                hv_LineStartRow = 0;
                hv_LineEndRow.Dispose();
                hv_LineEndRow = new HTuple(hv_Height);

                //生成轮廓
                ho_Preweld_Contour_single.Dispose();
                HOperatorSet.GenContourRegionXld(ho_Preweld_Region, out ho_Preweld_Contour_single,
                    "border");
                ho_Finalweld_Contour_single.Dispose();
                HOperatorSet.GenContourRegionXld(ho_Finalweld_Region, out ho_Finalweld_Contour_single,
                    "border");

                //合并到总轮廓对象
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ConcatObj(ho_ho_Preweld_Contour, ho_Preweld_Contour_single, out ExpTmpOutVar_0
                        );
                    ho_ho_Preweld_Contour.Dispose();
                    ho_ho_Preweld_Contour = ExpTmpOutVar_0;
                }
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ConcatObj(ho_ho_Finalweld_Contour, ho_Finalweld_Contour_single,
                        out ExpTmpOutVar_0);
                    ho_ho_Finalweld_Contour.Dispose();
                    ho_ho_Finalweld_Contour = ExpTmpOutVar_0;
                }

                //创建垂直线坐标数组
                hv_VerticalCols.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_VerticalCols = new HTuple();
                    hv_VerticalCols = hv_VerticalCols.TupleConcat(hv_VerticalCol_Center, hv_VerticalCol_Left, hv_VerticalCol_Right);
                }
                hv_LineColors.Dispose();
                hv_LineColors = new HTuple();
                hv_LineColors[0] = "red";
                hv_LineColors[1] = "yellow";
                hv_LineColors[2] = "green";

                //循环处理三条垂直线
                for (hv_line_idx = 0; (int)hv_line_idx <= 2; hv_line_idx = (int)hv_line_idx + 1)
                {
                    //获取当前垂直线参数
                    hv_CurrentCol.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_CurrentCol = hv_VerticalCols.TupleSelect(
                            hv_line_idx);
                    }

                    //创建垂直线作为XLD用于交点计算
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_LineXLD.Dispose();
                        HOperatorSet.GenContourPolygonXld(out ho_LineXLD, hv_LineStartRow.TupleConcat(
                            hv_LineEndRow), hv_CurrentCol.TupleConcat(hv_CurrentCol));
                    }

                    //计算交点
                    hv_PreIntersectionRows.Dispose(); hv_PreIntersectionCols.Dispose(); hv_PreIsOverlapping.Dispose();
                    HOperatorSet.IntersectionContoursXld(ho_Preweld_Contour_single, ho_LineXLD,
                        "all", out hv_PreIntersectionRows, out hv_PreIntersectionCols, out hv_PreIsOverlapping);
                    hv_FinalIntersectionRows.Dispose(); hv_FinalIntersectionCols.Dispose(); hv_FinalIsOverlapping.Dispose();
                    HOperatorSet.IntersectionContoursXld(ho_Finalweld_Contour_single, ho_LineXLD,
                        "all", out hv_FinalIntersectionRows, out hv_FinalIntersectionCols, out hv_FinalIsOverlapping);

                    //初始化当前线的间隙值（默认为0）
                    hv_CurrentUpperGap.Dispose();
                    hv_CurrentUpperGap = 0;
                    hv_CurrentLowerGap.Dispose();
                    hv_CurrentLowerGap = 0;

                    //处理预焊交点结果
                    if ((int)(new HTuple((new HTuple(hv_PreIntersectionRows.TupleLength())).TupleGreaterEqual(
                        2))) != 0)
                    {
                        //预焊：取最上和最下行
                        hv_PreRowsSorted.Dispose();
                        HOperatorSet.TupleSort(hv_PreIntersectionRows, out hv_PreRowsSorted);
                        hv_PreUpperRow.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_PreUpperRow = hv_PreRowsSorted.TupleSelect(
                                0);
                        }
                        hv_PreLowerRow.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_PreLowerRow = hv_PreRowsSorted.TupleSelect(
                                (new HTuple(hv_PreRowsSorted.TupleLength())) - 1);
                        }

                        //创建预焊交点十字标记
                        ho_CrossPreUpper_single.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_CrossPreUpper_single, hv_PreUpperRow,
                            hv_CurrentCol, 20, 0.785398);
                        ho_CrossPreLower_single.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_CrossPreLower_single, hv_PreLowerRow,
                            hv_CurrentCol, 20, 0.785398);

                        //合并到总对象
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.ConcatObj(ho_ho_CrossPreUpper, ho_CrossPreUpper_single, out ExpTmpOutVar_0
                                );
                            ho_ho_CrossPreUpper.Dispose();
                            ho_ho_CrossPreUpper = ExpTmpOutVar_0;
                        }
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.ConcatObj(ho_ho_CrossPreLower, ho_CrossPreLower_single, out ExpTmpOutVar_0
                                );
                            ho_ho_CrossPreLower.Dispose();
                            ho_ho_CrossPreLower = ExpTmpOutVar_0;
                        }

                        if (HDevWindowStack.IsOpen())
                        {
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                HOperatorSet.SetColor(HDevWindowStack.GetActive(), hv_LineColors.TupleSelect(
                                    hv_line_idx));
                            }
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.DispObj(ho_CrossPreUpper_single, HDevWindowStack.GetActive()
                                );
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.DispObj(ho_CrossPreLower_single, HDevWindowStack.GetActive()
                                );
                        }
                    }

                    //处理终焊交点结果
                    if ((int)(new HTuple((new HTuple(hv_FinalIntersectionRows.TupleLength())).TupleGreaterEqual(
                        2))) != 0)
                    {
                        //终焊：取最上和最下行
                        hv_FinalRowsSorted.Dispose();
                        HOperatorSet.TupleSort(hv_FinalIntersectionRows, out hv_FinalRowsSorted);
                        hv_FinalUpperRow.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_FinalUpperRow = hv_FinalRowsSorted.TupleSelect(
                                0);
                        }
                        hv_FinalLowerRow.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_FinalLowerRow = hv_FinalRowsSorted.TupleSelect(
                                (new HTuple(hv_FinalRowsSorted.TupleLength())) - 1);
                        }

                        //创建终焊交点十字标记
                        ho_CrossFinalUpper_single.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_CrossFinalUpper_single, hv_FinalUpperRow,
                            hv_CurrentCol, 20, 0.785398);
                        ho_CrossFinalLower_single.Dispose();
                        HOperatorSet.GenCrossContourXld(out ho_CrossFinalLower_single, hv_FinalLowerRow,
                            hv_CurrentCol, 20, 0.785398);

                        //合并到总对象
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.ConcatObj(ho_ho_CrossFinalUpper, ho_CrossFinalUpper_single,
                                out ExpTmpOutVar_0);
                            ho_ho_CrossFinalUpper.Dispose();
                            ho_ho_CrossFinalUpper = ExpTmpOutVar_0;
                        }
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.ConcatObj(ho_ho_CrossFinalLower, ho_CrossFinalLower_single,
                                out ExpTmpOutVar_0);
                            ho_ho_CrossFinalLower.Dispose();
                            ho_ho_CrossFinalLower = ExpTmpOutVar_0;
                        }

                        if (HDevWindowStack.IsOpen())
                        {
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                HOperatorSet.SetColor(HDevWindowStack.GetActive(), hv_LineColors.TupleSelect(
                                    hv_line_idx));
                            }
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.DispObj(ho_CrossFinalUpper_single, HDevWindowStack.GetActive()
                                );
                        }
                        if (HDevWindowStack.IsOpen())
                        {
                            HOperatorSet.DispObj(ho_CrossFinalLower_single, HDevWindowStack.GetActive()
                                );
                        }

                        //当两条线都有足够交点时计算高度和间隙
                        if ((int)(new HTuple((new HTuple(hv_PreIntersectionRows.TupleLength())).TupleGreaterEqual(
                            2))) != 0)
                        {
                            //计算间隙值
                            hv_CurrentUpperGap.Dispose();
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_CurrentUpperGap = hv_FinalUpperRow - hv_PreUpperRow;
                            }
                            hv_CurrentLowerGap.Dispose();
                            using (HDevDisposeHelper dh = new HDevDisposeHelper())
                            {
                                hv_CurrentLowerGap = -(hv_FinalLowerRow - hv_PreLowerRow);
                            }

                            //显示上间隙线段
                            if ((int)(new HTuple(hv_CurrentUpperGap.TupleGreater(0))) != 0)
                            {
                                ho_UpperGapLine_single.Dispose();
                                HOperatorSet.GenRegionLine(out ho_UpperGapLine_single, hv_PreUpperRow,
                                    hv_CurrentCol, hv_FinalUpperRow, hv_CurrentCol);
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ho_UpperGapLine, ho_UpperGapLine_single,
                                        out ExpTmpOutVar_0);
                                    ho_ho_UpperGapLine.Dispose();
                                    ho_ho_UpperGapLine = ExpTmpOutVar_0;
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                    {
                                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), hv_LineColors.TupleSelect(
                                            hv_line_idx));
                                    }
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    HOperatorSet.DispObj(ho_UpperGapLine_single, HDevWindowStack.GetActive()
                                        );
                                }
                            }

                            //显示下间隙线段
                            if ((int)(new HTuple(hv_CurrentLowerGap.TupleGreater(0))) != 0)
                            {
                                ho_LowerGapLine_single.Dispose();
                                HOperatorSet.GenRegionLine(out ho_LowerGapLine_single, hv_PreLowerRow,
                                    hv_CurrentCol, hv_FinalLowerRow, hv_CurrentCol);
                                {
                                    HObject ExpTmpOutVar_0;
                                    HOperatorSet.ConcatObj(ho_ho_LowerGapLine, ho_LowerGapLine_single,
                                        out ExpTmpOutVar_0);
                                    ho_ho_LowerGapLine.Dispose();
                                    ho_ho_LowerGapLine = ExpTmpOutVar_0;
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                                    {
                                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), hv_LineColors.TupleSelect(
                                            hv_line_idx));
                                    }
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                                }
                                if (HDevWindowStack.IsOpen())
                                {
                                    HOperatorSet.DispObj(ho_LowerGapLine_single, HDevWindowStack.GetActive()
                                        );
                                }
                            }
                        }
                    }

                    //将当前线的间隙值添加到数组中
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_UpperGaps = hv_UpperGaps.TupleConcat(
                                hv_CurrentUpperGap);
                            hv_UpperGaps.Dispose();
                            hv_UpperGaps = ExpTmpLocalVar_UpperGaps;
                        }
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        {
                            HTuple
                              ExpTmpLocalVar_LowerGaps = hv_LowerGaps.TupleConcat(
                                hv_CurrentLowerGap);
                            hv_LowerGaps.Dispose();
                            hv_LowerGaps = ExpTmpLocalVar_LowerGaps;
                        }
                    }
                }

                //=== 水平线法：以终焊中心行为准 ===
                hv_HorizontalRow.Dispose();
                hv_HorizontalRow = new HTuple(hv_RowF);
                hv_HorizontalCol_Start.Dispose();
                hv_HorizontalCol_Start = 0;
                hv_HorizontalCol_End.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_HorizontalCol_End = hv_Width - 1;
                }

                //创建水平线XLD用于交点计算
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_HorizontalLineXLD.Dispose();
                    HOperatorSet.GenContourPolygonXld(out ho_HorizontalLineXLD, hv_HorizontalRow.TupleConcat(
                        hv_HorizontalRow), hv_HorizontalCol_Start.TupleConcat(hv_HorizontalCol_End));
                }

                //计算水平线与预焊轮廓的交点
                hv_PreHorizontalRows.Dispose(); hv_PreHorizontalCols.Dispose(); hv_PreHorizontalOverlapping.Dispose();
                HOperatorSet.IntersectionContoursXld(ho_Preweld_Contour_single, ho_HorizontalLineXLD,
                    "all", out hv_PreHorizontalRows, out hv_PreHorizontalCols, out hv_PreHorizontalOverlapping);

                //计算水平线与终焊轮廓的交点
                hv_FinalHorizontalRows.Dispose(); hv_FinalHorizontalCols.Dispose(); hv_FinalHorizontalOverlapping.Dispose();
                HOperatorSet.IntersectionContoursXld(ho_Finalweld_Contour_single, ho_HorizontalLineXLD,
                    "all", out hv_FinalHorizontalRows, out hv_FinalHorizontalCols, out hv_FinalHorizontalOverlapping);

                //初始化水平间隙变量
                hv_CurrentLeftGap.Dispose();
                hv_CurrentLeftGap = 0;
                hv_CurrentRightGap.Dispose();
                hv_CurrentRightGap = 0;

                //处理水平线与预焊轮廓的交点
                if ((int)(new HTuple((new HTuple(hv_PreHorizontalRows.TupleLength())).TupleGreater(
                    0))) != 0)
                {
                    //对交点按列坐标排序（从左到右）
                    hv_PreHorizontalIndices.Dispose();
                    HOperatorSet.TupleSortIndex(hv_PreHorizontalCols, out hv_PreHorizontalIndices);

                    //取最左侧和最右侧的交点
                    hv_PreLeftCol.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_PreLeftCol = hv_PreHorizontalCols.TupleSelect(
                            hv_PreHorizontalIndices.TupleSelect(0));
                    }
                    hv_PreRightCol.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_PreRightCol = hv_PreHorizontalCols.TupleSelect(
                            hv_PreHorizontalIndices.TupleSelect((new HTuple(hv_PreHorizontalIndices.TupleLength()
                            )) - 1));
                    }

                    //创建交点十字标记
                    ho_CrossPreLeft_single.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_CrossPreLeft_single, hv_HorizontalRow,
                        hv_PreLeftCol, 20, 0.785398);
                    ho_CrossPreRight_single.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_CrossPreRight_single, hv_HorizontalRow,
                        hv_PreRightCol, 20, 0.785398);

                    //合并到总对象
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ho_CrossPreLeft, ho_CrossPreLeft_single, out ExpTmpOutVar_0
                            );
                        ho_ho_CrossPreLeft.Dispose();
                        ho_ho_CrossPreLeft = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ho_CrossPreRight, ho_CrossPreRight_single, out ExpTmpOutVar_0
                            );
                        ho_ho_CrossPreRight.Dispose();
                        ho_ho_CrossPreRight = ExpTmpOutVar_0;
                    }

                    //显示交点
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), "orange");
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_CrossPreLeft_single, HDevWindowStack.GetActive()
                            );
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_CrossPreRight_single, HDevWindowStack.GetActive()
                            );
                    }
                }

                //处理水平线与终焊轮廓的交点
                if ((int)(new HTuple((new HTuple(hv_FinalHorizontalRows.TupleLength())).TupleGreater(
                    0))) != 0)
                {
                    //对交点按列坐标排序（从左到右）
                    hv_FinalHorizontalIndices.Dispose();
                    HOperatorSet.TupleSortIndex(hv_FinalHorizontalCols, out hv_FinalHorizontalIndices);

                    //取最左侧和最右侧的交点
                    hv_FinalLeftCol.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_FinalLeftCol = hv_FinalHorizontalCols.TupleSelect(
                            hv_FinalHorizontalIndices.TupleSelect(0));
                    }
                    hv_FinalRightCol.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_FinalRightCol = hv_FinalHorizontalCols.TupleSelect(
                            hv_FinalHorizontalIndices.TupleSelect((new HTuple(hv_FinalHorizontalIndices.TupleLength()
                            )) - 1));
                    }

                    //创建交点十字标记
                    ho_CrossFinalLeft_single.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_CrossFinalLeft_single, hv_HorizontalRow,
                        hv_FinalLeftCol, 20, 0.785398);
                    ho_CrossFinalRight_single.Dispose();
                    HOperatorSet.GenCrossContourXld(out ho_CrossFinalRight_single, hv_HorizontalRow,
                        hv_FinalRightCol, 20, 0.785398);

                    //合并到总对象
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ho_CrossFinalLeft, ho_CrossFinalLeft_single, out ExpTmpOutVar_0
                            );
                        ho_ho_CrossFinalLeft.Dispose();
                        ho_ho_CrossFinalLeft = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConcatObj(ho_ho_CrossFinalRight, ho_CrossFinalRight_single,
                            out ExpTmpOutVar_0);
                        ho_ho_CrossFinalRight.Dispose();
                        ho_ho_CrossFinalRight = ExpTmpOutVar_0;
                    }

                    //显示交点
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.SetColor(HDevWindowStack.GetActive(), "pink");
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_CrossFinalLeft_single, HDevWindowStack.GetActive()
                            );
                    }
                    if (HDevWindowStack.IsOpen())
                    {
                        HOperatorSet.DispObj(ho_CrossFinalRight_single, HDevWindowStack.GetActive()
                            );
                    }

                    //计算水平间隙（当预焊和终焊都有交点时）
                    if ((int)(new HTuple((new HTuple(hv_PreHorizontalRows.TupleLength())).TupleGreater(
                        0))) != 0)
                    {
                        //计算左侧间隙和右侧间隙
                        hv_CurrentLeftGap.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_CurrentLeftGap = hv_FinalLeftCol - hv_PreLeftCol;
                        }
                        hv_CurrentRightGap.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_CurrentRightGap = -(hv_FinalRightCol - hv_PreRightCol);
                        }

                        //显示左侧间隙线段
                        if ((int)(new HTuple(hv_CurrentLeftGap.TupleGreater(0))) != 0)
                        {
                            ho_LeftGapLine_single.Dispose();
                            HOperatorSet.GenRegionLine(out ho_LeftGapLine_single, hv_HorizontalRow,
                                hv_PreLeftCol, hv_HorizontalRow, hv_FinalLeftCol);
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_ho_LeftGapLine, ho_LeftGapLine_single, out ExpTmpOutVar_0
                                    );
                                ho_ho_LeftGapLine.Dispose();
                                ho_ho_LeftGapLine = ExpTmpOutVar_0;
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.SetColor(HDevWindowStack.GetActive(), "blue");
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.DispObj(ho_LeftGapLine_single, HDevWindowStack.GetActive()
                                    );
                            }
                        }

                        //显示右侧间隙线段
                        if ((int)(new HTuple(hv_CurrentRightGap.TupleGreater(0))) != 0)
                        {
                            ho_RightGapLine_single.Dispose();
                            HOperatorSet.GenRegionLine(out ho_RightGapLine_single, hv_HorizontalRow,
                                hv_PreRightCol, hv_HorizontalRow, hv_FinalRightCol);
                            {
                                HObject ExpTmpOutVar_0;
                                HOperatorSet.ConcatObj(ho_ho_RightGapLine, ho_RightGapLine_single, out ExpTmpOutVar_0
                                    );
                                ho_ho_RightGapLine.Dispose();
                                ho_ho_RightGapLine = ExpTmpOutVar_0;
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.SetColor(HDevWindowStack.GetActive(), "blue");
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.SetLineWidth(HDevWindowStack.GetActive(), 3);
                            }
                            if (HDevWindowStack.IsOpen())
                            {
                                HOperatorSet.DispObj(ho_RightGapLine_single, HDevWindowStack.GetActive()
                                    );
                            }
                        }
                    }
                }

                //将水平间隙值添加到数组中
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_LiftGaps = hv_LiftGaps.TupleConcat(
                            hv_CurrentLeftGap);
                        hv_LiftGaps.Dispose();
                        hv_LiftGaps = ExpTmpLocalVar_LiftGaps;
                    }
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_RightGaps = hv_RightGaps.TupleConcat(
                            hv_CurrentRightGap);
                        hv_RightGaps.Dispose();
                        hv_RightGaps = ExpTmpLocalVar_RightGaps;
                    }
                }

            }

            //输出结果
            //所有HObject对象已经通过concat_obj合并为单个对象
            //所有数据元组也已经构建完成
            ho_ConnectedRegions1.Dispose();
            ho_RegionFillUp1.Dispose();
            ho_AreaFilteredRegions1.Dispose();
            ho_SortedRegions1.Dispose();
            ho_ConnectedRegions2.Dispose();
            ho_RegionFillUp2.Dispose();
            ho_SortedRegions2.Dispose();
            ho_Preweld_Region.Dispose();
            ho_Finalweld_Region.Dispose();
            ho_Preweld_Contour_single.Dispose();
            ho_Finalweld_Contour_single.Dispose();
            ho_LineXLD.Dispose();
            ho_CrossPreUpper_single.Dispose();
            ho_CrossPreLower_single.Dispose();
            ho_CrossFinalUpper_single.Dispose();
            ho_CrossFinalLower_single.Dispose();
            ho_UpperGapLine_single.Dispose();
            ho_LowerGapLine_single.Dispose();
            ho_HorizontalLineXLD.Dispose();
            ho_CrossPreLeft_single.Dispose();
            ho_CrossPreRight_single.Dispose();
            ho_CrossFinalLeft_single.Dispose();
            ho_CrossFinalRight_single.Dispose();
            ho_LeftGapLine_single.Dispose();
            ho_RightGapLine_single.Dispose();

            hv_Width.Dispose();
            hv_Height.Dispose();
            hv_Preweld_Num.Dispose();
            hv_Finalweld_Num.Dispose();
            hv_Num_Pairs.Dispose();
            hv_i.Dispose();
            hv_AreaP.Dispose();
            hv_RowP.Dispose();
            hv_ColumnP.Dispose();
            hv_RowP_min.Dispose();
            hv_ColumnP_min.Dispose();
            hv_RowP_max.Dispose();
            hv_ColumnP_max.Dispose();
            hv_AreaF.Dispose();
            hv_RowF.Dispose();
            hv_ColumnF.Dispose();
            hv_RowF_min.Dispose();
            hv_ColumnF_min.Dispose();
            hv_RowF_max.Dispose();
            hv_ColumnF_max.Dispose();
            hv_WidthP.Dispose();
            hv_HeightP.Dispose();
            hv_WidthF.Dispose();
            hv_HeightF.Dispose();
            hv_VerticalCol_Center.Dispose();
            hv_VerticalCol_Left.Dispose();
            hv_VerticalCol_Right.Dispose();
            hv_LineStartRow.Dispose();
            hv_LineEndRow.Dispose();
            hv_VerticalCols.Dispose();
            hv_LineColors.Dispose();
            hv_line_idx.Dispose();
            hv_CurrentCol.Dispose();
            hv_PreIntersectionRows.Dispose();
            hv_PreIntersectionCols.Dispose();
            hv_PreIsOverlapping.Dispose();
            hv_FinalIntersectionRows.Dispose();
            hv_FinalIntersectionCols.Dispose();
            hv_FinalIsOverlapping.Dispose();
            hv_CurrentUpperGap.Dispose();
            hv_CurrentLowerGap.Dispose();
            hv_PreRowsSorted.Dispose();
            hv_PreUpperRow.Dispose();
            hv_PreLowerRow.Dispose();
            hv_FinalRowsSorted.Dispose();
            hv_FinalUpperRow.Dispose();
            hv_FinalLowerRow.Dispose();
            hv_HorizontalRow.Dispose();
            hv_HorizontalCol_Start.Dispose();
            hv_HorizontalCol_End.Dispose();
            hv_PreHorizontalRows.Dispose();
            hv_PreHorizontalCols.Dispose();
            hv_PreHorizontalOverlapping.Dispose();
            hv_FinalHorizontalRows.Dispose();
            hv_FinalHorizontalCols.Dispose();
            hv_FinalHorizontalOverlapping.Dispose();
            hv_CurrentLeftGap.Dispose();
            hv_CurrentRightGap.Dispose();
            hv_PreHorizontalIndices.Dispose();
            hv_PreLeftCol.Dispose();
            hv_PreRightCol.Dispose();
            hv_FinalHorizontalIndices.Dispose();
            hv_FinalLeftCol.Dispose();
            hv_FinalRightCol.Dispose();

            return;
        }
    }
}
