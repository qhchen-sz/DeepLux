using EventMgrLib;
using HalconDotNet;
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
using Newtonsoft.Json.Linq;
using Plugin.ImageMerge.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;

namespace Plugin.ImageMerge.ViewModels
{
    public enum eLinkCommand
    {
        RowImageLink,
    }

    [Category("图像处理")]
    [DisplayName("图像合并")]
    [ModuleImageName("ImageMerge")]
    [Serializable]
    public class ImageMergeViewModel : ModuleBase
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr length);

        private int GetPixelBytes(string type)
        {
            switch (type.ToLower())
            {
                case "byte": return 1;
                case "uint2": return 2;
                case "int1": return 1;
                case "int2": return 2;
                case "int4": return 4;
                case "real": return 4;
                case "direction": return 1;
                case "complex": return 8;
                default: return 1;
            }
        }

        public ImageMergeViewModel()
        {
            _dataList.CollectionChanged += DataList_CollectionChanged;
        }

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            HImage canvas = null;

            try
            {
                ClearRoiAndText();

                List<ImageMergeItem> enabledItems = DataList
                    .Where(item => item != null && item.IsEnabled && item.InputImageLink != null && !string.IsNullOrWhiteSpace(item.InputImageLink.Text))
                    .ToList();

                if (enabledItems.Count == 0)
                {
                    Logger.AddLog($"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，可用待合并图像为空。", eMsgType.Warn);
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                _cropImageList = new List<RImage>();
                _cropRegionList = new List<HRegion>();

                // 第一遍：计算画布尺寸并收集图像数据
                int canvasWidth = 0;
                int canvasHeight = 0;
                string pixelType = "byte";
                var imageDataList = new List<(RImage image, ImageMergeItem item, int imgWidth, int imgHeight)>();

                foreach (ImageMergeItem item in enabledItems)
                {
                    RImage inputImage = GetRowImage(item);
                    if (inputImage == null || !inputImage.IsInitialized())
                    {
                        throw new InvalidOperationException($"图像引用[{item.InputImageLink?.Text}]未找到或未初始化。");
                    }

                    inputImage.GetImageSize(out int imgWidth, out int imgHeight);

                    int rightEdge = (int)Math.Round(item.Col1) + imgWidth;
                    int bottomEdge = (int)Math.Round(item.Row1) + imgHeight;

                    canvasWidth = Math.Max(canvasWidth, rightEdge);
                    canvasHeight = Math.Max(canvasHeight, bottomEdge);

                    imageDataList.Add((inputImage, item, imgWidth, imgHeight));

                    if (imageDataList.Count == 1)
                    {
                        HTuple typeTuple = inputImage.GetImageType();
                        pixelType = typeTuple.Length > 0 ? typeTuple.S : "byte";
                    }
                }

                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    throw new InvalidOperationException("画布尺寸无效。");
                }

                // 创建全0背景画布（支持多通道）
                int channelCount = imageDataList[0].image.CountChannels();
                int pixelBytes = GetPixelBytes(pixelType);
                HImage[] channelCanvases = new HImage[channelCount];
                IntPtr[] channelCanvasPtrs = new IntPtr[channelCount];
                for (int ch = 0; ch < channelCount; ch++)
                {
                    channelCanvases[ch] = new HImage();
                    channelCanvases[ch].GenImageConst(pixelType, canvasWidth, canvasHeight);
                    channelCanvasPtrs[ch] = channelCanvases[ch].GetImagePointer1(out HTuple _, out HTuple _, out HTuple _);
                }

                // 第二遍：按偏移坐标直接拼接整张图到画布
                foreach (var (inputImage, item, imgWidth, imgHeight) in imageDataList)
                {
                    int rowOffset = (int)Math.Round(item.Row1);
                    int colOffset = (int)Math.Round(item.Col1);

                    HRegion rect = CreateRegion(item, imgWidth, imgHeight);
                    _cropRegionList.Add(rect);

                    _cropImageList.Add(new RImage(inputImage.CopyObj(1, -1)));

                    int rowBytes = imgWidth * pixelBytes;
                    int canvasRowBytes = canvasWidth * pixelBytes;

                    HImage hInput = new HImage(inputImage);

                    // 逐通道 memcpy 到画布的 (colOffset, rowOffset) 位置
                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        HImage chImage = hInput.AccessChannel(ch + 1);
                        IntPtr srcPtr = chImage.GetImagePointer1(out HTuple _, out HTuple _, out HTuple _);

                        for (int r = 0; r < imgHeight; r++)
                        {
                            IntPtr src = new IntPtr(srcPtr.ToInt64() + r * rowBytes);
                            IntPtr dst = new IntPtr(channelCanvasPtrs[ch].ToInt64() + (rowOffset + r) * canvasRowBytes + colOffset * pixelBytes);
                            CopyMemory(dst, src, (UIntPtr)rowBytes);
                        }
                        chImage.Dispose();
                    }

                    hInput.Dispose();
                }

                // 合成多通道结果
                if (channelCount == 1)
                {
                    canvas = channelCanvases[0];
                }
                else if (channelCount == 2)
                {
                    HOperatorSet.Compose2(channelCanvases[0], channelCanvases[1], out HObject composed);
                    canvas = new HImage(composed);
                }
                else if (channelCount == 3)
                {
                    HOperatorSet.Compose3(channelCanvases[0], channelCanvases[1], channelCanvases[2], out HObject composed);
                    canvas = new HImage(composed);
                }
                else
                {
                    // >3通道：用 AppendChannel 逐个追加通道
                    HImage temp = channelCanvases[0];
                    for (int i = 1; i < channelCount; i++)
                    {
                        HImage appended = temp.AppendChannel(channelCanvases[i]);
                        if (temp != channelCanvases[0])
                            temp.Dispose();
                        temp = appended;
                    }
                    canvas = temp;
                }

                // 清理不再需要的通道画布
                for (int i = 0; i < channelCount; i++)
                {
                    if (channelCanvases[i] != canvas)
                        channelCanvases[i]?.Dispose();
                }

                ResultImage = new RImage(canvas.CopyObj(1, -1));
                ResultImage.DispViewID = DispViewID;
                DispImage = ResultImage;

                RefreshPreview();
                ChangeModuleRunStatus(eRunStatus.OK);
                return true;
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                Logger.GetExceptionMsg(ex);
                return false;
            }
            finally
            {
                canvas?.Dispose();
            }
        }

        public override void AddOutputParams()
        {
            Prj?.ClearOutputParam(ModuleParam);
            AddOutputParam("合并图像", "HImage", ResultImage);
            AddOutputParam("裁剪图像列表", "HImage[]", _cropImageList);
            AddOutputParam("裁剪区域列表", "HRegion[]", _cropRegionList);
            AddOutputParam("状态", "bool", ModuleParam.Status == eRunStatus.OK);
            AddOutputParam("时间", "int", ModuleParam.ElapsedTime);
        }

        public override void Loaded()
        {
            base.Loaded();
            EnsureCollectionEvents();

            var view = ModuleView as ImageMergeView;
            if (view == null)
            {
                return;
            }

            ClosedView = true;
            if (view.mWindowH == null)
            {
                view.mWindowH = new VMHWindowControl();
                view.winFormHost.Child = view.mWindowH;
            }

            LoadPreviewImageFromFirstRow();
        }

        public void CleanupMouseEvents()
        {
        }

        private void LoadPreviewImageFromFirstRow()
        {
            ImageMergeItem firstItem = DataList.FirstOrDefault(item =>
                item != null &&
                item.IsEnabled &&
                item.InputImageLink != null &&
                !string.IsNullOrWhiteSpace(item.InputImageLink.Text));

            if (firstItem == null)
            {
                DispImage = null;
                RefreshPreview();
                return;
            }

            DispImage = GetRowImage(firstItem);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var view = ModuleView as ImageMergeView;
            if (view?.mWindowH == null)
            {
                return;
            }

            if (DispImage != null && DispImage.IsInitialized())
            {
                view.mWindowH.HobjectToHimage(DispImage);
            }
            else
            {
                view.mWindowH.ClearWindow();
            }
        }

        private RImage GetRowImage(ImageMergeItem item)
        {
            object value = GetLinkValue(item.InputImageLink);
            if (value is RImage rImage && rImage.IsInitialized())
            {
                return rImage;
            }

            if (value is HImage hImage && hImage.IsInitialized())
            {
                return new RImage(hImage);
            }

            if (value is HObject hObject && hObject.IsInitialized())
            {
                return new RImage(hObject);
            }

            return null;
        }

        private HRegion CreateRegion(ImageMergeItem item, int imgWidth, int imgHeight)
        {
            HRegion region = new HRegion();
            int rowOffset = (int)Math.Round(item.Row1);
            int colOffset = (int)Math.Round(item.Col1);
            region.GenRectangle1(
                (double)rowOffset,
                (double)colOffset,
                (double)(rowOffset + imgHeight - 1),
                (double)(colOffset + imgWidth - 1)
            );
            return region;
        }

        private void EnsureCollectionEvents()
        {
            _dataList.CollectionChanged -= DataList_CollectionChanged;
            _dataList.CollectionChanged += DataList_CollectionChanged;

            foreach (ImageMergeItem item in _dataList)
            {
                item.PropertyChanged -= DataItem_PropertyChanged;
                item.PropertyChanged += DataItem_PropertyChanged;
            }
        }

        private void DataList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ImageMergeItem item in e.NewItems)
                {
                    item.PropertyChanged -= DataItem_PropertyChanged;
                    item.PropertyChanged += DataItem_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (ImageMergeItem item in e.OldItems)
                {
                    item.PropertyChanged -= DataItem_PropertyChanged;
                }
            }

            RenumberItems();
            LoadPreviewImageFromFirstRow();
        }

        private void DataItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageMergeItem.InputImageLink) || e.PropertyName == nameof(ImageMergeItem.IsEnabled))
            {
                LoadPreviewImageFromFirstRow();
            }
        }

        private void RenumberItems()
        {
            for (int i = 0; i < DataList.Count; i++)
            {
                DataList[i].Index = i + 1;
            }
        }

        private void AddRow()
        {
            DataList.Add(new ImageMergeItem
            {
                Index = DataList.Count + 1,
                Row1 = 0,
                Col1 = 0,
            });
        }

        private void RemoveSelectedRow()
        {
            if (SelectedIndex < 0 || SelectedIndex >= DataList.Count)
            {
                return;
            }

            DataList.RemoveAt(SelectedIndex);
            if (SelectedIndex >= DataList.Count)
            {
                SelectedIndex = DataList.Count - 1;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            string[] parts = obj.SendName.Split(',');
            if (parts.Length < 2)
            {
                return;
            }

            if (parts[1] == nameof(eLinkCommand.RowImageLink) && PendingRowLinkIndex >= 0 && PendingRowLinkIndex < DataList.Count)
            {
                DataList[PendingRowLinkIndex].InputImageLink.Text = obj.LinkName;
            }
        }

        private ObservableCollection<ImageMergeItem> _dataList = new ObservableCollection<ImageMergeItem>();
        public ObservableCollection<ImageMergeItem> DataList
        {
            get { return _dataList; }
            set
            {
                if (_dataList != null)
                {
                    _dataList.CollectionChanged -= DataList_CollectionChanged;
                    foreach (ImageMergeItem item in _dataList)
                    {
                        item.PropertyChanged -= DataItem_PropertyChanged;
                    }
                }

                Set(ref _dataList, value);
                if (_dataList == null)
                {
                    _dataList = new ObservableCollection<ImageMergeItem>();
                }

                EnsureCollectionEvents();
                RenumberItems();
            }
        }

        private ImageMergeItem _selectedData;
        public ImageMergeItem SelectedData
        {
            get { return _selectedData; }
            set { Set(ref _selectedData, value); }
        }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { Set(ref _selectedIndex, value); }
        }

        private int _pendingRowLinkIndex = -1;
        public int PendingRowLinkIndex
        {
            get { return _pendingRowLinkIndex; }
            set { Set(ref _pendingRowLinkIndex, value); }
        }

        [NonSerialized]
        private RImage _resultImage;
        public RImage ResultImage
        {
            get { return _resultImage; }
            set { Set(ref _resultImage, value); }
        }

        [NonSerialized]
        private List<RImage> _cropImageList = new List<RImage>();

        [NonSerialized]
        private List<HRegion> _cropRegionList = new List<HRegion>();

        [NonSerialized]
        private CommandBase _addDataCommand;
        public CommandBase AddDataCommand
        {
            get
            {
                if (_addDataCommand == null)
                {
                    _addDataCommand = new CommandBase(obj => AddRow());
                }
                return _addDataCommand;
            }
        }

        [NonSerialized]
        private CommandBase _delDataCommand;
        public CommandBase DelDataCommand
        {
            get
            {
                if (_delDataCommand == null)
                {
                    _delDataCommand = new CommandBase(obj => RemoveSelectedRow());
                }
                return _delDataCommand;
            }
        }

        [NonSerialized]
        private CommandBase _executeCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_executeCommand == null)
                {
                    _executeCommand = new CommandBase(obj => ExeModule());
                }
                return _executeCommand;
            }
        }

        [NonSerialized]
        private CommandBase _confirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_confirmCommand == null)
                {
                    _confirmCommand = new CommandBase(obj =>
                    {
                        CleanupMouseEvents();
                        (ModuleView as ImageMergeView)?.Close();
                    });
                }
                return _confirmCommand;
            }
        }

        [NonSerialized]
        private CommandBase _linkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_linkCommand == null)
                {
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _linkCommand = new CommandBase(obj =>
                    {
                        CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                        if (obj != null && int.TryParse(obj.ToString(), out int rowIndex))
                        {
                            PendingRowLinkIndex = rowIndex - 1;
                            EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},{nameof(eLinkCommand.RowImageLink)}");
                        }
                    });
                }
                return _linkCommand;
            }
        }

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            JArray itemArray = new JArray();
            foreach (ImageMergeItem item in DataList)
            {
                JObject itemObj = new JObject
                {
                    ["Index"] = item.Index,
                    ["InputImageLink"] = item.InputImageLink?.Text ?? "",
                    ["Row1"] = item.Row1,
                    ["Col1"] = item.Col1,
                    ["IsEnabled"] = item.IsEnabled
                };
                itemArray.Add(itemObj);
            }

            obj["DataList"] = itemArray;
            return obj.ToString();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            base.HVDeserialize(json);
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["DataList"] != null)
                {
                    DataList.Clear();
                    foreach (JObject itemObj in (JArray)obj["DataList"])
                    {
                        DataList.Add(new ImageMergeItem
                        {
                            Index = itemObj["Index"]?.Value<int>() ?? DataList.Count + 1,
                            InputImageLink = new LinkVarModel
                            {
                                Text = itemObj["InputImageLink"]?.ToString() ?? string.Empty
                            },
                            Row1 = itemObj["Row1"]?.Value<double>() ?? 0,
                            Col1 = itemObj["Col1"]?.Value<double>() ?? 0,
                            IsEnabled = itemObj["IsEnabled"]?.Value<bool>() ?? true,
                        });
                    }
                }

                EnsureCollectionEvents();
                RenumberItems();
                LoadPreviewImageFromFirstRow();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"ImageMergeViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
    }

    [Serializable]
    public class ImageMergeItem : NotifyPropertyBase
    {
        private int _index;
        public int Index
        {
            get { return _index; }
            set { Set(ref _index, value); }
        }

        private LinkVarModel _inputImageLink = new LinkVarModel();
        public LinkVarModel InputImageLink
        {
            get { return _inputImageLink; }
            set
            {
                if (_inputImageLink != null)
                {
                    _inputImageLink.PropertyChanged -= InputImageLink_PropertyChanged;
                }

                Set(ref _inputImageLink, value);

                if (_inputImageLink == null)
                {
                    _inputImageLink = new LinkVarModel();
                }

                _inputImageLink.PropertyChanged -= InputImageLink_PropertyChanged;
                _inputImageLink.PropertyChanged += InputImageLink_PropertyChanged;
            }
        }

        private void InputImageLink_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(InputImageLink));
        }

        private double _row1;
        public double Row1
        {
            get { return _row1; }
            set { Set(ref _row1, value); }
        }

        private double _col1;
        public double Col1
        {
            get { return _col1; }
            set { Set(ref _col1, value); }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { Set(ref _isEnabled, value); }
        }
    }
}
