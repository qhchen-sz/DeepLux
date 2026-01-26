using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VM.Halcon.Config
{
    public static class hym3Dtransfor
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct ImgPara
        {
            public IntPtr data; // unsigned char* 在 C# 中使用 IntPtr
            public int wid;
            public int hei;
            public int step;
            public int type;
        }
        [DllImport("hym3Dtransfor.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateData();

        [DllImport("hym3Dtransfor.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyMyData(IntPtr dataV);
        [DllImport("hym3Dtransfor.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern ImgPara hym_tiff_to_mat(ref ImgPara data1);

        [DllImport("hym3Dtransfor.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern ImgPara merge(ref ImgPara data1, ref ImgPara data2);

        private static ImgPara Get(HalconDotNet.HImage image)
        {
            ImgPara imgPara = new ImgPara();
            HTuple Ptr = null, type, step, _height;
            if (image != null)
            {
                if (image.CountChannels() == 2)
                {
                    image = image.Decompose2(out HImage image2);
                }
                else if (image.CountChannels() > 2)
                    return imgPara;
                HOperatorSet.GetImageType(image, out HTuple hTuple);
                string Type = hTuple;


                switch (Type)
                {
                    case "byte":
                        HOperatorSet.GetImagePointer1(image, out Ptr, out type, out step, out _height);
                        imgPara.hei = _height;
                        imgPara.step = step;
                        imgPara.type = 0;
                        imgPara.wid = step;

                        break;
                    case "int2":
                        HOperatorSet.GetImagePointer1(image, out Ptr, out type, out step, out _height);
                        imgPara.step = step * 2;
                        imgPara.type = 3;
                        imgPara.wid = step;
                        imgPara.hei = _height;
                        break;
                    case "real":
                        HOperatorSet.GetImagePointer1(image, out Ptr, out type, out step, out _height);
                        imgPara.step = step * 4;
                        imgPara.type = 5;
                        imgPara.wid = step;
                        imgPara.hei = _height;
                        break;
                    default:
                        break;
                }
                // 深拷贝图像数据
                if (imgPara.wid != 0)
                {
                    int numBytes = imgPara.hei * imgPara.step; // RGB图像，每个像素3字节
                    byte[] imageData = new byte[numBytes];
                    System.Runtime.InteropServices.Marshal.Copy(Ptr, imageData, 0, numBytes);
                    IntPtr newPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numBytes);
                    System.Runtime.InteropServices.Marshal.Copy(imageData, 0, newPtr, numBytes);
                    imgPara.data = newPtr;
                }



            }

            return imgPara;
        }
        public static HImage merge(DispImage image)
        {
            HTuple width1=0, width2=0, height1 = 0, height2 = 0;
            
            HImage outImage = new HImage();
            if (image == null)
                return outImage;
            if(image.GrayImage.IsInitialized())
                image.GrayImage.GetImageSize(out  width1, out  height1);
            if(image.HeightImage.IsInitialized())
                image.HeightImage.GetImageSize(out  width2, out  height2);
            if (width1 > 1 && width2 > 1)//灰度图高度图渲染
            {
                var imgPara1 = Get(image.GrayImage);
                var imgPara2 = Get(image.HeightImage);
                var result2 = hym_tiff_to_mat(ref imgPara2);
                var result = merge(ref result2, ref imgPara1);
                outImage.GenImageInterleaved(result.data, "bgr", result.wid, result.hei, -1, "byte", 0, 0, 0, 0, -1, 0);
            }
            else if (width1 > 1)//输出灰度图
            {
                //int channeles = image.GrayImage.CountChannels();
                outImage = image.GrayImage;
            }
            else if (width2 > 1)//高度图渲染
            {
                
                var imgPara2 = Get(image.HeightImage);
                var result2 = hym_tiff_to_mat(ref imgPara2);
                
                outImage.GenImageInterleaved(result2.data, "bgr", result2.wid, result2.hei, -1, "byte", 0, 0, 0, 0, -1, 0);
            }
            return outImage;
        }
    }
}
