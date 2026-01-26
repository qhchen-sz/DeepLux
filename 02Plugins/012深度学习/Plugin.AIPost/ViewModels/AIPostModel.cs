using EventMgrLib;
using HalconDotNet;
using Plugin.AIPost.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
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
using System.Collections.ObjectModel;
using Plugin.AIPost.Model;
using HandyControl.Controls;
using System.Data.Common;

namespace Plugin.AIPost.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        InputRegionLink,
        AreaUpLimt,
        AreaDnLimt,
        WidthUpLimt,
        WidthDnLimt,
        HeightUpLimt,
        HeightDnLimt

    }
    public enum eAiColor
    {
        red = 0,
        blue = 1,
        green = 2,
        cyan = 3,
        yellow = 4,
        coral = 5,
        orange = 6,
        pink = 7,

    }
    #endregion
    [Category("深度学习")]
    [DisplayName("AI后处理")]
    [ModuleImageName("AIPost")]
    [Serializable]
    public class AIPostModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }
            InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText, true);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    for (int i = 0; i < DefectVar.Count; i++)
                    {
                        if (ParaModelAll[DefectVar[i].Index].UseArea)
                        {
                            int AreaUpLimt = Convert.ToInt32( GetLinkValue(ParaModelAll[DefectVar[i].Index].AreaUpLimtstr));
                            int AreaDnLimt = Convert.ToInt32(GetLinkValue(ParaModelAll[DefectVar[i].Index].AreaDnLimtstr));
                            if (DefectVar[i].DefectArea > AreaUpLimt || DefectVar[i].DefectArea < AreaDnLimt)
                            {

                                ChangeModuleRunStatus(eRunStatus.NG);
                                return false;
                            }
                        }
                        else if (DefectVar[i].DefectArea != 0)
                        {
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }
                            
                        if (ParaModelAll[DefectVar[i].Index].UseWidth)
                        {
                            int WidthUpLimt = Convert.ToInt32(GetLinkValue(ParaModelAll[DefectVar[i].Index].WidthUpLimtstr));
                            int WidthDnLimt = Convert.ToInt32(GetLinkValue(ParaModelAll[DefectVar[i].Index].WidthDnLimtstr));
                            if (DefectVar[i].DefectWidth > WidthUpLimt || DefectVar[i].DefectWidth < WidthDnLimt)
                            {
                                ChangeModuleRunStatus(eRunStatus.OK);
                                return true;
                            }
                        }
                        else if (DefectVar[i].DefectWidth != 0)
                        {
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }
                        if (ParaModelAll[DefectVar[i].Index].UseHeight)
                        {
                            int HeightUpLimt = Convert.ToInt32(GetLinkValue(ParaModelAll[DefectVar[i].Index].HeightUpLimtstr));
                            int HeightDnLimt = Convert.ToInt32(GetLinkValue(ParaModelAll[DefectVar[i].Index].HeightDnLimtstr));
                            if (DefectVar[i].DefectHeight > HeightUpLimt || DefectVar[i].DefectWidth < HeightDnLimt)
                            {
                                ChangeModuleRunStatus(eRunStatus.OK);
                                return true;
                            }
                        }
                        else if (DefectVar[i].DefectHeight != 0)
                        {
                            ChangeModuleRunStatus(eRunStatus.NG);
                            return false;
                        }


                    }

                        ChangeModuleRunStatus(eRunStatus.OK);
                        return true;


                }
                else
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
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
            AddOutputParam("结果", "bool", Result);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop

        private List<string> _AiClassSource;
        private string _AiClass;
        /// <summary>AI类型</summary>
        public List<string> AiClassSource
        {
            get { return _AiClassSource; }
            set { Set(ref _AiClassSource, value); }
        }
        public string AiClass
        {
            get { return _AiClass; }
            set { _AiClass = value; 
                if(value != null)
                {
                    int index = AiClassSource.FindIndex(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
                    ParaModel = ParaModelAll[index];
                }

                RaisePropertyChanged(); 
            }
        }
        private bool _Result;
        /// <summary>结果</summary>
        public bool Result
        {
            get { return _Result; }
            set { Set(ref _Result, value); }
        }
        private string _InputImageLinkText;
        private string _InputRegionLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }
        public string InputRegionLinkText
        {
            get { return _InputRegionLinkText; }
            set { Set(ref _InputRegionLinkText, value); }
            
        }
        private DefectVarModel _SelectDefectVar;
        public DefectVarModel SelectDefectVar
        {
            get { return _SelectDefectVar; }
            set { _SelectDefectVar = value;
                if(value != null)
                {
                    ClearRoiAndText();
                    GetDispImage(InputImageLinkText);
                    eAiColor color = (eAiColor)value.Index;
                    string dispstr = "缺陷:" + (value.Index + 1);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + value.Index, ModuleParam.Remarks, HRoiType.检测结果, color.ToString(), new HObject(value.region)));
                    value.region.AreaCenter(out double Row, out double Column);
                    ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName + value.Index, ModuleParam.Remarks, HRoiType.文字显示, color.ToString(), dispstr, Column, Row, 16));
                    ShowHRoi();
                }
                
                RaisePropertyChanged(); }
        }
        private ObservableCollection<DefectVarModel> _DefectVar;
        public ObservableCollection<DefectVarModel> DefectVar
        {
            get { return _DefectVar; }
            set { _DefectVar = value; RaisePropertyChanged(); }
        }



        private ParaModel _ParaModel;
        public ParaModel ParaModel
        {
            get { return _ParaModel; }
            set { _ParaModel = value; RaisePropertyChanged(); }
        }
        private ObservableCollection<ParaModel> _ParaModelAll;
        public ObservableCollection<ParaModel> ParaModelAll
        {
            get { return _ParaModelAll; }
            set { _ParaModelAll = value; RaisePropertyChanged(); }
        }
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as AIPostView;
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
                GetDispImage(InputImageLinkText, true);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    ShowHRoi();
                }
            }
        }
        [NonSerialized]
        private CommandBase _DispAllRegion;
        public CommandBase DispAllRegion
        {
            get
            {
                if (_DispAllRegion == null)
                {
                    _DispAllRegion = new CommandBase((obj) =>
                    {
                        var ee = GetLinkValue(InputRegionLinkText);
                        if (ee is HRegion region)
                        {
                            int count = region.CountObj();
                            ClearRoiAndText();
                            GetDispImage(InputImageLinkText);
                            for (int i = 1; i <= count; i++)
                            {
                                HRegion temp = region.SelectObj(i);
                                double area = temp.AreaCenter(out double Row, out double Column);
                                if (area != 0)
                                {
                                    string dispstr = "类别:" + (i);
                                    eAiColor color = (eAiColor)(i-1);
                                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + i, ModuleParam.Remarks, HRoiType.检测结果, color.ToString(), new HObject(temp)));
                                    ShowHRoi(new HText(ModuleParam.ModuleEncode, ModuleParam.ModuleName + i, ModuleParam.Remarks, HRoiType.文字显示, color.ToString(), dispstr, Column, Row, 16));
                                    
                                }
                            }
                            ShowHRoi();
                        }
                    });
                }
                return _DispAllRegion;
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
                        var view = this.ModuleView as AIPostView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "InputRegionLink":
                    InputRegionLinkText = obj.LinkName;
                    var ee = GetLinkValue(InputRegionLinkText);
                    if(ee is HRegion region)
                    {
                        int count = region.CountObj();
                        AiClassSource = new List<string>();
                        ParaModelAll = new ObservableCollection<ParaModel>();
                        DefectVar = new ObservableCollection<DefectVarModel>();
                        for (int i = 0; i < count; i++)
                        {
                             HRegion temp = region.SelectObj(i+1);
                            temp = temp.Connection();
                            int count2 = temp.CountObj();
                            for (int j = 1; j <= count2; j++)
                            {
                                HRegion hRegion = temp.SelectObj(j);
                                HTuple para = hRegion.RegionFeatures(((new HTuple("area")).TupleConcat("width")).TupleConcat("height"));
                                int area = Convert.ToInt32((double)para[0]);
                                int width = Convert.ToInt32((double)para[1]);
                                int height = Convert.ToInt32((double)para[2]);
                                if (area != 0)
                                {
                                    DefectVar.Add(new DefectVarModel()
                                    {
                                        Index = i,
                                        region = new HRegion( hRegion),
                                        DefectArea = area,
                                        DefectWidth = width,
                                        DefectHeight = height,
                                        DefectType = "缺陷" + (i + 1)
                                    }) ;
                                }
                            }


                            AiClassSource.Add("缺陷" + (i + 1));
                            ParaModelAll.Add(new ParaModel());
                        }
                        ParaModel = ParaModelAll[0];
                        AiClass = AiClassSource[0];
                    }
                    break;
                case "AreaUpLimt":
                    ParaModel.AreaUpLimtstr = obj.LinkName;
                    break;
                case "AreaDnLimt":
                    ParaModel.AreaDnLimtstr = obj.LinkName;
                    break;
                case "WidthUpLimt":
                    ParaModel.WidthUpLimtstr = obj.LinkName;
                    break;
                case "WidthDnLimt":
                    ParaModel.WidthDnLimtstr = obj.LinkName;
                    break;
                case "HeightUpLimt":
                    ParaModel.HeightUpLimtstr = obj.LinkName;
                    break;
                case "HeightDnLimt":
                    ParaModel.HeightDnLimtstr = obj.LinkName;
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
                            case eLinkCommand.InputRegionLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRegionLink");
                                break;
                            case eLinkCommand.AreaUpLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},AreaUpLimt");
                                break;
                            case eLinkCommand.AreaDnLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},AreaDnLimt");
                                break;
                            case eLinkCommand.WidthUpLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},WidthUpLimt");
                                break;
                            case eLinkCommand.WidthDnLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},WidthDnLimt");
                                break;
                            case eLinkCommand.HeightUpLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},HeightUpLimt");
                                break;
                            case eLinkCommand.HeightDnLimt:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},HeightDnLimt");
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        #endregion
        #region Method
        //private void ShowHRoi(bool ShowLine=false)
        //{
        //    var view = ModuleView as DistancePPView;
        //    VMHWindowControl mWindowH;
        //    if (view == null || view.IsClosed)
        //    {
        //        mWindowH = ViewDic.GetView(DispImage.DispViewID);
        //    }
        //    else
        //    {
        //        mWindowH = view.mWindowH;
        //    }
        //    //mWindowH.ClearWindow();
        //    mWindowH.HobjectToHimage(DispImage);
        //    HObject hObject = new HObject();
        //    hObject.GenEmptyObj();
        //    hObject.Dispose();
        //    HOperatorSet.GenRegionLine(out hObject, P1YLinkValue, P1XLinkValue, P2YLinkValue, P2XLinkValue);
        //    mWindowH.DispObj(hObject);
        //    hObject.Dispose();
        //    List<HRoi> roiList = DispImage.mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
        //    foreach (HRoi roi in roiList)
        //    {
        //        if (roi.roiType == HRoiType.文字显示)
        //        {
        //            HText roiText = (HText)roi;
        //            ShowTool.SetFont(mWindowH.hControl.HalconWindow, roiText.size, "false", "false");
        //            ShowTool.SetMsg(mWindowH.hControl.HalconWindow, roiText.text, "image", roiText.row, roiText.col, roiText.drawColor, "false");
        //        }
        //        else
        //        {
        //            mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
        //        }
        //    }
        //}
        #endregion
    }
}
