using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace ImageControl
{
    internal class UniformSurfaceChart3D : SurfaceChart3D
    {
        private int m_nGridXNo;
        private int m_nGridYNo;

        public void SetPoint(int i, int j, float x, float y, float z)
        {
            int index = j * this.m_nGridXNo + i;
            this.m_vertices[index].x = x;
            this.m_vertices[index].y = y;
            this.m_vertices[index].z = z;
        }

        public void SetZ(int i, int j, float z) => this.m_vertices[j * this.m_nGridXNo + i].z = z;

        public void SetColor(int i, int j, Color color)
        {
            this.m_vertices[j * this.m_nGridXNo + i].color = color;
        }

        public void SetGrid(int xNo, int yNo, float xMin, float xMax, float yMin, float yMax)
        {
            this.SetDataNo(xNo * yNo);
            this.m_nGridXNo = xNo;
            this.m_nGridYNo = yNo;
            this.m_xMin = xMin;
            this.m_xMax = xMax;
            this.m_yMin = yMin;
            this.m_yMax = yMax;
            float num1 = (float)(((double)this.m_xMax - (double)this.m_xMin) / ((double)xNo - 1.0));
            float num2 = (float)(((double)this.m_yMax - (double)this.m_yMin) / ((double)yNo - 1.0));
            for (int i = 0; i < xNo; ++i)
            {
                for (int j = 0; j < yNo; ++j)
                {
                    float x = this.m_xMin + num1 * (float)i;
                    float y = this.m_yMin + num2 * (float)j;
                    this.m_vertices[j * xNo + i] = new Vertex3D();
                    this.SetPoint(i, j, i, j, 0.0f);
                }
            }
        }

        public ArrayList GetMeshes()
        {
            ArrayList meshes = new ArrayList();
            ColorMesh3D colorMesh3D = new ColorMesh3D();
            colorMesh3D.SetSize(this.m_nGridXNo * this.m_nGridYNo, 2 * (this.m_nGridXNo - 1) * (this.m_nGridYNo - 1));
            for (int index1 = 0; index1 < this.m_nGridXNo; ++index1)
            {
                for (int index2 = 0; index2 < this.m_nGridYNo; ++index2)
                {
                    int index3 = index2 * this.m_nGridXNo + index1;
                    Vertex3D vertex = this.m_vertices[index3];
                    if (vertex.z == 0)
                    {
                        continue;
                    }

                    this.m_vertices[index3].nMinI = index3;
                    colorMesh3D.SetPoint(index3, new Point3D((double)vertex.x, (double)vertex.y, (double)vertex.z));
                    colorMesh3D.SetColor(index3, vertex.color);
                }
            }
            int i1 = 0;
            for (int index4 = 0; index4 < this.m_nGridXNo - 1; ++index4)
            {
                for (int index5 = 0; index5 < this.m_nGridYNo - 1; ++index5)
                {


                    int m0 = index5 * this.m_nGridXNo + index4;//表示指向3D数据的索引
                    int m1 = index5 * this.m_nGridXNo + index4 + 1;//表示向右相邻数据的索引
                    int num = (index5 + 1) * this.m_nGridXNo + index4;//表示向下相邻数据的索引
                    int m2 = (index5 + 1) * this.m_nGridXNo + index4 + 1;

                    if (this.m_vertices[m0].z == 0 || this.m_vertices[m1].z == 0 || this.m_vertices[num].z == 0 || this.m_vertices[m2].z == 0)
                    {
                        continue;


                    }
                    colorMesh3D.SetTriangle(i1, m0, m1, num);
                    int i2 = i1 + 1;
                    colorMesh3D.SetTriangle(i2, num, m1, m2);
                    i1 = i2 + 1;
                }
            }
            meshes.Add((object)colorMesh3D);
            return meshes;
        }
    }
}
