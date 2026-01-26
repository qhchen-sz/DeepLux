using HalconDotNet;
using Plugin.Matching.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Dialogs.Views;
using HV.Models;

namespace Plugin.Matching.ViewModels
{
    [Serializable]
    public class EditViewModel:NotifyPropertyBase
    {
        #region Prop
        [NonSerialized]
        public MatchingViewModel MatchingViewModel;
        [NonSerialized]
        public EditView view;
        private double _StartPhi = -10f;
        /// <summary>
        /// 起始角度
        /// </summary>
        public double StartPhi
        {
            get { return _StartPhi; }
            set { Set(ref _StartPhi, value); }
        }
        private double _EndPhi = 10f;
        /// <summary>
        /// 结束角度
        /// </summary>
        public double EndPhi
        {
            get { return _EndPhi; }
            set { Set(ref _EndPhi, value); }
        }
        private double _MinScale = 0.8;
        /// <summary>
        /// 最小缩放比例
        /// </summary>
        public double MinScale
        {
            get { return _MinScale; }
            set { Set(ref _MinScale, value); }
        }
        private double _MaxScale = 1.1;
        /// <summary>
        /// 最大缩放比例
        /// </summary>
        public double MaxScale
        {
            get { return _MaxScale; }
            set { Set(ref _MaxScale, value); }
        }
        public Array CompTypes { get; set; }= Enum.GetValues(typeof(eCompType));
        private eCompType _CompType = eCompType.黑白对比不一致;
        /// <summary>
        /// 对比极性
        /// </summary>
        public eCompType CompType
        {
            get { return _CompType; }
            set { Set(ref _CompType, value); }
        }
        public Array Optimizations { get; set; } = Enum.GetValues(typeof(eOptimization));
        private eOptimization _Optimization = eOptimization.粗略;
        /// <summary>
        /// 精细程度
        /// </summary>
        public eOptimization Optimization
        {
            get { return _Optimization; }
            set { Set(ref _Optimization, value); }
        }
        public Array DrawShapes { get; set; } = Enum.GetValues(typeof(eDrawShape));
        private eDrawShape _DrawShape = eDrawShape.圆形;
        /// <summary>
        /// 涂抹形状
        /// </summary>
        public eDrawShape DrawShape
        {
            get { return _DrawShape; }
            set { Set(ref _DrawShape, value,new Action(() =>SetBurshRegion())); }
        }
        private int _DrawSize=10;
        /// <summary>
        /// 涂抹尺寸
        /// </summary>
        public int DrawSize
        {
            get { return _DrawSize; }
            set { Set(ref _DrawSize, value,new Action(() =>SetBurshRegion())); }
        }

        private int _Threshold = 120;
        /// <summary>
        /// 梯度阈值
        /// </summary>
        public int Threshold
        {
            get { return _Threshold; }
            set { Set(ref _Threshold, value); }
        }
        private int _MinLength = 30;
        /// <summary>
        /// 最小长度
        /// </summary>
        public int MinLength
        {
            get { return _MinLength; }
            set { Set(ref _MinLength, value); }
        }
        [NonSerialized]
        public HXLDCont contour_xld;
        [NonSerialized]
        public HObject OutImage;
        HObject finalRegion = new HObject();
        [NonSerialized]
        HObject brushRegion = new HObject();
        private eEditMode _EditMode = eEditMode.正常显示;
        /// <summary>
        /// 指定图像
        /// </summary>
        public eEditMode EditMode
        {
            get { return _EditMode; }
            set { Set(ref _EditMode, value, new Action(() => DrawOrWipe(_EditMode))); }
            //set
            //{
            //    _EditMode = value;
            //    //Set(ref _EditMode, value);
            //    Set(ref _EditMode, value, new Action(() =>
            //    {
            //        //switch (value)
            //        //{
            //        //    case eEditMode.正常显示:
            //        //        view.mWindowH.DrawModel = false;
            //        //        break;
            //        //    case eEditMode.绘制涂抹:
            //        //        DrawOrWipe(_EditMode);
            //        //        break;
            //        //    case eEditMode.擦除涂抹:
            //        //        DrawOrWipe(_EditMode);
            //        //        break;
            //        //    default:
            //        //        break;
            //        //}
            //    }));

            //}
        }

        #endregion

        #region Command
        [NonSerialized]
        private CommandBase _LoadedCommand;
        public CommandBase LoadedCommand
        {
            get
            {
                if (_LoadedCommand == null)
                {
                    _LoadedCommand = new CommandBase((obj) =>
                    {
                        if (view != null)
                        {
                            if (view.mWindowH == null)
                            {
                                view.mWindowH = new VMHWindowControl();
                                view.winFormHost.Child = view.mWindowH;
                                view.mWindowH.HobjectToHimage(OutImage);
                                //view.mWindowH.WindowH.DispHobject(contour_xld, "green");
                                //if (finalRegion.IsInitialized())
                                //{
                                //    view.mWindowH.DispObj(finalRegion, "blue");
                                //}
                                if (OutImage!=null&&OutImage.IsInitialized())
                                {
                                    FindModel();
                                }
                                
                                DrawSize = 5;
                            }
                        }
                    });
                }
                return _LoadedCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        [NonSerialized]
        private CommandBase _RelearnCommand;
        public CommandBase RelearnCommand
        {
            get
            {
                if (_RelearnCommand == null)
                {
                    _RelearnCommand = new CommandBase((obj) =>
                    {
                        CreateModel();
                        FindModel();
                        MatchingViewModel.ModeCoord = MatchingViewModel.MathCoord;
                    });
                }
                return _RelearnCommand;
            }
        }

        [NonSerialized]
        private CommandBase _ClearPaintCommand;
        public CommandBase ClearPaintCommand
        {
            get
            {
                if (_ClearPaintCommand == null)
                {
                    _ClearPaintCommand = new CommandBase((obj) =>
                    {
                        finalRegion.Dispose();
                        view.mWindowH.HobjectToHimage(OutImage);
                        view.mWindowH.WindowH.DispHobject(contour_xld, "green");
                    });
                }
                return _ClearPaintCommand;
            }
        }
        #endregion

        #region Method
        private void SetBurshRegion()
        {
            HObject ho_temp_brush = new HObject();
            HTuple hv_Row1 = 10, hv_Column1 = 10, hv_Row2 = null, hv_Column2 = null;
            HTuple imageWidth, imageHeight;
            if (OutImage == null || !OutImage.IsInitialized())
            {
                return;
            }
            HImage image = new HImage(OutImage);
            image.GetImageSize(out imageWidth, out imageHeight);
            switch (_DrawShape)
            {
                case eDrawShape.圆形:
                    HOperatorSet.GenCircle(out ho_temp_brush, imageWidth / 2, imageHeight / 2, DrawSize);
                    if (hv_Row1.D != 0)
                    {
                        if (brushRegion == null)
                        {
                            brushRegion = new HObject();
                        }
                        brushRegion.Dispose();
                        brushRegion = ho_temp_brush;
                    }
                    break;
                case eDrawShape.矩形:
                    HOperatorSet.GenRectangle1(out ho_temp_brush, 0, 0, DrawSize, DrawSize);
                    if (hv_Row1.D != 0)
                    {
                        brushRegion.Dispose();
                        brushRegion = ho_temp_brush;
                    }
                    break;
                default:
                    break;
            }

        }
        /// <summary>
        /// 绘制或者擦除涂抹
        /// </summary>
        /// <param name="editMode"></param>
        private void DrawOrWipe(eEditMode editMode)
        {
            if (editMode == eEditMode.正常显示)
                return;
            if (view == null) return;
            if (finalRegion == null)
                finalRegion = new HObject();
            view.mWindowH.DrawModel = true;
            view.mWindowH.Focus();
            HTuple hv_Button = null;
            HTuple hv_Row = null, hv_Column = null;
            HTuple areaBrush, rowBrush, columnBrush, homMat2D;
            HObject brush_region_affine = new HObject();
            HObject ho_Image = new HObject(OutImage);
            try
            {
                if (brushRegion == null) { SetBurshRegion(); }
                    
                if (!brushRegion.IsInitialized())
                {
                    MessageView.Ins.MessageBoxShow("未设置画刷!", eMsgType.Warn);
                    return;
                }
                else
                {
                    HOperatorSet.AreaCenter(brushRegion, out areaBrush, out rowBrush, out columnBrush);
                }
                string color = "blue";
                //画出笔刷
                switch (editMode)
                {
                    case eEditMode.绘制涂抹:
                        color = "blue";
                        break;
                    case eEditMode.擦除涂抹:
                        color = "red";
                        //检查finalRegion是否有效
                        if (!finalRegion.IsInitialized())
                        {
                            MessageView.Ins.MessageBoxShow("请先涂抹出合适区域,再使用擦除功能!",eMsgType.Warn);
                            return;
                        }
                        break;
                    default:
                        return;
                }
                HOperatorSet.SetColor(view.mWindowH.hv_window, color);
                //显示
                //view.mWindowH.HobjectToHimage(OutImage);
                view.mWindowH.DispObj(contour_xld, "green");
                if (finalRegion.IsInitialized())
                {
                    view.mWindowH.DispObj(finalRegion, color);
                }
                #region "循环,等待涂抹"

                //鼠标状态
                hv_Button = 0;
                // 4为鼠标右键
                while (hv_Button != 4)
                {
                    //一直在循环,需要让halcon控件也响应事件,不然到时候跳出循环,之前的事件会一起爆发触发,
                    Application.DoEvents();
                    hv_Row = -1;
                    hv_Column = -1;
                    //获取鼠标坐标
                    try
                    {
                        HOperatorSet.GetMposition(view.mWindowH.hv_window, out hv_Row, out hv_Column, out hv_Button);
                    }
                    catch (HalconException ex)
                    {
                        hv_Button = 0;
                    }
                    HOperatorSet.SetSystem("flush_graphic", "false");
                    HOperatorSet.DispObj(ho_Image, view.mWindowH.hv_window);
                    view.mWindowH.DispObj(contour_xld, "green");

                    if (finalRegion.IsInitialized())
                    {
                        view.mWindowH.DispObj(finalRegion, color);
                    }
                    //check if mouse cursor is over window
                    if (hv_Row >= 0 && hv_Column >= 0)
                    {
                        //放射变换
                        HOperatorSet.VectorAngleToRigid(rowBrush, columnBrush, 0, hv_Row, hv_Column, 0, out homMat2D);
                        brush_region_affine.Dispose();
                        HOperatorSet.AffineTransRegion(brushRegion, out brush_region_affine, homMat2D, "nearest_neighbor");
                        HOperatorSet.DispObj(brush_region_affine, view.mWindowH.hv_window);
                        HOperatorSet.SetSystem("flush_graphic", "true");
                        ShowTool.SetFont(view.mWindowH.hv_window, 20, "true", "false");
                        ShowTool.SetMsg(view.mWindowH.hv_window, "按下鼠标左键涂抹,右键结束!", "window", 20, 20, "green", "false");
                        //1为鼠标左键
                        if (hv_Button == 1)
                        {

                            //画出笔刷
                            switch (editMode)
                            {
                                case eEditMode.绘制涂抹:
                                    {
                                        if (finalRegion.IsInitialized())
                                        {
                                            HObject ExpTmpOutVar_0;
                                            HOperatorSet.Union2(finalRegion, brush_region_affine, out ExpTmpOutVar_0);
                                            finalRegion.Dispose();
                                            finalRegion = ExpTmpOutVar_0;
                                        }
                                        else
                                        {
                                            finalRegion = new HObject(brush_region_affine);
                                        }

                                    }
                                    break;
                                case eEditMode.擦除涂抹:
                                    {
                                        HObject ExpTmpOutVar_0;
                                        HOperatorSet.Difference(finalRegion, brush_region_affine, out ExpTmpOutVar_0);
                                        finalRegion.Dispose();
                                        finalRegion = ExpTmpOutVar_0;
                                    }
                                    break;
                                default:
                                    return;
                            }
                        }
                    }
                }
                #endregion
            }
            catch (HalconException HDevExpDefaultException)
            {
                throw HDevExpDefaultException;
            }
            finally
            {
                EditMode = eEditMode.正常显示;
                view.mWindowH.ClearROI();
                view.mWindowH.DispObj(finalRegion, "blue");
                view.mWindowH.DispObj(contour_xld, "green");
                view.mWindowH.DrawModel = false;
            }

        }
        public void CreateModel()
        {
            try
            {
                HImage image = new HImage(OutImage);
                if (image == null || !image.IsInitialized())
                {
                    Logger.AddLog($"{MatchingViewModel.ModuleParam.ModuleName}无图像！", eMsgType.Warn);
                    return;
                }
                //这里剪切图片
                //((HShapeModel)MatchingViewModel.ModelImage).CreateScaledShapeModel(image, MatchingViewModel.Levels, StartPhi, EndPhi, "auto", MinScale, MaxScale, "auto", REnum.EnumToStr(Optimization), "use_polarity", "auto", "auto");
                //HOperatorSet.CreateScaledShapeModel(image, MatchingViewModel.Levels, StartPhi, EndPhi, "auto", MinScale, MaxScale, CompType.ToString(), Optimization.ToString(), "auto", "auto", "auto", out HTuple hTuple);
                try
                {

                    HImage Reduceimage = new HImage();
                    if (finalRegion != null && finalRegion.IsInitialized())
                    {
                        HRegion region = new HRegion(finalRegion);
                        HRegion ReduceRegion = image.GetDomain().Difference(region);
                        Reduceimage = image.ReduceDomain(ReduceRegion);
                    }
                    else
                    {
                        Reduceimage = new HImage(image);
                    }
                    int filter = 1;
                    switch (Optimization)
                    {   
                        case eOptimization.粗略:
                            filter = 9;
                            break;
                        case eOptimization.正常:
                            filter = 5;
                            break;
                        case eOptimization.精细:
                            filter = 1;
                            break;
                        default:
                            break;
                    }
                    switch (MatchingViewModel.ModelType)
                    {
                        case eModelType.形状模板:
                            HXLDCont tempcontours = (Reduceimage.EdgesSubPix("canny", filter, 5, Threshold)).SelectContoursXld("contour_length", MinLength, 999999999, -0.5, 0.5).UnionAdjacentContoursXld(10,1,"attr_keep");
                            ((HShapeModel)MatchingViewModel.ModelImage).CreateScaledShapeModelXld(
                                tempcontours, //边缘运算符,滤波器,低阈值，高阈值
                                "auto", //金字塔的层数，可设为“auto”或0—10的整数  5
                                Math.Round(StartPhi * Math.PI / 180, 3), //模板旋转的起始角度     HTuple(-45).Rad()
                                Math.Round((EndPhi - StartPhi) * Math.PI / 180, 3), //模板旋转角度范围, >=0     HTuple(360).Rad()
                                "auto", //旋转角度的步长， >=0 and <=pi/16   auto
                                MinScale, //模板最小比例 0.9
                                MaxScale, //模板最大比例   1.1
                                "auto", //模板比例的步长  "auto"
                                "none", //设置模板优化和模板创建方法  none
                                REnum.EnumToStr(
                                    CompType), //匹配方法设置: ignore_color_polarity"忽略颜色极性  "ignore_global_polarity"忽视全部极性  "ignore_local_polarity"无视局部极性 "use_polarity"使用极性
                                5
                            );
                            break;
                        case eModelType.灰度模板:
                            ((HNCCModel)MatchingViewModel.ModelImage).CreateNccModel(
                                image,
                                "auto",
                                Math.Round(StartPhi * Math.PI / 180, 3),
                                Math.Round((EndPhi - StartPhi) * Math.PI / 180, 3),
                                "auto",
                                "use_polarity"
                            );
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageView.Ins.MessageBoxShow("图像对比度太低，无法创建模版!", eMsgType.Error);
                    //MessageBox.Show(ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
                //Find.CreateModel(MatchingViewModel.ModelType, image, finalRegion, Threshold, MatchingViewModel.Levels, StartPhi, EndPhi, MinScale, MaxScale, CompType, Optimization, ref MatchingViewModel.ModelImage);
                int mathNum = int.Parse(MatchingViewModel.GetLinkValue(MatchingViewModel.MathNum).ToString());
                Logger.AddLog(MatchingViewModel.ModuleParam.ModuleName + ":创建模板成功！");

            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }
        public void FindModel()
        {
            int mathNum = int.Parse(MatchingViewModel.GetLinkValue(MatchingViewModel.MathNum).ToString());
            HImage image = new HImage(OutImage);
            if (Find.FindModel(MatchingViewModel.ModelType, image, MatchingViewModel.ModelImage, MatchingViewModel.MinScore, mathNum, MatchingViewModel.MaxOverlap, MatchingViewModel.GreedDeg, out MatchingViewModel.MathCoord) > 0)
            {
                //仿射变换-检测结果
                HTuple tempMat2D = new HTuple();
                HOperatorSet.VectorAngleToRigid(0, 0, 0, MatchingViewModel.MathCoord.Y, MatchingViewModel.MathCoord.X, MatchingViewModel.MathCoord.Phi, out tempMat2D);
                //检测结果-对XLD应用任意加法 2D 变换
                contour_xld = ((HShapeModel)MatchingViewModel.ModelImage).GetShapeModelContours(1).AffineTransContourXld(new HHomMat2D(tempMat2D));
                view.mWindowH.HobjectToHimage(OutImage);
                if (finalRegion!=null && finalRegion.IsInitialized())
                {
                    view.mWindowH.DispObj(finalRegion, "blue");
                }
                view.mWindowH.DispObj(contour_xld, "green");
                view.mWindowH.DispObj(Gen.GetCoord(new RImage(image), MatchingViewModel.MathCoord), "red");
                HOperatorSet.GenCrossContourXld(
                        out HObject cross,
                        MatchingViewModel.MathCoord.Y,
                        MatchingViewModel.MathCoord.X,
                        10,
                        MatchingViewModel.MathCoord.Phi
                    );
                view.mWindowH.DispObj(cross, "cyan");
            }

        }
        #endregion
    }
}
