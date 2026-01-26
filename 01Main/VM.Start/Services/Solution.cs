using EventMgrLib;
using ICSharpCode.NRefactory.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
//using VisionCore.PLC.Communacation;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Extension;
using HV.Common.Helper;
using HV.Common.RightControl;
using HV.Communacation;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views;
using HV.Views.Dock;

namespace HV.Services
{
    [Serializable]
    public class Solution : NotifyPropertyBase
    {
        #region 单例模式
        private static Solution _Instance = null;

        public Solution() { }

        public static Solution Ins
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new Solution();
                }
                return _Instance;
            }
            set { _Instance = value; }
        }
        #endregion

        #region Prop
        public int CurrentProjectID = -1;
        public Project CurrentProject = null;
        public bool QuickMode = false;
        private eViewMode _ViewMode = eViewMode.One;
        private eViewMode _ChartViewMode = eViewMode.One;
        public CameraSetViewModel CameraSetViewModel;
        public HardwareConfigViewModel HardwareConfigViewModel;

        [NonSerialized]
        public UIDesignViewModel UIDesignViewModel;

        public LaserSetViewModel LaserSetViewModel;
        private bool _IsUseUIDesign = false;

        private string _UIDesignText = "";
        public string UIDesignText
        {
            get { return _UIDesignText; }
            set
            {
                _UIDesignText = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 画布模式
        /// </summary>
        public eViewMode ViewMode
        {
            get { return _ViewMode; }
            set
            {
                _ViewMode = value;
                RaisePropertyChanged();
            }
        }
        #region Prop
        public eViewMode ChartViewMode
        {
            get { return _ChartViewMode; }
            set
            {
                _ChartViewMode = value;
                RaisePropertyChanged();
            }
        }
        private Array _ChartTypes = Enum.GetValues(typeof(Types));
        public Array ChartTypes
        {
            get { return _ChartTypes; }
            set { _ChartTypes = value; }
        }
        /// <summary>
        /// 图表1类型
        /// </summary>
        private Types _ChartType1 = Types.柱状图;
        public Types ChartType1
        {
            get { return _ChartType1; }
            set
            {
                _ChartType1 = value;
                if(Solution.Ins.ChartType1!= value)
                    Solution.Ins.ChartType1 = value;
                ChartsSetViewModel.Ins.ChartType1 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表2类型
        /// </summary>
        private Types _ChartType2 = Types.柱状图;
        public Types ChartType2
        {
            get { return _ChartType2; }
            set
            {
                _ChartType2 = value;
                if (Solution.Ins.ChartType2 != value)
                    Solution.Ins.ChartType2 = value;
                ChartsSetViewModel.Ins.ChartType2 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表3类型
        /// </summary>
        private Types _ChartType3 = Types.柱状图;
        public Types ChartType3
        {
            get { return _ChartType3; }
            set
            {
                _ChartType3 = value;
                if (Solution.Ins.ChartType3 != value)
                    Solution.Ins.ChartType3 = value;
                ChartsSetViewModel.Ins.ChartType3 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表4类型
        /// </summary>
        private Types _ChartType4 = Types.柱状图;
        public Types ChartType4
        {
            get { return _ChartType4; }
            set
            {
                _ChartType4 = value;
                if (Solution.Ins.ChartType4 != value)
                    Solution.Ins.ChartType4 = value;
                ChartsSetViewModel.Ins.ChartType4 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表5类型
        /// </summary>
        private Types _ChartType5 = Types.柱状图;
        public Types ChartType5
        {
            get { return _ChartType5; }
            set
            {
                _ChartType5 = value;
                if (Solution.Ins.ChartType5 != value)
                    Solution.Ins.ChartType5 = value;
                ChartsSetViewModel.Ins.ChartType5 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表6类型
        /// </summary>
        private Types _ChartType6 = Types.柱状图;
        public Types ChartType6
        {
            get { return _ChartType6; }
            set
            {
                _ChartType6 = value;
                if (Solution.Ins.ChartType6 != value)
                    Solution.Ins.ChartType6 = value;
                ChartsSetViewModel.Ins.ChartType6 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表7类型
        /// </summary>
        private Types _ChartType7 = Types.柱状图;
        public Types ChartType7
        {
            get { return _ChartType7; }
            set
            {
                _ChartType7 = value;
                if (Solution.Ins.ChartType7 != value)
                    Solution.Ins.ChartType7 = value;
                ChartsSetViewModel.Ins.ChartType7 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表8类型
        /// </summary>
        private Types _ChartType8 = Types.柱状图;
        public Types ChartType8
        {
            get { return _ChartType8; }
            set
            {
                _ChartType8 = value;
                if (Solution.Ins.ChartType8 != value)
                    Solution.Ins.ChartType8 = value;
                ChartsSetViewModel.Ins.ChartType8 = value;
                RaisePropertyChanged();
            }
        }
        /// <summary>
        /// 图表9类型
        /// </summary>
        private Types _ChartType9 = Types.柱状图;
        public Types ChartType9
        {
            get { return _ChartType9; }
            set
            {
                _ChartType9 = value;
                if (Solution.Ins.ChartType9 != value)
                    Solution.Ins.ChartType9 = value;
                ChartsSetViewModel.Ins.ChartType9 = value;
                RaisePropertyChanged();
            }
        }
        #endregion
        public bool IsUseUIDesign
        {
            get { return this._IsUseUIDesign; }
            set
            {
                this._IsUseUIDesign = value;
                base.RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 流程列表
        /// </summary>
        public List<Project> ProjectList = new List<Project>();

        /// <summary>
        /// 系统变量
        /// </summary>
        public ObservableCollection<VarModel> SysVar = new ObservableCollection<VarModel>();
        public List<ECommunacation> eCommunacations = new List<ECommunacation>();
        #endregion

        #region Method
        public void SetIsEnable()
        {
            bool running = GetStates();
            if (running == true)
            {
                CommonMethods.UIAsync(() =>
                {
                    IsEnableControl.Ins.Stop = true;
                    IsEnableControl.Ins.RunCycle = false;
                    IsEnableControl.Ins.RunOnce = false;
                    IsEnableControl.Ins.NewSolution = false;
                    IsEnableControl.Ins.SolutionList = false;
                    IsEnableControl.Ins.Open = false;
                    IsEnableControl.Ins.Save = false;
                    IsEnableControl.Ins.GlobalVar = false;
                    IsEnableControl.Ins.CameraSet = false;
                    IsEnableControl.Ins.CommunicationSet = false;
                    IsEnableControl.Ins.HardwareConfig = false;
                    IsEnableControl.Ins.UIDesign = false;
                    ProcessView.Ins.btnRunOnce.Content = "\ue8f0";
                    ProcessView.Ins.btnRunCycle.Content = "\ue6ef";
                    ProcessView.Ins.btnRunOnce.ToolTip = "继续运行(F5)";
                    ProcessView.Ins.btnRunCycle.ToolTip = "逐过程,一步一步运行(F6)";
                });
            }
            else
            {
                CommonMethods.UIAsync(() =>
                {
                    IsEnableControl.Ins.Stop = false;
                    IsEnableControl.Ins.RunCycle = true;
                    IsEnableControl.Ins.RunOnce = true;
                    IsEnableControl.Ins.NewSolution = true;
                    IsEnableControl.Ins.SolutionList = true;
                    IsEnableControl.Ins.Open = true;
                    IsEnableControl.Ins.Save = true;
                    IsEnableControl.Ins.GlobalVar = true;
                    IsEnableControl.Ins.CameraSet = true;
                    IsEnableControl.Ins.CommunicationSet = true;
                    IsEnableControl.Ins.HardwareConfig = true;
                    IsEnableControl.Ins.UIDesign = true;
                    ProcessView.Ins.btnRunOnce.Content = "\ue67b";
                    ProcessView.Ins.btnRunCycle.Content = "\ue612";
                    ProcessView.Ins.btnRunOnce.ToolTip = "当前项目单次执行";
                    ProcessView.Ins.btnRunCycle.ToolTip = "当前项目连续执行";
                });
            }
        }

        public void LoadCommunacation()
        {
            if (eCommunacations == null)
                return;
            foreach (var item in eCommunacations)
            {
                item.IsConnected = false;
                item.IsHasObjectConnected = false;
            }
            EComManageer.setEcomList(eCommunacations); //将反序列化的LIST转为字典，并连接通信设备
        }

        public void UpdataCommunacation()
        {
            eCommunacations = EComManageer.GetEcomList();
        }

        public void ModuleLoad()
        {
            foreach (var item in ProjectList)
            {
                foreach (var item2 in item.ModuleDic)
                {
                   item2.Value.InitModule();
                }
            }
        }

        /// <summary>
        /// 创建解决方案
        /// </summary>
        public void CreateSolution()
        {
            var messageView = MessageView.Ins;
            messageView.MessageBoxShow(
                "创建新的解决方案会覆盖掉当前已有的解决方案，确认继续？",
                eMsgType.Warn,
                MessageBoxButton.OKCancel
            );
            if (messageView.DialogResult == true)
            {
                ToolView.Ins.processTree.ItemsSource = null;
                ProcessView.Ins.moduleTree.ItemsSource = null;
                Ins = new Solution();
                Ins.CreateProject(eProjectType.Process);
                Ins.CreateProject(eProjectType.Process);
                Ins.CreateProject(eProjectType.Process);
                Ins.ProjectList[0].ProjectInfo.ProcessName = "Home";
                Ins.ProjectList[1].ProjectInfo.ProcessName = "主流程";
                Ins.ProjectList[2].ProjectInfo.ProcessName = "End";
                ToolView.Ins.UpdateTree();
            }
        }

        /// <summary>
        /// 创建流程
        /// </summary>
        public int CreateProject(eProjectType projectType)
        {
            Project project = new Project();
            project.ProjectInfo.ProjectType = projectType;
            //获取新不重复的id  如果已经存在  1,2,4   那么久获得的id 是 3
            bool flag = false;
            int id = 0;
            do
            {
                flag = true;
                switch (projectType)
                {
                    case eProjectType.Process:
                    case eProjectType.Method:
                        foreach (Project prj in ProjectList)
                        {
                            if (
                                prj.ProjectInfo.ProjectID == id
                                && (
                                    prj.ProjectInfo.ProjectType == eProjectType.Method
                                    || prj.ProjectInfo.ProjectType == eProjectType.Process
                                )
                            )
                            {
                                id++;
                                flag = false;
                                break;
                            }
                        }
                        break;
                    case eProjectType.Folder:
                        foreach (Project prj in ProjectList)
                        {
                            if (
                                prj.ProjectInfo.FolderID == id
                                && prj.ProjectInfo.ProjectType == eProjectType.Folder
                            )
                            {
                                id++;
                                flag = false;
                                break;
                            }
                        }
                        break;
                    default:
                        break;
                }
                if (flag == true)
                {
                    break;
                }
            } while (true);

            project.ProjectInfo.ProjectID = id;
            string name = "";
            switch (project.ProjectInfo.ProjectType)
            {
                case eProjectType.Process:
                    name = "流程";
                    break;
                case eProjectType.Method:
                    name = "方法";
                    break;
                case eProjectType.Folder:
                    name = "文件夹";
                    break;
                default:
                    break;
            }
            project.ProjectInfo.ProcessName = name + project.ProjectInfo.ProjectID;
            if (ProjectList.Count >= 3)
            {
                ProjectList.Insert(ProjectList.Count - 1, project);
            }
            else
            {
                ProjectList.Add(project);
            }
            CurrentProjectID = project.ProjectInfo.ProjectID;
            CurrentProject = project;
            return id;
        }

        /// <summary>
        /// 根据id获取对应的project
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Project GetProjectById(int id)
        {
            return ProjectList.FirstOrDefault(
                c =>
                    c.ProjectInfo.ProjectID == id
                    && c.ProjectInfo.ProjectType != eProjectType.Folder
            );
        }

        public void DeleteProjectById(int id)
        {
            if (ProjectList.Count <= id)
                return;
            ProjectList.RemoveAt(id);
        }

        /// <summary>
        /// 获取所有项目运行状态
        /// </summary>
        /// <returns></returns>
        public bool GetStates()
        {
            foreach (Project prj in ProjectList)
            {
                if (prj.GetThreadStatus() == true)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 连续运行
        /// </summary>
        public void StartRun()
        {
            foreach (Project item in ProjectList)
            {
                bool station = false;
                foreach (var item2 in item.ModuleList)
                {
                    if (item2.ModuleParam.PluginName == "采集图像" && item2.ModuleParam.Status != eRunStatus.Disable && !item2.OnlineImage)
                    {
                        CommonMethods.UISync(() =>
                        {
                            MessageView.Ins.MessageBoxShow(item.ProjectInfo.ProcessName + item2.ModuleParam.ModuleName + "是离线图像", eMsgType.Warn);
                        });
                        station = true;
                        break;
                    }
                }
                if (station)
                    break;
            }
            ProjectList.ForEach(item =>
            {
                if (
                    item.ProjectInfo.ProjectRunMode == eProjectAutoRunMode.主动执行
                    && item.ProjectInfo.ProcessName != "Home"
                    && item.ProjectInfo.ProcessName != "End"
                )
                {
                    item.RunMode = eRunMode.RunCycle;
                    item.Start();
                }
            });
        }

        /// <summary>
        /// 执行一次
        /// </summary>
        public void ExecuteOnce()
        {
            ProjectList.ForEach(item =>
            {
                if (
                    item.ProjectInfo.ProjectRunMode == eProjectAutoRunMode.主动执行
                    && item.ProjectInfo.ProcessName != "Home"
                    && item.ProjectInfo.ProcessName != "End"
                )
                {
                    item.RunMode = eRunMode.RunOnce;
                    item.Start();
                }
            });
        }

        /// <summary>
        /// 停止运行
        /// </summary>
        public void StopRun()
        {
            ProjectList.ForEach(item =>
            {
                if (item.ProjectInfo.ProcessName == "End")
                {
                    item.RunMode = eRunMode.RunOnce;
                    item.Start();
                }
                else if (item.ProjectInfo.ProcessName == "Home") { }
                else
                {
                    item.RunMode = eRunMode.None;
                    item.Stop();
                }
            });
        }

        /// <summary>
        /// 连续运行
        /// </summary>
        /// <param name="projectID"></param>
        public void StartRun(int projectID)
        {
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).RunMode =
                eRunMode.RunCycle; //循环
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).Start();
        }

        /// <summary>
        /// 执行一次
        /// </summary>
        /// <param name="projectID"></param>
        public void ExecuteOnce(int projectID)
        {
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).RunMode =
                eRunMode.RunOnce; //循环
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).Start();
        }

        /// <summary>
        /// 停止运行
        /// </summary>
        /// <param name="projectID"></param>
        public void StopRun(int projectID)
        {
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).RunMode =
                eRunMode.None; //停止
            ProjectList.FirstOrDefault(c => c.ProjectInfo.ProjectID == projectID).Stop();
        }
        #endregion
    }
}
