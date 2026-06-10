using System;
using HalconDotNet;

namespace Plugin._3DPreProcessing.ViewModels
{
    [Serializable]
    public class Pretreat3DHelp
    {
        #region 3维滤波
        public void MeanImage(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.MeanImage(width, height);
        }

        public void MedianImage(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.MedianImage("square", width, "mirrored");
        }

        public void GaussImage(HImage inImage, out HImage outImage, int size)
        {
            outImage = inImage.GaussImage(size);
        }
        #endregion

        #region 3维形态学（深度图形态学）
        public void GrayDilation(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.GrayDilationRect(height, width);
        }

        public void GrayErosion(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.GrayErosionRect(height, width);
        }

        public void GrayOpening(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.GrayOpeningRect(height, width);
        }

        public void GrayClosing(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = inImage.GrayClosingRect(height, width);
        }
        #endregion

        #region 深度阈值裁剪
        public void ClipDepth(HImage inImage, out HImage outImage, double minZ, double maxZ)
        {
            outImage = new HImage();
            try
            {
                HOperatorSet.Threshold(inImage, out HObject region, minZ, maxZ);
                HOperatorSet.ReduceDomain(inImage, region, out HObject reduced);
                outImage = new HImage(reduced);
                region.Dispose();
                reduced.Dispose();
            }
            catch
            {
                outImage = new HImage(inImage);
            }
        }
        #endregion

        #region 深度填充（基于形态学填充深度图空洞）
        public void FillDepth(HImage inImage, out HImage outImage, int width, int height)
        {
            outImage = new HImage();
            try
            {
                HImage closed = inImage.GrayClosingRect(height, width);
                HImage opened = closed.GrayOpeningRect(height, width);
                outImage = new HImage(opened);
                closed.Dispose();
                opened.Dispose();
            }
            catch
            {
                outImage = new HImage(inImage);
            }
        }
        #endregion
    }
}
