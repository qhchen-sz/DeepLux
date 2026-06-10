using EventMgrLib;
using HalconDotNet;
using Plugin._3DPreProcessing.Models;
using Plugin._3DPreProcessing.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using VM.Halcon;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.ViewModels;
using VM.Halcon.Config;
using Newtonsoft.Json.Linq;

namespace Plugin._3DPreProcessing.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Roi
    }
    public enum eRoiType
    {
        全图,
        ROI链接,
    }
    #endregion

    [Category("3D")]
    [DisplayName("3D预处理")]
    [ModuleImageName("3DPreProcessing")]
    [Serializable]
    public class PreProcessing3DViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (string.IsNullOrEmpty(InputImageLinkText))
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
                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (m_outImage == null)
                    m_outImage = new RImage();
                if (m_InImage == null)
                    m_InImage = new HImage();
                if (m_PretreatHelp == null)
                    m_PretreatHelp = new Pretreat3DHelp();

                HImage hImage = new HImage();
                GetDispImage(InputImageLinkText, true);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    // 提取深度通道并转换为real
                    HOperatorSet.CountChannels(DispImage, out HTuple channelCount);
                    HObject depthChannelObj;
                    if (channelCount == 2)
                        HOperatorSet.Decompose2(DispImage, out depthChannelObj, out HObject _);
                    else
                        HOperatorSet.AccessChannel(DispImage, out depthChannelObj, 1);
                    HOperatorSet.ConvertImageType(depthChannelObj, out HObject depthRealObj, "real");
                    m_InImage = new HImage(depthRealObj);
                    depthChannelObj.Dispose();
                    depthRealObj.Dispose();
                }

                HImage TempImage;
                HRegion region = new HRegion();
                if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                    region = (HRegion)GetLinkValue(InputRoiLinkText);

                if (m_InImage != null && m_InImage.IsInitialized())
                {
                    int i = 0;
                    foreach (var item in m_ToolList)
                    {
                        if (i > 0)
                        {
                            TempImage = new HImage(m_outImage);
                        }
                        else
                        {
                            TempImage = new HImage(m_InImage);
                        }

                        switch (item.m_name)
                        {
                            case eOperatorType.均值滤波:
                                if (item.m_enable)
                                {
                                    if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                                        m_PretreatHelp.MeanImage(TempImage.ReduceDomain(region), out hImage, item.m_MeanImageWidth, item.m_MeanImageHeight);
                                    else
                                        m_PretreatHelp.MeanImage(TempImage, out hImage, item.m_MeanImageWidth, item.m_MeanImageHeight);
                                }
                                break;
                            case eOperatorType.中值滤波:
                                if (item.m_enable)
                                {
                                    if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                                        m_PretreatHelp.MedianImage(TempImage.ReduceDomain(region), out hImage, item.m_MedianImageWidth, item.m_MedianImageHeight);
                                    else
                                        m_PretreatHelp.MedianImage(TempImage, out hImage, item.m_MedianImageWidth, item.m_MedianImageHeight);
                                }
                                break;
                            case eOperatorType.高斯滤波:
                                if (item.m_enable)
                                {
                                    if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                                        m_PretreatHelp.GaussImage(TempImage.ReduceDomain(region), out hImage, item.m_GaussImageSize);
                                    else
                                        m_PretreatHelp.GaussImage(TempImage, out hImage, item.m_GaussImageSize);
                                }
                                break;
                            case eOperatorType.膨胀:
                                if (item.m_enable)
                                    m_PretreatHelp.GrayDilation(TempImage, out hImage, item.m_DilationWidth, item.m_DilationHeight);
                                break;
                            case eOperatorType.腐蚀:
                                if (item.m_enable)
                                    m_PretreatHelp.GrayErosion(TempImage, out hImage, item.m_ErosionWidth, item.m_ErosionHeight);
                                break;
                            case eOperatorType.开运算:
                                if (item.m_enable)
                                    m_PretreatHelp.GrayOpening(TempImage, out hImage, item.m_OpeningWidth, item.m_OpeningHeight);
                                break;
                            case eOperatorType.闭运算:
                                if (item.m_enable)
                                    m_PretreatHelp.GrayClosing(TempImage, out hImage, item.m_ClosingWidth, item.m_ClosingHeight);
                                break;
                            case eOperatorType.深度阈值裁剪:
                                if (item.m_enable)
                                    m_PretreatHelp.ClipDepth(TempImage, out hImage, item.m_ClipMin, item.m_ClipMax);
                                break;
                            case eOperatorType.深度填充:
                                if (item.m_enable)
                                    m_PretreatHelp.FillDepth(TempImage, out hImage, item.m_FillWidth, item.m_FillHeight);
                                break;
                        }
                        m_outImage = new RImage(hImage);
                        i++;
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
            base.AddOutputParams();
            AddOutputParam("预处理图像", "HImage", m_outImage);
        }

        #region Prop
        [NonSerialized]
        public RImage m_outImage;
        [NonSerialized]
        public HImage m_InImage;
        public Pretreat3DHelp m_PretreatHelp = new Pretreat3DHelp();

        public ObservableCollection<ModelData> m_ToolList { get; set; } = new ObservableCollection<ModelData>();

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText, true);
            }
        }

        private ModelData _SelectedText = new ModelData();
        public ModelData SelectedText
        {
            get { return _SelectedText; }
            set { Set(ref _SelectedText, value); }
        }

        private int _SelectedIndex;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set { Set(ref _SelectedIndex, value); }
        }

        private bool _IsDisp = false;
        public bool IsDisp
        {
            get { return _IsDisp; }
            set { Set(ref _IsDisp, value); }
        }

        private eRoiType _SelectedROIType = eRoiType.全图;
        public eRoiType SelectedROIType
        {
            get { return _SelectedROIType; }
            set
            {
                Set(ref _SelectedROIType, value, new Action(() =>
                {
                    if (value == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                    {
                        HRegion region = (HRegion)GetLinkValue(InputRoiLinkText);
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                        ShowHRoi();
                    }
                    else
                    {
                        var view = ModuleView as PreProcessing3DView;
                        if (view != null && view.mWindowH != null)
                            view.mWindowH.HobjectToHimage(DispImage);
                    }
                }));
            }
        }

        private string _InputRoiLinkText;
        public string InputRoiLinkText
        {
            get { return _InputRoiLinkText; }
            set { Set(ref _InputRoiLinkText, value); }
        }
        #endregion

        #region command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as PreProcessing3DView;
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
                    if (string.IsNullOrEmpty(InputImageLinkText)) return;
                }
                GetDispImage(InputImageLinkText, true);
                if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                {
                    HRegion region = (HRegion)GetLinkValue(InputRoiLinkText);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                    ShowHRoi();
                }
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
                        var view = ModuleView as PreProcessing3DView;
                        if (view == null) return;
                        if (m_outImage != null && m_outImage.IsInitialized())
                        {
                            view.mWindowH.HobjectToHimage(m_outImage);
                            if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                                ShowHRoi();
                            m_outImage = null;
                        }
                    });
                }
                return _ExecuteCommand;
            }
        }

        [NonSerialized]
        private CommandBase _ComposeCommand;
        public CommandBase ComposeCommand
        {
            get
            {
                if (_ComposeCommand == null)
                {
                    _ComposeCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as PreProcessing3DView;
                        if (view == null) return;
                        if (m_InImage != null && m_InImage.IsInitialized())
                        {
                            view.mWindowH.Image = new RImage(m_InImage);
                        }
                    });
                }
                return _ComposeCommand;
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
                        var view = this.ModuleView as PreProcessing3DView;
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
        private CommandBase _DataOperateCommand;
        public CommandBase DataOperateCommand
        {
            get
            {
                if (_DataOperateCommand == null)
                {
                    _DataOperateCommand = new CommandBase((obj) =>
                    {
                        string[] sArray = obj.ToString().Split('_');
                        if (sArray.Length == 2)
                        {
                            m_ToolList.Add(new ModelData()
                            {
                                m_name = (eOperatorType)Enum.Parse(typeof(eOperatorType), sArray[1])
                            });
                        }
                        else
                        {
                            switch (sArray[0])
                            {
                                case "remove":
                                    if (SelectedText == null) return;
                                    m_ToolList.Remove(SelectedText);
                                    if (m_ToolList.Count == 0)
                                        IsDisp = false;
                                    break;
                                case "up":
                                    if (SelectedText == null) return;
                                    int i = m_ToolList.IndexOf(SelectedText);
                                    if (i > 0)
                                        m_ToolList.Move(i, i - 1);
                                    break;
                                case "down":
                                    if (SelectedText == null) return;
                                    int j = m_ToolList.IndexOf(SelectedText);
                                    if (j + 1 < m_ToolList.Count)
                                        m_ToolList.Move(j, j + 1);
                                    break;
                                default:
                                    break;
                            }
                        }
                        SelectedIndex = m_ToolList.Count - 1;
                    });
                }
                return _DataOperateCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "InputRoiLink":
                    InputRoiLinkText = obj.LinkName;
                    HRegion region = (HRegion)GetLinkValue(InputRoiLinkText);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                    ShowHRoi();
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
                            case eLinkCommand.Roi:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRoiLink");
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

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["SelectedIndex"] = SelectedIndex;
            obj["IsDisp"] = IsDisp;
            obj["SelectedROIType"] = (int)SelectedROIType;
            obj["InputRoiLinkText"] = InputRoiLinkText ?? "";
            JArray arr = new JArray();
            if (m_ToolList != null)
            {
                foreach (var item in m_ToolList)
                {
                    JObject itemObj = new JObject();
                    itemObj["m_enable"] = item.m_enable;
                    itemObj["m_name"] = (int)item.m_name;
                    itemObj["m_MeanImageWidth"] = item.m_MeanImageWidth;
                    itemObj["m_MeanImageHeight"] = item.m_MeanImageHeight;
                    itemObj["m_MedianImageWidth"] = item.m_MedianImageWidth;
                    itemObj["m_MedianImageHeight"] = item.m_MedianImageHeight;
                    itemObj["m_GaussImageSize"] = item.m_GaussImageSize;
                    itemObj["m_DilationWidth"] = item.m_DilationWidth;
                    itemObj["m_DilationHeight"] = item.m_DilationHeight;
                    itemObj["m_ErosionWidth"] = item.m_ErosionWidth;
                    itemObj["m_ErosionHeight"] = item.m_ErosionHeight;
                    itemObj["m_OpeningWidth"] = item.m_OpeningWidth;
                    itemObj["m_OpeningHeight"] = item.m_OpeningHeight;
                    itemObj["m_ClosingWidth"] = item.m_ClosingWidth;
                    itemObj["m_ClosingHeight"] = item.m_ClosingHeight;
                    itemObj["m_ClipMin"] = item.m_ClipMin;
                    itemObj["m_ClipMax"] = item.m_ClipMax;
                    itemObj["m_FillWidth"] = item.m_FillWidth;
                    itemObj["m_FillHeight"] = item.m_FillHeight;
                    arr.Add(itemObj);
                }
            }
            obj["m_ToolList"] = arr;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["SelectedIndex"] != null) SelectedIndex = obj["SelectedIndex"].Value<int>();
                if (obj["IsDisp"] != null) IsDisp = obj["IsDisp"].Value<bool>();
                if (obj["SelectedROIType"] != null) SelectedROIType = (eRoiType)obj["SelectedROIType"].Value<int>();
                if (obj["InputRoiLinkText"] != null) InputRoiLinkText = obj["InputRoiLinkText"].ToString();
                if (obj["m_ToolList"] != null)
                {
                    JArray arr = (JArray)obj["m_ToolList"];
                    m_ToolList.Clear();
                    foreach (var item in arr)
                    {
                        ModelData md = new ModelData();
                        if (item["m_enable"] != null) md.m_enable = item["m_enable"].Value<bool>();
                        if (item["m_name"] != null) md.m_name = (eOperatorType)item["m_name"].Value<int>();
                        if (item["m_MeanImageWidth"] != null) md.m_MeanImageWidth = item["m_MeanImageWidth"].Value<int>();
                        if (item["m_MeanImageHeight"] != null) md.m_MeanImageHeight = item["m_MeanImageHeight"].Value<int>();
                        if (item["m_MedianImageWidth"] != null) md.m_MedianImageWidth = item["m_MedianImageWidth"].Value<int>();
                        if (item["m_MedianImageHeight"] != null) md.m_MedianImageHeight = item["m_MedianImageHeight"].Value<int>();
                        if (item["m_GaussImageSize"] != null) md.m_GaussImageSize = item["m_GaussImageSize"].Value<int>();
                        if (item["m_DilationWidth"] != null) md.m_DilationWidth = item["m_DilationWidth"].Value<int>();
                        if (item["m_DilationHeight"] != null) md.m_DilationHeight = item["m_DilationHeight"].Value<int>();
                        if (item["m_ErosionWidth"] != null) md.m_ErosionWidth = item["m_ErosionWidth"].Value<int>();
                        if (item["m_ErosionHeight"] != null) md.m_ErosionHeight = item["m_ErosionHeight"].Value<int>();
                        if (item["m_OpeningWidth"] != null) md.m_OpeningWidth = item["m_OpeningWidth"].Value<int>();
                        if (item["m_OpeningHeight"] != null) md.m_OpeningHeight = item["m_OpeningHeight"].Value<int>();
                        if (item["m_ClosingWidth"] != null) md.m_ClosingWidth = item["m_ClosingWidth"].Value<int>();
                        if (item["m_ClosingHeight"] != null) md.m_ClosingHeight = item["m_ClosingHeight"].Value<int>();
                        if (item["m_ClipMin"] != null) md.m_ClipMin = item["m_ClipMin"].Value<double>();
                        if (item["m_ClipMax"] != null) md.m_ClipMax = item["m_ClipMax"].Value<double>();
                        if (item["m_FillWidth"] != null) md.m_FillWidth = item["m_FillWidth"].Value<int>();
                        if (item["m_FillHeight"] != null) md.m_FillHeight = item["m_FillHeight"].Value<int>();
                        m_ToolList.Add(md);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"PreProcessing3DViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
