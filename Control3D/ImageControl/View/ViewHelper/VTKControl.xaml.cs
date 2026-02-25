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
        private readonly double[] _zoomAxis = new double[3] { 0, 0, 1 }; // 记忆缩放轴
        private bool _zoomAxisValid = false;

        private double _signedDist = 0.0;   // 有符号距离：>0 在焦点这一侧，<0 穿透到另一侧

        //// ====== 新增：交互时临时把焦点“挪到点云中心”，交互结束再恢复为“飞行焦点”所需字段 ======
        //private bool _pivotMode = false;          // 是否已进入“交互pivot模式”
        //private double _flyDist = 0.0;            // 飞行模式下 pos->fp 的距离（用于恢复飞行焦点）
        private readonly double[] _pivotDelta = new double[3]; // 切换到pivot时的平移量（保证画面不跳）
        //显示左键旋转中心
        private vtkActor _pivotActor;
        private vtkSphereSource _pivotSphere;

        // ——新增：记录上次中心点——
        private bool _hasLastPivot = false;
        private readonly double[] _lastPivot = new double[3];

        // ——新增：阈值系数（越大越容易沿用上次中心点）——
        // 建议 0.01~0.05 之间，按场景调；这里给 0.02（包围盒对角线的 2%）
        private const double PivotReuseThresholdFactor = 0.05;

        private MouseDragPivotMessageFilter dragPivotFilter;


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

        // =========================================================
        // 方案A：交互开始/结束时，临时把焦点切到点云中心（pivot）
        // =========================================================
        private bool TryGetVisiblePointCloudCenterByFrustum(
        vtkRenderer renderer,
        vtkCamera camera,
        out double[] pivot,
        out double[] mergedBounds)
        {
            pivot = null;
            mergedBounds = null;

            if (renderer == null || camera == null) return false;

            // 计算 aspect（用于获取相机视锥平面）
            int[] size = renderer.GetSize(); // [w,h]
            double w = (size != null && size.Length > 0) ? size[0] : 1.0;
            double h = (size != null && size.Length > 1) ? size[1] : 1.0;
            if (h < 1.0) h = 1.0;
            double aspect = w / h;

            // 相机视锥平面（6个平面，共24个系数）
            double[] frustum = new double[24];
            var planes = vtkPlanes.New();
            //camera.GetFrustumPlanes(aspect, frustum);
            IntPtr pFrustum = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(double) * 24);
            try
            {
                camera.GetFrustumPlanes(aspect, pFrustum);
                planes.SetFrustumPlanes(pFrustum);
                System.Runtime.InteropServices.Marshal.Copy(pFrustum, frustum, 0, 24);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(pFrustum);
            }

            //var planes = vtkPlanes.New();
            //planes.SetFrustumPlanes(frustum);

            // 合并所有“视锥内点云”的 bounds
            bool hasValid = false;
            double xmin = double.PositiveInfinity, ymin = double.PositiveInfinity, zmin = double.PositiveInfinity;
            double xmax = double.NegativeInfinity, ymax = double.NegativeInfinity, zmax = double.NegativeInfinity;

            var actors = renderer.GetActors();
            actors.InitTraversal();

            vtkActor actor;
            while ((actor = actors.GetNextActor()) != null)
            {
                if (actor == null || actor.GetVisibility() == 0) continue;

                // 排除 pivot 小球，避免它干扰中心计算
                if (_pivotActor != null && actor == _pivotActor) continue;

                // 只处理点云 actor（最小改动做法：通过 mapper 输入是否为 polydata 来近似判断）
                // 如果你有自己的点云actor列表，建议在这里改成 IsPointCloudActor(actor)
                var mapper = actor.GetMapper();
                if (mapper == null) continue;

                vtkPolyData inputPd = null;
                try
                {
                    var pdMapper = mapper as vtkPolyDataMapper;
                    if (pdMapper != null)
                    {
                        pdMapper.Update();
                        inputPd = pdMapper.GetInput();
                    }
                }
                catch { }

                if (inputPd == null) continue;
                if (inputPd.GetNumberOfPoints() <= 0) continue;

                double[] actorVisibleBounds;
                if (!TryGetActorVisibleBoundsInFrustum(actor, inputPd, planes, out actorVisibleBounds))
                    continue;

                if (actorVisibleBounds == null || actorVisibleBounds.Length != 6) continue;
                if (actorVisibleBounds[1] < actorVisibleBounds[0] ||
                    actorVisibleBounds[3] < actorVisibleBounds[2] ||
                    actorVisibleBounds[5] < actorVisibleBounds[4])
                    continue;

                xmin = Math.Min(xmin, actorVisibleBounds[0]);
                xmax = Math.Max(xmax, actorVisibleBounds[1]);
                ymin = Math.Min(ymin, actorVisibleBounds[2]);
                ymax = Math.Max(ymax, actorVisibleBounds[3]);
                zmin = Math.Min(zmin, actorVisibleBounds[4]);
                zmax = Math.Max(zmax, actorVisibleBounds[5]);
                hasValid = true;
            }

            if (!hasValid) return false;

            mergedBounds = new double[] { xmin, xmax, ymin, ymax, zmin, zmax };
            pivot = new double[]
            {
                0.5 * (xmin + xmax),
                0.5 * (ymin + ymax),
                0.5 * (zmin + zmax)
            };

            return true;
        }
        private bool TryGetActorVisibleBoundsInFrustum(
        vtkActor actor,
        vtkPolyData inputPd,
        vtkPlanes frustumPlanes,
        out double[] visibleWorldBounds)
        {
            visibleWorldBounds = null;

            if (actor == null || inputPd == null || frustumPlanes == null) return false;
            if (inputPd.GetNumberOfPoints() <= 0) return false;

            // 1) 先把点云从 actor 局部坐标变换到世界坐标
            var transform = vtkTransform.New();
            transform.SetMatrix(actor.GetMatrix());

            var tf = vtkTransformPolyDataFilter.New();
            tf.SetTransform(transform);
            tf.SetInput(inputPd);
            tf.Update();

            var worldPd = tf.GetOutput();
            if (worldPd == null || worldPd.GetNumberOfPoints() <= 0) return false;

            // 2) 用相机视锥（vtkPlanes）过滤几何体
            var extract = vtkExtractGeometry.New();
            extract.SetImplicitFunction(frustumPlanes);
            extract.SetInput(worldPd);
            extract.ExtractInsideOn();
            extract.ExtractBoundaryCellsOn();
            extract.Update();

            var ug = extract.GetOutput(); // vtkUnstructuredGrid
            if (ug == null) return false;

            long nPts = 0;
            try { nPts = ug.GetNumberOfPoints(); } catch { }
            if (nPts <= 0) return false;

            double[] b = ug.GetBounds();
            if (b == null || b.Length != 6) return false;
            if (b[1] < b[0] || b[3] < b[2] || b[5] < b[4]) return false;

            visibleWorldBounds = new double[] { b[0], b[1], b[2], b[3], b[4], b[5] };
            return true;
        }

        private static double ScreenDistance(vtkRenderer renderer, double[] a, double[] b)
        {
            renderer.SetWorldPoint(a[0], a[1], a[2], 1.0);
            renderer.WorldToDisplay();
            double[] da = renderer.GetDisplayPoint();

            renderer.SetWorldPoint(b[0], b[1], b[2], 1.0);
            renderer.WorldToDisplay();
            double[] db = renderer.GetDisplayPoint();

            double dx = da[0] - db[0];
            double dy = da[1] - db[1];
            return Math.Sqrt(dx * dx + dy * dy);
        }
        // 给 MessageFilter 调用（public）
        public void BeginPivotForInteraction()
        {
            //if (_pivotMode) return;
            if (OwnerVTKControl == null) return;

            var rw = this.RenderWindow;
            var renderer = rw.GetRenderers().GetFirstRenderer();
            var camera = renderer.GetActiveCamera();

            double[] pos = camera.GetPosition();
            double[] fp = camera.GetFocalPoint();

            //// 记录飞行距离（pos->fp）
            //double dx = fp[0] - pos[0];
            //double dy = fp[1] - pos[1];
            //double dz = fp[2] - pos[2];
            //_flyDist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            //if (_flyDist < 1e-9) _flyDist = 1e-6;
            //// pivot：点云中心（用 actor bounds 中心）
            //// ====== 修正点：ComputeVisiblePropBounds 返回的是 bounds，不是中心点 ======
            //double[] b = renderer.ComputeVisiblePropBounds(); // [xmin,xmax,ymin,ymax,zmin,zmax]

            //// bounds 无效保护（VTK 常用无效值：xmax < xmin 或为空场景）
            //if (b == null || b.Length != 6 || b[1] < b[0] || b[3] < b[2] || b[5] < b[4])
            //    return;

            //double[] pivot = new double[]
            //{
            //    0.5 * (b[0] + b[1]),
            //    0.5 * (b[2] + b[3]),
            //    0.5 * (b[4] + b[5])
            //};
            //double[] pivot = camera.GetPosition();
            //double[] pivot = camera.GetFocalPoint();
            double[] b;
            double[] pivot;
            if (!TryGetVisiblePointCloudCenterByFrustum(renderer, camera, out pivot, out b))
            {
                if (_pivotActor != null) _pivotActor.VisibilityOff(); // 可选：隐藏
                return;
            }
            // 半径自适应：用包围盒对角线的 1%
            double ddx = b[1] - b[0], ddy = b[3] - b[2], ddz = b[5] - b[4];
            double diag = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
            if (diag < 1e-9) diag = 1.0;
            //// ====== 新增：若本次 pivot 与上次 pivot 距离很近，则沿用上次 pivot ======
            //// 阈值：包围盒对角线 * 系数（尺度自适应）
            //double reuseEps = Math.Max(1e-6, diag * PivotReuseThresholdFactor);
            //阈值：屏幕距离
            double reuseEps = 5; // ⭐ 推荐 2~5 像素

            if (_hasLastPivot)
            {
                //double pdx = pivot[0] - _lastPivot[0];
                //double pdy = pivot[1] - _lastPivot[1];
                //double pdz = pivot[2] - _lastPivot[2];
                //double dist = Math.Sqrt(pdx * pdx + pdy * pdy + pdz * pdz);
                double dist = ScreenDistance(renderer, pivot, _lastPivot);
                if (dist <= reuseEps)
                {
                    // 沿用上次中心点
                    pivot[0] = _lastPivot[0];
                    pivot[1] = _lastPivot[1];
                    pivot[2] = _lastPivot[2];
                }
                else
                {
                    // 更新上次中心点为本次
                    _lastPivot[0] = pivot[0];
                    _lastPivot[1] = pivot[1];
                    _lastPivot[2] = pivot[2];
                }
            }
            else
            {
                // 第一次：记录
                _lastPivot[0] = pivot[0];
                _lastPivot[1] = pivot[1];
                _lastPivot[2] = pivot[2];
                _hasLastPivot = true;
            }
            // === 显示 pivot 小球（最小改动）===
            if (_pivotActor == null)
            {
                _pivotSphere = vtkSphereSource.New();
                _pivotSphere.SetThetaResolution(16);
                _pivotSphere.SetPhiResolution(16);

                var mapper = vtkPolyDataMapper.New();
                mapper.SetInputConnection(_pivotSphere.GetOutputPort());

                _pivotActor = vtkActor.New();
                _pivotActor.SetMapper(mapper);
                _pivotActor.GetProperty().SetColor(1, 0, 0); // 红色
                _pivotActor.GetProperty().SetOpacity(0.8);
                renderer.AddActor(_pivotActor);
            }

            _pivotSphere.SetRadius(diag * 0.01);
            _pivotSphere.SetCenter(pivot[0], pivot[1], pivot[2]);
            _pivotSphere.Update();
            _pivotActor.VisibilityOn();

            //double[] pivot = renderer.ComputeVisiblePropBounds();

/*            // 为了切换不跳画面：pos 也跟着平移同样的 delta，保持 (fp - pos) 不变,伪功能
            _pivotDelta[0] = pivot[0] - fp[0];
            _pivotDelta[1] = pivot[1] - fp[1];
            _pivotDelta[2] = pivot[2] - fp[2];

            camera.SetPosition(pos[0] + _pivotDelta[0], pos[1] + _pivotDelta[1], pos[2] + _pivotDelta[2]);*/
            camera.SetFocalPoint(pivot[0], pivot[1], pivot[2]);
            //camera.OrthogonalizeViewUp();
            //renderer.ResetCameraClippingRange();
            //_pivotMode = true;
            renderer.GetRenderWindow().Render();
        }

        // 给 MessageFilter 调用（public）
        //public void EndPivotForInteraction()
        //{
        //    if (!_pivotMode) return;

        //    var rw = this.RenderWindow;
        //    var renderer = rw.GetRenderers().GetFirstRenderer();
        //    var camera = renderer.GetActiveCamera();

        //    double[] pos = camera.GetPosition();
        //    double[] fpPivot = camera.GetFocalPoint(); // 交互时的 pivot

        //    // 用当前视线方向（pos->pivot）恢复“飞行焦点”
        //    double vx = fpPivot[0] - pos[0];
        //    double vy = fpPivot[1] - pos[1];
        //    double vz = fpPivot[2] - pos[2];
        //    double len = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        //    if (len < 1e-9) len = 1e-6;

        //    vx /= len; vy /= len; vz /= len;

        //    double fpFlyX = pos[0] + vx * _flyDist;
        //    double fpFlyY = pos[1] + vy * _flyDist;
        //    double fpFlyZ = pos[2] + vz * _flyDist;

        //    camera.SetFocalPoint(fpFlyX, fpFlyY, fpFlyZ);
        //    camera.OrthogonalizeViewUp();

        //    _pivotMode = false;
        //    renderer.GetRenderWindow().Render();
        //}

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

            if (dragPivotFilter == null)
            {
                dragPivotFilter = new MouseDragPivotMessageFilter(this.Handle, this);
                System.Windows.Forms.Application.AddMessageFilter(dragPivotFilter);
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

        public void OnCustomMouseWheel5(MouseEventArgs e)
        {
            vtkRenderWindow rw = this.RenderWindow;
            vtkRenderer renderer = rw.GetRenderers().GetFirstRenderer();
            vtkCamera camera = renderer.GetActiveCamera();

            double[] pos = camera.GetPosition();
            double[] fp = camera.GetFocalPoint();

            // 当前相机->焦点向量
            double dx = fp[0] - pos[0];
            double dy = fp[1] - pos[1];
            double dz = fp[2] - pos[2];
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // ---- 1) 更新/初始化缩放轴（只要距离足够，就用当前方向刷新轴）----
            // 这样：你旋转相机后，再滚轮缩放，轴会跟着更新；但靠近焦点时不会抖
            const double epsDir = 1e-12;
            if (dist > epsDir)
            {
                _zoomAxis[0] = dx / dist;
                _zoomAxis[1] = dy / dist;
                _zoomAxis[2] = dz / dist;
                _zoomAxisValid = true;

                // 如果第一次滚轮，初始化 signedDist 为当前距离（正）
                if (_signedDist == 0.0) _signedDist = dist;
                else
                {
                    // 正常情况下保持 signedDist 的“符号”，但用当前绝对距离纠正一下幅度
                    _signedDist = Math.Sign(_signedDist) * dist;
                }
            }
            else
            {
                // dist 极小还没轴：退化为默认轴
                if (!_zoomAxisValid)
                {
                    _zoomAxis[0] = 0; _zoomAxis[1] = 0; _zoomAxis[2] = 1;
                    _zoomAxisValid = true;
                }

                // dist 太小：用 signedDist 的符号维持状态（避免突然跳轴）
                if (Math.Abs(_signedDist) < 1e-9)
                    _signedDist = 1e-6; // 给一点初始值
            }

            // ---- 2) 计算“加法步进”而不是比例步进：避免越近越卡 ----
            // 步长跟当前距离相关，同时设置一个最小步长
            // 你也可以把 minStep 做成和数据包围盒相关（下面有更高级做法）
            double absD = Math.Abs(_signedDist);
            double factor = 0.05;      // 每格滚轮走 5% 距离（可调 0.02~0.15）
/*            double[] b = new double[6];*/
/*            renderer.ComputeVisiblePropBounds(b);*/
            double[] b = renderer.ComputeVisiblePropBounds();
            double sx = b[1] - b[0];
            double sy = b[3] - b[2];
            double sz = b[5] - b[4];
            double diag = Math.Sqrt(sx * sx + sy * sy + sz * sz);

            // minStep 设为对角线的千分之一或万分之一（按手感调）
            double minStep = Math.Max(diag * 0.001, 1e-6);
            /*            double minStep = 0.05;     // 最小步长（按你的坐标尺度调：0.01~5 都可能）*/

            double step = Math.Max(absD * factor, minStep);

            // 鼠标上滚：放大（往里走） => signedDist 减小
            // 鼠标下滚：缩小（往外走） => signedDist 增加
            if (e.Delta > 0) _signedDist -= step;
            else _signedDist += step;

            // ---- 3) 允许穿透：当跨过 0 时，别卡在 0 上（避免 dist=0 引发数值问题）----
            double crossEps = 1e-6;
            if (Math.Abs(_signedDist) < crossEps)
                _signedDist = (e.Delta > 0) ? -crossEps : crossEps;

            // ---- 4) 用“有符号距离 + 固定轴”更新相机位置（焦点不变）----
            pos[0] = fp[0] - _zoomAxis[0] * _signedDist;
            pos[1] = fp[1] - _zoomAxis[1] * _signedDist;
            pos[2] = fp[2] - _zoomAxis[2] * _signedDist;

            camera.SetPosition(pos[0], pos[1], pos[2]);
            // camera.SetFocalPoint(fp[0], fp[1], fp[2]); // 你想写也可以，确保不被别处改了

            // ---- 5) 动态裁剪面：让 near/far 随距离走，避免深度精度崩 ----
            double d = Math.Max(Math.Abs(_signedDist), 1e-6);
            double near = Math.Max(d * 0.001, 1e-6);
            double far = Math.Max(d * 2000.0, near * 10.0);
            camera.SetClippingRange(near, far);

            renderer.GetRenderWindow().Render();
        }

    }
    public class MouseDragPivotMessageFilter : IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
 /*       private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;*/

        private readonly IntPtr targetHwnd;
        private readonly MyRenderWindowControl targetControl;

        public MouseDragPivotMessageFilter(IntPtr hwnd, MyRenderWindowControl control)
        {
            targetHwnd = hwnd;
            targetControl = control;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (targetControl == null) return false;
            if (!targetControl.Is3DVisibleChild || !targetControl.IsHandleCreated || !targetControl.Visible)
                return false;

            if (!IsHandleChildOf(m.HWnd, targetHwnd))
                return false;

            // 左键/中键按下：进入 pivot 模式
            //if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_MBUTTONDOWN)
            if (m.Msg == WM_LBUTTONDOWN)
            {
                // 用 BeginInvoke 避免在消息线程里直接改相机导致偶发冲突
                targetControl.Invoke((Action)(() =>
                {
                    targetControl.BeginPivotForInteraction();
                }));
            }

/*            // 左键/中键抬起：退出 pivot 模式
            if (m.Msg == WM_LBUTTONUP)
            //if (m.Msg == WM_LBUTTONUP || m.Msg == WM_MBUTTONUP)
            {
                targetControl.Invoke((Action)(() =>
                {
                    targetControl.EndPivotForInteraction();
                }));
            }*/

            return false; // 不拦截，让 VTK 正常交互
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        private bool IsHandleChildOf(IntPtr child, IntPtr parent)
        {
            while (child != IntPtr.Zero)
            {
                if (child == parent) return true;
                child = GetParent(child);
            }
            return false;
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
        // HSV 转 RGB（0..255）
        public (byte r, byte g, byte b) HsvToRgbByte(double h, double s, double v)
        {
            h = (h % 360 + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;

            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);
            return (r, g, b);
        }
        public void CreatePointCloudFromTiffFastHalcon_AutoScale(HObject ho_Image)
        {
            if (ho_Image == null || !ho_Image.IsInitialized())
                return;

            // -----------------------------
            // 1) 统一转成 real（double），避免依赖 int2 / ScaleImage(1000) / scaleZ(10)
            // -----------------------------
            HOperatorSet.ConvertImageType(ho_Image, out HObject imgReal, "real");

            // 取 min/max（用于阈值域和统计）
            HTuple min, max;
            HOperatorSet.MinMaxGray(imgReal, imgReal, 0, out min, out max, out _);

            // -----------------------------
            // 2) 有效区域：把“背景/无效值”排掉
            //    这里沿用你原思路：min 过小（例如有 -32768）时稍微抬高阈值下限
            //    注意：eps 用于避免 min==max 或浮点边界
            // -----------------------------
            double eps = 1e-9;
            HObject ho_Range1;

            if (min.D < -500.0)
                HOperatorSet.Threshold(imgReal, out ho_Range1, min.D + 1.0, max.D);
            else
                HOperatorSet.Threshold(imgReal, out ho_Range1, min.D + eps, max.D);

            // 获取有效点坐标
            HTuple rows, cols;
            HOperatorSet.GetRegionPoints(ho_Range1, out rows, out cols);

            int numPoints = rows.Length;
            if (numPoints <= 0)
                return;

            // 一次性取灰度（double）
            HTuple grayVals;
            HOperatorSet.GetGrayval(imgReal, rows, cols, out grayVals);

            // -----------------------------
            // 3) 用百分位裁剪（1%~99%）获取稳定的显示范围（避免极端值压扁整体）
            // -----------------------------
            HOperatorSet.TupleSort(grayVals, out HTuple sorted);
            int n = sorted.Length;
            if (n <= 1)
                return;

            int idxLow = (int)Math.Floor(n * 0.01);
            int idxHigh = (int)Math.Floor(n * 0.99);
            if (idxHigh <= idxLow) idxHigh = Math.Min(n - 1, idxLow + 1);

            double zLow = sorted[idxLow].D;
            double zHigh = sorted[idxHigh].D;

            if (Math.Abs(zHigh - zLow) < 1e-12)
                zHigh = zLow + 1.0; // 防止除0

            // -----------------------------
            // 4) 先统计 XY 包围盒（用于自动确定 Z 的显示范围）
            // -----------------------------
            float minXCam = float.MaxValue, maxXCam = float.MinValue;
            float minYCam = float.MaxValue, maxYCam = float.MinValue;

            for (int i = 0; i < numPoints; i++)
            {
                float x = (float)cols[i].D;
                float y = (float)rows[i].D;

                if (x < minXCam) minXCam = x;
                if (x > maxXCam) maxXCam = x;
                if (y < minYCam) minYCam = y;
                if (y > maxYCam) maxYCam = y;
            }

            float rangeX = maxXCam - minXCam;
            float rangeY = maxYCam - minYCam;

            // -----------------------------
            // 5) 自动 Z 尺度：让Z的显示范围与XY尺度成比例（可调参数）
            //    zRangeFactor 越大，Z起伏看起来越夸张；越小越“扁”
            // -----------------------------
            double zRangeFactor = 0.5; // 你可改：0.2~2.0 视效果而定
            double targetZRange = Math.Max(rangeX, rangeY) * zRangeFactor;
            if (targetZRange < 1e-6) targetZRange = 1.0;

            double zScaleAuto = targetZRange / (zHigh - zLow);
            double zOffset = 0.0; // 想让最低点为0就保持0；想居中可改为 -targetZRange/2 等

            // -----------------------------
            // 6) 256 色 LUT（更通用，不依赖 short 65536）
            // -----------------------------
            /*            byte[] lutR = new byte[256];
                        byte[] lutG = new byte[256];
                        byte[] lutB = new byte[256];

                        for (int k = 0; k < 256; k++)
                        {
                            double t = k / 255.0; // 0..1
                            byte r = 0, g = 0, b = 0;

                            if (t < 0.33)
                            {
                                b = (byte)(255 * (t / 0.33));
                            }
                            else if (t < 0.66)
                            {
                                b = (byte)(255 * (1 - (t - 0.33) / 0.33));
                                g = (byte)(255 * ((t - 0.33) / 0.33));
                            }
                            else
                            {
                                g = (byte)(255 * (1 - (t - 0.66) / 0.34));
                                r = (byte)(255 * ((t - 0.66) / 0.34));
                            }

                            lutR[k] = r;
                            lutG[k] = g;
                            lutB[k] = b;
                        }*/
            int N = 4096; // 256 -> 4096（越大越平滑）
            byte[] lutR = new byte[N];
            byte[] lutG = new byte[N];
            byte[] lutB = new byte[N];

            for (int i = 0; i < N; i++)
            {
                double t = i / (double)(N - 1);     // 0..1
                                                    // 蓝(240°) -> 红(0°)
                double hue = (1.0 - t) * 240.0;     // 240..0
                double s = 1.0;
                double v = 1.0;

                (byte r, byte g, byte b) = HsvToRgbByte(hue, s, v);

                lutR[i] = r;
                lutG[i] = g;
                lutB[i] = b;
            }


            // -----------------------------
            // 7) 生成点云数据 + 颜色（自动Z + 自动颜色）
            // -----------------------------
            float[] pointData = new float[numPoints * 3];
            byte[] colorData = new byte[numPoints * 3];

            float minZCam = float.MaxValue, maxZCam = float.MinValue;

            for (int i = 0; i < numPoints; i++)
            {
                float x = (float)cols[i].D;
                float y = (float)rows[i].D;

                double gRaw = grayVals[i].D;
                double gClamped = gRaw;
                if (gClamped < zLow) gClamped = zLow;
                if (gClamped > zHigh) gClamped = zHigh;

                float z = (float)((gClamped - zLow) * zScaleAuto + zOffset);

                if (z < minZCam) minZCam = z;
                if (z > maxZCam) maxZCam = z;

                int idx3 = i * 3;
                pointData[idx3] = x;
                pointData[idx3 + 1] = y;
                pointData[idx3 + 2] = z;

                /*                double norm = (gClamped - zLow) / (zHigh - zLow); // 0..1
                                int c = (int)Math.Round(norm * 255.0);
                                if (c < 0) c = 0;
                                if (c > 255) c = 255;

                                colorData[idx3] = lutR[c];
                                colorData[idx3 + 1] = lutG[c];
                                colorData[idx3 + 2] = lutB[c];*/
                double norm = (gClamped - zLow) / (zHigh - zLow);   // 0..1
                norm = Math.Max(0.0, Math.Min(1.0, norm));

                // 可选：加一点 gamma，让颜色分布更“柔和/均匀”
                double gamma = 1.0; // 0.8~1.4 自己试
                norm = Math.Pow(norm, gamma);

                int c = (int)Math.Round(norm * (N - 1));
                if (c < 0) c = 0;
                if (c >= N) c = N - 1;

                colorData[idx3] = lutR[c];
                colorData[idx3 + 1] = lutG[c];
                colorData[idx3 + 2] = lutB[c];
            }

            // -----------------------------
            // 8) 传给 VTK（保持你原来的“Pinned + SetArray”方式）
            //    注意：如果你遇到随机崩溃/花屏，说明 VTK 可能未拷贝数据，你需要改成拷贝/非托管分配方式。
            // -----------------------------
            vtkPoints points = vtkPoints.New();
            vtkFloatArray floatArray = vtkFloatArray.New();
            floatArray.SetNumberOfComponents(3);

            GCHandle handlePoints = GCHandle.Alloc(pointData, GCHandleType.Pinned);
            try
            {
                IntPtr ptrPoints = handlePoints.AddrOfPinnedObject();
                // save=1 表示 VTK 会接管并在合适时释放（不同包装行为可能不同）
                floatArray.SetArray(ptrPoints, numPoints * 3, 1);
            }
            finally
            {
                handlePoints.Free();
            }
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

            // -----------------------------
            // 9) 相机：沿用你的包围盒逻辑（现在Z是自动尺度后的世界坐标）
            // -----------------------------
            float centerX = (minXCam + maxXCam) / 2.0f;
            float centerY = (minYCam + maxYCam) / 2.0f;
            float centerZ = (minZCam + maxZCam) / 2.0f;

            float rangeZ = maxZCam - minZCam;
            float maxRange = Math.Max(rangeX, Math.Max(rangeY, rangeZ));
            if (maxRange < 1e-6f) maxRange = 1.0f;

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

            // -----------------------------
            // 10) 坐标轴（沿用你的逻辑）
            // -----------------------------
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