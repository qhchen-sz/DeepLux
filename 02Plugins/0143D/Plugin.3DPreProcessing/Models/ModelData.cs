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
        无效高度过滤,
        矩形屏蔽,
        飞点去除,
        小孔填补,
        保边滤波,
        行条纹校正,
        列条纹校正,
        连通域筛选,
        平面去趋势,
        高度归一化,
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
                    case eOperatorType.无效高度过滤:
                        _m_value = $"{m_InvalidMinZ} ~ {m_InvalidMaxZ}";
                        break;
                    case eOperatorType.矩形屏蔽:
                        _m_value = $"R{m_MaskRow1}-{m_MaskRow2}, C{m_MaskCol1}-{m_MaskCol2}";
                        break;
                    case eOperatorType.飞点去除:
                        _m_value = $"中值{m_SpikeMedianSize}, 阈值{m_SpikeThreshold}";
                        break;
                    case eOperatorType.小孔填补:
                        _m_value = $"面积<={m_HoleMaxArea}, {m_HoleFillWidth} x {m_HoleFillHeight}";
                        break;
                    case eOperatorType.保边滤波:
                        _m_value = $"{m_EdgeSmoothWidth} x {m_EdgeSmoothHeight}, 阈值{m_EdgeThreshold}";
                        break;
                    case eOperatorType.行条纹校正:
                        _m_value = $"宽度{m_RowStripeWidth}, 强度{m_RowStripeStrength}";
                        break;
                    case eOperatorType.列条纹校正:
                        _m_value = $"高度{m_ColumnStripeHeight}, 强度{m_ColumnStripeStrength}";
                        break;
                    case eOperatorType.连通域筛选:
                        _m_value = m_ComponentKeepMax ? "保留最大区域" : $"{m_ComponentMinArea} ~ {m_ComponentMaxArea}";
                        break;
                    case eOperatorType.平面去趋势:
                        _m_value = $"{m_PlaneMinZ} ~ {m_PlaneMaxZ}";
                        break;
                    case eOperatorType.高度归一化:
                        _m_value = $"x{m_NormalizeScale} + {m_NormalizeOffset}";
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

        #region 无效高度过滤
        private double _m_InvalidMinZ = -1.0;
        public double m_InvalidMinZ
        {
            get { return _m_InvalidMinZ; }
            set { Set(ref _m_InvalidMinZ, value); m_value = ""; }
        }
        private double _m_InvalidMaxZ = 999999.0;
        public double m_InvalidMaxZ
        {
            get { return _m_InvalidMaxZ; }
            set { Set(ref _m_InvalidMaxZ, value); m_value = ""; }
        }
        private bool _m_InvalidPaintEnable = false;
        public bool m_InvalidPaintEnable
        {
            get { return _m_InvalidPaintEnable; }
            set { Set(ref _m_InvalidPaintEnable, value); m_value = ""; }
        }
        private double _m_InvalidFillValue = 0.0;
        public double m_InvalidFillValue
        {
            get { return _m_InvalidFillValue; }
            set { Set(ref _m_InvalidFillValue, value); m_value = ""; }
        }
        #endregion

        #region 矩形屏蔽
        private double _m_MaskRow1 = 0.0;
        public double m_MaskRow1
        {
            get { return _m_MaskRow1; }
            set { Set(ref _m_MaskRow1, value); m_value = ""; }
        }
        private double _m_MaskCol1 = 0.0;
        public double m_MaskCol1
        {
            get { return _m_MaskCol1; }
            set { Set(ref _m_MaskCol1, value); m_value = ""; }
        }
        private double _m_MaskRow2 = 100.0;
        public double m_MaskRow2
        {
            get { return _m_MaskRow2; }
            set { Set(ref _m_MaskRow2, value); m_value = ""; }
        }
        private double _m_MaskCol2 = 100.0;
        public double m_MaskCol2
        {
            get { return _m_MaskCol2; }
            set { Set(ref _m_MaskCol2, value); m_value = ""; }
        }
        private bool _m_MaskPaintEnable = true;
        public bool m_MaskPaintEnable
        {
            get { return _m_MaskPaintEnable; }
            set { Set(ref _m_MaskPaintEnable, value); m_value = ""; }
        }
        private double _m_MaskFillValue = -3.2768;
        public double m_MaskFillValue
        {
            get { return _m_MaskFillValue; }
            set { Set(ref _m_MaskFillValue, value); m_value = ""; }
        }
        #endregion

        #region 飞点去除
        private int _m_SpikeMedianSize = 3;
        public int m_SpikeMedianSize
        {
            get { return _m_SpikeMedianSize; }
            set { Set(ref _m_SpikeMedianSize, value); m_value = ""; }
        }
        private double _m_SpikeThreshold = 0.05;
        public double m_SpikeThreshold
        {
            get { return _m_SpikeThreshold; }
            set { Set(ref _m_SpikeThreshold, value); m_value = ""; }
        }
        #endregion

        #region 小孔填补
        private double _m_HoleMinZ = -1.0;
        public double m_HoleMinZ
        {
            get { return _m_HoleMinZ; }
            set { Set(ref _m_HoleMinZ, value); m_value = ""; }
        }
        private double _m_HoleMaxZ = 999999.0;
        public double m_HoleMaxZ
        {
            get { return _m_HoleMaxZ; }
            set { Set(ref _m_HoleMaxZ, value); m_value = ""; }
        }
        private double _m_HoleMaxArea = 50.0;
        public double m_HoleMaxArea
        {
            get { return _m_HoleMaxArea; }
            set { Set(ref _m_HoleMaxArea, value); m_value = ""; }
        }
        private int _m_HoleFillWidth = 5;
        public int m_HoleFillWidth
        {
            get { return _m_HoleFillWidth; }
            set { Set(ref _m_HoleFillWidth, value); m_value = ""; }
        }
        private int _m_HoleFillHeight = 5;
        public int m_HoleFillHeight
        {
            get { return _m_HoleFillHeight; }
            set { Set(ref _m_HoleFillHeight, value); m_value = ""; }
        }
        #endregion

        #region 保边滤波
        private int _m_EdgeSmoothWidth = 5;
        public int m_EdgeSmoothWidth
        {
            get { return _m_EdgeSmoothWidth; }
            set { Set(ref _m_EdgeSmoothWidth, value); m_value = ""; }
        }
        private int _m_EdgeSmoothHeight = 5;
        public int m_EdgeSmoothHeight
        {
            get { return _m_EdgeSmoothHeight; }
            set { Set(ref _m_EdgeSmoothHeight, value); m_value = ""; }
        }
        private double _m_EdgeThreshold = 0.03;
        public double m_EdgeThreshold
        {
            get { return _m_EdgeThreshold; }
            set { Set(ref _m_EdgeThreshold, value); m_value = ""; }
        }
        #endregion

        #region 条纹校正
        private int _m_RowStripeWidth = 101;
        public int m_RowStripeWidth
        {
            get { return _m_RowStripeWidth; }
            set { Set(ref _m_RowStripeWidth, value); m_value = ""; }
        }
        private double _m_RowStripeStrength = 1.0;
        public double m_RowStripeStrength
        {
            get { return _m_RowStripeStrength; }
            set { Set(ref _m_RowStripeStrength, value); m_value = ""; }
        }
        private int _m_ColumnStripeHeight = 101;
        public int m_ColumnStripeHeight
        {
            get { return _m_ColumnStripeHeight; }
            set { Set(ref _m_ColumnStripeHeight, value); m_value = ""; }
        }
        private double _m_ColumnStripeStrength = 1.0;
        public double m_ColumnStripeStrength
        {
            get { return _m_ColumnStripeStrength; }
            set { Set(ref _m_ColumnStripeStrength, value); m_value = ""; }
        }
        #endregion

        #region 连通域筛选
        private double _m_ComponentMinZ = -1.0;
        public double m_ComponentMinZ
        {
            get { return _m_ComponentMinZ; }
            set { Set(ref _m_ComponentMinZ, value); m_value = ""; }
        }
        private double _m_ComponentMaxZ = 999999.0;
        public double m_ComponentMaxZ
        {
            get { return _m_ComponentMaxZ; }
            set { Set(ref _m_ComponentMaxZ, value); m_value = ""; }
        }
        private double _m_ComponentMinArea = 100.0;
        public double m_ComponentMinArea
        {
            get { return _m_ComponentMinArea; }
            set { Set(ref _m_ComponentMinArea, value); m_value = ""; }
        }
        private double _m_ComponentMaxArea = 999999999.0;
        public double m_ComponentMaxArea
        {
            get { return _m_ComponentMaxArea; }
            set { Set(ref _m_ComponentMaxArea, value); m_value = ""; }
        }
        private bool _m_ComponentKeepMax = false;
        public bool m_ComponentKeepMax
        {
            get { return _m_ComponentKeepMax; }
            set { Set(ref _m_ComponentKeepMax, value); m_value = ""; }
        }
        #endregion

        #region 平面去趋势
        private double _m_PlaneMinZ = -1.0;
        public double m_PlaneMinZ
        {
            get { return _m_PlaneMinZ; }
            set { Set(ref _m_PlaneMinZ, value); m_value = ""; }
        }
        private double _m_PlaneMaxZ = 999999.0;
        public double m_PlaneMaxZ
        {
            get { return _m_PlaneMaxZ; }
            set { Set(ref _m_PlaneMaxZ, value); m_value = ""; }
        }
        private bool _m_PlaneKeepHeight = true;
        public bool m_PlaneKeepHeight
        {
            get { return _m_PlaneKeepHeight; }
            set { Set(ref _m_PlaneKeepHeight, value); m_value = ""; }
        }
        #endregion

        #region 高度归一化
        private double _m_NormalizeScale = 1.0;
        public double m_NormalizeScale
        {
            get { return _m_NormalizeScale; }
            set { Set(ref _m_NormalizeScale, value); m_value = ""; }
        }
        private double _m_NormalizeOffset = 0.0;
        public double m_NormalizeOffset
        {
            get { return _m_NormalizeOffset; }
            set { Set(ref _m_NormalizeOffset, value); m_value = ""; }
        }
        #endregion
    }
}
