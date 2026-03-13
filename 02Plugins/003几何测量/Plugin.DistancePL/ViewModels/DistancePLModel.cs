using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
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
using Plugin.DistancePL.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Shapes;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.DistancePL.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Line1,
        X,
        Y,
        
    }
    #endregion

    [Category("几何测量")]
    [DisplayName("点线距离")]
    [ModuleImageName("DistancePL")]
    [Serializable]
    public class DistancePLModel : ModuleBase
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

        // ========== 新增手动输入属性 ==========

        private bool _UseManualLine;
        public bool UseManualLine
        {
            get { return _UseManualLine; }
            set { Set(ref _UseManualLine, value); }
        }

      
        private double _ManualLineAngle=0;
        public double ManualLineAngle
        {
            get { return _ManualLineAngle; }
            set { Set(ref _ManualLineAngle, value); }
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

                    // 根据模式获取直线参数
                    ROILine line = null;
                    line = (ROILine)Prj.GetParamByName(Line1LinkText).Value;
                    

                    // 计算垂足和距离
                    Dis.PLPedal(PXLinkValue, PYLinkValue, line, out double outY, out double outX, out double dis);
                    PointX = Math.Round(outX, 4);
                    PointY = Math.Round(outY, 4);
                    Distance = Math.Round(dis, 4);

                    // 图形生成（垂足点、原始点、虚线、垂线）
                    double size = 30; 
                    HObject x1 = null, x2 = null, allDashes = null, perpLine = null;

                    // 垂足点
                    if (ShowResultPoint)
                    {
                        double row1 = outY, col1 = outX;
                        HOperatorSet.GenEmptyObj(out x1);
                        HOperatorSet.GenContourPolygonXld(out HObject line1,
                            new HTuple(new double[] { row1 - size / 2, row1 + size / 2 }),
                            new HTuple(new double[] { col1 - size / 2, col1 + size / 2 }));
                        HOperatorSet.GenContourPolygonXld(out HObject line2,
                            new HTuple(new double[] { row1 - size / 2, row1 + size / 2 }),
                            new HTuple(new double[] { col1 + size / 2, col1 - size / 2 }));
                        HOperatorSet.ConcatObj(line1, line2, out x1);
                    }

                    // 原始点
                    if (ShowResultPoint)
                    {
                        double row2 = PYLinkValue, col2 = PXLinkValue;
                        HOperatorSet.GenEmptyObj(out x2);
                        HOperatorSet.GenContourPolygonXld(out HObject line1,
                            new HTuple(new double[] { row2 - size / 2, row2 + size / 2 }),
                            new HTuple(new double[] { col2 - size / 2, col2 + size / 2 }));
                        HOperatorSet.GenContourPolygonXld(out HObject line2,
                            new HTuple(new double[] { row2 - size / 2, row2 + size / 2 }),
                            new HTuple(new double[] { col2 + size / 2, col2 - size / 2 }));
                        HOperatorSet.ConcatObj(line1, line2, out x2);
                    }

                    // 虚线（原点到垂足）
                    if (ShowResultLine)
                    {
                        double startX = PXLinkValue, startY = PYLinkValue;
                        double endX = outX, endY = outY;
                        double length = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
                        if (length >= 1e-6)
                        {
                            double dashLength = 30, gapLength = 10;
                            double segmentLength = dashLength + gapLength;
                            int dashCount = (int)Math.Ceiling(length / segmentLength);
                            double dx = (endX - startX) / length;
                            double dy = (endY - startY) / length;
                            HOperatorSet.GenEmptyObj(out allDashes);
                            for (int i = 0; i < dashCount; i++)
                            {
                                double cycleStartX = startX + i * segmentLength * dx;
                                double cycleStartY = startY + i * segmentLength * dy;
                                double segEndX = cycleStartX + dashLength * dx;
                                double segEndY = cycleStartY + dashLength * dy;
                                if (Math.Abs(segEndX - startX) > Math.Abs(endX - startX) ||
                                    Math.Abs(segEndY - startY) > Math.Abs(endY - startY))
                                {
                                    segEndX = endX;
                                    segEndY = endY;
                                }
                                HOperatorSet.GenContourPolygonXld(out HObject dashSegment,
                                    new HTuple(new double[] { cycleStartY, segEndY }),
                                    new HTuple(new double[] { cycleStartX, segEndX }));
                                HOperatorSet.ConcatObj(allDashes, dashSegment, out allDashes);
                                if (segEndX == endX && segEndY == endY) break;
                            }
                        }
                    }



                    // 垂线（以垂足为中心，方向由 ManualLineAngle 控制）

                    if (ShowResultLine)
                    {
                        double angleRad = ManualLineAngle * Math.PI / 180.0; // 角度转弧度
                        double halfLength = 200; // 线段半长，可改为配置属性

                        double dx = halfLength * Math.Cos(angleRad);
                        double dy = halfLength * Math.Sin(angleRad);

                        double startX = outX - dx;
                        double startY = outY - dy;
                        double endX = outX + dx;
                        double endY = outY + dy;

                        HOperatorSet.GenEmptyObj(out perpLine);
                        HOperatorSet.GenContourPolygonXld(out perpLine,
                            new HTuple(new double[] { startY, endY }),
                            new HTuple(new double[] { startX, endX }));
                    }

                    // 合并显示（垂足点+虚线+垂线）作为检测点P1
                    if (ShowResultPoint || ShowResultLine)
                    {
                        HObject combined = null;
                        HOperatorSet.GenEmptyObj(out combined);
                        if (ShowResultPoint && x1 != null)
                            HOperatorSet.ConcatObj(combined, x1, out combined);
                        if (ShowResultLine && allDashes != null)
                            HOperatorSet.ConcatObj(combined, allDashes, out combined);
                        if (ShowResultLine && perpLine != null)
                            HOperatorSet.ConcatObj(combined, perpLine, out combined);
                        if (combined != null && combined.IsInitialized())
                        {
                            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                                              HRoiType.检测点P1, "cyan", combined));
                        }
                    }

                    // 原始点作为检测点P2
                    if (ShowResultPoint && x2 != null)
                    {
                        ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks,
                                          HRoiType.检测点P2, "cyan", x2));
                    }

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
            AddOutputParam("垂点X", "double", PointX);
            AddOutputParam("垂点Y", "double", PointY);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop
        private double _Distance;
        public double Distance
        {
            get { return _Distance; }
            set { Set(ref _Distance, value); }
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

        private string _PXLinkText;
        public string PXLinkText
        {
            get { return _PXLinkText; }
            set { Set(ref _PXLinkText, value); }
        }

        private double _PXLinkValue;
        public double PXLinkValue
        {
            get { return _PXLinkValue; }
            set { _PXLinkValue = value; }
        }

        private string _PYLinkText;
        public string PYLinkText
        {
            get { return _PYLinkText; }
            set { Set(ref _PYLinkText, value); }
        }

        private double _PYLinkValue;
        public double PYLinkValue
        {
            get { return _PYLinkValue; }
            set { _PYLinkValue = value; }
        }

        private double _PointX;
        public double PointX
        {
            get { return _PointX; }
            set { Set(ref _PointX, value); }
        }

        private double _PointY;
        public double PointY
        {
            get { return _PointY; }
            set { Set(ref _PointY, value); }
        }

     

        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as DistancePLView;
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
                        var view = this.ModuleView as DistancePLView;
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
                case "PX":
                    PXLinkText = obj.LinkName;
                    break;
                case "PY":
                    PYLinkText = obj.LinkName;
                    break;
              
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
                            case eLinkCommand.Line1:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "object");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},Line1");
                                break;
                            case eLinkCommand.X:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},PX");
                                break;
                            case eLinkCommand.Y:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},PY");
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