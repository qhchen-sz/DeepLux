using EventMgrLib;
using HalconDotNet;
using Plugin.VarDefine.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VM.Common.Engine;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.Script;
using HV.Services;
using HV.ViewModels;
using HV.Views;

namespace Plugin.VarDefine.ViewModels
{
    [Category("变量工具")]
    [DisplayName("变量定义")]
    [ModuleImageName("VarDefine")]
    [Serializable]
    public class VarDefineViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            base.Stopwatch.Restart();
            try
            {
                if (this.IsAlwaysExe)
                {
                    if (this.Task_Compile != null && this.Task_Compile.Count > 0)
                    {
                        Task.WaitAll(this.Task_Compile.ToArray());
                    }
                    foreach (VarModel item in this.LocalVar)
                    {
                        if (item.Expression == "" || item.Expression == "NULL")
                            continue;

                        if (!item.IsCompileSuccess)
                        {
                            item.m_TempScriptSupport.Source =
                                ExpressionScriptTemplate.GetScriptCode(
                                    base.ModuleParam.ProjectID,
                                    base.ModuleParam.ModuleName,
                                    item.Expression
                                );
                            if (!item.m_TempScriptSupport.Compile())
                            {
                                item.IsCompileSuccess = false;
                                base.ChangeModuleRunStatus(eRunStatus.NG);
                                return false;
                            }
                            object result = item.m_TempScriptSupport.CodeRun();
                            item.Value = ConvertToExpectedType(result, item.DataType);
                            item.IsCompileSuccess = true;
                        }
                        else
                        {
                            object result = item.m_TempScriptSupport.CodeRun();
                            item.Value = ConvertToExpectedType(result, item.DataType);
                        }
                    }
                }
                base.ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                base.ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex, "", true);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            ClearOutputParam();
            base.AddOutputParams();
            foreach (VarModel item in this.LocalVar)
            {
                if (!this.IsAlwaysExe)
                {
                    if (base.Prj.OutputMap.ContainsKey(base.ModuleParam.ModuleName) &&
                        base.Prj.OutputMap[base.ModuleParam.ModuleName].ContainsKey(item.Name))
                    {
                        base.Prj.OutputMap[base.ModuleParam.ModuleName][item.Name].Note = item.Note;
                        break;
                    }
                }
                base.AddOutputParam(item.Name, item.DataType, item.Value, item.Note);
            }
        }

        public override void CompileScript()
        {
            if (this.Task_Compile == null)
                this.Task_Compile = new List<Task>();

            foreach (VarModel item in this.LocalVar)
            {
                if (item.Expression == "" || item.Expression == "NULL")
                    continue;

                VarModel varModel = item;
                this.Task_Compile.Add(Task.Run(() =>
                {
                    varModel.m_TempScriptSupport.Source =
                        ExpressionScriptTemplate.GetScriptCode(
                            this.ModuleParam.ProjectID,
                            this.ModuleParam.ModuleName,
                            varModel.Expression
                        );
                    varModel.IsCompileSuccess = varModel.m_TempScriptSupport.Compile();
                }));
            }
        }

        public int SelectedIndex
        {
            get => this._SelectedIndex;
            set
            {
                this._SelectedIndex = value;
                base.RaisePropertyChanged("SelectedIndex");
            }
        }

        public bool IsAlwaysExe
        {
            get => this._IsAlwaysExe;
            set
            {
                this._IsAlwaysExe = value;
                base.RaisePropertyChanged("IsAlwaysExe");
            }
        }

        public ObservableCollection<VarModel> LocalVar
        {
            get
            {
                if (this._LocalVar == null)
                    this._LocalVar = new ObservableCollection<VarModel>();
                return this._LocalVar;
            }
            set => this._LocalVar = value;
        }

        public ObservableCollection<ModuleList> Modules
        {
            get
            {
                if (this._Modules == null)
                    this._Modules = new ObservableCollection<ModuleList>();
                return this._Modules;
            }
            set => this._Modules = value;
        }

        public override void Loaded()
        {
            base.Loaded();
            this.ClosedView = true;
            foreach (VarModel item in this.LocalVar)
            {
                if (base.Prj.OutputMap.ContainsKey(base.ModuleParam.ModuleName) &&
                    base.Prj.OutputMap[base.ModuleParam.ModuleName].ContainsKey(item.Name) &&
                    base.Prj.OutputMap[base.ModuleParam.ModuleName][item.Name].Value != null)
                {
                    item.Value = base.Prj.OutputMap[base.ModuleParam.ModuleName][item.Name].Value;
                }
            }
        }

        public CommandBase ConfirmCommand
        {
            get
            {
                if (this._ConfirmCommand == null)
                {
                    this._ConfirmCommand = new CommandBase(obj =>
                    {
                        VarDefineView view = base.ModuleView as VarDefineView;
                        if (view == null) return;

                        if (CommonMethods.SameNameJudge(this.LocalVar, out string sameName))
                        {
                            MessageView.Ins.MessageBoxShow(
                                "(" + sameName + ")变量重名，请检查！",
                                eMsgType.Warn,
                                MessageBoxButton.OK,
                                true
                            );
                            return;
                        }

                        foreach (VarModel item in this.LocalVar)
                        {
                            if (item.Expression == "" || item.Expression == "NULL")
                                continue;

                            if (!item.IsCompileSuccess)
                            {
                                item.m_TempScriptSupport.Source =
                                    ExpressionScriptTemplate.GetScriptCode(
                                        base.ModuleParam.ProjectID,
                                        base.ModuleParam.ModuleName,
                                        item.Expression
                                    );
                                if (!item.m_TempScriptSupport.Compile())
                                {
                                    item.IsCompileSuccess = false;
                                    base.ChangeModuleRunStatus(eRunStatus.NG);
                                    MessageView.Ins.MessageBoxShow(
                                        "表达式错误，无法保存！",
                                        eMsgType.Warn,
                                        MessageBoxButton.OK,
                                        true
                                    );
                                    return;
                                }
                                object result = item.m_TempScriptSupport.CodeRun();
                                item.Value = ConvertToExpectedType(result, item.DataType);
                                item.IsCompileSuccess = true;
                            }
                        }
                        base.ChangeModuleRunStatus(eRunStatus.OK);
                        view.Close();
                    });
                }
                return this._ConfirmCommand;
            }
        }

        public CommandBase ExecuteCommand
        {
            get
            {
                if (this._ExecuteCommand == null)
                {
                    this._ExecuteCommand = new CommandBase(obj =>
                    {
                        bool temp = this.IsAlwaysExe;
                        this.IsAlwaysExe = true;
                        this.ExeModule();
                        this.IsAlwaysExe = temp;
                    });
                }
                return this._ExecuteCommand;
            }
        }

        public CommandBase AddVarCommand
        {
            get
            {
                if (this._AddVarCommand == null)
                {
                    this._AddVarCommand = new CommandBase(obj =>
                    {
                        string text = obj as string;
                        if (text == null) return;

                        if (text == "int")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = 0,
                                Note = "整数类型"
                            });
                        }
                        else if (text == "double")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = 0.0,
                                Note = "双精度浮点类型"
                            });
                        }
                        else if (text == "string")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = "",
                                Note = "字符串类型"
                            });
                        }
                        else if (text == "bool")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = false,
                                Note = "True为真，False为假"
                            });
                        }
                        else if (text == "Region")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new HRegion(10.0, 10.0, 5.0),
                                Note = "区域"
                            });
                        }
                        else if (text == "double[]")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new List<double>(),
                                Note = "双精度浮点数组"
                            });
                        }
                        else if (text == "int[]")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new List<int>(),
                                Note = "整数数组"
                            });
                        }
                        else if (text == "string[]")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new List<string>(),
                                Note = "字符串数组"
                            });
                        }
                        else if (text == "bool[]")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new List<bool>(),
                                Note = "布尔数组"
                            });
                        }
                        else if (text == "Image[]")
                        {
                            this.LocalVar.Add(new VarModel
                            {
                                Index = this.LocalVar.Count + 1,
                                Name = CommonMethods.GetNewVarName(text, this.LocalVar),
                                DataType = text,
                                Value = new List<HImage>(),
                                Note = "图像数组"
                            });
                        }
                        this.UpdateIndex();
                    });
                }
                return this._AddVarCommand;
            }
        }

        public CommandBase DeleteCommand
        {
            get
            {
                if (this._DeleteCommand == null)
                {
                    this._DeleteCommand = new CommandBase(obj =>
                    {
                        if (this.SelectedIndex != -1)
                        {
                            this.LocalVar.RemoveAt(this.SelectedIndex);
                            this.UpdateIndex();
                        }
                    });
                }
                return this._DeleteCommand;
            }
        }

        public CommandBase MoveCommand
        {
            get
            {
                if (this._MoveCommand == null)
                {
                    this._MoveCommand = new CommandBase(obj =>
                    {
                        string text = obj as string;
                        if (text == null) return;

                        if (text == "Up")
                        {
                            if (this.SelectedIndex > 0 && this.LocalVar.Count > 1)
                            {
                                this.LocalVar.Move(this.SelectedIndex, this.SelectedIndex - 1);
                                this.UpdateIndex();
                            }
                        }
                        else if (text == "Down")
                        {
                            if (this.SelectedIndex != -1 && this.LocalVar.Count > 1 &&
                                this.SelectedIndex < this.LocalVar.Count - 1)
                            {
                                this.LocalVar.Move(this.SelectedIndex, this.SelectedIndex + 1);
                                this.UpdateIndex();
                            }
                        }
                    });
                }
                return this._MoveCommand;
            }
        }

        private void UpdateIndex()
        {
            if (this.LocalVar.Count == 0) return;
            for (int i = 0; i < this.LocalVar.Count; i++)
            {
                this.LocalVar[i].Index = i;
            }
        }

        /// <summary>
        /// 将表达式结果转换为 VarModel.set_Value 所期望的类型。
        /// - 若目标类型为标量（double/int/string/bool）且值为数组/列表，则取第一个元素。
        /// - 若目标类型为数组（double[]/int[]/string[]/bool[]/Image[]）且值为标量，则包装为单元素列表。
        /// - 其他情况直接返回原值。
        /// </summary>
        private object ConvertToExpectedType(object value, string dataType)
        {
            if (value == null || string.IsNullOrEmpty(dataType))
                return value;

            switch (dataType)
            {
                case "double":
                    if (value is double[] dArray)
                        return dArray.Length > 0 ? dArray[0] : 0.0;
                    if (value is List<double> dList)
                        return dList.Count > 0 ? dList[0] : 0.0;
                    break;

                case "int":
                    if (value is int[] iArray)
                        return iArray.Length > 0 ? iArray[0] : 0;
                    if (value is List<int> iList)
                        return iList.Count > 0 ? iList[0] : 0;
                    break;

                case "string":
                    if (value is string[] sArray)
                        return sArray.Length > 0 ? sArray[0] : "";
                    if (value is List<string> sList)
                        return sList.Count > 0 ? sList[0] : "";
                    break;

                case "bool":
                    if (value is bool[] bArray)
                        return bArray.Length > 0 ? bArray[0] : false;
                    if (value is List<bool> bList)
                        return bList.Count > 0 ? bList[0] : false;
                    break;

                case "double[]":
                    if (value is double d)
                        return new List<double> { d };
                    if (value is double[] dArray2)
                        return new List<double>(dArray2);
                    break;

                case "int[]":
                    if (value is int i)
                        return new List<int> { i };
                    if (value is int[] iArray2)
                        return new List<int>(iArray2);
                    break;

                case "string[]":
                    if (value is string s)
                        return new List<string> { s };
                    if (value is string[] sArray2)
                        return new List<string>(sArray2);
                    break;

                case "bool[]":
                    if (value is bool b)
                        return new List<bool> { b };
                    if (value is bool[] bArray2)
                        return new List<bool>(bArray2);
                    break;

                case "Image[]":
                    if (value is HImage hImage)
                        return new List<HImage> { hImage };
                    if (value is HImage[] hImageArray)
                        return new List<HImage>(hImageArray);
                    break;
            }

            return value;
        }

        [NonSerialized]
        private List<Task> Task_Compile = new List<Task>();

        [NonSerialized]
        private int _SelectedIndex;

        private bool _IsAlwaysExe = true;

        private ObservableCollection<VarModel> _LocalVar;

        [NonSerialized]
        private ObservableCollection<ModuleList> _Modules;

        [NonSerialized]
        public ExpressionView expressionView;

        [NonSerialized]
        private CommandBase _ConfirmCommand;

        [NonSerialized]
        private CommandBase _ExecuteCommand;

        [NonSerialized]
        private CommandBase _AddVarCommand;

        [NonSerialized]
        private CommandBase _DeleteCommand;

        [NonSerialized]
        private CommandBase _MoveCommand;
    }
}