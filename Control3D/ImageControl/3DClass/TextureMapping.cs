using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;

namespace ImageControl
{
    public class TextureMapping
    {
        public DiffuseMaterial m_material;
        private bool m_bPseudoColor;

        public TextureMapping() => this.SetRGBMaping();

        public unsafe void SetRGBMaping()
        {
            WriteableBitmap image = new WriteableBitmap(64, 64, 96.0, 96.0, PixelFormats.Bgr24, (BitmapPalette)null);
            image.Lock();
            byte* backBuffer = (byte*)(void*)image.BackBuffer;
            int backBufferStride = image.BackBufferStride;
            for (int index1 = 0; index1 < 16; ++index1)
            {
                for (int index2 = 0; index2 < 16; ++index2)
                {
                    for (int index3 = 0; index3 < 16; ++index3)
                    {
                        int num1 = index2 % 4 * 16 + index3;
                        int num2 = index1 * 4 + index2 / 4;

                        // 计算偏移位置
                        long offset = ((long)(IntPtr)num2 * backBufferStride + (long)(IntPtr)num1 * 3);

                        // 写入字节值到指定偏移位置
                        backBuffer[offset] = (byte)(index3 * 17);
                        backBuffer[offset + 1] = (byte)(index2 * 17);
                        backBuffer[offset + 2] = (byte)(index1 * 17);
                    }
                }
            }
            image.AddDirtyRect(new Int32Rect(0, 0, 64, 64));
            image.Unlock();
            ImageBrush imageBrush = new ImageBrush((ImageSource)image);
            imageBrush.ViewportUnits = BrushMappingMode.Absolute;
            this.m_material = new DiffuseMaterial();
            this.m_material.Brush = (Brush)imageBrush;
            this.m_bPseudoColor = false;
        }

        public unsafe void SetPseudoMaping()
        {
            WriteableBitmap image = new WriteableBitmap(64, 64, 96.0, 96.0, PixelFormats.Bgr24, (BitmapPalette)null);
            image.Lock();
            byte* backBuffer = (byte*)(void*)image.BackBuffer;
            int backBufferStride = image.BackBufferStride;
            for (int index1 = 0; index1 < 64; ++index1)
            {
                for (int index2 = 0; index2 < 64; ++index2)
                {
                    Color color = TextureMapping.PseudoColor((double)(index1 * 64 + index2) / 4095.0);

                    // 计算偏移位置
                    long offset = ((long)(IntPtr)index1 * backBufferStride + (long)(IntPtr)index2 * 3);

                    // 写入颜色值到指定偏移位置
                    backBuffer[offset] = color.B;
                    backBuffer[offset + 1] = color.G;
                    backBuffer[offset + 2] = color.R;
                }
            }
            image.AddDirtyRect(new Int32Rect(0, 0, 64, 64));
            image.Unlock();
            ImageBrush imageBrush = new ImageBrush((ImageSource)image);
            imageBrush.ViewportUnits = BrushMappingMode.Absolute;
            this.m_material = new DiffuseMaterial();
            this.m_material.Brush = (Brush)imageBrush;
            this.m_bPseudoColor = true;
        }

        public Point GetMappingPosition(Color color)
        {
            return TextureMapping.GetMappingPosition(color, this.m_bPseudoColor);
        }

        public static Point GetMappingPosition(Color color, bool bPseudoColor)
        {
            if (bPseudoColor)
            {
                double num1 = (double)color.R / (double)byte.MaxValue;
                double num2 = (double)color.G / (double)byte.MaxValue;
                double num3 = (double)color.B / (double)byte.MaxValue;
                int num4 = (int)((num3 < num2 || num3 <= num1 ? (num2 <= num3 || num3 < num1 ? (num2 < num1 || num1 <= num3 ? 0.75 + 0.25 * (1.0 - num2) : 0.5 + 0.25 * num1) : 0.25 + 0.25 * (1.0 - num3)) : 0.25 * num2) * 4095.0);
                if (num4 < 0)
                    num4 = 0;
                if (num4 > 4095)
                    num4 = 4095;
                int num5 = num4 / 64;
                return new Point((double)(num4 % 64) / 64.0, (double)num5 / 64.0);
            }
            int num6 = (int)color.R / 17;
            int num7 = (int)color.G / 17;
            int num8 = (int)color.B / 17;
            return new Point((double)(num7 % 4 * 16 + num8) / 63.0, (double)(num6 * 4 + num7 / 4) / 63.0);
        }

        public static Color PseudoColor(double k)
        {
            if (k < 0.0)
                k = 0.0;
            if (k > 1.0)
                k = 1.0;
            double num1;
            double num2 = num1 = 0.0;
            double num3;
            double num4;
            double num5;
            if (k < 0.25)
            {
                num3 = 0.0;
                num4 = 4.0 * k;
                num5 = 1.0;
            }
            else if (k < 0.5)
            {
                num3 = 0.0;
                num4 = 1.0;
                num5 = 1.0 - 4.0 * (k - 0.25);
            }
            else if (k < 0.75)
            {
                num3 = 4.0 * (k - 0.5);
                num4 = 1.0;
                num5 = 0.0;
            }
            else
            {
                num3 = 1.0;
                num4 = 1.0 - 4.0 * (k - 0.75);
                num5 = 0.0;
            }
            return Color.FromRgb((byte)(num3 * (double)byte.MaxValue + 0.0), (byte)(num4 * (double)byte.MaxValue + 0.0), (byte)(num5 * (double)byte.MaxValue + 0.0));
        }
        public static Color PseudoColor(double k,double gray)
        {
            if (k < 0.0)
                k = 0.0;
            if (k > 1.0)
                k = 1.0;
            double num1;
            double num2 = num1 = 0.0;
            double num3;
            double num4;
            double num5;
            if (k < 0.25)
            {
                num3 = 0.0;
                num4 = 4.0 * k;
                num5 = 1.0;
            }
            else if (k < 0.5)
            {
                num3 = 0.0;
                num4 = 1.0;
                num5 = 1.0 - 4.0 * (k - 0.25);
            }
            else if (k < 0.75)
            {
                num3 = 4.0 * (k - 0.5);
                num4 = 1.0;
                num5 = 0.0;
            }
            else
            {
                num3 = 1.0;
                num4 = 1.0 - 4.0 * (k - 0.75);
                num5 = 0.0;
            }
            //byte  i= (byte)(gray);
            //return Color.FromRgb((byte)((num3 * (double)byte.MaxValue + gray * 2) / 3), (byte)((num4 * (double)byte.MaxValue + gray * 2) / 3), (byte)((num5 * (double)byte.MaxValue + gray * 2) / 3));
            return Color.FromRgb((byte)(gray), (byte)(gray), (byte)(gray));
        }
    }
}
