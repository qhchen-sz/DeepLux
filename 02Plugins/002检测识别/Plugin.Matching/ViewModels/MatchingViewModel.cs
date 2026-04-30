using DMSkin.Socket;
using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.GrabImage.Model;
using Plugin.Matching.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web.ModelBinding;
using System.Web.UI.WebControls;
using System.Windows.Documents;
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
using HV.Dialogs.Views;
using HV.Events;
using HV.Models;
using HV.Services;
using HV.ViewModels;
using HV.Views.Dock;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Plugin.Matching.ViewModels
{
    #region tempfile version
    /// <summary>
    /// 模板保存数据结构
    /// </summary>
    [Serializable]
    public class ModelSaveData
    {
        /// <summary>
        /// 模板数据（形状模板/灰度模板）
        /// </summary>
        public byte[] ModelData { get; set; }
        /// <summary>
        /// 模板裁剪图像数据
        /// </summary>
        public byte[] ModelCutImageData { get; set; }
        /// <summary>
        /// 模板区域ROI数据
        /// </summary>
        public byte[] TempletROIData { get; set; }
        /// <summary>
        /// 搜索区域ROI数据
        /// </summary>
        //public byte[] SearchROIData { get; set; }
    }
    #endregion
    
    #region no tempfile version
    /// <summary>
    /// 模板数据（使用内存序列化，不创建临时文件）
    /// </summary>
    [Serializable]
    public class ModelDataNoTempFile
    {
        /// <summary>
        /// 序列化后的模板数据
        /// </summary>
        public byte[] ModelData { get; set; }

        /// <summary>
        /// 序列化后的模板裁剪图像
        /// </summary>
        public byte[] ImageData { get; set; }

        /// <summary>
        /// 序列化后的模板ROI数据
        /// </summary>
        public byte[] RoiData { get; set; }

        /// <summary>
        /// 序列化后的搜索区域ROI数据
        /// </summary>
        public byte[] SearchRoiData { get; set; }

        /// <summary>
        /// 序列化后的涂抹区域数据
        /// </summary>
        public byte[] PaintRegionData { get; set; }
    }

    /// <summary>
    /// 模板匹配模块辅助类 - 无临时文件版本
    /// </summary>
    public static class MatchingModelHelperNoTempFile
    {
        /// <summary>
        /// 保存模板数据（使用内存序列化，不创建临时文件）
        /// </summary>
        /// <param name="modelImage">模板对象（HShapeModel或HNCCModel）</param>
        /// <param name="modelCutImage">裁剪图像</param>
        /// <param name="modelTemplet">模板ROI</param>
        /// <param name="modelSearch">搜索区域ROI</param>
        /// <param name="paintRegion">涂抹区域（HObject类型）</param>
        /// <returns>序列化的数据</returns>
        public static byte[] SaveModelDataNoTempFile(HHandle modelImage, HImage modelCutImage, ROI modelTemplet, ROI modelSearch, HObject paintRegion)
        {
            if (modelImage == null || !modelImage.IsInitialized())
                return null;

            try
            {
                ModelDataNoTempFile data = new ModelDataNoTempFile();

                // 1. 序列化模板图像（使用 SerializeImage）
                HSerializedItem serializedImage = modelCutImage.SerializeImage();
                data.ImageData = serializedImage; // HSerializedItem隐式转换为byte[]
                serializedImage.Dispose();

                // 2. 序列化模板ROI
                HRegion region = modelTemplet.GetRegion();
                if (region.IsInitialized())
                {
                    HSerializedItem serializedRoi = region.SerializeObject();
                    data.RoiData = serializedRoi;
                    serializedRoi.Dispose();
                }
                else
                {
                    // 如果region无效，使用空数据
                    data.RoiData = new byte[0];
                }

                // 3. 序列化搜索区域ROI
                if (modelSearch != null)
                {
                    HRegion searchRegion = modelSearch.GetRegion();
                    if (searchRegion.IsInitialized())
                    {
                        HSerializedItem serializedSearchRoi = searchRegion.SerializeObject();
                        data.SearchRoiData = serializedSearchRoi;
                        serializedSearchRoi.Dispose();
                    }
                    else
                    {
                        data.SearchRoiData = new byte[0];
                    }
                }
                else
                {
                    data.SearchRoiData = new byte[0];
                }

                // 5. 序列化涂抹区域
                if (paintRegion != null && paintRegion.IsInitialized())
                {
                    HSerializedItem serializedPaint = paintRegion.SerializeObject();
                    data.PaintRegionData = serializedPaint;
                    serializedPaint.Dispose();
                }
                else
                {
                    data.PaintRegionData = new byte[0];
                }

                // 6. 序列化模板（使用 Halcon 20.11 的 SerializeShapeModel/SerializeNccModel）
                if (modelImage is HShapeModel)
                {
                    HSerializedItem serializedModel = ((HShapeModel)modelImage).SerializeShapeModel();
                    data.ModelData = serializedModel; // HSerializedItem隐式转换为byte[]
                    serializedModel.Dispose();
                }
                else if (modelImage is HNCCModel)
                {
                    HSerializedItem serializedModel = ((HNCCModel)modelImage).SerializeNccModel();
                    data.ModelData = serializedModel; // HSerializedItem隐式转换为byte[]
                    serializedModel.Dispose();
                }

                // 5. 序列化整个配置数据
                BinaryFormatter formatter = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, data);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存模板失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载模板数据（使用内存反序列化，不创建临时文件）
        /// </summary>
        /// <param name="data">序列化的数据</param>
        /// <param name="modelType">模板类型</param>
        /// <param name="modelImage">输出：模板对象</param>
        /// <param name="modelCutImage">输出：裁剪图像</param>
        /// <param name="roiTemplet">输出：模板ROI</param>
        /// <param name="roiSearch">输出：搜索区域ROI</param>
        /// <param name="paintRegion">输出：涂抹区域（HObject类型）</param>
        /// <returns>是否成功</returns>
        public static bool LoadModelDataNoTempFile(byte[] data, eModelType modelType, out HHandle modelImage, out HImage modelCutImage, out ROIRectangle1 roiTemplet, out ROIRectangle1 roiSearch, out HObject paintRegion)
        {
            modelImage = null;
            modelCutImage = null;
            roiTemplet = null;
            roiSearch = null;
            paintRegion = null;

            if (data == null || data.Length == 0)
                return false;

            try
            {
                // 1. 反序列化配置数据
                BinaryFormatter formatter = new BinaryFormatter();
                ModelDataNoTempFile config;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    config = (ModelDataNoTempFile)formatter.Deserialize(ms);
                }

                // 2. 反序列化图像 - 使用HOperatorSet
                HSerializedItem serializedImage = new HSerializedItem(config.ImageData);
                HObject imageObj;
                HOperatorSet.DeserializeImage(out imageObj, serializedImage);
                modelCutImage = new HImage(imageObj);
                serializedImage.Dispose();

                // 3. 反序列化模板ROI - 使用HOperatorSet
                if (config.RoiData == null || config.RoiData.Length == 0)
                {
                    Console.WriteLine("警告: ROI数据为空");
                }
                else
                {
                    HSerializedItem serializedRoi = new HSerializedItem(config.RoiData);
                    HObject region;
                    //HOperatorSet.DeserializeRegion(out region, serializedRoi);
                    HOperatorSet.DeserializeObject(out region, serializedRoi);
                    serializedRoi.Dispose();

                    // 获取ROI坐标
                    HOperatorSet.SmallestRectangle1(region, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                    roiTemplet = new ROIRectangle1(row1.D, col1.D, row2.D, col2.D);
                    region.Dispose();
                }

                // 4. 反序列化搜索区域ROI
                if (config.SearchRoiData != null && config.SearchRoiData.Length > 0)
                {
                    HSerializedItem serializedSearchRoi = new HSerializedItem(config.SearchRoiData);
                    HObject searchRegion;
                    HOperatorSet.DeserializeObject(out searchRegion, serializedSearchRoi);
                    serializedSearchRoi.Dispose();

                    // 获取搜索区域ROI坐标
                    HOperatorSet.SmallestRectangle1(searchRegion, out HTuple sRow1, out HTuple sCol1, out HTuple sRow2, out HTuple sCol2);
                    roiSearch = new ROIRectangle1(sRow1.D, sCol1.D, sRow2.D, sCol2.D);
                    searchRegion.Dispose();
                }

                // 5. 反序列化涂抹区域
                if (config.PaintRegionData != null && config.PaintRegionData.Length > 0)
                {
                    HSerializedItem serializedPaint = new HSerializedItem(config.PaintRegionData);
                    HOperatorSet.DeserializeObject(out paintRegion, serializedPaint);
                    serializedPaint.Dispose();
                }

                // 6. 反序列化模板 - 使用HOperatorSet
                HSerializedItem serializedModel = new HSerializedItem(config.ModelData);
                if (modelType == eModelType.形状模板)
                {
                    HTuple handle;
                    HOperatorSet.DeserializeShapeModel(serializedModel, out handle);
                    modelImage = new HShapeModel(handle.H.Handle);
                }
                else
                {
                    HTuple handle;
                    HOperatorSet.DeserializeNccModel(serializedModel, out handle);
                    modelImage = new HNCCModel(handle.H.Handle);
                }
                serializedModel.Dispose();

                return modelImage != null && modelImage.IsInitialized();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载模板失败: {ex.Message}");
                return false;
            }
        }
    }
    #endregion
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        MathNum
    }

    public enum eOperateCommand
    {
        StartLearn,
        Edit,
        EndLearn,
        Cancel
    }

    public enum eEditMode
    {
        正常显示,
        绘制涂抹,
        擦除涂抹,
    }

    public enum eDrawShape
    {
        圆形,
        矩形,
    }

    #endregion

    [Category("检测识别")]
    [DisplayName("模版匹配")]
    [ModuleImageName("Matching")]
    [Serializable]
    public class MatchingViewModel : ModuleBase
    {
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (RoiList == null)
                RoiList = new Dictionary<string, ROI>();
        }

        private List<Coord_Info> coord_Infos = new List<Coord_Info>();
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
                ClearRoiAndText();
                if (InputImageLinkText == null)
                {
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，未链接图像源！",
                        eMsgType.Warn
                    );
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                // 模板加载（反序列化）- no tempfile version
                if (ModelData != null && ModelData.Length > 0 && ModelImage == null)
                {
                    bool success = MatchingModelHelperNoTempFile.LoadModelDataNoTempFile(
                        ModelData,
                        ModelType,
                        out HHandle modelImage,
                        out HImage modelCutImage,
                        out ROIRectangle1 roiTemplet,
                        out ROIRectangle1 roiSearch,
                        out HObject paintRegion);

                    if (success)
                    {
                        ModelImage = modelImage;
                        ModelCutImage = modelCutImage;
                        RoiList[ModuleParam.ModuleName + ROIDefine.Templet] = roiTemplet;
                        if (roiSearch != null)
                        {
                            RoiList[ModuleParam.ModuleName + ROIDefine.Search] = roiSearch;
                        }
                        // 恢复涂抹区域
                        if (paintRegion != null && paintRegion.IsInitialized())
                        {
                            editViewModel.finalRegion = paintRegion;
                        }

                        Logger.AddLog($"{ModuleParam.ModuleName} 模板加载成功（无临时文件）！", eMsgType.Info);
                    }
                    else
                    {
                        Logger.AddLog($"{ModuleParam.ModuleName} 模板加载失败！", eMsgType.Warn);
                    }
                }
                if (ModelImage==null || !ModelImage.IsInitialized())
                {
                    Logger.AddLog(
                        $"流程[{Solution.Ins.GetProjectById(ModuleParam.ProjectID).ProjectInfo.ProcessName}]执行[{ModuleParam.ModuleName}]模块失败，模版句柄为空！",
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
                ROI ModelSearch = RoiList[ModuleParam.ModuleName + ROIDefine.Search];
                ROI ModelTemplet = RoiList[ModuleParam.ModuleName + ROIDefine.Templet];
                int mathNum = int.Parse(GetLinkValue(MathNum).ToString());
                HImage imageReduce = new HImage(DispImage.ReduceDomain(ModelSearch.GetRegion()));
                //Find.FindModels(ModelType,
                //        imageReduce,
                //        ModelImage,
                //        MinScore,
                //        mathNum,
                //        MaxOverlap,
                //        GreedDeg,
                //        out coord_Infos);
                if (
                    Find.FindModel(
                        ModelType,
                        imageReduce,
                        ModelImage,
                        MinScore,
                        mathNum,
                        MaxOverlap,
                        GreedDeg,
                        out MathCoord
                    ) > 0
                )
                {
                    //仿射变换-检测结果
                    HTuple tempMat2D = new HTuple();
                    HOperatorSet.VectorAngleToRigid(
                        0,
                        0,
                        0,
                        MathCoord.Y,
                        MathCoord.X,
                        MathCoord.Phi,
                        out tempMat2D
                    );
                    //检测结果-对XLD应用任意加法 2D 变换
                    HXLDCont contour_xld = ((HShapeModel)ModelImage)
                        .GetShapeModelContours(1)
                        .AffineTransContourXld(new HHomMat2D(tempMat2D));
                    //检测中心-为输入点生成一个十字形状的 XLD 轮廓
                    HOperatorSet.GenCrossContourXld(
                        out HObject cross,
                        MathCoord.Y,
                        MathCoord.X,
                        10,
                        MathCoord.Phi
                    );
                    //ROI显示
                    if (ShowSearchRegion && ModuleView == null)
                    {
                        ShowHRoi(
                            new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName,
                                ModuleParam.Remarks,
                                HRoiType.搜索范围,
                                "blue",
                                new HObject(ModelSearch.GetRegion())
                            )
                        );
                    }
                    if (ShowResultContour)
                    {
                        ShowHRoi(
                            new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName,
                                ModuleParam.Remarks,
                                HRoiType.参考坐标,
                                "red",
                                new HObject(Gen.GetCoord(DispImage, MathCoord))
                            )
                        );
                        ShowHRoi(
                            new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName,
                                ModuleParam.Remarks,
                                HRoiType.检测中心,
                                "cyan",
                                new HObject(cross)
                            )
                        );
                        ShowHRoi(
                            new HRoi(
                                ModuleParam.ModuleEncode,
                                ModuleParam.ModuleName,
                                ModuleParam.Remarks,
                                HRoiType.检测结果,
                                "green",
                                new HObject(contour_xld)
                            )
                        );
                    }
                    MathCoord.Phi = MathCoord.Phi - ModeCoord.Phi;
                    MathCoord.Status = true;
                    ShowHRoi();
                    ChangeModuleRunStatus(eRunStatus.OK);
                    if (ModuleView != null)
                    {
                        CommonMethods.UIAsync(() =>
                        {
                            MathTemplateModels.Clear();
                            foreach (var item in coord_Infos)
                            {
                                MathTemplateModels.Add(
    new MathTemplateModel()
    {
        ID = MathTemplateModels.Count + 1,
        X = item.X,
        Y = item.Y,
        Deg = item.Phi,
        Score = item.Score
    }
);
                            }

                        });
                    }
                    return true;
                }
                else
                {
                    MathCoord.Status = false;
                    //DispImage.mHRoi.Clear();
                    ShowHRoi();
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
            }
            catch (Exception ex)
            {
                ChangeModuleRunStatus(eRunStatus.NG);
                //Logger.GetExceptionMsg(ex);
                return false;
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            AddOutputParam("X", "double", MathCoord.X);
            AddOutputParam("Y", "double", MathCoord.Y);
            AddOutputParam("Deg", "double", MathCoord.Phi);
            AddOutputParam("分数", "double", MathCoord.Score);
        }

        #region Prop
        private EditViewModel _editViewModel;

        /// <summary>
        /// 编辑模版Model
        /// </summary>
        public EditViewModel editViewModel
        {
            get
            {
                if (_editViewModel == null)
                {
                    _editViewModel = new EditViewModel();
                }
                return _editViewModel;
            }
            set { Set(ref _editViewModel, value); }
        }

        [NonSerialized]
        private bool _IsStudying = false;
        [NonSerialized]
        private HImage _ModelCutImage;
        public HImage ModelCutImage
        {
            get { return _ModelCutImage; }
            set { _ModelCutImage = value; }
        }

        /// <summary>
        /// 学习中
        /// </summary>
        public bool IsStudying
        {
            get { return _IsStudying; }
            set { Set(ref _IsStudying, value); }
        }
        private bool _ShowSearchRegion = true;

        /// <summary>
        /// 显示搜索区域
        /// </summary>
        public bool ShowSearchRegion
        {
            get { return _ShowSearchRegion; }
            set { Set(ref _ShowSearchRegion, value); }
        }
        private bool _ShowResultContour = true;

        /// <summary>
        /// 显示结果轮廓
        /// </summary>
        public bool ShowResultContour
        {
            get { return _ShowResultContour; }
            set { Set(ref _ShowResultContour, value); }
        }

        /// <summary> 模板图像 </summary>
        [NonSerialized]
        public HHandle ModelImage;

        /// <summary> 序列化后的模板数据 tempfile version </summary>
        public byte[] ModelData;

        /// <summary> 区域列表 </summary>
        [NonSerialized]
        public Dictionary<string, ROI> RoiList = new Dictionary<string, ROI>();
        private eSearchRegion _SearchRegionSource = eSearchRegion.矩形1;

        /// <summary>
        /// 搜索区域源
        /// </summary>
        public eSearchRegion SearchRegionSource
        {
            get { return _SearchRegionSource; }
            set { Set(ref _SearchRegionSource, value); }
        }
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
        private Rectangle1Model _Rectangle1SearchRegion;

        /// <summary>
        /// 输入图像链接文本
        /// </summary>
        public Rectangle1Model Rectangle1SearchRegion
        {
            get
            {
                if (_Rectangle1SearchRegion == null)
                {
                    _Rectangle1SearchRegion = new Rectangle1Model();
                }
                return _Rectangle1SearchRegion;
            }
            set { Set(ref _Rectangle1SearchRegion, value); }
        }
        private eModelType _ModelType;

        /// <summary>
        /// 模板类型
        /// </summary>
        public eModelType ModelType
        {
            get { return _ModelType; }
            set { Set(ref _ModelType, value); }
        }
        private int _Levels = 0;

        /// <summary>
        /// 金字塔层数
        /// </summary>
        public int Levels
        {
            get { return _Levels; }
            set { Set(ref _Levels, value); }
        }
        private LinkVarModel _MathNum = new LinkVarModel() { Value = 1 };

        /// <summary>
        /// 匹配个数
        /// </summary>
        public LinkVarModel MathNum
        {
            get { return _MathNum; }
            set { Set(ref _MathNum, value); }
        }
        private double _MaxOverlap = 0.5;

        /// <summary>
        /// 最大重叠
        /// </summary>
        public double MaxOverlap
        {
            get { return _MaxOverlap; }
            set { Set(ref _MaxOverlap, value); }
        }
        private double _GreedDeg = 0.9;

        /// <summary>
        /// 贪心算法
        /// </summary>
        public double GreedDeg
        {
            get { return _GreedDeg; }
            set { Set(ref _GreedDeg, value); }
        }

        /// <summary>
        /// 修改坐标
        /// </summary>
        public Coord_Info ChangeCoord = new Coord_Info();
        private double _MinScore = 0.5;

        /// <summary>
        /// 最小分数
        /// </summary>
        public double MinScore
        {
            get { return _MinScore; }
            set { Set(ref _MinScore, value); }
        }

        [NonSerialized]
        private ObservableCollection<MathTemplateModel> _MathTemplateModels;

        public ObservableCollection<MathTemplateModel> MathTemplateModels
        {
            get
            {
                if (_MathTemplateModels == null)
                {
                    _MathTemplateModels = new ObservableCollection<MathTemplateModel>();
                }
                return _MathTemplateModels;
            }
            set { _MathTemplateModels = value; }
        }

        [NonSerialized]
        public HXLDCont contour_xld;
        [NonSerialized]
        public HObject OutImage;
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as MatchingView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (view.mWindowH_Template == null)
                {
                    view.mWindowH_Template = new VMHWindowControl();
                    view.winFormHost1.Child = view.mWindowH_Template;
                }
                // 模板加载（反序列化）- no tempfile version
                if (ModelData != null && ModelData.Length > 0 && ModelImage == null)
                {
                    bool success = MatchingModelHelperNoTempFile.LoadModelDataNoTempFile(
                        ModelData,
                        ModelType,
                        out HHandle modelImage,
                        out HImage modelCutImage,
                        out ROIRectangle1 roiTemplet,
                        out ROIRectangle1 roiSearch,
                        out HObject paintRegion);

                    if (success)
                    {
                        ModelImage = modelImage;
                        ModelCutImage = modelCutImage;
                        RoiList[ModuleParam.ModuleName + ROIDefine.Templet] = roiTemplet;
                        if (roiSearch != null)
                        {
                            RoiList[ModuleParam.ModuleName + ROIDefine.Search] = roiSearch;
                        }
                        // 恢复涂抹区域
                        if (paintRegion != null && paintRegion.IsInitialized())
                        {
                            editViewModel.finalRegion = paintRegion;
                        }

                        Logger.AddLog($"{ModuleParam.ModuleName} 模板加载成功（无临时文件）！", eMsgType.Info);
                    }
                    else
                    {
                        Logger.AddLog($"{ModuleParam.ModuleName} 模板加载失败！", eMsgType.Warn);
                    }
                }
                // 模板加载（反序列化） - tempfile version
                // // 反序列化恢复模板
                // if (ModelData != null && ModelData.Length > 0 && ModelImage == null)
                // {
                //     try
                //     {
                //         string tempFile = "1.shm";
                //         string imageFile = "1.tiff";
                //         try
                //         {
                //             // 反序列化ModelSaveData
                //             System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                //             using (System.IO.MemoryStream ms = new System.IO.MemoryStream(ModelData))
                //             {
                //                 ModelSaveData saveData = (ModelSaveData)formatter.Deserialize(ms);

                //                 // 恢复模板数据
                //                 System.IO.File.WriteAllBytes(tempFile, saveData.ModelData);
                //                 if (ModelType == eModelType.形状模板)
                //                 {
                //                     HOperatorSet.ReadShapeModel(tempFile, out HTuple shapeHandle);
                //                     ModelImage = new HShapeModel(shapeHandle.H.Handle);
                //                 }
                //                 else
                //                 {
                //                     HOperatorSet.ReadNccModel(tempFile, out HTuple nccHandle);
                //                     ModelImage = new HNCCModel(nccHandle.H.Handle);
                //                 }

                //                 // 恢复模板裁剪图像
                //                 System.IO.File.WriteAllBytes(imageFile, saveData.ModelCutImageData);
                //                 HOperatorSet.ReadImage(out HObject imageObj, imageFile);
                //                 ModelCutImage = new HImage(imageObj);

                //                 // 恢复模板ROI
                //                 string templetRoiFile = "1_templet.reg";
                //                 // string searchRoiFile = "1_search.reg";
                //                 System.IO.File.WriteAllBytes(templetRoiFile, saveData.TempletROIData);
                //                 HOperatorSet.ReadRegion(out HObject templetRegion, templetRoiFile);
                //                 HOperatorSet.SmallestRectangle1(templetRegion, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                //                 ROIRectangle1 roiTemplet = new ROIRectangle1(row1.D, col1.D, row2.D, col2.D);
                //                 RoiList[ModuleParam.ModuleName + ROIDefine.Templet] = roiTemplet;

                //                 // // 恢复搜索ROI
                //                 // System.IO.File.WriteAllBytes(searchRoiFile, saveData.SearchROIData);
                //                 // HOperatorSet.ReadRegion(out HObject searchRegion, searchRoiFile);
                //                 // HOperatorSet.SmallestRectangle1(searchRegion, out HTuple row1_s, out HTuple col1_s, out HTuple row2_s, out HTuple col2_s);
                //                 // ROIRectangle1 roiSearch = new ROIRectangle1(row1_s.D, col1_s.D, row2_s.D, col2_s.D);
                //                 // RoiList[ModuleParam.ModuleName + ROIDefine.Search] = roiSearch;
                //             }

                //             // // 删除临时文件
                //             // System.IO.File.Delete(tempFile);
                //             // System.IO.File.Delete(imageFile);
                //             // System.IO.File.Delete(roiFile + "_templet");
                //             // System.IO.File.Delete(roiFile + "_search");

                //             Logger.AddLog($"{ModuleParam.ModuleName}模板加载成功！", eMsgType.Info);
                //         }
                //         catch (Exception ex)
                //         {
                //             Logger.AddLog($"{ModuleParam.ModuleName}模板加载失败：{ex.Message}", eMsgType.Warn);
                //         }
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.AddLog($"{ModuleParam.ModuleName}模板加载失败：{ex.Message}", eMsgType.Warn);
                //     }
                // }
                // 模板加载（反序列化） - tempfile version
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null)
                        return;
                }
                GetDispImage(InputImageLinkText);
                view.mWindowH.DispObj(DispImage);
                if (DispImage != null && DispImage.IsInitialized())
                {
                    view.mWindowH.hControl.MouseUp += HControl_MouseUp;
                    view.mWindowH.hControl.MouseMove += HControl_MouseMove;
                    view.mWindowH.hControl.MouseWheel += HControl_MouseWheel;
                    ShowHRoi();
                    ShowHText();

                    //if (ModelCutImage != null && ModelCutImage.IsInitialized())
                    //{
                    //    view.mWindowH_Template.HobjectToHimage(OutImage);
                    //}
                    //if (contour_xld != null && contour_xld.IsInitialized())
                    //{
                    //    view.mWindowH_Template.WindowH.DispHobject(contour_xld, "green");
                    //}
                    //ShowTemp();
                }
                editViewModel.MatchingViewModel = this;
                ShowTemp();
            }
        }

        [NonSerialized]
        private CommandBase _OperateCommand;
        public CommandBase OperateCommand
        {
            get
            {
                if (_OperateCommand == null)
                {
                    _OperateCommand = new CommandBase(
                        (obj) =>
                        {
                            try
                            {
                                eOperateCommand par = (eOperateCommand)obj;
                                switch (par)
                                {
                                    case eOperateCommand.StartLearn:
                                        IsStudying = true;
                                        if (DispImage == null || !DispImage.IsInitialized())
                                        {
                                            MessageView.Ins.MessageBoxShow("图像不能为空！");
                                            return;
                                        }
                                        var view = ModuleView as MatchingView;
                                        if (view == null)
                                            return;
                                        view.mWindowH.HobjectToHimage(DispImage);
                                        if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Templet))
                                        {
                                            ROIRectangle1 ROIRect1 = (ROIRectangle1)
                                                RoiList[ModuleParam.ModuleName + ROIDefine.Templet];
                                            view.mWindowH.WindowH.genRect1(
                                                ModuleParam.ModuleName + ROIDefine.Templet,
                                                ROIRect1.row1,
                                                ROIRect1.col1,
                                                ROIRect1.row2,
                                                ROIRect1.col2,
                                                ref RoiList
                                            );
                                        }
                                        else
                                        {
                                            view.mWindowH.WindowH.genRect1(
                                                ModuleParam.ModuleName + ROIDefine.Templet,
                                                view.mWindowH.hv_imageHeight / 2,
                                                view.mWindowH.hv_imageWidth / 2,
                                                view.mWindowH.hv_imageHeight / 2 + 150,
                                                view.mWindowH.hv_imageWidth / 2 + 150,
                                                ref RoiList
                                            );
                                        }
                                        break;
                                    case eOperateCommand.Edit:

                                        // 防呆：无有效图像时提示用户并返回，避免后续Halcon操作导致崩溃
                                        if (ModelCutImage == null || !ModelCutImage.IsInitialized())
                                        {
                                            MessageView.Ins.MessageBoxShow("请先学习后，在编辑模板！", eMsgType.Warn);
                                            return;
                                        }

                                        EditView editView = new EditView();
                                        editView.DataContext = editViewModel;
                                        editViewModel.view = editView;
                                        editViewModel.contour_xld = contour_xld;
                                        //模板区域
                                        #region 素质3连
                                        HImage temp = new HImage();
                                        ModelCutImage.GetImageSize(out HTuple w, out HTuple h);
                                        temp.GenImageConst("byte", w, h);
                                        #endregion
                                        //ROIRectangle1 ROIRect2 = (ROIRectangle1);
                                        //HOperatorSet.AreaCenter(ROIRect2.GetRegion(), out HTuple a, out HTuple r, out HTuple c);
                                        //在模板窗口显示模板
                                        //HOperatorSet.ReduceDomain(DispImage, RoiList[ModuleParam.ModuleName + ROIDefine.Templet].GetRegion(), out HObject CutImage);
                                        //HOperatorSet.CropDomain(CutImage, out OutImage);
                                        editViewModel.OutImage = ModelCutImage;
                                        editViewModel.MatchingViewModel = this;
                                        editView.ShowDialog();
                                        ShowTemp();
                                        //ExeModule();
                                        
                                        break;
                                    case eOperateCommand.EndLearn:
                                        try
                                        {
                                            IsStudying = false;
                                            switch (ModelType)
                                            {
                                                case eModelType.形状模板:
                                                    ModelImage = new HShapeModel();
                                                    break;
                                                case eModelType.灰度模板:
                                                    ModelImage = new HNCCModel();
                                                    break;
                                                default:
                                                    break;
                                            }
                                            CreateModel();
                                            if (ModelImage.IsInitialized())
                                            {
                                                ShowTemp();
                                                ExeModule();
                                                
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.GetExceptionMsg(ex);
                                        }
                                        break;
                                    case eOperateCommand.Cancel:
                                        IsStudying = false;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.GetExceptionMsg(ex);
                            }
                        }
                    );
                }
                return _OperateCommand;
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
                    _ExecuteCommand = new CommandBase(
                        (obj) =>
                        {
                            ExeModule();
                        }
                    );
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
                    _ConfirmCommand = new CommandBase(
                        (obj) =>
                        {
                            var view = this.ModuleView as MatchingView;
                            if (view != null)
                            {
                                view.Close();
                            }
                        }
                    );
                }
                return _ConfirmCommand;
            }
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "MathNumLink":
                    MathNum.Text = obj.LinkName;
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
                    EventMgr.Ins
                        .GetEvent<VarChangedEvent>()
                        .Subscribe(OnVarChanged, o => o.SendName.StartsWith($"{ModuleGuid}"));
                    _LinkCommand = new CommandBase(
                        (obj) =>
                        {
                            eLinkCommand linkCommand = (eLinkCommand)obj;
                            switch (linkCommand)
                            {
                                case eLinkCommand.InputImageLink:
                                    CommonMethods.GetModuleList(
                                        ModuleParam,
                                        VarLinkViewModel.Ins.Modules,
                                        "HImage"
                                    );
                                    EventMgr.Ins
                                        .GetEvent<OpenVarLinkViewEvent>()
                                        .Publish($"{ModuleGuid},InputImageLink");
                                    break;
                                case eLinkCommand.MathNum:
                                    CommonMethods.GetModuleList(
                                        ModuleParam,
                                        VarLinkViewModel.Ins.Modules,
                                        "int"
                                    );
                                    EventMgr.Ins
                                        .GetEvent<OpenVarLinkViewEvent>()
                                        .Publish($"{ModuleGuid},MathNumLink");
                                    break;
                                default:
                                    break;
                            }
                        }
                    );
                }
                return _LinkCommand;
            }
        }

        #endregion

        #region Method
        private void HControl_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                var view = ModuleView as MatchingView;
                if (view == null)
                    return;
                ;
                ROI roi = view.mWindowH.WindowH.smallestActiveROI(
                    out string info,
                    out string index
                );
                if (index.Length < 1)
                    return;
                RoiList[index] = roi;
                ShowHText();
            }
            catch (Exception ex) { }
        }

        private void HControl_MouseWheel(object sender, MouseEventArgs e)
        {
            ShowHText();
        }

        private void HControl_MouseMove(object sender, MouseEventArgs e)
        {
            ShowHText();
        }

        public void CreateModel()
        {
            try
            {
                if (DispImage == null || !DispImage.IsInitialized())
                {
                    Logger.AddLog($"{ModuleParam.ModuleName}无图像！", eMsgType.Warn);
                    return;
                }
                ROI ModelSearch = RoiList[ModuleParam.ModuleName + ROIDefine.Search];
                ROI ModelTemplet = RoiList[ModuleParam.ModuleName + ROIDefine.Templet];
                HImage hImage1  = new HImage(DispImage.ReduceDomain(ModelSearch.GetRegion()));
                ModelCutImage = DispImage.ReduceDomain(ModelTemplet.GetRegion()).CropDomain();
                editViewModel.OutImage = ModelCutImage;
                editViewModel.CreateModel();
                ModelImage = editViewModel.MatchingViewModel.ModelImage;
                // 学习模板后，保存模板数据到工程文件（不创建临时文件）no tempfile version
                if (ModelImage != null && ModelImage.IsInitialized())
                {
                    ModelData = MatchingModelHelperNoTempFile.SaveModelDataNoTempFile(
                        ModelImage,
                        ModelCutImage,
                        ModelTemplet,
                        ModelSearch,
                        editViewModel.finalRegion);

                    if (ModelData != null)
                    {
                        Logger.AddLog($"{ModuleParam.ModuleName} 模板序列化成功（无临时文件）!", eMsgType.Info);
                    }
                    else
                    {
                        Logger.AddLog($"{ModuleParam.ModuleName} 模板序列化失败！", eMsgType.Warn);
                    }
                }
                // // 模板加载（反序列化） - tempfile version
                // // 序列化模板数据并保存
                // if (ModelImage != null && ModelImage.IsInitialized())
                // {
                //     try
                //     {
                //         string tempFile = "1.shm";
                //         string imageFile = "1.tiff";
                //         try
                //         {
                //             // 保存模板数据
                //             if (ModelType == eModelType.形状模板)
                //             {
                //                 HOperatorSet.WriteShapeModel((HShapeModel)ModelImage, tempFile);
                //             }
                //             else
                //             {
                //                 HOperatorSet.WriteNccModel((HNCCModel)ModelImage, tempFile);
                //             }
                //             byte[] modelData = System.IO.File.ReadAllBytes(tempFile);

                //             // 保存模板裁剪图像
                //             HOperatorSet.WriteImage(ModelCutImage, "tiff", 0, imageFile);
                //             byte[] modelCutImageData = System.IO.File.ReadAllBytes(imageFile);

                //             // 保存模板ROI和搜索ROI
                //             string templetRoiFile = "1_templet.reg";
                //             // string searchRoiFile = "1_search.reg";
                //             HOperatorSet.WriteRegion(ModelTemplet.GetRegion(), templetRoiFile);
                //             // HOperatorSet.WriteRegion(ModelSearch.GetRegion(), searchRoiFile);
                //             byte[] templetRoiData = System.IO.File.ReadAllBytes(templetRoiFile);
                //             // byte[] searchRoiData = System.IO.File.ReadAllBytes(searchRoiFile);

                //             // 打包所有数据
                //             ModelSaveData saveData = new ModelSaveData
                //             {
                //                 ModelData = modelData,
                //                 ModelCutImageData = modelCutImageData,
                //                 TempletROIData = templetRoiData,
                //                 //SearchROIData = searchRoiData
                //             };

                //             // 序列化为字节数组
                //             System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                //             using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                //             {
                //                 formatter.Serialize(ms, saveData);
                //                 ModelData = ms.ToArray();
                //             }

                //             // // 删除临时文件
                //             // System.IO.File.Delete(tempFile);
                //             // System.IO.File.Delete(imageFile);
                //             // System.IO.File.Delete(roiFile + "_templet");
                //             // System.IO.File.Delete(roiFile + "_search");

                //             Logger.AddLog($"{ModuleParam.ModuleName} 模板序列化成功!", eMsgType.Info);
                //         }
                //         catch (Exception ex)
                //         {
                //             Logger.GetExceptionMsg(ex);
                //         }
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.GetExceptionMsg(ex);
                //     }
                // }
                // else
                // {
                //     ModelData = null;
                // }
                // // 模板加载（反序列化） - tempfile version
                //Find.CreateModel(
                //    ModelType,
                //    hImage1,
                //    ModelTemplet,
                //    editViewModel.Threshold,
                //    Levels,
                //    editViewModel.StartPhi,
                //    editViewModel.EndPhi,
                //    editViewModel.MinScale,
                //    editViewModel.MaxScale,
                //    editViewModel.CompType,
                //    editViewModel.Optimization,
                //    ref ModelImage
                //);
                if (ModelImage.IsInitialized())
                {
                    int mathNum = int.Parse(GetLinkValue(MathNum).ToString());
                    HImage hImage  = new HImage(DispImage.ReduceDomain(ModelSearch.GetRegion()));
                    Find.FindModel(
                        ModelType,
                        hImage,
                        ModelImage,
                        MinScore,
                        mathNum,
                        MaxOverlap,
                        GreedDeg,
                        out ModeCoord
                    );
                    ModelTemplet.GetRegion().SmallestRectangle1(out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                    editViewModel.MatchingViewModel.ModeCoord = ModeCoord;
                    editViewModel.MatchingViewModel.ModeCoord.Y = editViewModel.MatchingViewModel.ModeCoord.Y - row1;
                    editViewModel.MatchingViewModel.ModeCoord.X = editViewModel.MatchingViewModel.ModeCoord.X - col1;
                    Logger.AddLog(ModuleParam.ModuleName + ":创建模板成功！");
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
            }
        }

        public override void ShowHRoi()
        {
            var view = ModuleView as MatchingView;
            VMHWindowControl mWindowH;
            bool dispSearchRegion = true;
            if (view == null || view.IsClosed)
            {
                dispSearchRegion = false;
                return;
                mWindowH = ViewDic.GetView(DispImage.DispViewID);
                
            }
            else
            {
                mWindowH = view.mWindowH;
                if (mWindowH != null)
                {
                    mWindowH.ClearWindow();
                    mWindowH.Image = new RImage(DispImage);
                }
            }
            if (dispSearchRegion)
            {
                if (RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Search))
                {
                    ROIRectangle1 ROIRect1 = (ROIRectangle1)
                        RoiList[ModuleParam.ModuleName + ROIDefine.Search];
                    mWindowH.WindowH.genRect1(
                        ModuleParam.ModuleName + ROIDefine.Search,
                        ROIRect1.row1,
                        ROIRect1.col1,
                        ROIRect1.row2,
                        ROIRect1.col2,
                        ref RoiList
                    );
                }
                else
                {
                    mWindowH.WindowH.genRect1(
                        ModuleParam.ModuleName + ROIDefine.Search,
                        5,
                        5,
                        mWindowH.hv_imageHeight - 5,
                        mWindowH.hv_imageWidth - 5,
                        ref RoiList
                    );
                }
            }
            List<HRoi> roiList = mHRoi.Where(c => c.ModuleName == ModuleParam.ModuleName).ToList();
            foreach (HRoi roi in roiList)
            {
                if (roi.roiType == HRoiType.文字显示)
                {
                    HText roiText = (HText)roi;
                    ShowTool.SetFont(
                        mWindowH.hControl.HalconWindow,
                        roiText.size,
                        "false",
                        "false"
                    );
                    ShowTool.SetMsg(
                        mWindowH.hControl.HalconWindow,
                        roiText.text,
                        "image",
                        roiText.row,
                        roiText.col,
                        roiText.drawColor,
                        "false"
                    );
                }
                else if (roi.roiType == HRoiType.搜索范围)
                {
                    if (ShowSearchRegion && ModuleView == null)
                    {
                        mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                    }
                }
                else
                {
                    mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor);
                }
            }
        }

        private void ShowHText()
        {
            var view = ModuleView as MatchingView;
            if (view == null)
                return;
            if (RoiList.Count == 0 || DispImage == null)
                return;
            HTuple info = RoiList[ModuleParam.ModuleName + ROIDefine.Search].GetModelData();
            Rectangle1SearchRegion.Row1 = Math.Round(info.DArr[0], 0);
            Rectangle1SearchRegion.Col1 = Math.Round(info.DArr[1], 0);
            Rectangle1SearchRegion.Row2 = Math.Round(info.DArr[2], 0);
            Rectangle1SearchRegion.Col2 = Math.Round(info.DArr[3], 0);
            if (
                info.DArr[2] > view.mWindowH.hv_imageHeight
                || info.DArr[3] > view.mWindowH.hv_imageWidth
            )
            {
                ROIRectangle1 ROIRect1 = new ROIRectangle1(
                    Rectangle1SearchRegion.Row1,
                    Rectangle1SearchRegion.Col1,
                    view.mWindowH.hv_imageHeight - 5,
                    view.mWindowH.hv_imageWidth - 5
                );
                RoiList[ModuleParam.ModuleName + ROIDefine.Search] = ROIRect1;
            }
            ShowTool.SetFont(view.mWindowH.hControl.HalconWindow, 15, "false", "false");
            if (!IsStudying)
            {
                ShowTool.SetMsg(
                    view.mWindowH.hControl.HalconWindow,
                    "搜索框",
                    "image",
                    info.DArr[1] + 5,
                    info.DArr[0] + 5,
                    "cyan",
                    "false"
                );
            }
            if (IsStudying & RoiList.ContainsKey(ModuleParam.ModuleName + ROIDefine.Templet))
            {
                HTuple info1 = RoiList[ModuleParam.ModuleName + ROIDefine.Templet].GetModelData();
                ShowTool.SetMsg(
                    view.mWindowH.hControl.HalconWindow,
                    "学习框",
                    "image",
                    info1.DArr[1],
                    info1.DArr[0],
                    "cyan",
                    "false"
                );
            }
        }

        private void ShowTemp()
        {
            try
            {
                if (ModelCutImage == null || ModelImage == null)
                    return;
                //模板区域
                //HRegion ModeRegion = RoiList[
                //    ModuleParam.ModuleName + ROIDefine.Templet
                //].GetRegion();
                //在模板窗口显示模板
                //HOperatorSet.ReduceDomain(DispImage, ModeRegion, out HObject CutImage);
                //HOperatorSet.CropDomain(CutImage, out OutImage);
                //求中心
                //HOperatorSet.AreaCenter(
                //    ModeRegion,
                //    out HTuple FormArea,
                //    out HTuple FormY,
                //    out HTuple FormX
                //);
                //HOperatorSet.AreaCenter(
                //    ModelCutImage,
                //    out HTuple ToArea,
                //    out HTuple ToY,
                //    out HTuple ToX
                //);
                //检测结果-对XLD应用任意加法 2D 变换
                HOperatorSet.VectorAngleToRigid(
                    0,
                    0,
                    0,
                    editViewModel.MatchingViewModel.ModeCoord.Y,
                    editViewModel.MatchingViewModel.ModeCoord.X,
                    editViewModel.MatchingViewModel.ModeCoord.Phi,
                    out HTuple tempMat2D
                );
                contour_xld = ((HShapeModel)ModelImage)
                    .GetShapeModelContours(1)
                    .AffineTransContourXld(new HHomMat2D(tempMat2D));
                var view = ModuleView as MatchingView;
                if (view == null)
                    return;
                //显示
                view.mWindowH_Template.SetImageMessDisp(false);
                view.mWindowH_Template.HobjectToHimage(ModelCutImage);
                view.mWindowH_Template.WindowH.DispHobject(contour_xld, "green");
                view.mWindowH_Template.DispObj(Gen.GetCoord(new RImage(ModelCutImage), editViewModel.MatchingViewModel.ModeCoord), "red");
                HOperatorSet.GenCrossContourXld(
                        out HObject cross,
                        editViewModel.MatchingViewModel.ModeCoord.Y,
                        editViewModel.MatchingViewModel.ModeCoord.X,
                        10,
                        editViewModel.MatchingViewModel.ModeCoord.Phi
                    );
                view.mWindowH_Template.DispObj(cross, "cyan");
            }
            catch (Exception ex)
            {
                Logger.AddLog(ModuleParam.ModuleName + ":" + ex.Message);
            }
        }
        #endregion
    }
}
