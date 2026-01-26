using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows;

namespace ImageControl
{
    public class TransformMatrix
    {
        public Matrix3D m_viewMatrix = new Matrix3D();
        public Matrix3D m_projMatrix = new Matrix3D();
        public Matrix3D m_totalMatrix = new Matrix3D();
        public double m_scaleFactor = 1.3;
        private bool m_mouseDown;
        private Point m_movePoint;

        public void ResetView() => this.m_viewMatrix.SetIdentity();

        public void OnLBtnDown(Point pt)
        {
            this.m_mouseDown = true;
            this.m_movePoint = pt;
        }

        public void OnMouseMove(Point pt, Viewport3D viewPort)//控制3D模型旋转平移功能
        {
            if (!this.m_mouseDown)
                return;
            double actualWidth = viewPort.ActualWidth;
            double actualHeight = viewPort.ActualHeight;
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    this.m_viewMatrix.Translate(new Vector3D(2.0 * (pt.X - this.m_movePoint.X) / actualWidth, -2.0 * (pt.Y - this.m_movePoint.Y) / actualWidth, 0.0));
                    this.m_movePoint = pt;
                }
                else
                {
                    double angleInDegrees = 180.0 * (pt.X - this.m_movePoint.X) / actualWidth;
                    this.m_viewMatrix.Rotate(new Quaternion(new Vector3D(1.0, 0.0, 0.0), 180.0 * (pt.Y - this.m_movePoint.Y) / actualHeight));
                    this.m_viewMatrix.Rotate(new Quaternion(new Vector3D(0.0, 1.0, 0.0), angleInDegrees));
                    this.m_movePoint = pt;
                }
            }
            this.m_totalMatrix = Matrix3D.Multiply(this.m_projMatrix, this.m_viewMatrix);
        }
        public void OnMouseScale(Point pt,
            Chart3D chart3D,
            double Scale,Viewport3D viewPort)
        {
            double actualWidth = viewPort.ActualWidth;
            double actualHeight = viewPort.ActualHeight;
            double zMin = (double)chart3D.ZMin();
            double zMax = (double)chart3D.ZMax();

            float xMin = chart3D.XMin();
            float xMax = chart3D.XMax();
            float yMin = chart3D.YMin();
            float yMax = chart3D.YMax();
            //计算中心点
            double num1 = (xMin + xMax) / 2.0;
            double num2 = (yMin + yMax) / 2.0;
            double num3 = (zMin + zMax) / 2.0;
            //计算X轴Y轴Z轴一半长度
            double num4 = (xMax - xMin) / 2.0;
            double num5 = (yMax - yMin) / 2.0;
            double num6 = (zMax - zMin) / 2.0;

            int X = (int)(pt.X / actualWidth * chart3D.ImageWidth);
            int Y = (int)(pt.Y / actualWidth * chart3D.ImageHeight);
            this.m_projMatrix.SetIdentity();//设置为单位矩阵
            this.m_projMatrix.Translate(new Vector3D(-num1, -num2, -num3));
            var Xscale = 0.8 / num4 * Scale;
            var Yscale = 0.8 / num5 * Scale;
            var Zscale = 0.8 / num6 * Scale/10;
            //this.m_projMatrix.Scale(new Vector3D(Scale / num4, Scale / num5, Scale / num6));
            this.m_projMatrix.Scale(new Vector3D(Math.Min(Xscale, Yscale), Math.Min(Xscale, Yscale), Zscale));

            //m_viewMatrix.ScaleAt(new Vector3D(0.9, 0.9, 0.9), new Point3D(num1, num2, num3));
            //m_viewMatrix.ScaleAtPrepend(new Vector3D(0.9, 0.9, 0.9), new Point3D(X, Y, chart3D[X* Y].z));
            //this.m_viewMatrix.Translate(new Vector3D( (pt.X - this.m_movePoint.X+ num1) / actualWidth, -(pt.Y - this.m_movePoint.Y+ num2) / actualWidth, 0.0));
            //this.m_movePoint = pt;
            //this.m_viewMatrix.Scale(new Vector3D(1, 1, 1));
            this.m_totalMatrix = Matrix3D.Multiply(this.m_projMatrix, this.m_viewMatrix);
        }

        public void OnLBtnUp() => this.m_mouseDown = false;

        public void OnKeyDown(KeyEventArgs args)
        {
            switch (args.Key)
            {
                case Key.Home:
                    this.m_viewMatrix.SetIdentity();
                    break;
                case Key.OemPlus:
                    this.m_viewMatrix.Scale(new Vector3D(this.m_scaleFactor, this.m_scaleFactor, this.m_scaleFactor));
                    break;
                case Key.OemComma:
                    return;
                case Key.OemMinus:
                    this.m_viewMatrix.Scale(new Vector3D(1.0 / this.m_scaleFactor, 1.0 / this.m_scaleFactor, 1.0 / this.m_scaleFactor));
                    break;
                default:
                    return;
            }
            this.m_totalMatrix = Matrix3D.Multiply(this.m_projMatrix, this.m_viewMatrix);
        }

        public static Point3D Transform(Point3D pt1, Point3D center, double aX, double aZ)
        {
            double num1 = 3.1415925025939941 * aX / 180.0;
            double num2 = 3.1415925025939941 * aZ / 180.0;
            double num3 = pt1.X * Math.Cos(num2) + pt1.Z * Math.Sin(num2);
            double y = pt1.Y;
            double num4 = -pt1.X * Math.Sin(num2) + pt1.Z * Math.Cos(num2);
            return new Point3D(center.X + num3 * Math.Cos(num1) - y * Math.Sin(num1), center.Y + num3 * Math.Sin(num1) + y * Math.Cos(num1), center.Z + num4);
        }

        public static void Transform(Mesh3D model, Point3D center, double aX, double aZ)
        {
            double num1 = 3.1415925025939941 * aX / 180.0;
            double num2 = 3.1415925025939941 * aZ / 180.0;
            int vertexNo = model.GetVertexNo();
            for (int n = 0; n < vertexNo; ++n)
            {
                Point3D point = model.GetPoint(n);
                double num3 = point.X * Math.Cos(num2) + point.Z * Math.Sin(num2);
                double y1 = point.Y;
                double num4 = -point.X * Math.Sin(num2) + point.Z * Math.Cos(num2);
                double x = center.X + num3 * Math.Cos(num1) - y1 * Math.Sin(num1);
                double y2 = center.Y + num3 * Math.Sin(num1) + y1 * Math.Cos(num1);
                double z = center.Z + num4;
                model.SetPoint(n, x, y2, z);
            }
        }

        public void CalculateProjectionMatrix(Mesh3D mesh, double scaleFactor)
        {
            this.CalculateProjectionMatrix(mesh.m_xMin, mesh.m_xMax, mesh.m_yMin, mesh.m_yMax, mesh.m_zMin, mesh.m_zMax, scaleFactor);
        }

        public void CalculateProjectionMatrix(double min, double max, double scaleFactor)
        {
            this.CalculateProjectionMatrix(min, max, min, max, min, max, scaleFactor);
        }

        public void CalculateProjectionMatrix(
          double xMin,
          double xMax,
          double yMin,
          double yMax,
          double zMin,
          double zMax,
          double scaleFactor)
        {
            //计算中心点
            double num1 = (xMin + xMax) / 2.0;
            double num2 = (yMin + yMax) / 2.0;
            double num3 = (zMin + zMax) / 2.0;
            //计算X轴Y轴Z轴一半长度
            double num4 = (xMax - xMin) / 2.0;
            double num5 = (yMax - yMin) / 2.0;
            double num6 = (zMax - zMin) / 2.0;
            this.m_projMatrix.SetIdentity();//设置为单位矩阵
            this.m_projMatrix.Translate(new Vector3D(-num1, -num2, -num3));
            if (num4 < 1E-10)
                return;
            double mult = num4 / num5;
            var Xscale = scaleFactor / num4;
            var Yscale = scaleFactor / num5;
            var Zscale = scaleFactor / num6/10;
            //Math.Min(Xscale, Yscale);
            this.m_projMatrix.Scale(new Vector3D(Math.Min(Xscale, Yscale), Math.Min(Xscale, Yscale), Zscale));
            //this.m_projMatrix.Scale(new Vector3D(Math.Max(Xscale, Yscale), Math.Max(Xscale, Yscale), Zscale));
            this.m_totalMatrix = Matrix3D.Multiply(this.m_projMatrix, this.m_viewMatrix);
        }

        public Point VertexToScreenPt(Point3D point, Viewport3D viewPort)
        {
            Point3D point3D = this.m_totalMatrix.Transform(point);
            double actualWidth = viewPort.ActualWidth;
            double actualHeight = viewPort.ActualHeight;
            return new Point(actualWidth / 2.0 + point3D.X * actualWidth / 2.0, actualHeight / 2.0 - point3D.Y * actualWidth / 2.0);
        }

        public static Point ScreenPtToViewportPt(Point point, Viewport3D viewPort)
        {
            double actualWidth = viewPort.ActualWidth;
            double actualHeight = viewPort.ActualHeight;
            double x = point.X;
            double y = point.Y;
            return new Point((x - actualWidth / 2.0) * 2.0 / actualWidth, (actualHeight / 2.0 - y) * 2.0 / actualWidth);
        }

        public Point VertexToViewportPt(Point3D point, Viewport3D viewPort)
        {
            Point3D point3D = this.m_totalMatrix.Transform(point);
            return new Point(point3D.X, point3D.Y);
        }
    }
}
