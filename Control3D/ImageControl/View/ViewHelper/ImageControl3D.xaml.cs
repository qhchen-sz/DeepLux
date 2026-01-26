using LogModule;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static ImageControl.Chart3D;

namespace ImageControl
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class ImageControl3D : UserControl, IComponentConnector
    {
        public TransformMatrix m_transformMatrix = new TransformMatrix();
        public int m_nChartModelIndex = -1;
        private Chart3D m_3dChart;
        private ViewportRect m_selectRect = new ViewportRect();
        public int m_nRectModelIndex = -1;
        double scale= 1.0;
        public ImageControl3D()
        {
            InitializeComponent();
        }
        public void OnViewportMouseUp(object sender, MouseButtonEventArgs args)
        {
            args.GetPosition((IInputElement)this.mainViewport);
            if (args.ChangedButton == MouseButton.Left)
            {
                this.m_transformMatrix.OnLBtnUp();
            }
            else
            {
                if (args.ChangedButton != MouseButton.Right || this.m_nChartModelIndex == -1)
                    return;
                MeshGeometry3D geometry = Model3D.GetGeometry(this.mainViewport, this.m_nChartModelIndex);
                if (geometry == null)
                    return;
                this.m_3dChart.Select(this.m_selectRect, this.m_transformMatrix, this.mainViewport);
                this.m_3dChart.HighlightSelection(geometry, Color.FromRgb((byte)200, (byte)200, (byte)200));
            }
        }
        public void OnViewportMouseDown(object sender, MouseButtonEventArgs args)
        {
            System.Windows.Point position = args.GetPosition((IInputElement)this.mainViewport);
            if (args.ChangedButton == MouseButton.Left)
            {
                this.m_transformMatrix.OnLBtnDown(position);
            }
            else
            {
                if (args.ChangedButton != MouseButton.Right)
                    return;
                this.m_transformMatrix.m_viewMatrix.SetIdentity();
                //m_transformMatrix.m_projMatrix.SetIdentity();//设置为单位矩阵
                //m_transformMatrix.m_projMatrix.Translate(new Vector3D(-num1, -num2, -num3));
                //m_transformMatrix.m_projMatrix.Scale(new Vector3D(Scale / num4, Scale / num5, Scale / num6));
                //this.m_transformMatrix.m_projMatrix.SetIdentity();
                scale = 1.0;
                m_transformMatrix.CalculateProjectionMatrix((double)this.m_3dChart.XMin(), this.m_3dChart.XMax(),(double)this.m_3dChart.YMin(), this.m_3dChart.YMax(), (double)this.m_3dChart.ZMin(), this.m_3dChart.ZMax(), 0.8);
                //this.m_transformMatrix.m_totalMatrix = Matrix3D.Multiply(m_transformMatrix.m_projMatrix, m_transformMatrix.m_viewMatrix);
                this.TransformChart();
                //this.m_selectRect.OnMouseDown(position, this.mainViewport, this.m_nRectModelIndex);
            }
        }
        public void OnViewportMouseMove(object sender, MouseEventArgs args)
        {
            System.Windows.Point position = args.GetPosition((IInputElement)this.mainViewport);
            if (args.LeftButton == MouseButtonState.Pressed)
            {
                this.m_transformMatrix.OnMouseMove(position, this.mainViewport);
                this.TransformChart();
            }
            else
            {
                if (args.RightButton != MouseButtonState.Pressed)
                    return;
                this.m_selectRect.OnMouseMove(position, this.mainViewport, this.m_nRectModelIndex);
            }
        }
        private void TransformChart()
        {
            if (this.m_nChartModelIndex == -1)
                return;
            ModelVisual3D child = (ModelVisual3D)this.mainViewport.Children[this.m_nChartModelIndex];
            if (child.Content == null)
                return;
            Transform3DGroup transform = child.Content.Transform as Transform3DGroup;
            transform.Children.Clear();
            transform.Children.Add((Transform3D)new MatrixTransform3D(this.m_transformMatrix.m_totalMatrix));
        }

        public void SurfacePlot(Mat imageZ)
        {
            try
            {
                if (imageZ.Type() != MatType.CV_32F)
                {
                    Log.Error("Mat must be of type CV_32F.");
                    //throw new ArgumentException("Mat must be of type CV_32F.");
                }

                int xNo = imageZ.Width;
                int yNo = imageZ.Height;
                this.m_3dChart = new UniformSurfaceChart3D();
                this.m_3dChart.ImageWidth = xNo;
                this.m_3dChart.ImageHeight = yNo;
                ((UniformSurfaceChart3D)this.m_3dChart).SetGrid(xNo, yNo, 0, xNo, 0, yNo);

                int dataNo = this.m_3dChart.GetDataNo();

                for (int n = 0; n < dataNo; ++n)
                {
                    int xIndex = n % xNo;
                    int yIndex = n / xNo;
                    float zValue = imageZ.At<float>(yIndex, xIndex);

                    if (zValue != 0)
                    {
                        this.m_3dChart[n].z = zValue;
                    }
                }



                //int xNo = ImageWidth;
                //int yNo = ImageHeight;
                //this.m_3dChart = (Chart3D)new UniformSurfaceChart3D();
                //this.m_3dChart.ImageWidth = xNo;
                //this.m_3dChart.ImageHeight = yNo;
                //((UniformSurfaceChart3D)this.m_3dChart).SetGrid(xNo, yNo,0, xNo, 0, yNo);
                //double num1 = (double)this.m_3dChart.XCenter();
                //double num2 = (double)this.m_3dChart.YCenter();
                //int dataNo = this.m_3dChart.GetDataNo();


                //for (int n = 0; n < dataNo; ++n)
                //{
                //    Vertex3D vertex3D = this.m_3dChart[n];
                //    if (ImageZ[n] != 0)
                //        this.m_3dChart[n].z = (float)ImageZ[n];

                //}

                this.m_3dChart.GetDataRange();
                double zMin = (double)this.m_3dChart.ZMin();
                double zMax = (double)this.m_3dChart.ZMax();

                for (int n = 0; n < dataNo; ++n)
                {
                    if (this.m_3dChart[n].z != 0)
                    {
                        Color color = TextureMapping.PseudoColor(((double)this.m_3dChart[n].z - zMin) / (zMax - zMin));
                        this.m_3dChart[n].color = color;

                        //if (ImageG.Length!= ImageZ.Length)
                        //{
                        //    Color color = TextureMapping.PseudoColor(((double)this.m_3dChart[n].z - zMin) / (zMax - zMin));
                        //    this.m_3dChart[n].color = color;
                        //}
                        //else
                        //{
                        //    Color color = TextureMapping.PseudoColor(((double)this.m_3dChart[n].z - zMin) / (zMax - zMin), ImageG[n]);
                        //    this.m_3dChart[n].color = color;
                        //}
                    }

                }
                ArrayList meshes = ((UniformSurfaceChart3D)this.m_3dChart).GetMeshes();
                this.UpdateModelSizeInfo(meshes);
                Model3D model3D = new Model3D();
                Material backMaterial = (Material)new DiffuseMaterial((Brush)new SolidColorBrush(Colors.Gray));
                this.m_nChartModelIndex = model3D.UpdateModel(meshes, backMaterial, this.m_nChartModelIndex, this.mainViewport);
                float num4 = this.m_3dChart.XMin();
                float num5 = this.m_3dChart.XMax();
                this.m_transformMatrix.CalculateProjectionMatrix((double)num4, (double)num5, (double)this.m_3dChart.YMin(), this.m_3dChart.YMax(), zMin, zMax, 0.8);
                this.TransformChart();
            }
            catch (Exception e )
            {

               
            }
            
        }
        private void UpdateModelSizeInfo(ArrayList meshs)
        {
            int count = meshs.Count;
            int num1 = 0;
            int num2 = 0;
            for (int index = 0; index < count; ++index)
            {
                num1 += ((Mesh3D)meshs[index]).GetVertexNo();
                num2 += ((Mesh3D)meshs[index]).GetTriangleNo();
            }
            //this.labelVertNo.Content = (object)string.Format("Vertex No: {0:d}", (object)num1);
            //this.labelTriNo.Content = (object)string.Format("Triangle No: {0:d}", (object)num2);
        }

        private void Viewport3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            MessageBox.Show("MouseWheel");  // 或 Console.WriteLine，或打断点
            if (e.Delta > 0)
            {
                    scale *= 2;
            }

            else
            {
                    scale /= 2;
            }
            System.Windows.Point position = e.GetPosition((IInputElement)this.mainViewport);
            this.m_transformMatrix.OnMouseScale(position, this.m_3dChart,scale, this.mainViewport);
            this.TransformChart();
            

                //// 计算缩放因子
                //double scale = e.Delta > 0 ? 0.9 : 1.1;
                //var loc= e.GetPosition(canvasOn3D);
                //// 计算新的camera位置
                //Point3D newPosition = camera.Position;
                //Vector3D direction = camera.LookDirection;
                //direction.Normalize();
                //newPosition += direction * scale;

            //// 更新camera位置
            //Point3D p = new Point3D(loc.X,loc.Y, camera.Position.Z);
            //camera.Position = p;
            //ScaleTransform scaleTransform = new ScaleTransform(scale, scale);
            ////w_WindowClip.RenderTransformOrigin = new Point(position.X / w_WindowClip.ActualWidth, position.Y / w_WindowClip.ActualHeight);
            ////ScaleTransform scaleTransform = new ScaleTransform(scaleClip, scaleClip);
            //mainViewport.RenderTransformOrigin = loc;
            //mainViewport.RenderTransform = scaleTransform;

        }
        /// <summary>
        /// 读取图像，转换为灰度，并返回灰度值的浮点数组。
        /// </summary>
        /// <param name="imagePath">图像文件的路径。</param>
        /// <returns>包含图像灰度值的浮点数组和图像的宽度和高度。</returns>
        public static (float[] grayValues, int width, int height) ConvertImageToGrayscaleArray(string imagePath)
        {
            // 使用 OpenCvSharp 读取图像
            Mat resizedImage = new Mat();
             var srcImage = Cv2.ImRead(imagePath, ImreadModes.Grayscale);

            // 确保图像成功加载
            if (srcImage.Empty())
            {
                throw new InvalidOperationException("无法加载图像。");
            }
            Cv2.Resize(srcImage, resizedImage, new OpenCvSharp.Size(srcImage.Width / 10, srcImage.Height / 10), 0, 0, InterpolationFlags.Linear);
            // 获取图像的宽度和高度
            int width = resizedImage.Width;
            int height = resizedImage.Height;

            // 创建一个一维数组来存储灰度值
            float[] grayValues = new float[width * height];

            // 使用索引器访问图像数据
            var indexer = resizedImage.GetGenericIndexer<byte>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 直接将byte值转换为float并存储
                    grayValues[y * width + x] = indexer[y, x];
                }
            }

            return (grayValues, width, height);
        }

    }
}
