using EventMgrLib;
using HalconDotNet;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.ViewModels;
using Newtonsoft.Json.Linq;
using Plugin.AreaSpray.Views;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.AreaSpray.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        InputRoiLink,
    }

    public enum eRoiType
    {
        FullImage,
        RoiLink,
    }

    [Category("图像处理")]
    [DisplayName("区域喷绘")]
    [ModuleImageName("AreaSpray")]
    [Serializable]
    public class AreaSprayViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (InputImageLinkText != null) return;

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
            HImage sourceImage = null;
            HImage grayImage = null;
            HRegion roiRegion = null;
            HImage resultImage = null;

            try
            {
                ClearRoiAndText();
                if (string.IsNullOrWhiteSpace(InputImageLinkText))
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

                sourceImage = new HImage(DispImage);
                grayImage = EnsureGrayImage(sourceImage);
                roiRegion = GetEffectiveRegion(grayImage);

                resultImage = ExecuteSpray(grayImage, roiRegion);
                if (resultImage == null || !resultImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                ResultImage = new RImage(resultImage);
                DispImage = ResultImage;
                RefreshPreview();
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
            finally
            {
                sourceImage?.Dispose();
                grayImage?.Dispose();
                roiRegion?.Dispose();
                resultImage?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            Prj.ClearOutputParam(ModuleParam);
            AddOutputParam("图像", "HImage", ResultImage);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        private HImage EnsureGrayImage(HImage image)
        {
            try
            {
                HOperatorSet.CountChannels(image, out HTuple channels);
                if (channels.I <= 1)
                {
                    return image.CopyImage();
                }

                HOperatorSet.Rgb1ToGray(image, out HObject grayObj);
                var gray = new HImage(grayObj);
                grayObj.Dispose();
                return gray;
            }
            catch
            {
                return image.CopyImage();
            }
        }

        private HRegion GetEffectiveRegion(HImage sourceImage)
        {
            if (SelectedROIType == eRoiType.RoiLink && !string.IsNullOrWhiteSpace(InputRoiLinkText))
            {
                var region = ConvertToRegion(GetLinkValue(InputRoiLinkText));
                if (region != null && region.IsInitialized())
                {
                    return region;
                }
            }

            return sourceImage.GetDomain();
        }

        private HRegion ConvertToRegion(object roiObj)
        {
            try
            {
                if (roiObj == null)
                    return null;

                if (roiObj is HRegion region && region.IsInitialized())
                    return new HRegion(region);

                if (roiObj is HObject hObject && hObject.IsInitialized())
                    return new HRegion(hObject);

                if (roiObj is ROILine roiLine)
                    return new HRegion(roiLine.GetRegion());

                if (roiObj is ROICircle roiCircle)
                    return new HRegion(roiCircle.GetRegion());

                if (roiObj is ROICircularArc roiArc)
                    return new HRegion(roiArc.GetRegion());

                if (roiObj is ROIRectangle1 roiRect1)
                    return new HRegion(roiRect1.GetRegion());

                if (roiObj is ROIRectangle2 roiRect2)
                    return new HRegion(roiRect2.GetRegion());
            }
            catch
            {
            }

            return null;
        }

        private HImage ExecuteSpray(HImage sourceImage, HRegion roiRegion)
        {
            HImage result = sourceImage.CopyImage();
            if (roiRegion == null || !roiRegion.IsInitialized())
            {
                return result;
            }

            HRegion filledRegion = null;
            try
            {
                double gray = Math.Max(0, Math.Min(255, GrayValue));
                filledRegion = roiRegion.FillUp();
                HOperatorSet.OverpaintRegion(result, filledRegion, new HTuple(gray), "fill");
                return result;
            }
            finally
            {
                filledRegion?.Dispose();
            }
        }

        private void ShowCurrentRoi()
        {
            if (SelectedROIType != eRoiType.RoiLink || string.IsNullOrWhiteSpace(InputRoiLinkText))
            {
                ShowHRoi();
                return;
            }

            var region = ConvertToRegion(GetLinkValue(InputRoiLinkText));
            if (region != null && region.IsInitialized())
            {
                ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                ShowHRoi();
                region.Dispose();
            }
            else
            {
                ShowHRoi();
            }
        }

        private void RefreshPreview()
        {
            var view = ModuleView as AreaSprayView;
            if (view?.mWindowH == null)
                return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.Image = new RImage(DispImage);
                    view.mWindowH.DispObj(view.mWindowH.Image);
                }
                else
                {
                    view.mWindowH.ClearWindow();
                }
            });
        }

        private void TryExecutePreview()
        {
            if (string.IsNullOrWhiteSpace(InputImageLinkText))
                return;

            if (!IsRealtimePreview)
            {
                GetDispImage(InputImageLinkText, true);
                RefreshPreview();
                ShowCurrentRoi();
                return;
            }

            try
            {
                ExeModule();
                ShowCurrentRoi();
            }
            catch
            {
            }
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                if (!string.IsNullOrWhiteSpace(_InputImageLinkText))
                {
                    GetDispImage(_InputImageLinkText, true);
                    RefreshPreview();
                    ShowCurrentRoi();
                    TryExecutePreview();
                }
            }
        }

        private eRoiType _SelectedROIType = eRoiType.FullImage;
        public eRoiType SelectedROIType
        {
            get { return _SelectedROIType; }
            set
            {
                Set(ref _SelectedROIType, value);
                ShowCurrentRoi();
                TryExecutePreview();
            }
        }

        private string _InputRoiLinkText;
        public string InputRoiLinkText
        {
            get { return _InputRoiLinkText; }
            set
            {
                Set(ref _InputRoiLinkText, value);
                ShowCurrentRoi();
                TryExecutePreview();
            }
        }

        private double _GrayValue = 128;
        public double GrayValue
        {
            get { return _GrayValue; }
            set
            {
                Set(ref _GrayValue, value);
                TryExecutePreview();
            }
        }

        private bool _IsRealtimePreview = false;
        public bool IsRealtimePreview
        {
            get { return _IsRealtimePreview; }
            set
            {
                Set(ref _IsRealtimePreview, value);
                if (_IsRealtimePreview)
                {
                    TryExecutePreview();
                }
                else if (!string.IsNullOrWhiteSpace(InputImageLinkText))
                {
                    GetDispImage(InputImageLinkText, true);
                    RefreshPreview();
                    ShowCurrentRoi();
                }
            }
        }

        [NonSerialized]
        private RImage _ResultImage;
        public RImage ResultImage
        {
            get { return _ResultImage; }
            set { Set(ref _ResultImage, value); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as AreaSprayView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                SetDefaultLink();
                ShowCurrentRoi();
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1]);
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
                    break;
                case eLinkCommand.InputRoiLink:
                    InputRoiLinkText = obj.LinkName;
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
                        string type = obj.ToString() == nameof(eLinkCommand.InputRoiLink) ? "HRegion" : "HImage";
                        CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, type);
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{obj}");
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
                    _ExecuteCommand = new CommandBase((obj) => { ExeModule(); });
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
                        var view = ModuleView as AreaSprayView;
                        if (view != null)
                        {
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["SelectedROIType"] = (int)SelectedROIType;
            obj["InputRoiLinkText"] = InputRoiLinkText ?? "";
            obj["GrayValue"] = GrayValue;
            obj["IsRealtimePreview"] = IsRealtimePreview;
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
                if (obj["SelectedROIType"] != null) SelectedROIType = (eRoiType)obj["SelectedROIType"].Value<int>();
                if (obj["InputRoiLinkText"] != null) InputRoiLinkText = obj["InputRoiLinkText"].ToString();
                if (obj["GrayValue"] != null) GrayValue = obj["GrayValue"].Value<double>();
                if (obj["IsRealtimePreview"] != null) IsRealtimePreview = obj["IsRealtimePreview"].Value<bool>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"AreaSprayViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
