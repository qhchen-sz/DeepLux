//using CPublicDefine;
using HalconDotNet;
using Plugin.Blob.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Models;
//using VisionCore.core;

namespace Plugin.Blob.ViewModels
{
    [Serializable]
    public  class PretreatHelp
    {
        #region 连通域
        /// <summary>
        /// 连通域
        /// </summary>
        #endregion
        public void Connection(HRegion inRegion,out HRegion outRegion)
        {
            //outRegion = new HRegion();
            outRegion = inRegion.Connection();
        }
        #region 合并
        /// <summary>
        /// 合并
        /// </summary>
        #endregion
        public void Union1(HRegion inRegion, out HRegion outRegion)
        {
            outRegion = inRegion.Union1();
        }
        #region 补集
        /// <summary>
        /// 补集
        /// </summary>
        #endregion
        public void Complement(HRegion inRegion1, out HRegion outRegion)
        {
            outRegion = inRegion1.Complement();
        }
        #region 相减
        /// <summary>
        /// 相减
        /// </summary>
        #endregion
        public void Difference(HRegion inRegion1, HRegion inRegion2, out HRegion outRegion)
        {
            outRegion = inRegion1.Difference(inRegion2);
        }
        #region 相交
        /// <summary>
        /// 相交
        /// </summary>
        #endregion
        public void Intersection(HRegion inRegion1, HRegion inRegion2, out HRegion outRegion)
        {
            outRegion = inRegion1.Intersection(inRegion2);
        }
        #region 孔洞填充
        /// <summary>
        /// 孔洞填充
        /// </summary>
        #endregion
        public void FillUp(HRegion inRegion1, out HRegion outRegion)
        {
            outRegion = inRegion1.FillUp();
        }
        #region 开运算
        /// <summary>
        /// 开运算
        /// </summary>
        #endregion
        public void OpeningRectangle1(HRegion inRegion1, int width,int height, out HRegion outRegion)
        {
                outRegion = inRegion1.OpeningRectangle1(width, height);
        }
        public void OpeningCircle(HRegion inRegion1, int radius, out HRegion outRegion)
        {
            outRegion = inRegion1.OpeningCircle((double)radius);
        }
        #region 闭运算
        /// <summary>
        /// 闭运算
        /// </summary>
        #endregion
        public void ClosingRectangle1(HRegion inRegion1, int width, int height, out HRegion outRegion)
        {
            outRegion = inRegion1.ClosingRectangle1(width, height);
        }
        public void ClosingCircle(HRegion inRegion1, int radius, out HRegion outRegion)
        {
            outRegion = inRegion1.ClosingCircle((double)radius);
        }
        #region 腐蚀
        /// <summary>
        /// 腐蚀
        /// </summary>
        #endregion
        public void ErosionRectangle1(HRegion inRegion1, int width, int height, out HRegion outRegion)
        {
            outRegion = inRegion1.ErosionRectangle1(width, height);
        }
        public void ErosionCircle(HRegion inRegion1, int radius, out HRegion outRegion)
        {
            outRegion = inRegion1.ErosionCircle((double)radius);
        }
        #region 膨胀
        /// <summary>
        /// 膨胀
        /// </summary>
        #endregion
        public void DilationRectangle1(HRegion inRegion1, int width, int height, out HRegion outRegion)
        {
            outRegion = inRegion1.DilationRectangle1(width, height);
        }
        public void DilationCircle(HRegion inRegion1, int radius, out HRegion outRegion)
        {
            outRegion = inRegion1.DilationCircle((double)radius);
        }
        #region 特征筛选
        /// <summary>
        /// 特征筛选
        /// </summary>
        #endregion
        public void SelectShape(HRegion inRegion1,  eFilterConditions eFilterConditions, eConditionalRelationship eConditionalRelationship ,double min, double max, out HRegion outRegion)
        {
            outRegion = new HRegion();
            outRegion.GenEmptyObj();
            if (min > max)
                min = 0;

            switch (eFilterConditions)
            {
                case eFilterConditions.面积:
                    outRegion = inRegion1.SelectShape("area", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.宽度:
                    outRegion = inRegion1.SelectShape("width", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.高度:
                    outRegion = inRegion1.SelectShape("height", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.X:
                    outRegion = inRegion1.SelectShape("column", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.Y:
                    outRegion = inRegion1.SelectShape("row", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.角度:
                    outRegion = inRegion1.SelectShape("phi", eConditionalRelationship.ToString(), min*Math.PI/180, max * Math.PI / 180);
                    break;
                case eFilterConditions.圆度:
                    outRegion = inRegion1.SelectShape("roundness", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.矩形度:
                    outRegion = inRegion1.SelectShape("rectangularity", eConditionalRelationship.ToString(), min, max);
                    break;
                case eFilterConditions.凸度:
                    outRegion = inRegion1.SelectShape("convexity", eConditionalRelationship.ToString(), min, max);
                    break;
                default:
                    outRegion = inRegion1;
                    break;
            }
            
        }
        #region 转换
        /// <summary>
        /// 转换
        /// </summary>
        #endregion
        public void ShapeTrans(HRegion inRegion1, eConversionType eConversionType ,out HRegion outRegion)
        {
            outRegion = new HRegion();
            outRegion.GenEmptyObj();
            
            switch (eConversionType)
            {
                case eConversionType.凸形:
                    outRegion = inRegion1.ShapeTrans("convex");
                    break;
                case eConversionType.椭圆:
                    outRegion = inRegion1.ShapeTrans("ellipse");
                    break;
                case eConversionType.最小外接圆:
                    outRegion = inRegion1.ShapeTrans("outer_circle");
                    break;
                case eConversionType.最大内接圆:
                    outRegion = inRegion1.ShapeTrans("inner_circle");
                    break;
                case eConversionType.最小外接矩形1:
                    outRegion = inRegion1.ShapeTrans("rectangle1");
                    break;
                case eConversionType.最小外接矩形2:
                    outRegion = inRegion1.ShapeTrans("rectangle2");
                    break;
                case eConversionType.最大内接矩形1:
                    outRegion = inRegion1.ShapeTrans("inner_rectangle1");
                    break;
                default:
                    break;
            }
            
        }
        #region 获取最大区域
        /// <summary>
        /// 获取最大区域
        /// </summary>
        #endregion
        public void SelectShapeStd(HRegion inRegion1, out HRegion outRegion)
        {
            outRegion = inRegion1.SelectShapeStd("max_area",70);
        }
        #region 矩形分割
        /// <summary>
        /// 矩形分割
        /// </summary>
        #endregion
        public void PartitionRectangle(HRegion inRegion1,int width,int height, out HRegion outRegion)
        {
            outRegion = inRegion1.PartitionRectangle(width, height);
        }
        #region 图像调整
        /// <summary>
        /// 彩色图转灰度图
        /// </summary>
        public void TransImage(HImage inImage, out HImage outImage, eTransImageType ImageType, eTransImageChannel Channel)
        {
            outImage = new HImage();
            HObject Temp1 = new HObject(); Temp1.GenEmptyObj(); Temp1.Dispose();
            HObject Temp2 = new HObject(); Temp2.GenEmptyObj(); Temp2.Dispose();
            HObject Temp3 = new HObject(); Temp3.GenEmptyObj(); Temp3.Dispose();
            HObject Image1 = new HObject(); Image1.GenEmptyObj(); Image1.Dispose();
            HObject Image2 = new HObject(); Image2.GenEmptyObj(); Image2.Dispose();
            HObject Image3 = new HObject(); Image3.GenEmptyObj(); Image3.Dispose();
            switch (ImageType)
            {
                //case eTransImageType.通用比例转换:
                //    HOperatorSet.Decompose3(inImage, out Temp1, out Temp2, out Temp3);
                //    HOperatorSet.TransFromRgb(Temp1, Temp2, Temp3, out Image1, out Image2, out Image3, "cielab");
                //    break;
                case eTransImageType.通用比例转换:
                case eTransImageType.RGB:
                    HOperatorSet.Decompose3(inImage, out Image1, out Image2, out Image3);
                    break;
                case eTransImageType.HSV:
                    HOperatorSet.Decompose3(inImage, out Temp1, out Temp2, out Temp3);
                    HOperatorSet.TransFromRgb(Temp1, Temp2, Temp3,out Image1,out Image2,out Image3,"hsv");
                    break;
                case eTransImageType.HSI:
                case eTransImageType.YUV:
                    HOperatorSet.Decompose3(inImage, out Temp1, out Temp2, out Temp3);
                    HOperatorSet.TransFromRgb(Temp1, Temp2, Temp3, out Image1, out Image2, out Image3, "hsi"); 
                    break;
                //case eTransImageType.YUV:
                //    HOperatorSet.Decompose3(inImage, out Temp1, out Temp2, out Temp3);
                //    HOperatorSet.TransFromRgb(Temp1, Temp2, Temp3, out Image1, out Image2, out Image3, "'yuv'");
                //    break;
                default:
                    break;
            }
            switch (Channel)
            {
                case eTransImageChannel.第一通道:
                    outImage = new HImage(Image1);
                    break;
                case eTransImageChannel.第二通道:
                    outImage = new HImage(Image2);
                    break;
                case eTransImageChannel.第三通道:
                    outImage = new HImage(Image3);
                    break;
                default:
                    break;
            }
            Temp1.Dispose();
            Temp2.Dispose();
            Temp3.Dispose();
            Image1.Dispose();
            Image2.Dispose();
            Image3.Dispose();
        }
        /// <summary>
        ///图像镜像
        /// </summary>
        /// mirror_image(Image : ImageMirror : Mode : )
        /// rotate_image (Image, ImageRotate, -OrientationAngle / rad(180) * 180, 'constant')
        /// List of values: 'bicubic', 'bilinear', 'constant', 'nearest_neighbor', 'weighted'
        public void MirrorImage(HImage inImage, out HImage outImage, eMirrorImageType W)
        {
            //HOperatorSet.MirrorImage(inImage, out HObject imageMirror, W);

            String mode = "";
            switch (W)
            {
                case eMirrorImageType.水平镜像:
                    mode = "column";
                    break;
                case eMirrorImageType.垂直镜像:
                    mode = "row";
                    break;
                case eMirrorImageType.对角镜像:
                    mode = "diagonal";
                    break;
            }
            outImage = inImage.MirrorImage(mode);
        }
        /// <summary>
        /// 图像旋转
        /// </summary>
        /// rotate_image(Image : ImageRotate : Phi, Interpolation : )
        public void RotateImage(HImage inImage, out HImage outImage, eRotateImageAngle angle)
        {
            //HOperatorSet.RotateImage(inImage, out HObject imageMirror, angle.ToString(), "constant");
            double a = 0;
            switch (angle)
            {
                case eRotateImageAngle._90:
                    a = 90;
                    break;
                case eRotateImageAngle._180:
                    a = 180;
                    break;
                case eRotateImageAngle._270:
                    a = 270;
                    break;
            }
            outImage = inImage.RotateImage(a, "constant");
        }
        /// <summary>
        /// 修改图像尺寸
        /// </summary>
        /// change_format(Image : ImagePart : Width, Height : )
        public void ChangeFormat(HImage inImage, out HImage outImage, int W, int H)
        {
            //HOperatorSet.ChangeFormat(inImage, out HObject imageMirror, W, H);
            outImage = inImage.ChangeFormat(W, H);
        }
        #endregion
        #region 滤波
        /// <summary>
        /// 均值滤波
        /// </summary>
        /// mean_image(Image : ImageMean : MaskWidth, MaskHeight : )
        public void MeanImage(HImage inImage, out HImage outImage, int W, int H)
        {
            //HOperatorSet.MeanImage( inImage, out HObject region, W, H);
            outImage = inImage.MeanImage(W, H);
        }
        public void MeanImage(HImage inImage, HRegion region,out HImage outImage, int W, int H)
        {
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).MeanImage(W, H)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);

        }
        /// <summary>
        /// 中值滤波
        /// </summary>
        /// median_image(Image : ImageMedian : MaskType, Radius, Margin : )
        public void MedianImage(HImage inImage, out HImage outImage, int radius, int margin)
        {
            //HOperatorSet.MedianImage(inImage, out HObject imageMedian, "square", radius, margin);
            outImage = inImage.MedianImage("square",radius,margin);
        }
        public void MedianImage(HImage inImage, HRegion region, out HImage outImage, int radius, int margin)
        {
            //HOperatorSet.MedianImage(inImage, out HObject imageMedian, "square", radius, margin);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).MedianImage("square", radius, margin)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 高斯滤波
        /// </summary>
        /// gauss_image(Image : ImageGauss : Size : )
        public void GaussImage(HImage inImage, out HImage outImage, int Size)
        {
            //HOperatorSet.GaussImage(inImage, out HObject ImageGauss, Size);
            outImage = inImage.GaussImage(Size);
        }
        public void GaussImage(HImage inImage, HRegion region, out HImage outImage, int Size)
        {
            //HOperatorSet.MedianImage(inImage, out HObject imageMedian, "square", radius, margin);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).GaussImage(Size)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        #endregion
        #region 形态学运算
        /// <summary>
        /// 灰度膨胀 
        /// </summary>
        /// gray_dilation(Image,SE :ImageDilation : : ) 灰度值膨胀   对灰度值进行操作而不是区域结构操作
        public void GrayDilation(HImage inImage, out HImage outImage, int W, int H)
        {
            //HOperatorSet.GrayDilationRect(inImage, out HObject ImageGauss, W, H);
            outImage = inImage.GrayDilationRect(H, W);
        }
        /// <summary>
        /// 灰度腐蚀 
        /// </summary>
        /// gray_erosion(Image,SE :ImageErosion : : ) 灰度值腐蚀
        public void GrayErosion(HImage inImage, out HImage outImage,int W, int H)
        {
            //HOperatorSet.GrayErosionRect(inImage, out HObject ImageGauss, W, H);
            outImage = inImage.GrayErosionRect(H, W);
        }
        #endregion
        #region 图像增强
        /// <summary>
        /// 图像锐化 
        /// </summary>
        /// emphasize(Image : ImageEmphasize : MaskWidth, MaskHeight, Factor : )201
        public void EmphaSize(HImage inImage,out HImage outImage, int W, int H, double Comp)
        {
            //HOperatorSet.Emphasize(inImage, out HObject imageEmphasize, W, H, Comp);
            outImage = inImage.Emphasize(W, H, Comp);
        }
        public void EmphaSize(HImage inImage, HRegion region,out HImage outImage, int W, int H, double Comp)
        {
            //HOperatorSet.Emphasize(inImage, out HObject imageEmphasize, W, H, Comp);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).Emphasize(W, H, Comp)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 增加图像对比度
        /// </summary>
        ///illuminate(Image : ImageIlluminate : MaskWidth, MaskHeight, Factor : )
        public void Illuminate(HImage inImage, out HImage outImage, int W, int H, double Comp)
        {
            //HOperatorSet.Illuminate(inImage, out HObject ImageIlluminate, W, H, Comp);
            outImage = inImage.Illuminate(W, H, Comp);
        }
        public void Illuminate(HImage inImage, HRegion region, out HImage outImage, int W, int H, double Comp)
        {
            //HOperatorSet.Illuminate(inImage, out HObject ImageIlluminate, W, H, Comp);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).Illuminate(W, H, Comp)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 图像亮度调节
        /// </summary>
        /// min_max_gray(Regions, Image : : Percent : Min, Max, Range)确定区域最大最小灰度值
        ///scale_image (ImageReduced, ImageScaled, 255 / Range, -Min * 255 / Range)
        public void ScaleImage(HImage inImage, out HImage outImage, double mult, double add)
        {
            //HOperatorSet.ScaleImage(inImage, out HObject ImageIlluminate, W, H);
            outImage = inImage.ScaleImage(mult, add);
        }
        public void ScaleImage(HImage inImage, HRegion region, out HImage outImage, double mult, double add)
        {
            //HOperatorSet.ScaleImage(inImage, out HObject ImageIlluminate, W, H);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).ScaleImage(mult, add)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 灰度开运算
        /// </summary>
        /// opening_rectangle1(Region : RegionOpening : Width, Height : )打开区域
        public void Opening(HImage inImage, out HImage outImage, int W, int H)
        {
            //HOperatorSet.GrayOpeningRect(inImage, out HObject ImageGauss, W, H);
            outImage = inImage.GrayOpeningRect(H, W);
        }
        public void Opening(HImage inImage, HRegion region, out HImage outImage, int W, int H)
        {
            //HOperatorSet.GrayOpeningRect(inImage, out HObject ImageGauss, W, H);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).GrayOpeningRect(H, W)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 灰度闭运算
        /// </summary>
        /// closing_rectangle1(Region : RegionClosing : Width, Height : )关闭一个区域。
        public void Closing(HImage inImage, out HImage outImage, int W, int H)
        {
            //HOperatorSet.GrayClosingRect(inImage, out HObject ImageGauss, W, H);
            outImage = inImage.GrayClosingRect(H, W);
        }
        public void Closing(HImage inImage, HRegion region, out HImage outImage, int W, int H)
        {
            //HOperatorSet.GrayClosingRect(inImage, out HObject ImageGauss, W, H);
            region.GetRegionPoints(out HTuple rows, out HTuple columns);
            outImage = inImage.CopyImage();
            HTuple Gray = (inImage.ReduceDomain(region).GrayClosingRect(H, W)).GetGrayval(rows, columns);
            outImage.SetGrayval(rows, columns, Gray);
        }
        /// <summary>
        /// 反转图像-颜色取反
        /// </summary>
        /// invert_image(Image : ImageInvert : : )
        public void InvertImage(HImage inImage, out HImage outImage, bool A)
        {
                if (!A)
                {
                    //  HOperatorSet.InvertImage(inImage, out imageInvert);
                    outImage = inImage.InvertImage();
                }
                else
                {
                    outImage = inImage;
                }
        }
        #endregion     
        #region 二值化
        /// <summary>
        /// 二值化
        /// </summary>
        /// <param name="LowThreshold">低阈值</param>
        /// <param name="HeighThreshold">高阈值</param>
        /// <param name="IsReverse">是否反色</param>
        /// <exception cref="ex">异常抛出</exception>
        public void Threshold(HImage inImage, out HImage outImage, double LowThreshold, double HeighThreshold, bool IsReverse)
        {
            try
            {
                //区域处理
                HObject ho_Regions, ho_Image_out1, ho_Image_out2;
                HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
                HOperatorSet.Threshold(inImage, out ho_Regions, LowThreshold, HeighThreshold);
                if (IsReverse)
                {
                    HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                    HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                    HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 255, "fill");
                    HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 255);
                    HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 0, "fill");
                }
                else
                {
                    HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                    HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                    HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 0, "fill");
                    HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 0);
                    HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 255, "fill");
                }
                outImage = new HImage(ho_Image_out2);
                return;
            }
            catch (Exception ex)
            {
                outImage = inImage;
                Debug.Write(ex.Message);
            }
        }
        public void Threshold(HImage inImage, HRegion region, out HImage outImage, double LowThreshold, double HeighThreshold, bool IsReverse)
        {
            try
            {
                //区域处理
                HObject ho_Regions, ho_Image_out1, ho_Image_out2;
                HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
                HOperatorSet.Threshold(inImage.ReduceDomain(region), out ho_Regions, LowThreshold, HeighThreshold);
                if (IsReverse)
                {
                    HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                    HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                    HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 255, "fill");
                    HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 255);
                    HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 0, "fill");
                }
                else
                {
                    HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                    HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                    HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 0, "fill");
                    HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 0);
                    HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 255, "fill");
                }
                outImage = new HImage(ho_Image_out2);
                return;
            }
            catch (Exception ex)
            {
                outImage = inImage;
                Debug.Write(ex.Message);
            }
        }
        /// <summary>
        /// 均值二值化
        /// </summary>
        /// <param name="Width">宽度</param>
        /// <param name="Height">高度</param>
        /// <param name="Skew">偏移</param>
        /// <param name="Type">类型</param>
        /// <exception cref="ex"> 
        ///var_threshold(Image : Region : MaskWidth, MaskHeight, StdDevScale, AbsThreshold, LightDark : )
        public void VarThreshold(HImage inImage, out HImage outImage, double VarWidth, double VarHeight, double VarSkew, eVarThresholdType varType)
        {
            try
            {
                HObject ho_Regions, ho_Image_out1, ho_Image_out2;
                HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
                string VarType = "";
                switch (varType)
                {
                    case eVarThresholdType.等于:
                        VarType = "equal";
                        break;
                    case eVarThresholdType.不等于:
                        VarType = "not_equal";
                        break;
                    case eVarThresholdType.小于等于:
                        VarType = "dark";
                        break;
                    case eVarThresholdType.大于等于:
                        VarType = "light";
                        break;
                }
                HOperatorSet.VarThreshold(inImage, out ho_Regions, VarWidth, VarHeight, VarSkew / 100, 30, VarType);
                HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 0, "fill");
                HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 0);
                HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 255, "fill");
                outImage = new HImage(ho_Image_out2);
            }
            catch (Exception ex)
            {
                outImage = inImage;
                Debug.Write(ex.Message);
            }
        }
        public void VarThreshold(HImage inImage, HRegion region, out HImage outImage, double VarWidth, double VarHeight, double VarSkew, eVarThresholdType varType)
        {
            try
            {
                HObject ho_Regions, ho_Image_out1, ho_Image_out2;
                HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
                string VarType = "";
                switch (varType)
                {
                    case eVarThresholdType.等于:
                        VarType = "equal";
                        break;
                    case eVarThresholdType.不等于:
                        VarType = "not_equal";
                        break;
                    case eVarThresholdType.小于等于:
                        VarType = "dark";
                        break;
                    case eVarThresholdType.大于等于:
                        VarType = "light";
                        break;
                }
                HOperatorSet.VarThreshold(inImage.ReduceDomain(region), out ho_Regions, VarWidth, VarHeight, VarSkew / 100, 30, VarType);
                HOperatorSet.GetImageSize(inImage, out hv_Width, out hv_Height);
                HOperatorSet.GenImageConst(out ho_Image_out1, "byte", hv_Width, hv_Height);
                HOperatorSet.OverpaintRegion(ho_Image_out1, ho_Regions, 0, "fill");
                HOperatorSet.GenImageProto(ho_Image_out1, out ho_Image_out2, 0);
                HOperatorSet.OverpaintRegion(ho_Image_out2, ho_Regions, 255, "fill");
                outImage = new HImage(ho_Image_out2);
            }
            catch (Exception ex)
            {
                outImage = inImage;
                Debug.Write(ex.Message);
            }
        }
        #endregion
    }
}
