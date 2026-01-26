using Kitware.VTK;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;
using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;
using HalconDotNet;

namespace ImageControl
{

    public class MyRenderWindowControl : Kitware.VTK.RenderWindowControl
    {

        private MouseWheelMessageFilter mouseWheelFilter;
        private MouseRightButtonMessageFilter rightButtonFilter;
        /*        private vtkRenderWindowInteractor m_RenderWindowInteractorMy;*/
        public bool Is3DVisibleChild { get; set; } = false;
        public VTKControl OwnerVTKControl { get; set; }
        /*        public MyRenderWindowControl(VTKControl owner)
                {
                    OwnerVTKControl = owner;
                }*/
        /*        protected override void OnHandleCreated(EventArgs e)
                {
                    base.OnHandleCreated(e);
        *//*            // 反射查看字段是否有值
                    var field = typeof(Kitware.VTK.RenderWindowControl)
                        .GetField("m_RenderWindowInteractor", BindingFlags.Instance | BindingFlags.NonPublic);
                    var val = field?.GetValue(this);*//*
                    if (mouseWheelFilter == null)
                    {
                        mouseWheelFilter = new MouseWheelMessageFilter(this.Handle, this); // 传 this
                        System.Windows.Forms.Application.AddMessageFilter(mouseWheelFilter);
                    }
                    if (rightButtonFilter == null)
                    {
                        rightButtonFilter = new MouseRightButtonMessageFilter(this.Handle, this);
                        Application.AddMessageFilter(rightButtonFilter);
                    }
                }*/
        public void InitMessageFilters()
        {
            if (mouseWheelFilter == null)
            {
                mouseWheelFilter = new MouseWheelMessageFilter(this.Handle, this);
                System.Windows.Forms.Application.AddMessageFilter(mouseWheelFilter);
            }
            if (rightButtonFilter == null && OwnerVTKControl != null)
            {
                rightButtonFilter = new MouseRightButtonMessageFilter(this.Handle, this, OwnerVTKControl);
                System.Windows.Forms.Application.AddMessageFilter(rightButtonFilter);
            }
        }
        //first version
        public  void OnCustomMouseWheel2(MouseEventArgs e)
        {
/*            // 1. 反射获得 m_RenderWindowInteractor（可选，如果你有其他用途）
            var field = typeof(Kitware.VTK.RenderWindowControl)
                .GetField("m_RenderWindowInteractor", BindingFlags.Instance | BindingFlags.NonPublic);
            var val = field?.GetValue(this);
            var vtkRenderWindowInteractor2 = val as vtkRenderWindowInteractor;*/

            // 2. 获取当前 VTK 渲染器和相机
            vtkRenderWindow rw = this.RenderWindow;
            vtkRenderer renderer = rw.GetRenderers().GetFirstRenderer();
            vtkCamera camera = renderer.GetActiveCamera();

            // 3. 相机缩放逻辑
            double[] pos = camera.GetPosition();
            double[] fp = camera.GetFocalPoint();
            double[] dir = { fp[0] - pos[0], fp[1] - pos[1], fp[2] - pos[2] };
            double dist = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            // 步长：缩放因子，每次乘0.8表示缩小距离（放大），乘1.2表示增大距离（缩小）
            double factor = 0.85;
            if (e.Delta > 0 && dist > 1e-3) // 向上滚轮（放大），加下限防止零距离
                dist *= factor;
            else if (e.Delta < 0)
                dist /= factor;

            // 单位向量
            double norm = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            if (norm < 1e-8) return; // 防止除零
            dir[0] /= norm; dir[1] /= norm; dir[2] /= norm;
            double newPosX = fp[0] - dir[0] * dist;
            double newPosY = fp[1] - dir[1] * dist;
            double newPosZ = fp[2] - dir[2] * dist;

            camera.SetPosition(newPosX, newPosY, newPosZ);
            camera.SetClippingRange(0.001, dist * 100000);
            renderer.GetRenderWindow().Render();
        }


        //suitable
        public void OnCustomMouseWheel(MouseEventArgs e)
        {
            vtkRenderWindow rw = this.RenderWindow;
            vtkRenderer renderer = rw.GetRenderers().GetFirstRenderer();
            vtkCamera camera = renderer.GetActiveCamera();

            // 1. 获取相机位置和焦点
            double[] pos = camera.GetPosition();
            double[] fp = camera.GetFocalPoint();

            // 2. 计算相机当前视线方向（屏幕法线方向）
            double[] viewDir = { fp[0] - pos[0], fp[1] - pos[1], fp[2] - pos[2] };
            double dist = Math.Sqrt(viewDir[0] * viewDir[0] + viewDir[1] * viewDir[1] + viewDir[2] * viewDir[2]);

/*            double[] fp_real = { fp[0] - dist,  }*/
            if (dist < 1e-8)
            {
                return; // 防止除零
            } 
            /*            if (dist <= 0) {
                            viewDir[0] = pos[0] - fp[0];
                        }*/
            viewDir[0] /= dist; viewDir[1] /= dist; viewDir[2] /= dist; // 单位向量

            // 3. 计算缩放步长
            double factor = 0.95; // 缩放因子，每次0.85倍/1.176倍
            double step = (e.Delta > 0 ? 1 : -1) * dist * (1 - factor);
/*            // 防止缩放到极限距离
            if (dist + step < 1e-3) return;*/

            // 4. 相机和焦点一起平移
            pos[0] += viewDir[0] * step;
            pos[1] += viewDir[1] * step;
            pos[2] += viewDir[2] * step;


            fp[0] += viewDir[0] * step;
            fp[1] += viewDir[1] * step;
            fp[2] += viewDir[2] * step;


            double x = pos[0] - fp[0];
            double y = pos[1] - fp[1];
            double z = pos[2] - fp[2];

            camera.SetPosition(pos[0], pos[1], pos[2]);
            camera.SetFocalPoint(fp[0], fp[1], fp[2]);

            // 5. 自动裁剪范围，防止缩小后点云被裁掉
            double near = 0.001;
            double far = Math.Max(Math.Abs(step) * 1000, 10000);
            camera.SetClippingRange(near, far);
            renderer.GetRenderWindow().Render();
        }

        //焦点不变，只改变相机位置
        public void OnCustomMouseWheel4(MouseEventArgs e)
        {
            vtkRenderWindow rw = this.RenderWindow;
            vtkRenderer renderer = rw.GetRenderers().GetFirstRenderer();
            vtkCamera camera = renderer.GetActiveCamera();

            // 1. 获取相机位置和焦点
            double[] pos = camera.GetPosition();
            double[] fp = camera.GetFocalPoint();

            // 2. 计算视线方向和距离
            double[] viewDir = { fp[0] - pos[0], fp[1] - pos[1], fp[2] - pos[2] };
            double dist = Math.Sqrt(viewDir[0] * viewDir[0] + viewDir[1] * viewDir[1] + viewDir[2] * viewDir[2]);

            // 避免除零，极小距离时用上一次缩放方向（或默认方向）
            double lastViewDirX = 0, lastViewDirY = 0, lastViewDirZ = 1;
            if (dist < 1e-8)
            {
                // 如果上一次有保存的方向，用它（建议你加到类里保存），否则用默认
                viewDir[0] = lastViewDirX;
                viewDir[1] = lastViewDirY;
                viewDir[2] = lastViewDirZ;
                dist = 1e-6;
            }
            else
            {
                // 归一化并保存
                viewDir[0] /= dist; viewDir[1] /= dist; viewDir[2] /= dist;
                // 如果你想让方向记忆，可以在这里保存 viewDir
                // lastViewDirX = viewDir[0]; lastViewDirY = viewDir[1]; lastViewDirZ = viewDir[2];
            }

            // 3. 缩放步长和新距离
            double factor = 0.95; // 缩放速度可调，0.8~0.98都可以
            double newDist;
            if (e.Delta > 0)
                newDist = dist * factor;   // 放大
            else
                newDist = dist / factor;   // 缩小

            // 允许newDist为负，实现穿透
            // 若你想禁止“穿透”可加保护：if (newDist < 0.00001) newDist = 0.00001;

            // 4. 只移动相机，焦点不动
            pos[0] = fp[0] - viewDir[0] * newDist;
            pos[1] = fp[1] - viewDir[1] * newDist;
            pos[2] = fp[2] - viewDir[2] * newDist;

            camera.SetPosition(pos[0], pos[1], pos[2]);
            // 焦点不变
            // ViewUp不变

            // 5. 裁剪范围（保证穿透点云后不会丢失可见性）
            double near = 0.001;
            double far = Math.Max(Math.Abs(newDist) * 1000, 10000);
            camera.SetClippingRange(near, far);

            renderer.GetRenderWindow().Render();
        }

    }

    public class MouseWheelMessageFilter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private IntPtr targetHwnd;
        /*        private VTKControl targetControl;*/
        private MyRenderWindowControl targetControl;

        public MouseWheelMessageFilter(IntPtr hwnd, MyRenderWindowControl control)
        {
            this.targetHwnd = hwnd;
            this.targetControl = control;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && IsHandleChildOf(m.HWnd, targetHwnd))
            {
                //test pasted!!!
                /*                // 只在3D窗口正在显示时弹窗
                                if (targetControl != null && targetControl.Is3DVisibleChild)
                                {
                                    MessageBox.Show("只在自定义 VTK 控件且3D窗口显示时捕获滚轮", "提示");
                                }
                                return false;*/
                /*                if (targetControl != null && targetControl.Is3DVisibleChild)
                                {
                                    // 构造MouseEventArgs
                                    int delta = (short)((m.WParam.ToInt32() >> 16) & 0xffff); // 高16位
                                    System.Drawing.Point pt = System.Windows.Forms.Control.MousePosition;
                                    MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, pt.X, pt.Y, delta);
                                    targetControl.OnCustomMouseWheel(e);
                                }*/
                if (targetControl != null && targetControl.Is3DVisibleChild && targetControl.IsHandleCreated && targetControl.Visible)
                {
                    // 构造MouseEventArgs
                    /*                    int delta = (short)((m.WParam.ToInt32() >> 16) & 0xffff);*/
                    long wParam = m.WParam.ToInt64();
                    int delta = (short)((wParam >> 16) & 0xffff);
                    System.Drawing.Point pt = System.Windows.Forms.Control.MousePosition;
                    MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, pt.X, pt.Y, delta);
                    targetControl.OnCustomMouseWheel(e);
                }

                // 可选：是否拦截消息传递
                return true; // false表示继续传递消息
            }
            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        private bool IsHandleChildOf(IntPtr child, IntPtr parent)
        {
            while (child != IntPtr.Zero)
            {
                if (child == parent)
                    return true;
                child = GetParent(child);
            }
            return false;
        }
    }

    public class MouseRightButtonMessageFilter : IMessageFilter
    {
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private DateTime lastDownTime = DateTime.MinValue;
        private readonly int clickThresholdMs = 250; // 250ms内算“单击”
        private bool rightButtonDown = false;
        private IntPtr targetHwnd;
        private MyRenderWindowControl targetControl; // 或 MyRenderWindowControl
        private VTKControl owner;
        private ContextMenuStrip customMenu; // 新菜单
        public MouseRightButtonMessageFilter(IntPtr hwnd, MyRenderWindowControl control, VTKControl owner)
        {
            this.targetHwnd = hwnd;
            this.targetControl = control;
            this.owner = owner;
            // 在这里定义一个新的右键菜单
            customMenu = new ContextMenuStrip();

            var fitWindow = new ToolStripMenuItem("适应窗口");
            fitWindow.Click += (s, e) =>
            {
                this.owner.FitWindow();
            };

/*            var addLight = new ToolStripMenuItem("添加亮度图");
            addLight.Click += (s, e) =>
            {
                this.owner.AddLightimg();
            };*/
            customMenu.Items.Add(fitWindow);
/*            customMenu.Items.Add(addLight);*/
        }

        public bool PreFilterMessage(ref Message m)
        {
            // 记录鼠标右键按下时间
            if (m.Msg == WM_RBUTTONDOWN && IsHandleChildOf(m.HWnd, targetHwnd))
            {
                rightButtonDown = true;
                lastDownTime = DateTime.Now;
            }

            // 鼠标右键释放
            if (m.Msg == WM_RBUTTONUP && IsHandleChildOf(m.HWnd, targetHwnd))
            {
                if (rightButtonDown)
                {
                    rightButtonDown = false;
                    double interval = (DateTime.Now - lastDownTime).TotalMilliseconds;

                    // 如果在阈值时间内释放，视为“单击”
                    if (interval <= clickThresholdMs)
                    {
                        targetControl.BeginInvoke((Action)(() =>
                        {
                            /*MessageBox.Show("右键单击被捕获！（不影响右键菜单和旋转）");*/
                            var mousePos = Cursor.Position; // 屏幕位置
                            customMenu.Show(mousePos);
                        }));
                    }
                }
                return false;
            }

/*            if (m.Msg == WM_RBUTTONUP && IsHandleChildOf(m.HWnd, targetHwnd))
            {
                *//*                MessageBox.Show("右键单击被捕获！（不影响右键菜单和旋转）");*//*
                targetControl.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show("右键单击被捕获！（不影响右键菜单和旋转）");
                }));
                return false;
            }*/

            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        private bool IsHandleChildOf(IntPtr child, IntPtr parent)
        {
            while (child != IntPtr.Zero)
            {
                if (child == parent)
                    return true;
                child = GetParent(child);
            }
            return false;
        }
    }



    public partial class VTKControl : UserControl
    {
        public MyRenderWindowControl vtkRenderWindow2;
        public vtkRenderWindowInteractor renderWindowInteractor;//OprnGL加速
        private vtkActor actor;
        private Mat oriImg;
        private double minZ = double.MaxValue;
        /*        private bool flag_first_in = false; //是否是第一次进入系统标志*/
        /*        private vtkImageActor imgActorGlobal;*/
        /*        public bool Is3DVisible { get; set; } = false;*/
/*        private bool flag_loadimg = false;*/
        private vtkFloatArray floatArray_vtk;
        private vtkUnsignedCharArray colors_vtk;

        private vtkOrientationMarkerWidget axesWidget;
        private vtkAxesActor axesActor; // 只需全局保存引用即可

        private vtkAxesActor axesLight;
        private vtkOrientationMarkerWidget widgetLight;

        public double scaleZ = 1.0;

        //camera params
        public struct CamParams {
            public double[] pos;
            public double[] fp;
            public double[] up;
            public double angle;
            public double[] clip;
        };

        public CamParams camparams;

        public VTKControl()
        {
            InitializeComponent();
            this.Loaded += UserControl_Loaded;
            this.Loaded += (s, e) =>
            {
                if (vtkRenderWindow2 != null)
                {
                    vtkRenderWindow2.OwnerVTKControl = this;
                    vtkRenderWindow2.InitMessageFilters(); // 这里初始化
                }
            };
        }

        public void DispImage(Mat image)//加载图片
        {
            minZ = double.MaxValue;
            oriImg = image.Clone();
            CreatePointCloudFromTiffFast(image);


        }
        public void FitWindow()//自适应图片
        {
            try {
                vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
                /*            renderer.ResetCamera();*/
                vtkRenderWindow2.RenderWindow.Render();
                vtkCamera camera = renderer.GetActiveCamera();
                camera.SetPosition(camparams.pos[0], camparams.pos[1], camparams.pos[2]);
                camera.SetFocalPoint(camparams.fp[0], camparams.fp[1], camparams.fp[2]);
                camera.SetViewUp(camparams.up[0], camparams.up[1], camparams.up[2]);
                camera.SetViewAngle(camparams.angle);
                camera.SetClippingRange(camparams.clip[0], camparams.clip[1]);
                /*            renderer.ResetCameraClippingRange(); // 可选：有新几何体时推荐*/
                vtkRenderWindow2.RenderWindow.Render();
            }
            catch (Exception) { }

        }

        public void AddLightimg2() {
            if (oriImg == null || oriImg.Empty() || oriImg.Type() != MatType.CV_16SC1)
                return;

            int width = oriImg.Width, height = oriImg.Height;
            Cv2.MinMaxLoc(oriImg, out double min, out double max);

            // 构建透明遮罩图像（RGBA）
            Mat mask = new Mat();
            Cv2.Threshold(oriImg, mask, 0, 255, ThresholdTypes.Binary);
            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }

            Cv2.MinMaxLoc(oriImg, out double minVal1, out double maxVal1, out _, out _, mask);

            Mat rgba = new Mat(oriImg.Size(), MatType.CV_8UC4, Scalar.All(0));
            for (int y = 0; y < oriImg.Rows; y++)
            {
                for (int x = 0; x < oriImg.Cols; x++)
                {
                    short v = oriImg.At<short>(y, x);
                    if (v > 0)
                    {
                        byte gray = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        rgba.Set<Vec4b>(y, x, new Vec4b(gray, gray, gray, 255));
                    }
                }
            }

            byte[] imgRaw = new byte[width * height * 4];
            Marshal.Copy(rgba.Data, imgRaw, 0, imgRaw.Length);
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(imgRaw, 0);

            vtkImageImport importer = vtkImageImport.New();
            importer.CopyImportVoidPointer(ptr, imgRaw.Length);
            importer.SetDataScalarTypeToUnsignedChar();
            importer.SetNumberOfScalarComponents(4);
            importer.SetWholeExtent(0, width - 1, 0, height - 1, 0, 0);
            importer.SetDataExtentToWholeExtent();
            importer.Update();

            vtkImageActor imgActor = vtkImageActor.New();
            imgActor.SetInput(importer.GetOutput());

            imgActor.SetPosition(0, 0, minZ); // 使用点云的minZ抬升亮度图

            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.AddActor(imgActor);

            // 开启透明混合支持
            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);

            renderer.Render();  // 渲染刷新
            vtkRenderWindow2.RenderWindow.Render();
        }
        //优化速度前
        public void AddLightimg3()
        {
            if (oriImg.Empty() || oriImg.Type() != MatType.CV_16SC1)
                return;

            int width = oriImg.Width, height = oriImg.Height;
            double min, max;
            Cv2.MinMaxLoc(oriImg, out min, out max);

            // === Step 1. 生成带Alpha通道的图像（0值透明） ===
            double minVal1, maxVal1;
            Mat mask = new Mat();
            Cv2.Threshold(oriImg, mask, 0, 255, ThresholdTypes.Binary);

            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }
            /*            if (mask.Type() != MatType.CV_8UC1)
                            mask = mask.CvtColor(ColorConversionCodes.GRAY2BGR).CvtColor(ColorConversionCodes.BGR2GRAY);*/

            Cv2.MinMaxLoc(oriImg, out minVal1, out maxVal1, out _, out _, mask);

            Mat rgba = new Mat(oriImg.Size(), MatType.CV_8UC4, Scalar.All(0)); // BGRA
            for (int y = 0; y < oriImg.Rows; y++)
            {
                for (int x = 0; x < oriImg.Cols; x++)
                {
                    short v = oriImg.At<short>(y, x);
                    if (v > 0)
                    {
                        byte gray = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        rgba.Set<Vec4b>(y, x, new Vec4b(gray, gray, gray, 255)); // B,G,R,A
                    }
                    // 否则保持 (0,0,0,0)
                }
            }

            // === Step 2. 用 vtkImageImport 显示 RGBA 图像 ===
            vtkImageImport importer = vtkImageImport.New();
            byte[] imgRaw = new byte[width * height * 4];
            Marshal.Copy(rgba.Data, imgRaw, 0, imgRaw.Length);
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(imgRaw, 0);

            importer.CopyImportVoidPointer(ptr, imgRaw.Length);
            importer.SetDataScalarTypeToUnsignedChar();
            importer.SetNumberOfScalarComponents(4);
            importer.SetWholeExtent(0, width - 1, 0, height - 1, 0, 0);
            importer.SetDataExtentToWholeExtent();
            importer.Update();

            vtkImageActor imgActor = vtkImageActor.New();
            imgActor.SetInput(importer.GetOutput());
            /*            imgActor.GetProperty().SetOpacity(1);*/

            // === Step 3. 点云处理 ===
            Mat binary = new Mat();
            Mat nonZeroLocations = new Mat();
            Cv2.Threshold(oriImg, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);
            floatArray.SetNumberOfTuples(numPoints);

            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(4);
            colors.SetName("Colors");

            double scale = 10.0;
            short minVal = (short)((min + 0.1) / scale);
            short maxVal = (short)(max / scale);
            /*            double minZ = double.MaxValue;*/

            for (int i = 0; i < numPoints; i++)
            {
                var pt = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = pt.X, y = pt.Y;
                short z = (short)(oriImg.At<short>(y, x) / scale);
                floatArray.SetTuple3(i, x, y, z);
                if (z < minZ) minZ = z;

                double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                byte alpha = 30;
                colors.InsertNextTuple4(r, g, b, alpha);
            }

            points.SetData(floatArray);
            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors);

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            actor = vtkActor.New();
            actor.SetMapper(mapper);
            /*            actor.GetProperty().SetPointSize(5);*/
            actor.GetProperty().SetOpacity(0.3); // 每个点颜色决定透明度

            // 将原图抬升到 minZ 层
            imgActor.SetPosition(0, 0, minZ);

            // === Step 4. 渲染 ===
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            renderer.AddActor(imgActor);
            renderer.AddActor(actor);
            renderer.SetBackground(0.1, 0.1, 0.1);

            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);

            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();

            // 相机保存
            vtkCamera camera = renderer.GetActiveCamera();
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();

        }
        //优化速度后
        public void AddLightimg()
        {
            if (oriImg.Empty() || oriImg.Type() != MatType.CV_16SC1)
                return;

            int width = oriImg.Width, height = oriImg.Height;
            double min, max;
            Cv2.MinMaxLoc(oriImg, out min, out max);

            // === Step 1. 生成带Alpha通道的图像（0值透明） ===
            double minVal1, maxVal1;
            Mat mask = new Mat();
            Cv2.Threshold(oriImg, mask, 0, 255, ThresholdTypes.Binary);

            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }
            /*            if (mask.Type() != MatType.CV_8UC1)
                            mask = mask.CvtColor(ColorConversionCodes.GRAY2BGR).CvtColor(ColorConversionCodes.BGR2GRAY);*/

            Cv2.MinMaxLoc(oriImg, out minVal1, out maxVal1, out _, out _, mask);

            Mat rgba = new Mat(oriImg.Size(), MatType.CV_8UC4, Scalar.All(0)); // BGRA
            for (int y = 0; y < oriImg.Rows; y++)
            {
                for (int x = 0; x < oriImg.Cols; x++)
                {
                    short v = oriImg.At<short>(y, x);
                    if (v > 0)
                    {
                        byte gray = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        rgba.Set<Vec4b>(y, x, new Vec4b(gray, gray, gray, 255)); // B,G,R,A
                    }
                    // 否则保持 (0,0,0,0)
                }
            }

            // === Step 2. 直接传给VTK ===
            vtkImageImport importer = vtkImageImport.New();
            importer.CopyImportVoidPointer(rgba.Data, width * height * 4);
            importer.SetDataScalarTypeToUnsignedChar();
            importer.SetNumberOfScalarComponents(4);
            importer.SetWholeExtent(0, width - 1, 0, height - 1, 0, 0);
            importer.SetDataExtentToWholeExtent();
            importer.Update();

            vtkImageActor imgActor = vtkImageActor.New();
            imgActor.SetInput(importer.GetOutput());
            vtkPoints points = vtkPoints.New();
            vtkPolyData polyData = vtkPolyData.New();

            if (floatArray_vtk != null && colors_vtk != null)
            {
                points.SetData(floatArray_vtk);
/*                vtkPolyData polyData = vtkPolyData.New();*/
                polyData.SetPoints(points);
                polyData.GetPointData().SetScalars(colors_vtk);
            }
            else { 

                // === Step 3. 点云处理（RGB，无需Alpha通道） ===
                Mat binary = new Mat();
                Mat nonZeroLocations = new Mat();
                Cv2.Threshold(oriImg, binary, min + 0.1, max, ThresholdTypes.Tozero);
                Cv2.FindNonZero(binary, nonZeroLocations);

                int numPoints = nonZeroLocations.Rows;
/*                vtkPoints points = vtkPoints.New();*/
                vtkFloatArray floatArray = vtkFloatArray.New();
                floatArray.SetNumberOfComponents(3);
                floatArray.SetNumberOfTuples(numPoints);

                vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
                colors.SetNumberOfComponents(3);
                colors.SetName("Colors");

                scaleZ = 10.0;
                short minVal = (short)((min + 0.1) / scaleZ);
                short maxVal = (short)(max / scaleZ);

                double minZ = double.MaxValue;

                // 并行处理点云（如需加速，可使用Parallel.For, 这里为演示单线程）
                for (int i = 0; i < numPoints; i++)
                {
                    var pt = nonZeroLocations.At<OpenCvSharp.Point>(i);
                    int x = pt.X, y = pt.Y;
                    short z = (short)(oriImg.At<short>(y, x) / scaleZ);
                    floatArray.SetTuple3(i, x, y, z);
                    if (z < minZ) minZ = z;

                    double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                    byte r = 0, g = 0, b = 0;
                    if (normalizedValue < 0.33)
                    {
                        b = (byte)(255 * (normalizedValue / 0.33));
                    }
                    else if (normalizedValue < 0.66)
                    {
                        b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                        g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                    }
                    else
                    {
                        g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                        r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                    }
                    colors.InsertNextTuple3(r, g, b);
                }
                points.SetData(floatArray);
                /*vtkPolyData polyData = vtkPolyData.New();*/
                polyData.SetPoints(points);
                polyData.GetPointData().SetScalars(colors);
            }


/*            points.SetData(floatArray);
            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors);*/

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            actor = vtkActor.New();
            actor.SetMapper(mapper);
            actor.GetProperty().SetOpacity(0.5);

            // === Step 4. 渲染 ===
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            imgActor.SetPosition(0, 0, minZ);
            renderer.AddActor(imgActor);
            renderer.AddActor(actor);
            renderer.SetBackground(0.1, 0.1, 0.1);

            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);
            vtkCamera camera = renderer.GetActiveCamera();
            camera.SetPosition(camparams.pos[0], camparams.pos[1], camparams.pos[2]);
            camera.SetFocalPoint(camparams.fp[0], camparams.fp[1], camparams.fp[2]);
            camera.SetViewUp(camparams.up[0], camparams.up[1], camparams.up[2]);
            camera.SetViewAngle(camparams.angle);
            camera.SetClippingRange(camparams.clip[0], camparams.clip[1]);
            /*            renderer.ResetCamera();*/
            // 完全删除三轴标志
            if (axesWidget != null)
            {
                axesWidget.SetEnabled(0);       // 禁用widget
                axesWidget.SetInteractor(null); // 解除与窗口交互器的绑定（可选但更保险）
                axesWidget = null;              // 释放引用
            }

            // 如果你还添加了三维世界的轴（比如 renderer.AddActor(axesActor)）
            if (axesActor != null)
            {
                renderer.RemoveActor(axesActor);
                axesActor = null;               // 释放引用
            }
            // ---- xyz坐标轴标记 ----
            axesLight = vtkAxesActor.New();
/*            // 让轴身变粗（默认0.5，建议0.05-0.2之间）
            axesLight.SetCylinderRadius(5);

            // 让箭头锥体变粗（默认0.6，建议0.1-0.5之间）
            axesLight.SetConeRadius(5);

            // 可选：让轴的末端球体也变大
            axesLight.SetSphereRadius(5);

            // 可选：调整轴长
            axesLight.SetTotalLength(10, 10, 10); // x/y/z轴长度*/
            widgetLight = vtkOrientationMarkerWidget.New();
            widgetLight.SetOrientationMarker(axesLight);
            widgetLight.SetInteractor(vtkRenderWindow2.RenderWindow.GetInteractor());
            widgetLight.SetViewport(0.0, 0.0, 0.2, 0.4);
            widgetLight.SetEnabled(1);
            widgetLight.InteractiveOff();
            // -----------------------
            vtkRenderWindow2.RenderWindow.Render();
        }

        private void InitializeVTKRenderWindow()
        {
            vtkRenderWindow2 = Windows3D;
            //vtkRenderWindow2.Dock = DockStyle.Fill;
            //vtkHostPanel.Controls.Add(vtkRenderWindow2);

            //vtkRenderWindow2.RenderWindow.SetMultiSamples(0); // 关闭抗锯齿以提高性能
            //vtkRenderWindow2.RenderWindow.SetAlphaBitPlanes(1); // 启用alpha通道，有助于某些类型的渲染
            //renderWindowInteractor = vtkRenderWindowInteractor.New();
            //renderWindowInteractor.SetRenderWindow(vtkRenderWindow2.RenderWindow);
            //// 启动交互器的事件循环
            //renderWindowInteractor.Start();
            //vtkRenderWindow.RenderWindow.GetRenderers().GetFirstRenderer().SetBackground(0.1, 0.2, 0.4);
            //var renderer = vtkRenderer.New();
            //renderer.SetBackground(0.1, 0.2, 0.4);
            //vtkRenderWindow.RenderWindow.AddRenderer(renderer);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeVTKRenderWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
        public void VtkRenderWindow_Load()
        {
            //var renderer = vtkRenderWindow.RenderWindow.GetRenderers().GetFirstRenderer();

            //var plyReader = vtkPLYReader.New();
            //plyReader.SetFileName("C:\\Users\\caopengfei\\Desktop\\ye\\PLY\\0.ply");
            //try
            //{
            //    plyReader.Update();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Failed to load PLY file: {ex.Message}");
            //    return; // 退出方法，避免进一步的操作
            //}

            //var mapper = vtkPolyDataMapper.New();
            //mapper.SetInputConnection(plyReader.GetOutputPort());

            //var actor = vtkActor.New();
            //actor.SetMapper(mapper);

            //renderer.AddActor(actor);
            //renderer.ResetCamera();
            //vtkRenderWindow.RenderWindow.Render();


            vtkPoints points = new vtkPoints(); // 新建点云类型
            
            for (int i = 1; i < 170; i++) // 170个点
            {
                points.InsertPoint(i, (double)i, (double)i * 2, (double)i * 3); // 修改点的坐标以避免重叠
            }
            vtkPolyData polydata = vtkPolyData.New();
            polydata.SetPoints(points);

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polydata); // 直接设置输入数据

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            vtkActor actor = vtkActor.New();
            actor.SetMapper(mapper);
            actor.GetProperty().SetPointSize(4f); // 确保点的大小足够大以便观察
            actor.GetProperty().SetColor(1.0, 0.0, 0.0); // 设置点的颜色为红色

            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.SetBackground(0.1, 0.1, 0.1); // 设置深色背景
            renderer.AddActor(actor); // 添加actor到渲染器

            renderer.ResetCamera(); // 重置相机
            vtkRenderWindow2.RenderWindow.Render(); // 执行渲染

        }


    public void CreatePointCloudFromTiffFast_Halcon(HObject ho_Image)
        {
            if (ho_Image == null || !ho_Image.IsInitialized())
            {
                return;
            }

            // 获取图像信息
            HTuple type;
            HOperatorSet.GetImageType(ho_Image, out type);

            // 检查是否是 16 位有符号整型图像（Halcon 中一般是 "int2"）
            if (type.S != "int2")
            {
                return;
            }

            // 保存原始图像（可选）
            // oriImg = ... // Halcon 没有直接 Mat 类型，保留 HObject 即可

            // 获取图像的最小最大值
            HTuple min, max;
            HOperatorSet.MinMaxGray(ho_Image, ho_Image, 0, out min, out max, out _);


/*            // 1. 找到 min+0.1 以下的区域
            HObject ho_BelowThreshold;
            HOperatorSet.Threshold(ho_Image, out ho_BelowThreshold, float.MinValue, min + 0.1); // 下限给一个很小的值

            // 2. 把这些区域的灰度值设为 0
            HOperatorSet.PaintRegion(ho_BelowThreshold, ho_Image, out ho_Image, 0, "fill");*/
            // 区间 1: min+0.1 到 0（负值部分，排除 0 以下的值）
            HObject ho_Range1;
            HOperatorSet.Threshold(ho_Image, out ho_Range1, min + 0.1, -1);//原图为int格式

            // 区间 2: 0 到 max（正值部分，排除小于 0 的值）
            HObject ho_Range2;
            HOperatorSet.Threshold(ho_Image, out ho_Range2, 1, max);

            // 合并两个区间
            HObject ho_Threshold;
            HOperatorSet.Union2(ho_Range1, ho_Range2, out ho_Threshold);

            // 提取非零点的 (row, col)
            HTuple rows, cols;
            HOperatorSet.GetRegionPoints(ho_Threshold, out rows, out cols);


            int numPoints = rows.Length;
            if (numPoints == 0)
            {
                return;
            }
            // 一次性获取这些点的灰度值
            HTuple grayVals;
            HOperatorSet.GetGrayval(ho_Image, rows, cols, out grayVals);


            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);
            floatArray.SetNumberOfTuples(numPoints);

            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(3);
            colors.SetName("Colors");

            scaleZ = 10.0;
            short minVal = (short)((min.D + 0.1) / scaleZ);
            short maxVal = (short)(max.D / scaleZ);

            // === 包围盒自动相机设置 ===
            float minXCam = float.MaxValue, maxXCam = float.MinValue;
            float minYCam = float.MaxValue, maxYCam = float.MinValue;
            float minZCam = float.MaxValue, maxZCam = float.MinValue;

            int index = 0;
            for (int i = 0; i < numPoints; i++)
            {
                int x = (int)cols[i];
                int y = (int)rows[i];
                short z = (short)(grayVals[i].I / scaleZ);

/*                // 从图像获取深度值
                HTuple grayVal;
                HOperatorSet.GetGrayval(ho_Image, y, x, out grayVal);*/
/*                short z = (short)(grayVal.I / scaleZ);*/
                if (z < minZ) minZ = z;

                // 更新包围盒
                if (x < minXCam) minXCam = x; if (x > maxXCam) maxXCam = x;
                if (y < minYCam) minYCam = y; if (y > maxYCam) maxYCam = y;
                if (z < minZCam) minZCam = z; if (z > maxZCam) maxZCam = z;

                floatArray.SetTuple3(index, x, y, z);

                // 归一化
                double normalizedValue = (double)(z - minVal) / (maxVal - minVal);

                // 映射颜色
                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                colors.InsertNextTuple3(r, g, b);
                index++;
            }
            //for cam pose
            float centerX = (minXCam + maxXCam) / 2.0f;
            float centerY = (minYCam + maxYCam) / 2.0f;
            float centerZ = (minZCam + maxZCam) / 2.0f;

            float rangeX = maxXCam - minXCam;
            float rangeY = maxYCam - minYCam;
            float rangeZ = maxZCam - minZCam;
            float maxRange = Math.Max(rangeX, Math.Max(rangeY, rangeZ));

            points.SetData(floatArray);

            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors); // Add color information

            //for fast add light img
            floatArray_vtk = floatArray;
            colors_vtk = colors;

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            /*            if (actor==null)*/
            actor = vtkActor.New();
            /*            renderer.RemoveActor(actor);*/
            actor.SetMapper(mapper);

            renderer.SetBackground(0.1, 0.1, 0.1);

            renderer.AddActor(actor);


            //renderWindowInteractor.SetRenderWindow(vtkRenderWindow2.RenderWindow);
            /*            if (!flag_first_in)
                        {*/
            vtkCamera camera = renderer.GetActiveCamera();
            double cameraZ = centerZ + maxRange * 2.0;  // 正上方看下来
            camera.SetPosition(centerX, centerY, cameraZ);
            camera.SetFocalPoint(centerX, centerY, centerZ);
            camera.SetViewUp(0, 1, 0); // Y轴朝上
            camera.SetViewAngle(30);
            camera.SetClippingRange(0.001, maxRange * 100000);
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();

            // 1. 创建三轴坐标轴Actor
            axesActor = vtkAxesActor.New();

            // 2. 创建小窗口widget并把axes放进去
            axesWidget = vtkOrientationMarkerWidget.New();
            axesWidget.SetOutlineColor(0.9300, 0.5700, 0.1300); // 可选，设置外框颜色
            axesWidget.SetOrientationMarker(axesActor);
            axesWidget.SetInteractor(vtkRenderWindow2.RenderWindow.GetInteractor());
            /*            axesWidget.SetViewport(0.0, 0.0, 0.15, 0.3); // 左下角，占窗口15%x30%区域*/
            axesWidget.SetViewport(0.0, 0.0, 0.2, 0.4); // 左下角，占窗口15%x30%区域
            axesWidget.SetEnabled(1);     // 启用
            axesWidget.InteractiveOff();  // 不可鼠标拖动（如需可拖动可删去这行）

            vtkRenderWindow2.RenderWindow.Render();
            // floatArray 和 colors 就可以直接传给 vtkPolyData 使用
        }

    public void CreatePointCloudFromTiffFastHalcon_Fast(HObject ho_Image)
    {
        if (ho_Image == null || !ho_Image.IsInitialized())
            return;

        // 获取图像信息
        HTuple type;
        HOperatorSet.GetImageType(ho_Image, out type);
            //if (type.S != "int2") // 必须是16位有符号整型
            //    return;
            scaleZ = 10;
            // 获取图像的最小最大值
            HTuple min, max;
            HObject ho_Range1;
            HOperatorSet.MinMaxGray(ho_Image, ho_Image, 0, out min, out max, out _);
            if (min < -100)
            {
                HOperatorSet.Threshold(ho_Image, out ho_Range1, min + 1, max);
            }
            else
            {
                HOperatorSet.Threshold(ho_Image, out ho_Range1, min + 0.01, max);
                //ho_Range1 = ((HImage)ho_Image).GetDomain();
            }
            HOperatorSet.ScaleImage(ho_Image, out HObject ImageScale, 1000, 0);
            HOperatorSet.ConvertImageType(ImageScale, out ImageScale, "int2");
            /*        HOperatorSet.Threshold(ho_Image, out ho_Range1, min + 0.1, -1);*/

            // 区间2: 1 到 max

            /*        HOperatorSet.Threshold(ho_Image, out ho_Range2, 1, max);*/

            // 获取所有有效点的(row, col)
            HTuple rows, cols;
        HOperatorSet.GetRegionPoints(ho_Range1, out rows, out cols);
        int numPoints = rows.Length;
        if (numPoints == 0)
            return;

        // 一次性获取这些点的灰度值
        HTuple grayVals;
        HOperatorSet.GetGrayval(ImageScale, rows, cols, out grayVals);
            HOperatorSet.TupleMin(grayVals, out min);
            HOperatorSet.TupleMax(grayVals, out max);
            // ==== 准备LUT（颜色映射表） ====
            //scaleZ = 10;
            short minVal = (short)((min.D) / scaleZ);
        short maxVal = (short)(max.D / scaleZ);

        // 65536 对应 short 的所有可能值（-32768 ~ 32767）
        byte[] lutR = new byte[65536];
        byte[] lutG = new byte[65536];
        byte[] lutB = new byte[65536];

        for (int z = minVal; z <= maxVal; z++)
        {
            // 映射到 0~65535
            int lutIndex = z + 32768;
            if (lutIndex < 0 || lutIndex >= 65536)
                continue;

            double normalizedValue = (double)(z - minVal) / (maxVal - minVal);
            byte r = 0, g = 0, b = 0;
            if (normalizedValue < 0.33)
            {
                b = (byte)(255 * (normalizedValue / 0.33));
            }
            else if (normalizedValue < 0.66)
            {
                b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
            }
            else
            {
                g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
            }

            lutR[lutIndex] = r;
            lutG[lutIndex] = g;
            lutB[lutIndex] = b;
        }

        // ==== 一次性生成点云 & 颜色数据 ====
        float[] pointData = new float[numPoints * 3];
        byte[] colorData = new byte[numPoints * 3];
        float minXCam = float.MaxValue, maxXCam = float.MinValue;
        float minYCam = float.MaxValue, maxYCam = float.MinValue;
        float minZCam = float.MaxValue, maxZCam = float.MinValue;

        for (int i = 0; i < numPoints; i++)
        {
            int x = (int)cols[i];
            int y = (int)rows[i];
            int temp = grayVals[i];
            int z = (int)Math.Round(temp / scaleZ, 0);
                //int z =(int)e/scaleZ;

                // 更新包围盒
                if (x < minXCam) minXCam = x;
            if (x > maxXCam) maxXCam = x;
            if (y < minYCam) minYCam = y;
            if (y > maxYCam) maxYCam = y;
            if (z < minZCam) minZCam = z;
            if (z > maxZCam) maxZCam = z;

            // 点云
            int idx3 = i * 3;
            pointData[idx3] = x;
            pointData[idx3 + 1] = y;
            pointData[idx3 + 2] = z;

            // 颜色（查LUT，注意加偏移）
            int lutIndex = z + 32768;
            colorData[idx3] = lutR[lutIndex];
            colorData[idx3 + 1] = lutG[lutIndex];
            colorData[idx3 + 2] = lutB[lutIndex];
        }

        // ==== 一次性传给VTK ====
        vtkPoints points = vtkPoints.New();
        vtkFloatArray floatArray = vtkFloatArray.New();
        floatArray.SetNumberOfComponents(3);
        GCHandle handlePoints = GCHandle.Alloc(pointData, GCHandleType.Pinned);
        try
        {
            IntPtr ptrPoints = handlePoints.AddrOfPinnedObject();
            floatArray.SetArray(ptrPoints, numPoints * 3, 1);
        }
        finally
        {
            handlePoints.Free();
        }
        points.SetData(floatArray);
/*            floatArray.SetArray(pointData, numPoints * 3, 1);*/
        points.SetData(floatArray);

        vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
        colors.SetNumberOfComponents(3);
        colors.SetName("Colors");
        GCHandle handleColors = GCHandle.Alloc(colorData, GCHandleType.Pinned);
        try
        {
            IntPtr ptrColors = handleColors.AddrOfPinnedObject();
            colors.SetArray(ptrColors, numPoints * 3, 1);
        }
        finally
        {
            handleColors.Free();
        }
/*            colors.SetArray(colorData, numPoints * 3, 1);*/

        vtkPolyData polyData = vtkPolyData.New();
        polyData.SetPoints(points);
        polyData.GetPointData().SetScalars(colors);

        vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
        glyphFilter.SetInput(polyData);

        vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
        mapper.SetInputConnection(glyphFilter.GetOutputPort());

        vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
        renderer.RemoveAllViewProps();
        actor = vtkActor.New();
        actor.SetMapper(mapper);
        renderer.SetBackground(0.1, 0.1, 0.1);
        renderer.AddActor(actor);

        // ==== 设置相机 ====
        float centerX = (minXCam + maxXCam) / 2.0f;
        float centerY = (minYCam + maxYCam) / 2.0f;
        float centerZ = (minZCam + maxZCam) / 2.0f;
        float rangeX = maxXCam - minXCam;
        float rangeY = maxYCam - minYCam;
        float rangeZ = maxZCam - minZCam;
        float maxRange = Math.Max(rangeX, Math.Max(rangeY, rangeZ));

        vtkCamera camera = renderer.GetActiveCamera();
        double cameraZ = centerZ + maxRange * 2.0;
        camera.SetPosition(centerX, centerY, cameraZ);
        camera.SetFocalPoint(centerX, centerY, centerZ);
        camera.SetViewUp(0, 1, 0);
        camera.SetViewAngle(30);
        camera.SetClippingRange(0.001, maxRange * 100000);

        camparams.pos = camera.GetPosition();
        camparams.fp = camera.GetFocalPoint();
        camparams.up = camera.GetViewUp();
        camparams.angle = camera.GetViewAngle();
        camparams.clip = camera.GetClippingRange();

            // ==== 坐标轴 ====
            axesActor = vtkAxesActor.New();
        axesWidget = vtkOrientationMarkerWidget.New();
        axesWidget.SetOutlineColor(0.93, 0.57, 0.13);
        axesWidget.SetOrientationMarker(axesActor);
        axesWidget.SetInteractor(vtkRenderWindow2.RenderWindow.GetInteractor());
        axesWidget.SetViewport(0.0, 0.0, 0.2, 0.4);
        axesWidget.SetEnabled(1);
        axesWidget.InteractiveOff();

        vtkRenderWindow2.RenderWindow.Render();
    }

        public void CreatePointCloudFromTiffFast(Mat mat)
        {
            //var mat = Cv2.ImRead(imagePath, ImreadModes.Unchanged); // 使用Unchanged保持图像原始深度
            
            if (mat.Empty() || mat.Type() != MatType.CV_16SC1)
            {
                return;
            }
            oriImg = mat.Clone();
            //应用阈值分割
            Cv2.MinMaxLoc(mat, out double min, out double max);
            var binary = new Mat();
            var nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            // 使用 FindNonZero 查找所有非零点
            Cv2.FindNonZero(binary, nonZeroLocations);
            int index = 0;
            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3); // x, y, z
            floatArray.SetNumberOfTuples(numPoints);

            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(3); // R, G, B
            colors.SetName("Colors");

            short minVal = short.MaxValue; //ushort 2 short
            short maxVal = short.MinValue;
/*            double minZ = double.MaxValue;*/
            scaleZ = 10.0;

            minVal = (short)((min+0.1) / scaleZ);
            maxVal = (short)(max / scaleZ);
            //First pass to find min and max grayscale values
            //    for (int y = 0; y < mat.Rows; y++)
            //{
            //    for (int x = 0; x < mat.Cols; x++)
            //    {
            //        ushort z = (ushort)(mat.At<ushort>(y, x) / scale);
            //        if (z < minVal) minVal = z;
            //        if (z > maxVal) maxVal = z;
            //    }
            //}

            // === 包围盒自动相机设置 ===
            float minXCam = float.MaxValue, maxXCam = float.MinValue;
            float minYCam = float.MaxValue, maxYCam = float.MinValue;
            float minZCam = float.MaxValue, maxZCam = float.MinValue;

/*            vtkRenderWindow2.bounds.min_x = minXCam;
            vtkRenderWindow2.bounds.max_x = maxXCam;
            vtkRenderWindow2.bounds.min_y = minYCam;
            vtkRenderWindow2.bounds.max_y = maxYCam;
            vtkRenderWindow2.bounds.min_z = minZCam;
            vtkRenderWindow2.bounds.max_z = maxZCam;*/

            // 遍历非零点
            for (int i = 0; i < nonZeroLocations.Rows; i++)
            {
                // 获取每个点的坐标
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = point.X;
                int y = point.Y;

                // 在这里处理每个点，例如打印出来
                short z = (short)(mat.At<short>(y, x) / scaleZ);
                if (z < minZ) minZ = z;
                //for cam pose
                if (x < minXCam) minXCam = x; if (x > maxXCam) maxXCam = x;
                if (y < minYCam) minYCam = y; if (y > maxYCam) maxYCam = y;
                if (z < minZCam) minZCam = z; if (z > maxZCam) maxZCam = z;
                floatArray.SetTuple3(index, x, y, z);

                // Map the grayscale value to a color
                double normalizedValue = (double)(z - minVal) / (maxVal - minVal);

                //original
                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    // Blue increases from 0 to 255
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    // Blue decreases from 255 to 0, Green increases from 0 to 255
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    // Green decreases from 255 to 0, Red increases from 0 to 255
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                /*                //added by qhchen
                                // 根据归一化的值计算颜色
                                byte r = (byte)(255* normalizedValue), g = (byte)(255 * normalizedValue), b = (byte)(255 * normalizedValue);
                                *//*                r = (byte)(255);*/

                colors.InsertNextTuple3(r, g, b);
                index++;
            }

            //for cam pose
            float centerX = (minXCam + maxXCam) / 2.0f;
            float centerY = (minYCam + maxYCam) / 2.0f;
            float centerZ = (minZCam + maxZCam) / 2.0f;

            float rangeX = maxXCam - minXCam;
            float rangeY = maxYCam - minYCam;
            float rangeZ = maxZCam - minZCam;
            float maxRange = Math.Max(rangeX, Math.Max(rangeY, rangeZ));

            points.SetData(floatArray);

            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors); // Add color information

            //for fast add light img
            floatArray_vtk = floatArray;
            colors_vtk = colors;

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            /*            if (actor==null)*/
            actor = vtkActor.New();
/*            renderer.RemoveActor(actor);*/
            actor.SetMapper(mapper);

            renderer.SetBackground(0.1, 0.1, 0.1);
            
            renderer.AddActor(actor);


            //renderWindowInteractor.SetRenderWindow(vtkRenderWindow2.RenderWindow);
/*            if (!flag_first_in)
            {*/
            vtkCamera camera = renderer.GetActiveCamera();
            double cameraZ = centerZ + maxRange * 2.0;  // 正上方看下来
            camera.SetPosition(centerX, centerY, cameraZ);
            camera.SetFocalPoint(centerX, centerY, centerZ);
            camera.SetViewUp(0, 1, 0); // Y轴朝上
            camera.SetViewAngle(30);
            camera.SetClippingRange(0.001, maxRange * 100000);
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();
            /*            flag_first_in = true;*/
            /*                renderer.ResetCamera();*/
            /*            }*/
            /*            else {
                            vtkCamera camera = renderer.GetActiveCamera();
                            camera.SetPosition(camparams.pos[0], camparams.pos[1], camparams.pos[2]);
                            camera.SetFocalPoint(camparams.fp[0], camparams.fp[1], camparams.fp[2]);
                            camera.SetViewUp(camparams.up[0], camparams.up[1], camparams.up[2]);
                            camera.SetViewAngle(camparams.angle);
                            camera.SetClippingRange(camparams.clip[0], camparams.clip[1]);
                        }*/

            // 1. 创建三轴坐标轴Actor
            axesActor = vtkAxesActor.New();

            // 2. 创建小窗口widget并把axes放进去
            axesWidget = vtkOrientationMarkerWidget.New();
            axesWidget.SetOutlineColor(0.9300, 0.5700, 0.1300); // 可选，设置外框颜色
            axesWidget.SetOrientationMarker(axesActor);
            axesWidget.SetInteractor(vtkRenderWindow2.RenderWindow.GetInteractor());
            /*            axesWidget.SetViewport(0.0, 0.0, 0.15, 0.3); // 左下角，占窗口15%x30%区域*/
            axesWidget.SetViewport(0.0, 0.0, 0.2, 0.4); // 左下角，占窗口15%x30%区域
            axesWidget.SetEnabled(1);     // 启用
            axesWidget.InteractiveOff();  // 不可鼠标拖动（如需可拖动可删去这行）

            vtkRenderWindow2.RenderWindow.Render();


            // 启动交互器的事件循环
            //renderWindowInteractor.Start();
        }


        public void CreatePointCloudFromTiffFast2(Mat mat)
        {
            if (mat.Empty())
            {
                return;
            }

            if (mat.Type() != MatType.CV_16SC1)
            {
                return;
            }
            int cc = 0;
            int tt = 0;
            for (int y = 0; y < mat.Rows; y++)  // 遍历行（y坐标）
            {
                for (int x = 0; x < mat.Cols; x++)  // 遍历列（x坐标）
                {
                    short pixelValue = mat.At<short>(y, x);
                    if (pixelValue == 0) {
                        cc++;
                    }
                    tt++;
                }
            }
            // 应用阈值分割
            Cv2.MinMaxLoc(mat, out double min, out double max);
            var binary = new Mat();
            var nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            int index = 0;
            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3); // x, y, z
            floatArray.SetNumberOfTuples(numPoints);

            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(3); // R, G, B
            colors.SetName("Colors");

            // 获取所有的 z 值
            List<short> zListOri = new List<short>();
            for (int i = 0; i < nonZeroLocations.Rows; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                short z = (short)(mat.At<short>(point.Y, point.X) / 10.0);  // 假设 scale 是 10
                zListOri.Add(z);
            }
            short minin = zListOri.Min();
            ushort ing = (ushort)minin;
/*            ushort minin = ushort(zListOri.Min());*/

            // 获取所有的 z 值
            List<ushort> zList = new List<ushort>();
            for (int i = 0; i < nonZeroLocations.Rows; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                ushort z = (ushort)(mat.At<ushort>(point.Y, point.X) / 10.0);  // 假设 scale 是 10
                zList.Add(z);
            }

            // 计算 1% 和 99% 分位数
            double z1 = Percentile(zList, 1);
            double z99 = Percentile(zList, 99);

            // 过滤数据：剔除小于 z1 和大于 z99 的数据
            List<ushort> validZValues = zList.Where(z => z >= z1 && z <= z99).ToList();

            // 归一化处理
            double validZMin = validZValues.Min();
            double validZMax = validZValues.Max();

            // 遍历有效数据进行归一化
            for (int i = 0; i < nonZeroLocations.Rows; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = point.X;
                int y = point.Y;

                ushort z = (ushort)(mat.At<ushort>(y, x) / 10.0);
                // 颜色映射（根据归一化值设置颜色）
                byte r = 0, g = 0, b = 0;
                // 剔除超出范围的值
                if (z < z1 || z > z99)
                {
                    // 不进行处理或做特殊处理
                    //continue;
                    r = 255;
                    g = 255;
                    b = 255;
                }
                else {
                    // 归一化处理
                    double normalizedValue = (z - validZMin) / (validZMax - validZMin);
                    if (normalizedValue < 0.33)
                    {
                        b = (byte)(255 * (normalizedValue / 0.33));
                    }
                    else if (normalizedValue < 0.66)
                    {
                        b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                        g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                    }
                    else
                    {
                        g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                        r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                    }
                }

                // 设置点云数据和颜色
                floatArray.SetTuple3(index, x, y, (float)z);
                colors.InsertNextTuple3(r, g, b);
                index++;
            }

            points.SetData(floatArray);

            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors); // Add color information

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            if (actor == null)
                actor = vtkActor.New();
            renderer.RemoveActor(actor);
            actor.SetMapper(mapper);

            renderer.SetBackground(0.1, 0.1, 0.1);

            renderer.AddActor(actor);


            //renderWindowInteractor.SetRenderWindow(vtkRenderWindow2.RenderWindow);
            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();
            // 启动交互器的事件循环
            //renderWindowInteractor.Start();
        }

        private double Percentile(List<ushort> sortedList, double percentile)
        {
            sortedList.Sort();  // 排序数据
            int N = sortedList.Count;
            double n = (N - 1) * percentile / 100.0 + 1;
            if (n == 1d) return sortedList[0];
            else if (n == N) return sortedList[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sortedList[k - 1] + d * (sortedList[k] - sortedList[k - 1]);
            }
        }


        public void CreatePointCloudFromTiffFast_sphere_alpha(Mat mat)
        {
            if (mat.Empty())
                return;
            if (mat.Type() != MatType.CV_16SC1)
                return;

            // 预处理
            Cv2.MinMaxLoc(mat, out double min, out double max);
            var binary = new Mat();
            var nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            double scale = 10.0;
            short minVal = (short)((min + 0.1) / scale);
            short maxVal = (short)(max / scale);

            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps(); // 清除所有旧actor，保证只显示这次的

            // 只显示前100个点，太多会很慢
            int testPointCount = Math.Min(10000, nonZeroLocations.Rows);

            for (int i = 0; i < testPointCount; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = point.X;
                int y = point.Y;
                short z = (short)(mat.At<short>(y, x) / scale);

                double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                // 颜色计算同原来
                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                // 透明度可以和深度相关，也可以都给 0.3 或 1.0
                /*                double alpha = 0.1 + 0.7 * normalizedValue; // 0.3~1.0*/
                double alpha = 0.5;

                // === 关键：每个点变成小球，独立actor ===
                var sphere = vtkSphereSource.New();
                sphere.SetCenter(x, y, z);
                sphere.SetRadius(0.5); // 试试 2~8，看你需求
                var mapper = vtkPolyDataMapper.New();
                mapper.SetInputConnection(sphere.GetOutputPort());
                var sphereActor = vtkActor.New();
                sphereActor.SetMapper(mapper);
                sphereActor.GetProperty().SetColor(r / 255.0, g / 255.0, b / 255.0);
                sphereActor.GetProperty().SetOpacity(alpha); // 0=全透明, 1=不透明

                renderer.AddActor(sphereActor);
            }

            // 渲染相关设置
            renderer.SetBackground(0.1, 0.1, 0.1);
            renderer.UseDepthPeelingOn(); // 强化透明度混合
            renderer.SetUseDepthPeeling(1);
            renderer.SetMaximumNumberOfPeels(100);
            renderer.SetOcclusionRatio(0.1);

            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);

            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();
        }


        public void CreatePointCloudFromTiffFast_with_oriimg_woalpha(Mat mat)
            {
                if (mat.Empty() || mat.Type() != MatType.CV_16SC1)
                    return;
                double min, max;
                Cv2.MinMaxLoc(mat, out min, out max);
                // --- Step 1. 原图（灰度）底面 ---
                int width = mat.Width, height = mat.Height;
            /*                Mat mat8 = new Mat();
                            double min, max;
                            Cv2.MinMaxLoc(mat, out min, out max);
                            mat.ConvertTo(mat8, MatType.CV_8UC1, 255.0 / (max - min + 1e-6), -min * 255.0 / (max - min + 1e-6));*/
            // 假设 mat 是 CV_16U 或 CV_16S 单通道图像
            /*            Mat mask = mat != 0; // 生成掩码，非零处为255*/
                Mat mask = new Mat();
                Cv2.Threshold(mat, mask, 0, 255, ThresholdTypes.Binary);
/*            Mat mask = new Mat();
                Cv2.Compare(mat, 0, mask, CmpType.Neq);*/
                // 转成8位单通道以防万一
                if (mask.Type() != MatType.CV_8UC1)
                {
                    Mat tmp = new Mat();
                    mask.ConvertTo(tmp, MatType.CV_8UC1);
                    mask = tmp;
                }

            // 仅对非零像素计算最小值和最大值
                double minVal1, maxVal1;
                Cv2.MinMaxLoc(mat, out minVal1, out maxVal1, out _, out _, mask);

                // 归一化（只对非零区域有效），避免除零
                Mat matNorm = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.All(0)); // 初始化为0

                for (int y = 0; y < mat.Rows; y++)
                {
                    for (int x = 0; x < mat.Cols; x++)
                    {
                        short v = mat.At<short>(y, x);  // 16位可根据原始类型替换
                        if (v != 0)
                        {
                            byte mapped = (byte)(((v - minVal1) / (maxVal1 - minVal1)) * 255.0);
                            matNorm.Set<byte>(y, x, mapped);
                        }
                        // v==0 保持为0
                    }
                }

// matNorm 即为只对非零做拉伸的8位图，零区为0
/*                // 1. 先把mat归一化到0~255
                Mat mat8 = new Mat();
                Cv2.Normalize(mat, mat8, 0, 255, NormTypes.MinMax, MatType.CV_8UC1);*/

                var imgData = vtkImageData.New();
                imgData.SetDimensions(width, height, 1);
                imgData.SetExtent(0, width - 1, 0, height - 1, 0, 0);
                imgData.SetScalarTypeToUnsignedChar();
                imgData.SetNumberOfScalarComponents(1);
                imgData.AllocateScalars();

                // 拷贝数据（假设mat8为C# OpenCvSharp的灰度Mat）
                byte[] raw = new byte[width * height];
                Marshal.Copy(matNorm.Data, raw, 0, raw.Length);
                Marshal.Copy(raw, 0, imgData.GetScalarPointer(), raw.Length);

                // 建立actor
                var imgActor = vtkImageActor.New();
                imgActor.SetInput(imgData);
                // --- Step 2. 点云 ---
                var binary = new Mat();
                var nonZeroLocations = new Mat();
                Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
                Cv2.FindNonZero(binary, nonZeroLocations);
                int index = 0, numPoints = nonZeroLocations.Rows;
                vtkPoints points = vtkPoints.New();
                vtkFloatArray floatArray = vtkFloatArray.New();
                floatArray.SetNumberOfComponents(3);
                floatArray.SetNumberOfTuples(numPoints);

                vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
                colors.SetNumberOfComponents(3);
                colors.SetName("Colors");

                double scale = 10.0;
                short minVal = (short)((min + 0.1) / scale);
                short maxVal = (short)(max / scale);

                for (int i = 0; i < nonZeroLocations.Rows; i++)
                {
                    OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                    int x = point.X;
                    int y = point.Y;
                    short z = (short)(mat.At<short>(y, x) / scale);
                    floatArray.SetTuple3(index, x, y, z);

                    double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);
                    byte r = 0, g = 0, b = 0;
                    if (normalizedValue < 0.33)
                    {
                        b = (byte)(255 * (normalizedValue / 0.33));
                    }
                    else if (normalizedValue < 0.66)
                    {
                        b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                        g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                    }
                    else
                    {
                        g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                        r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                    }
                    colors.InsertNextTuple3(r, g, b);
                    index++;
                }

                points.SetData(floatArray);

                vtkPolyData polyData = vtkPolyData.New();
                polyData.SetPoints(points);
                polyData.GetPointData().SetScalars(colors);

                vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
                glyphFilter.SetInput(polyData);

                vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
                mapper.SetInputConnection(glyphFilter.GetOutputPort());

                vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
                if (actor == null)
                    actor = vtkActor.New();
                renderer.RemoveActor(actor);
                actor.SetMapper(mapper);
                actor.GetProperty().SetPointSize(5);

                renderer.SetBackground(0.1, 0.1, 0.1);

                // --- Step 3. 渲染 ---
                renderer.RemoveAllViewProps(); // 移除旧actor，避免重复
                renderer.AddActor(imgActor);
                renderer.AddActor(actor);

                renderer.ResetCamera();
                vtkRenderWindow2.RenderWindow.Render();

                // 获取当前相机参数
                vtkCamera camera = renderer.GetActiveCamera();
                camparams.pos = camera.GetPosition();
                camparams.fp = camera.GetFocalPoint();
                camparams.up = camera.GetViewUp();
                camparams.angle = camera.GetViewAngle();
                camparams.clip = camera.GetClippingRange();
            }


        public void CreatePointCloudFromTiffFast_with_oriimg_with_alpha_z0(Mat mat)
        {
            if (mat.Empty() || mat.Type() != MatType.CV_16SC1)
                return;

            int width = mat.Width, height = mat.Height;
            double min, max;
            Cv2.MinMaxLoc(mat, out min, out max);

            // --- Step 1. 原图底面，归一化到8位，只对非0做stretch ---
            Mat mask = new Mat();
            Cv2.Threshold(mat, mask, 0, 255, ThresholdTypes.Binary);

            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }

            double minVal1, maxVal1;
            Cv2.MinMaxLoc(mat, out minVal1, out maxVal1, out _, out _, mask);

            Mat matNorm = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.All(0)); // 初始化为0

            for (int y = 0; y < mat.Rows; y++)
            {
                for (int x = 0; x < mat.Cols; x++)
                {
                    short v = mat.At<short>(y, x);
                    if (v != 0)
                    {
                        byte mapped = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        matNorm.Set<byte>(y, x, mapped);
                    }
                    // v==0 保持为0
                }
            }

            var imgData = vtkImageData.New();
            imgData.SetDimensions(width, height, 1);
            imgData.SetExtent(0, width - 1, 0, height - 1, 0, 0);
            imgData.SetScalarTypeToUnsignedChar();
            imgData.SetNumberOfScalarComponents(1);
            imgData.AllocateScalars();

            byte[] raw = new byte[width * height];
            Marshal.Copy(matNorm.Data, raw, 0, raw.Length);
            Marshal.Copy(raw, 0, imgData.GetScalarPointer(), raw.Length);

            var imgActor = vtkImageActor.New();
            imgActor.SetInput(imgData);
            // imgActor.GetProperty().SetOpacity(1.0); // 默认就是不透明

            // --- Step 2. 点云数据（带alpha） ---
            var binary = new Mat();
            var nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);
            floatArray.SetNumberOfTuples(numPoints);

            // 4通道支持alpha
            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(4);
            colors.SetName("Colors");

            double scale = 10.0;
            short minVal = (short)((min + 0.1) / scale);
            short maxVal = (short)(max / scale);

            for (int i = 0; i < numPoints; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = point.X;
                int y = point.Y;
                short z = (short)(mat.At<short>(y, x) / scale);
                floatArray.SetTuple3(i, x, y, z);

                double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                // 设置点云透明度（0-255）
                byte alpha = 10; // 你可以调整为80、100、150等，看实际叠加效果

                colors.InsertNextTuple4(r, g, b, alpha);
            }

            points.SetData(floatArray);
            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors);

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            if (actor == null) actor = vtkActor.New();
            actor.SetMapper(mapper);
            actor.GetProperty().SetPointSize(5);
/*            actor.GetProperty().SetOpacity(1.0); // 非常关键！*/
            actor.GetProperty().SetOpacity(0.5); // 点云很透明

            // --- Step 3. 渲染叠加 ---
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            renderer.AddActor(imgActor);
            renderer.AddActor(actor);

            renderer.SetBackground(0.1, 0.1, 0.1);

            // 开启alpha混合
            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);
            renderer.UseDepthPeelingOn();
            renderer.SetUseDepthPeeling(1);
            renderer.SetMaximumNumberOfPeels(100);
            renderer.SetOcclusionRatio(0.1);

            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();

            // 获取当前相机参数
            vtkCamera camera = renderer.GetActiveCamera();
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();
        }

        public void CreatePointCloudFromTiffFast_with_oriimg_with_alpha_zmin(Mat mat)
        {
            if (mat.Empty() || mat.Type() != MatType.CV_16SC1)
                return;

            int width = mat.Width, height = mat.Height;
            double min, max;
            Cv2.MinMaxLoc(mat, out min, out max);

            // --- Step 1. 原图底面，归一化到8位，只对非0做stretch ---
            Mat mask = new Mat();
            Cv2.Threshold(mat, mask, 0, 255, ThresholdTypes.Binary);

            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }

            double minVal1, maxVal1;
            Cv2.MinMaxLoc(mat, out minVal1, out maxVal1, out _, out _, mask);

            Mat matNorm = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.All(0)); // 初始化为0

            for (int y = 0; y < mat.Rows; y++)
            {
                for (int x = 0; x < mat.Cols; x++)
                {
                    short v = mat.At<short>(y, x);
                    if (v != 0)
                    {
                        byte mapped = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        matNorm.Set<byte>(y, x, mapped);
                    }
                    // v==0 保持为0
                }
            }

            var imgData = vtkImageData.New();
            imgData.SetDimensions(width, height, 1);
            imgData.SetExtent(0, width - 1, 0, height - 1, 0, 0);
            imgData.SetScalarTypeToUnsignedChar();
            imgData.SetNumberOfScalarComponents(1);
            imgData.AllocateScalars();

            byte[] raw = new byte[width * height];
            Marshal.Copy(matNorm.Data, raw, 0, raw.Length);
            Marshal.Copy(raw, 0, imgData.GetScalarPointer(), raw.Length);

            var imgActor = vtkImageActor.New();
            imgActor.SetInput(imgData);

            // --- Step 2. 点云数据（带alpha） ---
            var binary = new Mat();
            var nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);
            floatArray.SetNumberOfTuples(numPoints);

            // 4通道支持alpha
            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(4);
            colors.SetName("Colors");

            double scale = 10.0;
            short minVal = (short)((min + 0.1) / scale);
            short maxVal = (short)(max / scale);

            double minZ = double.MaxValue; // 用于原图抬高

            for (int i = 0; i < numPoints; i++)
            {
                OpenCvSharp.Point point = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = point.X;
                int y = point.Y;
                short z = (short)(mat.At<short>(y, x) / scale);
                floatArray.SetTuple3(i, x, y, z);

                if (z < minZ) minZ = z;

                double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                byte alpha = 30; // 点云透明度（可调），10-100越小越透明
                colors.InsertNextTuple4(r, g, b, alpha);
            }

            points.SetData(floatArray);
            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors);

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            if (actor == null) actor = vtkActor.New();
            actor.SetMapper(mapper);
/*            actor.GetProperty().SetPointSize(5);*/
            actor.GetProperty().SetOpacity(0.3); // 必须为1.0让每个点自己的alpha生效

            // 关键！把原图抬高到minZ
            imgActor.SetPosition(0, 0, minZ);

            // --- Step 3. 渲染叠加 ---
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            renderer.AddActor(imgActor);
            renderer.AddActor(actor);

            renderer.SetBackground(0.1, 0.1, 0.1);

            // Alpha混合设置，ActiViz 5.8大部分只需这些，深度剥离部分版本无效
            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);

            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();

            // 获取当前相机参数
            vtkCamera camera = renderer.GetActiveCamera();
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();
        }

        public void CreatePointCloudFromTiffFast_final(Mat mat)
        {
            if (mat.Empty() || mat.Type() != MatType.CV_16SC1)
                return;

            int width = mat.Width, height = mat.Height;
            double min, max;
            Cv2.MinMaxLoc(mat, out min, out max);

            // === Step 1. 生成带Alpha通道的图像（0值透明） ===
            double minVal1, maxVal1;
            Mat mask = new Mat();
            Cv2.Threshold(mat, mask, 0, 255, ThresholdTypes.Binary);

            if (mask.Type() != MatType.CV_8UC1)
            {
                Mat tmp = new Mat();
                mask.ConvertTo(tmp, MatType.CV_8UC1);
                mask = tmp;
            }
/*            if (mask.Type() != MatType.CV_8UC1)
                mask = mask.CvtColor(ColorConversionCodes.GRAY2BGR).CvtColor(ColorConversionCodes.BGR2GRAY);*/

            Cv2.MinMaxLoc(mat, out minVal1, out maxVal1, out _, out _, mask);

            Mat rgba = new Mat(mat.Size(), MatType.CV_8UC4, Scalar.All(0)); // BGRA
            for (int y = 0; y < mat.Rows; y++)
            {
                for (int x = 0; x < mat.Cols; x++)
                {
                    short v = mat.At<short>(y, x);
                    if (v > 0)
                    {
                        byte gray = (byte)(((v - minVal1) / (maxVal1 - minVal1 + 1e-6)) * 255.0);
                        rgba.Set<Vec4b>(y, x, new Vec4b(gray, gray, gray, 255)); // B,G,R,A
                    }
                    // 否则保持 (0,0,0,0)
                }
            }

            // === Step 2. 用 vtkImageImport 显示 RGBA 图像 ===
            vtkImageImport importer = vtkImageImport.New();
            byte[] imgRaw = new byte[width * height * 4];
            Marshal.Copy(rgba.Data, imgRaw, 0, imgRaw.Length);
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(imgRaw, 0);

            importer.CopyImportVoidPointer(ptr, imgRaw.Length);
            importer.SetDataScalarTypeToUnsignedChar();
            importer.SetNumberOfScalarComponents(4);
            importer.SetWholeExtent(0, width - 1, 0, height - 1, 0, 0);
            importer.SetDataExtentToWholeExtent();
            importer.Update();

            vtkImageActor imgActor = vtkImageActor.New();
            imgActor.SetInput(importer.GetOutput());
/*            imgActor.GetProperty().SetOpacity(1);*/

            // === Step 3. 点云处理 ===
            Mat binary = new Mat();
            Mat nonZeroLocations = new Mat();
            Cv2.Threshold(mat, binary, min + 0.1, max, ThresholdTypes.Tozero);
            Cv2.FindNonZero(binary, nonZeroLocations);

            int numPoints = nonZeroLocations.Rows;
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);
            floatArray.SetNumberOfTuples(numPoints);

            vtkUnsignedCharArray colors = vtkUnsignedCharArray.New();
            colors.SetNumberOfComponents(4);
            colors.SetName("Colors");

            double scale = 10.0;
            short minVal = (short)((min + 0.1) / scale);
            short maxVal = (short)(max / scale);
/*            double minZ = double.MaxValue;*/

            for (int i = 0; i < numPoints; i++)
            {
                var pt = nonZeroLocations.At<OpenCvSharp.Point>(i);
                int x = pt.X, y = pt.Y;
                short z = (short)(mat.At<short>(y, x) / scale);
                floatArray.SetTuple3(i, x, y, z);
                if (z < minZ) minZ = z;

                double normalizedValue = (double)(z - minVal) / (maxVal - minVal + 1e-6);

                byte r = 0, g = 0, b = 0;
                if (normalizedValue < 0.33)
                {
                    b = (byte)(255 * (normalizedValue / 0.33));
                }
                else if (normalizedValue < 0.66)
                {
                    b = (byte)(255 * (1 - (normalizedValue - 0.33) / 0.33));
                    g = (byte)(255 * ((normalizedValue - 0.33) / 0.33));
                }
                else
                {
                    g = (byte)(255 * (1 - (normalizedValue - 0.66) / 0.34));
                    r = (byte)(255 * ((normalizedValue - 0.66) / 0.34));
                }

                byte alpha = 30;
                colors.InsertNextTuple4(r, g, b, alpha);
            }

            points.SetData(floatArray);
            vtkPolyData polyData = vtkPolyData.New();
            polyData.SetPoints(points);
            polyData.GetPointData().SetScalars(colors);

            vtkVertexGlyphFilter glyphFilter = vtkVertexGlyphFilter.New();
            glyphFilter.SetInput(polyData);

            vtkPolyDataMapper mapper = vtkPolyDataMapper.New();
            mapper.SetInputConnection(glyphFilter.GetOutputPort());

            if (actor == null) actor = vtkActor.New();
            actor.SetMapper(mapper);
/*            actor.GetProperty().SetPointSize(5);*/
            actor.GetProperty().SetOpacity(0.3); // 每个点颜色决定透明度

            // 将原图抬升到 minZ 层
            imgActor.SetPosition(0, 0, minZ);

            // === Step 4. 渲染 ===
            vtkRenderer renderer = vtkRenderWindow2.RenderWindow.GetRenderers().GetFirstRenderer();
            renderer.RemoveAllViewProps();
            renderer.AddActor(imgActor);
            renderer.AddActor(actor);
            renderer.SetBackground(0.1, 0.1, 0.1);

            vtkRenderWindow2.RenderWindow.AlphaBitPlanesOn();
            vtkRenderWindow2.RenderWindow.SetMultiSamples(0);

            renderer.ResetCamera();
            vtkRenderWindow2.RenderWindow.Render();

            // 相机保存
            vtkCamera camera = renderer.GetActiveCamera();
            camparams.pos = camera.GetPosition();
            camparams.fp = camera.GetFocalPoint();
            camparams.up = camera.GetViewUp();
            camparams.angle = camera.GetViewAngle();
            camparams.clip = camera.GetClippingRange();
        }


    }


}