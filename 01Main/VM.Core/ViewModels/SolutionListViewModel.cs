using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using
    HV.Common.Helper;
using HV.PersistentData;
using HV.Views;
using WPFLocalizeExtension.Engine;
using System.Collections.ObjectModel;
using System.IO;

namespace HV.ViewModels
{
    public class SolutionListViewModel : NotifyPropertyBase
    {
        #region Singleton

        private static readonly SolutionListViewModel _instance = new SolutionListViewModel();

        private SolutionListViewModel()
        {

        }
        public static SolutionListViewModel Ins
        {
            get { return _instance; }
        }

        #endregion

        #region Prop

        [NonSerialized]
        private ObservableCollection<ProjectModel> _ProjectModels;
        [NonSerialized]
        private ProjectModel _SelectedProject;
        public ProjectModel SelectedProject
        {
            get { return _SelectedProject; }
            set { _SelectedProject = value; }
        }
        public ObservableCollection<ProjectModel> ProjectModels
        {
            get
            {
                if (_ProjectModels == null)
                {
                    _ProjectModels = new ObservableCollection<ProjectModel>();
                }
                return _ProjectModels;
            }
            set { _ProjectModels = value; }
        }

        private string _AutoLoadPath;
        public string AutoLoadPath
        {
            get { return _AutoLoadPath; }
            set { _AutoLoadPath = value; RaisePropertyChanged(); }
        }
        #endregion

        #region Command

        private CommandBase _activatedCommand;
        public CommandBase ActivatedCommand
        {
            get
            {
                if (_activatedCommand == null)
                {
                    _activatedCommand = new CommandBase((obj) =>
                    {
                        if (SolutionListView.Ins.IsClosed)
                        {
                            SolutionListView.Ins.IsClosed = false;
                        }

                    });
                }
                return _activatedCommand;
            }
        }
        private CommandBase confirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (confirmCommand == null)
                {
                    confirmCommand = new CommandBase((obj) =>
                    {
                        SolutionListView.Ins.Close();
                    });
                }
                return confirmCommand;
            }
        }

        private CommandBase _SolutionCommand;
        public CommandBase SolutionCommand
        {
            get
            {
                if (_SolutionCommand == null)
                {
                    _SolutionCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "Open":

                                break;
                            case "DefaultOpen":
                                if(SelectedProject != null)
                                {
                                    HV.PersistentData.SystemConfig.Ins.SolutionPathText = SelectedProject.Path;
                                    HV.PersistentData.SystemConfig.Ins.ProjectModels = ProjectModels;
                                    SystemConfig.Ins.SaveSystemConfig();
                                    AutoLoadPath = SelectedProject.Name;
                                }
                                
                                break;
                            case "AddCurrentThis":

                                break;
                            case "AddCurrent":
                                OpenFileDialog openFileDialog = new OpenFileDialog();
                                openFileDialog.Filter = "解决方案 (*.vm)|*.vm";
                                openFileDialog.DefaultExt = "vm";
                                if (openFileDialog.ShowDialog() == true)
                                {
                                    
                                    ProjectModels.Add(new ProjectModel()
                                    {
                                        ID = ProjectModels.Count + 1,
                                        Name = Path.GetFileNameWithoutExtension(openFileDialog.FileName),
                                        Path = openFileDialog.FileName,
                                    });
                                }
                                break;
                            case "Del":
                                if (SelectedProject != null)
                                    ProjectModels.Remove(SelectedProject);
                                break;
                            case "MoveUp":
                                if(SelectedProject.ID > 1)
                                {
                                    int index = ProjectModels.IndexOf(SelectedProject);
                                    if (index > 0)
                                    {
                                        // 交换当前项和上一项
                                        var temp = ProjectModels[index - 1];
                                        ProjectModels[index - 1] = ProjectModels[index];
                                        ProjectModels[index] = temp;
                                        Upset();
                                    }
                                }
                                break;
                            case "MoveDown":
                                if (SelectedProject.ID < ProjectModels.Count)
                                {
                                    int index = ProjectModels.IndexOf(SelectedProject);
                                    if (index >= 0 && index < ProjectModels.Count - 1)
                                    {
                                        // 交换当前项和下一项
                                        var temp = ProjectModels[index + 1];
                                        ProjectModels[index + 1] = ProjectModels[index];
                                        ProjectModels[index] = temp;
                                        Upset();
                                    }
                                }

                                break;
                            default:
                                break;
                        }

                    });
                }
                return _SolutionCommand;
            }
        }
        #endregion
        private void Upset()
        {
            for (int i = 0; i < ProjectModels.Count; i++)
            {
                ProjectModels[i].ID = i + 1;
            }
        }
    }
    [Serializable]
    public class ProjectModel : NotifyPropertyBase
    {
        /// <summary>
        /// 序号
        /// </summary>
        /// 
        private int _ID;
        public int ID 
        {
            get { return _ID; }
            set { _ID = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 显示的名称
        /// </summary>
        /// 
        private string _Name;
        public string Name 
        {
            get { return _Name; }
            set { _Name = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 备注
        /// </summary>
        /// 
        private string _Remarks;
        public string Remarks
        {
            get { return _Remarks; }
            set { _Remarks = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 路径
        /// </summary>
        /// 
        string _Path;
        public string Path
        {
            get { return _Path; }
            set { _Path = value; RaisePropertyChanged(); }
        }
        /// <summary>
        /// 图标
        /// </summary>

    }
}
