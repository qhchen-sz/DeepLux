using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;

namespace ImageControl
{
    internal class SurfaceChart3D : Chart3D
    {
        public override void Select(ViewportRect rect, TransformMatrix matrix, Viewport3D viewport3d)
        {
            int dataNo = this.GetDataNo();
            if (dataNo == 0)
                return;
            double num1 = rect.XMin();
            double num2 = rect.XMax();
            double num3 = rect.YMin();
            double num4 = rect.YMax();
            for (int index = 0; index < dataNo; ++index)
            {
                Point viewportPt = matrix.VertexToViewportPt(new Point3D((double)this.m_vertices[index].x, (double)this.m_vertices[index].y, (double)this.m_vertices[index].z), viewport3d);
                if (viewportPt.X > num1 && viewportPt.X < num2 && viewportPt.Y > num3 && viewportPt.Y < num4)
                    this.m_vertices[index].selected = true;
                else
                    this.m_vertices[index].selected = false;
            }
        }

        public override void HighlightSelection(MeshGeometry3D meshGeometry, Color selectColor)
        {
            int dataNo = this.GetDataNo();
            if (dataNo == 0)
                return;
            for (int index = 0; index < dataNo; ++index)
            {
                Point point = !this.m_vertices[index].selected ? TextureMapping.GetMappingPosition(this.m_vertices[index].color, true) : TextureMapping.GetMappingPosition(selectColor, true);
                int nMinI = this.m_vertices[index].nMinI;
                meshGeometry.TextureCoordinates[nMinI] = point;
            }
        }
    }
}
