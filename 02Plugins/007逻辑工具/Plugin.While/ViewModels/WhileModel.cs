using EventMgrLib;
using Newtonsoft.Json.Linq;
using Plugin.While.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.ViewModels;

namespace Plugin.While.ViewModels
{
    #region enum
    #endregion
    [Category("逻辑工具")]
    [DisplayName("循环工具")]
    [ModuleImageName("While")]
    [Serializable]
    public class WhileModel : ModuleBase
    {
        // 添加新的字段来存储double类型的值
        private double _currentDoubleIndex = 0.0;
        private double _currentProgress = 0.0;
        private double _currentValue = 0.0;

        public override bool ExeModule()
        {
            int start = 0;
            int end = 0;
            Stopwatch.Restart();
            switch (LoopMode)
            {
                case eLoopMode.Increase:
                    start = Convert.ToInt32(GetLinkValue(Start));
                    end = Convert.ToInt32(GetLinkValue(End));
                    ModuleParam.CyclicCount = end - start;
                    // 更新double值
                    _currentDoubleIndex = Convert.ToDouble(ModuleParam.pIndex);
                    _currentProgress = ModuleParam.CyclicCount > 0 ?
                        (double)ModuleParam.pIndex / ModuleParam.CyclicCount : 0.0;
                    _currentValue = start + _currentDoubleIndex;
                    break;
                case eLoopMode.Decrease:
                    start = Convert.ToInt32(GetLinkValue(Start));
                    end = Convert.ToInt32(GetLinkValue(End));
                    ModuleParam.CyclicCount = end - start;
                    // 更新double值
                    _currentDoubleIndex = Convert.ToDouble(ModuleParam.pIndex);
                    _currentProgress = ModuleParam.CyclicCount > 0 ?
                        (double)ModuleParam.pIndex / ModuleParam.CyclicCount : 0.0;
                    _currentValue = start - _currentDoubleIndex;
                    break;
                case eLoopMode.Loop:
                    ModuleParam.CyclicCount = int.MaxValue;
                    // 更新double值
                    _currentDoubleIndex = Convert.ToDouble(ModuleParam.pIndex);
                    _currentProgress = 0.0; // 无限循环无法计算进度
                    _currentValue = _currentDoubleIndex;
                    break;
                case eLoopMode.Foreach:
                    // 更新double值
                    _currentDoubleIndex = Convert.ToDouble(ModuleParam.pIndex);
                    _currentProgress = 0.0;
                    _currentValue = _currentDoubleIndex;
                    break;
                default:
                    break;
            }

            // 触发属性变更通知，以便输出参数更新
            RaisePropertyChanged(nameof(CurrentDoubleIndex));
            RaisePropertyChanged(nameof(Progress));
            RaisePropertyChanged(nameof(CurrentValue));

            ChangeModuleRunStatus(eRunStatus.OK);
            return true;
        }

        // 添加一个方法来更新double输出值
        private void UpdateDoubleOutputs()
        {
            if (ModuleParam != null && ModuleParam.pIndex != null)
            {
                // 更新double类型的索引值
                _currentDoubleIndex = Convert.ToDouble(ModuleParam.pIndex);

                // 更新归一化值（0.0-1.0之间的进度）
                if (ModuleParam.CyclicCount > 0 && ModuleParam.CyclicCount != int.MaxValue)
                {
                    _currentProgress = (double)ModuleParam.pIndex / ModuleParam.CyclicCount;
                }
                else
                {
                    _currentProgress = 0.0;
                }

                // 更新当前值（根据起始值和索引计算）
                if (LoopMode == eLoopMode.Increase)
                {
                    double startVal = Convert.ToDouble(GetLinkValue(Start));
                    _currentValue = startVal + _currentDoubleIndex;
                }
                else if (LoopMode == eLoopMode.Decrease)
                {
                    double startVal = Convert.ToDouble(GetLinkValue(Start));
                    _currentValue = startVal - _currentDoubleIndex;
                }
                else
                {
                    _currentValue = _currentDoubleIndex;
                }

                // 触发属性变更通知
                RaisePropertyChanged(nameof(CurrentDoubleIndex));
                RaisePropertyChanged(nameof(Progress));
                RaisePropertyChanged(nameof(CurrentValue));
            }
        }

        // 重写基类方法，添加double类型的输出参数
        public override void AddOutputParams()
        {
            // 原有的int索引输出
            AddOutputParam("索引", "int", ModuleParam.pIndex+2);

            // 添加double类型的索引输出 - 使用属性而不是lambda
            AddOutputParam("索引Double", "double", CurrentDoubleIndex+2);

            // 添加归一化进度输出（0.0-1.0）
            AddOutputParam("进度", "double", Progress);

            // 添加当前值输出（根据起始值和索引计算）
            AddOutputParam("当前值", "double", CurrentValue);

            base.AddOutputParams();
        }

        // 添加属性来暴露double值
        [Browsable(false)]  // 不在属性窗口中显示，只作为输出参数
        public double CurrentDoubleIndex
        {
            get { return _currentDoubleIndex; }
            private set
            {
                if (_currentDoubleIndex != value)
                {
                    _currentDoubleIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Browsable(false)]  // 不在属性窗口中显示，只作为输出参数
        public double Progress
        {
            get { return _currentProgress; }
            private set
            {
                if (_currentProgress != value)
                {
                    _currentProgress = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Browsable(false)]  // 不在属性窗口中显示，只作为输出参数
        public double CurrentValue
        {
            get { return _currentValue; }
            private set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    RaisePropertyChanged();
                }
            }
        }

        // 可以添加一个方法来获取double类型的索引（供其他模块使用）
        public double GetDoubleIndex()
        {
            return CurrentDoubleIndex;
        }

        #region Prop
        private LinkVarModel _Start = new LinkVarModel() { Text = "0" };

        public LinkVarModel Start
        {
            get { return _Start; }
            set { _Start = value; RaisePropertyChanged(); }
        }
        private LinkVarModel _End = new LinkVarModel() { Text = "0" };

        public LinkVarModel End
        {
            get { return _End; }
            set { _End = value; RaisePropertyChanged(); }
        }
        private eLoopMode _LoopMode = eLoopMode.Increase;

        public eLoopMode LoopMode
        {
            get { return _LoopMode; }
            set { _LoopMode = value; RaisePropertyChanged(); }
        }
        #endregion
        #region Command
        private void OnVarChanged(VarChangedEventParamModel obj)
        {

            switch (obj.SendName.Split(',')[1])
            {
                case "StartLinkText":
                    Start.Text = obj.LinkName;
                    break;
                case "EndLinkText":
                    End.Text = obj.LinkName;
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
                    //以类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        switch (obj.ToString())
                        {
                            case "Start":
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},StartLinkText");
                                break;
                            case "End":
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},EndLinkText");
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
        private CommandBase _ConfirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase((obj) =>
                    {
                        var view = this.ModuleView as WhileView;
                        if (view != null)
                        {
                            ExeModule();
                            view.Close();
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        #endregion

        // 如果需要，可以添加一个方法来重置double值
        public void ResetDoubleValues()
        {
            CurrentDoubleIndex = 0.0;
            Progress = 0.0;
            CurrentValue = 0.0;
        }

        // 在每次循环迭代中调用此方法来更新double输出
        public void OnEachIteration()
        {
            UpdateDoubleOutputs();
        }

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["Start"] = Start?.Text ?? "";
            obj["End"] = End?.Text ?? "";
            obj["LoopMode"] = (int)LoopMode;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Start"] != null && Start != null) Start.Text = obj["Start"].ToString();
                if (obj["End"] != null && End != null) End.Text = obj["End"].ToString();
                if (obj["LoopMode"] != null) LoopMode = (eLoopMode)obj["LoopMode"].Value<int>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"WhileModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
}