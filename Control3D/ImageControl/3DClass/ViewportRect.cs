using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows;

namespace ImageControl
{
    public class ViewportRect : Mesh3D
    {
        private double m_x1;
        private double m_y1;
        private double m_x2;
        private double m_y2;
        public double m_lineWidth = 0.005;
        public double m_zLevel = 1.0;

        public ViewportRect()
        {
            this.SetSize(8, 8);
            this.SetTriangle(0, 0, 4, 1);
            this.SetTriangle(1, 1, 4, 5);
            this.SetTriangle(2, 1, 5, 2);
            this.SetTriangle(3, 2, 5, 6);
            this.SetTriangle(4, 2, 6, 3);
            this.SetTriangle(5, 3, 6, 7);
            this.SetTriangle(6, 0, 3, 7);
            this.SetTriangle(7, 0, 7, 4);
            this.SetColor(byte.MaxValue, (byte)0, (byte)0);
        }

        private void SetRect(double xC, double yC, double w, double h)
        {
            this.SetPoint(0, xC - w / 2.0, yC + h / 2.0, this.m_zLevel);
            this.SetPoint(1, xC + w / 2.0, yC + h / 2.0, this.m_zLevel);
            this.SetPoint(2, xC + w / 2.0, yC - h / 2.0, this.m_zLevel);
            this.SetPoint(3, xC - w / 2.0, yC - h / 2.0, this.m_zLevel);
            this.SetPoint(4, xC - w / 2.0 + this.m_lineWidth, yC + h / 2.0 - this.m_lineWidth, this.m_zLevel);
            this.SetPoint(5, xC + w / 2.0 - this.m_lineWidth, yC + h / 2.0 - this.m_lineWidth, this.m_zLevel);
            this.SetPoint(6, xC + w / 2.0 - this.m_lineWidth, yC - h / 2.0 + this.m_lineWidth, this.m_zLevel);
            this.SetPoint(7, xC - w / 2.0 + this.m_lineWidth, yC - h / 2.0 + this.m_lineWidth, this.m_zLevel);
        }

        private void SetRect()
        {
            this.SetRect((this.m_x1 + this.m_x2) / 2.0, (this.m_y1 + this.m_y2) / 2.0, Math.Abs(this.m_x2 - this.m_x1), Math.Abs(this.m_y2 - this.m_y1));
        }

        public void SetRect(Point pt1, Point pt2)
        {
            this.m_x1 = pt1.X;
            this.m_y1 = pt1.Y;
            this.m_x2 = pt2.X;
            this.m_y2 = pt2.Y;
            this.SetRect();
        }

        public ArrayList GetMeshes()
        {
            ArrayList meshes = new ArrayList();
            meshes.Add((object)this);
            int vertexNo = this.GetVertexNo();
            for (int index = 0; index < vertexNo; ++index)
                this.m_vertIndices[index] = index;
            return meshes;
        }

        public void OnMouseDown(Point pt, Viewport3D viewport3d, int nModelIndex)
        {
            if (nModelIndex == -1)
                return;
            MeshGeometry3D geometry = Model3D.GetGeometry(viewport3d, nModelIndex);
            if (geometry == null)
                return;
            Point viewportPt = TransformMatrix.ScreenPtToViewportPt(pt, viewport3d);
            this.SetRect(viewportPt, viewportPt);
            this.UpdatePositions(geometry);
        }

        public void OnMouseMove(Point pt, Viewport3D viewport3d, int nModelIndex)
        {
            if (nModelIndex == -1)
                return;
            MeshGeometry3D geometry = Model3D.GetGeometry(viewport3d, nModelIndex);
            if (geometry == null)
                return;
            Point viewportPt = TransformMatrix.ScreenPtToViewportPt(pt, viewport3d);
            this.m_x2 = viewportPt.X;
            this.m_y2 = viewportPt.Y;
            this.SetRect();
            this.UpdatePositions(geometry);
        }

        public double XMin() => this.m_x1 < this.m_x2 ? this.m_x1 : this.m_x2;

        public double XMax() => this.m_x1 < this.m_x2 ? this.m_x2 : this.m_x1;

        public double YMin() => this.m_y1 < this.m_y2 ? this.m_y1 : this.m_y2;

        public double YMax() => this.m_y1 < this.m_y2 ? this.m_y2 : this.m_y1;
    }
}
