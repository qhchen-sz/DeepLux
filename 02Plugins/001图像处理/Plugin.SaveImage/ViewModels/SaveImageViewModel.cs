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
using System.Threading;

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
        private static readonly object _ExeLock = new object();
        private static readonly object _saveImageLock = new object();
        private static readonly object _deleteLock = new object();
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

                // 加锁保护：确保从获取图像到复制图像的过程不被其他工作流干扰
                lock (_ExeLock)
                {
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
                        // 生成路径信息（这些是轻量级操作，可同步执行）
                        DateTime dt = DateTime.Now;
                        string linkFolderPath = FileFouderPath;
                        if (!string.IsNullOrEmpty(linkFolderName))
                        {
                            linkFolderPath = Path.Combine(FileFouderPath, linkFolderName);
                        }
                        string dateFolderPath = Path.Combine(linkFolderPath, dt.ToString("yyyyMMdd"));
                        string fileName = GenerateFileName();
                        string imageFullPath = Path.Combine(dateFolderPath, fileName + "." + ImageStytleList[SelectedIndex]);
                        string saveType = SelectedSaveType.ToString();

                        // 复制图像数据，避免原对象被释放导致问题
                        HImage imageCopy = new HImage(DispImage.CopyImage());

                        // 获取ROI数据（需要在主线程获取，因为可能涉及UI相关数据）
                        List<HRoi> roiCopy = null;
                        if (DispImage.mHRoi != null && DispImage.mHRoi.Count > 0)
                        {
                            roiCopy = new List<HRoi>(DispImage.mHRoi);
                        }

                        // 所有耗时操作放到异步任务中执行，不阻塞主线程
                        Task.Run(() => SaveImageAsync(imageCopy, FileFouderPath, linkFolderPath, dateFolderPath, imageFullPath, saveType, roiCopy));

                        ChangeModuleRunStatus(eRunStatus.OK);
                        return true;
                    }
                    else
                    {
                        ChangeModuleRunStatus(eRunStatus.NG);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        /// <summary>
        /// 异步保存图像（所有耗时操作都在后台线程执行）
        /// </summary>
        private void SaveImageAsync(HImage image, string baseFolderPath, string linkFolderPath, string dateFolderPath, string imageFullPath, string saveType, List<HRoi> roiCopy)
        {
            System.Diagnostics.Stopwatch saveStopwatch = new System.Diagnostics.Stopwatch();
            try
            {
                // 确保目录存在
                EnsureDirectoriesExist(baseFolderPath, linkFolderPath, dateFolderPath);

                //// 图像有效性检查
                //if (!ValidateImageForSave(image))
                //{
                //    Logger.AddLog($"图像无效，跳过保存: {imageFullPath}", eMsgType.Warn);
                //    return;
                //}

                //// 磁盘空间检查（低于100MB时跳过保存）
                //if (!HasSufficientDiskSpace(imageFullPath))
                //{
                //    return;
                //}

                // 根据保存类型执行保存
                switch (saveType)
                {
                    case "原图":
                        // 原图保存使用独立锁，不阻塞截图操作
                        lock (_saveImageLock)
                        {
                            saveStopwatch.Start();
                            HOperatorSet.WriteImage(image, ImageStytleList[SelectedIndex], 50, imageFullPath);
                            saveStopwatch.Stop();
                            Logger.AddLog($"保存原图完成，路径: {imageFullPath}，耗时: {saveStopwatch.ElapsedMilliseconds}ms", eMsgType.Info);
                        }
                        break;
                    case "截图":
                        // 截图模式需要访问UI窗口，使用 _viewLock 保护
                        SaveScreenshotAsync(image, imageFullPath, roiCopy);
                        break;
                    case "离线图片":
                        // 离线图片处理
                        break;
                }

                // 自动删除旧图片（异步执行，带频率限制）
                if (AutoDelete && SaveDay > 0)
                {
                    TryRemoveOldImagesAsync(linkFolderPath, SaveDay);
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"异步保存图像失败: {ex.Message}", eMsgType.Error);
            }
            finally
            {
                // 释放图像副本
                if (image != null && image.IsInitialized())
                {
                    image.Dispose();
                }
            }
        }

        /// <summary>
        /// 检查磁盘剩余空间是否充足（低于100MB时跳过保存）
        /// </summary>
        private bool HasSufficientDiskSpace(string path, long minimumBytes = 100 * 1024 * 1024)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                    return true;
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.AvailableFreeSpace < minimumBytes)
                {
                    Logger.AddLog($"磁盘空间不足: 剩余{drive.AvailableFreeSpace / 1024 / 1024}MB < 100MB, 路径: {path}", eMsgType.Warn);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.AddLog($"检查磁盘空间失败: {ex.Message}", eMsgType.Warn);
                return true; // 检查失败时不阻止保存
            }
        }

        /// <summary>
        /// 验证图像是否有效（非空、已初始化、尺寸合法）
        /// </summary>
        private bool ValidateImageForSave(HImage image)
        {
            try
            {
                if (image == null || !image.IsInitialized())
                {
                    Logger.AddLog("图像为空或未初始化，跳过保存", eMsgType.Warn);
                    return false;
                }
                HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
                if (w.I <= 0 || h.I <= 0)
                {
                    Logger.AddLog($"图像尺寸无效: {w.I}x{h.I}，跳过保存", eMsgType.Warn);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.AddLog($"图像有效性检查失败: {ex.Message}", eMsgType.Warn);
                return false;
            }
        }

        /// <summary>
        /// 确保所有目录存在（轻量级操作）
        /// </summary>
        private void EnsureDirectoriesExist(string baseFolderPath, string linkFolderPath, string dateFolderPath)
        {
            if (!Directory.Exists(baseFolderPath))
            {
                Directory.CreateDirectory(baseFolderPath);
            }
            if (!string.IsNullOrEmpty(linkFolderPath) && !Directory.Exists(linkFolderPath))
            {
                Directory.CreateDirectory(linkFolderPath);
            }
            if (!string.IsNullOrEmpty(dateFolderPath) && !Directory.Exists(dateFolderPath))
            {
                Directory.CreateDirectory(dateFolderPath);
            }
        }

        /// <summary>
        /// 异步保存截图（需要访问UI窗口，必须回到主线程执行）
        /// </summary>
        private void SaveScreenshotAsync(HImage image, string imageFullPath, List<HRoi> roiCopy)
        {
            System.Diagnostics.Stopwatch saveStopwatch = new System.Diagnostics.Stopwatch();
            try
            {
                // UI操作必须回到主线程执行
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_viewLock)
                    {
                        saveStopwatch.Start();
                        VMHWindowControl mWindowH = VisionView.Ins.GetImageBox(99);

                        HWindow hWindow = mWindowH.hControl.HalconWindow;
                        HOperatorSet.GetImageSize(image, out HTuple Width, out HTuple Height);
                        hWindow.SetWindowExtents(0, 0, Width, Height);
                        mWindowH.HobjectToHimage(image);

                        // ROI信息已在主线程获取并传入，直接使用
                        if (roiCopy != null && roiCopy.Count > 0)
                        {
                            ShowHRoiForSave(mWindowH, roiCopy, imageFullPath);
                        }
                        else
                        {
                            HOperatorSet.DumpWindow(hWindow, "jpeg", imageFullPath);
                        }
                        saveStopwatch.Stop();
                        Logger.AddLog($"保存截图完成，路径: {imageFullPath}，耗时: {saveStopwatch.ElapsedMilliseconds}ms", eMsgType.Info);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.AddLog($"保存截图失败: {ex.Message}", eMsgType.Error);
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
                        VMHWindowControl mWindowH = VisionView.Ins.GetImageBox(99);
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

        ///// <summary>
        ///// 旧版全量删除方法（已弃用，改用 RemoveOldImagesBatched 分批删除）
        ///// </summary>
        //private void RemoveOldImages(string baseDirectoryPath, int saveDays)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(baseDirectoryPath) || saveDays <= 0 || !Directory.Exists(baseDirectoryPath))
        //            return;
        //
        //        DateTime currentTime = DateTime.Now;
        //        var cutoffDate = currentTime.AddDays(-saveDays);
        //
        //        // 删除baseDirectoryPath下的所有日期文件夹中的旧文件
        //        foreach (var directory in Directory.GetDirectories(baseDirectoryPath))
        //        {
        //            RemoveFilesFromDirectory(directory, cutoffDate);
        //
        //            // 如果日期文件夹为空，删除空文件夹
        //            if (Directory.GetFiles(directory).Length == 0 &&
        //                Directory.GetDirectories(directory).Length == 0)
        //            {
        //                try
        //                {
        //                    Directory.Delete(directory);
        //                }
        //                catch (Exception ex)
        //                {
        //                    Logger.AddLog($"删除空目录失败: {directory}, 错误: {ex.Message}", eMsgType.Warn);
        //                }
        //            }
        //        }
        //
        //        Logger.AddLog($"在文件夹 {Path.GetFileName(baseDirectoryPath)} 中自动删除超过{saveDays}天的图片完成", eMsgType.Info);
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.AddLog($"自动删除图片时发生错误: {ex.Message}", eMsgType.Warn);
        //    }
        //}

        // ========== 原实现（已弃用，保留注释参考）==========

        ///// <summary>
        ///// 异步执行删除旧图片（带频率限制，避免频繁遍历）
        ///// 同一个路径每24小时最多执行一次
        ///// </summary>
        //private void TryRemoveOldImagesAsync(string baseDirectoryPath, int saveDays)
        //{
        //    Task.Run(() =>
        //    {
        //        lock (_deleteLock)
        //        {
        //            // 检查该路径是否在24小时内执行过删除
        //            if (_lastDeleteTimes.TryGetValue(baseDirectoryPath, out DateTime lastTime))
        //            {
        //                if ((DateTime.Now - lastTime).TotalHours < 0.01)
        //                {
        //                    return;
        //                }
        //            }
        //
        //            // 更新该路径的最后删除时间
        //            _lastDeleteTimes[baseDirectoryPath] = DateTime.Now;
        //
        //            RemoveOldImages(baseDirectoryPath, saveDays);
        //        }
        //    });
        //}

        ///// <summary>
        ///// 删除目录下的过期文件
        ///// </summary>
        //private void RemoveFilesFromDirectory(string directoryPath, DateTime cutoffDate)
        //{
        //    try
        //    {
        //        foreach (var file in Directory.GetFiles(directoryPath))
        //        {
        //            try
        //            {
        //                var fileInfo = new FileInfo(file);
        //                if (fileInfo.CreationTime < cutoffDate || fileInfo.LastWriteTime < cutoffDate)
        //                {
        //                    fileInfo.Attributes = FileAttributes.Normal;
        //                    File.Delete(file);
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.AddLog($"删除文件失败: {file}, 错误: {ex.Message}", eMsgType.Warn);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.AddLog($"访问目录失败: {directoryPath}, 错误: {ex.Message}", eMsgType.Warn);
        //    }
        //}

        ///// <summary>
        ///// 旧版全量删除方法（已弃用，改用 RemoveOldImagesBatched 分批删除）
        ///// </summary>
        //private void RemoveOldImages(string baseDirectoryPath, int saveDays)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(baseDirectoryPath) || saveDays <= 0 || !Directory.Exists(baseDirectoryPath))
        //            return;
        //
        //        DateTime currentTime = DateTime.Now;
        //        var cutoffDate = currentTime.AddDays(-saveDays);
        //
        //        // 删除baseDirectoryPath下的所有日期文件夹中的旧文件
        //        foreach (var directory in Directory.GetDirectories(baseDirectoryPath))
        //        {
        //            RemoveFilesFromDirectory(directory, cutoffDate);
        //
        //            // 如果日期文件夹为空，删除空文件夹
        //            if (Directory.GetFiles(directory).Length == 0 &&
        //                Directory.GetDirectories(directory).Length == 0)
        //            {
        //                try
        //                {
        //                    Directory.Delete(directory);
        //                }
        //                catch (Exception ex)
        //                {
        //                    Logger.AddLog($"删除空目录失败: {directory}, 错误: {ex.Message}", eMsgType.Warn);
        //                }
        //            }
        //        }
        //
        //        Logger.AddLog($"在文件夹 {Path.GetFileName(baseDirectoryPath)} 中自动删除超过{saveDays}天的图片完成", eMsgType.Info);
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.AddLog($"自动删除图片时发生错误: {ex.Message}", eMsgType.Warn);
        //    }
        //}

        // ========== 新实现：改进的分批删除（实例级别状态） ==========

        /// <summary>
        /// 待清理路径的队列（实例级别，避免多实例共享导致状态混乱）
        /// 注意：由于类会被反序列化，不能使用 readonly
        /// </summary>
        private HashSet<string> _pendingCleanupPaths = new HashSet<string>();

        /// <summary>
        /// 是否有删除任务正在执行（实例级别）
        /// </summary>
        private volatile bool _isCleanupRunning = false;

        /// <summary>
        /// 上次清理时间记录（实例级别，每个实例独立控制清理频率）
        /// 注意：由于类会被反序列化，不能使用 readonly
        /// </summary>
        private Dictionary<string, DateTime> _lastCleanupTimes = new Dictionary<string, DateTime>();

        /// <summary>
        /// 保护本实例清理状态的锁
        /// 注意：由于类会被反序列化，不能使用 readonly
        /// </summary>
        private object _instanceCleanupLock = new object();

        /// <summary>
        /// 异步执行删除旧图片（改进版：分批执行 + 频率控制 + 立即返回）
        /// </summary>
        /// <param name="baseDirectoryPath">待清理的根目录</param>
        /// <param name="saveDays">保留天数</param>
        private void TryRemoveOldImagesAsync(string baseDirectoryPath, int saveDays)
        {
            // 防御性检查：确保所有字段已初始化（反序列化后可能为null）
            if (_pendingCleanupPaths == null)
                _pendingCleanupPaths = new HashSet<string>();
            if (_lastCleanupTimes == null)
                _lastCleanupTimes = new Dictionary<string, DateTime>();
            if (_instanceCleanupLock == null)
                _instanceCleanupLock = new object();

            // 将待清理路径加入队列（HashSet自动去重）
            lock (_pendingCleanupPaths)
            {
                _pendingCleanupPaths.Add(baseDirectoryPath);
            }

            // 启动后台清理任务（如果当前没有运行的话）
            Task.Run(() => ProcessCleanupQueueAsync(saveDays));
        }

        /// <summary>
        /// 处理清理队列（分批执行，避免阻塞）
        /// </summary>
        private void ProcessCleanupQueueAsync(int saveDays)
        {
            // 防御性检查：确保所有字段已初始化（反序列化后可能为null）
            if (_pendingCleanupPaths == null)
                _pendingCleanupPaths = new HashSet<string>();
            if (_lastCleanupTimes == null)
                _lastCleanupTimes = new Dictionary<string, DateTime>();
            if (_instanceCleanupLock == null)
                _instanceCleanupLock = new object();

            // 使用 lock 确保只有一个清理任务在运行
            lock (_deleteLock)
            {
                if (_isCleanupRunning)
                    return;
                _isCleanupRunning = true;
            }

            try
            {
                List<string> pathsToClean;
                lock (_pendingCleanupPaths)
                {
                    if (_pendingCleanupPaths.Count == 0)
                        return;
                    pathsToClean = new List<string>(_pendingCleanupPaths);
                    _pendingCleanupPaths.Clear();
                }

                foreach (var baseDirectoryPath in pathsToClean)
                {
                    // 线程安全地检查清理时间
                    bool shouldCleanup = false;
                    lock (_instanceCleanupLock)
                    {
                        if (_lastCleanupTimes.TryGetValue(baseDirectoryPath, out DateTime lastTime))
                        {
                            if ((DateTime.Now - lastTime).TotalHours < 24)
                            {
                                // 不满足清理条件，保留在队列中下次再试
                                lock (_pendingCleanupPaths)
                                {
                                    _pendingCleanupPaths.Add(baseDirectoryPath);
                                }
                                continue;
                            }
                        }
                        shouldCleanup = true;
                    }

                    if (shouldCleanup)
                    {
                        // 执行分批删除
                        RemoveOldImagesBatched(baseDirectoryPath, saveDays);

                        // 线程安全地更新最后清理时间
                        lock (_instanceCleanupLock)
                        {
                            _lastCleanupTimes[baseDirectoryPath] = DateTime.Now;
                        }
                    }
                }
            }
            finally
            {
                _isCleanupRunning = false;
            }
        }

        /// <summary>
        /// 分批删除旧图片（每次只处理一个日期文件夹，避免长时间阻塞）
        /// </summary>
        /// <param name="baseDirectoryPath">根目录路径</param>
        /// <param name="saveDays">保留天数</param>
        private void RemoveOldImagesBatched(string baseDirectoryPath, int saveDays)
        {
            System.Diagnostics.Stopwatch deleteStopwatch = new System.Diagnostics.Stopwatch();
            try
            {
                if (string.IsNullOrEmpty(baseDirectoryPath) || saveDays <= 0 || !Directory.Exists(baseDirectoryPath))
                    return;

                deleteStopwatch.Start();
                DateTime currentTime = DateTime.Now;
                DateTime cutoffDate = currentTime.AddDays(-saveDays);

                // 获取所有日期文件夹
                string[] dateDirectories;
                try
                {
                    dateDirectories = Directory.GetDirectories(baseDirectoryPath);
                }
                catch (Exception ex)
                {
                    Logger.AddLog($"访问目录失败: {baseDirectoryPath}, 错误: {ex.Message}", eMsgType.Warn);
                    return;
                }

                int deletedCount = 0;
                foreach (var dateDir in dateDirectories)
                {
                    // 解析日期文件夹名称，判断是否过期
                    string dirName = Path.GetFileName(dateDir);
                    if (IsDateFolderExpired(dirName, cutoffDate))
                    {
                        // 删除该日期文件夹下的所有过期文件
                        int count = DeleteExpiredFilesInDirectory(dateDir, cutoffDate);
                        deletedCount += count;

                        // 删除空文件夹
                        DeleteEmptyDirectory(dateDir);
                    }

                    // 每处理完一个日期文件夹，让出CPU，避免长时间阻塞
                    Thread.Sleep(5);
                }
                deleteStopwatch.Stop();

                Logger.AddLog($"自动清理完成，路径: {baseDirectoryPath}，删除文件数: {deletedCount}，耗时: {deleteStopwatch.ElapsedMilliseconds}ms", eMsgType.Info);
            }
            catch (Exception ex)
            {
                Logger.AddLog($"分批删除图片时发生错误: {ex.Message}", eMsgType.Warn);
            }
        }

        /// <summary>
        /// 判断日期文件夹是否过期（根据文件夹名称判断，避免遍历文件）
        /// </summary>
        /// <param name="folderName">文件夹名称，期望格式 yyyyMMdd</param>
        /// <param name="cutoffDate">截止日期</param>
        /// <returns>是否过期</returns>
        private bool IsDateFolderExpired(string folderName, DateTime cutoffDate)
        {
            // 跳过不符合日期格式的文件夹
            if (string.IsNullOrEmpty(folderName) || folderName.Length != 8)
                return false;

            // 尝试解析日期
            if (DateTime.TryParseExact(folderName, "yyyyMMdd", null, DateTimeStyles.None, out DateTime folderDate))
            {
                return folderDate < cutoffDate;
            }

            return false;
        }

        /// <summary>
        /// 删除目录下的过期文件（分批执行，每100个文件让出一次CPU）
        /// </summary>
        /// <returns>实际删除的文件数量</returns>
        private int DeleteExpiredFilesInDirectory(string directoryPath, DateTime cutoffDate)
        {
            int deletedCount = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(directoryPath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate || fileInfo.LastWriteTime < cutoffDate)
                        {
                            fileInfo.Attributes = FileAttributes.Normal;
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.AddLog($"删除文件失败: {file}, 错误: {ex.Message}", eMsgType.Warn);
                    }

                    // 每处理100个文件让出一次CPU
                    if (deletedCount % 50 == 0)
                    {
                        Thread.Sleep(5);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"访问目录失败: {directoryPath}, 错误: {ex.Message}", eMsgType.Warn);
            }
            return deletedCount;
        }

        /// <summary>
        /// 删除空目录（如果目录下没有文件也没有子目录）
        /// </summary>
        private void DeleteEmptyDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    if (Directory.GetFiles(directoryPath).Length == 0 &&
                        Directory.GetDirectories(directoryPath).Length == 0)
                    {
                        Directory.Delete(directoryPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"删除空目录失败: {directoryPath}, 错误: {ex.Message}", eMsgType.Warn);
            }
        }

        public void ShowHRoiForSave(VMHWindowControl windos, List<HRoi> hRois, string ImageNames)
        {
            if (windos != null)
            {
                windos.DispText.Clear();
                List<HRoi> Temp = new List<HRoi>(hRois);

                // 计算文字大小补偿系数
                // CalcDisplaySize 内部做 originalSize / (ImageWidth / ViewPort.Width)
                // 为使全分辨率截图中文字保持 originalSize，提前乘以该系数
                double textScale = 1.0;
                if (windos.hv_imageWidth > 0 && windos.hControl.Width > 0)
                {
                    textScale = (double)windos.hv_imageWidth / windos.hControl.Width;
                }

                // 保存原始值，用于 DumpWindow 后还原，避免影响显示窗口
                Dictionary<HText, int> savedOriginalSizes = new Dictionary<HText, int>();

                foreach (HRoi roi in Temp)
                {
                    if (roi.roiType == HRoiType.文字显示)
                    {
                        HText roiText = (HText)roi;
                        if (roiText.originalSize > 0 && textScale > 1.0)
                        {
                            savedOriginalSizes[roiText] = roiText.originalSize;
                            roiText.originalSize = (int)(roiText.originalSize * textScale);
                        }
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

                // 还原 originalSize，避免影响后续显示
                foreach (var kv in savedOriginalSizes)
                {
                    kv.Key.originalSize = kv.Value;
                }
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
                            // ========== 改进版：立即返回，不等待删除完成 ==========
                            // 先立即提示用户删除已开始
                            MessageBox.Show($"已在文件夹 {linkFolderName} 中启动后台删除，请稍候...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                            // 异步执行删除，在后台线程执行
                            Task.Run(() =>
                            {
                                try
                                {
                                    lock (_deleteLock)
                                    {
                                        RemoveOldImagesBatched(directoryPath, SaveDay);
                                    }
                                    Logger.AddLog($"手动删除完成，路径: {directoryPath}", eMsgType.Info);
                                }
                                catch (Exception ex)
                                {
                                    Logger.AddLog($"手动删除失败: {ex.Message}", eMsgType.Error);
                                }
                            });
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