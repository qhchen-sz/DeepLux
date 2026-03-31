using Plugin.Blob.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VM.Halcon;
using HV.Common.Provide;
using HV.Core;
using VM.Halcon.Config;

namespace Plugin.Blob.Views
{
    /// <summary>
    /// SaveImageView.xaml 的交互逻辑
    /// </summary>
    public partial class PerProcessingView : ModuleViewBase
    {
        public PerProcessingView()
        {
            InitializeComponent();
        }

        private void ModuleViewBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (mWindowH == null)
            {
                mWindowH = new VMHWindowControl();
                winFormHost.Child = mWindowH;
            }
        }

        private void btnComp_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel1 = DataContext as BlobViewModel;
            if (viewModel1 == null) return;
            if (viewModel1.DispImage != null && viewModel1.DispImage.IsInitialized())
                mWindowH.Image = viewModel1.DispImage;
            mWindowH.DispObj(mWindowH.Image);
        }

        //private void btnComp_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    var viewModel1 = DataContext as BlobViewModel;
        //    if (viewModel1 == null) return;
        //    viewModel1.ExeModule();
        //    if (viewModel1.m_outImage != null && viewModel1.m_outImage.IsInitialized())
        //        mWindowH.Image = new RImage(viewModel1.m_outImage);
        //    mWindowH.DispObj(mWindowH.Image);
        //}

        // ========== 阈值键盘调整功能 ==========

        /// <summary>
        /// ToggleButton点击事件：切换调整最小值/最大值
        /// 注意：IsChecked已绑定到IsAdjustingMinThreshold，此处只处理焦点转移
        /// </summary>
        private void tblThreshold_Click(object sender, RoutedEventArgs e)
        {
            // 确保RangeSlider获得焦点，以便接收键盘事件
            rangeThreshold.Focus();
        }

        /// <summary>
        /// RangeSlider键盘事件处理：支持↑↓精细调整，Shift+↑↓快速调整
        /// </summary>
        private void rangeThreshold_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = DataContext as BlobViewModel;
            if (viewModel == null) return;

            // 根据当前选择的模式确定调整哪个阈值
            bool isAdjustingMin = viewModel.IsAdjustingMinThreshold;

            // 计算调整量：普通按键1，Shift按键10
            int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;

            switch (e.Key)
            {
                case Key.Up:
                case Key.PageUp:
                    // 增加阈值
                    if (isAdjustingMin)
                    {
                        int newValue = Math.Min(255, viewModel.ThresholdMin + step);
                        // 确保下限不超过上限
                        newValue = Math.Min(newValue, viewModel.ThresholdMax);
                        viewModel.ThresholdMin = newValue;
                    }
                    else
                    {
                        viewModel.ThresholdMax = Math.Min(255, viewModel.ThresholdMax + step);
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                case Key.PageDown:
                    // 减少阈值
                    if (isAdjustingMin)
                    {
                        int newValue = Math.Max(0, viewModel.ThresholdMin - step);
                        // 确保下限不超过上限
                        newValue = Math.Min(newValue, viewModel.ThresholdMax);
                        viewModel.ThresholdMin = newValue;
                    }
                    else
                    {
                        int newValue = Math.Max(0, viewModel.ThresholdMax - step);
                        // 确保上限不小于下限
                        newValue = Math.Max(newValue, viewModel.ThresholdMin);
                        viewModel.ThresholdMax = newValue;
                    }
                    e.Handled = true;
                    break;

                case Key.Home:
                    // 跳到最小值
                    if (isAdjustingMin)
                    {
                        viewModel.ThresholdMin = 0;
                    }
                    else
                    {
                        int newValue = Math.Max(0, viewModel.ThresholdMax);
                        newValue = Math.Max(newValue, viewModel.ThresholdMin);
                        viewModel.ThresholdMax = newValue;
                    }
                    e.Handled = true;
                    break;

                case Key.End:
                    // 跳到最大值
                    if (isAdjustingMin)
                    {
                        int newValue = Math.Min(255, viewModel.ThresholdMin);
                        newValue = Math.Min(newValue, viewModel.ThresholdMax);
                        viewModel.ThresholdMin = newValue;
                    }
                    else
                    {
                        viewModel.ThresholdMax = 255;
                    }
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Tab键切换调整目标（由于ToggleButton的IsChecked已绑定，此处只需处理焦点）
                    // 但仍需要切换模式以支持键盘操作
                    viewModel.IsAdjustingMinThreshold = !viewModel.IsAdjustingMinThreshold;
                    e.Handled = true;
                    break;

                default:
                    break;
            }
        }
    }

    // ========== Converter类 ==========

    /// <summary>
    /// 当调整下限时，下限值高亮，上限值不变
    /// </summary>
    public class BoolToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAdjustingMin)
            {
                // 当调整下限(True)时，下限值高亮显示
                return isAdjustingMin ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 当调整下限时，上限值保持灰色；调整上限时，上限值高亮
    /// </summary>
    public class BoolToForegroundConverterInverse : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAdjustingMin)
            {
                // 当调整下限(True)时，上限值保持灰色；调整上限(False)时，上限值高亮
                return isAdjustingMin ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.Red);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
