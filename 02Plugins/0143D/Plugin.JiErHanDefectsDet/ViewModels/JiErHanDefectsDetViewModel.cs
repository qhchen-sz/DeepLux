using EventMgrLib;
using HalconDotNet;
using Plugin.JiErHanDefectsDet.Views;
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
using static Plugin.JiErHanDefectsDet.ViewModels.JEHDDModel;
using System.Collections.ObjectModel;
using Application = System.Windows.Application;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace Plugin.JiErHanDefectsDet.ViewModels
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
    [DisplayName("极耳焊缺陷检")]
    [ModuleImageName("JiErHanDefectsDet")]
    [Serializable]
    public class JiErHanDefectsDetViewModel : ModuleBase
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
                JEHDDModel.HalconToImgPara(DispImage, out JEHDDModel.ImgPara img_para);
                Vector3d transformation_matrix = new Vector3d(1, 1, MPScale);
                FuncPara func_para = new FuncPara()
                {
                    normal_degree = NormalDegree,//最小搜索角度
                    curvature_threshold = CurvatureThreshold,//曲率阈值
                    min_defects_size = MinDefectsSize,//最小缺陷点数
                    z_threshold = ZThreshold,//最小缺陷高度
                    radius = Radius,//搜索半径
                };
                bool debug_mode = false;
                //创建mask_para，并分配内存
                ImgPara mask_para = new ImgPara();
                mask_para.data = Marshal.AllocHGlobal(img_para.wid * img_para.hei);
                Defect defects = new Defect();
                bool run_flag = detect_smooth_surface_v1(ref img_para, ref transformation_matrix, 
                                    ref func_para, out mask_para, out defects, debug_mode);
                if (!run_flag)
                {
                    Logger.AddLog("AI模型执行失败！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                //获取图像，并对图像进行处理
                // A) mask(byte) from ImgPara
                HObject maskByte = ImgParaMaskToHImageByte(mask_para);
                //// B) 要求：mask 对齐原图数据类型（仅“对齐”，不直接用于显示）
                ////    这一步满足你的要求：maskConverted 与 originalImage 同类型
                //HObject maskConverted = ConvertMaskToMatchImageType(maskByte, DispImage);
                //// C) addWeighted 混合
                //if (merged != null)
                //{
                //    merged.Dispose();
                //    merged = null;
                //}
                //merged = AddWeightedRgb(maskConverted, 0.4, DispImage);
                //if (merged == null || !merged.IsInitialized())
                //{
                //    merged = null;
                //}
                // D) 从 maskByte 中按“灰度值分组”找轮廓，并用不同颜色叠加显示
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
                    /*                    // 精确匹配该类别像素
                                        HOperatorSet.Threshold(maskByte, out HObject region, v, v);

                                        // 连通域
                                        HOperatorSet.Connection(region, out HObject conn);

                                        // 面积过滤（避免噪点）
                                        HOperatorSet.SelectShape(conn, out HObject sel, "area", "and", 500, 999999999);

                                        // 轮廓 XLD
                                        HOperatorSet.GenContourRegionXld(sel, out HObject xld, "border");*/

                    defectResults.Add(new DefectResult
                    {
                        Id = colorIdx,
                        Area = defects.defects_size[colorIdx] * transformation_matrix.x * transformation_matrix.y,
                        Region = region1.Clone()   // ⚠️ 一定要 Clone
                    });

                    int index = colorIdx % len;
                    eAiColor color = (eAiColor)index;
                    if (ShowDefectsContour) {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "Contour_" + colorIdx.ToString(),
                            ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)index).ToString(), new HObject(region1), false));
                    }
                    if (ShowDefectsArea) {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName + "Area_" + colorIdx.ToString(), 
                            ModuleParam.Remarks, HRoiType.检测结果, ((eAiColor)index).ToString(), new HObject(region1), true));
                    }
                    region.Dispose();
                    region1.Dispose();

                    colorIdx++;
                }
                maskByte.Dispose();

                //手动释放内存
                Marshal.FreeHGlobal(img_para.data);
                img_para.data = IntPtr.Zero;
                Marshal.FreeHGlobal(mask_para.data);
                mask_para.data = IntPtr.Zero;

                //maskConverted.Dispose();
                //TODO
                //merged.Dispose();

/*                //defects area
                double scale = transformation_matrix.x * transformation_matrix.y;

                var results = defects.defects_size
                    .Where(s => s > 0)                   // 去掉 0（或 <=0）
                    .Select(s => new DefectResult
                    {
                        Area = s * scale
                    })
                    .ToList();*/

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



                var view = ModuleView as JiErHanDefectsDetView;
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
                //merged.Dispose();
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
        private float _NormalDegree = 2.0f;
        public float NormalDegree
        {
            get { return _NormalDegree; }
            set
            {
                _NormalDegree = value;
                RaisePropertyChanged();
            }
        }
        private float _CurvatureThreshold = 5.0f;
        public float CurvatureThreshold
        {
            get { return _CurvatureThreshold; }
            set
            {
                _CurvatureThreshold = value;
                RaisePropertyChanged();
            }
        }
        private int _MinDefectsSize = 500;
        public int MinDefectsSize
        {
            get { return _MinDefectsSize; }
            set
            {
                _MinDefectsSize = value;
                RaisePropertyChanged();
            }
        }
        private float _ZThreshold = 7.0f;
        public float ZThreshold
        {
            get { return _ZThreshold; }
            set
            {
                _ZThreshold = value;
                RaisePropertyChanged();
            }
        }
        private float _Radius = 2.0f;
        public float Radius
        {
            get { return _Radius; }
            set
            {
                _Radius = value;
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
            var view = ModuleView as JiErHanDefectsDetView;
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
                        var view = this.ModuleView as JiErHanDefectsDetView;
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
            var view = ModuleView as JiErHanDefectsDetView;
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
