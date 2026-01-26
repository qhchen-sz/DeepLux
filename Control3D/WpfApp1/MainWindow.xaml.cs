using ImageControl;
using System;
using System.Collections.Generic;
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
using ICSharpCode.WpfDesign.Designer.OutlineView;
using ICSharpCode.WpfDesign.Designer.Services;
using System.Collections.ObjectModel;
using System.Drawing;
using Color = System.Drawing.Color;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Continuous_contour;
using VM.Halcon;

namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private ObservableCollection<MyItem> sourceItems;
        private ObservableCollection<MyItem> targetItems;

        public MainWindow()
        {
            InitializeComponent();
            
            //InitializeData();
        }

        //private void InitializeData()
        //{
        //    sourceItems = new ObservableCollection<MyItem>
        //{
        //    new MyItem { Name = "Item 1" },
        //    new MyItem { Name = "Item 2" },
        //    new MyItem { Name = "Item 3" }
        //};
        //    targetItems = new ObservableCollection<MyItem>();

        //    sourceListView.ItemsSource = sourceItems;
        //    targetListView.ItemsSource = targetItems;
        //}

        //private Point _startPoint;

        //private void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    _startPoint = e.GetPosition(null);
        //}

        //private void ListView_PreviewMouseMove(object sender, MouseEventArgs e)
        //{
        //    Point mousePos = e.GetPosition(null);
        //    Vector diff = _startPoint - mousePos;

        //    if (e.LeftButton == MouseButtonState.Pressed &&
        //        (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
        //         Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        //    {
        //        ListView listView = sender as ListView;
        //        ListViewItem listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
        //        if (listViewItem == null) return;

        //        MyItem item = (MyItem)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
        //        DataObject dragData = new DataObject("myFormat", item);
        //        DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
        //    }
        //}

        //private void ListView_DragEnter(object sender, DragEventArgs e)
        //{
        //    if (!e.Data.GetDataPresent("myFormat") || sender == e.Source)
        //    {
        //        e.Effects = DragDropEffects.None;
        //    }
        //}

        //private void ListView_Drop(object sender, DragEventArgs e)
        //{
        //    if (e.Data.GetDataPresent("myFormat"))
        //    {
        //        MyItem item = e.Data.GetData("myFormat") as MyItem;
        //        ListView listView = sender as ListView;
        //        ObservableCollection<MyItem> items = listView.ItemsSource as ObservableCollection<MyItem>;

        //        // 获取鼠标位置
        //        Point dropPosition = e.GetPosition(listView);

        //        // 找到最接近的项目索引
        //        int index = GetInsertionIndex(listView, dropPosition);

        //        if (item != null && items != null)
        //        {
        //            // 插入到计算出的索引位置
        //            items.Insert(index, item);

        //            // 如果是从另一个ListView拖来的，还需要从原ListView中移除
        //            //if (sourceItems.Contains(item))
        //            //{
        //            //    sourceItems.Remove(item);
        //            //}
        //        }
        //    }
        //}
        //private int GetInsertionIndex(ListView listView, Point position)
        //{
        //    //int index = 0;
        //    for (int i = 0; i < listView.Items.Count; i++)
        //    {
        //        ListViewItem item = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(i);
        //        if (item != null)
        //        {
        //            Point itemPosition = item.TransformToAncestor(listView).Transform(new Point(0, item.ActualHeight / 2));
        //            if (position.Y < itemPosition.Y)
        //            {
        //                return i;
        //            }
        //        }
        //    }
        //    // 如果没有找到合适的位置，返回列表末尾
        //    return listView.Items.Count;
        //}

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        //imagecontrol.DrawRet();
        //        //image.VtkRenderWindow_Load();
        //        //Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
        //        //openFileDialog.Filter = "(*.tif)|*.tif|All files(*.*)|*.*";
        //        //if ((bool)openFileDialog.ShowDialog())
        //        //{
        //        //    if (System.IO.File.Exists(openFileDialog.FileName))
        //        //    {
        //        //        image.CreatePointCloudFromTiffFast(openFileDialog.FileName);
        //        //    }
        //        //    else
        //        //    {
        //        //        throw new Exception("读取指定图片失败，图片路径不存在:" + openFileDialog.FileName);
        //        //    }

        //        //}

        //    }
        //    catch (Exception ee)
        //    {

        //        throw;
        //    }

        //}

        //void uxTreeView_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        //{
        //    PrepareTool(uxTreeView.SelectedItem as MyFooNode, false);
        //}

        //void uxTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        //{
        //    PrepareTool(uxTreeView.SelectedItem as MyFooNode, false);
        //}

        //void Toolbox_DragStarted(object sender, MouseButtonEventArgs e)
        //{
        //    PrepareTool(e.GetDataContext() as MyFooNode, true);
        //}

        //void PrepareTool(MyFooNode node, bool drag)
        //{
        //    if (node != null)
        //    {

        //        //Get the Type we want to use for the MyFooNode being dragged out here.
        //        Type type =/* GetTypeForFooType(*/node.FooType/*)*/;

        //        var tool = new CreateComponentTool(type);
        //        //if (MyDesignerModel.Instance.DesignSurface != null)
        //        //{
        //        //    MyDesignerModel.Instance.DesignSurface.DesignContext.Services.Tool.CurrentTool = tool;
        //        //    if (drag)
        //        //    {
        //        //        DragDrop.DoDragDrop(this, tool, DragDropEffects.Copy);
        //        //    }
        //        //}
        //    }
        //}
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
                VMHWindowControl mWindowH = new VMHWindowControl();
                windows.Child = mWindowH;
            

        }
        Continuous_contour_Base _Base=new Continuous_contour_Base();
        CvDrawObj.CvDrawRect _rect = new CvDrawObj.CvDrawRect(new CvRect(809,126, 1445.59724358774, 100));
        CvDrawObj.CvDrawRotatedRect _roi = new CvDrawObj.CvDrawRotatedRect(new CvRotatedRect(809.333333333333, 126.666666666667, 2.28267873323278, 1445.59724358774, 100));
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Continuous_contour_Base _Base = new Continuous_contour_Base();
            _Base.show();
            //imagecontrol.DrayRoi(_rect);

            //imagecontrol.ImageWindows.CvDrawObj.CvDrawRotatedRect _roi = new CvDrawObj.CvDrawRotatedRect(new CvRotatedRect(809.333333333333, 126.666666666667, 2.28267873323278, 1445.59724358774, 100));
            //DispTextPara dispTextPara = new DispTextPara()
            //{
            //    Color = Color.Red,
            //    FontSize = 16f,
            //    Point = new CvPoint(500, 500),
            //    Text = "测试"
            //};
            //imagecontrol.DispText(dispTextPara);

            //imagecontrol.SaveCutImage("");
            //image3d.CreatePointCloudFromTiffFast("C:\\Users\\caopengfei\\Desktop\\zhou\\顶盖焊\\2.tif");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var X = _rect.Rect.X;
            var Y = _rect.Rect.Y;
            //var angel = _roi.RotatedRect.Angle;
            var length1 = _rect.Rect.Width;
            var length2 = _rect.Rect.Height;

            int start_X = Convert.ToInt16( Math.Round(X));
            int start_Y = Convert.ToInt16(Math.Round(Y));

            int end_X = Convert.ToInt16(Math.Round(X + length1));
            int end_Y = Convert.ToInt16(Math.Round(Y + length2));

            var img = imagecontrol.HeightImage;
            List<double> points = new List<double>();
            double max = 0, min = 9999999999;
            
            for (int x = start_X; x < end_X; x++)
            {
                int se = end_Y - start_Y;
                double temp = 0;
                    for (int y = start_Y; y < end_Y; y++)
                    {
                        short pixelValue = img.At<short>(y, x);
                    if (pixelValue==0)
                        se -= 1;
                    
                    temp += pixelValue;
                    }
                var value = temp / se;
                if(value> max)
                    max = value;
                if(value< min)
                    min = value;
                
                points.Add(value);
            }
            double range = max - min;
            if(range!= -9999999999)
            {
                Mat img2 = new Mat(Convert.ToInt16(Math.Round(range) / 10 + 100), points.Count, MatType.CV_8UC1, Scalar.All(255));
                for (int i = 0; i < points.Count - 1; i++)
                {
                    double point1= points[i] / -10 + max / 10 + 50;
                    double point2 = points[i+1] / -10 + max / 10 + 50;
                    if (point1 > 0)
                    {
                        Point pt1 = new Point(i, point1);
                        Point pt2 = new Point(i + 1, point2);
                        Cv2.Line(img2, pt1, pt2, Scalar.Black, 1);
                    }
                    else
                        continue;




                }
                imagecontrol.Showimage(img2);
            }

            //Cv2.ImShow("q", img2);




            // 遍历图像的每个像素
            //for (int y = start_Y; y < end_Y; y++)
            //{
            //    for (int x = start_X; x < end_X; x++)
            //    {
            //        short pixelValue = img.At<short>(y, x);
            //        points.Add(pixelValue);
            //    }
            //}
        }
    }
    public class MyItem
    {
        public string Name { get; set; }
    }
    

}
