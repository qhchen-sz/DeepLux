using EventMgrLib;
using HalconDotNet;
using Plugin.PLCRead.Models;
using Plugin.PLCRead.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
using HV.Communacation;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views;
using HV.Views.Dock;
using System.Text.RegularExpressions;

namespace Plugin.PLCRead.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Line1,
        Line2,
    }
    #endregion

    [Category("文件通讯")]
    [DisplayName("PLC读取")]
    [ModuleImageName("PLCRead")]
    [Serializable]
    public class PLCReadViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
        }
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                string plcKey = "";
                //foreach (var item in EComManageer.s_ECommunacationDic)
                //{
                //    if (item.Value.m_connectKey == CurKey)
                //    {
                //        plcKey = item.Key;
                //    }
                //}
                foreach (var item in PlcReadVar)
                {
                    if (!EComManageer.readRegister(CurKey, item.DataType, item.Name, out ReadValue))
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                    else
                    {

                        Logger.AddLog(ReadValue.ToString());
                        item.Value = ReadValue;
                    }
                        

                }
                ChangeModuleRunStatus(eRunStatus.OK);
                //if (plcKey != "")
                //{
                //    //EComManageer.s_ECommunacationDic[plcKey].m_IntNumber = HV.Common.Enums.PLCIntDataLengthEnum._16位;
                //    //EComManageer.s_ECommunacationDic[plcKey].m_DoubleNumber = HV.Common.Enums.PLCDoubleDataLengthEnum._32位;
                //    if (EComManageer.readRegister(plcKey, DataType, StartAddress, out ReadValue))
                //    {
                //        Logger.AddLog(ReadValue.ToString());
                //        ChangeModuleRunStatus(eRunStatus.OK);
                //    }
                //    else
                //    {
                //        ChangeModuleRunStatus(eRunStatus.NG);
                //    }
                //}
                //else
                //{
                //    ChangeModuleRunStatus(eRunStatus.NG);
                //}
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


            //switch (DataType)
            //{
            //    case PLCDataWriteReadTypeEnum.布尔:
            //        break;
            //    case PLCDataWriteReadTypeEnum.整型:
            //        if (ReadValue == "") ReadValue = "0";
            //        AddOutputParam("读取int值", "int", int.Parse(ReadValue));
            //        break;
            //    case PLCDataWriteReadTypeEnum.浮点:
            //        if (ReadValue == "") ReadValue = "0.0000";
            //        AddOutputParam("读取double值", "double", Convert.ToDouble(ReadValue).ToString("0.0000"));
            //        break;
            //    case PLCDataWriteReadTypeEnum.字符串:
            //        break;
            //}
            int index = 0;
            ClearOutputParam();
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
            string ResultAll = "";
            foreach (var item in PlcReadVar)
            {
                index++;
                //string name = item.Name.Replace("[i]", ".i");
                string pattern = @"\[(\d+)\]"; // 匹配方括号内的数字
                string replacement = "_$1"; // 将匹配到的数字前加点（.）
                
                string modifiedString = Regex.Replace(item.Name, pattern, replacement);

                string pattern2 = @"\."; // 匹配点字符
                string replacement2 = "-"; // 替换为短横线
                 modifiedString = Regex.Replace(modifiedString, pattern2, replacement2);

                switch (item.DataType)
                {
                    case PLCDataWriteReadTypeEnum.布尔:
                        if (item.Value != null)
                        {
                            AddOutputParam(modifiedString, "bool", bool.Parse(item.Value));
                            ResultAll+= item.Value.ToString()+"@";
                        }
                        else
                        {
                            AddOutputParam(modifiedString, "bool", false);
                            ResultAll += false.ToString() + "@";
                        }
                            
                        break;
                    case PLCDataWriteReadTypeEnum.整型:
                        if (item.Value != null)
                        {
                            AddOutputParam(modifiedString, "int", int.Parse(item.Value));
                            ResultAll += item.Value.ToString() + "@";
                        }
                            
                        else
                        {
                            AddOutputParam(modifiedString, "int", 0);
                            ResultAll += 0.ToString() + "@";
                        }
                            
                        break;
                    case PLCDataWriteReadTypeEnum.浮点:
                        if (item.Value != null)
                        {
                            AddOutputParam(modifiedString, "double", Convert.ToDouble(ReadValue));
                            ResultAll += ReadValue.ToString() + "@";
                        }

                        else
                        {
                            AddOutputParam(modifiedString, "double", 0.00);
                            ResultAll += 0.00.ToString() + "@";
                        }
                            
                        break;
                    case PLCDataWriteReadTypeEnum.字符串:
                        if (item.Value != null)
                        {
                            AddOutputParam(modifiedString, "string", item.Value.ToString());
                            ResultAll += item.Value.ToString() + "@";
                        }
                            
                        else
                        {
                            AddOutputParam(modifiedString, "string", "");
                            ResultAll += "@";
                        }
                            
                        break;
                }
                AddOutputParam("总读取", "string", ResultAll);
            }

        }
        #region Prop
        private ObservableCollection<ReadVarModel> _PlcReadVar = new ObservableCollection<ReadVarModel>();
        /// <summary>
        /// 变量
        /// </summary>
        public ObservableCollection<ReadVarModel> PlcReadVar
        {
            get { return _PlcReadVar; }
            set { _PlcReadVar = value; RaisePropertyChanged(); }
        }

        private ReadVarModel _SelectPlcReadVar;
        public ReadVarModel SelectPlcReadVar
        {
            get { return _SelectPlcReadVar; }
            set { _SelectPlcReadVar = value; RaisePropertyChanged(); }
        }
        /// <summary>读取出来的值</summary>
        string ReadValue = "";
        
        /// <summary>
        /// 寄存器起始地址
        /// </summary>
        private int _StartAddress = 1;
        public int StartAddress
        {
            get { return _StartAddress; }
            set { Set(ref _StartAddress, value); }
        }
        /// <summary>
        /// 当前Key
        /// </summary>
        private string _CurKey = "";
        public string CurKey
        {
            get { return _CurKey; }
            set { _CurKey = value; RaisePropertyChanged(); }
        }
        private PLCDataWriteReadTypeEnum _DataType = PLCDataWriteReadTypeEnum.浮点;
        /// <summary>
        /// 指定图像
        /// </summary>
        public PLCDataWriteReadTypeEnum DataType
        {
            get
            {
                return _DataType;
            }
            set
            {
                Set(ref _DataType, value);
            }
        }
        /// <summary>
        /// Com口数据,用来显示KEY列表数据源
        /// </summary>
        private List<string> _ComKeys;
        public List<string> ComKeys
        {
            get
            {
                if (_ComKeys == null) _ComKeys = new List<string>();
                return _ComKeys;
            }
            set { _ComKeys = value; RaisePropertyChanged(); }
        }
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            ComKeys = EComManageer.GetPLCConnectKeys();
            upDataRemaks();
            if (CurKey==""&& ComKeys != null && ComKeys.Count > 0)
            {
                CurKey = ComKeys[0];
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
                        var view = this.ModuleView as PLCReadView;
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
        private CommandBase _AddVarCommand;
        public CommandBase AddVarCommand
        {
            get
            {
                if (_AddVarCommand == null)
                {
                    _AddVarCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "int":
                                PlcReadVar.Add(new ReadVarModel() { DataType = PLCDataWriteReadTypeEnum.整型, Name = "Value",Note="" });
                                break;
                            case "double":
                                PlcReadVar.Add(new ReadVarModel() { DataType = PLCDataWriteReadTypeEnum.浮点, Name = "Value", Note = "" });
                                break;
                            case "string":
                                PlcReadVar.Add(new ReadVarModel() { DataType = PLCDataWriteReadTypeEnum.字符串, Name = "Value", Note = "" });
                                break;
                            case "bool":
                                PlcReadVar.Add(new ReadVarModel() { DataType = PLCDataWriteReadTypeEnum.布尔, Name = "Value", Note = "" });
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _AddVarCommand;
            }
        }
        [NonSerialized]
        private CommandBase _DeleteCommand;
        public CommandBase DeleteCommand
        {
            get
            {
                if (_DeleteCommand == null)
                {
                    _DeleteCommand = new CommandBase((obj) =>
                    {
                        PlcReadVar.Remove(SelectPlcReadVar);
                        //var view = ModuleView as PLCReadView;
                        //if (view.dg.SelectedIndex == -1) return;
                        //Vars.RemoveAt(view.dg.SelectedIndex);
                        //UpdateData();
                    });
                }
                return _DeleteCommand;
            }
        }
        [NonSerialized]
        private CommandBase _MoveCommand;
        public CommandBase MoveCommand
        {
            get
            {
                if (_MoveCommand == null)
                {
                    _MoveCommand = new CommandBase((obj) =>
                    {
                        var view = ModuleView as PLCReadView;
                        int index = PlcReadVar.IndexOf(SelectPlcReadVar);

                        switch (obj)
                        {
                            case "Up":

                                if (index == 0 || index == -1)
                                    return;
                                var temp1 = PlcReadVar[index-1];
                                PlcReadVar[index - 1] = SelectPlcReadVar;
                                PlcReadVar[index] = temp1;
                                SelectPlcReadVar = PlcReadVar[index - 1];
                                break;
                            case "Down":
                                 index = PlcReadVar.IndexOf(SelectPlcReadVar);
                                if (index == PlcReadVar.Count-1 || index == -1)
                                    return;
                                var temp2 = PlcReadVar[index + 1];
                                PlcReadVar[index + 1] = SelectPlcReadVar;
                                PlcReadVar[index] = temp2;
                                SelectPlcReadVar = PlcReadVar[index + 1];
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _MoveCommand;
            }
        }
        #endregion
        #region Method
        private void UpdateData()
        {

        }
        public void upDataRemaks()
        {
            if (CurKey == ""|| CurKey == null) return;
            string[] arr = CurKey.Split('.');
            foreach (var item in Solution.Ins.ProjectList)
            {
                //if (item.ProjectInfo.ProjectID == int.Parse(arr[0]))
                //{
                //    arr[0] = item.ProjectInfo.ProcessName;
                //}
            }
            //Remarks = arr[0] + "." + arr[1];
        }
        #endregion
    }
}
