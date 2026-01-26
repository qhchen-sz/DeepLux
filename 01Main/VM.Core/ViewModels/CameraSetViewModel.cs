using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EventMgrLib;
using Microsoft.Win32;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Extension;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.PersistentData;
using HV.Services;
using HV.Views;
using WPFLocalizeExtension.Engine;
using VM.Halcon.Config;
using HalconDotNet;

namespace HV.ViewModels
{
    [Serializable]
    public class CameraSetViewModel : NotifyPropertyBase
    {
        #region Singleton

        //private static readonly CameraSetViewModel _instance = new CameraSetViewModel();

        private CameraSetViewModel()
        {
            Timer_ContinuousAcq = new Timer();
            Timer_ContinuousAcq.Interval = 100;
            Timer_ContinuousAcq.Tick += ContinuousAcqMethod;
        }


        public static CameraSetViewModel Ins
        {
            get
            {
                if (Solution.Ins.CameraSetViewModel == null)
                {
                    Solution.Ins.CameraSetViewModel = new CameraSetViewModel();
                }
                return Solution.Ins.CameraSetViewModel;
            }
        }

        #endregion

        #region Prop
        [NonSerialized]
        private int _SelectedIndex;

        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set { _SelectedIndex = value; RaisePropertyChanged(); }
        }

        [NonSerialized]
        public Timer Timer_ContinuousAcq;
        /// <summary>相机列表</summary>
        public ObservableCollection<CameraBase> CameraModels { get; set; } = new ObservableCollection<CameraBase>();
        private CameraBase _SelectedCameraModel;

        public CameraBase SelectedCameraModel
        {
            get { return _SelectedCameraModel; }
            set { _SelectedCameraModel = value; RaisePropertyChanged(); }
        }

        private List<string> _CameraTypes= PluginService.PluginDic_Camera.Keys.ToList();

        public List<string> CameraTypes
        {
            get { return _CameraTypes ; }
            set { _CameraTypes  = value; }
        }
        private string _SelectedCameraType;

        public string SelectedCameraType
        {
            get { return _SelectedCameraType; }
            set { _SelectedCameraType = value; }
        }
        private List<CameraInfoModel> _CameraNos=new List<CameraInfoModel>();

        public List<CameraInfoModel> CameraNos
        {
            get { return _CameraNos; }
            set { _CameraNos = value; }
        }
        private CameraInfoModel _CameraNo =new CameraInfoModel();

        public CameraInfoModel CameraNo
        {
            get { return _CameraNo; }
            set { _CameraNo = value; }
        }

        #endregion

        #region Command
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
                            case "Add":
                                try
                                {
                                    if (CameraSetView.Ins.cmbCameraNo.SelectedIndex == -1) return;
                                    int index = CameraModels.FindIndex(c => c.SerialNo == CameraSetView.Ins.cmbCameraNo.SelectedValue.ToString());
                                    if (index >= 0)
                                    {
                                        MessageView.Ins.MessageBoxShow("该设备已经添加列表!");
                                        return;
                                    }
                                    //根据选中的插件 new一个 模块
                                    PluginsInfo m_PluginsInfo = PluginService.PluginDic_Camera[CameraTypes[CameraSetView.Ins.cmbCameraType.SelectedIndex]];
                                    CameraBase module = (CameraBase)Activator.CreateInstance(m_PluginsInfo.ModuleType);

                                    //确定新模块的不重名名称
                                    if (CameraModels != null)
                                    {
                                        if (CameraModels.Count > 0)
                                        {
                                            List<string> nameList = CameraModels.Select(c => c.CameraNo).ToList();
                                            while (true)
                                            {
                                                if (!nameList.Contains("Dev" + CameraBase.LastNo))
                                                {
                                                    break;//没有重名就跳出循环
                                                }
                                                CameraBase.LastNo++;
                                            }
                                        }
                                        else
                                        {
                                            CameraBase.LastNo++;
                                        }
                                    }
                                    module.CameraNo = "Dev" + CameraBase.LastNo;
                                    var cameraInfo = CameraSetView.Ins.cmbCameraNo.SelectedItem as CameraInfoModel; 
                                    if (cameraInfo != null)
                                    {
                                        module.SerialNo = cameraInfo.SerialNO;
                                        module.CameraType = m_PluginsInfo.ModuleName;
                                        module.Remarks = $"{m_PluginsInfo.ModuleName}_{module.SerialNo}";
                                        module.ExtInfo= cameraInfo.ExtInfo;
                                        CameraModels.Add(module);
                                        EventMgr.Ins.GetEvent<AddCameraEvent>().Publish(new AddCameraEventParamModel() { Camera = module, OperateType = eOperateType.Add });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.GetExceptionMsg(ex);
                                }
                                break;
                            case "Delete":
                                if (SelectedCameraModel == null) return;
                                CameraModels.Remove(SelectedCameraModel);
                                break;
                            case "Modify":
                                break;

                            default:
                                break;
                        }
                        SelectedIndex = CameraModels.Count - 1;
                        EventMgrLib.EventMgr.Ins.GetEvent<HardwareChangedEvent>().Publish();
                    });
                }
                return _DataOperateCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ButtonOperateCommand;
        public CommandBase ButtonOperateCommand
        {
            get
            {
                if (_ButtonOperateCommand == null)
                {
                    _ButtonOperateCommand = new CommandBase((obj) =>
                    {
                        switch (obj)
                        {
                            case "Connect":
                                if (SelectedCameraModel == null) return;
                                SelectedCameraModel.ConnectDev();
                                break;
                            case "Disconnect":
                                if (SelectedCameraModel == null) return;
                                SelectedCameraModel.DisConnectDev();
                                break;
                            case "SingleAcq":
                                if (SelectedCameraModel == null || !SelectedCameraModel.Connected) return;
                                Task.Run(() =>
                                {
                                    //SelectedCameraModel.SetTriggerMode(eTrigMode.内触发);
                                    SelectedCameraModel.SetExposureTime(SelectedCameraModel.ExposeTime);
                                    SelectedCameraModel.SetGain(SelectedCameraModel.Gain);
                                    SelectedCameraModel.EventWait.Reset();
                                    SelectedCameraModel.CaptureImage(false);
                                    SelectedCameraModel.EventWait.WaitOne();
                                    if (SelectedCameraModel.DispImage != null)
                                    {
                                        //CameraSetView.Ins.mWindowH.Image = new RImage(SelectedCameraModel.DispImage);
                                        //SelectedCameraModel.DispImage.GetImageSize(out HTuple width, out HTuple height);
                                        CameraSetView.Ins.mWindowH.HobjectToHimage(SelectedCameraModel.DispImage);
                                    }
                                });
                                break;
                            case "ContinuousAcq":
                                if (SelectedCameraModel == null) return;
                                if (Timer_ContinuousAcq.Enabled)
                                {
                                    Timer_ContinuousAcq.Stop();return;
                                }
                                else
                                {
                                    Timer_ContinuousAcq.Start(); return;
                                }
                            default:
                                break;
                        }
                    });
                }
                return _ButtonOperateCommand;
            }
        }
        [NonSerialized]
        private CommandBase _ActivatedCommand;
        public CommandBase ActivatedCommand
        {
            get
            {
                if (_ActivatedCommand == null)
                {
                    _ActivatedCommand = new CommandBase((obj) =>
                    {
                        if (CameraSetView.Ins.IsClosed)
                        {
                            CameraSetView.Ins.IsClosed = false;
                        }

                    });
                }
                return _ActivatedCommand;
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
                        CameraSetView.Ins.Close();
                    });
                }
                return _ConfirmCommand;
            }
        }

        #endregion

        #region Method
        private void ContinuousAcqMethod(object sender, EventArgs e)
        {
            if (SelectedCameraModel == null) return;
            
            SelectedCameraModel.CaptureImage(false);
            if (SelectedCameraModel.DispImage != null )
            {
                CameraSetView.Ins.mWindowH.Image = new RImage(SelectedCameraModel.DispImage);
            }
        }
        [OnDeserialized()]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            Timer_ContinuousAcq = new Timer();
            Timer_ContinuousAcq.Interval = 100;
            Timer_ContinuousAcq.Tick += ContinuousAcqMethod;
        }

        #endregion
    }
}
