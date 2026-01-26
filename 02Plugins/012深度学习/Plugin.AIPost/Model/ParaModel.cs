using HalconDotNet;
using HV.Common.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Plugin.AIPost.Model
{
    [Serializable]
    public class ParaModel : NotifyPropertyBase
    {
        /// <summary>面积</summary>
        #region

        private int _AreaUpLimt= 999999999, _AreaDnLimt= 0;
        private string _AreaUpLimtstr = "999999999", _AreaDnLimtstr = "0";
        public int AreaUpLimt
        {
            get { return _AreaUpLimt; }
            set { _AreaUpLimt = value; RaisePropertyChanged(); }
        }
        public int AreaDnLimt
        {
            get { return _AreaDnLimt; }
            set { _AreaDnLimt = value; RaisePropertyChanged(); }
        }
        public string AreaUpLimtstr
        {
            get { return _AreaUpLimtstr; }
            set { _AreaUpLimtstr = value; RaisePropertyChanged(); }
        }
        public string AreaDnLimtstr
        {
            get { return _AreaDnLimtstr; }
            set { _AreaDnLimtstr = value; RaisePropertyChanged(); }
        }
        private bool _UseArea=false;
        public bool UseArea
        {
            get { return _UseArea; }
            set { Set(ref _UseArea, value); }
        }
        #endregion
        /// <summary>宽度</summary>
        #region

        private int _WidthUpLimt= 999999999, _WidthDnLimt= 0;
        private string _WidthUpLimtstr = "999999999", _WidthDnLimtstr="0";
        public int WidthUpLimt
        {
            get { return _WidthUpLimt; }
            set { _WidthUpLimt = value; RaisePropertyChanged(); }
        }
        public int WidthDnLimt
        {
            get { return _WidthDnLimt; }
            set { _WidthDnLimt = value; RaisePropertyChanged(); }
        }
        public string WidthUpLimtstr
        {
            get { return _WidthUpLimtstr; }
            set { _WidthUpLimtstr = value; RaisePropertyChanged(); }
        }
        public string WidthDnLimtstr
        {
            get { return _WidthDnLimtstr; }
            set { _WidthDnLimtstr = value; RaisePropertyChanged(); }
        }
        private bool _UseWidth=false;
        public bool UseWidth
        {
            get { return _UseWidth; }
            set { Set(ref _UseWidth, value); }
        }


        #endregion
        /// <summary>长度</summary>
        #region

        private int _HeightUpLimt= 999999999, _HeightDnLimt= 0;
        private string _HeightUpLimtstr= "999999999", _HeightDnLimtstr="0";
        public int HeightUpLimt
        {
            get { return _HeightUpLimt; }
            set { _HeightUpLimt = value; RaisePropertyChanged(); }
        }
        public int HeightDnLimt
        {
            get { return _HeightDnLimt; }
            set { _HeightDnLimt = value; RaisePropertyChanged(); }
        }
        public string HeightUpLimtstr
        {
            get { return _HeightUpLimtstr; }
            set { _HeightUpLimtstr = value; RaisePropertyChanged(); }
        }
        public string HeightDnLimtstr
        {
            get { return _HeightDnLimtstr; }
            set { _HeightDnLimtstr = value; RaisePropertyChanged(); }
        }
        private bool _UseHeight = false;
        public bool UseHeight
        {
            get { return _UseHeight; }
            set { Set(ref _UseHeight, value); }
        }
        #endregion
    }
    [Serializable]
    public class DefectVarModel : NotifyPropertyBase
    {
        /// <summary>缺陷类型</summary>
        #region
        private string _DefectType;
        public string DefectType
        {
            get { return _DefectType; }
            set { Set(ref _DefectType, value); }
        }
        #endregion
        /// <summary>缺陷面积</summary>
        #region
        private int _DefectArea;
        public int DefectArea
        {
            get { return _DefectArea; }
            set { Set(ref _DefectArea, value); }
        }
        #endregion
        /// <summary>缺陷长度</summary>
        #region
        private int _DefectHeight;
        public int DefectHeight
        {
            get { return _DefectHeight; }
            set { Set(ref _DefectHeight, value); }
        }
        #endregion
        /// <summary>缺陷宽度</summary>
        #region
        private int _DefectWidth;
        public int DefectWidth
        {
            get { return _DefectWidth; }
            set { Set(ref _DefectWidth, value); }
        }
        #endregion
        /// <summary>区域</summary>
        public HRegion region {  get; set; }
        /// <summary>缺陷索引</summary>
        public int Index { get; set; }
    }
}
    

    
