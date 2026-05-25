 using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VM.Halcon;
using VM.Halcon.Config;
using HV.Common.Enums;
using HV.ViewModels.Dock;

namespace HV.Views.Dock
{
    /// <summary>
    /// DataView.xaml 的交互逻辑
    /// </summary>
    public partial class VisionView : System.Windows.Controls.UserControl
    {
        #region Singleton
        private static readonly VisionView _instance = new VisionView();
        private VisionView()
        {
            InitializeComponent();
            this.DataContext = VisionViewModel.Ins;
            // this.KeyDown += VisionView_KeyDown;
            // this.Focusable = true;
            for (int i = 1; i <= 16; i++)
            {
                GetImageBox(i);
            }
            ShowCanvasAll();
        }

        public static VisionView Ins
        {
            get { return _instance; }
        }
        #endregion
        #region Prop
        private eViewMode _ViewMode = eViewMode.One;
        public eViewMode ViewMode
        {
            get { return _ViewMode; }
            set
            {
                _ViewMode = value;
                ShowCanvasAll();
            }
        }
        private bool _isFullScreen = false;
        private int _fullScreenWindowIndex = -1;
        // 全屏时被移到 gridwindow 的原始 host（WinForms child 始终不动）
        private WindowsFormsHost _fullscreenHost;
        // 退出全屏时恢复 grid 布局所需的快照
        private int _savedGridRow, _savedGridColumn, _savedGridRowSpan, _savedGridColumnSpan;
        private Thickness _savedHostMargin;
        private bool _isProcessingDoubleClick = false;

        #endregion
        #region Method
        public void Show(RImage rImage)
        {
            if (rImage.DispViewID > 0 && rImage.DispViewID < 17)
            {
                ViewDic.mViewDic[rImage.DispViewID].ShowHImage(rImage);
            }
        }
        public VMHWindowControl GetImageBox(int key)
        {
            if (!ViewDic.mViewDic.ContainsKey(key))
            {
                VMHWindowControl mWindowH = new VMHWindowControl();
                mWindowH.BackgroundImageLayout = ImageLayout.Center;
                mWindowH.WindowIndex = key;
                ViewDic.mViewDic.Add(key, mWindowH);
            }
            ViewDic.mViewDic[key].MyDoubleClick -= VisionView_MyDoubleClick;
            ViewDic.mViewDic[key].MyDoubleClick += VisionView_MyDoubleClick;
            return ViewDic.mViewDic[key];
        }

        private void VisionView_MyDoubleClick(object sender, EventArgs e)
        {
            // 防重复处理：如果正在处理中，直接返回
            if (_isProcessingDoubleClick) return;
            _isProcessingDoubleClick = true;

            try
            {
                if (sender is VMHWindowControl window)
                {
                    if (_isFullScreen)
                    {
                        ExitFullScreen();
                    }
                    else
                    {
                        EnterFullScreen(window.WindowIndex);
                    }
                }
            }
            finally
            {
                // 延迟重置标志，防止快速双击时被阻塞
                Task.Delay(200).ContinueWith(_ => _isProcessingDoubleClick = false);
            }
        }

        // private void VisionView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        // {
        //     ESC 退出全屏已禁用，改用双击退出
        //     if (e.Key == System.Windows.Input.Key.Escape && _isFullScreen)
        //     {
        //        ExitFullScreen();
        //     }
        // }

        /// <summary>
        /// 在 grid 的 Children 中查找包含指定 windowIndex 的 WindowsFormsHost
        /// </summary>
        private WindowsFormsHost FindHostInGrid(int windowIndex)
        {
            var targetControl = ViewDic.mViewDic[windowIndex];
            foreach (System.Windows.UIElement child in grid.Children)
            {
                if (child is WindowsFormsHost host && host.Child == targetControl)
                {
                    return host;
                }
            }
            return null;
        }

        /// <summary>
        /// 进入全屏放大模式
        /// Hidden 预布局 + 防呆定时器方案：
        /// 同步阶段仅做最轻量操作（移动 host、设尺寸、切换可见性），
        /// 不做 UpdateLayout/DoEvents，由 20ms 防呆定时器兜底纠正。
        /// </summary>
        private void EnterFullScreen(int windowIndex)
        {
            if (_isFullScreen || windowIndex < 1 || windowIndex > 16)
                return;

            _isFullScreen = true;
            _fullScreenWindowIndex = windowIndex;

            _fullscreenHost = FindHostInGrid(windowIndex);
            if (_fullscreenHost == null)
            {
                _isFullScreen = false;
                _fullScreenWindowIndex = -1;
                return;
            }

            // 保存 grid 布局快照
            _savedGridRow = Grid.GetRow(_fullscreenHost);
            _savedGridColumn = Grid.GetColumn(_fullscreenHost);
            _savedGridRowSpan = Grid.GetRowSpan(_fullscreenHost);
            _savedGridColumnSpan = Grid.GetColumnSpan(_fullscreenHost);
            _savedHostMargin = _fullscreenHost.Margin;

            // 从已稳定布局的 grid 读取目标尺寸
            int targetW = (int)grid.ActualWidth;
            int targetH = (int)grid.ActualHeight;

            var vmControl = ViewDic.mViewDic[windowIndex];
            vmControl.SuppressSizeChangedEvent();

            // 移动 host 到 gridwindow（Hidden 状态，参与布局但不渲染）
            grid.Children.Remove(_fullscreenHost);
            _fullscreenHost.Margin = new Thickness(0);
            Grid.SetRow(_fullscreenHost, 0);
            Grid.SetColumn(_fullscreenHost, 0);
            Grid.SetRowSpan(_fullscreenHost, 1);
            Grid.SetColumnSpan(_fullscreenHost, 1);
            gridwindow.Visibility = Visibility.Hidden;
            gridwindow.Children.Add(_fullscreenHost);

            // 直接设置尺寸和居中（不做 UpdateLayout/DoEvents，由定时器兜底）
            if (targetW > 0 && targetH > 0)
            {
                vmControl.hControl.Size = new System.Drawing.Size(targetW, targetH);
                vmControl.hControl.WindowSize = new System.Drawing.Size(targetW, targetH);
            }
            //vmControl.DispImageFitImage();

            // 居中完成后切换可见性，用户直接看到最终画面
            grid.Visibility = Visibility.Collapsed;
            gridwindow.Visibility = Visibility.Visible;

            // ★ 防呆定时器：20ms 后兜底纠正居中并恢复 SizeChanged
            int capturedIndex = windowIndex;
            var safetyTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            safetyTimer.Tick += (s, e) =>
            {
                safetyTimer.Stop();
                if (!_isFullScreen || _fullScreenWindowIndex != capturedIndex) return;
                var ctrl = ViewDic.mViewDic[capturedIndex];
                if (ctrl != null)
                {
                    ctrl.ResumeSizeChangedEvent();
                    ctrl.DispImageFitImage();
                }
            };
            safetyTimer.Start();
        }

        /// <summary>
        /// 检查 Halcon 控件是否已完成尺寸更新。
        /// WinForms 控件嵌在 Hidden 的 WPF 容器中时，显式设置的 WindowSize
        /// 可能尚未生效，通过对比实际 WindowSize 判断布局是否完成。
        /// </summary>
        private bool IsWindowSizeReady(VMHWindowControl vmControl, int expectedW, int expectedH)
        {
            return vmControl.hControl.WindowSize.Width == expectedW
                && vmControl.hControl.WindowSize.Height == expectedH;
        }

        /// <summary>
        /// 退出全屏放大模式
        /// Hidden 预布局 + 防呆定时器方案（与 EnterFullScreen 对称）：
        /// 同步阶段仅做最轻量操作（移动 host、恢复布局属性、切换可见性），
        /// 由防呆定时器兜底读取稳定后的 ActualWidth/Height 并居中。
        /// </summary>
        private void ExitFullScreen()
        {
            if (!_isFullScreen)
                return;

            int prevFullScreenIndex = _fullScreenWindowIndex;
            _isFullScreen = false;
            _fullScreenWindowIndex = -1;

            var capturedHost = _fullscreenHost;
            _fullscreenHost = null;

            if (capturedHost != null)
            {
                ViewDic.mViewDic[prevFullScreenIndex]?.SuppressSizeChangedEvent();

                // 把 host 从 gridwindow 移回 grid，恢复原始布局属性
                gridwindow.Children.Remove(capturedHost);
                capturedHost.Margin = _savedHostMargin;
                grid.Children.Add(capturedHost);
                Grid.SetRow(capturedHost, _savedGridRow);
                Grid.SetColumn(capturedHost, _savedGridColumn);
                Grid.SetRowSpan(capturedHost, _savedGridRowSpan);
                Grid.SetColumnSpan(capturedHost, _savedGridColumnSpan);
            }
            gridwindow.Visibility = Visibility.Collapsed;
            grid.Visibility = Visibility.Visible;

            // ★ 防呆定时器：50ms 后兜底读取稳定尺寸并居中（与 EnterFullScreen 策略对称）
            var safetyTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            safetyTimer.Tick += (s, e) =>
            {
                safetyTimer.Stop();
                var ctrl = ViewDic.mViewDic[prevFullScreenIndex];
                if (ctrl != null && capturedHost != null)
                {
                    int w = (int)capturedHost.ActualWidth;
                    int h = (int)capturedHost.ActualHeight;
                    if (w > 0 && h > 0)
                    {
                        ctrl.hControl.Size = new System.Drawing.Size(w, h);
                        ctrl.hControl.WindowSize = new System.Drawing.Size(w, h);
                    }
                    ctrl.ResumeSizeChangedEvent();
                    ctrl.DispImageFitImage();
                }
                else if (ctrl != null)
                {
                    // 极端情况：host 为 null，至少确保事件不被永久抑制
                    ctrl.ResumeSizeChangedEvent();
                }
            };
            safetyTimer.Start();
        }

        private void ShowCanvasAll()
        {
            RowDefinition row1 = new RowDefinition();
            RowDefinition row2 = new RowDefinition();
            RowDefinition row3 = new RowDefinition();
            RowDefinition row4 = new RowDefinition();
            ColumnDefinition col1 = new ColumnDefinition();
            ColumnDefinition col2 = new ColumnDefinition();
            ColumnDefinition col3 = new ColumnDefinition();
            ColumnDefinition col4 = new ColumnDefinition();
            ColumnDefinition col5 = new ColumnDefinition();
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();
            WindowsFormsHost[] windowsFormsHost = new WindowsFormsHost[16];
            for (int i = 0; i < 16; i++)
            {
                windowsFormsHost[i] = new WindowsFormsHost();
            }
            switch (_ViewMode)
            {
                case eViewMode.One:
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);
                    break;
                case eViewMode.Two:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    break;
                case eViewMode.Three:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);
                    Grid.SetRowSpan(windowsFormsHost[0], 2);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 1);
                    Grid.SetColumn(windowsFormsHost[2], 1);

                    break;
                case eViewMode.Four:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 1);
                    Grid.SetColumn(windowsFormsHost[2], 0);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 1);
                    Grid.SetColumn(windowsFormsHost[3], 1);

                    break;
                case eViewMode.Five:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);
                    Grid.SetColumnSpan(windowsFormsHost[0], 2);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 2);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 1);
                    Grid.SetColumn(windowsFormsHost[2], 0);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 1);
                    Grid.SetColumn(windowsFormsHost[3], 1);

                    windowsFormsHost[4].Margin = new Thickness(5);
                    windowsFormsHost[4].Child = GetImageBox(5);
                    grid.Children.Add(windowsFormsHost[4]);
                    Grid.SetRow(windowsFormsHost[4], 1);
                    Grid.SetColumn(windowsFormsHost[4], 2);
                    break;
                case eViewMode.Six:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 0);
                    Grid.SetColumn(windowsFormsHost[2], 2);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 1);
                    Grid.SetColumn(windowsFormsHost[3], 0);

                    windowsFormsHost[4].Margin = new Thickness(5);
                    windowsFormsHost[4].Child = GetImageBox(5);
                    grid.Children.Add(windowsFormsHost[4]);
                    Grid.SetRow(windowsFormsHost[4], 1);
                    Grid.SetColumn(windowsFormsHost[4], 1);

                    windowsFormsHost[5].Margin = new Thickness(5);
                    windowsFormsHost[5].Child = GetImageBox(6);
                    grid.Children.Add(windowsFormsHost[5]);
                    Grid.SetRow(windowsFormsHost[5], 1);
                    Grid.SetColumn(windowsFormsHost[5], 2);
                    break;
                case eViewMode.Seven:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);
                    Grid.SetColumnSpan(windowsFormsHost[0], 2);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 2);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 0);
                    Grid.SetColumn(windowsFormsHost[2], 3);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 1);
                    Grid.SetColumn(windowsFormsHost[3], 0);

                    windowsFormsHost[4].Margin = new Thickness(5);
                    windowsFormsHost[4].Child = GetImageBox(5);
                    grid.Children.Add(windowsFormsHost[4]);
                    Grid.SetRow(windowsFormsHost[4], 1);
                    Grid.SetColumn(windowsFormsHost[4], 1);

                    windowsFormsHost[5].Margin = new Thickness(5);
                    windowsFormsHost[5].Child = GetImageBox(6);
                    grid.Children.Add(windowsFormsHost[5]);
                    Grid.SetRow(windowsFormsHost[5], 1);
                    Grid.SetColumn(windowsFormsHost[5], 2);

                    windowsFormsHost[6].Margin = new Thickness(5);
                    windowsFormsHost[6].Child = GetImageBox(7);
                    grid.Children.Add(windowsFormsHost[6]);
                    Grid.SetRow(windowsFormsHost[6], 1);
                    Grid.SetColumn(windowsFormsHost[6], 3);
                    break;
                case eViewMode.Eight:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 0);
                    Grid.SetColumn(windowsFormsHost[2], 2);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 0);
                    Grid.SetColumn(windowsFormsHost[3], 3);

                    windowsFormsHost[4].Margin = new Thickness(5);
                    windowsFormsHost[4].Child = GetImageBox(5);
                    grid.Children.Add(windowsFormsHost[4]);
                    Grid.SetRow(windowsFormsHost[4], 1);
                    Grid.SetColumn(windowsFormsHost[4], 0);

                    windowsFormsHost[5].Margin = new Thickness(5);
                    windowsFormsHost[5].Child = GetImageBox(6);
                    grid.Children.Add(windowsFormsHost[5]);
                    Grid.SetRow(windowsFormsHost[5], 1);
                    Grid.SetColumn(windowsFormsHost[5], 1);

                    windowsFormsHost[6].Margin = new Thickness(5);
                    windowsFormsHost[6].Child = GetImageBox(7);
                    grid.Children.Add(windowsFormsHost[6]);
                    Grid.SetRow(windowsFormsHost[6], 1);
                    Grid.SetColumn(windowsFormsHost[6], 2);

                    windowsFormsHost[7].Margin = new Thickness(5);
                    windowsFormsHost[7].Child = GetImageBox(8);
                    grid.Children.Add(windowsFormsHost[7]);
                    Grid.SetRow(windowsFormsHost[7], 1);
                    Grid.SetColumn(windowsFormsHost[7], 3);
                    break;
                case eViewMode.Night:
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);
                    windowsFormsHost[0].Margin = new Thickness(5);
                    windowsFormsHost[0].Child = GetImageBox(1);
                    grid.Children.Add(windowsFormsHost[0]);
                    Grid.SetRow(windowsFormsHost[0], 0);
                    Grid.SetColumn(windowsFormsHost[0], 0);

                    windowsFormsHost[1].Margin = new Thickness(5);
                    windowsFormsHost[1].Child = GetImageBox(2);
                    grid.Children.Add(windowsFormsHost[1]);
                    Grid.SetRow(windowsFormsHost[1], 0);
                    Grid.SetColumn(windowsFormsHost[1], 1);

                    windowsFormsHost[2].Margin = new Thickness(5);
                    windowsFormsHost[2].Child = GetImageBox(3);
                    grid.Children.Add(windowsFormsHost[2]);
                    Grid.SetRow(windowsFormsHost[2], 0);
                    Grid.SetColumn(windowsFormsHost[2], 2);

                    windowsFormsHost[3].Margin = new Thickness(5);
                    windowsFormsHost[3].Child = GetImageBox(4);
                    grid.Children.Add(windowsFormsHost[3]);
                    Grid.SetRow(windowsFormsHost[3], 1);
                    Grid.SetColumn(windowsFormsHost[3], 0);

                    windowsFormsHost[4].Margin = new Thickness(5);
                    windowsFormsHost[4].Child = GetImageBox(5);
                    grid.Children.Add(windowsFormsHost[4]);
                    Grid.SetRow(windowsFormsHost[4], 1);
                    Grid.SetColumn(windowsFormsHost[4], 1);

                    windowsFormsHost[5].Margin = new Thickness(5);
                    windowsFormsHost[5].Child = GetImageBox(6);
                    grid.Children.Add(windowsFormsHost[5]);
                    Grid.SetRow(windowsFormsHost[5], 1);
                    Grid.SetColumn(windowsFormsHost[5], 2);

                    windowsFormsHost[6].Margin = new Thickness(5);
                    windowsFormsHost[6].Child = GetImageBox(7);
                    grid.Children.Add(windowsFormsHost[6]);
                    Grid.SetRow(windowsFormsHost[6], 2);
                    Grid.SetColumn(windowsFormsHost[6], 0);

                    windowsFormsHost[7].Margin = new Thickness(5);
                    windowsFormsHost[7].Child = GetImageBox(8);
                    grid.Children.Add(windowsFormsHost[7]);
                    Grid.SetRow(windowsFormsHost[7], 2);
                    Grid.SetColumn(windowsFormsHost[7], 1);

                    windowsFormsHost[8].Margin = new Thickness(5);
                    windowsFormsHost[8].Child = GetImageBox(9);
                    grid.Children.Add(windowsFormsHost[8]);
                    Grid.SetRow(windowsFormsHost[8], 2);
                    Grid.SetColumn(windowsFormsHost[8], 2);

                    break;
                case eViewMode.Ten:
                    // 2行 x 5列 = 10窗口
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.ColumnDefinitions.Add(col5);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    for (int i = 0; i < 10; i++)
                    {
                        windowsFormsHost[i].Margin = new Thickness(5);
                        windowsFormsHost[i].Child = GetImageBox(i + 1);
                        grid.Children.Add(windowsFormsHost[i]);
                        Grid.SetRow(windowsFormsHost[i], i / 5);
                        Grid.SetColumn(windowsFormsHost[i], i % 5);
                    }
                    break;
                case eViewMode.Twelve:
                    // 3行 x 4列 = 12窗口
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);
                    for (int i = 0; i < 12; i++)
                    {
                        windowsFormsHost[i].Margin = new Thickness(5);
                        windowsFormsHost[i].Child = GetImageBox(i + 1);
                        grid.Children.Add(windowsFormsHost[i]);
                        Grid.SetRow(windowsFormsHost[i], i / 4);
                        Grid.SetColumn(windowsFormsHost[i], i % 4);
                    }
                    break;
                case eViewMode.Fifteen:
                    // 3行 x 5列 = 15窗口
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.ColumnDefinitions.Add(col5);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);
                    for (int i = 0; i < 15; i++)
                    {
                        windowsFormsHost[i].Margin = new Thickness(5);
                        windowsFormsHost[i].Child = GetImageBox(i + 1);
                        grid.Children.Add(windowsFormsHost[i]);
                        Grid.SetRow(windowsFormsHost[i], i / 5);
                        Grid.SetColumn(windowsFormsHost[i], i % 5);
                    }
                    break;
                case eViewMode.Sixteen:
                    // 4行 x 4列 = 16窗口
                    grid.ColumnDefinitions.Add(col1);
                    grid.ColumnDefinitions.Add(col2);
                    grid.ColumnDefinitions.Add(col3);
                    grid.ColumnDefinitions.Add(col4);
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);
                    grid.RowDefinitions.Add(row3);
                    grid.RowDefinitions.Add(row4);
                    for (int i = 0; i < 16; i++)
                    {
                        windowsFormsHost[i].Margin = new Thickness(5);
                        windowsFormsHost[i].Child = GetImageBox(i + 1);
                        grid.Children.Add(windowsFormsHost[i]);
                        Grid.SetRow(windowsFormsHost[i], i / 4);
                        Grid.SetColumn(windowsFormsHost[i], i % 4);
                    }
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
    public class ViewDic
    {
        public static Dictionary<int, VMHWindowControl> mViewDic = new Dictionary<int, VMHWindowControl>();
        public static VMHWindowControl GetView(int key)
        {
            int actualKey = key + 1;
            if (!mViewDic.ContainsKey(actualKey))
                return null;
            return mViewDic[actualKey];
        }

    }

}
