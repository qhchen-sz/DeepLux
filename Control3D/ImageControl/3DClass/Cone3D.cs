using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageControl
{
    public class Cone3D : Mesh3D
    {
        private int m_nRes;

        public Cone3D(double a, double b, double h, int nRes)
        {
            this.SetMesh(nRes);
            this.SetData(a, b, h);
        }

        private void SetMesh(int nRes)
        {
            this.SetSize(nRes + 2, 2 * nRes);
            for (int index = 0; index < nRes - 1; ++index)
            {
                this.SetTriangle(index, index, index + 1, nRes + 1);
                this.SetTriangle(nRes + index, index + 1, index, nRes);
            }
            this.SetTriangle(nRes - 1, nRes - 1, 0, nRes + 1);
            this.SetTriangle(2 * nRes - 1, 0, nRes - 1, nRes);
            this.m_nRes = nRes;
        }

        private void SetData(double a, double b, double h)
        {
            double num1 = 6.2831850051879883 / (double)this.m_nRes;
            for (int n = 0; n < this.m_nRes; ++n)
            {
                double num2 = (double)n * num1;
                this.SetPoint(n, a * Math.Cos(num2), b * Math.Sin(num2), 0.0);
            }
            this.SetPoint(this.m_nRes, 0.0, 0.0, 0.0);
            this.SetPoint(this.m_nRes + 1, 0.0, 0.0, h);
            this.m_xMin = -a;
            this.m_xMax = a;
            this.m_yMin = -b;
            this.m_yMax = b;
            this.m_zMin = 0.0;
            this.m_zMax = h;
        }
    }
}
