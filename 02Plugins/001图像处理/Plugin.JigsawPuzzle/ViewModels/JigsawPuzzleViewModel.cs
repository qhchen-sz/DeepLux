using DMSkin.Socket;
using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.GrabImage.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.ModelBinding;
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
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using Plugin.JigsawPuzzle.Views;

namespace Plugin.JigsawPuzzle.ViewModels
{
    #region enum

    public enum eLinkCommand
    {
        InputImageLink,
        MathNum
    }
    public enum eROIMatrix
    {
        手动输入,
        链接数组
    }

    #endregion

    [Category("图像处理")]
    [DisplayName("矩形阵列")]
    [ModuleImageName("JigsawPuzzle")]
    [Serializable]
    public class JigsawPuzzleViewModel : ModuleBase
    {
        private List<Coord_Info> coord_Infos = new List<Coord_Info>();

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
        [NonSerialized]
        HRegion OutRegion = new HRegion();
        [NonSerialized]
        List<RImage> OutImage = new List<RImage>();
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，未链接图像源！",
                        eMsgType.Warn
                    );
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                //HRegion hRegion = new HRegion();
                GetDispImage(InputImageLinkText);
                ShowHRoiList();
                ShowHTextList(false);
                OutRegion = new HRegion();
                OutRegion.GenEmptyObj();
                OutImage = new List<RImage>();
                foreach (var item in DataList)
                {
                    HRegion rect = new HRegion();
                    if (CheckList.IsOutRoiImageChecked)
                    {
                        RImage rImage = new RImage(DispImage);
                        rImage.CropRectangle2(item.Y, item.X, item.Deg / 180 * Math.PI, item.L1, item.L2, "true", "constant");
                        OutImage.Add(rImage);
                    }

                    rect.GenRectangle2(item.Y, item.X, item.Deg / 180 * Math.PI, item.L1, item.L2);
                    
                    OutRegion = OutRegion.ConcatObj(rect);
                }

                
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                    
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                //Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            AddOutputParam("区域", "HRegion", OutRegion);
            AddOutputParam("截取图像", "HImage[]", OutImage);
        }

        #region Prop
        private eROIMatrix _SearchRegionSource = eROIMatrix.手动输入;
        public eROIMatrix SearchRegionSource
        {
            get { return _SearchRegionSource; }
            set { Set(ref _SearchRegionSource, value); }
        }
        private List<HRegion> OutRegionList = new List<HRegion>(); 
        /// <summary>
        /// 组件选中
        /// </summary>
        private JigsawPuzzleCheckBox _checkList = new JigsawPuzzleCheckBox();

        public JigsawPuzzleCheckBox CheckList
        {
            get { return _checkList; }
            set { Set(ref _checkList, value); }
        }

        private ObservableCollection<JigsawPuzzleData> _dataList = new ObservableCollection<JigsawPuzzleData>();

        /// <summary>
        /// 数据列表
        /// </summary>
        public ObservableCollection<JigsawPuzzleData> DataList
        {
            get { return _dataList; }
            set
            {
                _dataList = value;
                RaisePropertyChanged();
            }
        }

        private JigsawPuzzleData _JigsawPuzzleData;

        public JigsawPuzzleData JigsawPuzzleData
        {
            get { return _JigsawPuzzleData; }
            set
            {
                _JigsawPuzzleData = value;

                RaisePropertyChanged();
            }
        }



        /// <summary> 区域列表 </summary>
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();



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
                    /* ShowHRoi();*/
                }
            }
        }


        /// <summary>
        /// 输入图像链接文本
        /// </summary>

        #endregion

        #region Command

        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as JigsawPuzzleView;
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
                    if (InputImageLinkText == null)
                        return;
                }

                GetDispImage(InputImageLinkText);
                view.mWindowH.DispObj(DispImage);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                    view.mWindowH.hControl.MouseDown += HControl_MouseDown;
                    view.mWindowH.hControl.MouseMove += HControl_MouseMove;
                    view.mWindowH.hControl.MouseWheel += HControl_MouseWheel;
                }

                ShowHRoiList();
                ShowHTextList(false);
            }
        }


        [NonSerialized] private CommandBase _AddDataCommand;

        public CommandBase AddDataCommand
        {
            get
            {
                if (_AddDataCommand == null)
                {
                    _AddDataCommand = new CommandBase(
                        (obj) => { AddData(); }
                    );
                }

                return _AddDataCommand;
            }
        }

        [NonSerialized] private CommandBase _DelDataCommand;

        public CommandBase DelDataCommand
        {
            get
            {
                if (_DelDataCommand == null)
                {
                    _DelDataCommand = new CommandBase(
                        (obj) => { DelData(JigsawPuzzleData); }
                    );
                }

                return _DelDataCommand;
            }
        }

        [NonSerialized] private CommandBase _OperateCommand;

        public CommandBase OperateCommand
        {
            get
            {
                if (_OperateCommand == null)
                {
                    _OperateCommand = new CommandBase(
                        (obj) =>
                        {
                            try
                            {
                            }
                            catch (Exception ex)
                            {
                                Logger.GetExceptionMsg(ex);
                            }
                        }
                    );
                }

                return _OperateCommand;
            }
        }

        [NonSerialized] private CommandBase _ExecuteCommand;

        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase(
                        (obj) => { ExeModule(); }
                    );
                }

                return _ExecuteCommand;
            }
        }

        [NonSerialized] private CommandBase _ConfirmCommand;

        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase(
                        (obj) =>
                        {
                            var view = this.ModuleView as JigsawPuzzleView;
                            if (view != null)
                            {
                                view.Close();
                            }
                        }
                    );
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
                default:
                    break;
            }
        }

        [NonSerialized] private CommandBase _LinkCommand;

        public CommandBase LinkCommand
        {
            get
            {
                if (_LinkCommand == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins
                        .GetEvent<VarChangedEvent>()
                        .Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase(
                        (obj) =>
                        {
                            eLinkCommand linkCommand = (eLinkCommand)obj;
                            switch (linkCommand)
                            {
                                case eLinkCommand.InputImageLink:
                                    CommonMethods.GetModuleList(
                                        ModuleParam,
                                        VarLinkViewModel.Ins.Modules,
                                        "HImage"
                                    );
                                    EventMgr.Ins
                                        .GetEvent<OpenVarLinkViewEvent>()
                                        .Publish($"{ModuleGuid},InputImageLink");
                                    break;
                                case eLinkCommand.MathNum:
                                    CommonMethods.GetModuleList(
                                        ModuleParam,
                                        VarLinkViewModel.Ins.Modules,
                                        "int"
                                    );
                                    EventMgr.Ins
                                        .GetEvent<OpenVarLinkViewEvent>()
                                        .Publish($"{ModuleGuid},MathNumLink");
                                    break;
                                default:
                                    break;
                            }
                        }
                    );
                }

                return _LinkCommand;
            }
        }

        #endregion

        #region Method

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try

            {
                
                var view = ModuleView as JigsawPuzzleView;
                if (view == null) return;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
                if (index.Length < 1)
                {
                    ShowHTextList(false);
                    IsDraw = false;
                    return;
                } 
                RoiList[index] = roi;
                //var view = ModuleView as JigsawPuzzleView;
                //if (view == null)
                //    return;
                //;
                //ROI roi = view.mWindowH.WindowH.smallestActiveROI(
                //    out string info,
                //    out string index
                //);
                //if (index.Length < 1)
                //    return;
                //RoiList[index] = roi;

                //ShowHRoiList();
                ShowHTextList();
                IsDraw = false;
            }
            catch (Exception ex)
            {
            }
        }
        public bool IsDraw = false;
        private void HControl_MouseDown(object sender, MouseEventArgs e)
        {
            IsDraw = true;
        }
        private void HControl_MouseWheel(object sender, MouseEventArgs e)
        {
            /*ShowHTextList();*/
            ShowHTextList(false);
            //ShowHRoi();
        }

        private void HControl_MouseMove(object sender, MouseEventArgs e)
        {
            //if(e.Button == MouseButtons.Left)
            //{
            //    ShowHTextList(false);
            //}

        }

        public void CreateModel()
        {
        }

        public override void ShowHRoi()
        {
            //var view = ModuleView as JigsawPuzzleView;


            //VMHWindowControl mWindowH;
            //bool dispSearchRegion = true;
            //if (view == null || view.IsClosed)
            //{
            //    mWindowH = ViewDic.GetView(DispImage.DispViewID);
            //    dispSearchRegion = false;
            //}
            //else
            //{
            //    mWindowH = view.mWindowH;
            //    if (mWindowH != null)
            //    {
            //        mWindowH.ClearWindow();
            //        mWindowH.Image = new RImage(DispImage);
            //    }
            //}

            //if (dispSearchRegion)
            //{
            //    if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Search))
            //    {
            //        ROIRectangle1 ROIRect1 = (ROIRectangle1)
            //            RoiList[ModuleParam.ModuleName + ROIDefine.Search];
            //        mWindowH.WindowH.genRect1(
            //            ModuleParam.ModuleName + ROIDefine.Search,
            //            ROIRect1.row1,
            //            ROIRect1.col1,
            //            ROIRect1.row2,
            //            ROIRect1.col2,
            //            ref RoiList
            //        );
            //    }
            //    else
            //    {
            //        mWindowH.WindowH.genRect1(
            //            ModuleParam.ModuleName + ROIDefine.Search,
            //            5,
            //            5,
            //            mWindowH.hv_imageHeight - 5,
            //            mWindowH.hv_imageWidth - 5,
            //            ref RoiList
            //        );
                    
            //    }
            //}

            //List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            //foreach (HRoi roi in roiList)
            //{
            //    if (roi.roiType == HRoiType.文字显示)
            //    {
            //        HText roiText = (HText)roi;
            //        ShowTool.SetFont(
            //            mWindowH.hControl.HalconWindow,
            //            roiText.size,
            //            "false",
            //            "false"
            //        );
            //        ShowTool.SetMsg(
            //            mWindowH.hControl.HalconWindow,
            //            roiText.text,
            //            "image",
            //            roiText.row,
            //            roiText.col,
            //            roiText.drawColor,
            //            "false"
            //        );
            //    }
            //    else if (roi.roiType == HRoiType.搜索范围)
            //    {
            //        if (ShowSearchRegion && ModuleView == null)
            //        {
            //            mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
            //        }
            //    }
            //    else
            //    {
            //        mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
            //    }
            //}
        }

 


        private void AddData()
        {
            if (!(DispImage != null && DispImage.IsInitialized()))
            {
                return;
            }

            JigsawPuzzleData JigsawPuzzleData = new JigsawPuzzleData();
            JigsawPuzzleData.PropertyChanged += OnPropertyChangedHandler;

            if (DataList.Count == 0)
            {
                JigsawPuzzleData.NO = "1";
                JigsawPuzzleData.Y = 30;
                JigsawPuzzleData.X = 30;
            }
            else
            {
                JigsawPuzzleData.NO = Convert.ToString(int.Parse(DataList.LastOrDefault().NO) + 1);
                JigsawPuzzleData.Y = DataList.LastOrDefault().Y + 10;
                JigsawPuzzleData.X =DataList.LastOrDefault().X + 10;
            }

            JigsawPuzzleData.Deg = 0;
            JigsawPuzzleData.L1 = 50;
            JigsawPuzzleData.L2 = 50;
            DataList.Add(JigsawPuzzleData);
            ShowHRoiList();
            ShowHTextList(false);
        }

        private void OnPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            //ShowHRoiList();
            if(!IsDraw)
                ShowHTextList(false);
        }

        private void DelData(JigsawPuzzleData JigsawPuzzleData)
        {
            DataList.Remove(JigsawPuzzleData);
            ShowHRoiList();
            ShowHTextList(false);
        }

        private void ShowHRoiList()
        {
            var view = ModuleView as JigsawPuzzleView;
            VMHWindowControl mWindowH;
            bool dispSearchRegion = true;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispSearchRegion = false;
            }
            else
            {
                mWindowH = view.mWindowH;
                if (mWindowH != null)
                {
                    mWindowH.ClearWindow();
                    mWindowH.Image = new RImage(DispImage);
                }
            }

            if (dispSearchRegion)
            {
                foreach (JigsawPuzzleData puzzleData in DataList)
                {
                    mWindowH.WindowH.genRect2(
                        ModuleParam.ModuleName + ROIDefine.Search + puzzleData.NO,
                        puzzleData.Y,
                        puzzleData.X,
                        puzzleData.Deg * (Math.PI / 180.0),
                        puzzleData.L1,
                        puzzleData.L2,
                        ref RoiList
                    );
                }
            }
        }

//_isHTextUpdating false是拖动调用   true是更改表格值后调用
        private void ShowHTextList(bool IsRoiMove=true)
        {
            var view = ModuleView as JigsawPuzzleView;
            if (view == null)
                return;
            if (RoiList.Count == 0 || DispImage == null)
                return;
            string pattern = @"\d+$";
            ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 30, "false", "false");
            List<string> list = new List<string>();
            foreach (JigsawPuzzleData jigsawPuzzleData in DataList)
            {
                foreach (KeyValuePair<string, ROI> keyValuePair in RoiList)
                {
                    
                   Match match = Regex.Match(keyValuePair.Key, pattern);
                    if (match.Success)
                    {
                        list.Add(keyValuePair.Key);
                        
                        string numbers = match.Value;
                        if (numbers == jigsawPuzzleData.NO)
                        {
                            ROIRectangle2 rectangle2 = (ROIRectangle2)keyValuePair.Value;
                            //HTuple info = keyValuePair.Value.GetModelData();
                            if (IsRoiMove)
                            {
                                jigsawPuzzleData.Deg = rectangle2.Deg;
                                jigsawPuzzleData.Y = rectangle2.MidR;
                                jigsawPuzzleData.X = rectangle2.MidC;
                                jigsawPuzzleData.L1 = rectangle2.Length1;
                                jigsawPuzzleData.L2 = rectangle2.Length2;
                            }
                            else
                            {
                                rectangle2.Deg = jigsawPuzzleData.Deg;
                                rectangle2.MidR = jigsawPuzzleData.Y;
                                rectangle2.MidC = jigsawPuzzleData.X;
                                rectangle2.Length1 = jigsawPuzzleData.L1;
                                rectangle2.Length2 = jigsawPuzzleData.L2;
                                view.mWindowH.WindowH.Repaint();
                                //        view.mWindowH.WindowH.genRect2(
                                //ModuleParam.ModuleName + ROIDefine.Search + jigsawPuzzleData.NO,
                                //jigsawPuzzleData.X,
                                //jigsawPuzzleData.Y,
                                //jigsawPuzzleData.Deg * (Math.PI / 180.0),
                                //jigsawPuzzleData.L1,
                                //jigsawPuzzleData.L2,
                                //ref RoiList
                                //);
                            }

                        }
                    }
                }
                


            }
            foreach (var item in list)
            {
                RoiList[item] = view.mWindowH.WindowH.getRoi(item);
                ROIRectangle2 oIRectangle2 = (ROIRectangle2)RoiList[item];
                Match match = Regex.Match(item, pattern);
                if (match.Success)
                {
                    ShowTool.SetMsg(
                    view.mWindowH.hControl.HalconWindow,
                    match.Value,
                    "image",
                    oIRectangle2.MidC,
                    oIRectangle2.MidR,
                    "cyan",
                    "false"
                    );
                }

            }
        }

        #endregion
    }
}
