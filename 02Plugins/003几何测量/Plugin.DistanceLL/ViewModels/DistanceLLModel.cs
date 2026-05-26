using EventMgrLib;
using HalconDotNet;
using Plugin.DistanceLL.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
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

namespace Plugin.DistanceLL.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Line1,
        Line2,
        Line1Point1X,
        Line1Point1Y,
        Line1Point2X,
        Line1Point2Y,
        Line2Point1X,
        Line2Point1Y,
        Line2Point2X,
        Line2Point2Y,
    }

    public enum eLineSourceMode
    {
        链接直线,
        两点构造,
    }

    public enum eDistanceMode
    {
        线1中点到线2距离,
        线2中点到线1距离,
        两条线最大距离,
        两条线最小距离,
        两条线平均距离,
    }
    #endregion

    [Category("几何测量")]
    [DisplayName("线线距离")]
    [ModuleImageName("DistanceLL")]
    [Serializable]
    public class DistanceLLModel : ModuleBase
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
                if (DispImage != null && DispImage.IsInitialized())
                {
                    // 两点构造模式下，从链接变量读取点坐标实际值
                    if (Line1SourceMode == eLineSourceMode.两点构造)
                    {
                        Line1Point1X = Convert.ToDouble(GetLinkValue(Line1P1XLinkText));
                        Line1Point1Y = Convert.ToDouble(GetLinkValue(Line1P1YLinkText));
                        Line1Point2X = Convert.ToDouble(GetLinkValue(Line1P2XLinkText));
                        Line1Point2Y = Convert.ToDouble(GetLinkValue(Line1P2YLinkText));
                    }
                    if (Line2SourceMode == eLineSourceMode.两点构造)
                    {
                        Line2Point1X = Convert.ToDouble(GetLinkValue(Line2P1XLinkText));
                        Line2Point1Y = Convert.ToDouble(GetLinkValue(Line2P1YLinkText));
                        Line2Point2X = Convert.ToDouble(GetLinkValue(Line2P2XLinkText));
                        Line2Point2Y = Convert.ToDouble(GetLinkValue(Line2P2YLinkText));
                    }

                    ROILine line1 = GetLine(Line1SourceMode, Line1LinkText,
                        Line1Point1X, Line1Point1Y, Line1Point2X, Line1Point2Y);
                    ROILine line2 = GetLine(Line2SourceMode, Line2LinkText,
                        Line2Point1X, Line2Point1Y, Line2Point2X, Line2Point2Y);

                    if (line1 == null || line2 == null)
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }

                    HObject resultLine = null;
                    HObject cross1 = null, cross2 = null;
                    int crossSize = 30;

                    switch (DistanceMode)
                    {
                        case eDistanceMode.线1中点到线2距离:
                            {
                                double midX = (line1.StartX + line1.EndX) / 2.0;
                                double midY = (line1.StartY + line1.EndY) / 2.0;
                                HMisc.ProjectionPl(midX, midY,
                                    line2.StartX, line2.StartY, line2.EndX, line2.EndY,
                                    out double projX, out double projY);
                                Distance = Math.Round(HMisc.DistancePl(midX, midY,
                                    line2.StartX, line2.StartY, line2.EndX, line2.EndY), 4);
                                Gen.GenCross(out cross1, midY, midX, crossSize, 0);
                                Gen.GenCross(out cross2, projY, projX, crossSize, 0);
                                Gen.GenContour(out resultLine, midY, projY, midX, projX);
                            }
                            break;
                        case eDistanceMode.线2中点到线1距离:
                            {
                                double midX = (line2.StartX + line2.EndX) / 2.0;
                                double midY = (line2.StartY + line2.EndY) / 2.0;
                                HMisc.ProjectionPl(midX, midY,
                                    line1.StartX, line1.StartY, line1.EndX, line1.EndY,
                                    out double projX, out double projY);
                                Distance = Math.Round(HMisc.DistancePl(midX, midY,
                                    line1.StartX, line1.StartY, line1.EndX, line1.EndY), 4);
                                Gen.GenCross(out cross1, midY, midX, crossSize, 0);
                                Gen.GenCross(out cross2, projY, projX, crossSize, 0);
                                Gen.GenContour(out resultLine, midY, projY, midX, projX);
                            }
                            break;
                        case eDistanceMode.两条线最大距离:
                            {
                                HMisc.DistanceSl(
                                    line1.StartX, line1.StartY, line1.EndX, line1.EndY,
                                    line2.StartX, line2.StartY, line2.EndX, line2.EndY,
                                    out double minDist, out double maxDist);
                                Distance = Math.Round(maxDist, 4);
                                // 在两条线的中点处显示标记
                                double mid1X = (line1.StartX + line1.EndX) / 2.0;
                                double mid1Y = (line1.StartY + line1.EndY) / 2.0;
                                double mid2X = (line2.StartX + line2.EndX) / 2.0;
                                double mid2Y = (line2.StartY + line2.EndY) / 2.0;
                                Gen.GenCross(out cross1, mid1Y, mid1X, crossSize, 0);
                                Gen.GenCross(out cross2, mid2Y, mid2X, crossSize, 0);
                                Gen.GenContour(out resultLine, mid1Y, mid2Y, mid1X, mid2X);
                            }
                            break;
                        case eDistanceMode.两条线最小距离:
                            {
                                HMisc.DistanceSl(
                                    line1.StartX, line1.StartY, line1.EndX, line1.EndY,
                                    line2.StartX, line2.StartY, line2.EndX, line2.EndY,
                                    out double minDist, out double maxDist);
                                Distance = Math.Round(minDist, 4);
                                double mid1X = (line1.StartX + line1.EndX) / 2.0;
                                double mid1Y = (line1.StartY + line1.EndY) / 2.0;
                                double mid2X = (line2.StartX + line2.EndX) / 2.0;
                                double mid2Y = (line2.StartY + line2.EndY) / 2.0;
                                Gen.GenCross(out cross1, mid1Y, mid1X, crossSize, 0);
                                Gen.GenCross(out cross2, mid2Y, mid2X, crossSize, 0);
                                Gen.GenContour(out resultLine, mid1Y, mid2Y, mid1X, mid2X);
                            }
                            break;
                        case eDistanceMode.两条线平均距离:
                            {
                                Distance = Math.Round(Dis.DisLL(line1, line2), 4);
                                ROILine midLine = Dis.middleLine(line1, line2);
                                Gen.GenCross(out cross1, (line1.StartY + line1.EndY) / 2.0,
                                    (line1.StartX + line1.EndX) / 2.0, crossSize, 0);
                                Gen.GenCross(out cross2, (line2.StartY + line2.EndY) / 2.0,
                                    (line2.StartX + line2.EndX) / 2.0, crossSize, 0);
                                if (midLine != null)
                                    Gen.GenContour(out resultLine, midLine.StartY, midLine.EndY,
                                        midLine.StartX, midLine.EndX);
                            }
                            break;
                    }

                    if (ShowResultLine && resultLine != null && resultLine.IsInitialized())
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                            ModuleParam.Remarks, HRoiType.测量直线1, "cyan", new HObject(resultLine)));

                    if (ShowResultPoint && cross1 != null && cross1.IsInitialized())
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                            ModuleParam.Remarks, HRoiType.检测点P1, "green", new HObject(cross1)));

                    if (ShowResultPoint && cross2 != null && cross2.IsInitialized())
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName,
                            ModuleParam.Remarks, HRoiType.检测点P2, "green", new HObject(cross2)));

                    ShowHRoi();
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
            AddOutputParam("距离", "double", Distance);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        private ROILine GetLine(eLineSourceMode mode, string linkText,
            double p1x, double p1y, double p2x, double p2y)
        {
            if (mode == eLineSourceMode.链接直线)
            {
                return (ROILine)Prj.GetParamByName(linkText).Value;
            }
            else
            {
                return new ROILine()
                {
                    StartX = p1x,
                    StartY = p1y,
                    EndX = p2x,
                    EndY = p2y,
                };
            }
        }

        #region Prop
        private double _Distance;
        public double Distance
        {
            get { return _Distance; }
            set { Set(ref _Distance, value); }
        }

        private eDistanceMode _DistanceMode = eDistanceMode.两条线平均距离;
        public eDistanceMode DistanceMode
        {
            get { return _DistanceMode; }
            set { Set(ref _DistanceMode, value); }
        }

        private bool _ShowResultLine = true;
        public bool ShowResultLine
        {
            get { return _ShowResultLine; }
            set { Set(ref _ShowResultLine, value); }
        }

        private bool _ShowResultPoint = true;
        public bool ShowResultPoint
        {
            get { return _ShowResultPoint; }
            set { Set(ref _ShowResultPoint, value); }
        }

        public ROILine Line1 { get; set; } = new ROILine();
        public ROILine Line2 { get; set; } = new ROILine();

        // 直线1输入模式
        private eLineSourceMode _Line1SourceMode = eLineSourceMode.链接直线;
        public eLineSourceMode Line1SourceMode
        {
            get { return _Line1SourceMode; }
            set { Set(ref _Line1SourceMode, value); }
        }

        // 直线2输入模式
        private eLineSourceMode _Line2SourceMode = eLineSourceMode.链接直线;
        public eLineSourceMode Line2SourceMode
        {
            get { return _Line2SourceMode; }
            set { Set(ref _Line2SourceMode, value); }
        }

        // 直线1两点坐标
        private string _Line1P1XLinkText;
        public string Line1P1XLinkText
        {
            get { return _Line1P1XLinkText; }
            set { Set(ref _Line1P1XLinkText, value); }
        }
        private string _Line1P1YLinkText;
        public string Line1P1YLinkText
        {
            get { return _Line1P1YLinkText; }
            set { Set(ref _Line1P1YLinkText, value); }
        }
        private string _Line1P2XLinkText;
        public string Line1P2XLinkText
        {
            get { return _Line1P2XLinkText; }
            set { Set(ref _Line1P2XLinkText, value); }
        }
        private string _Line1P2YLinkText;
        public string Line1P2YLinkText
        {
            get { return _Line1P2YLinkText; }
            set { Set(ref _Line1P2YLinkText, value); }
        }

        public double Line1Point1X { get; set; }
        public double Line1Point1Y { get; set; }
        public double Line1Point2X { get; set; }
        public double Line1Point2Y { get; set; }

        // 直线2两点坐标
        private string _Line2P1XLinkText;
        public string Line2P1XLinkText
        {
            get { return _Line2P1XLinkText; }
            set { Set(ref _Line2P1XLinkText, value); }
        }
        private string _Line2P1YLinkText;
        public string Line2P1YLinkText
        {
            get { return _Line2P1YLinkText; }
            set { Set(ref _Line2P1YLinkText, value); }
        }
        private string _Line2P2XLinkText;
        public string Line2P2XLinkText
        {
            get { return _Line2P2XLinkText; }
            set { Set(ref _Line2P2XLinkText, value); }
        }
        private string _Line2P2YLinkText;
        public string Line2P2YLinkText
        {
            get { return _Line2P2YLinkText; }
            set { Set(ref _Line2P2YLinkText, value); }
        }

        public double Line2Point1X { get; set; }
        public double Line2Point1Y { get; set; }
        public double Line2Point2X { get; set; }
        public double Line2Point2Y { get; set; }

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private string _Line1LinkText;
        public string Line1LinkText
        {
            get { return _Line1LinkText; }
            set { Set(ref _Line1LinkText, value); }
        }

        private string _Line2LinkText;
        public string Line2LinkText
        {
            get { return _Line2LinkText; }
            set { Set(ref _Line2LinkText, value); }
        }
        #endregion

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["DistanceMode"] = (int)DistanceMode;
            obj["ShowResultLine"] = ShowResultLine;
            obj["ShowResultPoint"] = ShowResultPoint;
            obj["Line1SourceMode"] = (int)Line1SourceMode;
            obj["Line2SourceMode"] = (int)Line2SourceMode;
            obj["Line1P1XLinkText"] = Line1P1XLinkText ?? "";
            obj["Line1P1YLinkText"] = Line1P1YLinkText ?? "";
            obj["Line1P2XLinkText"] = Line1P2XLinkText ?? "";
            obj["Line1P2YLinkText"] = Line1P2YLinkText ?? "";
            obj["Line2P1XLinkText"] = Line2P1XLinkText ?? "";
            obj["Line2P1YLinkText"] = Line2P1YLinkText ?? "";
            obj["Line2P2XLinkText"] = Line2P2XLinkText ?? "";
            obj["Line2P2YLinkText"] = Line2P2YLinkText ?? "";
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["Line1LinkText"] = Line1LinkText ?? "";
            obj["Line2LinkText"] = Line2LinkText ?? "";
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["DistanceMode"] != null) DistanceMode = (eDistanceMode)obj["DistanceMode"].Value<int>();
                if (obj["ShowResultLine"] != null) ShowResultLine = obj["ShowResultLine"].Value<bool>();
                if (obj["ShowResultPoint"] != null) ShowResultPoint = obj["ShowResultPoint"].Value<bool>();
                if (obj["Line1SourceMode"] != null) Line1SourceMode = (eLineSourceMode)obj["Line1SourceMode"].Value<int>();
                if (obj["Line2SourceMode"] != null) Line2SourceMode = (eLineSourceMode)obj["Line2SourceMode"].Value<int>();
                if (obj["Line1P1XLinkText"] != null) Line1P1XLinkText = obj["Line1P1XLinkText"].ToString();
                if (obj["Line1P1YLinkText"] != null) Line1P1YLinkText = obj["Line1P1YLinkText"].ToString();
                if (obj["Line1P2XLinkText"] != null) Line1P2XLinkText = obj["Line1P2XLinkText"].ToString();
                if (obj["Line1P2YLinkText"] != null) Line1P2YLinkText = obj["Line1P2YLinkText"].ToString();
                if (obj["Line2P1XLinkText"] != null) Line2P1XLinkText = obj["Line2P1XLinkText"].ToString();
                if (obj["Line2P1YLinkText"] != null) Line2P1YLinkText = obj["Line2P1YLinkText"].ToString();
                if (obj["Line2P2XLinkText"] != null) Line2P2XLinkText = obj["Line2P2XLinkText"].ToString();
                if (obj["Line2P2YLinkText"] != null) Line2P2YLinkText = obj["Line2P2YLinkText"].ToString();
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["Line1LinkText"] != null) Line1LinkText = obj["Line1LinkText"].ToString();
                if (obj["Line2LinkText"] != null) Line2LinkText = obj["Line2LinkText"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"DistanceLLModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as DistanceLLView;
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
                        var view = this.ModuleView as DistanceLLView;
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
                case "Line1":
                    Line1LinkText = obj.LinkName;
                    break;
                case "Line2":
                    Line2LinkText = obj.LinkName;
                    break;
                case "Line1Point1X":
                    Line1P1XLinkText = obj.LinkName;
                    break;
                case "Line1Point1Y":
                    Line1P1YLinkText = obj.LinkName;
                    break;
                case "Line1Point2X":
                    Line1P2XLinkText = obj.LinkName;
                    break;
                case "Line1Point2Y":
                    Line1P2YLinkText = obj.LinkName;
                    break;
                case "Line2Point1X":
                    Line2P1XLinkText = obj.LinkName;
                    break;
                case "Line2Point1Y":
                    Line2P1YLinkText = obj.LinkName;
                    break;
                case "Line2Point2X":
                    Line2P2XLinkText = obj.LinkName;
                    break;
                case "Line2Point2Y":
                    Line2P2YLinkText = obj.LinkName;
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
                            case eLinkCommand.Line1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "object");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1");
                                break;
                            case eLinkCommand.Line2:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "object");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line2");
                                break;
                            case eLinkCommand.Line1Point1X:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1Point1X");
                                break;
                            case eLinkCommand.Line1Point1Y:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1Point1Y");
                                break;
                            case eLinkCommand.Line1Point2X:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1Point2X");
                                break;
                            case eLinkCommand.Line1Point2Y:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1Point2Y");
                                break;
                            case eLinkCommand.Line2Point1X:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line2Point1X");
                                break;
                            case eLinkCommand.Line2Point1Y:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line2Point1Y");
                                break;
                            case eLinkCommand.Line2Point2X:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line2Point2X");
                                break;
                            case eLinkCommand.Line2Point2Y:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line2Point2Y");
                                break;
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
    }
}
