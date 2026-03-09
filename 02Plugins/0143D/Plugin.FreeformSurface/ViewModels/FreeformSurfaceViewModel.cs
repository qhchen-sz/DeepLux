using EventMgrLib;
using HalconDotNet;
using Plugin.FreeformSurface.Views;
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
using HandyControl.Controls;
using static Plugin.FreeformSurface.ViewModels.FFSModel;
using System.Collections.ObjectModel;
using Application = System.Windows.Application;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace Plugin.FreeformSurface.ViewModels
{
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
    public enum eLinkCommand
    {
        InputImageLink,
    }
    [Category("3D")]
    [DisplayName("自由曲面")]
    [ModuleImageName("FreeformSurface")]
    [Serializable]
    public class FreeformSurfaceViewModel : ModuleBase
    {
        //private HObject merged = null;
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
                InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
            }
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
                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {

                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                bool status = FFSModel.SurfaceDefectDetection(DispImage, MPScale, ShieldWidth, 
                    ShieldHeight, PartitionHeight, ZLSelect, ZHSelect, out HObject maskByte);
                if (!status)
                {
                    Logger.AddLog("自由曲面算法执行失败！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                List<int> values = GetMaskNonZeroValues(maskByte);

                string[] colors =
                {
                "red","green","blue","cyan","magenta","yellow","orange","violet",
                "spring green","dodger blue","pink","gold","turquoise","chartreuse"
            };
                List<DefectResult> defectResults = new List<DefectResult>();
                int colorIdx = 0;
                int len = Enum.GetValues(typeof(eAiColor)).Length;
                foreach (int v in values)
                {
                    HOperatorSet.Threshold(maskByte, out HObject region, v, v);
                    HRegion region1 = new HRegion(region);
                    double areaPixels = region1.AreaCenter(out double centerRow, out double centerCol);
                    Logger.AddLog("自由曲面算法缺陷_" + colorIdx.ToString(), eMsgType.Info);
                    Logger.AddLog("centerRow:" + centerRow.ToString(), eMsgType.Info);
                    Logger.AddLog("centerCol:" + centerCol.ToString(), eMsgType.Info);


                    defectResults.Add(new DefectResult
                    {
                        Id = colorIdx,
                        Area = areaPixels,
                        Region = region1.Clone()   // ⚠️ 一定要 Clone
                    });

                    int index = colorIdx % len;
                    eAiColor color = (eAiColor)index;
                    if (ShowDefectsContour)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "Contour_" + colorIdx.ToString(),
                            ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)index).ToString(), new HObject(region1), false));
                    }
                    if (ShowDefectsArea)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "Area_" + colorIdx.ToString(),
                            ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)index).ToString(), new HObject(region1), true));
                    }
                    region.Dispose();
                    region1.Dispose();

                    colorIdx++;
                }
                maskByte.Dispose();

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (UIDefects == null)
                    {
                        UIDefects = new ObservableCollection<DefectResult>();
                    }
                    UIDefects.Clear();
                    foreach (var r in defectResults)
                        UIDefects.Add(r);
                }));


                var view = ModuleView as FreeformSurfaceView;
                VMHWindowControl mWindowH;
                if (view == null || view.IsClosed)
                {
                    mWindowH = ViewDic.GetView(DispImage.DispViewID);
                }
                else
                {
                    mWindowH = view.mWindowH;
                }
                ShowHRoi();
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
            this.Prj.ClearOutputParam(this.ModuleParam);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }
        #region Prop
        [NonSerialized]
        private ObservableCollection<DefectResult> _uiDefects;

        public ObservableCollection<DefectResult> UIDefects
        {
            get { return _uiDefects; }
            set { Set(ref _uiDefects, value); }
        }

        private double _MPScale = 1.0;
        public double MPScale
        {
            get { return _MPScale; }
            set
            {
                _MPScale = value;
                RaisePropertyChanged();
            }
        }
        private double _ShieldWidth = 100.0;
        public double ShieldWidth
        {
            get { return _ShieldWidth; }
            set
            {
                _ShieldWidth = value;
                RaisePropertyChanged();
            }
        }
        private double _ShieldHeight = 300.0;
        public double ShieldHeight
        {
            get { return _ShieldHeight; }
            set
            {
                _ShieldHeight = value;
                RaisePropertyChanged();
            }
        }
        private double _PartitionHeight = 200.0;
        public double PartitionHeight
        {
            get { return _PartitionHeight; }
            set
            {
                _PartitionHeight = value;
                RaisePropertyChanged();
            }
        }
        private double _ZLSelect = -100.0;
        public double ZLSelect
        {
            get { return _ZLSelect; }
            set
            {
                _ZLSelect = value;
                RaisePropertyChanged();
            }
        }
        private double _ZHSelect = 100.0;
        public double ZHSelect
        {
            get { return _ZHSelect; }
            set
            {
                _ZHSelect = value;
                RaisePropertyChanged();
            }
        }
        private bool _ShowDefectsContour = true;
        /// <summary>显示缺陷轮廓 </summary>
        public bool ShowDefectsContour
        {
            get { return _ShowDefectsContour; }
            set { Set(ref _ShowDefectsContour, value); }
        }
        private bool _ShowDefectsArea = false;
        /// <summary>显示缺陷区域 </summary>
        public bool ShowDefectsArea
        {
            get { return _ShowDefectsArea; }
            set { Set(ref _ShowDefectsArea, value); }
        }

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

        [NonSerialized]
        private DefectResult _selectedDefect;
        public DefectResult SelectedDefect
        {
            get => _selectedDefect;
            set
            {
                if (ReferenceEquals(_selectedDefect, value))
                    return;
                Set(ref _selectedDefect, value);
                HighlightSelectedDefect();
            }
        }
        //点击过的不再高亮显示，禁用
        private DefectResult _lastHighlighted;
        private void HighlightSelectedDefect()
        {
            if (SelectedDefect == null)
                return;

            if (ReferenceEquals(_lastHighlighted, SelectedDefect))
                return;

            _lastHighlighted = SelectedDefect;

            try
            {
                // 清除之前的高亮 ROI（只清高亮，不清检测结果）
                ClearHighlightRoi();


                // 显示高亮 ROI（用不同颜色）
                ShowHRoi(new HRoi(
                    ModuleParam.ModuleEncode,
                    "SelectedDefect",
                    ModuleParam.Remarks,
                    HRoiType.检测结果,
                    "yellow",
                    SelectedDefect.Region,
                    false
                ));

                /*                // 可选：显示文本
                                string text = $"类别:{SelectedDefect.LabelName}  得分:{SelectedDefect.Score:F2}";
                                ShowHRoi(new HText(
                                    ModuleParam.ModuleEncode,
                                    "SelectedText",
                                    ModuleParam.Remarks,
                                    HRoiType.文字显示,
                                    "yellow",
                                    text,
                                    SelectedDefect.CX,
                                    SelectedDefect.CY,
                                    18
                                ));*/

                ShowHRoi();
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }
        private void ClearHighlightRoi()
        {
            // mHRoi 是 ModuleBase 里维护的 ROI 列表（你项目里一定有）
            if (mHRoi == null || mHRoi.Count == 0)
                return;

            // 找到需要删除的高亮 ROI
            var toRemove = mHRoi
                .Where(r =>
                    (r.ModuleName == "SelectedDefect" || r.ModuleName == "SelectedText"))
                .ToList();

            foreach (var roi in toRemove)
            {
/*                // ✅ 释放 Halcon 对象，释放之后不能再次点击
                try
                {
                    roi.hobject?.Dispose();   // 关键：释放真正占内存的 Halcon 资源
                }
                catch { }*/
                mHRoi.Remove(roi);
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as FreeformSurfaceView;
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
                    view.mWindowH.HobjectToHimage(DispImage);
/*                    view.mWindowH.hControl.MouseUp += HControl_MouseUp;*/
                    ShowHRoi();
                }

            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
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
                        var view = this.ModuleView as FreeformSurfaceView;
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
        public override void ShowHRoi()
        {
            var view = ModuleView as FreeformSurfaceView;
            VMHWindowControl mWindowH;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
                if (mWindowH != null)
                {
                    mWindowH.ClearWindow();
                    //HObject imgToShow = (merged != null && merged.IsInitialized())
                    //                    ? merged
                    //                    : DispImage;

                    //if (imgToShow == null || !imgToShow.IsInitialized())
                    //    return;

                    //mWindowH.Image = new RImage(imgToShow);
                    mWindowH.Image = new RImage(DispImage);

                }
            }
            //List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName.StartsWith(ModuleParam.ModuleName) ||
            c.ModuleName.StartsWith("SelectedDefect")).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(
                        mWindowH.hControl.HalconWindow,
                        roiText.size,
                        "false",
                        "false"
                    );
                    ShowTool.SetMsg(
                        mWindowH.hControl.HalconWindow,
                        roiText.text,
                        "image",
                        roiText.row,
                        roiText.col,
                        roiText.drawColor,
                        "false"
                    );
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                }
            }
        }

        #endregion
    }
}
