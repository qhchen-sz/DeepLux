using EventMgrLib;
using HalconDotNet;
using Plugin.CropImage.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using VM.Halcon;
using VM.Halcon.Config;
using VM.Halcon.Model;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.Services;
using HV.Models;
using HV.ViewModels;
using HV.Views.Dock;
using Newtonsoft.Json.Linq;

namespace Plugin.CropImage.ViewModels
{
    public enum eLinkCommand
    {
        InputImageLink,
        CenterXLink,
        CenterYLink,
        Length1Link,
        Length2Link,
        AngleLink,
    }

    public enum eROISource
    {
        手动输入,
        链接数组,
    }

    [Category("图像处理")]
    [DisplayName("裁剪图片")]
    [ModuleImageName("CropImage")]
    [Serializable]
    public class CropImageViewModel : ModuleBase
    {
        [NonSerialized]
        private List<RImage> _outImageList = new List<RImage>();
        [NonSerialized]
        private List<HRegion> _outRegionList = new List<HRegion>();

        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();

        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                ClearRoiAndText();
                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，未链接图像源！",
                        eMsgType.Warn
                    );
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                GetDispImage(InputImageLinkText);

                if (DispImage == null || !DispImage.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                _outImageList = new List<RImage>();
                _outRegionList = new List<HRegion>();

                if (SearchRegionSource == eROISource.手动输入)
                {
                    foreach (var item in DataList)
                    {
                        HRegion rect = new HRegion();
                        rect.GenRectangle2(item.Y, item.X, item.Deg / 180.0 * Math.PI, item.L1, item.L2);
                        _outRegionList.Add(rect);

                        if (IsOutputCropImage)
                        {
                            RImage rImage = new RImage(DispImage);
                            HImage cropped = rImage.CropRectangle2(item.Y, item.X, item.Deg / 180.0 * Math.PI, item.L1, item.L2, "true", "constant");
                            _outImageList.Add(new RImage(cropped));
                        }
                    }
                }
                else
                {
                    List<double> centerXList = GetLinkArray(CenterXLink);
                    List<double> centerYList = GetLinkArray(CenterYLink);
                    List<double> length1List = GetLinkArray(Length1Link);
                    List<double> length2List = GetLinkArray(Length2Link);
                    List<double> angleList = GetLinkArray(AngleLink);

                    int count = new[] { centerXList.Count, centerYList.Count, length1List.Count, length2List.Count, angleList.Count }.Min();

                    for (int i = 0; i < count; i++)
                    {
                        double x = centerXList[i];
                        double y = centerYList[i];
                        double l1 = length1List[i];
                        double l2 = length2List[i];
                        double deg = angleList[i];

                        HRegion rect = new HRegion();
                        rect.GenRectangle2(y, x, deg / 180.0 * Math.PI, l1, l2);
                        _outRegionList.Add(rect);

                        if (IsOutputCropImage)
                        {
                            RImage rImage = new RImage(DispImage);
                            HImage cropped = rImage.CropRectangle2(y, x, deg / 180.0 * Math.PI, l1, l2, "true", "constant");
                            _outImageList.Add(new RImage(cropped));
                        }
                    }
                }

                ShowHRoiList();
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
            base.AddOutputParams();
            for (int i = 0; _outImageList != null && i < _outImageList.Count; i++)
            {
                AddOutputParam($"裁剪图{i + 1}", "HImage", _outImageList[i]);
            }
            for (int i = 0; _outRegionList != null && i < _outRegionList.Count; i++)
            {
                AddOutputParam($"ROI区域{i + 1}", "HRegion", _outRegionList[i]);
            }
        }

        #region Prop
        private string _inputImageLinkText;
        public string InputImageLinkText
        {
            get { return _inputImageLinkText; }
            set
            {
                _inputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
            }
        }

        private bool _isOutputCropImage = true;
        public bool IsOutputCropImage
        {
            get { return _isOutputCropImage; }
            set { Set(ref _isOutputCropImage, value); }
        }

        private eROISource _searchRegionSource = eROISource.手动输入;
        public eROISource SearchRegionSource
        {
            get { return _searchRegionSource; }
            set { Set(ref _searchRegionSource, value); }
        }

        private ObservableCollection<CropImageData> _dataList = new ObservableCollection<CropImageData>();
        public ObservableCollection<CropImageData> DataList
        {
            get { return _dataList; }
            set
            {
                if (_dataList != null)
                {
                    _dataList.CollectionChanged -= DataList_CollectionChanged;
                    foreach (var item in _dataList)
                        item.PropertyChanged -= DataItem_PropertyChanged;
                }
                Set(ref _dataList, value);
                if (_dataList != null)
                {
                    _dataList.CollectionChanged += DataList_CollectionChanged;
                    foreach (var item in _dataList)
                        item.PropertyChanged += DataItem_PropertyChanged;
                }
            }
        }

        private CropImageData _selectedData;
        public CropImageData SelectedData
        {
            get { return _selectedData; }
            set { _selectedData = value; RaisePropertyChanged(); }
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { Set(ref _selectedIndex, value); }
        }

        private LinkVarModel _centerXLink = new LinkVarModel();
        public LinkVarModel CenterXLink
        {
            get { return _centerXLink; }
            set { _centerXLink = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _centerYLink = new LinkVarModel();
        public LinkVarModel CenterYLink
        {
            get { return _centerYLink; }
            set { _centerYLink = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _length1Link = new LinkVarModel();
        public LinkVarModel Length1Link
        {
            get { return _length1Link; }
            set { _length1Link = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _length2Link = new LinkVarModel();
        public LinkVarModel Length2Link
        {
            get { return _length2Link; }
            set { _length2Link = value; RaisePropertyChanged(); }
        }

        private LinkVarModel _angleLink = new LinkVarModel();
        public LinkVarModel AngleLink
        {
            get { return _angleLink; }
            set { _angleLink = value; RaisePropertyChanged(); }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();

            _dataList.CollectionChanged -= DataList_CollectionChanged;
            _dataList.CollectionChanged += DataList_CollectionChanged;
            foreach (var item in _dataList)
            {
                item.PropertyChanged -= DataItem_PropertyChanged;
                item.PropertyChanged += DataItem_PropertyChanged;
            }

            var view = ModuleView as CropImageView;
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
                    if (InputImageLinkText == null)
                        return;
                }

                GetDispImage(InputImageLinkText);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.HobjectToHimage(DispImage);
                    view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                    view.mWindowH.hControl.MouseDown += HControl_MouseDown;
                    view.mWindowH.hControl.MouseWheel += HControl_MouseWheel;
                }
                ShowHRoiList();
            }
        }

        public override void SetDefaultLink()
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
            var moduls = VarLinkViewModel.Ins.Modules.LastOrDefault();
            if (moduls == null || moduls.VarModels.Count == 0)
            {
                return;
            }
            InputImageLinkText = $"&{moduls.DisplayName}.{moduls.VarModels[0].Name}";
        }

        [NonSerialized] private CommandBase _executeCommand;
        public CommandBase ExecuteCommand
        {
            get
            {
                if (_executeCommand == null)
                    _executeCommand = new CommandBase((obj) => ExeModule());
                return _executeCommand;
            }
        }

        [NonSerialized] private CommandBase _confirmCommand;
        public CommandBase ConfirmCommand
        {
            get
            {
                if (_confirmCommand == null)
                    _confirmCommand = new CommandBase((obj) =>
                    {
                        CleanupMouseEvents();
                        var view = this.ModuleView as CropImageView;
                        if (view != null) view.Close();
                    });
                return _confirmCommand;
            }
        }

        [NonSerialized] private CommandBase _addDataCommand;
        public CommandBase AddDataCommand
        {
            get
            {
                if (_addDataCommand == null)
                    _addDataCommand = new CommandBase((obj) => AddData());
                return _addDataCommand;
            }
        }

        [NonSerialized] private CommandBase _delDataCommand;
        public CommandBase DelDataCommand
        {
            get
            {
                if (_delDataCommand == null)
                    _delDataCommand = new CommandBase((obj) =>
                    {
                        if (SelectedIndex >= 0 && SelectedIndex < DataList.Count)
                            DataList.RemoveAt(SelectedIndex);
                        ShowHRoiList();
                    });
                return _delDataCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "CenterXLink":
                    CenterXLink.Text = obj.LinkName;
                    break;
                case "CenterYLink":
                    CenterYLink.Text = obj.LinkName;
                    break;
                case "Length1Link":
                    Length1Link.Text = obj.LinkName;
                    break;
                case "Length2Link":
                    Length2Link.Text = obj.LinkName;
                    break;
                case "AngleLink":
                    AngleLink.Text = obj.LinkName;
                    break;
                default:
                    break;
            }
        }

        [NonSerialized] private CommandBase _linkCommand;
        public CommandBase LinkCommand
        {
            get
            {
                if (_linkCommand == null)
                {
                    EventMgr.Ins.GetEvent<VarChangedEvent>().Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _linkCommand = new CommandBase((obj) =>
                    {
                        eLinkCommand linkCommand = (eLinkCommand)obj;
                        switch (linkCommand)
                        {
                            case eLinkCommand.InputImageLink:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HImage");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputImageLink");
                                break;
                            case eLinkCommand.CenterXLink:
                                OpenDoubleArrayLink($"{ModuleGuid},CenterXLink");
                                break;
                            case eLinkCommand.CenterYLink:
                                OpenDoubleArrayLink($"{ModuleGuid},CenterYLink");
                                break;
                            case eLinkCommand.Length1Link:
                                OpenDoubleArrayLink($"{ModuleGuid},Length1Link");
                                break;
                            case eLinkCommand.Length2Link:
                                OpenDoubleArrayLink($"{ModuleGuid},Length2Link");
                                break;
                            case eLinkCommand.AngleLink:
                                OpenDoubleArrayLink($"{ModuleGuid},AngleLink");
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _linkCommand;
            }
        }
        #endregion

        #region Method
        public void CleanupMouseEvents()
        {
            var view = ModuleView as CropImageView;
            if (view?.mWindowH?.hControl != null)
            {
                view.mWindowH.hControl.MouseUp -= HControl_MouseUp;
                view.mWindowH.hControl.MouseDown -= HControl_MouseDown;
                view.mWindowH.hControl.MouseWheel -= HControl_MouseWheel;
            }
        }

        private void OpenDoubleArrayLink(string eventParam)
        {
            CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "");
            foreach (var module in VarLinkViewModel.Ins.Modules.ToList())
            {
                var toRemove = module.VarModels.Where(v => v.DataType != "double[]").ToList();
                foreach (var v in toRemove)
                    module.VarModels.Remove(v);
                if (module.VarModels.Count == 0)
                    VarLinkViewModel.Ins.Modules.Remove(module);
            }
            EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish(eventParam);
        }

        private void AddData()
        {
            if (!(DispImage != null && DispImage.IsInitialized()))
                return;

            var data = new CropImageData();
            if (DataList.Count == 0)
            {
                data.NO = "1";
                data.Y = 100;
                data.X = 100;
            }
            else
            {
                if (int.TryParse(DataList.LastOrDefault()?.NO, out int lastNo))
                    data.NO = (lastNo + 1).ToString();
                else
                    data.NO = (DataList.Count + 1).ToString();
                data.Y = DataList.LastOrDefault().Y + 10;
                data.X = DataList.LastOrDefault().X + 10;
            }
            data.Deg = 0;
            data.L1 = 50;
            data.L2 = 50;
            DataList.Add(data);
            ShowHRoiList();
        }

        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            var view = ModuleView as CropImageView;
            if (view == null) return;
            ROI roi = view.mWindowH.WindowH.smallestActiveROI(out string info, out string index);
            if (string.IsNullOrEmpty(index)) return;

            string prefix = ModuleParam.ModuleName + ROIDefine.Search;
            if (!index.StartsWith(prefix)) return;

            string no = index.Substring(prefix.Length);
            var item = DataList.FirstOrDefault(d => d.NO == no);
            if (item == null) return;

            ROIRectangle2 rect2 = roi as ROIRectangle2;
            if (rect2 != null)
            {
                item.X = Math.Round(rect2.MidC, 3);
                item.Y = Math.Round(rect2.MidR, 3);
                item.L1 = Math.Round(rect2.Length1, 3);
                item.L2 = Math.Round(rect2.Length2, 3);
                item.Deg = Math.Round(rect2.Phi * 180.0 / Math.PI, 3);
                RoiList[index] = roi;
            }
        }

        private void HControl_MouseDown(object sender, MouseEventArgs e)
        {
        }

        private void HControl_MouseWheel(object sender, MouseEventArgs e)
        {
        }

        public override void ShowHRoi()
        {
            ShowHRoiList();
        }

        private void DataList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (CropImageData item in e.NewItems)
                    item.PropertyChanged += DataItem_PropertyChanged;
            if (e.OldItems != null)
                foreach (CropImageData item in e.OldItems)
                    item.PropertyChanged -= DataItem_PropertyChanged;
            ShowHRoiList();
        }

        private void DataItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ShowHRoiList();
        }

        private void ShowHRoiList()
        {
            var view = ModuleView as CropImageView;
            VMHWindowControl mWindowH;
            bool dispSearchRegion = true;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                dispSearchRegion = false;
            }
            else
            {
                mWindowH = view.mWindowH;
            }

            if (dispSearchRegion && SearchRegionSource == eROISource.手动输入)
            {
                foreach (var item in DataList)
                {
                    mWindowH.WindowH.genRect2(
                        ModuleParam.ModuleName + ROIDefine.Search + item.NO,
                        item.Y,
                        item.X,
                        item.Deg * (Math.PI / 180.0),
                        item.L1,
                        item.L2,
                        ref RoiList
                    );
                }

                mWindowH.DispText.Clear();
                for (int i = 0; i < DataList.Count; i++)
                {
                    mWindowH.DispText.Add(new Text
                    {
                        text = (i + 1).ToString(),
                        row = (int)DataList[i].Y + 5,
                        col = (int)DataList[i].X + 5,
                        color = "green"
                    });
                }
            }
        }

        private List<double> GetLinkArray(LinkVarModel linkVar)
        {
            List<double> result = new List<double>();
            if (linkVar == null || string.IsNullOrEmpty(linkVar.Text))
                return result;

            try
            {
                object value = GetLinkValue(linkVar);
                if (value is List<double> list)
                    result = list;
                else if (value is double[] arr)
                    result = new List<double>(arr);
                else if (value is List<int> intList)
                    result = intList.Select(x => (double)x).ToList();
                else if (value is int[] intArr)
                    result = intArr.Select(x => (double)x).ToList();
            }
            catch (Exception ex)
            {
                Logger.AddLog($"CropImage.GetLinkArray 解析失败: {ex.Message}", eMsgType.Warn);
            }
            return result;
        }
        #endregion

        #region Serialize
        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["IsOutputCropImage"] = IsOutputCropImage;
            obj["SearchRegionSource"] = (int)SearchRegionSource;

            JArray arr = new JArray();
            if (DataList != null)
            {
                foreach (var item in DataList)
                {
                    JObject itemObj = new JObject();
                    itemObj["NO"] = item.NO ?? "";
                    itemObj["X"] = item.X;
                    itemObj["Y"] = item.Y;
                    itemObj["L1"] = item.L1;
                    itemObj["L2"] = item.L2;
                    itemObj["Deg"] = item.Deg;
                    arr.Add(itemObj);
                }
            }
            obj["DataList"] = arr;

            obj["CenterXLink"] = CenterXLink?.Text ?? "";
            obj["CenterYLink"] = CenterYLink?.Text ?? "";
            obj["Length1Link"] = Length1Link?.Text ?? "";
            obj["Length2Link"] = Length2Link?.Text ?? "";
            obj["AngleLink"] = AngleLink?.Text ?? "";

            JArray roiArray = new JArray();
            foreach (var kvp in RoiList)
            {
                HTuple data = kvp.Value.GetModelData();
                JObject roiObj = new JObject
                {
                    ["Key"] = kvp.Key,
                    ["Type"] = kvp.Value.Type.ToString(),
                    ["Color"] = kvp.Value.Color,
                    ["ModelData"] = new JArray(data.ToDArr().Select(d => d))
                };
                roiArray.Add(roiObj);
            }
            obj["RoiList"] = roiArray;
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
                if (obj["IsOutputCropImage"] != null) IsOutputCropImage = obj["IsOutputCropImage"].Value<bool>();
                if (obj["SearchRegionSource"] != null) SearchRegionSource = (eROISource)obj["SearchRegionSource"].Value<int>();

                if (obj["DataList"] != null)
                {
                    JArray arr = (JArray)obj["DataList"];
                    DataList.Clear();
                    foreach (var item in arr)
                    {
                        DataList.Add(new CropImageData()
                        {
                            NO = item["NO"]?.ToString() ?? "",
                            X = item["X"]?.Value<double>() ?? 0,
                            Y = item["Y"]?.Value<double>() ?? 0,
                            L1 = item["L1"]?.Value<double>() ?? 50,
                            L2 = item["L2"]?.Value<double>() ?? 50,
                            Deg = item["Deg"]?.Value<double>() ?? 0
                        });
                    }
                }

                if (obj["CenterXLink"] != null && CenterXLink != null) CenterXLink.Text = obj["CenterXLink"].ToString();
                if (obj["CenterYLink"] != null && CenterYLink != null) CenterYLink.Text = obj["CenterYLink"].ToString();
                if (obj["Length1Link"] != null && Length1Link != null) Length1Link.Text = obj["Length1Link"].ToString();
                if (obj["Length2Link"] != null && Length2Link != null) Length2Link.Text = obj["Length2Link"].ToString();
                if (obj["AngleLink"] != null && AngleLink != null) AngleLink.Text = obj["AngleLink"].ToString();
                if (obj["RoiList"] != null)
                {
                    RoiList.Clear();
                    foreach (JToken token in (JArray)obj["RoiList"])
                    {
                        string key = token["Key"]?.ToString();
                        string type = token["Type"]?.ToString();
                        string color = token["Color"]?.ToString() ?? "yellow";
                        JArray dataArr = (JArray)token["ModelData"];
                        if (string.IsNullOrEmpty(key) || dataArr == null)
                            continue;
                        double[] data = dataArr.Select(d => d.Value<double>()).ToArray();
                        ROI roi = CreateROIFromData(type, data);
                        if (roi != null)
                        {
                            roi.Color = color;
                            RoiList[key] = roi;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.AddLog($"CropImageViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);
            }
        }
        private ROI CreateROIFromData(string type, double[] data)
        {
            switch (type)
            {
                case "Circle":
                    if (data.Length >= 3)
                        return new ROICircle(data[0], data[1], data[2]);
                    break;
                case "Line":
                    if (data.Length >= 4)
                        return new ROILine(data[0], data[1], data[2], data[3]);
                    break;
                case "Rectangle1":
                    if (data.Length >= 4)
                        return new ROIRectangle1(data[0], data[1], data[2], data[3]);
                    break;
                case "Rectangle2":
                    if (data.Length >= 5)
                        return new ROIRectangle2(data[0], data[1], data[2], data[3], data[4]);
                    break;
            }
            return null;
        }
        #endregion
    }

    [Serializable]
    public class CropImageData : NotifyPropertyBase
    {
        public string NO { get; set; }

        private double _x;
        public double X
        {
            get { return _x; }
            set { _x = value; RaisePropertyChanged(); }
        }

        private double _y;
        public double Y
        {
            get { return _y; }
            set { _y = value; RaisePropertyChanged(); }
        }

        private double _l1;
        public double L1
        {
            get { return _l1; }
            set { _l1 = value; RaisePropertyChanged(); }
        }

        private double _l2;
        public double L2
        {
            get { return _l2; }
            set { _l2 = value; RaisePropertyChanged(); }
        }

        private double _deg;
        public double Deg
        {
            get { return _deg; }
            set { _deg = value; RaisePropertyChanged(); }
        }
    }
}
