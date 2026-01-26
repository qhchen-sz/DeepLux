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
using System.Windows.Shapes;

namespace HV.Views
{
    /// <summary>
    /// Loading.xaml 的交互逻辑
    /// </summary>
    public partial class Loading : Window
    {
        Action handler;

        private Loading(Action handler)
        {
            InitializeComponent();


            this.handler = handler;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            handler?.BeginInvoke(OnComplate, null);
        }

        private void OnComplate(IAsyncResult ar)
        {
            this.Dispatcher.Invoke(new Action(() => { Close(); }));
        }

        public static Loading ShowLoading(Window owner, Action handler, string str)
        {
            var loading = new Loading(handler);
            loading.txtTitle.Content = str;

            // 设置Loading窗体的Owner属性为传入的主窗体
            loading.Owner = owner;
            loading.WindowState = owner.WindowState;
            // 设置窗体显示位置为主窗体的中心
            loading.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // 设置Loading窗体的尺寸与主窗体相同
            loading.Width = owner.Width - 100;
            loading.Height = owner.Height - 100;

            loading.Show();


            return loading;
        }
        public static void UpdateMessage(Loading obj, string str)
        {
            obj.txtTitle.Content = str;
        }
        public static void UpdataLocaton(Window owner, Loading obj)
        {

        }
        public static void Close(Loading wait)
        {
            if (wait != null)
            {
                wait.Close();
            }
        }
    }
}
