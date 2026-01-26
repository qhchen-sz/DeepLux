using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;

namespace VM.Halcon.Model
{

    public class TR
    {

        public static HImage GetRGBImage(HImage hImage)
        {
            HImage imageResult = new HImage();
            try
            {
                var (heightPara, grayPara) = Convert2ChannelHImage(hImage);
                var heightParaRaw = heightPara.Para;
                var grayParaRaw = grayPara.Para;
                //result imgpara 
                GCHandle handle = default;
                try
                {
                    byte[] renderBuffer = new byte[heightPara.Para.wid * heightPara.Para.hei * 3]; // BGR
                    handle = GCHandle.Alloc(renderBuffer, GCHandleType.Pinned);

                    TR.ImgPara img_render = new TR.ImgPara
                    {
                        data = handle.AddrOfPinnedObject(),
                        wid = heightPara.Para.wid,
                        hei = heightPara.Para.hei,
                        channels = 3,
                        type = 16, // CV_8UC3
                        step = heightPara.Para.wid * 3
                    };
                    // 调用你的 C++ DLL 接口传入 img_render
                    TR.tiff_render_dll(ref heightParaRaw, ref grayParaRaw, out img_render, false);
                    imageResult = GetImgParaAsHImage(img_render);
                    // 此时 renderBuffer 就包含了图像数据，你可以保存、展示等
                    ////保存图像数据
                    //if (debug_mode)
                    //{
                    //    string savePath = @"C:\Users\Administrator\Desktop\renderedCsharp.jpg";
                    //    TR.SaveImgParaAsHImage(img_render, savePath);
                    //}
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free(); // ✅ 释放 GCHandle，避免内存泄漏
                    if (heightPara.Handle.IsAllocated)
                    {
                        heightPara.Handle.Free(); // ✅ 释放 GCHandle，避免内存泄漏
                        heightPara.Buffer = null;
                    }
                    if (grayPara.Handle.IsAllocated)
                    {
                        grayPara.Handle.Free(); // ✅ 释放 GCHandle，避免内存泄漏
                        grayPara.Buffer = null;
                    }

                }
            }
            catch (Exception e)
            {

            }
            return imageResult;
        }

        #region my method
        public class ImgParaWithHandle
        {
            public ImgPara Para { get; set; }
            public GCHandle Handle { get; set; }

            public byte[] Buffer { get; set; }  // ✅ 加这个字段保存 imageData
        }

        public static HImage GetImgParaAsHImage(ImgPara img)
        {
            int width = img.wid;
            int height = img.hei;
            int channels = img.channels;

            if (img.type != 16 || channels != 3)
                throw new NotSupportedException("当前只支持 CV_8UC3 类型图像");

            int stride = img.step;
            int totalBytes = height * stride;
            byte[] interleaved = new byte[totalBytes];

            // 拷贝内存
            Marshal.Copy(img.data, interleaved, 0, totalBytes);

            // 拆通道（BGR 顺序）
            byte[] blue = new byte[width * height];
            byte[] green = new byte[width * height];
            byte[] red = new byte[width * height];

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int pixelIndex = i * stride + j * 3;

                    int idx = i * width + j;
                    blue[idx] = interleaved[pixelIndex];     // B
                    green[idx] = interleaved[pixelIndex + 1]; // G
                    red[idx] = interleaved[pixelIndex + 2];   // R
                }
            }

            // 创建 Halcon 图像通道
            HObject hBlue, hGreen, hRed;
            HOperatorSet.GenImage1(out hBlue, "byte", width, height, Marshal.UnsafeAddrOfPinnedArrayElement(blue, 0));
            HOperatorSet.GenImage1(out hGreen, "byte", width, height, Marshal.UnsafeAddrOfPinnedArrayElement(green, 0));
            HOperatorSet.GenImage1(out hRed, "byte", width, height, Marshal.UnsafeAddrOfPinnedArrayElement(red, 0));

            // 合并为 RGB 彩色图像
            HObject rgbImage;
            HOperatorSet.Compose3(hRed, hGreen, hBlue, out rgbImage); // 注意顺序：R, G, B
            HImage hImage = new HImage(rgbImage);
            // 保存图像
            //HOperatorSet.WriteImage(rgbImage, "tiff", 0, savePath);

            // 清理
            hRed.Dispose();
            hGreen.Dispose();
            hBlue.Dispose();
            rgbImage.Dispose();
            return hImage;
        }
        private static int MapHalconTypeToInt(string type)
        {
            switch (type)
            {
                case "byte":
                    return 0;  // 通常表示8位灰度图像
                case "uint2":
                    return 2;  // CV_16UC1, 16位图像（无符号）
                /*                case "int2":
                                    return 2;  // 16位图像（有符号）*/
                case "real":
                    return 5;  // CV_32FC1, 32位浮点图像
                default:
                    throw new NotSupportedException($"Unsupported Halcon image type: {type}");
            }
        }

        private static int GetBytesPerPixel(string type)
        {
            switch (type)
            {
                case "byte":
                    return 1;
                case "uint2":
                case "int2":
                    return 2;
                case "real":
                    return 4;
                default:
                    throw new NotSupportedException($"Unknown image type: {type}");
            }
        }
        public static ImgParaWithHandle ConvertSingleChannelHImageToImgParaFast(HImage hImage)
        {
            hImage.GetImageSize(out HTuple width, out HTuple height);
            HOperatorSet.GetImagePointer1(hImage, out HTuple ptr, out HTuple type, out HTuple step, out HTuple _);

            string pixelType = type.ToString().ToLower().Trim('"');
            int channels = hImage.CountChannels();
            int bytesPerPixel = GetBytesPerPixel(pixelType);
            int numBytes = width * height * bytesPerPixel;
            //added at 20251027
            step = step * bytesPerPixel;
            /*            Console.WriteLine("step:" + step.ToString());*/
            // 从Halcon复制到托管内存（仅一次）
            byte[] imageData = new byte[numBytes];
            Marshal.Copy(ptr, imageData, 0, numBytes);

            // 固定数组内存（防止GC移动）
            GCHandle handle = GCHandle.Alloc(imageData, GCHandleType.Pinned);

            var imgPara = new ImgPara
            {
                data = handle.AddrOfPinnedObject(),
                wid = width,
                hei = height,
                step = step,
                channels = channels,
                type = MapHalconTypeToInt(pixelType)
            };
            return new ImgParaWithHandle
            {
                Para = imgPara,
                Handle = handle,
                Buffer = imageData
            };
            /*            return new ImgPara
                        {
                            data = handle.AddrOfPinnedObject(),
                            wid = width,
                            hei = height,
                            step = step,
                            channels = channels,
                            type = MapHalconTypeToInt(pixelType)
                        };*/
        }

        //存在内存泄漏问题和两次copy内存风险
        public static ImgPara ConvertSingleChannelHImageToImgPara(HImage hImage)
        {
            hImage.GetImageSize(out HTuple width, out HTuple height);

            // 获取图像指针和类型
            HOperatorSet.GetImagePointer1(hImage, out HTuple ptr, out HTuple type, out HTuple step, out HTuple _);

            string pixelType = type.ToString().ToLower().Trim('"'); // 如 "byte", "uint2", 等
            int channels = 1;
            channels = hImage.CountChannels();
            Console.WriteLine("channels:" + channels.ToString());

            int bytesPerPixel = GetBytesPerPixel(pixelType);
            //added at 20251027
            step = step * bytesPerPixel;
            int numBytes = width * height * bytesPerPixel;

            byte[] imageData = new byte[numBytes];
            System.Runtime.InteropServices.Marshal.Copy(ptr, imageData, 0, numBytes);

            IntPtr newPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numBytes);
            System.Runtime.InteropServices.Marshal.Copy(imageData, 0, newPtr, numBytes);

            return new ImgPara
            {
                data = newPtr,
                wid = width,
                hei = height,
                step = step,
                channels = channels,
                type = MapHalconTypeToInt(pixelType)
            };
        }

        //split origin tiff image to height image and gray image
        public static (ImgParaWithHandle HeightPara, ImgParaWithHandle GrayPara) Convert2ChannelHImage(HImage hImage)
        {
            if (hImage == null)
            {
                throw new ArgumentNullException(nameof(hImage), "传入的HImage对象为null。");
            }

            HTuple width, height, channels;
            hImage.GetImageSize(out width, out height);
            channels = hImage.CountChannels();

            //只有高度图
            if (channels == 1)
            {
                // 高度图归一化（sub_image操作）
                HTuple min, max, range;
                HOperatorSet.MinMaxGray(hImage, hImage, 0, out min, out max, out range);
                HImage constImage = new HImage();
                HOperatorSet.GenImageProto(hImage, out HObject constObj, min);
                constImage.Dispose();
                constImage = new HImage(constObj);
                constObj.Dispose();

                HImage resultHeightImage = new HImage();
                HOperatorSet.SubImage(hImage, constImage, out HObject resultHeightObj, 1.0, 0.0);
                resultHeightImage.Dispose();
                resultHeightImage = new HImage(resultHeightObj);
                resultHeightObj.Dispose();
                ImgParaWithHandle heightPara = ConvertSingleChannelHImageToImgParaFast(resultHeightImage);
                ImgParaWithHandle grayPara = new ImgParaWithHandle
                {
                    Para = new ImgPara
                    {
                        data = IntPtr.Zero,
                        wid = 0,
                        hei = 0,
                        type = 0,
                        step = 0,
                        channels = 0
                    },
                    Handle = default(GCHandle), // 或 GCHandle.Alloc(new byte[0], GCHandleType.Pinned); 视场景而定
                    Buffer = null
                };
                return (heightPara, grayPara);
            }
            else
            {
                if (channels != 2)
                {
                    throw new ArgumentException("Only 2-channel images are supported for this function.");
                }

                // Step 1: 分解通道
                HObject heightObj, grayObj;
                HOperatorSet.Decompose2(hImage, out heightObj, out grayObj);
                HImage heightImage = new HImage(heightObj);
                HImage grayImage = new HImage(grayObj);

                // Step 2: 高度图归一化（sub_image操作）
                HTuple min, max, range;
                HOperatorSet.MinMaxGray(heightImage, heightImage, 0, out min, out max, out range);
                HImage constImage = new HImage();
                HOperatorSet.GenImageProto(heightImage, out HObject constObj, min);
                constImage.Dispose();
                constImage = new HImage(constObj);
                constObj.Dispose();

                HImage resultHeightImage = new HImage();
                HOperatorSet.SubImage(heightImage, constImage, out HObject resultHeightObj, 1.0, 0.0);
                resultHeightImage.Dispose();
                resultHeightImage = new HImage(resultHeightObj);
                resultHeightObj.Dispose();

                // Step 3: 转换成 ImgPara
                ImgParaWithHandle heightPara = ConvertSingleChannelHImageToImgParaFast(resultHeightImage);
                ImgParaWithHandle grayPara = ConvertSingleChannelHImageToImgParaFast(grayImage);

                return (heightPara, grayPara);
            }
        }
        //old version
        public static ImgPara ConvertHImageToimage(HImage hImage)
        {
            if (hImage == null)
            {
                throw new ArgumentNullException(nameof(hImage), "传入的HImage对象为null。");
            }

            HTuple width, height, channels, step;
            hImage.GetImageSize(out width, out height);
            channels = hImage.CountChannels();

            ImgPara imagePara = new ImgPara();

            Console.WriteLine("channels:" + channels.ToString());
            if (channels != 3 && channels != 1)
            {
                throw new ArgumentException("Only 1-channel (Gray) or 3-channel (BGR) images are supported.");
            }

            HObject InterImage = null;
            try
            {
                if (channels == 1)
                {
                    HObject hObject = new HObject();
                    HOperatorSet.Compose3(hImage, hImage, hImage, out hObject);
                    hImage = new HImage(hObject);
                    hObject.Dispose();
                }

                // 彩色图像
                HOperatorSet.InterleaveChannels(hImage, out InterImage, "rgb", "match", 255);
                HOperatorSet.GetImagePointer1(InterImage, out HTuple Ptr, out HTuple type, out step, out HTuple _height);

                // 深拷贝图像数据
                int numBytes = width * height * 3; // RGB图像，每个像素3字节
                byte[] imageData = new byte[numBytes];
                System.Runtime.InteropServices.Marshal.Copy(Ptr, imageData, 0, numBytes);
                IntPtr newPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numBytes);
                System.Runtime.InteropServices.Marshal.Copy(imageData, 0, newPtr, numBytes);

                imagePara.data = newPtr;


                imagePara.wid = width;
                imagePara.hei = height;
                imagePara.step = step;
            }
            finally
            {
                InterImage?.Dispose();
            }

            return imagePara;
        }

        public static HImage LoadTiffImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("图像路径不能为空。", nameof(imagePath));
            }

            if (!System.IO.File.Exists(imagePath))
            {
                throw new FileNotFoundException("图像文件不存在：", imagePath);
            }

            try
            {
                // 创建 HImage 对象并读取 TIFF 图像
                HImage hImage = new HImage();
                hImage.ReadImage(imagePath); // 支持tiff、jpg、png等多种格式

                return hImage;
            }
            catch (HalconException ex)
            {
                throw new InvalidOperationException($"读取 TIFF 图像失败: {ex.Message}", ex);
            }
        }


        #endregion

        #region DLL Method
        [StructLayout(LayoutKind.Sequential)]
        public struct ImgPara
        {
            public IntPtr data; // 对应 C++ 的 unsigned char*
            public int wid;     // 图像宽度
            public int hei;     // 图像高度
            public int type;    // 图像类型
            public int step;    // 图像的步幅（每行的字节数）
            public int channels;
        }

        [DllImport("TiffRender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void tiff_render_dll(
           ref ImgPara img_height,
           ref ImgPara img_gray,
           out ImgPara img_render,
           bool debug_mode);
        #endregion
    }
}
