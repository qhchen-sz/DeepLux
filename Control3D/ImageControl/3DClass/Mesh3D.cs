using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace ImageControl
{
    public class Mesh3D
    {
        protected Point3D[] m_points;
        protected int[] m_vertIndices;
        protected Color m_color;
        protected Triangle3D[] m_tris;
        public double m_xMin;
        public double m_xMax;
        public double m_yMin;
        public double m_yMax;
        public double m_zMin;
        public double m_zMax;

        public int GetVertexNo() => this.m_points == null ? 0 : this.m_points.Length;

        public virtual void SetVertexNo(int nSize)
        {
            this.m_points = new Point3D[nSize];
            this.m_vertIndices = new int[nSize];
        }

        public int GetTriangleNo() => this.m_tris == null ? 0 : this.m_tris.Length;

        public void SetTriangleNo(int nSize) => this.m_tris = new Triangle3D[nSize];

        public virtual void SetSize(int nVertexNo, int nTriangleNo)
        {
            this.SetVertexNo(nVertexNo);
            this.SetTriangleNo(nTriangleNo);
        }

        public Point3D GetPoint(int n) => this.m_points[n];

        public void SetPoint(int n, Point3D pt) => this.m_points[n] = pt;

        public void SetPoint(int n, double x, double y, double z)
        {
            this.m_points[n] = new Point3D(x, y, z);
        }

        public Triangle3D GetTriangle(int n) => this.m_tris[n];

        public void SetTriangle(int n, Triangle3D triangle) => this.m_tris[n] = triangle;

        public void SetTriangle(int i, int m0, int m1, int m2)
        {
            this.m_tris[i] = new Triangle3D(m0, m1, m2);
        }

        public Vector3D GetTriangleNormal(int n)
        {

            Triangle3D triangle = this.GetTriangle(n);
            if (triangle == null)
                return new Vector3D();
            Point3D point1 = this.GetPoint(triangle.n0);
            Point3D point2 = this.GetPoint(triangle.n1);
            Point3D point3 = this.GetPoint(triangle.n2);
            double num1 = point2.X - point1.X;
            double num2 = point2.Y - point1.Y;
            double num3 = point2.Z - point1.Z;
            double num4 = point3.X - point1.X;
            double num5 = point3.Y - point1.Y;
            double num6 = point3.Z - point1.Z;
            double num7 = num2 * num6 - num3 * num5;
            double num8 = num3 * num4 - num1 * num6;
            double num9 = num1 * num5 - num2 * num4;
            double num10 = Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
            return new Vector3D(num7 / num10, num8 / num10, num9 / num10);
        }

        public virtual Color GetColor(int nV) => this.m_color;

        public void SetColor(byte r, byte g, byte b) => this.m_color = Color.FromRgb(r, g, b);

        public void SetColor(Color color) => this.m_color = color;

        public void UpdatePositions(MeshGeometry3D meshGeometry)
        {
            int vertexNo = this.GetVertexNo();
            for (int index = 0; index < vertexNo; ++index)
                meshGeometry.Positions[index] = this.m_points[index];
        }

        public virtual void SetTestModel()
        {
            double num = 10.0;
            this.SetSize(3, 1);
            this.SetPoint(0, -0.5, 0.0, 0.0);
            this.SetPoint(1, 0.5, 0.5, 0.3);
            this.SetPoint(2, 0.0, 0.5, 0.0);
            this.SetTriangle(0, 0, 2, 1);
            this.m_xMin = 0.0;
            this.m_xMax = 2.0 * num;
            this.m_yMin = 0.0;
            this.m_yMax = num;
            this.m_zMin = -num;
            this.m_zMax = num;
        }
    }
}
