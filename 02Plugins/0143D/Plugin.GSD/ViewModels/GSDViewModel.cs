using EventMgrLib;
using HalconDotNet;
using Plugin.GSD.Views;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Common.Enums;
using static Plugin.GSD.ViewModels.GSDA;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Plugin.GSD.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
    }

    [Category("3D")]
    [DisplayName("台阶缝隙检测")]
    [ModuleImageName("GSD")]
    [Serializable]
    public class GSDViewModel : ModuleBase
    {
        public override void SetDefaultLink()
        {
            if (InputImageLinkText == null)
            {
                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
                if (moduls == null || moduls.VarModels.Count == 0)
                {
                    return;
                }
                if (InputImageLinkText == null)
                    InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
            }
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();

            try
            {
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText);
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 获取高度图（2通道图需分解）
                HImage heightImage = new HImage();
                HTuple channels = DispImage.CountChannels();
                if (channels == 2)
                    heightImage = DispImage.Decompose2(out HImage grayImage);
                else
                    heightImage = DispImage;

                // 取 ROI 区域内的所有点
                HRegion roiDomain = DispImage.GetDomain();
                roiDomain.GetRegionPoints(out HTuple pointY, out HTuple pointX);

                // 读取各点的 Z 值（灰度值 = 高度）
                HTuple pointZ = heightImage.GetGrayval(pointY, pointX);

                // 判断有效点数是否满足要求
                bool pointValid = pointZ.Length >= 1000;
                if (pointValid == false) {
                    Logger.AddLog("有效点数 < 1000, 不满足要求！", eMsgType.Error);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }


                // 调用 C++ DLL 计算台阶宽度和高度
                GSDA.ResultPara result;
                GSDA.Vector3d transformationMatrix = new GSDA.Vector3d(
                    TransformationMatrixX, TransformationMatrixY, TransformationMatrixZ);
                double threshold = HeightThreshold;
                //处理保存路径的问题
                if (DebugPath != _DefaultPath)
                {
                    DebugPath += "\\bspline\\";
                    //更新_DefaultPath,防止重复运行时图片堆积
                    _DefaultPath = DebugPath;
                }
                else if (DebugPath == "") {
                    DebugPath = _DebugPath;
                }
                GSDA.RunGSDAFix(
                    new List<double>(pointX.ToDArr()),
                    new List<double>(pointY.ToDArr()),
                    new List<double>(pointZ.ToDArr()),
                    ref transformationMatrix, ref threshold, out result,
                    DebugPath, DeepMode, DebugMode);
                // ref 参数回写
                TransformationMatrixX = transformationMatrix.x;
                TransformationMatrixY = transformationMatrix.y;
                TransformationMatrixZ = transformationMatrix.z;
                HeightThreshold = threshold;

                StepWidth = result.step_width;
                StepHeight = result.step_height;

                VMHWindowControl mWindowH;
                var view = ModuleView as GSDView;
                if (!IsOpenWindows)
                {
                    mWindowH = ViewDic.GetView(DispImage.DispViewID);
                }
                else
                {
                    mWindowH = view.mWindowH;
                }

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

        public override void AddOutputParams()
        {
            this.Prj.ClearOutputParam(this.ModuleParam);
            AddOutputParam("台阶宽度", "double", StepWidth);
            AddOutputParam("台阶高度", "double", StepHeight);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK ? true : false);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        #region Prop

        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    ShowHRoi();
                }
            }
        }

        private double _StepWidth;
        /// <summary>
        /// 台阶宽度
        /// </summary>
        public double StepWidth
        {
            get { return _StepWidth; }
            set { _StepWidth = value; RaisePropertyChanged(); }
        }

        private double _StepHeight;
        /// <summary>
        /// 台阶高度
        /// </summary>
        public double StepHeight
        {
            get { return _StepHeight; }
            set { _StepHeight = value; RaisePropertyChanged(); }
        }

        #region 算法参数

        private double _TransformationMatrixX = 0.005;
        /// <summary>变换矩阵 X</summary>
        public double TransformationMatrixX
        {
            get { return _TransformationMatrixX; }
            set { _TransformationMatrixX = value; RaisePropertyChanged(); }
        }

        private double _TransformationMatrixY = 0.1;
        /// <summary>变换矩阵 Y</summary>
        public double TransformationMatrixY
        {
            get { return _TransformationMatrixY; }
            set { _TransformationMatrixY = value; RaisePropertyChanged(); }
        }

        private double _TransformationMatrixZ = 1;
        /// <summary>变换矩阵 Z</summary>
        public double TransformationMatrixZ
        {
            get { return _TransformationMatrixZ; }
            set { _TransformationMatrixZ = value; RaisePropertyChanged(); }
        }

        private double _HeightThreshold = 0.01;
        /// <summary>高度阈值</summary>
        public double HeightThreshold
        {
            get { return _HeightThreshold; }
            set { _HeightThreshold = value; RaisePropertyChanged(); }
        }

        private bool _DeepMode = true;
        /// <summary>深度模式</summary>
        public bool DeepMode
        {
            get { return _DeepMode; }
            set { _DeepMode = value; RaisePropertyChanged(); }
        }

        private bool _DebugMode = false;
        /// <summary>调试模式</summary>
        public bool DebugMode
        {
            get { return _DebugMode; }
            set { _DebugMode = value; RaisePropertyChanged(); }
        }
        private string _DefaultPath = "D:\\HymsonVision\\GSDResults\\bspline\\";

        private string _DebugPath = "D:\\HymsonVision\\GSDResults\\bspline\\";
        /// <summary>调试输出路径</summary>
        public string DebugPath
        {
            get { return _DebugPath; }
            set { _DebugPath = value; RaisePropertyChanged(); }
        }

        #endregion

        #endregion

        private bool IsLoad = false;

        #region Command

        public override void Loaded()
        {
            IsLoad = true;
            base.Loaded();
            var view = ModuleView as GSDView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                GetDispImage(InputImageLinkText);
            }
            IsLoad = false;
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            eLinkCommand linkCommand = (eLinkCommand)Enum.Parse(typeof(eLinkCommand), obj.SendName.Split(',')[1].ToString());
            switch (linkCommand)
            {
                case eLinkCommand.InputImageLink:
                    InputImageLinkText = obj.LinkName;
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
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
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
                        var view = this.ModuleView as GSDView;
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
        private CommandBase _SelectFolderCommand;
        public CommandBase SelectFolderCommand
        {
            get
            {
                if (_SelectFolderCommand == null)
                {
                    _SelectFolderCommand = new CommandBase((obj) =>
                    {
                        using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            dialog.SelectedPath = DebugPath;
                            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                DebugPath = dialog.SelectedPath;
                            }
                        }
                    });
                }
                return _SelectFolderCommand;
            }
        }


        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["TransformationMatrixX"] = TransformationMatrixX;
            obj["TransformationMatrixY"] = TransformationMatrixY;
            obj["TransformationMatrixZ"] = TransformationMatrixZ;
            obj["HeightThreshold"] = HeightThreshold;
            obj["DeepMode"] = DeepMode;
            obj["DebugMode"] = DebugMode;
            obj["DebugPath"] = DebugPath ?? "";
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["InputImageLinkText"] != null) InputImageLinkText = obj["InputImageLinkText"].ToString();
                if (obj["TransformationMatrixX"] != null) TransformationMatrixX = obj["TransformationMatrixX"].Value<double>();
                if (obj["TransformationMatrixY"] != null) TransformationMatrixY = obj["TransformationMatrixY"].Value<double>();
                if (obj["TransformationMatrixZ"] != null) TransformationMatrixZ = obj["TransformationMatrixZ"].Value<double>();
                if (obj["HeightThreshold"] != null) HeightThreshold = obj["HeightThreshold"].Value<double>();
                if (obj["DeepMode"] != null) DeepMode = obj["DeepMode"].Value<bool>();
                if (obj["DebugMode"] != null) DebugMode = obj["DebugMode"].Value<bool>();
                if (obj["DebugPath"] != null) DebugPath = obj["DebugPath"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"GSDViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
