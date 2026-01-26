using EventMgrLib;
using Plugin.DataCheck.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HV.Common;
using HV.Common.Helper;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views;


namespace Plugin.DataCheck.Model
{
    [Serializable]
    public class DataCheckModel: NotifyPropertyBase
    {
        private int _ID;
        /// <summary>
        /// 索引
        /// </summary>
        public int ID
        {
            get { return _ID; }
            set { Set(ref _ID, value); }
        }
        private bool _IsCheck=true;
        /// <summary>
        /// 是否检查
        /// </summary>
        public bool IsCheck
        {
            get { return _IsCheck; }
            set { Set(ref _IsCheck, value); }
        }
        private string _DataLinkText;
        /// <summary>
        /// 数据链接
        /// </summary>
        public string DataLinkText
        {
            get { return _DataLinkText; }
            set { Set(ref _DataLinkText, value); }
        }
        private string _DataLinkValue;
        /// <summary>
        /// 数据链接值
        /// </summary>
        public string DataLinkValue
        {
            get { return _DataLinkValue; }
            set { Set(ref _DataLinkValue, value); }
        }
        private bool _State;
        /// <summary>
        /// 状态
        /// </summary>
        public bool State
        {
            get { return _State; }
            set { Set(ref _State, value); }
        }
        private string _lowerLimit="-999999";
        private string _lowerLimitStr = "";
        /// <summary>
        /// 下限位
        /// </summary>
        public string lowerLimit
        {
            get { return _lowerLimit; }
            set { Set(ref _lowerLimit, value); }
        }
        public string lowerLimitStr
        {
            get { return _lowerLimitStr; }
            set { Set(ref _lowerLimitStr, value); }
        }
        private string _upperLimit="99999";
        private string _upperLimitStr = "";
        /// <summary>
        /// 上限位
        /// </summary>
        public string upperLimit
        {
            get { return _upperLimit; }
            set { Set(ref _upperLimit, value); }
        }
        public string upperLimitStr
        {
            get { return _upperLimitStr; }
            set { Set(ref _upperLimitStr, value); }
        }
        private string _lowerDeviation="0";
        /// <summary>
        /// 下公差
        /// </summary>
        public string lowerDeviation
        {
            get { return _lowerDeviation; }
            set { Set(ref _lowerDeviation, value); }
        }
        private string _upperDeviation="0";
        /// <summary>
        /// 上公差
        /// </summary>
        public string upperDeviation
        {
            get { return _upperDeviation; }
            set { Set(ref _upperDeviation, value); }
        }
        private string _DataType;
        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType
        {
            get { return _DataType; }
            set { Set(ref _DataType, value); }
        }

        [NonSerialized]
        private CommandBase _LinkCommand;
        public Guid ModuleGuid = Guid.NewGuid();
        public ModuleParam ModuleParam;
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
                            case eLinkCommand.LowerLimit:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LowerLimitLink,"+ID);
                                break;
                            case eLinkCommand.UpperLimit:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},UpperLimitLink," + ID);
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _LinkCommand;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            if (obj.SendName.Split(',').Length < 3)
                return;
            string var = obj.SendName.Split(',')[2];
            int.TryParse(var, out int count);
            if (count != ID)
                return;
            switch (obj.SendName.Split(',')[1])
            {
                case "LowerLimitLink":
                    lowerLimitStr = obj.LinkName;
                    break;
                case "UpperLimitLink":
                    upperLimitStr = obj.LinkName;
                    break;
                default:
                    break;
            }
        }
        private string _LinkDataName;

        public string LinkDataName
        {
            get { return _LinkDataName; }
            set { Set(ref _LinkDataName, value); }
        }


    }
}
