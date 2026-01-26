using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace ImageControl
{
    public class Chart3D
    {
        public static int SHAPE_NO = 5;
        public int ImageWidth;
        public int ImageHeight;
        protected Vertex3D[] m_vertices;
        protected float m_xMin;
        protected float m_xMax;
        protected float m_yMin;
        protected float m_yMax;
        protected float m_zMin;
        protected float m_zMax;
        private float m_axisLengthWidthRatio = 200f;
        private float m_xAxisLength;
        private float m_yAxisLength;
        private float m_zAxisLength;
        private float m_xAxisCenter;
        private float m_yAxisCenter;
        private float m_zAxisCenter;
        private bool m_bUseAxes;
        public Color m_axisColor = Color.FromRgb((byte)0, (byte)0, (byte)196);

        public Vertex3D this[int n]
        {
            get => this.m_vertices[n];
            set => this.m_vertices[n] = value;
        }

        public float XCenter() => (float)(((double)this.m_xMin + (double)this.m_xMax) / 2.0);

        public float YCenter() => (float)(((double)this.m_yMin + (double)this.m_yMax) / 2.0);

        public float XRange() => this.m_xMax - this.m_xMin;

        public float YRange() => this.m_yMax - this.m_yMin;

        public float ZRange() => this.m_zMax - this.m_zMin;

        public float XMin() => this.m_xMin;

        public float XMax() => this.m_xMax;

        public float YMin() => this.m_yMin;

        public float YMax() => this.m_yMax;

        public float ZMin() => this.m_zMin;

        public float ZMax() => this.m_zMax;

        public int GetDataNo() => this.m_vertices.Length;

        public void SetDataNo(int nSize) => this.m_vertices = new Vertex3D[nSize];

        public void GetDataRange()
        {
            int dataNo = this.GetDataNo();
            if (dataNo == 0)
                return;
            this.m_xMin = float.MaxValue;
            this.m_yMin = float.MaxValue;
            this.m_zMin = float.MaxValue;
            this.m_xMax = float.MinValue;
            this.m_yMax = float.MinValue;
            this.m_zMax = float.MinValue;
            for (int n = 0; n < dataNo; ++n)
            {
                float x = this[n].x;
                float y = this[n].y;
                float z = this[n].z;
                if ((double)this.m_xMin > (double)x)
                    this.m_xMin = x;
                if ((double)this.m_yMin > (double)y)
                    this.m_yMin = y;
                if ((double)this.m_zMin > (double)z && z != 0)
                    this.m_zMin = z;
                if ((double)this.m_xMax < (double)x)
                    this.m_xMax = x;
                if ((double)this.m_yMax < (double)y)
                    this.m_yMax = y;
                if ((double)this.m_zMax < (double)z && z != 0)
                    this.m_zMax = z;
            }
        }

        public void SetAxes(float x0, float y0, float z0, float xL, float yL, float zL)
        {
            this.m_xAxisLength = xL;
            this.m_yAxisLength = yL;
            this.m_zAxisLength = zL;
            this.m_xAxisCenter = x0;
            this.m_yAxisCenter = y0;
            this.m_zAxisCenter = z0;
            this.m_bUseAxes = true;
        }

        public void SetAxes() => this.SetAxes(0.05f);

        public void SetAxes(float margin)
        {
            float num1 = this.m_xMax - this.m_xMin;
            float num2 = this.m_yMax - this.m_yMin;
            float num3 = this.m_zMax - this.m_zMin;
            this.SetAxes(this.m_xMin - margin * num1, this.m_yMin - margin * num2, this.m_zMin - margin * num3, (float)(1.0 + 2.0 * (double)margin) * num1, (float)(1.0 + 2.0 * (double)margin) * num2, (float)(1.0 + 2.0 * (double)margin) * num3);
        }

        public void AddAxesMeshes(ArrayList meshs)
        {
            if (!this.m_bUseAxes)
                return;
            float num = (float)(((double)this.m_xAxisLength + (double)this.m_yAxisLength + (double)this.m_zAxisLength) / (3.0 * (double)this.m_axisLengthWidthRatio));
            Mesh3D model1 = (Mesh3D)new Cylinder3D((double)num, (double)num, (double)this.m_xAxisLength, 6);
            model1.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model1, new Point3D((double)this.m_xAxisCenter + (double)this.m_xAxisLength / 2.0, (double)this.m_yAxisCenter, (double)this.m_zAxisCenter), 0.0, 90.0);
            meshs.Add((object)model1);
            Mesh3D model2 = (Mesh3D)new Cone3D(2.0 * (double)num, 2.0 * (double)num, (double)num * 5.0, 6);
            model2.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model2, new Point3D((double)this.m_xAxisCenter + (double)this.m_xAxisLength, (double)this.m_yAxisCenter, (double)this.m_zAxisCenter), 0.0, 90.0);
            meshs.Add((object)model2);
            Mesh3D model3 = (Mesh3D)new Cylinder3D((double)num, (double)num, (double)this.m_yAxisLength, 6);
            model3.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model3, new Point3D((double)this.m_xAxisCenter, (double)this.m_yAxisCenter + (double)this.m_yAxisLength / 2.0, (double)this.m_zAxisCenter), 90.0, 90.0);
            meshs.Add((object)model3);
            Mesh3D model4 = (Mesh3D)new Cone3D(2.0 * (double)num, 2.0 * (double)num, (double)num * 5.0, 6);
            model4.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model4, new Point3D((double)this.m_xAxisCenter, (double)this.m_yAxisCenter + (double)this.m_yAxisLength, (double)this.m_zAxisCenter), 90.0, 90.0);
            meshs.Add((object)model4);
            Mesh3D model5 = (Mesh3D)new Cylinder3D((double)num, (double)num, (double)this.m_zAxisLength, 6);
            model5.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model5, new Point3D((double)this.m_xAxisCenter, (double)this.m_yAxisCenter, (double)this.m_zAxisCenter + (double)this.m_zAxisLength / 2.0), 0.0, 0.0);
            meshs.Add((object)model5);
            Mesh3D model6 = (Mesh3D)new Cone3D(2.0 * (double)num, 2.0 * (double)num, (double)num * 5.0, 6);
            model6.SetColor(this.m_axisColor);
            TransformMatrix.Transform(model6, new Point3D((double)this.m_xAxisCenter, (double)this.m_yAxisCenter, (double)this.m_zAxisCenter + (double)this.m_zAxisLength), 0.0, 0.0);
            meshs.Add((object)model6);
        }

        public virtual void Select(ViewportRect rect, TransformMatrix matrix, Viewport3D viewport3d)
        {
        }

        public virtual void HighlightSelection(MeshGeometry3D meshGeometry, Color selectColor)
        {
        }

        public enum SHAPE
        {
            BAR,
            ELLIPSE,
            CYLINDER,
            CONE,
            PYRAMID,
        }
    }
}
