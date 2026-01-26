using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace ImageControl
{
    public class ColorMesh3D : Mesh3D
    {
        private Color[] m_colors;

        public override void SetVertexNo(int nSize)
        {
            this.m_points = new Point3D[nSize];
            this.m_colors = new Color[nSize];
        }

        public override Color GetColor(int nV) => this.m_colors[nV];

        public void SetColor(int nV, byte r, byte g, byte b)
        {
            this.m_colors[nV] = Color.FromRgb(r, g, b);
        }

        public void SetColor(int nV, Color color) => this.m_colors[nV] = color;
    }
}
