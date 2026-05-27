using EventMgrLib;
using HalconDotNet;
using Plugin.BlobFilter.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Newtonsoft.Json.Linq;

namespace Plugin.BlobFilter.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
    }

    public enum eFilterMode
    {
        剔除范围内,
        剔除范围外,
    }
    #endregion

    [Category("几何测量")]
    [DisplayName("标签图面积过滤")]
    [ModuleImageName("BlobFilter")]
    [Serializable]
    public class BlobFilterModel : ModuleBase
    {
        public override void SetDefaultLink()
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
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                DispImage.MinMaxGray(DispImage, 0, out double min, out double max, out double range);
                int classCount = (int)max;
                if (classCount < 1)
                {
                    ChangeModuleRunStatus(eRunStatus.OK);
                    return true;
                }

                HOperatorSet.GetImageSize(DispImage, out HTuple imgWidth, out HTuple imgHeight);

                HRegion allSurviving = new HRegion();
                allSurviving.GenEmptyObj();

                RImage filteredLabel = new RImage();
                filteredLabel.GenImageConst("byte", imgWidth, imgHeight);

                List<double> areaList = new List<double>();

                for (int i = 1; i <= classCount; i++)
                {
                    HRegion classRegion = DispImage.Threshold((HTuple)i, (HTuple)i);
                    int blobCount = classRegion.CountObj();
                    if (blobCount == 0)
                    {
                        classRegion.Dispose();
                        continue;
                    }

                    HRegion connected = classRegion.Connection();
                    classRegion.Dispose();

                    int connCount = connected.CountObj();
                    for (int j = 1; j <= connCount; j++)
                    {
                        HRegion blob = connected.SelectObj(j);
                        double area = blob.AreaCenter(out double row, out double col);

                        bool keep;
                        if (FilterMode == eFilterMode.剔除范围内)
                            keep = area < MinArea || area > MaxArea;
                        else
                            keep = area >= MinArea && area <= MaxArea;

                        if (keep)
                        {
                            areaList.Add(Math.Round(area, 2));
                            allSurviving = allSurviving.ConcatObj(blob);
                            HImage temp = filteredLabel.PaintRegion(blob, (HTuple)i, "fill");
                            filteredLabel.Dispose();
                            filteredLabel = new RImage(temp);

                            if (ShowAreaText)
                            {
                                string text = $"{area:F1}";
                                ShowHRoi(new HText(ModuleParam.ModuleEncode,
                                    $"{ModuleParam.ModuleName}_{i}_{j}",
                                    ModuleParam.Remarks, HRoiType.文字显示,
                                    "green", text, col, row, 12));
                            }
                        }
                        blob.Dispose();
                    }
                    connected.Dispose();
                }

                FilteredImage?.Dispose();
                FilteredImage = filteredLabel;

                FilteredRegion?.Dispose();
                FilteredRegion = allSurviving;

                Areas = areaList;
                BlobCount = areaList.Count;
                AreasText = string.Join(", ", areaList);

                if (ShowRegion && FilteredRegion != null && FilteredRegion.IsInitialized())
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                        ModuleParam.Remarks, HRoiType.检测结果, "green",
                        new HObject(FilteredRegion)));

                if (ShowFilteredImage && FilteredImage != null && FilteredImage.IsInitialized())
                    DispImage = FilteredImage;

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
            AddOutputParam("过滤图", "HImage", FilteredImage);
            if (FilteredRegion != null && FilteredRegion.IsInitialized())
                AddOutputParam("区域", "HRegion", FilteredRegion);
            else
                AddOutputParam("区域", "HRegion", new HRegion());
            AddOutputParam("面积列表", "double[]", Areas);
            AddOutputParam("连通域数量", "int", BlobCount);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private double _MinArea = 0;
        public double MinArea
        {
            get { return _MinArea; }
            set { Set(ref _MinArea, value); }
        }

        private double _MaxArea = 999999;
        public double MaxArea
        {
            get { return _MaxArea; }
            set { Set(ref _MaxArea, value); }
        }

        private eFilterMode _FilterMode = eFilterMode.剔除范围外;
        public eFilterMode FilterMode
        {
            get { return _FilterMode; }
            set { Set(ref _FilterMode, value); }
        }

        private bool _ShowFilteredImage = true;
        public bool ShowFilteredImage
        {
            get { return _ShowFilteredImage; }
            set { Set(ref _ShowFilteredImage, value); }
        }

        private bool _ShowRegion = true;
        public bool ShowRegion
        {
            get { return _ShowRegion; }
            set { Set(ref _ShowRegion, value); }
        }

        private bool _ShowAreaText = true;
        public bool ShowAreaText
        {
            get { return _ShowAreaText; }
            set { Set(ref _ShowAreaText, value); }
        }

        [NonSerialized]
        private RImage _FilteredImage;
        public RImage FilteredImage
        {
            get { return _FilteredImage; }
            set { _FilteredImage = value; }
        }

        [NonSerialized]
        private HRegion _FilteredRegion;
        public HRegion FilteredRegion
        {
            get { return _FilteredRegion; }
            set { _FilteredRegion = value; }
        }

        private List<double> _Areas = new List<double>();
        public List<double> Areas
        {
            get { return _Areas; }
            set { Set(ref _Areas, value); }
        }

        private int _BlobCount;
        public int BlobCount
        {
            get { return _BlobCount; }
            set { Set(ref _BlobCount, value); }
        }

        private string _AreasText;
        public string AreasText
        {
            get { return _AreasText; }
            set { Set(ref _AreasText, value); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as BlobFilterView;
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
                        var view = this.ModuleView as BlobFilterView;
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
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged,
                        o => o.SendName.StartsWith($"{ModuleGuid}"));
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
        #endregion

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["MinArea"] = MinArea;
            obj["MaxArea"] = MaxArea;
            obj["FilterMode"] = (int)FilterMode;
            obj["ShowFilteredImage"] = ShowFilteredImage;
            obj["ShowRegion"] = ShowRegion;
            obj["ShowAreaText"] = ShowAreaText;
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
                if (obj["MinArea"] != null) MinArea = obj["MinArea"].Value<double>();
                if (obj["MaxArea"] != null) MaxArea = obj["MaxArea"].Value<double>();
                if (obj["FilterMode"] != null) FilterMode = (eFilterMode)obj["FilterMode"].Value<int>();
                if (obj["ShowFilteredImage"] != null) ShowFilteredImage = obj["ShowFilteredImage"].Value<bool>();
                if (obj["ShowRegion"] != null) ShowRegion = obj["ShowRegion"].Value<bool>();
                if (obj["ShowAreaText"] != null) ShowAreaText = obj["ShowAreaText"].Value<bool>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"BlobFilterModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion

    }
}
