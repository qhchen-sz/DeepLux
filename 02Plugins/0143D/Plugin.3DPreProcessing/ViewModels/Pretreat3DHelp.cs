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

        #region 工业3D预处理
        public void FilterInvalidDepth(HImage inImage, out HImage outImage, double minZ, double maxZ, bool paintInvalid, double fillValue)
        {
            outImage = new HImage();
            HObject validRegion = null;
            HObject imageRegion = null;
            HObject invalidRegion = null;
            HObject resultObj = null;
            try
            {
                HOperatorSet.Threshold(inImage, out validRegion, minZ, maxZ);
                if (paintInvalid)
                {
                    HOperatorSet.GetImageSize(inImage, out HTuple width, out HTuple height);
                    HOperatorSet.GenRectangle1(out imageRegion, 0, 0, height - 1, width - 1);
                    HOperatorSet.Difference(imageRegion, validRegion, out invalidRegion);
                    HOperatorSet.PaintRegion(invalidRegion, inImage, out resultObj, fillValue, "fill");
                }
                else
                {
                    HOperatorSet.ReduceDomain(inImage, validRegion, out resultObj);
                }
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                validRegion?.Dispose();
                imageRegion?.Dispose();
                invalidRegion?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void MaskRectangle(HImage inImage, out HImage outImage, double row1, double col1, double row2, double col2, bool paintMask, double fillValue)
        {
            outImage = new HImage();
            HObject maskRegion = null;
            HObject imageRegion = null;
            HObject keepRegion = null;
            HObject resultObj = null;
            try
            {
                HOperatorSet.GetImageSize(inImage, out HTuple width, out HTuple height);
                row1 = Math.Max(0, Math.Min(height.D - 1, row1));
                row2 = Math.Max(0, Math.Min(height.D - 1, row2));
                col1 = Math.Max(0, Math.Min(width.D - 1, col1));
                col2 = Math.Max(0, Math.Min(width.D - 1, col2));
                double r1 = Math.Min(row1, row2);
                double r2 = Math.Max(row1, row2);
                double c1 = Math.Min(col1, col2);
                double c2 = Math.Max(col1, col2);

                HOperatorSet.GenRectangle1(out maskRegion, r1, c1, r2, c2);
                if (paintMask)
                {
                    HOperatorSet.PaintRegion(maskRegion, inImage, out resultObj, fillValue, "fill");
                }
                else
                {
                    HOperatorSet.GenRectangle1(out imageRegion, 0, 0, height - 1, width - 1);
                    HOperatorSet.Difference(imageRegion, maskRegion, out keepRegion);
                    HOperatorSet.ReduceDomain(inImage, keepRegion, out resultObj);
                }
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                maskRegion?.Dispose();
                imageRegion?.Dispose();
                keepRegion?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void RemoveSpike(HImage inImage, out HImage outImage, int medianSize, double threshold)
        {
            outImage = new HImage();
            HObject medianObj = null;
            HObject diffObj = null;
            HObject absDiffObj = null;
            HObject spikeRegion = null;
            HObject spikeMedianObj = null;
            HObject resultObj = null;
            try
            {
                medianSize = EnsureOdd(Math.Max(3, medianSize));
                HOperatorSet.MedianImage(inImage, out medianObj, "square", medianSize, "mirrored");
                HOperatorSet.SubImage(inImage, medianObj, out diffObj, 1.0, 0.0);
                HOperatorSet.AbsImage(diffObj, out absDiffObj);
                HOperatorSet.Threshold(absDiffObj, out spikeRegion, threshold, 999999999.0);
                HOperatorSet.ReduceDomain(medianObj, spikeRegion, out spikeMedianObj);
                HOperatorSet.PaintGray(spikeMedianObj, inImage, out resultObj);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                medianObj?.Dispose();
                diffObj?.Dispose();
                absDiffObj?.Dispose();
                spikeRegion?.Dispose();
                spikeMedianObj?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void FillSmallHoles(HImage inImage, out HImage outImage, double minZ, double maxZ, double maxArea, int fillWidth, int fillHeight)
        {
            outImage = new HImage();
            HObject validRegion = null;
            HObject imageRegion = null;
            HObject invalidRegion = null;
            HObject connected = null;
            HObject smallHoles = null;
            HObject fillCandidate = null;
            HObject fillHoleObj = null;
            HObject resultObj = null;
            HImage closed = null;
            try
            {
                HOperatorSet.Threshold(inImage, out validRegion, minZ, maxZ);
                HOperatorSet.GetImageSize(inImage, out HTuple width, out HTuple height);
                HOperatorSet.GenRectangle1(out imageRegion, 0, 0, height - 1, width - 1);
                HOperatorSet.Difference(imageRegion, validRegion, out invalidRegion);
                HOperatorSet.Connection(invalidRegion, out connected);
                HOperatorSet.SelectShape(connected, out smallHoles, "area", "and", 1.0, Math.Max(1.0, maxArea));

                fillWidth = Math.Max(1, fillWidth);
                fillHeight = Math.Max(1, fillHeight);
                closed = inImage.GrayClosingRect(fillHeight, fillWidth);
                fillCandidate = closed;
                HOperatorSet.ReduceDomain(fillCandidate, smallHoles, out fillHoleObj);
                HOperatorSet.PaintGray(fillHoleObj, inImage, out resultObj);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                validRegion?.Dispose();
                imageRegion?.Dispose();
                invalidRegion?.Dispose();
                connected?.Dispose();
                smallHoles?.Dispose();
                fillHoleObj?.Dispose();
                resultObj?.Dispose();
                closed?.Dispose();
            }
        }

        public void EdgePreserveSmooth(HImage inImage, out HImage outImage, int width, int height, double edgeThreshold)
        {
            outImage = new HImage();
            HObject meanObj = null;
            HObject diffObj = null;
            HObject absDiffObj = null;
            HObject smoothRegion = null;
            HObject smoothMeanObj = null;
            HObject resultObj = null;
            try
            {
                width = Math.Max(1, width);
                height = Math.Max(1, height);
                HOperatorSet.MeanImage(inImage, out meanObj, width, height);
                HOperatorSet.SubImage(inImage, meanObj, out diffObj, 1.0, 0.0);
                HOperatorSet.AbsImage(diffObj, out absDiffObj);
                HOperatorSet.Threshold(absDiffObj, out smoothRegion, 0.0, edgeThreshold);
                HOperatorSet.ReduceDomain(meanObj, smoothRegion, out smoothMeanObj);
                HOperatorSet.PaintGray(smoothMeanObj, inImage, out resultObj);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                meanObj?.Dispose();
                diffObj?.Dispose();
                absDiffObj?.Dispose();
                smoothRegion?.Dispose();
                smoothMeanObj?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void CorrectRowStripe(HImage inImage, out HImage outImage, int width, double strength)
        {
            outImage = new HImage();
            HObject trendObj = null;
            HObject residualObj = null;
            HObject scaledResidualObj = null;
            HObject resultObj = null;
            try
            {
                width = EnsureOdd(Math.Max(3, width));
                strength = Math.Max(0.0, Math.Min(1.0, strength));
                HOperatorSet.MeanImage(inImage, out trendObj, 1, width);
                HOperatorSet.SubImage(inImage, trendObj, out residualObj, 1.0, 0.0);
                HOperatorSet.ScaleImage(residualObj, out scaledResidualObj, -strength, 0.0);
                HOperatorSet.AddImage(inImage, scaledResidualObj, out resultObj, 1.0, 0.0);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                trendObj?.Dispose();
                residualObj?.Dispose();
                scaledResidualObj?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void CorrectColumnStripe(HImage inImage, out HImage outImage, int height, double strength)
        {
            outImage = new HImage();
            HObject trendObj = null;
            HObject residualObj = null;
            HObject scaledResidualObj = null;
            HObject resultObj = null;
            try
            {
                height = EnsureOdd(Math.Max(3, height));
                strength = Math.Max(0.0, Math.Min(1.0, strength));
                HOperatorSet.MeanImage(inImage, out trendObj, height, 1);
                HOperatorSet.SubImage(inImage, trendObj, out residualObj, 1.0, 0.0);
                HOperatorSet.ScaleImage(residualObj, out scaledResidualObj, -strength, 0.0);
                HOperatorSet.AddImage(inImage, scaledResidualObj, out resultObj, 1.0, 0.0);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                trendObj?.Dispose();
                residualObj?.Dispose();
                scaledResidualObj?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void FilterComponents(HImage inImage, out HImage outImage, double minZ, double maxZ, double minArea, double maxArea, bool keepMax)
        {
            outImage = new HImage();
            HObject region = null;
            HObject connected = null;
            HObject selected = null;
            HObject resultObj = null;
            try
            {
                HOperatorSet.Threshold(inImage, out region, minZ, maxZ);
                HOperatorSet.Connection(region, out connected);
                if (keepMax)
                    HOperatorSet.SelectShapeStd(connected, out selected, "max_area", 70);
                else
                    HOperatorSet.SelectShape(connected, out selected, "area", "and", Math.Max(1.0, minArea), Math.Max(minArea, maxArea));
                HOperatorSet.ReduceDomain(inImage, selected, out resultObj);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                region?.Dispose();
                connected?.Dispose();
                selected?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void RemovePlaneTrend(HImage inImage, out HImage outImage, double minZ, double maxZ, bool keepCenterHeight)
        {
            outImage = new HImage();
            HObject domain = null;
            HObject planeObj = null;
            HObject resultObj = null;
            try
            {
                HOperatorSet.Threshold(inImage, out domain, minZ, maxZ);
                HOperatorSet.AreaCenter(domain, out _, out HTuple centerR, out HTuple centerC);
                HOperatorSet.FitSurfaceFirstOrder(domain, inImage, "regression", 5, 0.1, out HTuple alpha, out HTuple beta, out HTuple gamma);
                HOperatorSet.GetImageSize(inImage, out HTuple width, out HTuple height);
                HOperatorSet.GenImageSurfaceFirstOrder(out planeObj, "real", alpha, beta, gamma, centerR, centerC, width, height);
                HOperatorSet.SubImage(inImage, planeObj, out resultObj, 1.0, keepCenterHeight ? gamma.D : 0.0);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                domain?.Dispose();
                planeObj?.Dispose();
                resultObj?.Dispose();
            }
        }

        public void NormalizeHeight(HImage inImage, out HImage outImage, double scale, double offset)
        {
            outImage = new HImage();
            HObject resultObj = null;
            try
            {
                HOperatorSet.ScaleImage(inImage, out resultObj, scale, offset);
                outImage = new HImage(resultObj);
            }
            catch
            {
                outImage = new HImage(inImage);
            }
            finally
            {
                resultObj?.Dispose();
            }
        }

        private int EnsureOdd(int value)
        {
            if (value < 1) return 1;
            return value % 2 == 0 ? value + 1 : value;
        }
        #endregion
    }
}
