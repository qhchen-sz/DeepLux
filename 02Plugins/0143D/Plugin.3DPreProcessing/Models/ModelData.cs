using System;
using HV.Common.Helper;

namespace Plugin._3DPreProcessing.Models
{
    #region enum
    public enum eOperatorType
    {
        均值滤波,
        中值滤波,
        高斯滤波,
        膨胀,
        腐蚀,
        开运算,
        闭运算,
        深度阈值裁剪,
        深度填充,
    }
    #endregion

    [Serializable]
    public class ModelData : NotifyPropertyBase
    {
        /// <summary>
        /// 启用
        /// </summary>
        private bool _m_enable = true;
        public bool m_enable
        {
            get { return _m_enable; }
            set { Set(ref _m_enable, value); }
        }

        /// <summary>
        /// 操作类型
        /// </summary>
        private eOperatorType _m_name;
        public eOperatorType m_name
        {
            get { return _m_name; }
            set { Set(ref _m_name, value); }
        }

        /// <summary>
        /// 显示参数值
        /// </summary>
        private string _m_value;
        public string m_value
        {
            get
            {
                if (_m_value == null)
                    _m_value = string.Empty;
                switch (m_name)
                {
                    case eOperatorType.均值滤波:
                        _m_value = $"{m_MeanImageWidth} x {m_MeanImageHeight}";
                        break;
                    case eOperatorType.中值滤波:
                        _m_value = $"{m_MedianImageWidth} x {m_MedianImageHeight}";
                        break;
                    case eOperatorType.高斯滤波:
                        _m_value = $"核大小 {m_GaussImageSize}";
                        break;
                    case eOperatorType.膨胀:
                        _m_value = $"{m_DilationWidth} x {m_DilationHeight}";
                        break;
                    case eOperatorType.腐蚀:
                        _m_value = $"{m_ErosionWidth} x {m_ErosionHeight}";
                        break;
                    case eOperatorType.开运算:
                        _m_value = $"{m_OpeningWidth} x {m_OpeningHeight}";
                        break;
                    case eOperatorType.闭运算:
                        _m_value = $"{m_ClosingWidth} x {m_ClosingHeight}";
                        break;
                    case eOperatorType.深度阈值裁剪:
                        _m_value = $"{m_ClipMin} ~ {m_ClipMax}";
                        break;
                    case eOperatorType.深度填充:
                        _m_value = $"{m_FillWidth} x {m_FillHeight}";
                        break;
                }
                return _m_value;
            }
            set { Set(ref _m_value, value); }
        }

        #region 均值滤波
        private int _m_MeanImageWidth = 5;
        public int m_MeanImageWidth
        {
            get { return _m_MeanImageWidth; }
            set { Set(ref _m_MeanImageWidth, value); m_value = ""; }
        }
        private int _m_MeanImageHeight = 5;
        public int m_MeanImageHeight
        {
            get { return _m_MeanImageHeight; }
            set { Set(ref _m_MeanImageHeight, value); m_value = ""; }
        }
        #endregion

        #region 中值滤波
        private int _m_MedianImageWidth = 5;
        public int m_MedianImageWidth
        {
            get { return _m_MedianImageWidth; }
            set { Set(ref _m_MedianImageWidth, value); m_value = ""; }
        }
        private int _m_MedianImageHeight = 5;
        public int m_MedianImageHeight
        {
            get { return _m_MedianImageHeight; }
            set { Set(ref _m_MedianImageHeight, value); m_value = ""; }
        }
        #endregion

        #region 高斯滤波
        private int _m_GaussImageSize = 5;
        public int m_GaussImageSize
        {
            get { return _m_GaussImageSize; }
            set { Set(ref _m_GaussImageSize, value); m_value = ""; }
        }
        #endregion

        #region 膨胀
        private int _m_DilationWidth = 5;
        public int m_DilationWidth
        {
            get { return _m_DilationWidth; }
            set { Set(ref _m_DilationWidth, value); m_value = ""; }
        }
        private int _m_DilationHeight = 5;
        public int m_DilationHeight
        {
            get { return _m_DilationHeight; }
            set { Set(ref _m_DilationHeight, value); m_value = ""; }
        }
        #endregion

        #region 腐蚀
        private int _m_ErosionWidth = 5;
        public int m_ErosionWidth
        {
            get { return _m_ErosionWidth; }
            set { Set(ref _m_ErosionWidth, value); m_value = ""; }
        }
        private int _m_ErosionHeight = 5;
        public int m_ErosionHeight
        {
            get { return _m_ErosionHeight; }
            set { Set(ref _m_ErosionHeight, value); m_value = ""; }
        }
        #endregion

        #region 开运算
        private int _m_OpeningWidth = 5;
        public int m_OpeningWidth
        {
            get { return _m_OpeningWidth; }
            set { Set(ref _m_OpeningWidth, value); m_value = ""; }
        }
        private int _m_OpeningHeight = 5;
        public int m_OpeningHeight
        {
            get { return _m_OpeningHeight; }
            set { Set(ref _m_OpeningHeight, value); m_value = ""; }
        }
        #endregion

        #region 闭运算
        private int _m_ClosingWidth = 5;
        public int m_ClosingWidth
        {
            get { return _m_ClosingWidth; }
            set { Set(ref _m_ClosingWidth, value); m_value = ""; }
        }
        private int _m_ClosingHeight = 5;
        public int m_ClosingHeight
        {
            get { return _m_ClosingHeight; }
            set { Set(ref _m_ClosingHeight, value); m_value = ""; }
        }
        #endregion

        #region 深度阈值裁剪
        private double _m_ClipMin = -1000.0;
        public double m_ClipMin
        {
            get { return _m_ClipMin; }
            set { Set(ref _m_ClipMin, value); m_value = ""; }
        }
        private double _m_ClipMax = 1000.0;
        public double m_ClipMax
        {
            get { return _m_ClipMax; }
            set { Set(ref _m_ClipMax, value); m_value = ""; }
        }
        #endregion

        #region 深度填充
        private int _m_FillWidth = 5;
        public int m_FillWidth
        {
            get { return _m_FillWidth; }
            set { Set(ref _m_FillWidth, value); m_value = ""; }
        }
        private int _m_FillHeight = 5;
        public int m_FillHeight
        {
            get { return _m_FillHeight; }
            set { Set(ref _m_FillHeight, value); m_value = ""; }
        }
        #endregion
    }
}
