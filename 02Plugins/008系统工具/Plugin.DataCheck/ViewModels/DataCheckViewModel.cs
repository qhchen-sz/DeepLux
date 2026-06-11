using ControlzEx.Standard;
using EventMgrLib;
using HalconDotNet;
using Newtonsoft.Json.Linq;
using Plugin.DataCheck.Model;
using Plugin.DataCheck.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Serialization;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Extension;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;

namespace Plugin.DataCheck.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        Data,
        LowerLimit,
        UpperLimit,
    }
    #endregion

    [Category("系统工具")]
    [DisplayName("数据检查")]
    [ModuleImageName("DataCheck")]
    [Serializable]
    public class DataCheckViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {

        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                Result = true;
                if (DataChecks.Count == 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                CheckResult = new List<bool>();
                CheckResultNote = "";
                foreach (var Data in DataChecks)
                {
                    if (Data.IsCheck)
                    {
                        var value = GetLinkValue(Data.DataLinkText);
                        double valueD = 0;
                        if (value != null)
                        {
                            switch (Data.DataType)
                            {
                                case "int":
                                case "double":
                                case "bool":
                                    double.TryParse(value.ToString(), out valueD);
                                    break;
                                default:
                                    double.TryParse(value.ToString(), out valueD);
                                    break;
                            }
                        }
                        // 截取小数点
                        if (IsTruncateDecimal && DecimalPlaces >= 0)
                        {
                            double factor = Math.Pow(10, DecimalPlaces);
                            valueD = Math.Truncate(valueD * factor) / factor;
                        }

                        Data.DataLinkValue = valueD.ToString();
                        string lowerStr = GetLinkValue(Data.lowerLimitStr)?.ToString() ?? Data.lowerLimit;
                        string upperStr = GetLinkValue(Data.upperLimitStr)?.ToString() ?? Data.upperLimit;
                        double.TryParse(lowerStr, out double LimtLower);
                        double.TryParse(upperStr, out double LimtUpper);

                        bool itemResult;
                        if (IsReverseCheck)
                        {
                            itemResult = valueD < LimtLower || valueD > LimtUpper;
                        }
                        else
                        {
                            itemResult = valueD >= LimtLower && valueD <= LimtUpper;
                        }

                        if (itemResult)
                        {
                            Data.State = true;
                            CheckResult.Add(true);
                        }
                        else
                        {
                            Data.State = false;
                            CheckResult.Add(false);
                            Result = false;
                        }
                        CheckResultNote += Data.DataLinkText;
                    }
                    else
                    {
                        Data.State = true;
                        CheckResult.Add(true);
                    }
                }
                if (Result)
                {
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
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
            AddOutputParam("总结果", "bool[]", CheckResult, CheckResultNote);
        }
        #region Prop
        /// <summary>
        /// 数据信息
        /// </summary>
        public ObservableCollection<DataCheckModel> DataChecks { get; set; } = new ObservableCollection<DataCheckModel>();

        private DataCheckModel _SelectedData = new DataCheckModel();
        /// <summary>
        /// 选中的文本
        /// </summary>
        public DataCheckModel SelectedData
        {
            get { return _SelectedData; }
            set { Set(ref _SelectedData, value); }
        }

        private string _LinkDataName;

        public List<bool> CheckResult = new List<bool>();
        public string CheckResultNote = "";
        public string LinkDataName
        {
            get { return _LinkDataName; }
            set { Set(ref _LinkDataName, value); }
        }

        private string _LinkDataValue;

        public string LinkDataValue
        {
            get { return _LinkDataName; }
            set { Set(ref _LinkDataName, value); }
        }

        private bool _Result = false;

        public bool Result
        {
            get { return _Result; }
            set { Set(ref _Result, value); }
        }

        private bool _IsReverseCheck;
        /// <summary>
        /// 小于或大于（反向判定）
        /// </summary>
        public bool IsReverseCheck
        {
            get { return _IsReverseCheck; }
            set { Set(ref _IsReverseCheck, value); }
        }

        private bool _IsTruncateDecimal;
        /// <summary>
        /// 截取小数点
        /// </summary>
        public bool IsTruncateDecimal
        {
            get { return _IsTruncateDecimal; }
            set { Set(ref _IsTruncateDecimal, value); }
        }

        private int _DecimalPlaces = 3;
        /// <summary>
        /// 小数位数
        /// </summary>
        public int DecimalPlaces
        {
            get { return _DecimalPlaces; }
            set { Set(ref _DecimalPlaces, value); }
        }

        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            foreach (var item in DataChecks)
            {
                item.ModuleGuid = this.ModuleGuid;
                item.ModuleParam = this.ModuleParam;
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
                        var view = this.ModuleView as DataCheckView;
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
                case "LinkData":
                    LinkDataName = obj.LinkName;
                    var newItem = new DataCheckModel()
                    {
                        DataLinkText = LinkDataName,
                        ID = DataChecks.Count + 1,
                        DataType = obj.DataType,
                        ModuleGuid = this.ModuleGuid,
                        ModuleParam = this.ModuleParam
                    };
                    if (obj.DataType == "bool")
                    {
                        newItem.upperLimit = "1.1";
                        newItem.lowerLimit = "0.6";
                        newItem.upperLimitStr = "1.1";
                        newItem.lowerLimitStr = "0.6";
                    }
                    else
                    {
                        newItem.upperLimit = "9999";
                        newItem.lowerLimit = "-9999";
                        newItem.upperLimitStr = "9999";
                        newItem.lowerLimitStr = "-9999";
                    }
                    // 添加时立即获取当前值显示
                    var val = GetLinkValue(LinkDataName);
                    if (val != null)
                    {
                        double.TryParse(val.ToString(), out double d);
                        newItem.DataLinkValue = d.ToString();
                    }
                    DataChecks.Add(newItem);
                    break;
                default:
                    break;
            }
        }
        private void OnVarChanged1(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "DataChanged":
                    SelectedData.DataLinkText = obj.LinkName;
                    SelectedData.DataType = obj.DataType;
                    break;
                default:
                    break;
            }
        }

        [NonSerialized]
        private CommandBase _LinkCommandButton;
        public CommandBase LinkCommandButton
        {
            get
            {
                if (_LinkCommandButton == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommandButton = new CommandBase((obj) =>
                    {
                        CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules,"int,int[],double,double[],bool","double");
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},LinkData");
                    });
                }
                return _LinkCommandButton;
            }
        }
        [NonSerialized]
        private CommandBase _DataChangedCommand;
        public CommandBase DataChangedCommand
        {
            get
            {
                if (_DataChangedCommand == null)
                {
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged1, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _DataChangedCommand = new CommandBase((obj) =>
                    {
                        CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules);
                        EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},DataChanged");
                    });
                }
                return _DataChangedCommand;
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
                        switch (obj)
                        {
                            case "Delete":
                                if (SelectedData == null) return;
                                DataChecks.Remove(SelectedData);
                                break;
                            case "DownMove":
                                if ((Convert.ToInt32(SelectedData.ID)) < DataChecks.Count)
                                    DataChecks.Move(Convert.ToInt32(SelectedData.ID) - 1, Convert.ToInt32(SelectedData.ID));
                                break;
                            case "UpMove":
                                if ((Convert.ToInt32(SelectedData.ID) - 1) > 0)
                                    DataChecks.Move(Convert.ToInt32(SelectedData.ID) - 1, Convert.ToInt32(SelectedData.ID) - 2);
                                break;
                            default:
                                break;
                        }
                        IDChanged();
                    });
                }
                return _DataOperateCommand;
            }
        }

        #endregion
        #region Method
        private void IDChanged()
        {
            int ID = 1;
            foreach (var Data in DataChecks)
            {
                Data.ID = ID;
                ID += 1;
            }
        }
        #endregion

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["LinkDataName"] = LinkDataName ?? "";
            obj["Result"] = Result;
            obj["IsReverseCheck"] = IsReverseCheck;
            obj["IsTruncateDecimal"] = IsTruncateDecimal;
            obj["DecimalPlaces"] = DecimalPlaces;
            JArray arr = new JArray();
            if (DataChecks != null)
            {
                foreach (var item in DataChecks)
                {
                    JObject itemObj = new JObject();
                    itemObj["ID"] = item.ID;
                    itemObj["IsCheck"] = item.IsCheck;
                    itemObj["DataLinkText"] = item.DataLinkText ?? "";
                    itemObj["lowerLimit"] = item.lowerLimit ?? "";
                    itemObj["lowerLimitStr"] = item.lowerLimitStr ?? "";
                    itemObj["upperLimit"] = item.upperLimit ?? "";
                    itemObj["upperLimitStr"] = item.upperLimitStr ?? "";
                    itemObj["lowerDeviation"] = item.lowerDeviation ?? "";
                    itemObj["upperDeviation"] = item.upperDeviation ?? "";
                    itemObj["DataType"] = item.DataType ?? "";
                    arr.Add(itemObj);
                }
            }
            obj["DataChecks"] = arr;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["LinkDataName"] != null) LinkDataName = obj["LinkDataName"].ToString();
                if (obj["Result"] != null) Result = obj["Result"].Value<bool>();
                if (obj["IsReverseCheck"] != null) IsReverseCheck = obj["IsReverseCheck"].Value<bool>();
                if (obj["IsTruncateDecimal"] != null) IsTruncateDecimal = obj["IsTruncateDecimal"].Value<bool>();
                if (obj["DecimalPlaces"] != null) DecimalPlaces = obj["DecimalPlaces"].Value<int>();
                if (obj["DataChecks"] != null)
                {
                    JArray arr = (JArray)obj["DataChecks"];
                    DataChecks.Clear();
                    foreach (var item in arr)
                    {
                        DataChecks.Add(new DataCheckModel()
                        {
                            ID = item["ID"]?.Value<int>() ?? 0,
                            IsCheck = item["IsCheck"]?.Value<bool>() ?? true,
                            DataLinkText = item["DataLinkText"]?.ToString() ?? "",
                            lowerLimit = item["lowerLimit"]?.ToString() ?? "",
                            lowerLimitStr = item["lowerLimitStr"]?.ToString() ?? "",
                            upperLimit = item["upperLimit"]?.ToString() ?? "",
                            upperLimitStr = item["upperLimitStr"]?.ToString() ?? "",
                            lowerDeviation = item["lowerDeviation"]?.ToString() ?? "",
                            upperDeviation = item["upperDeviation"]?.ToString() ?? "",
                            DataType = item["DataType"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"DataCheckViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
}
