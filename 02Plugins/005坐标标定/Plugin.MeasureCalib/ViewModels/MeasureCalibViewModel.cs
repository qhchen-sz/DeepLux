using EventMgrLib;
using Plugin.MeasureCalib.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
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

namespace Plugin.MeasureCalib.ViewModels
{
    public enum eCalibMode
    {
        Ratio,
        HolePlate,
    }

    public enum eLinkCommand
    {
        CameraAngleLink,
    }

    [Category("坐标标定")]
    [DisplayName("测量标定")]
    [ModuleImageName("MeasureCalib")]
    [Serializable]
    public class MeasureCalibViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            Stopwatch.Restart();

            try
            {
                switch (CalibMode)
                {
                    case eCalibMode.Ratio:
                        // 比例模式：直接使用设置的像素当量值
                        break;
                    case eCalibMode.HolePlate:
                        throw new NotImplementedException("孔板模式暂未实现");
                    default:
                        break;
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
            this.Prj.ClearOutputParam(this.ModuleParam);
            AddOutputParam("像素当量", "double[]", new List<double> { PixelEquivalentX, PixelEquivalentY, PixelEquivalentZ });
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop

        private eCalibMode _CalibMode = eCalibMode.Ratio;
        /// <summary>
        /// 标定模式
        /// </summary>
        public eCalibMode CalibMode
        {
            get { return _CalibMode; }
            set
            {
                _CalibMode = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 标定模式字典（用于下拉框显示中文名称）
        /// </summary>
        public Dictionary<eCalibMode, string> CalibModeDict
        {
            get
            {
                return new Dictionary<eCalibMode, string>
                {
                    { eCalibMode.Ratio, "比例模式" },
                    { eCalibMode.HolePlate, "孔板模式" },
                };
            }
        }

        private string _CameraAngleLinkText = "0";
        /// <summary>
        /// 相机角度链接文本
        /// </summary>
        public string CameraAngleLinkText
        {
            get { return _CameraAngleLinkText; }
            set
            {
                _CameraAngleLinkText = value;
                RaisePropertyChanged();
            }
        }

        private double _PixelEquivalentX = 1.0;
        /// <summary>
        /// X方向像素当量 (um/pix)
        /// </summary>
        public double PixelEquivalentX
        {
            get { return _PixelEquivalentX; }
            set
            {
                if (Math.Abs(value) < 1e-10)
                {
                    if (_PixelEquivalentX > 0)
                    {
                        _PixelEquivalentX = -0.1;
                        //Logger.AddLog("像素当量X不能为0，已自动跳至-0.1", eMsgType.Warn);
                    }
                    else if (_PixelEquivalentX < 0)
                    {
                        _PixelEquivalentX = 0.1;
                        //Logger.AddLog("像素当量X不能为0，已自动跳至0.1", eMsgType.Warn);
                    }
                }
                else
                {
                    _PixelEquivalentX = value;
                }
                RaisePropertyChanged();
            }
        }

        private double _PixelEquivalentY = 1.0;
        /// <summary>
        /// Y方向像素当量 (um/pix)
        /// </summary>
        public double PixelEquivalentY
        {
            get { return _PixelEquivalentY; }
            set
            {
                if (Math.Abs(value) < 1e-10)
                {
                    if (_PixelEquivalentY > 0)
                    {
                        _PixelEquivalentY = -0.1;
                        //Logger.AddLog("像素当量Y不能为0，已自动跳至-0.1", eMsgType.Warn);
                    }
                    else if (_PixelEquivalentY < 0)
                    {
                        _PixelEquivalentY = 0.1;
                        //Logger.AddLog("像素当量Y不能为0，已自动跳至0.1", eMsgType.Warn);
                    }
                }
                else
                {
                    _PixelEquivalentY = value;
                }
                RaisePropertyChanged();
            }
        }

        private double _PixelEquivalentZ = 1.0;
        /// <summary>
        /// Z方向像素当量 (um/pix)
        /// </summary>
        public double PixelEquivalentZ
        {
            get { return _PixelEquivalentZ; }
            set
            {
                if (Math.Abs(value) < 1e-10)
                {
                    if (_PixelEquivalentZ > 0)
                    {
                        _PixelEquivalentZ = -0.1;
                        //Logger.AddLog("像素当量Z不能为0，已自动跳至-0.1", eMsgType.Warn);
                    }
                    else if (_PixelEquivalentZ < 0)
                    {
                        _PixelEquivalentZ = 0.1;
                        //Logger.AddLog("像素当量Z不能为0，已自动跳至0.1", eMsgType.Warn);
                    }
                }
                else
                {
                    _PixelEquivalentZ = value;
                }
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Command

        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as MeasureCalibView;
            if (view != null)
            {
                ClosedView = true;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.CameraAngleLink:
                    CameraAngleLinkText = obj.LinkName;
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
                            case eLinkCommand.CameraAngleLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},CameraAngleLink");
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
                        var view = this.ModuleView as MeasureCalibView;
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

        #region 序列化
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["CalibMode"] = (int)CalibMode;
            obj["CameraAngleLinkText"] = CameraAngleLinkText ?? "";
            obj["PixelEquivalentX"] = PixelEquivalentX;
            obj["PixelEquivalentY"] = PixelEquivalentY;
            obj["PixelEquivalentZ"] = PixelEquivalentZ;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["CalibMode"] != null) CalibMode = (eCalibMode)obj["CalibMode"].Value<int>();
                if (obj["CameraAngleLinkText"] != null) CameraAngleLinkText = obj["CameraAngleLinkText"].ToString();
                if (obj["PixelEquivalentX"] != null) PixelEquivalentX = obj["PixelEquivalentX"].Value<double>();
                if (obj["PixelEquivalentY"] != null) PixelEquivalentY = obj["PixelEquivalentY"].Value<double>();
                if (obj["PixelEquivalentZ"] != null) PixelEquivalentZ = obj["PixelEquivalentZ"].Value<double>();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"MeasureCalibViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        #endregion
    }
}
