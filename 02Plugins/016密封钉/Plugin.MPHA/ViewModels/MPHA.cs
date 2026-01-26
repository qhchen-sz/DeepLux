using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace Plugin.MPHA.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        TopSurfaceRegionLink1,
        TopSurfaceRegionLink2,
        TopSurfaceRegionLink3,
        TopSurfaceRegionLink4,
        NailSurfaceRegionLink,
        TopSurfaceXTextLink,
        TopSurfaceYTextLink,
        TopSurfaceWidthTextLink,
        TopSurfaceHeightTextLink,
        NailSurfaceX1TextLink,
        NailSurfaceY1TextLink,
        NailSurfaceX2TextLink,
        NailSurfaceY2TextLink,
    }
    public class MPHA
    {

        #region Csharp method
        public static PointArray PreProcessArray(double[] X, double[] Y, double[] Z)
        {
            PointArray pa = new PointArray();
            //double[] x = X.ToArray();
            //double[] y = Y.ToArray();
            //double[] z = Z.ToArray();
            int type_length = sizeof(double);
            IntPtr ptrx = Marshal.AllocHGlobal(X.Length * type_length);
            IntPtr ptry = Marshal.AllocHGlobal(Y.Length * type_length);
            IntPtr ptrz = Marshal.AllocHGlobal(Z.Length * type_length);
            Marshal.Copy(X, 0, ptrx, X.Length);
            Marshal.Copy(Y, 0, ptry, Y.Length);
            Marshal.Copy(Z, 0, ptrz, Z.Length);
            pa.x = ptrx;
            pa.y = ptry;
            pa.z = ptrz;
            pa.length = X.Length;
            return pa;
        }


        public static void FreePontArray(PointArray pa)
        {
            if (pa.x != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.x);
                pa.x = IntPtr.Zero;
            }
            if (pa.y != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.y);
                pa.y = IntPtr.Zero;
            }
            if (pa.z != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pa.z);
                pa.z = IntPtr.Zero;
            }
        }

        public static void HalconToImgPara(HObject hoImage, out ImgPara para)
        {
            para = new ImgPara();

            HObject imageH = null;
            HObject imageFloat = null;

            try
            {
                // 1. 图像通道检查
                HOperatorSet.CountChannels(hoImage, out HTuple channels);

                if (channels.I == 2)
                {
                    HOperatorSet.Decompose2(hoImage, out imageH, out HObject _);
                }
                else if (channels.I == 1)
                {
                    imageH = hoImage.Clone();
                }
                else
                {
                    throw new ArgumentException("Only single-channel images (e.g. height map) are supported.");
                }

                // 2. 获取宽高
                HOperatorSet.GetImageSize(imageH, out HTuple width, out HTuple height);
                para.wid = width;
                para.hei = height;
                para.channels = 1;

                // 3. 转换为 float32 图像
                HOperatorSet.ConvertImageType(imageH, out imageFloat, "real");

                // 4. 获取指针数据（修正版本）
                HOperatorSet.GetImagePointer1(imageFloat, out HTuple ptr, out HTuple type, out HTuple w, out HTuple h);

                IntPtr dataPtr = (IntPtr)ptr.L;

                if (type.S != "real")
                    throw new ArgumentException("Expected a 'real' (float32) image.");

                int totalPixels = w * h;
                int totalBytes = totalPixels * sizeof(float);

                float[] buffer = new float[totalPixels];
                Marshal.Copy(dataPtr, buffer, 0, totalPixels);

                para.data = Marshal.AllocHGlobal(totalBytes);
                Marshal.Copy(buffer, 0, para.data, totalPixels);

                para.step = para.wid * sizeof(float);
                para.type = 5; // 自定义 float32
            }
            finally
            {
                // 确保释放 HObject 对象
                if (imageH != null && imageH.IsInitialized()) imageH.Dispose();
                if (imageFloat != null && imageFloat.IsInitialized()) imageFloat.Dispose();
            }
        }

        public static void FreeData(ref ImgPara imgData)
        {
            if (imgData.data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(imgData.data);
                imgData.data = IntPtr.Zero;
            }
        }

        public static bool RunMPHA(ref ImgPara img,
        ref FuncPara func_para, ref Vector3d transformationMatrix,
        out ResultParaPindisk result, bool debug_mode = false)
        {
            result = default; // 确保在异常情况下也有初始化
            try
            {
                bool flag = measure_pindisk_height_dllv3(ref img, ref func_para, ref transformationMatrix, out result, debug_mode);
                return flag;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                FreeData(ref img);
            }
        }


        //public static void RunMPHA(ref CornerPoints cp,
        //    double [] CenX, double[] CenY, double[] CenZ,
        //    ref FuncPara func_para, ref Vector3d transformationMatrix,
        //    out ResultParaPindisk result, bool debug_mode = false)
        //{
        //    PointArray central_pa = PreProcessArray(CenX, CenY, CenZ);
        //    measure_pindisk_height_dllv2(ref cp, ref central_pa, ref func_para, ref transformationMatrix, out result, debug_mode);
        //    FreePontArray(central_pa);
        //}
        //  public static void RunMPHA(List<double> X,
        // List<double> Y, List<double> Z, ref Vector3d transformationMatrix,
        //out ResultParaPindisk result, ref TiffPara tiff_para,
        // float central_plane_size, bool debug_mode = false)
        //      {
        //      PointArray pa = PreProcessArray(X, Y, Z);
        //      measure_pindisk_height_dll(ref pa, ref transformationMatrix, out result, ref tiff_para, central_plane_size, debug_mode);
        //      FreePontArray(pa);
        //  }

        public static void Send3DPindiskPoints(HObject ho_Image, HObject ho_RegionCen, HObject ho_RegionBot,
            out HTuple hv_PointXCen, out HTuple hv_PointYCen, out HTuple hv_PointZCen)
        {



            // Local iconic variables 

            HObject ho_ImageH = null, ho_ImageG = null;

            // Local control variables 

            HTuple hv_Channels = new HTuple(), hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ImageH);
            HOperatorSet.GenEmptyObj(out ho_ImageG);
            hv_PointXCen = new HTuple();
            hv_PointYCen = new HTuple();
            hv_PointZCen = new HTuple();

            try
            {
                try
                {
                    hv_Channels.Dispose();
                    HOperatorSet.CountChannels(ho_Image, out hv_Channels);
                    ho_ImageH.Dispose();
                    HOperatorSet.GenEmptyObj(out ho_ImageH);
                    if ((int)(new HTuple(hv_Channels.TupleEqual(2))) != 0)
                    {
                        ho_ImageH.Dispose(); ho_ImageG.Dispose();
                        HOperatorSet.Decompose2(ho_Image, out ho_ImageH, out ho_ImageG);
                    }
                    else
                    {
                        ho_ImageH.Dispose();
                        ho_ImageH = new HObject(ho_Image);
                    }
                    //bottom points
                    //hv_PointYBot.Dispose(); hv_PointXBot.Dispose();
                    //HOperatorSet.GetRegionPoints(ho_RegionBot, out hv_PointYBot, out hv_PointXBot);
                    //hv_PointZBot.Dispose();
                    //HOperatorSet.GetGrayval(ho_ImageH, hv_PointYBot, hv_PointXBot, out hv_PointZBot);
                    //centeral points
                    hv_PointYCen.Dispose(); hv_PointXCen.Dispose();
                    HOperatorSet.GetRegionPoints(ho_RegionCen, out hv_PointYCen, out hv_PointXCen);
                    hv_PointZCen.Dispose();
                    HOperatorSet.GetGrayval(ho_ImageH, hv_PointYCen, hv_PointXCen, out hv_PointZCen);
                    hv_PointXCen = hv_PointXCen * 1.0;
                    hv_PointYCen = hv_PointYCen * 1.0;
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);

                }
                ho_ImageH.Dispose();
                ho_ImageG.Dispose();

                hv_Channels.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_ImageH.Dispose();
                ho_ImageG.Dispose();

                hv_Channels.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }

        public static void Create3DRGB(HObject ho_HeightImage, HObject ho_GrayImage, out HObject ho_MultiChannelImage,
      HTuple hv_DispGrade)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_imageOut = null, ho_Region = null, ho_Region1 = null;
            HObject ho_ObjectSelectedB = null, ho_ObjectSelectedG = null;
            HObject ho_ObjectSelectedR = null, ho_ImageCleared = null, ho_ImageResult_R = null;
            HObject ho_ImageResult_G = null, ho_ImageResult_B = null, ho_MultiChannelImage1 = null;
            HObject ho_ImageReduced = null;

            // Local copy input parameter variables 
            HObject ho_GrayImage_COPY_INP_TMP;
            ho_GrayImage_COPY_INP_TMP = new HObject(ho_GrayImage);



            // Local control variables 

            HTuple hv_Channels = new HTuple(), hv_Min = new HTuple();
            HTuple hv_Max = new HTuple(), hv_Range = new HTuple();
            HTuple hv_step = new HTuple(), hv_Sequence1 = new HTuple();
            HTuple hv_Sequence2 = new HTuple(), hv_Number = new HTuple();
            HTuple hv_Sequence_B = new HTuple(), hv_Sequence_G = new HTuple();
            HTuple hv_Sequence_R = new HTuple(), hv_Number_R = new HTuple();
            HTuple hv_Number_G = new HTuple(), hv_Number_B = new HTuple();
            HTuple hv_R = new HTuple(), hv_G1 = new HTuple(), hv_G2 = new HTuple();
            HTuple hv_G = new HTuple(), hv_B = new HTuple(), hv_Type = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_MultiChannelImage);
            HOperatorSet.GenEmptyObj(out ho_imageOut);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedB);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedG);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedR);
            HOperatorSet.GenEmptyObj(out ho_ImageCleared);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_R);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_G);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_B);
            HOperatorSet.GenEmptyObj(out ho_MultiChannelImage1);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced);
            try
            {
                try
                {
                    hv_Channels.Dispose();
                    HOperatorSet.CountChannels(ho_HeightImage, out hv_Channels);
                    if ((int)(new HTuple(hv_Channels.TupleGreater(1))) != 0)
                    {
                        ho_imageOut.Dispose(); ho_GrayImage_COPY_INP_TMP.Dispose();
                        HOperatorSet.Decompose2(ho_HeightImage, out ho_imageOut, out ho_GrayImage_COPY_INP_TMP
                            );
                    }
                    else
                    {
                        ho_imageOut.Dispose();
                        ho_imageOut = new HObject(ho_HeightImage);
                    }


                    hv_Min.Dispose(); hv_Max.Dispose(); hv_Range.Dispose();
                    HOperatorSet.MinMaxGray(ho_imageOut, ho_imageOut, 0, out hv_Min, out hv_Max,
                        out hv_Range);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Region.Dispose();
                        HOperatorSet.Threshold(ho_imageOut, out ho_Region, hv_Min + 0.001, hv_Max);
                    }

                    hv_Min.Dispose(); hv_Max.Dispose(); hv_Range.Dispose();
                    HOperatorSet.MinMaxGray(ho_Region, ho_imageOut, 0, out hv_Min, out hv_Max,
                        out hv_Range);
                    if ((int)(new HTuple(hv_DispGrade.TupleEqual("精细"))) != 0)
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / 255;
                        }
                    }
                    else if ((int)(new HTuple(hv_DispGrade.TupleEqual("适中"))) != 0)
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / (255 / 2);
                        }
                    }
                    else
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / (255 / 10);
                        }
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence1.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Min, hv_Max + hv_step, hv_step, out hv_Sequence1);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence2.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Min + hv_step, hv_Max + (2 * hv_step), hv_step,
                            out hv_Sequence2);
                    }
                    if ((int)(new HTuple((new HTuple(hv_Sequence1.TupleLength())).TupleGreater(
                        new HTuple(hv_Sequence2.TupleLength())))) != 0)
                    {
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            HTuple ExpTmpOutVar_0;
                            HOperatorSet.TupleRemove(hv_Sequence1, (new HTuple(hv_Sequence1.TupleLength()
                                )) - 1, out ExpTmpOutVar_0);
                            hv_Sequence1.Dispose();
                            hv_Sequence1 = ExpTmpOutVar_0;
                        }
                    }
                    else if ((int)(new HTuple((new HTuple(hv_Sequence1.TupleLength()
                        )).TupleLess(new HTuple(hv_Sequence2.TupleLength())))) != 0)
                    {
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            HTuple ExpTmpOutVar_0;
                            HOperatorSet.TupleRemove(hv_Sequence2, (new HTuple(hv_Sequence2.TupleLength()
                                )) - 1, out ExpTmpOutVar_0);
                            hv_Sequence2.Dispose();
                            hv_Sequence2 = ExpTmpOutVar_0;
                        }
                    }
                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_imageOut, out ho_Region1, hv_Sequence1, hv_Sequence2);


                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_Region1, out hv_Number);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_B.Dispose();
                        HOperatorSet.TupleGenSequence(1, hv_Number / 2, 1, out hv_Sequence_B);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_G.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Number / 4, 3 * (hv_Number / 4), 1, out hv_Sequence_G);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_R.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Number / 2, hv_Number, 1, out hv_Sequence_R);
                    }
                    ho_ObjectSelectedB.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedB, hv_Sequence_B);
                    ho_ObjectSelectedG.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedG, hv_Sequence_G);
                    ho_ObjectSelectedR.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedR, hv_Sequence_R);
                    hv_Number_R.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedR, out hv_Number_R);
                    hv_Number_G.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedG, out hv_Number_G);
                    hv_Number_B.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedB, out hv_Number_B);
                    ho_ImageCleared.Dispose();
                    HOperatorSet.GenImageProto(ho_imageOut, out ho_ImageCleared, 0);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_R.Dispose();
                        HOperatorSet.TupleGenSequence(0, 255, 255 / hv_Number_R, out hv_R);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G1.Dispose();
                        HOperatorSet.TupleGenSequence(0, 255, 255 / (hv_Number_G / 2), out hv_G1);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G2.Dispose();
                        HOperatorSet.TupleGenSequence(255, 0, -255 / (hv_Number_G / 2), out hv_G2);
                    }
                    hv_G.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G = new HTuple();
                        hv_G = hv_G.TupleConcat(hv_G1, hv_G2);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_B.Dispose();
                        HOperatorSet.TupleGenSequence(255, 0, -255 / hv_Number_B, out hv_B);
                    }


                    ho_ImageResult_R.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedR, ho_ImageCleared, out ho_ImageResult_R,
                        hv_R, "fill");

                    ho_ImageResult_G.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedG, ho_ImageCleared, out ho_ImageResult_G,
                        hv_G, "fill");

                    ho_ImageResult_B.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedB, ho_ImageCleared, out ho_ImageResult_B,
                        hv_B, "fill");


                    ho_MultiChannelImage.Dispose();
                    HOperatorSet.Compose3(ho_ImageResult_R, ho_ImageResult_G, ho_ImageResult_B,
                        out ho_MultiChannelImage);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConvertImageType(ho_MultiChannelImage, out ExpTmpOutVar_0, "byte");
                        ho_MultiChannelImage.Dispose();
                        ho_MultiChannelImage = ExpTmpOutVar_0;
                    }
                    hv_Type.Dispose();
                    HOperatorSet.GetImageType(ho_GrayImage_COPY_INP_TMP, out hv_Type);
                    if ((int)(new HTuple(hv_Type.TupleEqual("byte"))) != 0)
                    {
                        ho_MultiChannelImage1.Dispose();
                        HOperatorSet.Compose3(ho_GrayImage_COPY_INP_TMP, ho_GrayImage_COPY_INP_TMP,
                            ho_GrayImage_COPY_INP_TMP, out ho_MultiChannelImage1);
                        ho_ImageReduced.Dispose();
                        HOperatorSet.ReduceDomain(ho_MultiChannelImage1, ho_Region, out ho_ImageReduced
                            );
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.AddImage(ho_ImageReduced, ho_MultiChannelImage, out ExpTmpOutVar_0,
                                0.5, 30);
                            ho_MultiChannelImage.Dispose();
                            ho_MultiChannelImage = ExpTmpOutVar_0;
                        }
                    }
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                }


                ho_GrayImage_COPY_INP_TMP.Dispose();
                ho_imageOut.Dispose();
                ho_Region.Dispose();
                ho_Region1.Dispose();
                ho_ObjectSelectedB.Dispose();
                ho_ObjectSelectedG.Dispose();
                ho_ObjectSelectedR.Dispose();
                ho_ImageCleared.Dispose();
                ho_ImageResult_R.Dispose();
                ho_ImageResult_G.Dispose();
                ho_ImageResult_B.Dispose();
                ho_MultiChannelImage1.Dispose();
                ho_ImageReduced.Dispose();

                hv_Channels.Dispose();
                hv_Min.Dispose();
                hv_Max.Dispose();
                hv_Range.Dispose();
                hv_step.Dispose();
                hv_Sequence1.Dispose();
                hv_Sequence2.Dispose();
                hv_Number.Dispose();
                hv_Sequence_B.Dispose();
                hv_Sequence_G.Dispose();
                hv_Sequence_R.Dispose();
                hv_Number_R.Dispose();
                hv_Number_G.Dispose();
                hv_Number_B.Dispose();
                hv_R.Dispose();
                hv_G1.Dispose();
                hv_G2.Dispose();
                hv_G.Dispose();
                hv_B.Dispose();
                hv_Type.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_GrayImage_COPY_INP_TMP.Dispose();
                ho_imageOut.Dispose();
                ho_Region.Dispose();
                ho_Region1.Dispose();
                ho_ObjectSelectedB.Dispose();
                ho_ObjectSelectedG.Dispose();
                ho_ObjectSelectedR.Dispose();
                ho_ImageCleared.Dispose();
                ho_ImageResult_R.Dispose();
                ho_ImageResult_G.Dispose();
                ho_ImageResult_B.Dispose();
                ho_MultiChannelImage1.Dispose();
                ho_ImageReduced.Dispose();

                hv_Channels.Dispose();
                hv_Min.Dispose();
                hv_Max.Dispose();
                hv_Range.Dispose();
                hv_step.Dispose();
                hv_Sequence1.Dispose();
                hv_Sequence2.Dispose();
                hv_Number.Dispose();
                hv_Sequence_B.Dispose();
                hv_Sequence_G.Dispose();
                hv_Sequence_R.Dispose();
                hv_Number_R.Dispose();
                hv_Number_G.Dispose();
                hv_Number_B.Dispose();
                hv_R.Dispose();
                hv_G1.Dispose();
                hv_G2.Dispose();
                hv_G.Dispose();
                hv_B.Dispose();
                hv_Type.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }

        #endregion


        #region dll method
        [StructLayout(LayoutKind.Sequential)]
        public struct ImgPara
        {
            public IntPtr data;
            public int wid;
            public int hei;
            public int type;
            public int step;
            public int channels;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Vector3d
        {
            public double x;
            public double y;
            public double z;
            public Vector3d(double x, double y, double z)
            {
                this.x = x; this.y = y; this.z = z;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct PointArray
        {
            public IntPtr x;
            public IntPtr y;
            public IntPtr z;
            public int length;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ResultParaPindisk
        {
            public double plane_angle;
            public double plane_height_gap;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TiffPara
        {
            public int clos;
            public int rows;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CornerPoints
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public double[] x;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public double[] y;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public double[] z;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FuncPara
        {
            public float radius;
            public float normal_degree;
            public float curvature_threshold;
            public bool use_curvature;
            public float central_plane_size;
            public float distance_threshold;
            public int min_planar_points;
        }

        [DllImport("MeasurePindiskHeight.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool measure_pindisk_height_dllv3(ref ImgPara img,
        ref FuncPara func_para,
        ref Vector3d transformation_matrix,
        out ResultParaPindisk result,
        bool debug_mode);

/*        //preprocessing array to pointcloud
        [DllImport("MeasurePindiskHeight.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void measure_pindisk_height_dll3(ref PointArray pa,
            ref Vector3d transformationMatrix,
            out ResultParaPindisk result,
            ref TiffPara tiff_para,
            float central_plane_size,
            bool debug_mode);


        [DllImport("MeasurePindiskHeight.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void measure_pindisk_height_dll2(ref PointArray bottom_pa,
            ref PointArray central_pa,
            ref Vector3d transformationMatrix,
            out ResultParaPindisk result,
            bool debug_mode);

        //corner points and central cloud version
        [DllImport("MeasurePindiskHeight.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void measure_pindisk_height_dllv2(ref CornerPoints bottom_points,
        ref PointArray central_pa,
        ref FuncPara func_para,
        ref Vector3d transformation_matrix,
        out ResultParaPindisk result,
        bool debug_mode);
        [DllImport("MeasurePindiskHeight.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void measure_pindisk_height_dll(ref PointArray pa,
    ref Vector3d transformationMatrix,
    out ResultParaPindisk result,
    ref TiffPara tiff_para,
    float central_plane_size,
    bool debug_mode);*/
        #endregion
    }
}
