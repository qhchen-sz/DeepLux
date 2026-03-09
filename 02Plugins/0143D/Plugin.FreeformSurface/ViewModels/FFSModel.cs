using HalconDotNet;
using System;
using System.Collections.Generic;

namespace Plugin.FreeformSurface.ViewModels
{

    public class FFSModel
    {
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

        /// <summary>
        /// 自由曲面缺陷检测算法
        /// </summary>
        /// <param name="image">输入图像（可以是单通道或双通道）</param>
        /// <param name="zScale">Z轴缩放系数</param>
        /// <param name="shieldWidth">腐蚀宽度</param>
        /// <param name="shieldHeight">腐蚀高度</param>
        /// <param name="partitionHeight">切分高度</param>
        /// <param name="zlSelect">凹陷阈值（低阈值）</param>
        /// <param name="zhSelect">凸起阈值（高阈值）</param>
        /// <param name="imageMask">输出的掩码图像</param>
        /// <returns>处理是否成功</returns>
        public static bool SurfaceDefectDetection(
            HObject image,
            double zScale,
            double shieldWidth,
            double shieldHeight,
            double partitionHeight,
            double zlSelect,
            double zhSelect,
            out HObject imageMask)
        {
            // 初始化输出
            HOperatorSet.GenEmptyObj(out imageMask);

            try
            {
                // 1. 检查图像通道数
                HOperatorSet.CountChannels(image, out HTuple channels);

                HObject image1, image2;
                if (channels == 2)
                {
                    // 双通道图像，分解为两个单通道
                    HOperatorSet.Decompose2(image, out image1, out image2);
                }
                else if (channels == 1)
                {
                    // 单通道图像
                    image1 = image;
                    HOperatorSet.GenEmptyObj(out image2);
                }
                else
                {
                    // 不支持的通道数
                    return false;
                }

                // 2. 获取图像尺寸
                HOperatorSet.GetImageSize(image1, out HTuple width, out HTuple height);

                // 3. 创建清理后的图像（real类型）
                HOperatorSet.GenImageConst(out HObject imageCleared, "real", width, height);

                // 4. 获取图像的最小最大灰度值
                HOperatorSet.MinMaxGray(image1, image1, 0, out HTuple min, out HTuple max, out HTuple range);

                // 5. 阈值分割，获取有效区域
                HOperatorSet.Threshold(image1, out HObject region, min + 3, max);

                // 6. 计算差集
                HOperatorSet.Difference(image1, region, out HObject regionDifference);

                // 7. 连通域分析
                HOperatorSet.Connection(region, out HObject connectedRegions);

                // 8. 腐蚀操作
                HOperatorSet.ErosionRectangle1(connectedRegions, out HObject regionErosion, shieldWidth, shieldHeight);

                // 9. 转换为最小外接矩形
                HOperatorSet.ShapeTrans(regionErosion, out HObject regionTrans, "rectangle1");

                // 10. 计算最小外接矩形的宽度
                HOperatorSet.RegionFeatures(regionTrans, "width", out HTuple value1);

                // 11. 切分最小外接矩形
                HOperatorSet.PartitionRectangle(regionTrans, out HObject partitioned, value1[0].D + 100, partitionHeight);

                // 12. 筛选面积大于10的区域
                HOperatorSet.SelectShape(partitioned, out partitioned, "area", "and", 10, "max");

                // 13. 统计区域数量
                HOperatorSet.CountObj(partitioned, out HTuple number);

                // 14. 对每个分区进行处理
                for (int index1 = 1; index1 <= number.I; index1++)
                {
                    // 选择当前对象
                    HOperatorSet.SelectObj(partitioned, out HObject objectSelected, index1);

                    // 灰度投影
                    HOperatorSet.GrayProjections(objectSelected, image1, "simple", out HTuple horProjection, out HTuple vertProjection);

/*                    // 获取峰值中心
                    HTuple max1 = vertProjection.TupleMax();
                    HTuple indices = vertProjection.TupleFind(max1);

                    // 计算最小面积旋转外接矩形
                    HOperatorSet.SmallestRectangle2(objectSelected, out HTuple row, out HTuple column,
                        out HTuple phi, out HTuple length1, out HTuple length2);

                    // 全局中心坐标
                    double fRow = row.D;
                    double fCol = column.D - length2.D + indices.D;*/

                    // 获取区域内的点和灰度值
                    HOperatorSet.GetRegionPoints(objectSelected, out HTuple rows, out HTuple columns);
                    HOperatorSet.GetGrayval(image1, rows, columns, out HTuple grayval);

                    // 计算区域的高度和宽度
                    HOperatorSet.RegionFeatures(objectSelected, new HTuple("height", "width"), out HTuple value);
                    HTuple intValue = value.TupleInt();

                    // 创建矩阵并填充灰度值
                    HOperatorSet.CreateMatrix(intValue[0].I, intValue[1].I, grayval, out HTuple matrixID);

/*                    // 计算每行的最大值
                    HOperatorSet.MaxMatrix(matrixID, "rows", out HTuple matrixMaxID);
                    HOperatorSet.GetFullMatrix(matrixMaxID, out HTuple values1);*/

                    // 创建投影矩阵
                    HOperatorSet.CreateMatrix(1, intValue[1].I, vertProjection, out HTuple matrixID1);

                    // 重复矩阵以匹配原始矩阵的行数
                    HOperatorSet.RepeatMatrix(matrixID1, intValue[0].I, 1, out HTuple matrixRepeatedID);

                    // 矩阵相减
                    HOperatorSet.SubMatrix(matrixID, matrixRepeatedID, out HTuple matrixSubID);

                    // 缩放矩阵
                    HOperatorSet.ScaleMatrix(matrixSubID, zScale, out HTuple matrixScaledID);

                    // 获取处理后的值
                    HOperatorSet.GetFullMatrix(matrixScaledID, out HTuple values);

                    // 将处理后的值设置回图像
                    HOperatorSet.SetGrayval(imageCleared, rows, columns, values);

                    // 清理矩阵
                    HOperatorSet.ClearMatrix(matrixID);
/*                    HOperatorSet.ClearMatrix(matrixMaxID);*/
                    HOperatorSet.ClearMatrix(matrixID1);
                    HOperatorSet.ClearMatrix(matrixRepeatedID);
                    HOperatorSet.ClearMatrix(matrixSubID);
                    HOperatorSet.ClearMatrix(matrixScaledID);
                }

                // 15. 区域缩放和腐蚀
                HOperatorSet.ZoomRegion(region, out HObject regionZoom, 0.02, 0.01);
                HOperatorSet.Erosion1(region, regionZoom, out HObject regionErosion1, 1);
                HOperatorSet.Difference(image1, regionErosion1, out HObject regionDifference1);

                // 16. 将差集区域填充为0
                HOperatorSet.PaintRegion(regionDifference1, imageCleared, out imageCleared, 0, "fill");

                // 17. 阈值分割，生成低阈值区域（凹陷）
                HOperatorSet.Threshold(imageCleared, out HObject regionLow, "min", zlSelect);

                // 18. 阈值分割，生成高阈值区域（凸起）
                HOperatorSet.Threshold(imageCleared, out HObject regionHeight, zhSelect, "max");

                // 19. 如果有第二通道，创建掩码图像
                if (channels == 2)
                {
                    HOperatorSet.GenImageProto(image2, out imageMask, 0);
                }
                else
                {
                    HOperatorSet.GenImageConst(out imageMask, "byte", width, height);
                }

                // 20. 在掩码图像上绘制低阈值区域（灰度值125）
                HOperatorSet.PaintRegion(regionLow, imageMask, out imageMask, 125, "fill");

                // 21. 在掩码图像上绘制高阈值区域（灰度值255）
                HOperatorSet.PaintRegion(regionHeight, imageMask, out imageMask, 255, "fill");

                // 清理临时对象
                region?.Dispose();
                regionDifference?.Dispose();
                connectedRegions?.Dispose();
                regionErosion?.Dispose();
                regionTrans?.Dispose();
                partitioned?.Dispose();
                regionLow?.Dispose();
                regionHeight?.Dispose();
                regionZoom?.Dispose();
                regionErosion1?.Dispose();
                regionDifference1?.Dispose();
                imageCleared?.Dispose();

                if (channels == 2)
                {
                    image1?.Dispose();
                    image2?.Dispose();
                }

                return true;
            }
            catch (Exception ex)
            {
                // 异常处理
                Console.WriteLine($"SurfaceDefectDetection Error: {ex.Message}");
                return false;
            }
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

    }
}
