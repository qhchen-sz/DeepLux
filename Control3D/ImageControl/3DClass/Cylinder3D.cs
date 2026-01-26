using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageControl
{
    public class Cylinder3D : Mesh3D
    {
        private int m_nRes;

        public Cylinder3D(double a, double b, double h, int nRes)
        {
            this.SetMesh(nRes);
            this.SetData(a, b, h);
        }

        private void SetMesh(int nRes)
        {
            this.SetSize(2 * nRes + 2, 4 * nRes);
            for (int index = 0; index < nRes; ++index)
            {
                int num1 = index;
                int num2 = index != nRes - 1 ? index + 1 : 0;
                this.SetTriangle(index * 4, num1, num2, nRes + num1);
                this.SetTriangle(index * 4 + 1, nRes + num1, num2, nRes + num2);
                this.SetTriangle(index * 4 + 2, num2, num1, 2 * nRes);
                this.SetTriangle(index * 4 + 3, nRes + num1, nRes + num2, 2 * nRes + 1);
            }
            this.m_nRes = nRes;
        }

        private void SetData(double a, double b, double h)
        {
            double num1 = 6.2831850051879883 / (double)this.m_nRes;
            for (int n = 0; n < this.m_nRes; ++n)
            {
                double num2 = (double)n * num1;
                this.SetPoint(n, a * Math.Cos(num2), b * Math.Sin(num2), -h / 2.0);
            }
            for (int index = 0; index < this.m_nRes; ++index)
            {
                double num3 = (double)index * num1;
                this.SetPoint(this.m_nRes + index, a * Math.Cos(num3), b * Math.Sin(num3), h / 2.0);
            }
            this.SetPoint(2 * this.m_nRes, 0.0, 0.0, -h / 2.0);
            this.SetPoint(2 * this.m_nRes + 1, 0.0, 0.0, h / 2.0);
            this.m_xMin = -a;
            this.m_xMax = a;
            this.m_yMin = -b;
            this.m_yMax = b;
            this.m_zMin = -h / 2.0;
            this.m_zMax = h / 2.0;
        }
    }
}
