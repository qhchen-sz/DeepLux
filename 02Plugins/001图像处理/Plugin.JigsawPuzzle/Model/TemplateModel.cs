using EventMgrLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HV.Common;
using HV.Common.Helper;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views;
using System.Deployment.Internal;

namespace Plugin.GrabImage.Model
{
    [Serializable]
    public class MathTemplateModel : NotifyPropertyBase
    {
        private int _ID;

        /// <summary>
        /// ID
        /// </summary>
        public int ID
        {
            get { return _ID; }
            set { Set(ref _ID, value); }
        }

        private double _Score;

        /// <summary>
        /// 匹配分数
        /// </summary>
        public double Score
        {
            get { return _Score; }
            set { Set(ref _Score, value); }
        }

        private double _X;

        /// <summary>
        /// X坐标
        /// </summary>
        public double X
        {
            get { return _X; }
            set { Set(ref _X, value); }
        }

        private double _Y;

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y
        {
            get { return _Y; }
            set { Set(ref _Y, value); }
        }

        private double _Deg;

        /// <summary>
        /// 角度
        /// </summary>
        public double Deg
        {
            get { return _Deg; }
            set { Set(ref _Deg, value); }
        }
    }

    [Serializable]
    public class JigsawPuzzleCheckBox : NotifyPropertyBase
    {
        //输出裁剪图像
        private bool _isOutRoiImageChecked = false;

        public bool IsOutRoiImageChecked
        {
            get => _isOutRoiImageChecked;
            set { Set(ref _isOutRoiImageChecked, value); }
        }

        //世界mm坐标
        private bool _isWorldChecked = false;

        public bool IsWorldChecked
        {
            get => _isWorldChecked;
            set { Set(ref _isWorldChecked, value); }
        }

        //整体移动
        private bool _isOmChecked = false;

        public bool IsOmChecked
        {
            get => _isOmChecked;
            set { Set(ref _isOmChecked, value); }
        }

        //显示结果
        private bool _isOutResult = false;

        public bool IsOutResult
        {
            get => _isOutResult;
            set { Set(ref _isOutResult, value); }
        }

        //输出点云XYZ数组
        private bool _isDianChecked = false;

        public bool IsDianChecked
        {
            get => _isDianChecked;
            set { Set(ref _isDianChecked, value); }
        }

        //输出世界坐标
        private bool _isOutWorldChecked = false;

        public bool IsOutWorldChecked
        {
            get => _isOutWorldChecked;
            set { Set(ref _isOutWorldChecked, value); }
        }

        //创建阵列以坐标系
        private bool _isCreateZlChecked = false;

        public bool IsCreateZlChecked
        {
            get => _isCreateZlChecked;
            set { Set(ref _isCreateZlChecked, value); }
        }

        //截图的图像保留图型
        private bool _isScreenshotChecked = false;

        public bool IsScreenshotChecked
        {
            get => _isScreenshotChecked;
            set { Set(ref _isScreenshotChecked, value); }
        }
    }

    [Serializable]
    public class JigsawPuzzleData : NotifyPropertyBase
    {
        // 定义一个委托类型，用于回调方法的签名
        public delegate void PropertyChangedHandler(object sender, PropertyChangedEventArgs e);

        // 定义一个事件，当任意属性改变时将触发此事件
        public event PropertyChangedHandler PropertyChanged;
        public int ID { get; set; }
        //截图的图像保留图型
        private string _no="";
        public string NO
        {
            get => _no;
            set
            {
                if (_no.Equals(value))
                {
                    return;
                }
                Set(ref _no, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(NO)));
            }
        }
        private double _x=200;

        public double X
        {
            get => _x;
            set
            { 
                if (_x.Equals(value))
                {
                    return;
                }
                Set(ref _x, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(X)));
            }
        }
        private double _y =200;

        public double Y
        {
            get => _y;
            set
            {
                if (_y.Equals(value))
                {
                    return;
                }
                Set(ref _y, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Y)));
            }
        }
        private double _l1 =20;

        public double L1
        {
            get => _l1;
            set
            {
                if (_l1.Equals(value))
                {
                    return;
                }
                Set(ref _l1, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(L1)));
            }
        }
        private double _l2 =20;

        public double L2
        {
            get => _l2;
            set
            {
                if (_l2.Equals(value))
                {
                    return;
                }
                Set(ref _l2, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(L2)));
            }
        }
        private double _deg =0;

        public double Deg
        {
            get => _deg;
            set
            {
                if (_deg.Equals(value))
                {
                    return;
                }
                Set(ref _deg, value);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Deg)));
            }
        }
        // 用于触发PropertyChanged事件的方法
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
    }

}
