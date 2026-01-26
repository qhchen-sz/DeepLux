using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.SealingPinYuhan.ViewModels
{
    public static class Algorithm
    {
        // Local procedures 
        public static void mfdyuhan(HObject ho_Image, out HObject ho_Circle, out HTuple hv_Row1,
            out HTuple hv_Column1, out HTuple hv_Radius, out HTuple hv_Area2)
        {



            // Local iconic variables 

            HObject ho_ConnectedRegions = null, ho_SelectedRegions = null;
            HObject ho_Region2 = null;

            // Local control variables 

            HTuple hv_Column1L = new HTuple(), hv_Row2 = new HTuple();
            HTuple hv_Column2 = new HTuple(), hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Circle);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_Region2);
            hv_Row1 = new HTuple();
            hv_Column1 = new HTuple();
            hv_Radius = new HTuple();
            hv_Area2 = new HTuple();
            try
            {
                try
                {
                    hv_Row1.Dispose();
                    hv_Row1 = 0;
                    hv_Column1L.Dispose();
                    hv_Column1L = 0;
                    hv_Radius.Dispose();
                    hv_Radius = 0;
                    hv_Area2.Dispose();
                    hv_Area2 = 0;
                    ho_Circle.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Circle);
                    ho_Circle.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Circle, 1, 1);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_Circle, out ho_ConnectedRegions);
                    ho_SelectedRegions.Dispose();
                    HOperatorSet.SelectShapeStd(ho_ConnectedRegions, out ho_SelectedRegions,
                        "max_area", 70);
                    hv_Row1.Dispose(); hv_Column1.Dispose(); hv_Radius.Dispose();
                    HOperatorSet.SmallestCircle(ho_SelectedRegions, out hv_Row1, out hv_Column1,
                        out hv_Radius);
                    ho_Region2.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region2, 2, 2);
                    hv_Area2.Dispose(); hv_Row2.Dispose(); hv_Column2.Dispose();
                    HOperatorSet.AreaCenter(ho_Region2, out hv_Area2, out hv_Row2, out hv_Column2);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    hv_Row1.Dispose();
                    hv_Row1 = 0;
                    hv_Column1L.Dispose();
                    hv_Column1L = 0;
                    hv_Radius.Dispose();
                    hv_Radius = 0;
                    hv_Area2.Dispose();
                    hv_Area2 = 0;
                    ho_Circle.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_Circle);
                    throw new HalconException(hv_Exception);
                }

                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();
                ho_Region2.Dispose();

                hv_Column1L.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();
                ho_Region2.Dispose();

                hv_Column1L.Dispose();
                hv_Row2.Dispose();
                hv_Column2.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }
    }
}
