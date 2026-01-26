using ICSharpCode.CodeCompletion;
using Plugin.CSharpScript.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Media;
using VM.Common.Engine;
using VM.Script.Support;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Models;

namespace Plugin.CSharpScript.ViewModels
{
    [Category("逻辑工具")]
    [DisplayName("C#脚本")]
    [ModuleImageName("CSharpScript")]
    [Serializable]
    public class CSharpScriptViewModel : ModuleBase
    {
        public CSharpScriptViewModel()
        {
            
        }
        public override bool ExeModule()
        {
            try
            {
                Stopwatch.Restart();
                if (IsCompiled == false)
                {
                    Compile();
                }
                m_ScriptSupport.CodeRun();
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.AddLog($"{ModuleParam.ModuleName}未编译或编译出错，请检查!", HV.Common.Enums.eMsgType.Error, isDispGrowl: true);
                return false;
            }
        }
        public override void AddOutputParams()
        {
            base.AddOutputParams();
        }

        #region Prop
        [NonSerialized]
        private bool IsCompiled = false;
        [field: NonSerialized]
        private ObservableCollection<ModuleList> _Modules;
        public VarModel Var { get; set; } = new VarModel();
        public int ModuleIndex { get; set; }
        [NonSerialized]
        public ScriptSupport _m_ScriptSupport;
        public ScriptSupport m_ScriptSupport
        {
            get
            {
                if (_m_ScriptSupport == null)
                {
                    _m_ScriptSupport = new ScriptSupport();
                    _m_ScriptSupport.Source = CsharpText;
                    _m_ScriptSupport.Compile(ModuleParam);
                }
                return _m_ScriptSupport;
            }
            set { _m_ScriptSupport = value; }
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
        //public string CsharpText = ScriptTemplate.s_RawScript;
        public string CsharpText =
            "using HV.Dialogs.Views;" +
            "\r\nusing HV.Common.Enums;" +
            "\r\nusing HV.Common.Provide;" +
            "\r\nusing HV.Common;" +
            "\r\nusing HV.Core;" +
            "\r\nusing HV.ViewModels;" +
            "\r\nusing HV.Models;" +
            "\r\nusing System;" +
            "\r\nusing System.Windows;" +
            "\r\nusing System.Collections.Generic;" +
            "\r\nusing System.Linq;" +
            "\r\nusing System.Text;" +
            "\r\nusing System.Threading.Tasks;" +
            "\r\nusing Sample.CSS;" +
            "\r\nusing HalconDotNet;" +
            "\r\nusing System.Xml;" +
            "\r\n\r\n    public class MyScript: ModuleBase" +
            "\r\n    {" +
            "\r\n        public override bool ExeModule()" +
            "\r\n        {" +
            "\r\n         //脚本代码请写在下方" +
            "\r\n                                "+
            "\r\n            return true;" +
            "\r\n        }" +
            "\r\n        public void Init(ModuleParam moduleParam)" +
            "\r\n        {" +
            "\r\n            ModuleParam = moduleParam;" +
            "\r\n        }" +
            "\r\n}" +
            "\r\n" +
            "\r\n";
        #endregion

        #region Command
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
                        if (Compile())
                        {
                            ExeModule();
                        }
                    });
                }
                return _ExecuteCommand;
            }
        }
        [NonSerialized]
        private CommandBase _CompileCommand;
        public CommandBase CompileCommand
        {
            get
            {
                if (_CompileCommand == null)
                {
                    _CompileCommand = new CommandBase((obj) =>
                    {
                        Compile();
                    });
                }
                return _CompileCommand;
            }
        }
        [NonSerialized]
        private CommandBase _FormatCommand;
        /// <summary>
        /// 格式化代码 待完善
        /// </summary>
        public CommandBase FormatCommand
        {
            get
            {
                if (_FormatCommand == null)
                {
                    _FormatCommand = new CommandBase((obj) =>
                    {
                        var view = this.ModuleView as CSharpScriptView;
                        if (view != null)
                        {
                        }

                    });
                }
                return _FormatCommand;
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
                        if (Compile())
                        {
                            var view = this.ModuleView as CSharpScriptView;
                            if (view != null)
                            {
                                CsharpText = view.editor.Text;
                                view.Close();
                            }
                        }
                    });
                }
                return _ConfirmCommand;
            }
        }

        #endregion
        #region Method
        private bool Compile()
        {
            var view = this.ModuleView as CSharpScriptView;
            if (view != null)
            {
                m_ScriptSupport.Source = view.editor.Text;
            }
            if (m_ScriptSupport.Compile(ModuleParam))
            {
                IsCompiled = true;
                if (view != null)
                {
                    view.tip.Text = "编译成功";
                    view.tip.Foreground = Brushes.Lime;
                }
                return true;
            }
            else
            {
                IsCompiled = false;
                if (view != null)
                {
                    view.tip.Text = m_ScriptSupport.ErrorText;
                    view.tip.Foreground = Brushes.Red;
                }
            }
            return false;

        }
        #endregion

    }
}
