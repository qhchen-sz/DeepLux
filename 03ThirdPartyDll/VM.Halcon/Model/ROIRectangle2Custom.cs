using System;
using HalconDotNet;
using System.Xml.Serialization;

namespace VM.Halcon.Model
{
    /// <summary>
    /// 自定义旋转矩形ROI，支持4个专用控制点：
    /// Handle 0: 长边左中点 - 控制角度
    /// Handle 1: 长边右中点 - 控制宽度
    /// Handle 2: 短边上中点 - 控制长度
    /// Handle 3: 中心点     - 控制位置
    /// 中心处有十字线，分别指示宽度和长度方向
    /// </summary>
    [Serializable]
    public class ROIRectangle2Custom : ROI
    {
        public double _Length1 = 10;
        /// <summary>半宽度（垂直于Phi方向）</summary>
        public double Length1
        {
            get { return _Length1; }
            set { Set(ref _Length1, value); }
        }

        public double _Length2 = 10;
        /// <summary>半长度（Phi方向）</summary>
        public double Length2
        {
            get { return _Length2; }
            set { Set(ref _Length2, value); }
        }

        public double _MidR;
        /// <summary>中心点Row坐标</summary>
        public double MidR
        {
            get { return _MidR; }
            set { Set(ref _MidR, value); }
        }

        public double _MidC;
        /// <summary>中心点Col坐标</summary>
        public double MidC
        {
            get { return _MidC; }
            set { Set(ref _MidC, value); }
        }

        public double _Phi;
        /// <summary>旋转角度（弧度）</summary>
        public double Phi
        {
            get { return _Phi; }
            set { Set(ref _Phi, value); }
        }

        /// <summary>旋转角度（度）</summary>
        public double Deg
        {
            get { return ((HTuple)_Phi).TupleDeg(); }
            set { Phi = ((HTuple)value).TupleRad(); }
        }

        // 辅助变量
        [NonSerialized]
        HTuple rowsInit;
        [NonSerialized]
        HTuple colsInit;
        [NonSerialized]
        HTuple rows = 100;
        [NonSerialized]
        HTuple cols = 100;
        [NonSerialized]
        HHomMat2D hom2D;
        [NonSerialized]
        HHomMat2D tmp;

        /// <summary>构造函数</summary>
        public ROIRectangle2Custom()
        {
            Type = ROIType.Rectangle2;
            NumHandles = 4; // 左中点(角度) + 右中点(宽度) + 上中点(长度) + 中心点(位置)
            ActiveHandleId = 3;
        }

        public ROIRectangle2Custom(double row, double col, double phi, double length1, double length2)
        {
            Type = ROIType.Rectangle2;
            CreateRectangle2(row, col, phi, length1, length2);
        }

        public override void CreateRectangle2(double row, double col, double phi, double length1, double length2)
        {
            base.CreateRectangle2(row, col, phi, length1, length2);
            this.MidR = row;
            this.MidC = col;
            this.Length1 = length1;
            this.Length2 = length2;
            this.Phi = phi;

            // 归一化坐标定义：
            // Handle 0: 长边左中点 (-Length1方向) -> (0, -1)
            // Handle 1: 长边右中点 (+Length1方向) -> (0, 1)
            // Handle 2: 短边上中点 (-Length2方向) -> (-1, 0)
            // Handle 3: 中心点 -> (0, 0)
            rowsInit = new HTuple(new double[] { 0.0, 0.0, -1.0, 0.0 });
            colsInit = new HTuple(new double[] { -1.0, 1.0, 0.0, 0.0 });

            hom2D = new HHomMat2D();
            tmp = new HHomMat2D();

            updateHandlePos();
        }

        /// <summary>在鼠标位置创建新的ROI实例</summary>
        public override void CreateROI(double midX, double midY)
        {
            MidR = midY;
            MidC = midX;
            Length1 = 100;
            Length2 = 50;
            Phi = 0.0;

            rowsInit = new HTuple(new double[] { 0.0, 0.0, -1.0, 0.0 });
            colsInit = new HTuple(new double[] { -1.0, 1.0, 0.0, 0.0 });

            hom2D = new HHomMat2D();
            tmp = new HHomMat2D();
            updateHandlePos();
        }

        /// <summary>绘制ROI到窗口</summary>
        public override void Draw(HalconDotNet.HWindow window)
        {
            // 绘制矩形轮廓
            window.SetDraw("margin");
            window.DispRectangle2(MidR, MidC, -Phi, Length1, Length2);
            window.SetDraw("fill");

            // 绘制4个控制点（方形）
            for (int i = 0; i < NumHandles; i++)
                window.DispRectangle2(rows[i].D, cols[i].D, -Phi, 4, 4);

            // 绘制方向箭头（仅在控制点附近，颜色与边框一致）
            // Handle 1 在 Length1轴正方向：(sin(Phi), cos(Phi))，控制宽度
            // Handle 2 在 Length2轴正方向：(-cos(Phi), sin(Phi))，控制长度
            window.SetColor("cyan");

            double arrowRatio = 0.6; // 箭头长度占半轴的比例
            double arrowSize = 3;    // 箭头尖端大小（像素）

            // 宽度方向箭头：从中心指向 Handle 1 方向
            double wRow = MidR + Math.Sin(Phi) * Length1 * arrowRatio;
            double wCol = MidC + Math.Cos(Phi) * Length1 * arrowRatio;
            window.DispArrow(MidR, MidC, wRow, wCol, arrowSize);

            // 长度方向箭头：从中心指向 Handle 2 方向
            double lRow = MidR - Math.Cos(Phi) * Length2 * arrowRatio;
            double lCol = MidC + Math.Sin(Phi) * Length2 * arrowRatio;
            window.DispArrow(MidR, MidC, lRow, lCol, arrowSize);
        }

        /// <summary>返回ROI句柄到图像点(x,y)的最近距离</summary>
        public override double DistToClosestHandle(double x, double y)
        {
            double max = 10000;
            double[] val = new double[NumHandles];

            for (int i = 0; i < NumHandles; i++)
                val[i] = HMisc.DistancePp(y, x, rows[i].D, cols[i].D);

            for (int i = 0; i < NumHandles; i++)
            {
                if (val[i] < max)
                {
                    max = val[i];
                    ActiveHandleId = i;
                }
            }
            return val[ActiveHandleId];
        }

        /// <summary>绘制活动句柄</summary>
        public override void DisplayActive(HalconDotNet.HWindow window)
        {
            window.DispRectangle2(rows[ActiveHandleId].D,
                                  cols[ActiveHandleId].D,
                                  -Phi, 4, 4);
        }

        /// <summary>获取ROI描述的HALCON区域</summary>
        public override HRegion GetRegion()
        {
            HRegion region = new HRegion();
            region.GenRectangle2(MidR, MidC, -Phi, Length1, Length2);
            return region;
        }

        public override HXLDCont GetXLD()
        {
            HXLDCont xld = new HXLDCont();
            xld.GenRectangle2ContourXld(MidR, MidC, Phi, Length1, Length2);
            return xld;
        }

        /// <summary>获取ROI的模型数据</summary>
        public override HTuple GetModelData()
        {
            return new HTuple(new double[] { MidR, MidC, Phi, Length1, Length2 });
        }

        /// <summary>根据鼠标位置重新计算ROI形状</summary>
        public override void moveByHandle(double newX, double newY)
        {
            switch (ActiveHandleId)
            {
                case 0: // 长边左中点 - 控制角度
                    {
                        // Handle 0 位于 (-sin(Phi), -cos(Phi)) 方向
                        // 需要 Phi 使 Handle 跟随鼠标：cos(Phi)=(MidC-newX)/dist, sin(Phi)=(MidR-newY)/dist
                        double vRow = newY - MidR;
                        double vCol = newX - MidC;
                        double dist = Math.Sqrt(vRow * vRow + vCol * vCol);
                        if (dist > 1)
                        {
                            Phi = Math.Atan2(-vRow, -vCol);
                        }
                    }
                    break;

                case 1: // 长边右中点 - 控制宽度 (Length1)
                    {
                        // Length1轴方向：(sin(Phi), cos(Phi)) in (Row, Col)
                        double vRow = newY - MidR;
                        double vCol = newX - MidC;
                        // 投影到Length1轴方向
                        double proj = vRow * Math.Sin(Phi) + vCol * Math.Cos(Phi);
                        Length1 = Math.Max(Math.Abs(proj), 0.01);
                    }
                    break;

                case 2: // 短边上中点 - 控制长度 (Length2)
                    {
                        // Length2轴方向：(-cos(Phi), sin(Phi)) in (Row, Col)
                        double vRow = newY - MidR;
                        double vCol = newX - MidC;
                        // 投影到Length2轴方向
                        double proj = vRow * Math.Cos(Phi) - vCol * Math.Sin(Phi);
                        Length2 = Math.Max(Math.Abs(proj), 0.01);
                    }
                    break;

                case 3: // 中心点 - 控制位置
                    MidC = newX;
                    MidR = newY;
                    break;
            }
            updateHandlePos();
        }

        /// <summary>更新控制点位置</summary>
        private void updateHandlePos()
        {
            try
            {
                hom2D.HomMat2dIdentity();
                hom2D = hom2D.HomMat2dTranslate(MidC, MidR);
                hom2D = hom2D.HomMat2dRotateLocal(Phi);
                tmp = hom2D.HomMat2dScaleLocal(Length1, Length2);
                cols = tmp.AffineTransPoint2d(colsInit, rowsInit, out rows);
            }
            catch (Exception)
            {
            }
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
