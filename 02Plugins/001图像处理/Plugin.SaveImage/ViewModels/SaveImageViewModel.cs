using EventMgrLib;
using Plugin.SaveImage.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Attributes;
using HV.Common;
using HV.Common.Helper;
using HV.Core;
using HV.Events;
using HV.ViewModels;
using Microsoft.WindowsAPICodePack.Dialogs;
using HV.Models;
using HV.Common.Enums;
using HV.Common.Provide;
using System.IO;
using HalconDotNet;
using VM.Halcon;
using HV.Views.Dock;
using HV.Services;
using System.Windows;
using VM.Halcon.Config;
using System.Globalization;

namespace Plugin.SaveImage.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        PictureName,
        SaveImagePath,
        ImageFilePath,
    }

    public enum eSaveShape
    {
        原图,
        截图,
        离线图片,
    }

    public enum LinkMode
    {
        Path,
        Variable,
    }

    public enum TimePrefixOption
    {
        无前缀,
        年月日时分秒,
    }
    #endregion

    [Category("图像处理")]
    [DisplayName("存储图像")]
    [ModuleImageName("SaveImage")]
    [Serializable]
    public class SaveImageViewModel : ModuleBase
    {
        private static readonly object _viewLock = new object();
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
                if (InputImageLinkText == null || InputImageLinkText == "")
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                GetDispImage(InputImageLinkText);

                string FileFouderPath = "";
                if (SelectedMode == LinkMode.Path)
                    FileFouderPath = FilePath;
                else
                    FileFouderPath = GetLinkValue(SaveImageLinkText).ToString();

                //                保存路径 /
                //├── P001 /              ← 产品编号变量值
                //│   ├── 20231201 /
                //│   │   ├── B001.jpg   ← 批次号变量值
                //│   │   └── B002.jpg
                //│   └── 20231202 /
                //├── P002 /              ← 产品编号变量值
                //│   └── 20231201 /
                //│       └── B003.jpg   ← 批次号变量值
                //└── ...

                // 获取链接变量的实际值（动态内容）
                string linkFolderName = GetDynamicLinkFolderName();

                if (DispImage != null && DispImage.IsInitialized() && FileFouderPath != "")
                {
                    if (!Directory.Exists(FileFouderPath))
                    {
                        Directory.CreateDirectory(FileFouderPath);
                    }

                    // 创建以链接变量值命名的文件夹
                    string linkFolderPath = FileFouderPath;
                    if (!string.IsNullOrEmpty(linkFolderName))
                    {
                        linkFolderPath = Path.Combine(FileFouderPath, linkFolderName);
                        if (!Directory.Exists(linkFolderPath))
                        {
                            Directory.CreateDirectory(linkFolderPath);
                        }
                    }

                    DateTime dt = DateTime.Now;

                    // 在链接变量文件夹下创建日期文件夹
                    string dateFolderPath = Path.Combine(linkFolderPath, dt.ToString("yyyyMMdd"));
                    if (!Directory.Exists(dateFolderPath))
                    {
                        Directory.CreateDirectory(dateFolderPath);
                    }

                    // 生成文件名
                    string fileName = GenerateFileName();
                    string imageFullPath = Path.Combine(dateFolderPath, fileName + "." + ImageStytleList[SelectedIndex]);

                    // 保存图像
                    SaveImageToPath(imageFullPath);

                    // 自动删除旧图片
                    if (AutoDelete && SaveDay > 0)
                    {
                        RemoveOldImages(linkFolderPath, SaveDay);
                    }

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

        private string GetDynamicLinkFolderName()
        {
            string linkFolderName = "";

            if (!string.IsNullOrEmpty(TempFilePath.Text))
            {
                if (TempFilePath.Text.StartsWith("&"))
                {
                    // 获取链接变量的实际值（动态内容）
                    try
                    {
                        object linkValue = GetLinkValue(TempFilePath.Text);
                        if (linkValue != null)
                        {
                            linkFolderName = linkValue.ToString();

                            // 确保文件夹名称合法
                            linkFolderName = MakeValidFolderName(linkFolderName);

                            // 如果值为空，使用默认名称
                            if (string.IsNullOrEmpty(linkFolderName))
                            {
                                linkFolderName = "DefaultFolder";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"获取链接变量值失败: {ex.Message}", eMsgType.Warn);
                        linkFolderName = "ErrorFolder";
                    }
                }
                else
                {
                    // 直接使用输入的文本
                    linkFolderName = TempFilePath.Text;
                }
            }

            return linkFolderName;
        }

        private string MakeValidFolderName(string folderName)
        {
            // 移除非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                folderName = folderName.Replace(invalidChar, '_');
            }

            // 限制长度
            if (folderName.Length > 100)
            {
                folderName = folderName.Substring(0, 100);
            }

            return folderName.Trim();
        }

        private string GenerateFileName()
        {
            DateTime dt = DateTime.Now;

            if (ImageNameTime)
            {
                return dt.ToString("HH_mm_ss");
            }
            else
            {
                string LinkName = "";
                if (PicName.Text.StartsWith("&"))
                {
                    // 获取图片名称链接变量的实际值
                    try
                    {
                        object linkValue = GetLinkValue(PicName.Text);
                        if (linkValue != null)
                        {
                            LinkName = linkValue.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"获取图片名称链接变量值失败: {ex.Message}", eMsgType.Warn);
                        LinkName = "ErrorName";
                    }
                }
                else
                {
                    LinkName = PicName.Text;
                }

                if (string.IsNullOrEmpty(LinkName))
                    LinkName = "1";

                return GetTimePrefix() + MakeValidFileName(LinkName);
            }
        }

        private string MakeValidFileName(string fileName)
        {
            // 移除非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            // 限制长度
            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return fileName.Trim();
        }

        private void SaveImageToPath(string imageFullPath)
        {
            switch (SelectedSaveType)
            {
                case eSaveShape.原图:
                    HOperatorSet.WriteImage(DispImage, ImageStytleList[SelectedIndex], 0, imageFullPath);
                    break;
                case eSaveShape.截图:
                    // 在需要访问共享资源的地方加锁
                    lock (_viewLock)
                    {
                        VMHWindowControl mWindowH = ViewDic.GetView(98);
                        HWindow hWindow = mWindowH.hControl.HalconWindow;
                        HOperatorSet.GetImageSize(DispImage, out HTuple Width, out HTuple Height);
                        hWindow.SetWindowExtents(0, 0, Width, Height);
                        mWindowH.HobjectToHimage(DispImage);
                        ShowHRoiForSave(mWindowH, DispImage.mHRoi, imageFullPath);
                    }
                    break;
                case eSaveShape.离线图片:
                    // 离线图片处理逻辑
                    break;
            }
        }

        private string GetTimePrefix()
        {
            DateTime now = DateTime.Now;

            switch (SelectedTimePrefix)
            {
                case TimePrefixOption.年月日时分秒:
                    return now.ToString("yyyyMMdd_HHmmss_");
                case TimePrefixOption.无前缀:
                default:
                    return "";
            }
        }

        private void RemoveOldImages(string baseDirectoryPath, int saveDays)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDirectoryPath) || saveDays <= 0 || !Directory.Exists(baseDirectoryPath))
                    return;

                DateTime currentTime = DateTime.Now;
                var cutoffDate = currentTime.AddDays(-saveDays);

                // 删除baseDirectoryPath下的所有日期文件夹中的旧文件
                foreach (var directory in Directory.GetDirectories(baseDirectoryPath))
                {
                    RemoveFilesFromDirectory(directory, cutoffDate);

                    // 如果日期文件夹为空，删除空文件夹
                    if (Directory.GetFiles(directory).Length == 0 &&
                        Directory.GetDirectories(directory).Length == 0)
                    {
                        try
                        {
                            Directory.Delete(directory);
                        }
                        catch (Exception ex)
                        {
                            Logger.AddLog($"删除空目录失败: {directory}, 错误: {ex.Message}", eMsgType.Warn);
                        }
                    }
                }

                Logger.AddLog($"在文件夹 {Path.GetFileName(baseDirectoryPath)} 中自动删除超过{saveDays}天的图片完成", eMsgType.Info);
            }
            catch (Exception ex)
            {
                Logger.AddLog($"自动删除图片时发生错误: {ex.Message}", eMsgType.Warn);
            }
        }

        private void RemoveFilesFromDirectory(string directoryPath, DateTime cutoffDate)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directoryPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate || fileInfo.LastWriteTime < cutoffDate)
                        {
                            fileInfo.Attributes = FileAttributes.Normal;
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"删除文件失败: {file}, 错误: {ex.Message}", eMsgType.Warn);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"访问目录失败: {directoryPath}, 错误: {ex.Message}", eMsgType.Warn);
            }
        }

        public void ShowHRoiForSave(VMHWindowControl windos, List<HRoi> hRois, string ImageNames)
        {
            if (windos != null)
            {
                windos.DispText.Clear();
                List<HRoi> Temp = new List<HRoi>(hRois);

                //HTuple width = new HTuple(), height = new HTuple();
                //double scale = 1;
                //if (DispImage != null)
                //{
                //    HOperatorSet.GetImageSize(DispImage, out width, out height);
                //    int windowsW = windos.hControl.Width;
                //    int windowsH = windos.hControl.Height;
                //    double scaleX = width.D / windowsW;
                //    double scaleY = height.D / windowsH;
                //    //scale = Math.Min(scaleX, scaleY);
                //    scale = scaleX * scaleY;
                //}



                foreach (HRoi roi in Temp)
                {
                    if (roi.roiType == HRoiType.文字显示)
                    {
                        HText roiText = (HText)roi;
                        roiText.size = roiText.size;
                        ShowTool.SetFont(
                            windos.hControl.HalconWindow,
                            roiText.size,
                            "false",
                            "false"
                        );
                        windos.WindowH.DispText(roiText);
                    }
                    else
                    {
                        windos.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                    }
                }
                windos.WindowH.ResetWindowImage(false);
                HOperatorSet.DumpWindow(windos.hControl.HalconWindow, "jpeg", ImageNames);
                //windos.DispText.Clear();

                //foreach (HRoi roi in hRois)
                //{
                //    if (roi.roiType == HRoiType.文字显示)
                //    {
                //        HText roiText = (HText)roi;
                //        roiText.size = roiText.size / 5;
                //        ShowTool.SetFont(
                //            windos.hControl.HalconWindow,
                //            roiText.size,
                //            "false",
                //            "false"
                //        );
                //        windos.WindowH.DispText(roiText);
                //    }
                //}

                //windos.WindowH.ResetWindowImage(true);
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    VisionView.Ins.ViewMode = Solution.Ins.ViewMode;
                //});
            }
        }

        public override void AddOutputParams()
        {
        }

        #region Prop
        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        private string _SaveImageLinkText;
        public string SaveImageLinkText
        {
            get { return _SaveImageLinkText; }
            set { Set(ref _SaveImageLinkText, value); }
        }

        private string _SaveImageLinkTextValue;
        public string SaveImageLinkTextValue
        {
            get { return _SaveImageLinkTextValue; }
            set { Set(ref _SaveImageLinkTextValue, value); }
        }

        private eSaveShape _SelectedSaveType = eSaveShape.原图;
        public eSaveShape SelectedSaveType
        {
            get { return _SelectedSaveType; }
            set { Set(ref _SelectedSaveType, value); }
        }

        private LinkMode _selectedMode = LinkMode.Path;
        public LinkMode SelectedMode
        {
            get { return _selectedMode; }
            set { _selectedMode = value; RaisePropertyChanged(); }
        }

        private string _FilePath;
        public string FilePath
        {
            get { return _FilePath; }
            set { _FilePath = value; RaisePropertyChanged(); }
        }

        public List<string> ImageStytleList { get; set; } = new List<string>() { "bmp", "jpg", "png", "tiff", "gif" };

        private int _SelectedIndex = 0;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set { Set(ref _SelectedIndex, value); }
        }

        private int _SaveDay = 7;
        public int SaveDay
        {
            get { return _SaveDay; }
            set { Set(ref _SaveDay, value); }
        }

        private LinkVarModel _PicName = new LinkVarModel() { Text = "1" };
        public LinkVarModel PicName
        {
            get { return _PicName; }
            set { _PicName = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _TempFilePath = new LinkVarModel();
        public LinkVarModel TempFilePath
        {
            get { return _TempFilePath; }
            set { _TempFilePath = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _SaveImagePath = new LinkVarModel();
        public LinkVarModel SaveImagePath
        {
            get { return _SaveImagePath; }
            set { _SaveImagePath = value; RaisePropertyChanged(); }
        }

        private bool _ImageNameTime;
        public bool ImageNameTime
        {
            get { return _ImageNameTime; }
            set { Set(ref _ImageNameTime, value); }
        }

        private bool _AutoDelete = true;
        public bool AutoDelete
        {
            get { return _AutoDelete; }
            set { Set(ref _AutoDelete, value); }
        }

        private TimePrefixOption _selectedTimePrefix = TimePrefixOption.无前缀;
        public TimePrefixOption SelectedTimePrefix
        {
            get { return _selectedTimePrefix; }
            set { Set(ref _selectedTimePrefix, value); }
        }

        private string _customTimeFormat = "yyyyMMdd_HHmmss";
        public string CustomTimeFormat
        {
            get { return _customTimeFormat; }
            set { Set(ref _customTimeFormat, value); }
        }

        private bool _isCustomTimeFormatVisible = false;
        public bool IsCustomTimeFormatVisible
        {
            get { return _isCustomTimeFormatVisible; }
            set { Set(ref _isCustomTimeFormatVisible, value); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as SaveImageView;
            if (view != null)
            {
                ClosedView = true;
                if (InputImageLinkText == null || InputImageLinkText == "")
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
                        var view = ModuleView as SaveImageView;
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
            if (obj == null || string.IsNullOrEmpty(obj.SendName))
                return;

            var parts = obj.SendName.Split(',');
            if (parts.Length < 2)
                return;

            string commandType = parts[1];
            switch (commandType)
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "PictureName":
                    PicName.Text = obj.LinkName ?? "1";
                    break;
                case "SaveImagePath":
                    SaveImagePath.Text = obj.LinkName;
                    break;
                case "ImageFilePath":
                    TempFilePath.Text = obj.LinkName;
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
                            case eLinkCommand.PictureName:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},PictureName");
                                break;
                            case eLinkCommand.SaveImagePath:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},SaveImagePath");
                                break;
                            case eLinkCommand.ImageFilePath:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "string");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},ImageFilePath");
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

        [NonSerialized]
        private CommandBase _ManualDeleteCommand;
        public CommandBase ManualDeleteCommand
        {
            get
            {
                if (_ManualDeleteCommand == null)
                {
                    _ManualDeleteCommand = new CommandBase((obj) =>
                    {
                        string directoryPath = "";
                        if (SelectedMode == LinkMode.Path)
                            directoryPath = FilePath;
                        else
                            directoryPath = GetLinkValue(SaveImageLinkText).ToString();

                        // 获取链接变量的实际值
                        string linkFolderName = GetDynamicLinkFolderName();
                        if (!string.IsNullOrEmpty(linkFolderName))
                        {
                            directoryPath = Path.Combine(directoryPath, linkFolderName);
                        }

                        if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
                        {
                            RemoveOldImages(directoryPath, SaveDay);
                            MessageBox.Show($"已在文件夹 {linkFolderName} 中完成手动删除");
                        }
                        else
                        {
                            MessageBox.Show("目录不存在或路径无效");
                        }
                    });
                }
                return _ManualDeleteCommand;
            }
        }
        #endregion
    }
}