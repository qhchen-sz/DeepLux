using DMSkin.Socket;
using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Plugin.NPointCal.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.ModelBinding;
using System.Web.UI.WebControls.Expressions;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;
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
using HV.Views;
using HV.Views.Dock;

namespace Plugin.NPointCal.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        DispContentLink,
        StatusLink,
        InputImageLink,
        InputImageX,
        InputImageY,
        InputRealX,
        InputRealY,

    }
    public enum eInputType
    {
        小数
    }
    #endregion

    [Category("坐标标定")]
    [DisplayName("九点标定")]
    [ModuleImageName("NPointCal")]
    [Serializable]
    public class NPointCal : ModuleBase
    {
        // 添加一个计数器来跟踪自动执行的次数
        private int _autoExecuteCounter = 0;

        public override void SetDefaultLink()
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

        public override bool ExeModule()
        {
            Stopwatch.Restart();

            try
            {
                int requiredPoints = 0;
                switch (MPointType)
                {
                    case PointType.Three:
                        requiredPoints = 3;
                        break;
                    case PointType.Nine:
                        requiredPoints = 9;
                        break;
                    case PointType.Fourteen:
                        requiredPoints = 14;
                        break;
                }

                if (NPointCalParams == null)
                {
                    // 在UI线程上初始化集合
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        NPointCalParams = new ObservableCollection<NPointCalParam>();
                    });
                }

                // 检查是否已达到所需点数
                int currentCount = NPointCalParams.Count;
                if (currentCount >= requiredPoints)
                {
                    // 如果已经达到所需点数，只计算标定矩阵
                    CalculateCalibrationMatrix();
                    AddOutputParams();
                    ChangeModuleRunStatus(eRunStatus.OK);
                    return true;
                }

                // 获取步长（从输入中解析）
                double step = 3.0; // 默认步长
                if (!string.IsNullOrWhiteSpace(InputRealXLinkText))
                {
                    double parsedStep = GetDoubleFromLinkText(InputRealXLinkText);
                    if (parsedStep != 0)
                    {
                        step = Math.Abs(parsedStep);
                    }
                }

                // 获取当前输入的点（图像坐标）
                double imageX = GetDoubleFromLinkText(InputPixelXLinkText);
                double imageY = GetDoubleFromLinkText(InputPixelYLinkText);

                // 根据自动执行次数决定机械坐标
                double realX = 0;
                double realY = 0;

                // 根据当前执行次数计算对应的机械坐标
                int currentPointIndex = _autoExecuteCounter % 9; // 0-8循环

                switch (currentPointIndex)
                {
                    case 0: // 第一个点：(0, 0)
                        realX = 0;
                        realY = 0;
                        break;
                    case 1: // 第二个点：(step, 0)
                        realX = step;
                        realY = 0;
                        break;
                    case 2: // 第三个点：(step, step)
                        realX = step;
                        realY = step;
                        break;
                    case 3: // 第四个点：(0, step)
                        realX = 0;
                        realY = step;
                        break;
                    case 4: // 第五个点：(-step, step)
                        realX = -step;
                        realY = step;
                        break;
                    case 5: // 第六个点：(-step, 0)
                        realX = -step;
                        realY = 0;
                        break;
                    case 6: // 第七个点：(-step, -step)
                        realX = -step;
                        realY = -step;
                        break;
                    case 7: // 第八个点：(0, -step)
                        realX = 0;
                        realY = -step;
                        break;
                    case 8: // 第九个点：(step, -step)
                        realX = step;
                        realY = -step;
                        break;
                }

                // 在UI线程上执行集合修改
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 检查是否已经存在相同坐标的点（防止重复添加）
                    bool pointExists = NPointCalParams.Any(p =>
                        Math.Abs(p.ImageX - imageX) < 0.001 &&
                        Math.Abs(p.ImageY - imageY) < 0.001 &&
                        Math.Abs(p.RealX - realX) < 0.001 &&
                        Math.Abs(p.RealY - realY) < 0.001);

                    if (!pointExists)
                    {
                        // 计算新ID
                        int newId = NPointCalParams.Count + 1;

                        // 添加当前点
                        NPointCalParams.Add(new NPointCalParam()
                        {
                            ID = newId,
                            ImageX = imageX,
                            ImageY = imageY,
                            RealX = realX,
                            RealY = realY
                        });

                        Logger.AddLog($"已自动添加第{newId}个点，机械坐标: ({realX:F3}, {realY:F3})", eMsgType.Info);
                        _autoExecuteCounter++;
                    }
                    else
                    {
                        Logger.AddLog("当前点已存在，跳过添加", eMsgType.Info);
                        // 即使点已存在，也增加计数器，以便下次获取下一个点
                        _autoExecuteCounter++;
                    }
                });

                // 检查是否达到所需点数（需要重新获取计数，因为集合可能在UI线程中更新）
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (NPointCalParams.Count >= requiredPoints)
                    {
                        // 计算标定矩阵
                        CalculateCalibrationMatrix();

                        // 测试验证（可选）
                        if (testImagex != 0 || testImagey != 0)
                        {
                            if (MHomMat2DTransl.Length > 0)
                            {
                                HTuple X = new HTuple();
                                HTuple Y = new HTuple();
                                HOperatorSet.AffineTransPoint2d(MHomMat2DTransl, testImagex, testImagey, out X, out Y);
                                testrealx = X.D;
                                testrealy = Y.D;
                            }
                        }

                        AddOutputParams();
                        ChangeModuleRunStatus(eRunStatus.OK);

                        // 如果是九点模式且达到9个点，显示完成信息
                        if (MPointType == PointType.Nine && NPointCalParams.Count >= 9)
                        {
                            Logger.AddLog($"九点标定已完成，共{NPointCalParams.Count}个点", eMsgType.Success);
                        }
                    }
                    else
                    {
                        Logger.AddLog($"当前点数：{NPointCalParams.Count}，需要{requiredPoints}个点才能计算标定", eMsgType.Info);
                        ChangeModuleRunStatus(eRunStatus.OK);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.AddLog($"执行错误: {ex.Message}", eMsgType.Error);
                return false;
            }
        }

        private void CalculateCalibrationMatrix()
        {
            try
            {
                List<double> LImageX = new List<double>();
                List<double> LImageY = new List<double>();
                List<double> LRealX = new List<double>();
                List<double> LRealY = new List<double>();

                int pointsToUse = 0;
                switch (MPointType)
                {
                    case PointType.Three:
                        pointsToUse = 3;
                        break;
                    case PointType.Nine:
                        pointsToUse = 9;
                        break;
                    case PointType.Fourteen:
                        pointsToUse = 9; // 14点标定使用前9个点计算变换矩阵
                        break;
                }

                // 收集用于计算变换矩阵的点
                for (int i = 0; i < Math.Min(pointsToUse, NPointCalParams.Count); i++)
                {
                    LImageX.Add(NPointCalParams[i].ImageX);
                    LImageY.Add(NPointCalParams[i].ImageY);
                    LRealX.Add(NPointCalParams[i].RealX);
                    LRealY.Add(NPointCalParams[i].RealY);
                }

                if (LImageX.Count >= 3)
                {
                    // 计算仿射变换矩阵
                    HOperatorSet.VectorToHomMat2d(
                        new HTuple(LImageX.ToArray()),
                        new HTuple(LImageY.ToArray()),
                        new HTuple(LRealX.ToArray()),
                        new HTuple(LRealY.ToArray()),
                        out mHomMat2DTransl);

                    // 如果是14点标定，计算旋转中心
                    if (MPointType == PointType.Fourteen && NPointCalParams.Count >= 14)
                    {
                        List<double> XfitCir = new List<double>();
                        List<double> YfitCir = new List<double>();

                        // 使用最后5个点拟合圆（计算旋转中心）
                        for (int i = 9; i < 14; i++)
                        {
                            if (i < NPointCalParams.Count)
                            {
                                XfitCir.Add(NPointCalParams[i].ImageX);
                                YfitCir.Add(NPointCalParams[i].ImageY);
                            }
                        }

                        if (XfitCir.Count >= 3)
                        {
                            Fit.FitCircle(YfitCir.ToArray(), XfitCir.ToArray(), out Circle_Info 拟合圆);
                            mRotateCenterX = 拟合圆.CenterX;
                            mRotateCenterY = 拟合圆.CenterY;
                        }
                    }

                    Logger.AddLog("标定矩阵计算完成", eMsgType.Success);
                }
                else
                {
                    Logger.AddLog("点数不足，无法计算标定矩阵", eMsgType.Info);
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"计算标定矩阵错误: {ex.Message}", eMsgType.Error);
            }
        }

        public override void InitModule()
        {
            IsOpenWindows = true;
            // 初始化时，重置计数器
            _autoExecuteCounter = 0;
            // 初始化时，如果表格为空，可以不做任何操作
            // ExeModule();  // 注释掉这一行，因为初始化时不应该执行标定
            IsOpenWindows = false;
        }

        #region 抄过来
        /// <summary>点类型：3/9/14</summary>
        private PointType mPointType = PointType.Nine;
        /// <summary>点类型：3/9/14</summary>
        public PointType MPointType { get => mPointType; set => mPointType = value; }
        /// <summary>标定:手动/自动 </summary>

        /// <summary>相机:固定/变化 </summary>
        private bool mCamerFix = false;
        /// <summary>相机:固定/变化 </summary>
        public bool MCamerFix { get => mCamerFix; set => mCamerFix = value; }
        /// <summary>输入X</summary>
        private string _InputPixelXLinkText = "数据链接";
        /// <summary>输入X</summary>
        public string InputPixelXLinkText
        {
            get { return _InputPixelXLinkText; }
            set { _InputPixelXLinkText = value; RaisePropertyChanged(); }
        }
        /// <summary>输入Y</summary>
        private string _InputPixelYLinkText = "数据链接";
        /// <summary>输入Y</summary>
        public string InputPixelYLinkText
        {
            get { return _InputPixelYLinkText; }
            set { _InputPixelYLinkText = value; RaisePropertyChanged(); }
        }
        /// <summary>输入X</summary>
        private string _InputRealXLinkText = "数据链接";
        /// <summary>输入X</summary>
        public string InputRealXLinkText
        {
            get { return _InputRealXLinkText; }
            set { _InputRealXLinkText = value; RaisePropertyChanged(); }
        }
        /// <summary>输入Y</summary>
        private string _InputRealYLinkText = "数据链接";
        /// <summary>输入Y</summary>
        public string InputRealYLinkText
        {
            get { return _InputRealYLinkText; }
            set { _InputRealYLinkText = value; RaisePropertyChanged(); }
        }

        /// <summary>基准角度</summary>
        private double mBaseAngle = 0.0;
        /// <summary>基准角度</summary>
        public double MBaseAngle { get => mBaseAngle; set => mBaseAngle = value; }
        private bool mAngleNot = false;
        /// <summary>角度取反</summary>
        public bool MAngleNot { get => mAngleNot; set => mAngleNot = value; }
        /// <summary>是否启用旋转中心标定</summary>
        private bool mRotateCentre = true;
        /// <summary>MarkX</summary>
        private double mMarkX = 0.0;
        /// <summary>MarkY</summary>
        private double mMarkY = 0.0;
        /// <summary>旋转角度</summary>
        private double mRotateAngle = 0.0;
        /// <summary>是否自动清空</summary>
        private bool mAutoCleare = false;
        /// <summary>检查标定结果</summary>
        private bool mCheckCalResult = false;
        /// <summary>旋转中心X</summary>
        private double mRotateCenterX = 0.0;
        /// <summary>旋转中心Y</summary>
        private double mRotateCenterY = 0.0;
        ///<summary>相机和X轴的夹角 </summary>
        private double mPhiSingle = 0.0;
        ///<summary>标定RMS</summary>
        private double mCalibRms = 0.0;
        /// <summary>平移矩阵</summary>
        [NonSerialized]
        private HTuple mHomMat2DTransl = new HTuple();
        /// <summary>旋转矩阵</summary>
        [NonSerialized]
        private HHomMat2D mHomMat2DRotate = new HHomMat2D();
        //坐标集合-序列话也无所谓*****************************************
        /// <summary>像素坐标X</summary>
        public double[] ImageX_S = new double[14];
        /// <summary>像素坐标Y</summary>
        public double[] ImageY_S = new double[14];
        /// <summary>机械坐标X</summary>
        public double[] RobotX_S = new double[14];
        /// <summary>机械坐标Y</summary>
        public double[] RobotY_S = new double[14];
        /// <summary>自动标定计数</summary>
        public int mAutoCalCounter = 0;
        /// <summary>标定自动清零</summary>
        public bool mAutoClear = false;
        /// <summary>标定点信息</summary>
        [NonSerialized]
        public List<NPoint> mNPoint = new List<NPoint>();
        private int _CurrentRow;
        public int CurrentRow
        {
            get { return _CurrentRow; }
            set { _CurrentRow = value; }
        }

        private double _testImagex;
        public double testImagex
        {
            get { return _testImagex; }
            set { _testImagex = value; RaisePropertyChanged(); }
        }
        private double _testImagey;
        public double testImagey
        {
            get { return _testImagey; }
            set { _testImagey = value; RaisePropertyChanged(); }
        }
        private double _testrealx;
        public double testrealx
        {
            get { return _testrealx; }
            set { _testrealx = value; RaisePropertyChanged(); }
        }
        private double _testrealy;
        public double testrealy
        {
            get { return _testrealy; }
            set { _testrealy = value; RaisePropertyChanged(); }
        }


        #endregion
        #region Prop
        private string _InputImageLinkText;
        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }
        private ObservableCollection<NPointCalParam> _NPointCalParams = new ObservableCollection<NPointCalParam>();
        public ObservableCollection<NPointCalParam> NPointCalParams
        {
            get { return _NPointCalParams; }
            set { _NPointCalParams = value; RaisePropertyChanged(); }
        }

        public void SaveNPointCalParamsToFile(ObservableCollection<NPointCalParam> data, string filePath)
        {
            // 使用StringBuilder高效构建文本内容
            var sb = new StringBuilder();

            foreach (var item in data)
            {
                // 添加四个属性值，用空格分隔
                sb.Append($"{item.ImageX} {item.ImageY} {item.RealX} {item.RealY}");

                // 每个记录用回车分隔
                sb.AppendLine();  // 相当于添加 \r\n
            }

            // 写入文件（覆盖模式）
            File.WriteAllText(filePath, sb.ToString());
        }

        public ObservableCollection<NPointCalParam> LoadNPointCalParamsFromFile(string filePath)
        {
            var result = new ObservableCollection<NPointCalParam>();

            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("校准数据文件未找到", filePath);
            }

            // 读取所有行
            string[] lines = File.ReadAllLines(filePath);
            int idCounter = 1;  // 自增ID计数器

            foreach (string line in lines)
            {
                // 跳过空行
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // 分割行数据（假设使用空格分隔）
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // 验证数据格式（应有4个数值）
                if (parts.Length != 4)
                {
                    // 可选：记录错误或跳过
                    continue;
                }

                // 尝试解析数值
                if (double.TryParse(parts[0], out double imageX) &&
                    double.TryParse(parts[1], out double imageY) &&
                    double.TryParse(parts[2], out double realX) &&
                    double.TryParse(parts[3], out double realY))
                {
                    // 创建对象并添加到集合
                    result.Add(new NPointCalParam
                    {
                        ID = idCounter++,
                        ImageX = imageX,
                        ImageY = imageY,
                        RealX = realX,
                        RealY = realY
                    });
                }
            }

            return result;
        }
        #endregion
        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as NPointCalView;
            if (view != null)
            {
                SetDefaultLink();
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
                        // 执行模块，会自动添加点
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
                        var view = this.ModuleView as NPointCalView;
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
        private CommandBase _AddCurrentPointCommand;
        public CommandBase AddCurrentPointCommand
        {
            get
            {
                if (_AddCurrentPointCommand == null)
                {
                    _AddCurrentPointCommand = new CommandBase((obj) =>
                    {
                        try
                        {
                            // 获取当前输入的点
                            double imageX = GetDoubleFromLinkText(InputPixelXLinkText);
                            double imageY = GetDoubleFromLinkText(InputPixelYLinkText);
                            double realX = GetDoubleFromLinkText(InputRealXLinkText);
                            double realY = GetDoubleFromLinkText(InputRealYLinkText);

                            // 检查是否已经存在相同坐标的点
                            bool pointExists = NPointCalParams.Any(p =>
                                Math.Abs(p.ImageX - imageX) < 0.001 &&
                                Math.Abs(p.ImageY - imageY) < 0.001 &&
                                Math.Abs(p.RealX - realX) < 0.001 &&
                                Math.Abs(p.RealY - realY) < 0.001);

                            if (!pointExists)
                            {
                                // 重新计算所有行的ID，确保连续
                                int newId = NPointCalParams.Count + 1;

                                NPointCalParams.Add(new NPointCalParam()
                                {
                                    ID = newId,
                                    ImageX = imageX,
                                    ImageY = imageY,
                                    RealX = realX,
                                    RealY = realY
                                });

                                Logger.AddLog($"已手动添加第{newId}个点", eMsgType.Info);
                            }
                            else
                            {
                                Logger.AddLog("当前点已存在，跳过添加", eMsgType.Info);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.AddLog($"添加点失败: {ex.Message}", eMsgType.Error);
                        }
                    });
                }
                return _AddCurrentPointCommand;
            }
        }

        // 辅助方法：从链接文本中获取double值
        private double GetDoubleFromLinkText(string linkText)
        {
            if (string.IsNullOrWhiteSpace(linkText))
                return 0;

            if (linkText.StartsWith("&"))
            {
                try
                {
                    return base.GetDouble(linkText);
                }
                catch
                {
                    if (double.TryParse(linkText.Replace("&", ""), out double result))
                        return result;
                    return 0;
                }
            }
            else
            {
                if (double.TryParse(linkText, out double result))
                    return result;
                return 0;
            }
        }

        [NonSerialized]
        private CommandBase _GenerateNinePointsCommand;
        public CommandBase GenerateNinePointsCommand
        {
            get
            {
                if (_GenerateNinePointsCommand == null)
                {
                    _GenerateNinePointsCommand = new CommandBase((obj) =>
                    {

                    });
                }
                return _GenerateNinePointsCommand;
            }
        }
        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "ImageX":
                    InputPixelXLinkText = obj.LinkName;
                    break;
                case "ImageY":
                    InputPixelYLinkText = obj.LinkName;
                    break;
                case "RealX":
                    InputRealXLinkText = obj.LinkName;
                    break;
                case "RealY":
                    InputRealYLinkText = obj.LinkName;
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
                    //以GUID+类名作为筛选器
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.DispContentLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "bool,string,double,int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},DispContent");
                                break;
                            case eLinkCommand.StatusLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "bool");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},StatusLink");
                                break;
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.InputImageX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},ImageX");
                                break;
                            case eLinkCommand.InputImageY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},ImageY");
                                break;
                            case eLinkCommand.InputRealX:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},RealX");
                                break;
                            case eLinkCommand.InputRealY:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "double");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},RealY");
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
                            case "AddRow":
                                // 查找当前最大ID
                                int newId = NPointCalParams.Count + 1;

                                NPointCalParams.Add(new NPointCalParam() { ID = newId });
                                break;

                            case "DeleteRow":
                                if ((CurrentRow < 0) || (NPointCalParams.Count <= 0)) return;

                                NPointCalParams.RemoveAt(CurrentRow);
                                // 重新计算ID，确保连续
                                RecalculateIds();
                                break;

                            case "WriteToFile":
                                {
                                    System.Windows.Forms.SaveFileDialog sa = new System.Windows.Forms.SaveFileDialog();
                                    sa.Filter = "文本文件|*.txt|所有文件|*.*";
                                    sa.Title = "保存标定点数据";
                                    if (sa.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                    {
                                        SaveNPointCalParamsToFile(NPointCalParams, sa.FileName);
                                    }
                                }
                                break;

                            case "LoadFromFile":
                                {
                                    System.Windows.Forms.OpenFileDialog op = new System.Windows.Forms.OpenFileDialog();
                                    op.Filter = "文本文件|*.txt|所有文件|*.*";
                                    op.Title = "加载标定点数据";
                                    if (op.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                                    {
                                        NPointCalParams = LoadNPointCalParamsFromFile(op.FileName);
                                    }
                                }
                                break;


                            case "Test":
                                if (MHomMat2DTransl.Length > 0)
                                {
                                    HTuple X = new HTuple();
                                    HTuple Y = new HTuple();
                                    HOperatorSet.AffineTransPoint2d(MHomMat2DTransl, testImagex, testImagey, out X, out Y);
                                    testrealx = X.D;
                                    testrealy = Y.D;
                                }
                                else
                                {
                                    System.Windows.Forms.MessageBox.Show("请先执行标定，获取标定矩阵", "提示",
                                        System.Windows.Forms.MessageBoxButtons.OK,
                                        System.Windows.Forms.MessageBoxIcon.Warning);
                                }
                                break;

                            case "ClearTable":
                                NPointCalParams.Clear();
                                _autoExecuteCounter = 0;
                                Logger.AddLog("表格已清空", eMsgType.Info);
                                break;

                            default:
                                break;
                        }
                    });
                }
                return _DataOperateCommand;
            }
        }

        // 重新计算所有行的ID，确保连续
        private void RecalculateIds()
        {
            for (int i = 0; i < NPointCalParams.Count; i++)
            {
                NPointCalParams[i].ID = i + 1;
            }
            // 触发集合更改通知
            RaisePropertyChanged(nameof(NPointCalParams));
        }

        public override void AddOutputParams()
        {
            // 移除IsOpenWindows条件，确保自动执行时也能输出参数
            base.AddOutputParams();
            AddOutputParam("MHomMat2DTransl", "Htuple", MHomMat2DTransl);
            AddOutputParam("mRotateCenterX", "double", mRotateCenterX);
            AddOutputParam("mRotateCenterY", "double", mRotateCenterY);
        }

        /// <summary>平移矩阵</summary>
        public HTuple MHomMat2DTransl { get => mHomMat2DTransl; set => mHomMat2DTransl = value; }
        #endregion
    }

    [Serializable]
    public class NPointCalParam : INotifyPropertyChanged
    {
        private int _id;
        private double _imageX;
        private double _imageY;
        private double _realX;
        private double _realY;

        public int ID
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged(nameof(ID)); }
        }

        public double ImageX
        {
            get { return _imageX; }
            set { _imageX = value; OnPropertyChanged(nameof(ImageX)); }
        }

        public double ImageY
        {
            get { return _imageY; }
            set { _imageY = value; OnPropertyChanged(nameof(ImageY)); }
        }

        public double RealX
        {
            get { return _realX; }
            set { _realX = value; OnPropertyChanged(nameof(RealX)); }
        }

        public double RealY
        {
            get { return _realY; }
            set { _realY = value; OnPropertyChanged(nameof(RealY)); }
        }
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}