using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using HV.Common.Enums;
using HV.Common.Helper;
using HalconDotNet;

namespace Plugin.Blob.Models
{
    #region enum
    public enum eOperatorType
    {
        连通,
        合并,
        补集,
        相减,
        相交,
        孔洞填充,

        开运算,
        闭运算,
        腐蚀,
        膨胀,

        特征筛选,
        转换,
        矩形分割,
        获取最大区域


    }
    public enum eConversionType
    {
        凸形,
        椭圆,
        最大内接圆,
        最小外接圆,
        最小外接矩形1,
        最小外接矩形2,
        最大内接矩形1,
    }
    public enum eTransImageType
    {
        通用比例转换,
        RGB,
        HSV,
        HSI,
        YUV,
    }
    public enum eStructuralElements
    {
        矩形,
        圆形
    }
    public enum eConditionalRelationship
    {
        and,
        or
    }
    public enum eFilterConditions
    {
        面积,
        宽度,
        高度,
        X,
        Y,
        角度,
        圆度,
        矩形度,
        凸度
    }
    public enum eTransImageChannel
    {
        第一通道,
        第二通道,
        第三通道,
    }
    public enum eMirrorImageType
    {
        水平镜像,
        垂直镜像,
        对角镜像,
    }
    public enum eVarThresholdType
    {
        大于等于,
        小于等于,
        等于,
        不等于,
    }
    public enum eRotateImageAngle
    { 
        _90,
        _180,
        _270,
    }
    #endregion

    [Serializable]
    public class ModelData : NotifyPropertyBase
    {
        [NonSerialized]
        public HRegion ResultRegion;
        [field: NonSerialized]
        public Array StructuralElements   //结构元素
        {
            get { return Enum.GetValues(typeof(eStructuralElements)); }
            set {; }
        }
        [field: NonSerialized]
        public Array ConversionType   //半径参数
        {
            get { return Enum.GetValues(typeof(eConversionType)); }
            set {; }
        }
        [field: NonSerialized]
        public Array ConditionalRelationship   //条件关系
        {
            get { return Enum.GetValues(typeof(eConditionalRelationship)); }
            set {; }
        }
        [field: NonSerialized]
        public Array FilterConditions   //筛选条件
        {
            get { return Enum.GetValues(typeof(eFilterConditions)); }
            set {; }
        }
        [field: NonSerialized]
        public Array TransImageTypes   //图像转换类型
        {
            get { return Enum.GetValues(typeof(eTransImageType)); }
            set {; }
        }
        [field: NonSerialized]
        public Array TransImageChannels   //转换通道
        {
            get { return Enum.GetValues(typeof(eTransImageChannel)); }
            set {; }
        }
        [field: NonSerialized]
        public Array MirrorImageTypes   //镜像类型
        {
            get { return Enum.GetValues(typeof(eMirrorImageType)); }
            set {; }
        }
        [field: NonSerialized]
        public Array VarThresholdTypes   //比较类型
        {
            get { return Enum.GetValues(typeof(eVarThresholdType)); }
            set {; }
        }
        [field: NonSerialized]
        public Array RotateImageAngles   //旋转角度
        {
            get { return Enum.GetValues(typeof(eRotateImageAngle)); }
            set {; }
        }
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
        /// 数据类型
        /// </summary>
        private eOperatorType _m_name;
        public eOperatorType m_name
        {
            get { return _m_name; }
            set { Set(ref _m_name, value); }
        }
        /// <summary>
        /// 数据类型
        /// </summary>
        private int _m_id;
        public int m_id
        {
            get { return _m_id; }
            set { Set(ref _m_id, value); }
        }
        /// <summary>
        /// 显示数据
        /// </summary>
        private string _m_value;
        public string m_value
        {
            get
            {
                if (_m_value == null)
                    _m_value = string.Empty;
                //switch (m_name)
                //{
                //    //图像调整
                //    case eOperatorType.彩色转灰:
                //        _m_value = m_TransImageType.ToString() + " ; " + m_TransImageChannel;
                //        break;
                //    case eOperatorType.图像镜像:
                //        _m_value = m_MirrorImageType.ToString();
                //        break;
                //    case eOperatorType.图像旋转:
                //        _m_value = m_RotateImageAngle.ToString();
                //        break;
                //    case eOperatorType.修改图像尺寸:
                //        _m_value = m_ChangeImageWidth.ToString() + " ; " + m_ChangeImageHeight.ToString();
                //        break;
                //    //滤波
                //    case eOperatorType.均值滤波:
                //        _m_value = m_MeanImageWidth.ToString() + " ; " + m_MeanImageHeight.ToString();
                //        break;
                //    case eOperatorType.中值滤波:
                //        _m_value = m_MedianImageWidth.ToString() + " ; " + m_MedianImageHeight.ToString();
                //        break;
                //    case eOperatorType.高斯滤波:
                //        _m_value = m_GaussImageSize.ToString();
                //        break;
                //    //形态学运算
                //    case eOperatorType.灰度膨胀:
                //        _m_value = m_GrayDilationWidth.ToString() + " ; " + m_GrayDilationHeight.ToString();
                //        break;
                //    case eOperatorType.灰度腐蚀:
                //        _m_value = m_GrayErosionWidth.ToString() + " ; " + m_GrayErosionHeight.ToString();
                //        break;
                //    //图像增强
                //    case eOperatorType.锐化:
                //        _m_value = m_EmphaSizeWidth.ToString() + " ; " + m_EmphaSizeHeight.ToString() + " ; " + m_EmphaSizeFactor.ToString();
                //        break;
                //    case eOperatorType.对比度:
                //        _m_value = m_IlluminateWidth.ToString() + " ; " + m_IlluminateHeight.ToString() + " ; " + m_IlluminateFactor.ToString();
                //        break;
                //    case eOperatorType.亮度调节:
                //        _m_value = m_ScaleImageMult.ToString() + " ; " + m_ScaleImageAdd.ToString();
                //        break;
                //    case eOperatorType.灰度开运算:
                //        _m_value = m_OpeningWidth.ToString() + " ; " + m_OpeningHeight.ToString();
                //        break;
                //    case eOperatorType.灰度闭运算:
                //        _m_value = m_ClosingWidth.ToString() + " ; " + m_ClosingHeight.ToString();
                //        break;
                //    case eOperatorType.反色:
                //        _m_value = m_InvertImageLogic.ToString();
                //        break;
                //    //二值化
                //    case eOperatorType.二值化:
                //        _m_value = m_ThresholdLow.ToString() + " ; " + m_ThresholdHight.ToString() + " ; " +
                //                   m_ThresholdReverse.ToString();
                //        break;
                //    case eOperatorType.均值二值化:
                //        _m_value = m_VarThresholdWidth.ToString() + " ; " + m_VarThresholdHeight.ToString() + " ; " +
                //                   m_VarThresholdSkew.ToString() + " ; " + m_VarThresholdType.ToString();
                //        break;
                //}
                return _m_value;
            }
            set { Set(ref _m_value, value); }
        }

        private eConditionalRelationship _m_ConditionalRelationship = eConditionalRelationship.and;//比较类型
        public eConditionalRelationship m_ConditionalRelationship
        {
            get { return _m_ConditionalRelationship; }
            set { Set(ref _m_ConditionalRelationship, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private eFilterConditions _m_FilterConditions = eFilterConditions.面积;//比较类型
        public eFilterConditions m_FilterConditions
        {
            get { return _m_FilterConditions; }
            set { Set(ref _m_FilterConditions, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #region 合并
        private Array _Union1Index;
        [field: NonSerialized]
        public Array Union1Index   //转换通道
        {
            get { return _Union1Index; }
            set { Set(ref _Union1Index, value); }
        }

        private string _m_Union1Index = "上一个区域";//比较类型
        public string m_Union1Index
        {
            get { return _m_Union1Index; }
            set { Set(ref _m_Union1Index, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 补集
        private Array _ComplementIndex;
        [field: NonSerialized]
        public Array ComplementIndex   
        {
            get { return _ComplementIndex; }
            set { Set(ref _ComplementIndex, value); }
        }

        private string _m_ComplementIndex = "上一个区域";
        public string m_ComplementIndex
        {
            get { return _m_ComplementIndex; }
            set { Set(ref _m_ComplementIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 相减
        private Array _DifferenceIndex;
        [field: NonSerialized]
        public Array DifferenceIndex
        {
            get { return _DifferenceIndex; }
            set { Set(ref _DifferenceIndex, value); }
        }

        private string _m_DifferenceIndex = "上一个区域";
        public string m_DifferenceIndex
        {
            get { return _m_DifferenceIndex; }
            set { Set(ref _m_DifferenceIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }

        private Array _DifferenceIndex2;
        [field: NonSerialized]
        public Array DifferenceIndex2
        {
            get { return _DifferenceIndex2; }
            set { Set(ref _DifferenceIndex2, value); }
        }

        private string _m_DifferenceIndex2 = "上一个区域";
        public string m_DifferenceIndex2
        {
            get { return _m_DifferenceIndex2; }
            set { Set(ref _m_DifferenceIndex2, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 相交
        private Array _IntersectionIndex;
        [field: NonSerialized]
        public Array IntersectionIndex
        {
            get { return _IntersectionIndex; }
            set { Set(ref _IntersectionIndex, value); }
        }

        private string _m_IntersectionIndex = "上一个区域";
        public string m_IntersectionIndex
        {
            get { return _m_IntersectionIndex; }
            set { Set(ref _m_IntersectionIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }

        private Array _IntersectionIndex2;
        [field: NonSerialized]
        public Array IntersectionIndex2
        {
            get { return _IntersectionIndex2; }
            set { Set(ref _IntersectionIndex2, value); }
        }

        private string _m_IntersectionIndex2 = "上一个区域";
        public string m_IntersectionIndex2
        {
            get { return _m_IntersectionIndex2; }
            set { Set(ref _m_IntersectionIndex2, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 孔洞填充
        private Array _FillIndex;
        [field: NonSerialized]
        public Array FillIndex
        {
            get { return _FillIndex; }
            set { Set(ref _FillIndex, value); }
        }

        private string _m_FillIndex = "上一个区域";
        public string m_FillIndex
        {
            get { return _m_FillIndex; }
            set { Set(ref _m_FillIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 开运算
        private Array _OpenIndex;
        [field: NonSerialized]
        public Array OpenIndex
        {
            get { return _OpenIndex; }
            set { Set(ref _OpenIndex, value); }
        }

        private string _m_OpenIndex = "上一个区域";
        public string m_OpenIndex
        {
            get { return _m_OpenIndex; }
            set { Set(ref _m_OpenIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_OpenWidth = 10; //宽            1-999  step = 1
        public int m_OpenWidth
        {
            get { return _m_OpenWidth; }
            set { Set(ref _m_OpenWidth, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_OpenHeight = 10; //高           1-999  step = 1
        public int m_OpenHeight
        {
            get { return _m_OpenHeight; }
            set { Set(ref _m_OpenHeight, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_OpenRadius = 10; //高           1-999  step = 1
        public int m_OpenRadius
        {
            get { return _m_OpenRadius; }
            set { Set(ref _m_OpenRadius, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 闭运算
        private Array _CloseIndex;
        [field: NonSerialized]
        public Array CloseIndex
        {
            get { return _CloseIndex; }
            set { Set(ref _CloseIndex, value); }
        }

        private string _m_CloseIndex = "上一个区域";
        public string m_CloseIndex
        {
            get { return _m_CloseIndex; }
            set { Set(ref _m_CloseIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private eStructuralElements _m_StructuralElements = eStructuralElements.矩形;//比较类型
        public eStructuralElements m_StructuralElements
        {
            get { return _m_StructuralElements; }
            set { Set(ref _m_StructuralElements, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_CloseWidth = 10; //宽            1-999  step = 1
        public int m_CloseWidth
        {
            get { return _m_CloseWidth; }
            set { Set(ref _m_CloseWidth, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_CloseHeight = 10; //高           1-999  step = 1
        public int m_CloseHeight
        {
            get { return _m_CloseHeight; }
            set { Set(ref _m_CloseHeight, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_CloseRadius = 10; //高           1-999  step = 1
        public int m_CloseRadius
        {
            get { return _m_CloseRadius; }
            set { Set(ref _m_CloseRadius, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 腐蚀
        private Array _ErosionIndex;
        [field: NonSerialized]
        public Array ErosionIndex
        {
            get { return _ErosionIndex; }
            set { Set(ref _ErosionIndex, value); }
        }

        private string _m_ErosionIndex = "上一个区域";
        public string m_ErosionIndex
        {
            get { return _m_ErosionIndex; }
            set { Set(ref _m_ErosionIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_ErosionWidth = 10; //宽            1-999  step = 1
        public int m_ErosionWidth
        {
            get { return _m_ErosionWidth; }
            set { Set(ref _m_ErosionWidth, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_ErosionHeight = 10; //高           1-999  step = 1
        public int m_ErosionHeight
        {
            get { return _m_ErosionHeight; }
            set { Set(ref _m_ErosionHeight, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_ErosionRadius = 10; //高           1-999  step = 1
        public int m_ErosionRadius
        {
            get { return _m_ErosionRadius; }
            set { Set(ref _m_ErosionRadius, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 膨胀
        private Array _DilationIndex;
        [field: NonSerialized]
        public Array DilationIndex
        {
            get { return _DilationIndex; }
            set { Set(ref _DilationIndex, value); }
        }

        private string _m_DilationIndex = "上一个区域";
        public string m_DilationIndex
        {
            get { return _m_DilationIndex; }
            set { Set(ref _m_DilationIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_DilationWidth = 10; //宽            1-999  step = 1
        public int m_DilationWidth
        {
            get { return _m_DilationWidth; }
            set { Set(ref _m_DilationWidth, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_DilationHeight = 10; //高           1-999  step = 1
        public int m_DilationHeight
        {
            get { return _m_DilationHeight; }
            set { Set(ref _m_DilationHeight, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_DilationRadius = 10; //高           1-999  step = 1
        public int m_DilationRadius
        {
            get { return _m_DilationRadius; }
            set { Set(ref _m_DilationRadius, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 特征筛选
        private Array _FeaturesIndex;
        [field: NonSerialized]
        public Array FeaturesIndex
        {
            get { return _FeaturesIndex; }
            set { Set(ref _FeaturesIndex, value); }
        }

        private string _m_FeaturesIndex = "上一个区域";
        public string m_FeaturesIndex
        {
            get { return _m_FeaturesIndex; }
            set { Set(ref _m_FeaturesIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private double _m_FeaturesMin = 1; //最小值
        public double m_FeaturesMin
        {
            get { return _m_FeaturesMin; }
            set { Set(ref _m_FeaturesMin, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private double _m_FeaturesMax = 9999999; //最大值            1-999  step = 1
        public double m_FeaturesMax
        {
            get { return _m_FeaturesMax; }
            set { Set(ref _m_FeaturesMax, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        #endregion
        #region 转换
        private Array _ConversionIndex;
        [field: NonSerialized]
        public Array ConversionIndex
        {
            get { return _ConversionIndex; }
            set { Set(ref _ConversionIndex, value); }
        }

        private string _m_ConversionIndex = "上一个区域";
        public string m_ConversionIndex
        {
            get { return _m_ConversionIndex; }
            set { Set(ref _m_ConversionIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private eConversionType _m_ConversionType = eConversionType.最小外接矩形1;//比较类型
        public eConversionType m_ConversionType
        {
            get { return _m_ConversionType; }
            set { Set(ref _m_ConversionType, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }

        #endregion
        #region 获取最大区域
        private Array _ShapeStdIndex;
        [field: NonSerialized]
        public Array ShapeStdIndex
        {
            get { return _ShapeStdIndex; }
            set { Set(ref _ShapeStdIndex, value); }
        }

        private string _m_ShapeStdIndex = "上一个区域";
        public string m_ShapeStdIndex
        {
            get { return _m_ShapeStdIndex; }
            set { Set(ref _m_ShapeStdIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }


        #endregion
        #region 矩形分割
        private Array _DivisionIndex;
        [field: NonSerialized]
        public Array DivisionIndex
        {
            get { return _DivisionIndex; }
            set { Set(ref _DivisionIndex, value); }
        }

        private string _m_DivisionIndex = "上一个区域";
        public string m_DivisionIndex
        {
            get { return _m_DivisionIndex; }
            set { Set(ref _m_DivisionIndex, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_DivisionWidth = 10; //宽            1-999  step = 1
        public int m_DivisionWidth
        {
            get { return _m_DivisionWidth; }
            set { Set(ref _m_DivisionWidth, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }
        private int _m_DivisionHeight = 10; //高           1-999  step = 1
        public int m_DivisionHeight
        {
            get { return _m_DivisionHeight; }
            set { Set(ref _m_DivisionHeight, value); m_value = ""; }  //m_value="" 是为了改变一下值，以便界面更新m_value
        }

        #endregion

    }
}
