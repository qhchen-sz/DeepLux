using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using System.Windows.Input;
using static OpenCvSharp.CvDispObj;
using static OpenCvSharp.CvDrawObj;
using Cursor = System.Windows.Forms.Cursor;
using Cursors = System.Windows.Forms.Cursors;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace ImageControl
{
    [ToolboxItem(true)]
    public class CvControl : Control
    {
        public CvControl()
        {
            SetStyle(ControlStyles.ContainerControl
                   | ControlStyles.Selectable
                   | ControlStyles.SupportsTransparentBackColor
                   | ControlStyles.CacheText
                   | ControlStyles.UseTextForAccessibility, false);
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.Opaque
                | ControlStyles.ResizeRedraw
                | ControlStyles.FixedWidth
                | ControlStyles.FixedHeight
                | ControlStyles.StandardClick
                | ControlStyles.UserMouse
                | ControlStyles.StandardDoubleClick
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.EnableNotifyMessage
                | ControlStyles.DoubleBuffer
                | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = System.Drawing.Color.Black;
            DoubleBuffered = true;

            Point2f[] src = new Point2f[] { new Point2f(0f, 0f), new Point2f(512f, 0f), new Point2f(512f, 512f) };
            _imageMatrix = Cv2.GetAffineTransform(InputArray.Create(src), InputArray.Create(src));
        }

        #region 重写成员
#pragma warning disable 0067

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override System.Drawing.Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override Cursor Cursor { get => base.Cursor; set => base.Cursor = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override bool AllowDrop { get => base.AllowDrop; set => base.AllowDrop = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override System.Drawing.Point AutoScrollOffset { get => base.AutoScrollOffset; set => base.AutoScrollOffset = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override System.Windows.Forms.Layout.LayoutEngine LayoutEngine => base.LayoutEngine;
        [DefaultValue(typeof(System.Drawing.Color), "Black")]
        public override System.Drawing.Color BackColor { get => base.BackColor; set => base.BackColor = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override System.Drawing.Image BackgroundImage { get => base.BackgroundImage; set => base.BackgroundImage = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override ImageLayout BackgroundImageLayout { get => base.BackgroundImageLayout; set => base.BackgroundImageLayout = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new ControlCollection Controls { get => base.Controls; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override RightToLeft RightToLeft { get => base.RightToLeft; set => base.RightToLeft = value; }
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public override string Text { get => base.Text; set => base.Text = value; }
        protected override Padding DefaultPadding => Padding.Empty;
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size(512, 512);
        protected override Padding DefaultMargin => Padding.Empty;
        protected override Cursor DefaultCursor => Cursors.Arrow;

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler AutoSizeChanged;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler BackgroundImageChanged;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler BackgroundImageLayoutChanged;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler RightToLeftChanged;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler TextChanged;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event ControlEventHandler ControlAdded;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event ControlEventHandler ControlRemoved;
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler ForeColorChanged;
#pragma warning restore 0067

        #endregion

        private Mat _image;
        private System.Drawing.Image _imageGdi;
        private MatType _imageType = MatType.CV_8UC1;
        private OpenCvSharp.Size _imageSize = new OpenCvSharp.Size(512, 512);
        private Mat _imageMatrix;   // _imageMatrix 将图像坐标转换为控件坐标

        private ICollection<CvDispObj> _dispObjs = new System.Collections.ObjectModel.Collection<CvDispObj>();
        private ICollection<CvDrawObj> _drawObjs = new System.Collections.ObjectModel.Collection<CvDrawObj>();

        private System.Drawing.Point _mouseDragPos = System.Drawing.Point.Empty;
        private string _mousePosPixel;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.PageUnit = System.Drawing.GraphicsUnit.Display;
            g.RenderingOrigin = System.Drawing.Point.Empty;

            if (_image.IsValid())
            {
                Point2f[] ps1 = new Point2f[] { new Point2f(0f, 0f), new Point2f(_imageSize.Width, _imageSize.Height) };
                List<Point2f> ps2 = new List<Point2f>(ps1.Length);
                Cv2.Transform(InputArray.Create(ps1), OutputArray.Create(ps2), _imageMatrix);
                g.DrawImage(_imageGdi, ps2[0].X, ps2[0].Y, ps2[1].X - ps2[0].X, ps2[1].Y - ps2[0].Y);
                if (!string.IsNullOrWhiteSpace(_mousePosPixel))
                {
                    var txtSize = g.MeasureString(_mousePosPixel, System.Drawing.SystemFonts.MessageBoxFont);
                    var txtRect = new System.Drawing.RectangleF(ClientSize.Width - txtSize.Width, ClientSize.Height - txtSize.Height, txtSize.Width, txtSize.Height);
                    g.FillRectangle(System.Drawing.Brushes.LightYellow, txtRect);
                    g.DrawString(_mousePosPixel, System.Drawing.SystemFonts.MessageBoxFont, System.Drawing.Brushes.Black, txtRect);
                    g.DrawRectangle(System.Drawing.Pens.Orange, txtRect.X, txtRect.Y, txtRect.Width, txtRect.Height);
                }
            }

            foreach (CvDispObj obj in _dispObjs)
            {
                obj.OnPaint(g, _imageMatrix);
            }
            foreach (CvDispObj obj in _drawObjs)
            {
                obj.OnPaint(g, _imageMatrix);
            }
            base.OnPaint(e);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            FitImage();
            base.OnSizeChanged(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            bool repaint = false;
            bool handled = false;
            double imgX, imgY;
            ToImagePos(e.X, e.Y, out imgX, out imgY);
            //int imgRow = Convert.ToInt32(Math.Round(imgY - 0.5, MidpointRounding.AwayFromZero));
            //int imgCol = Convert.ToInt32(Math.Round(imgX - 0.5, MidpointRounding.AwayFromZero));
            //bool hasImage = _image.IsValid();
            //bool captureImage = imgRow >= 0 && imgCol >= 0 && imgRow < _imageSize.Height && imgCol < _imageSize.Width;
            if (e.Button == MouseButtons.Left)
            {
                if (ModifierKeys == Keys.Control)
                {
                    _mouseDragPos = e.Location;
                    handled = true;
                }
                if (!handled)
                {
                    foreach (CvDrawObj obj in _drawObjs)
                    {
                        obj.OnMouseDown(e.X, e.Y, imgX, imgY, e.Button, ref repaint, ref handled);
                        if (handled)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                if (!handled)
                {
                    foreach (CvDrawObj obj in _drawObjs)
                    {
                        obj.OnMouseDown(e.X, e.Y, imgX, imgY, e.Button, ref repaint, ref handled);
                        if (handled)
                        {
                            break;
                        }
                    }
                }
            }

            base.OnMouseDown(e);
            if (repaint)
            {
                Invalidate();
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool repaint = true;
            bool handled = false;
            _mousePosPixel = string.Empty;
            double imgX, imgY;
            ToImagePos(e.X, e.Y, out imgX, out imgY);
            int imgRow = Convert.ToInt32(Math.Round(imgY - 0.5, MidpointRounding.AwayFromZero));
            int imgCol = Convert.ToInt32(Math.Round(imgX - 0.5, MidpointRounding.AwayFromZero));
            bool hasImage = _image.IsValid();
            bool captureImage = imgRow >= 0 && imgCol >= 0 && imgRow < _imageSize.Height && imgCol < _imageSize.Width;
            if (e.Button == MouseButtons.Left)
            {
                if (hasImage && captureImage && ModifierKeys == Keys.Control)
                {
                    int moveX = e.X - _mouseDragPos.X;
                    int moveY = e.Y - _mouseDragPos.Y;
                    _mouseDragPos = e.Location;
                    _imageMatrix.At<double>(0, 2) += moveX;
                    _imageMatrix.At<double>(1, 2) += moveY;
                    handled = true;
                    repaint = true;
                }
                if (!handled)
                {
                    foreach (CvDrawObj obj in _drawObjs)
                    {
                        obj.OnMouseMove(e.X, e.Y, imgX, imgY, ref repaint, ref handled);
                        if (handled)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                if (hasImage && captureImage && ModifierKeys == Keys.Control)
                {
                    int moveX = e.X - _mouseDragPos.X;
                    int moveY = e.Y - _mouseDragPos.Y;
                    _mouseDragPos = e.Location;
                    _imageMatrix.At<double>(0, 2) += moveX;
                    _imageMatrix.At<double>(1, 2) += moveY;
                    handled = true;
                    repaint = true;
                }
                if (!handled)
                {
                    foreach (CvDrawObj obj in _drawObjs)
                    {
                        if (obj is CvDrawPolygon)
                        {
                            handled = true;
                            obj.OnMouseMove(e.X, e.Y, imgX, imgY, ref repaint, ref handled);
                            if (handled)
                            {
                                break;
                            }
                        }

                    }
                }
            }
            if (!handled)
            {
                if (hasImage && captureImage)
                {
                    switch (_imageType.Channels)
                    {
                        case 1:
                            _mousePosPixel = string.Format("{0}, {1} ({2})", imgRow, imgCol, _image.At<byte>(imgRow, imgCol));
                            break;
                        case 3:
                            Vec3b vec3b = _image.At<Vec3b>(imgRow, imgCol);
                            _mousePosPixel = string.Format("{0}, {1} ({2}, {3}, {4})", imgRow, imgCol, vec3b[0], vec3b[1], vec3b[2]);
                            break;
                        case 4:
                            Vec4b vec4b = _image.At<Vec4b>(imgRow, imgCol);
                            _mousePosPixel = string.Format("{0}, {1} ({2}, {3}, {4}, {5})", imgRow, imgCol, vec4b[0], vec4b[1], vec4b[2], vec4b[3]);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    _mousePosPixel = string.Format("{0}, {1}", imgRow, imgCol);
                }
                //repaint = true;
                //handled = true;
            }
            base.OnMouseMove(e);
            if (repaint)
            {
                Invalidate();
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool repaint = false;
            if (e.Button == MouseButtons.Left)
            {
                bool handled = false;
                double imgX, imgY;
                ToImagePos(e.X, e.Y, out imgX, out imgY);
                foreach (CvDrawObj obj in _drawObjs)
                {
                    obj.OnMouseUp(e.X, e.Y, imgX, imgY, ref repaint, ref handled);
                    if (handled)
                    {
                        break;
                    }
                }
            }
            base.OnMouseUp(e);
            if (repaint)
            {
                Invalidate();
            }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                FitImage();
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_image.IsValid())
            {
                Point2d[] imgPos = new Point2d[] { new Point2d(0d, 0d), new Point2d(_imageSize.Width, _imageSize.Height) };
                Point2d[] ctlPos;
                ToControlPos(imgPos, out ctlPos);

                double zoom = e.Delta > 0 ? 1.1 : 0.9;
                double w = ctlPos[1].X - ctlPos[0].X;
                double h = ctlPos[1].Y - ctlPos[0].Y;
                double x = ctlPos[0].X - (e.X - ctlPos[0].X) / w * (w * zoom - w);
                double y = ctlPos[0].Y - (e.Y - ctlPos[0].Y) / h * (h * zoom - h);
                w *= zoom;
                h *= zoom;
                if (w > ClientSize.Width / 10 && h > ClientSize.Height / 10 && w / 1000d < ClientSize.Width && h / 1000d < ClientSize.Height)
                {
                    Point2f[] srcPs = new Point2f[3] { new Point2f(0f, 0f), new Point2f(_imageSize.Width, 0f), new Point2f(_imageSize.Width, _imageSize.Height) };
                    Point2f[] dstPs = new Point2f[3] { new Point2f((float)x, (float)y), new Point2f((float)(x + w), (float)y), new Point2f((float)(x + w), (float)(y + h)) };
                    _imageMatrix.IfDispose();
                    _imageMatrix = Cv2.GetAffineTransform(InputArray.Create(srcPs), InputArray.Create(dstPs));
                    Invalidate();
                }
            }
        }


        public void DispImage(Mat image)
        {
            if (_image.IsValid())
            {
                _image.Dispose();
                _imageGdi.Dispose();
            }
            if (image.IsValid())
            {
                MatType imgType = image.Type();
                if (imgType != _imageType)
                {
                    if (imgType != MatType.CV_8UC1 && imgType != MatType.CV_8UC3 && imgType != MatType.CV_8UC4)
                    {
                        throw new ArgumentException("image.Type() != MatType.CV_8UC1 && image.Type() != MatType.CV_8UC3 && image.Type() != MatType.CV_8UC4.", nameof(image));
                    }
                    _imageType = imgType;
                }
                OpenCvSharp.Size imgSize = image.Size();
                if (imgSize != _imageSize)
                {
                    _imageSize = imgSize;
                    FitImageCore();
                }
                _image = image.Clone();
                _imageGdi = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            }
            Invalidate();
        }
        public void DispObj(params CvDispObj[] objs)
        {
            if (objs != null && objs.Length > 0)
            {
                foreach (CvDispObj obj in objs)
                {
                    if (obj != null)
                    {
                        _dispObjs.Add(obj);
                    }
                }
                Invalidate();
            }
        }
        public void DispObj(IEnumerable<CvDispObj> objs)
        {
            if (objs != null)
            {
                foreach (CvDispObj obj in objs)
                {
                    if (obj != null)
                    {
                        _dispObjs.Add(obj);
                    }
                }
                Invalidate();
            }
        }
        public void DrawObj(CvDrawObj obj, bool clear = false)
        {
            if (clear)
            {
                _drawObjs.Clear();
            }
            _drawObjs.Add(obj);
            Invalidate();
        }
        public void DispClear(bool keepImage,bool keepDraw)
        {
            if(!keepDraw)
                _drawObjs.Clear();
            if (_dispObjs.Count > 0)
            {
                _dispObjs.Clear();
            }
            if (!keepImage && _image.IsValid())
            {
                _image.Dispose();
                _imageGdi.Dispose();
            }
            Invalidate();
        }
        public void FitImage()
        {
            FitImageCore();
            Invalidate();
        }
        private void FitImageCore()
        {
            var winSize = ClientSize;
            var imgSize = _imageSize;
            var wR = (float)winSize.Width / imgSize.Width;
            var hR = (float)winSize.Height / imgSize.Height;

            Point2f[] srcPs = new Point2f[3] { new Point2f(0f, 0f), new Point2f(imgSize.Width, 0f), new Point2f(imgSize.Width, imgSize.Height) };

            float x, y, w, h;
            if (wR > hR)
            {
                w = imgSize.Width * hR;
                h = imgSize.Height * hR;
                y = 0f;
                x = (winSize.Width - w) / 2f;
            }
            else
            {
                w = imgSize.Width * wR;
                h = imgSize.Height * wR;
                x = 0f;
                y = (winSize.Height - h) / 2f;
            }
            Point2f[] dstPs = new Point2f[3] { new Point2f(x, y), new Point2f(x + w, y), new Point2f(x + w, y + h) };
            _imageMatrix.IfDispose();
            _imageMatrix = Cv2.GetAffineTransform(InputArray.Create(srcPs), InputArray.Create(dstPs));
        }
        public void ToControlPos(double imgX, double imgY, out double ctlX, out double ctlY)
        {
            Point2d[] ps1 = new Point2d[] { new Point2d(imgX, imgY) };
            List<Point2d> ps2 = new List<Point2d>(ps1.Length);
            Cv2.Transform(InputArray.Create(ps1), OutputArray.Create(ps2), _imageMatrix);
            ctlX = ps2[0].X;
            ctlY = ps2[0].Y;
        }
        public void ToControlPos(Point2d[] imgPos, out Point2d[] ctlPos)
        {
            List<Point2d> ps2 = new List<Point2d>(imgPos.Length);
            Cv2.Transform(InputArray.Create(imgPos), OutputArray.Create(ps2), _imageMatrix);
            ctlPos = ps2.ToArray();
        }
        public void ToImagePos(double ctlX, double ctlY, out double imgX, out double imgY)
        {
            Point2d[] ps1 = new Point2d[] { new Point2d(ctlX, ctlY) };
            List<Point2d> ps2 = new List<Point2d>(ps1.Length);
            using (Mat ctlMatrix = _imageMatrix.InvertAffineTransform())
            {
                Cv2.Transform(InputArray.Create(ps1), OutputArray.Create(ps2), ctlMatrix);
            }
            imgX = ps2[0].X;
            imgY = ps2[0].Y;
        }
        public void ToImagePos(Point2d[] ctlPos, out Point2d[] imgPos)
        {
            List<Point2d> ps2 = new List<Point2d>(ctlPos.Length);
            using (Mat ctlMatrix = _imageMatrix.InvertAffineTransform())
            {
                Cv2.Transform(InputArray.Create(ctlPos), OutputArray.Create(ps2), ctlMatrix);
            }
            imgPos = ps2.ToArray();
        }

        public void SaveRenderedImage(string filePath)
        {
            if (_image == null || !_image.IsValid())
            {
                return;
            }

            // 创建一个与控件大小相同的位图
            using (var bitmap = new Bitmap(_imageSize.Width, _imageSize.Height))
            using (var g = Graphics.FromImage(bitmap))
            {
                // 设置高质量渲染
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // 绘制原始图像
                g.DrawImage(_imageGdi, 0, 0, _imageSize.Width, _imageSize.Height);

                // 应用图像矩阵变换
                // 使用较小的缩放因子来统一缩放
                float uniformScale = Math.Min((float)_image.Width / Width, (float)_image.Height / Height);

                g.Transform = new Matrix(
                    uniformScale,  // 水平缩放
                    0,             // 水平对垂直轴的倾斜（剪切），通常为0
                    0,             // 垂直对水平轴的倾斜（剪切），通常为0
                    uniformScale,  // 垂直缩放
                    0,             // 水平平移
                    0              // 垂直平移
                );

                // 绘制显示对象
                foreach (var obj in _dispObjs)
                {

                    if(obj is CvDispObj.CvDispText)
                    {
                        var temp = obj as CvDispObj.CvDispText;
                        CvPoint cvPoint = new CvPoint(temp.Pos.X, temp.Pos.Y);
                        temp.Pos = new CvPoint(temp.Pos.X * (float)_image.Width / Width, temp.Pos.Y * (float)_image.Height / Height);
                        obj.OnPaint(g, _imageMatrix);
                        temp.Pos = cvPoint;
                    }
                    
                }

                // 绘制绘图对象
                foreach (var obj in _drawObjs)
                {
                    obj.OnPaint(g, _imageMatrix);
                }

                // 重置变换
                g.ResetTransform();

                // 保存到文件
                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        public void SaveImage(string filePath)
        {
            if (_image != null )
            {
                _image.SaveImage(filePath);
                return;
            }
        }
    }
}
