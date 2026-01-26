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
    public class Model3D : ModelVisual3D
    {
        private TextureMapping m_mapping = new TextureMapping();

        public void SetRGBColor() => this.m_mapping.SetRGBMaping();

        public void SetPsedoColor() => this.m_mapping.SetPseudoMaping();

        private void SetModel(ArrayList meshs, Material backMaterial)
        {
            int count = meshs.Count;
            if (count == 0)
                return;
            MeshGeometry3D geometry = new MeshGeometry3D();
            int num1 = 0;
            for (int index1 = 0; index1 < count; ++index1)
            {
                Mesh3D mesh3D = (Mesh3D)meshs[index1];
                int vertexNo = mesh3D.GetVertexNo();
                int triangleNo = mesh3D.GetTriangleNo();
                if (vertexNo > 0 && triangleNo > 0)
                {
                    double[] numArray1 = new double[vertexNo];
                    double[] numArray2 = new double[vertexNo];
                    double[] numArray3 = new double[vertexNo];
                    for (int index2 = 0; index2 < vertexNo; ++index2)
                        numArray1[index2] = numArray2[index2] = numArray3[index2] = 0.0;
                    for (int n = 0; n < triangleNo; ++n)
                    {

                        Triangle3D triangle = mesh3D.GetTriangle(n);
                        Vector3D triangleNormal = mesh3D.GetTriangleNormal(n);
                        if (triangle == null || triangleNormal == null)
                            continue;
                        int n0 = triangle.n0;
                        int n1 = triangle.n1;
                        int n2 = triangle.n2;
                        numArray1[n0] += triangleNormal.X;
                        numArray2[n0] += triangleNormal.Y;
                        numArray3[n0] += triangleNormal.Z;
                        numArray1[n1] += triangleNormal.X;
                        numArray2[n1] += triangleNormal.Y;
                        numArray3[n1] += triangleNormal.Z;
                        numArray1[n2] += triangleNormal.X;
                        numArray2[n2] += triangleNormal.Y;
                        numArray3[n2] += triangleNormal.Z;
                    }
                    for (int index3 = 0; index3 < vertexNo; ++index3)
                    {
                        double num2 = Math.Sqrt(numArray1[index3] * numArray1[index3] + numArray2[index3] * numArray2[index3] + numArray3[index3] * numArray3[index3]);
                        if (num2 > 1E-20)
                        {
                            numArray1[index3] /= num2;
                            numArray2[index3] /= num2;
                            numArray3[index3] /= num2;
                        }
                        geometry.Positions.Add(mesh3D.GetPoint(index3));
                        Point mappingPosition = this.m_mapping.GetMappingPosition(mesh3D.GetColor(index3));
                        geometry.TextureCoordinates.Add(new Point(mappingPosition.X, mappingPosition.Y));
                        geometry.Normals.Add(new Vector3D(numArray1[index3], numArray2[index3], numArray3[index3]));
                    }
                    for (int n = 0; n < triangleNo; ++n)
                    {
                        Triangle3D triangle = mesh3D.GetTriangle(n);
                        if (triangle == null)
                            continue;
                        int n0 = triangle.n0;
                        int n1 = triangle.n1;
                        int n2 = triangle.n2;
                        geometry.TriangleIndices.Add(num1 + n0);
                        geometry.TriangleIndices.Add(num1 + n1);
                        geometry.TriangleIndices.Add(num1 + n2);
                    }
                    num1 += vertexNo;
                }
            }
            Material material = (Material)this.m_mapping.m_material;
            GeometryModel3D geometryModel3D = new GeometryModel3D((Geometry3D)geometry, material);
            geometryModel3D.Transform = (Transform3D)new Transform3DGroup();
            if (backMaterial != null)
                geometryModel3D.BackMaterial = backMaterial;
            this.Content = (System.Windows.Media.Media3D.Model3D)geometryModel3D;
        }

        public static MeshGeometry3D GetGeometry(Viewport3D viewport3d, int nModelIndex)
        {
            if (nModelIndex == -1)
                return (MeshGeometry3D)null;
            ModelVisual3D child = (ModelVisual3D)viewport3d.Children[nModelIndex];
            return child.Content == null ? (MeshGeometry3D)null : (MeshGeometry3D)((GeometryModel3D)child.Content).Geometry;
        }

        public int UpdateModel(
          ArrayList meshs,
          Material backMaterial,
          int nModelIndex,
          Viewport3D viewport3d)
        {
            if (nModelIndex >= 0)
            {
                ModelVisual3D child = (ModelVisual3D)viewport3d.Children[nModelIndex];
                viewport3d.Children.Remove((Visual3D)child);
            }
            if (backMaterial == null)
                this.SetRGBColor();
            else
                this.SetPsedoColor();
            this.SetModel(meshs, backMaterial);
            int count = viewport3d.Children.Count;
            viewport3d.Children.Add((Visual3D)this);
            return count;
        }
    }
}
