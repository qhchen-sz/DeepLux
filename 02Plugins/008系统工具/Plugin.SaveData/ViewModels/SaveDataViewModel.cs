using EventMgrLib;
using HalconDotNet;
using Plugin.SaveData.Models;
using Plugin.SaveData.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using System.Windows.Data;
using System.Globalization;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Threading;

namespace Plugin.SaveData.ViewModels
{
    public enum eLinkCommand
    {
        SaveFilePath,
        SaveFileName,
        AddContent,
        AddLine,

    }
    public enum LinkMode
    {
        Path,
        Variable,

    }
    [Serializable]
    [Category("系统工具")]
    [DisplayName("CSV存储")]
    [ModuleImageName("SaveData")]

    public class SaveDataViewModel : ModuleBase
    {
        [NonSerialized]
        private ReadOnlyFileWriter _fileWriter;

        [NonSerialized]
        private List<Task> Task_Compile = new List<Task>();

        private ObservableCollection<VarSetModel> _VarSet;

        [NonSerialized]
        private VarSetModel _SelectedVar;

        [NonSerialized]
        private int _SelectedIndex;

        [NonSerialized]
        private ObservableCollection<ModuleList> _Modules;

        [NonSerialized]
        public ExpressionView expressionView;

        [NonSerialized]
        private CommandBase _ConfirmCommand;

        [NonSerialized]
        private CommandBase _ExecuteCommand;

        [NonSerialized]
        private CommandBase _AddCommand;

        [NonSerialized]
        private CommandBase _DeleteCommand;

        [NonSerialized]
        private CommandBase _MoveCommand; 
        [NonSerialized]
        private CommandBase _LineCommand; 

        //保留小数点个数
        private int _DecimalPlaces=3;
        public int DecimalPlaces
        {
            get { return _DecimalPlaces; }
            set { _DecimalPlaces = value; RaisePropertyChanged(); }
        }
        //文件名是否添加时间后缀
        private bool _AutoAddTime=true;
        public bool AutoAddTime
        {
            get { return _AutoAddTime; }
            set { _AutoAddTime = value; RaisePropertyChanged(); }
        }
        //路径是否用时间启用子文件夹
        private bool _UseDateFolder =false;
        public bool UseDateFolder
        {
            get { return _UseDateFolder; }
            set { _UseDateFolder = value; RaisePropertyChanged(); }
        }
        //第一列是否添加时间
        private bool _UseFirstColumnTime=true;
        public bool UseFirstColumnTime
        {
            get { return _UseFirstColumnTime; }
            set { _UseFirstColumnTime = value; RaisePropertyChanged(); }
        }
        private LinkMode _selectedMode;
        public LinkMode SelectedMode
        {
            get { return _selectedMode; }
            set { _selectedMode = value; RaisePropertyChanged(); }
        }
        private string _FilePath ;
        public string FilePath
        {
            get { return _FilePath; }
            set { _FilePath = value; RaisePropertyChanged(); }
        }
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { _InputImageLinkText = value; RaisePropertyChanged(); }
        }
        private string _InputFileLinkText = "数据保存";
        public string InputFileLinkText
        {
            get { return _InputFileLinkText; }
            set { _InputFileLinkText = value; RaisePropertyChanged(); }
        }
        public ObservableCollection<VarSetModel> SaveData
        {
            get
            {
                if (_VarSet == null)
                {
                    _VarSet = new ObservableCollection<VarSetModel>();
                }
                return _VarSet;
            }
            set { _VarSet = value; }
        }

        public VarSetModel SelectedVar
        {
            get { return _SelectedVar; }
            set { _SelectedVar = value; }
        }

        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set
            {
                _SelectedIndex = value;
                RaisePropertyChanged("SelectedIndex");
            }
        }

        public ObservableCollection<ModuleList> Modules
        {
            get
            {
                if (_Modules == null)
                {
                    _Modules = new ObservableCollection<ModuleList>();
                }
                return _Modules;
            }
            set { _Modules = value; }
        }

        public CommandBase ConfirmCommand
        {
            get
            {
                if (_ConfirmCommand == null)
                {
                    _ConfirmCommand = new CommandBase(
                        delegate
                        {
                            if (base.ModuleView is VarSetView varSetView)
                            {
                                //foreach (VarSetModel current in SaveData)
                                //{
                                //    if (
                                //        !current.IsCompileSuccess
                                //    )
                                //    {
                                //        current.m_TempScriptSupport.Source =
                                //            ExpressionScriptTemplate.GetScriptCode(
                                //                base.ModuleParam.ProjectID,
                                //                base.ModuleParam.ModuleName,
                                //                current.Expression
                                //            );
                                //        if (!current.m_TempScriptSupport.Compile())
                                //        {
                                //            current.IsCompileSuccess = false;
                                //            ChangeModuleRunStatus(eRunStatus.NG);
                                //            MessageView.Ins.MessageBoxShow(
                                //                "表达式错误，无法保存！",
                                //                eMsgType.Warn
                                //            );
                                //            return;
                                //        }
                                //        current.IsCompileSuccess = true;
                                //    }
                                //}
                                ChangeModuleRunStatus(eRunStatus.OK);
                                varSetView.Close();
                            }
                        }
                    );
                }
                return _ConfirmCommand;
            }
        }

        public CommandBase ExecuteCommand
        {
            get
            {
                if (_ExecuteCommand == null)
                {
                    _ExecuteCommand = new CommandBase(
                        delegate
                        {
                            ExeModule();
                        }
                    );
                }
                return _ExecuteCommand;
            }
        }

        //public CommandBase AddCommand
        //{
        //    get
        //    {
        //        if (_AddCommand == null)
        //        {
        //            EventMgr.Ins
        //                .GetEvent<VarChangedEvent>()
        //                .Subscribe(
        //                    OnVarChanged,
        //                    (VarChangedEventParamModel o) => o.SendName.StartsWith($"{ModuleGuid}")
        //                );
        //            _AddCommand = new CommandBase(
        //                delegate
        //                {
        //                    CommonMethods.GetModuleList(
        //                        base.ModuleParam,
        //                        VarLinkViewModel.Ins.Modules,
        //                        "bool,int,double,string,bool[],int[],double[],string[],HImage,Image[]"
        //                    );
        //                    EventMgr.Ins
        //                        .GetEvent<OpenVarLinkViewEvent>()
        //                        .Publish($"{ModuleGuid},VarSetLinkText,IsAdd");
        //                }
        //            );
        //        }
        //        return _AddCommand;
        //    }
        //}

        public CommandBase DeleteCommand
        {
            get
            {
                if (_DeleteCommand == null)
                {
                    _DeleteCommand = new CommandBase(
                        delegate
                        {
                            if (SelectedIndex != -1)
                            {
                                SaveData.RemoveAt(SelectedIndex);
                                UpdateIndex();
                                if(SaveData.Count!=0)
                                    SelectedIndex = SaveData.Count - 1;
                            }
                        }
                    );
                }
                return _DeleteCommand;
            }
        }

        public CommandBase MoveCommand
        {
            get
            {
                if (_MoveCommand == null)
                {
                    _MoveCommand = new CommandBase(
                        delegate(object obj)
                        {
                            switch (obj as string)
                            {
                                case "Up":
                                    if (SelectedIndex > 0 && SaveData.Count > 1)
                                    {
                                        SaveData.Move(SelectedIndex, SelectedIndex - 1);
                                        UpdateIndex();
                                    }
                                    break;
                                case "Down":
                                    if (
                                        SelectedIndex != -1
                                        && SaveData.Count > 1
                                        && SelectedIndex != SaveData.Count - 1
                                    )
                                    {
                                        SaveData.Move(SelectedIndex, SelectedIndex + 1);
                                        UpdateIndex();
                                    }
                                    break;
                            }
                        }
                    );
                }
                return _MoveCommand;
            }
        }

        public CommandBase LineCommand
        {
            get
            {
                if (_LineCommand == null)
                {
                    _LineCommand = new CommandBase(
                        delegate (object obj)
                        {
                            SaveData.Add(
                                                    new VarSetModel
                                                    {
                                                        Index = SaveData.Count,
                                                        Name = "",
                                                        Link ="",
                                                        DataType = "换行",
                                                    }
                                                );
                            SelectedIndex = SaveData.Count - 1;
                        }
                    );
                }
                return _LineCommand;
            }
        }
        /// <summary>
        /// 获取文件路径
        /// </summary>
        private string GetFilePath()
        {
            string filePath = "";
            string fileName = GetLinkValue(InputFileLinkText).ToString();
            string time = DateTime.Now.ToString("yyyy_MM_dd");

            // 根据选择的模式获取基础路径
            if (SelectedMode == LinkMode.Path)
                filePath = FilePath;
            else
                filePath = GetLinkValue(InputImageLinkText).ToString();

            // 如果启用自动添加时间后缀
            if (AutoAddTime)
            {
                fileName += time;
            }

            // 如果使用日期文件夹归类
            if (UseDateFolder)
            {
                filePath = Path.Combine(filePath, time);
            }

            // 添加扩展名
            fileName += ".csv";

            // 合并完整路径
            return Path.Combine(filePath, fileName);
        }

        /// <summary>
        /// 获取表头行
        /// </summary>
        private string GetHeaderLine()
        {
            string columnNames = "";

            // 如果启用第一列为时间
            if (UseFirstColumnTime)
            {
                columnNames = "时间";
            }

            // 添加数据列名
            bool isFirstDataColumn = true;
            foreach (var item in SaveData)
            {
                if (item.DataType == "换行")
                {
                    break; // 遇到换行就停止，表头只需要第一行的列名
                }

                if (isFirstDataColumn && !string.IsNullOrEmpty(columnNames))
                {
                    columnNames += "," + item.Name;
                }
                else if (isFirstDataColumn && string.IsNullOrEmpty(columnNames))
                {
                    columnNames = item.Name;
                }
                else
                {
                    columnNames += "," + item.Name;
                }

                isFirstDataColumn = false;
            }

            return columnNames;
        }

        /// <summary>
        /// 获取数据行
        /// </summary>
        private List<string> GetDataLines()
        {
            List<string> dataLines = new List<string>();
            string columnValues = "";

            // 如果启用第一列为时间
            if (UseFirstColumnTime)
            {
                columnValues = DateTime.Now.ToString("yy_MM_dd HH:mm:ss");
            }

            string temp = columnValues;
            foreach (var item in SaveData)
            {
                if (item.DataType == "换行")
                {
                    dataLines.Add(temp);
                    temp = columnValues; // 重置为初始值（包含时间）
                    continue;
                }
                if (!string.IsNullOrEmpty(temp))
                {
                    var linkValue = GetLinkValue(item.Link);
                    if (linkValue is double doubleValue)
                    {
                        string format = "F" + DecimalPlaces.ToString();
                        temp += "," + doubleValue.ToString(format);
                    }
                    else
                    {
                        temp += "," + linkValue;
                    }
                }
                else
                {
                    temp = GetLinkValue(item.Link).ToString();
                }
            }

            // 添加最后一行（如果有数据）
            if (!string.IsNullOrEmpty(temp))
            {
                dataLines.Add(temp);
            }

            return dataLines;
        }
        public override bool ExeModule()
        {
            base.Stopwatch.Restart();
            try
            {
                // 初始化文件写入器（如果还没有）
                if (_fileWriter == null)
                {
                    string filePath = GetFilePath();

                    // 确保目录存在
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    _fileWriter = new ReadOnlyFileWriter(filePath);

                    // 如果是新文件，写入表头
                    if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                    {
                        _fileWriter.WriteLine(GetHeaderLine());
                    }
                }

                // 获取并写入数据
                var dataLines = GetDataLines();
                _fileWriter.WriteLines(dataLines);

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

        // 程序关闭时释放资源
        public void Dispose()
        {
            _fileWriter?.Dispose();
        }
        public override void AddOutputParams()
        {
            base.AddOutputParams();
            //foreach (VarSetModel item in SaveData)
            //{
            //    AddOutputParam(item.Name, item.DataType, item.Value, item.Note);
            //}
        }

        public override void CompileScript()
        {


        }

        public override void Loaded()
        {
            base.Loaded();
            ClosedView = true;
            foreach (VarSetModel item in SaveData)
            {
                VarModel var = base.Prj.GetParamByName(item.Link);



            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            string[] strings = obj.SendName.Split(',');
            string text = strings[1];
            string text2 = text;
            if (text2 == "AddContent")
            {
                if (obj.IsAdd)
                {
                    SaveData.Add(
                        new VarSetModel
                        {
                            Index = SaveData.Count,
                            Name = obj.Name,
                            Link = obj.LinkName,
                            DataType = obj.DataType,
                        }
                    );
                    SelectedIndex = SaveData.Count - 1;
                }
                else if (SelectedVar != null)
                {
                    SelectedVar.Link = obj.LinkName;
                }
                UpdateIndex();
            }
            else if(text2 == "InputImageLinkText")
            {
                InputImageLinkText =obj.LinkName;
            }
            else if (text2 == "InputFileLinkText")
            {
                InputFileLinkText = obj.LinkName;
            }
            
        }

        private void UpdateIndex()
        {
            if (SaveData.Count != 0)
            {
                for (int i = 0; i < SaveData.Count; i++)
                {
                    SaveData[i].Index = i;
                }
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
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.AddContent:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "bool,int,double,string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},AddContent,IsAdd");
                                break;
                            case eLinkCommand.SaveFilePath:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLinkText");
                                break;
                            case eLinkCommand.SaveFileName:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputFileLinkText");
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
        private CommandBase _FilePathCommand;
        public CommandBase FilePathCommand
        {
            get
            {
                if (_FilePathCommand == null)
                {
                    _FilePathCommand = new CommandBase((obj) =>
                    {
                        CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true };

                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            FilePath = dialog.FileName;
                        }

                    });
                }
                return _FilePathCommand;
            }
        }
    }

}
