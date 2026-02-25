using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Plugin.JiErHanDefectsDet.ViewModels
{

    //public static class NativeBootstrap
    //{
    //    [DllImport("kernel32.dll", SetLastError = true)]
    //    static extern bool SetDllDirectory(string lpPathName);

    //    public static void Init()
    //    {
    //        var dir = Path.GetDirectoryName(typeof(NativeBootstrap).Assembly.Location);
    //        SetDllDirectory(dir);
    //    }
    //}

    public class JEHDDModel
    {
        /// <summary>
        /// ImgPara(mask: CV_8UC1, single-channel) -> HALCON HObject image ("byte")
        /// 自动处理 step != wid 的情况（会做一次行拷贝，保证连续）
        /// </summary>
        public static HObject ImgParaMaskToHImageByte(in ImgPara mask)
        {
            if (mask.data == IntPtr.Zero)
                throw new ArgumentNullException(nameof(mask.data));
            if (mask.channels != 1)
                throw new ArgumentException("Mask must be single-channel (channels == 1).");
            // 你的 C++ mask 是 CV_8UC1，OpenCV type=0
            // 这里不强依赖 type 的数值（有些项目自定义），但建议你确保它是 byte mask
            if (mask.step <= 0 || mask.wid <= 0 || mask.hei <= 0)
                throw new ArgumentException("Invalid mask dimensions/step.");

            // 若 step == wid（8bit 单通道），说明行连续，可以直接 GenImage1
            if (mask.step == mask.wid)
            {
                HOperatorSet.GenImage1(out HObject img, "byte", mask.wid, mask.hei, mask.data);
                return img;
            }
            else {

                return null;
            }
            //// 否则说明每行有 padding，需要拷贝成连续 buffer 再 GenImage1
            //int dstRowBytes = mask.wid; // byte image, 1 byte/pixel
            //byte[] managed = new byte[mask.wid * mask.hei];

            //unsafe
            //{
            //    byte* src = (byte*)mask.data.ToPointer();
            //    fixed (byte* dst = managed)
            //    {
            //        for (int y = 0; y < mask.hei; y++)
            //        {
            //            Buffer.MemoryCopy(
            //                src + y * mask.step,
            //                dst + y * dstRowBytes,
            //                dstRowBytes,
            //                dstRowBytes);
            //        }
            //    }
            //}

            //GCHandle handle = default;
            //try
            //{
            //    handle = GCHandle.Alloc(managed, GCHandleType.Pinned);
            //    IntPtr ptr = handle.AddrOfPinnedObject();
            //    HOperatorSet.GenImage1(out HObject img, "byte", mask.wid, mask.hei, ptr);
            //    return img;
            //}
            //finally
            //{
            //    if (handle.IsAllocated) handle.Free();
            //}
        }
        /// <summary>
        /// 对 mask 图像统计所有出现过的灰度值（排除 0）。
        /// </summary>
        public static List<int> GetMaskNonZeroValues(HObject maskImage)
        {
            // 获取全域
            HOperatorSet.GetDomain(maskImage, out HObject domain);

            // 统计 0..255 直方图
            HOperatorSet.GrayHisto(domain, maskImage, out HTuple absHisto, out HTuple relHisto);

            var values = new List<int>();
            // absHisto[i] 表示灰度 i 的像素数
            for (int i = 1; i <= 255; i++)
            {
                if (absHisto[i].I > 0)
                    values.Add(i);
            }

            // 通常你希望更“稳定”的顺序：从大到小（255、127、85…）
            values.Sort((a, b) => b.CompareTo(a));
            return values;
        }

        // ----------------------------
        // 2) 让 mask 的数据类型“对齐原图类型”
        //    （原图是 real -> mask 转 real；原图是 int2 -> mask 转 int2；等等）
        // ----------------------------
        public static HObject ConvertMaskToMatchImageType(HObject maskByte, HObject original)
        {
            HOperatorSet.GetImageType(original, out HTuple oriType);
            string t = oriType.S ?? "byte";

            // maskByte 是 byte。对齐到原图类型。
            // Halcon ConvertImageType 支持: "byte", "int2", "uint2", "int4", "real", ...
            HOperatorSet.ConvertImageType(maskByte, out HObject maskConverted, t);
            return maskConverted;
        }
        // ----------------------------
        // 6) addWeighted: out = a*img1 + b*img2
        //    Halcon 用 AddImage / ScaleImage 来实现
        // ----------------------------
        public static HObject AddWeightedRgb(HObject rgb1, double alpha, HObject rgb2)
        {
            // rgb1/rgb2 都是 3通道 byte 图
            // Halcon 的 AddImage 支持多通道对应相加
            // out = rgb1*alpha + rgb2*beta
            HOperatorSet.ScaleImage(rgb1, out HObject s1, alpha, 0);
            HOperatorSet.ScaleImage(rgb2, out HObject s2, 1-alpha, 0);
            HOperatorSet.AddImage(s1, s2, out HObject sum, 1.0, 0.0);

            // 可能超出 0..255，截断到 byte
            HOperatorSet.ConvertImageType(sum, out HObject outByte, "byte");
            return outByte;
        }

        //UI友好型结果显示
        [Serializable]
        public class DefectResult
        {
            public int Id { get; set; }          // ← 缺陷唯一ID
            public double Area { get; set; }
            [NonSerialized]
            public HRegion _region;
            public HRegion Region
            {
                get => _region;
                set => _region = value;
            }
            //public HRegion Region { get; set; }  // ← 缺陷区域（核心）
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
        public struct FuncPara
        {
            public float normal_degree;
            public float curvature_threshold;
            public int min_defects_size;
            public float z_threshold;
            public float radius;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Defect
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
            public int[] defects_size;
        }

        [DllImport("SurfaceSmoothDetect.dll", EntryPoint = "detect_smooth_surface_v1", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool detect_smooth_surface_v1(ref ImgPara img,
            ref Vector3d transformation_matrix,
            ref FuncPara func_para,
            out ImgPara mask_para,
            out Defect defects,
            bool debug_mode);
        #endregion
    }
}
