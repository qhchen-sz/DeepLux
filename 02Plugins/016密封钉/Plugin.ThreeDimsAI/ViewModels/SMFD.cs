using HalconDotNet;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.ThreeDimsAI.ViewModels
{
    public class SMFD
    {

        #region CSharp method
        public static int GetOpenCVTypeFromPixelFormat(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format24bppRgb:
                    return 16; // Equivalent to CV_8UC3
                case PixelFormat.Format8bppIndexed:
                    return 0; // Equivalent to CV_8UC1
                default:
                    throw new ArgumentException("Unsupported pixel format");
            }
        }
        public static ImgPara GetBitmapData_gpt(Bitmap bitmap)
        {
            // 1) 锁定 Bitmap，拿到带 padding 的原始数据
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int width = bmpData.Width;
            int height = bmpData.Height;
            int paddedStride = bmpData.Stride;          // 含 padding
            int channels = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            int packedStride = width * channels;        // 我们想要的“紧凑” stride

            // 2) 拷贝带 padding 的数据到托管数组
            int paddedBytes = paddedStride * height;
            byte[] padded = new byte[paddedBytes];
            Marshal.Copy(bmpData.Scan0, padded, 0, paddedBytes);

            // 3) 创建一个紧凑的数组，把每行前 width*channels 字节拷过来
            byte[] packed = new byte[packedStride * height];
            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(
                    padded,
                    y * paddedStride,    // 源行起始
                    packed,
                    y * packedStride,    // 目标行起始
                    packedStride);       // 每行拷 packedStride 字节
            }

            // 4) 解锁 Bitmap
            bitmap.UnlockBits(bmpData);

            // 5) 分配非托管内存并拷 packed 数据进去
            IntPtr ptr = Marshal.AllocHGlobal(packed.Length);
            Marshal.Copy(packed, 0, ptr, packed.Length);

            // 6) 填充 ImgData
            var imgData = new ImgPara();
            imgData.data = ptr;
            imgData.wid = width;
            imgData.hei = height;
            imgData.step = packedStride;
            imgData.type = GetOpenCVTypeFromPixelFormat(bitmap.PixelFormat);
            imgData.channels = channels;

            return imgData;
        }
        // 调用完成后，记得用这个释放内存：
        public static void FreeBitmapData_gpt(ImgPara imgData)
        {
            if (imgData.data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(imgData.data);
                imgData.data = IntPtr.Zero;
            }
        }

        public static void MatToImgPara(Mat img, out ImgPara para)
        {
            para = new ImgPara();

            if (img.Empty() || img.Data == IntPtr.Zero)
            {
                throw new ArgumentException("Unsupported img format");
            }

            para.wid = img.Width;
            para.hei = img.Height;
            para.channels = img.Channels();
            para.step = (int)img.Step();
            para.type = img.Type();              // 和 C++ mat.type() 完全一致

            long totalBytes = img.Step() * img.Rows;

            // 分配非托管内存
            para.data = Marshal.AllocHGlobal((IntPtr)totalBytes);

            // 不使用 unsafe：使用 Marshal.Copy 进行深拷贝
            byte[] buffer = new byte[totalBytes];

            // 将 Mat 的内部数据复制到托管数组
            Marshal.Copy(img.Data, buffer, 0, (int)totalBytes);

            // 再从托管数组复制到非托管内存
            Marshal.Copy(buffer, 0, para.data, (int)totalBytes);
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

        public static void AiInit(bool enable, string engine_path) {
            AiInit_dll(enable, engine_path);
        }
        //仅推理用
        public static bool RunSMFD(ref ImgPara imgPara,
            float score_thres,
            float iou_thres,
            out Defect defects,
            bool debug_mode = false)
        {
            try
            {
                bool flag = mfd_segmentation_dll(ref imgPara, score_thres, iou_thres, out defects, debug_mode);
                FreeBitmapData_gpt(imgPara);
                return true && flag;
            }
            catch (Exception ex)
            {
                defects = default;
                return false;
            }

        }
/*        public static void RunSMFD(ref ImgPara imgPara,
            string engine_path,
            float score_thres,
            float iou_thres,
            out Defect defects,
            bool debug_mode = false)
        {
            mfd_segmentation_dll(ref imgPara, engine_path, score_thres, iou_thres, out defects, debug_mode);
            FreeBitmapData_gpt(imgPara);
        }*/
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
        public struct Defect
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public float[] area_bbox;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public double[] cx;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public double[] cy;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public double[] half_width;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public double[] half_height;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public double[] angle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public float[] height;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public float[] score;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public int[] label_id;
            public int size;
        }

        [DllImport("SegmentationMFD.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AiInit_dll(bool enable, string engine_path);

        [DllImport("SegmentationMFD.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool mfd_segmentation_dll(ref ImgPara imgpara,
            float score_thres,
            float iou_thres,
            out Defect defects,
            bool debug_mode);

/*        [DllImport("SegmentationMFD.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void mfd_segmentation_dll(ref ImgPara imgpara,
            string engine_path,
            float score_thres,
            float iou_thres,
            out Defect defects,
            bool debug_mode);*/
        #endregion
    }
}
