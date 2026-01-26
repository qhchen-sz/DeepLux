using EventMgrLib;
using HalconDotNet;
using Plugin.MPHA.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Documents;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using static Plugin.MPHA.ViewModels.MPHA;

namespace Plugin.MPHA.ViewModels
{
    #region enum
    public enum RoiParaType
    {
        区域,
        数组
    }

    #endregion

    [Category("密封钉")]
    [DisplayName("翘钉算法")]
    [ModuleImageName("MPHA")]
    [Serializable]
    public class MPHAViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (InputImageLinkText == null)
            {
                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
                if (moduls == null || moduls.VarModels.Count == 0)
                {
                    return;
                }
                if (InputImageLinkText == null)
                    InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
            }
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
       
            try
            {
                MPHeight = 0;
                MPAngel = 0;
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                var view = ModuleView as MPHAView;
                if (IsOpenWindows)
                {
                    HOperatorSet.Decompose2(DispImage, out HObject imageH, out HObject imageG);
                    MPHA.Create3DRGB(imageH, imageG, out HObject multimage, "精细");
                    view.mWindowH.DispObj(multimage);
                }
                if (DispImage.Type == "3D")
                {


/*                    HObject Region1 = new HObject();
                    HObject Region2 = new HObject();
                    if(SelectedROIType == RoiParaType.区域)
                    {
                        HOperatorSet.GenEmptyObj(out HObject Region1_1);
                        HOperatorSet.GenEmptyObj(out HObject Region1_2);
                        HOperatorSet.GenEmptyObj(out HObject Region1_3);
                        HOperatorSet.GenEmptyObj(out HObject Region1_4);
                        if (GetLinkValue(TopSurfaceRegionText1) is HObject tem1)
                        {
                            Region1_1 = tem1;
                        }
                        if (GetLinkValue(TopSurfaceRegionText2) is HObject tem2)
                        {
                            Region1_2 = tem2;
                        }
                        if (GetLinkValue(TopSurfaceRegionText3) is HObject tem3)
                        {
                            Region1_3 = tem3;
                        }
                        if (GetLinkValue(TopSurfaceRegionText4) is HObject tem4)
                        {
                            Region1_4 = tem4;
                        }
                        //HObject Region1_2 = (HObject)GetLinkValue(TopSurfaceRegionText2);
                        //HObject Region1_3 = (HObject)GetLinkValue(TopSurfaceRegionText3);
                        //HObject Region1_4 = (HObject)GetLinkValue(TopSurfaceRegionText4);
                        HOperatorSet.Union2(Region1_1, Region1_2, out Region1);
                        HOperatorSet.Union2(Region1_3, Region1, out Region1);
                        HOperatorSet.Union2(Region1_4, Region1, out Region1);

                        Region2 = (HObject)GetLinkValue(NailSurfaceRegionText);
                    }
                    else if(SelectedROIType == RoiParaType.数组)
                    {
                        int.TryParse( GetLinkValue(TopSurfaceXText).ToString(),out int TopSurfaceX);
                        int.TryParse(GetLinkValue(TopSurfaceYText).ToString(), out int TopSurfaceY);
                        int.TryParse(GetLinkValue(TopSurfaceWidthText).ToString(), out int TopSurfaceWidth);
                        int.TryParse(GetLinkValue(TopSurfaceHeightText).ToString(), out int TopSurfaceHeight);

                        int.TryParse(GetLinkValue(NailSurfaceX1Text).ToString(), out int NailSurfaceX1);
                        int.TryParse(GetLinkValue(NailSurfaceY1Text).ToString(), out int NailSurfaceY1);
                        int.TryParse(GetLinkValue(NailSurfaceX2Text).ToString(), out int NailSurfaceX2);
                        int.TryParse(GetLinkValue(NailSurfaceY2Text).ToString(), out int NailSurfaceY2);

                        HOperatorSet.GetImageSize(DispImage,out HTuple width,out HTuple height);
                        HOperatorSet.GenRectangle1(out HObject Region1_1, TopSurfaceY, TopSurfaceX, TopSurfaceY+TopSurfaceHeight, TopSurfaceX+TopSurfaceWidth);
                        HOperatorSet.GenRectangle1(out HObject Region1_2, TopSurfaceY, width- TopSurfaceX- TopSurfaceWidth, TopSurfaceY + TopSurfaceHeight, width - TopSurfaceX);
                        HOperatorSet.GenRectangle1(out HObject Region1_3, height - TopSurfaceY- TopSurfaceHeight, width - TopSurfaceX - TopSurfaceWidth, height - TopSurfaceY, width - TopSurfaceX);
                        HOperatorSet.GenRectangle1(out HObject Region1_4, height - TopSurfaceY - TopSurfaceHeight, TopSurfaceX, height - TopSurfaceY, TopSurfaceX + TopSurfaceWidth);

                        HOperatorSet.Union2(Region1_1, Region1_2, out Region1);
                        HOperatorSet.Union2(Region1_3, Region1, out Region1);
                        HOperatorSet.Union2(Region1_4, Region1, out Region1);
                        HOperatorSet.GenRectangle1(out Region2, NailSurfaceY1, NailSurfaceX1, NailSurfaceY2, NailSurfaceX2);
                    }
*/
                    //        HOperatorSet.Connection(Region1, out HObject hObject);
                    //        HOperatorSet.AreaCenter(hObject, out HTuple area, out HTuple Y, out HTuple X);
                    //        HOperatorSet.GetGrayval(DispImage.Decompose2(out HImage temp), Y, X, out HTuple Z);
                    //        CornerPoints cornerPoints = new CornerPoints()
                    //        {
                    //            x = X.DArr,
                    //            y = Y.DArr,
                    //            z = Z.DArr
                    //        };
                    //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName+"顶盖区域", ModuleParam.Remarks, HRoiType.检测范围, "blue", Region1));
                    //        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName+"钉面区域", ModuleParam.Remarks, HRoiType.检测范围, "red", Region2));
                    //        MPHA.Send3DPindiskPoints(DispImage, Region1, Region2,
                    //out HTuple hv_PointXCen, out HTuple hv_PointYCen, out HTuple hv_PointZCen);
                    //        if(hv_PointXCen .Length== 0)
                    //        {
                    //            ChangeModuleRunStatus(eRunStatus.NG);
                    //            return false;
                    //        }
                    MPHA.HalconToImgPara(DispImage, out MPHA.ImgPara img_para);


                    MPHA.Vector3d transformationMatrix = new MPHA.Vector3d(1, 1, MPScale);
                    FuncPara funcPara = new FuncPara()
                    {
                        radius = MPSerachRadius,//搜索半径
                        normal_degree = MPNormal_Degree,//最小搜索角度
                        curvature_threshold = Curvature_Threshold,//曲率阈值
                        use_curvature = Use_Curvature,//曲率开关
                        central_plane_size = Central_Plane_Size,//钉面区域
                        distance_threshold = MPDistance_Threshold,//噪声阈值
                        min_planar_points = MPMin_Planar_Points,//最小面需要多少点
                    };
                    MPHA.ResultParaPindisk result;
                    //MPHA.TiffPara tiff_para;
                    //tiff_para.clos = 625;
                    //tiff_para.rows = 645;
                    bool run_flag = MPHA.RunMPHA(ref img_para, ref funcPara, ref transformationMatrix, out result);


/*                    RunMPHA(ref cornerPoints, hv_PointXCen.DArr, hv_PointYCen.DArr, hv_PointZCen.DArr, ref funcPara, ref transformationMatrix, out result);*/


                    MPHeight = Math.Round(result.plane_height_gap / MPScale, 3);
                    MPAngel = Math.Round(result.plane_angle, 3);
                    ShowHRoi();

                }
                else
                {

                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                VMHWindowControl mWindowH;
                if (!IsOpenWindows)
                {
                    mWindowH = ViewDic.GetView(DispImage.DispViewID);
                }
                else
                {
                    mWindowH = view.mWindowH;
                }
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
                
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }
        public override void AddOutputParams()
        {
            //OutLine.Status == eRunStatus.OK ? true : false; 
            this.Prj.ClearOutputParam(this.ModuleParam);
            AddOutputParam("钉面高度", "double", MPHeight);
            AddOutputParam("钉面角度", "double", MPAngel);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop

        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    ShowHRoi();
                }
            }
        }

        private string _TopSurfaceRegionText1;
        /// <summary>
        /// 输入顶盖区域链接文本
        /// </summary>
        public string TopSurfaceRegionText1
        {
            get { return _TopSurfaceRegionText1; }
            set
            {
                _TopSurfaceRegionText1 = value;
                RaisePropertyChanged();
            }
        }
        private string _TopSurfaceRegionText2;
        /// <summary>
        /// 输入顶盖区域链接文本
        /// </summary>
        public string TopSurfaceRegionText2
        {
            get { return _TopSurfaceRegionText2; }
            set
            {
                _TopSurfaceRegionText2 = value;
                RaisePropertyChanged();
            }
        }
        private string _TopSurfaceRegionText3;
        /// <summary>
        /// 输入顶盖区域链接文本
        /// </summary>
        public string TopSurfaceRegionText3
        {
            get { return _TopSurfaceRegionText3; }
            set
            {
                _TopSurfaceRegionText3 = value;
                RaisePropertyChanged();
            }
        }
        private string _TopSurfaceRegionText4;
        /// <summary>
        /// 输入顶盖区域链接文本
        /// </summary>
        public string TopSurfaceRegionText4
        {
            get { return _TopSurfaceRegionText4; }
            set
            {
                _TopSurfaceRegionText4 = value;
                RaisePropertyChanged();
            }
        }
        private string _NailSurfaceRegionText;
        /// <summary>
        /// 输入钉面区域链接文本
        /// </summary>
        public string NailSurfaceRegionText
        {
            get { return _NailSurfaceRegionText; }
            set
            {
                _NailSurfaceRegionText = value;
                RaisePropertyChanged();
            }
        }

        private string _TopSurfaceXText="10";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string TopSurfaceXText
        {
            get { return _TopSurfaceXText; }
            set
            {
                _TopSurfaceXText = value;
                RaisePropertyChanged();
            }
        }

        private string _TopSurfaceYText="10";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string TopSurfaceYText
        {
            get { return _TopSurfaceYText; }
            set
            {
                _TopSurfaceYText = value;
                RaisePropertyChanged();
            }
        }

        private string _TopSurfaceWidthText = "50";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string TopSurfaceWidthText
        {
            get { return _TopSurfaceWidthText; }
            set
            {
                _TopSurfaceWidthText = value;
                RaisePropertyChanged();
            }
        }

        private string _TopSurfaceHeightText = "50";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string TopSurfaceHeightText
        {
            get { return _TopSurfaceHeightText; }
            set
            {
                _TopSurfaceHeightText = value;
                RaisePropertyChanged();
            }
        }


        private string _NailSurfaceX1Text = "269";
        /// <summary>
        /// 输入钉面数组链接文本
        /// </summary>
        public string NailSurfaceX1Text
        {
            get { return _NailSurfaceX1Text; }
            set
            {
                _NailSurfaceX1Text = value;
                RaisePropertyChanged();
            }
        }

        private string _NailSurfaceY1Text = "286";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string NailSurfaceY1Text
        {
            get { return _NailSurfaceY1Text; }
            set
            {
                _NailSurfaceY1Text = value;
                RaisePropertyChanged();
            }
        }

        private string _NailSurfaceX2Text = "348";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string NailSurfaceX2Text
        {
            get { return _NailSurfaceX2Text; }
            set
            {
                _NailSurfaceX2Text = value;
                RaisePropertyChanged();
            }
        }

        private string _NailSurfaceY2Text = "349";
        /// <summary>
        /// 输入顶盖数组链接文本
        /// </summary>
        public string NailSurfaceY2Text
        {
            get { return _NailSurfaceY2Text; }
            set
            {
                _NailSurfaceY2Text = value;
                RaisePropertyChanged();
            }
        }
        #endregion
        private bool IsLoad = false;

        private double _MPHeight = 0;
        public double MPHeight
        {
            get { return _MPHeight; }
            set
            {
                _MPHeight = value;
                RaisePropertyChanged();
            }
        }

        private double _MPAngel = 0;
        public double MPAngel
        {
            get { return _MPAngel; }
            set
            {
                _MPAngel = value;
                RaisePropertyChanged();
            }
        }

        private int _MPScale = 200;
        public int MPScale
        {
            get { return _MPScale; }
            set
            {
                _MPScale = value;
                RaisePropertyChanged();
            }
        }
        private float _MPSerachRadius = 5.0f;
        public float MPSerachRadius
        {
            get { return _MPSerachRadius; }
            set
            {
                _MPSerachRadius = value;
                RaisePropertyChanged();
            }
        }
        private float _MPNormal_Degree = 1.0f;
        public float MPNormal_Degree
        {
            get { return _MPNormal_Degree; }
            set
            {
                _MPNormal_Degree = value;
                RaisePropertyChanged();
            }
        }

        private float _Curvature_Threshold = 0.0f;
        public float Curvature_Threshold
        {
            get { return _Curvature_Threshold; }
            set
            {
                _Curvature_Threshold = value;
                RaisePropertyChanged();
            }
        }

        private bool _Use_Curvature = false;
        public bool Use_Curvature
        {
            get { return _Use_Curvature; }
            set
            {
                _Use_Curvature = value;
                RaisePropertyChanged();
            }
        }

        private float _Central_Plane_Size = 75;
        public float Central_Plane_Size
        {
            get { return _Central_Plane_Size; }
            set
            {
                _Central_Plane_Size = value;
                RaisePropertyChanged();
            }
        }


        private float _MPDistance_Threshold = 0.0f;
        public float MPDistance_Threshold
        {
            get { return _MPDistance_Threshold; }
            set
            {
                _MPDistance_Threshold = value;
                RaisePropertyChanged();
            }
        }
        private int _MPMin_Planar_Points = 100;
        public int MPMin_Planar_Points
        {
            get { return _MPMin_Planar_Points; }
            set
            {
                _MPMin_Planar_Points = value;
                RaisePropertyChanged();
            }
        }

        private RoiParaType _SelectedROIType = RoiParaType.区域;
        public RoiParaType SelectedROIType
        {
            get { return _SelectedROIType; }
            set
            {
                _SelectedROIType = value;
                RaisePropertyChanged();
            }
        }

        #region Command
        public override void Loaded()
        {
            IsLoad = true;
            base.Loaded();
            var view = ModuleView as MPHAView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    HOperatorSet.Decompose2(DispImage, out HObject imageH, out HObject imageG);
                    MPHA.Create3DRGB(imageH, imageG, out HObject multimage, "精细");
                    view.mWindowH.DispObj(multimage);
                    //view.mWindowH.HobjectToHimage(DispImage);

                    //ShowHRoi();
                }
            }
            IsLoad = false;
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceRegionLink1:
                    TopSurfaceRegionText1 = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceRegionLink2:
                    TopSurfaceRegionText2 = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceRegionLink3:
                    TopSurfaceRegionText3 = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceRegionLink4:
                    TopSurfaceRegionText4 = obj.LinkName;
                    break;
                case eLinkCommand.NailSurfaceRegionLink:
                    NailSurfaceRegionText = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceXTextLink:
                    TopSurfaceXText = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceYTextLink:
                    TopSurfaceYText = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceWidthTextLink:
                    TopSurfaceWidthText = obj.LinkName;
                    break;
                case eLinkCommand.TopSurfaceHeightTextLink:
                    TopSurfaceHeightText = obj.LinkName;
                    break;
                default:
                    break;
            }
        }
        [NonSerialized]
        private CommandBase _LinkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.TopSurfaceRegionLink1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceRegionLink1");
                                break;
                            case eLinkCommand.TopSurfaceRegionLink2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceRegionLink2");
                                break;
                            case eLinkCommand.TopSurfaceRegionLink3:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceRegionLink3");
                                break;
                            case eLinkCommand.TopSurfaceRegionLink4:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceRegionLink4");
                                break;
                            case eLinkCommand.NailSurfaceRegionLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},NailSurfaceRegionLink");
                                break;
                            case eLinkCommand.TopSurfaceXTextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceXTextLink");
                                break;
                            case eLinkCommand.TopSurfaceYTextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceYTextLink");
                                break;
                            case eLinkCommand.TopSurfaceWidthTextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceWidthTextLink");
                                break;
                            case eLinkCommand.TopSurfaceHeightTextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},TopSurfaceHeightTextLink");
                                break;
                            case eLinkCommand.NailSurfaceX1TextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},NailSurfaceX1TextLink");
                                break;
                            case eLinkCommand.NailSurfaceY1TextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},NailSurfaceY1TextLink");
                                break;
                            case eLinkCommand.NailSurfaceX2TextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},NailSurfaceX2TextLink");
                                break;
                            case eLinkCommand.NailSurfaceY2TextLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},NailSurfaceY2TextLink");
                                break;
                            default:
                                break;
                        }

                    });
                }
                return _LinkCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ExecuteCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase((obj) =>
                    {

                        ExeModule();
                    });
                }
                return _ExecuteCommand;
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
                        var view = this.ModuleView as MPHAView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        #endregion

        #region Method

        #endregion
    }
}
