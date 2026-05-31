using EventMgrLib;
using HalconDotNet;
using Plugin.Jigsaw.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
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
using HV.ViewModels;
using HV.Views.Dock;

namespace Plugin.Jigsaw.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
    }
    #endregion

    [Category("深度学习")]
    [DisplayName("拼图算法")]
    [ModuleImageName("Jigsaw")]
    [Serializable]
    public class JigsawViewModel : ModuleBase
    {
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

                HImage Image = DispImage;
                HTuple Width, Height;
                Image.GetImageSize(out Width, out Height);

                // 2. 自动定位产品
                HRegion Region = Image.Threshold((double)ThresholdMin, (double)ThresholdMax);
                if (Region == null || !Region.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                HRegion ConnectedRegions = Region.Connection();
                if (ConnectedRegions == null || !ConnectedRegions.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                HRegion ObjectRegion = ConnectedRegions.SelectShape("area", "and", 10000, 9999999999);
                HRegion SelectedRegions = ObjectRegion.SelectShapeStd("max_area", 70);
                if (SelectedRegions == null || !SelectedRegions.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                SelectedRegions.SmallestRectangle1(out HTuple Row1, out HTuple Col1, out HTuple Row2, out HTuple Col2);

                HTuple ProductHeight = Row2 - Row1;
                HTuple ProductWidth = Col2 - Col1;

                // 3. 用户自定义核心参数
                HTuple EdgeThickness = this.EdgeThickness;
                HTuple SideLen = ProductHeight;
                HTuple L = (HTuple)(SideLen / DivisionFactor);
                HTuple OverlapPixels = this.OverlapPixels;
                HTuple Step = L - OverlapPixels;

                if (Step <= 0)
                    Step = L;

                HTuple FinalNumCols = this.FinalNumCols;
                HTuple EnableLeftSide = this.EnableLeftSide == 1 ? 1 : 0;

                // 4. 提取切片并统一尺寸
                HObject AllBlocks = new HObject();
                AllBlocks.GenEmptyObj();

                // --- 顶部切片 ---
                HTuple TopYStart = Row1;
                HTuple TopYEnd = Row1 + EdgeThickness;

                List<HTuple> XStarts = new List<HTuple>();
                HTuple X = Col1;
                while (X + L <= Col2)
                {
                    XStarts.Add(X);
                    X = X + Step;
                }
                if (XStarts.Count > 0 && XStarts[XStarts.Count - 1] + L > Col2)
                {
                    XStarts[XStarts.Count - 1] = Col2 - L;
                }

                HTuple NumPartsTop = XStarts.Count;

                for (int i = 0; i < NumPartsTop; i++)
                {
                    HTuple StartX = XStarts[i];
                    HTuple EndX = StartX + L;
                    HObject Block;
                    HOperatorSet.CropRectangle1(Image, out Block, TopYStart, StartX, TopYEnd, EndX);
                    HOperatorSet.ConcatObj(AllBlocks, Block, out AllBlocks);
                    // 不要在这里释放 Block，因为它已经被 concatObj 合并到 AllBlocks
                }

                // --- 底部切片 ---
                HTuple BotYStart = Row2 - EdgeThickness;
                HTuple BotYEnd = Row2;

                for (int i = 0; i < NumPartsTop; i++)
                {
                    HTuple StartX = XStarts[i];
                    HTuple EndX = StartX + L;
                    HObject Block;
                    HOperatorSet.CropRectangle1(Image, out Block, BotYStart, StartX, BotYEnd, EndX);
                    HOperatorSet.ConcatObj(AllBlocks, Block, out AllBlocks);
                    // 不要在这里释放 Block
                }

                // --- 左侧切片 ---
                HTuple LeftBlockCount = 0;
                if (EnableLeftSide == 1)
                {
                    HTuple LeftXStart = Col1;
                    HTuple LeftXEnd = Col1 + EdgeThickness;

                    HTuple LeftValidStartY = Row1;
                    HTuple LeftValidEndY = Row2;

                    List<HTuple> YStarts = new List<HTuple>();
                    HTuple Y = LeftValidStartY;
                    while (Y + L <= LeftValidEndY)
                    {
                        YStarts.Add(Y);
                        Y = Y + Step;
                    }
                    if (YStarts.Count > 0 && YStarts[YStarts.Count - 1] + L > LeftValidEndY)
                    {
                        YStarts[YStarts.Count - 1] = LeftValidEndY - L;
                    }

                    LeftBlockCount = YStarts.Count;

                    for (int i = 0; i < LeftBlockCount; i++)
                    {
                        HTuple StartY = YStarts[i];
                        HTuple EndY = StartY + L;
                        HObject Block;
                        HOperatorSet.CropRectangle1(Image, out Block, StartY, LeftXStart, EndY, LeftXEnd);
                        HOperatorSet.ConcatObj(AllBlocks, Block, out AllBlocks);
                        // 不要在这里释放 Block
                    }
                }

                // 5. 按配对规则进行拼接
                HObject FinalStripes = new HObject();
                FinalStripes.GenEmptyObj();

                // 5.1 顶部 + 底部配对
                for (int i = 0; i < NumPartsTop; i++)
                {
                    HObject TopBlock = AllBlocks.SelectObj(i + 1);
                    HObject BottomBlock = AllBlocks.SelectObj(i + 1 + NumPartsTop);
                    HObject CombinedPair = new HObject();
                    HOperatorSet.ConcatObj(TopBlock, BottomBlock, out CombinedPair);
                    HObject StripesCombined;
                    HOperatorSet.TileImages(CombinedPair, out StripesCombined, new HTuple(1), "vertical");
                    HOperatorSet.ConcatObj(FinalStripes, StripesCombined, out FinalStripes);
                    // 不要释放 StripesCombined，因为 FinalStripes 引用了它
                    TopBlock.Dispose();
                    BottomBlock.Dispose();
                    CombinedPair.Dispose();
                }

                // 5.2 左侧配对拼接
                if (EnableLeftSide == 1)
                {
                    HTuple LeftStartIdx = NumPartsTop * 2 + 1;

                    HTuple MidPos = LeftBlockCount / 2;

                    List<HTuple[]> PairIndices = new List<HTuple[]>();

                    for (int i = 0; i < MidPos; i++)
                    {
                        HTuple LeftIdx = MidPos - 1 - i;
                        HTuple RightIdx = MidPos + i;
                        if (LeftIdx >= 0 && RightIdx < LeftBlockCount && LeftIdx < RightIdx)
                        {
                            PairIndices.Add(new HTuple[] { LeftIdx, RightIdx });
                        }
                    }

                    for (int i = 0; i < PairIndices.Count; i++)
                    {
                        HTuple Idx1 = PairIndices[i][0];
                        HTuple Idx2 = PairIndices[i][1];

                        HObject LeftBlock1 = AllBlocks.SelectObj(LeftStartIdx + Idx1);
                        HObject LeftBlock2 = AllBlocks.SelectObj(LeftStartIdx + Idx2);

                        HObject LeftBlock1_Rot;
                        HObject LeftBlock2_Rot;
                       
                        HOperatorSet.RotateImage(LeftBlock1, out LeftBlock1_Rot, new HTuple(-90), "constant");
                        HOperatorSet.RotateImage(LeftBlock2, out LeftBlock2_Rot, new HTuple(-90), "constant");
                        HOperatorSet.RotateImage(LeftBlock2, out LeftBlock2_Rot, new HTuple(90), "constant");

                        HObject PairCurrent = new HObject();
                        HOperatorSet.ConcatObj(LeftBlock1_Rot, LeftBlock2_Rot, out PairCurrent);
                        HObject StripesCurrent;
                        HOperatorSet.TileImages(PairCurrent, out StripesCurrent, new HTuple(1), "vertical");
                        HOperatorSet.ConcatObj(FinalStripes, StripesCurrent, out FinalStripes);

                        LeftBlock1.Dispose();
                        LeftBlock2.Dispose();
                        LeftBlock1_Rot.Dispose();
                        LeftBlock2_Rot.Dispose();
                        
                        PairCurrent.Dispose();
                        // 不要释放 StripesCurrent，因为 FinalStripes 引用了它
                    }
                }

                // 6. 最终生成结果图片
                HObject FinalResultObj;
                HOperatorSet.TileImages(FinalStripes, out FinalResultObj, new HTuple(FinalNumCols), "horizontal");
                if (FinalResultObj == null || !FinalResultObj.IsInitialized())
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 克隆图像以确保 DisplayImage 拥有独立的数据副本
                HObject ImageCopy;
                HOperatorSet.CopyObj(FinalResultObj, out ImageCopy, 1, -1);

                // 输出
                if (OutputImage != null)
                    OutputImage.Dispose();
                OutputImage = new HImage(ImageCopy);
                ImageCopy.Dispose();
                if (OutRegion == null)
                    OutRegion = new HRegion();
                OutRegion.GenEmptyObj();

                // ShowHRoi(); // 暂时禁用，输出图像而非ROI

                ChangeModuleRunStatus(eRunStatus.OK);

                // 释放中间对象
                AllBlocks.Dispose();
                FinalStripes.Dispose();
                FinalResultObj.Dispose();

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
                // 所有对象已在 return 前正确释放
            }
        }

        public override void AddOutputParams()
        {
            base.AddOutputParams();
            AddOutputParam("输出图像", "HImage", OutputImage);
            AddOutputParam("区域", "HRegion", OutRegion);
        }

        #region Prop
        [NonSerialized]
        public HImage OutputImage;

        private int _ThresholdMin = 120;
        private int _ThresholdMax = 255;

        public int ThresholdMin
        {
            get { return _ThresholdMin; }
            set { Set(ref _ThresholdMin, value); }
        }

        public int ThresholdMax
        {
            get { return _ThresholdMax; }
            set { Set(ref _ThresholdMax, value); }
        }

        private int _EdgeThickness = 100;
        public int EdgeThickness
        {
            get { return _EdgeThickness; }
            set { Set(ref _EdgeThickness, value); }
        }

        private int _DivisionFactor = 4;
        public int DivisionFactor
        {
            get { return _DivisionFactor; }
            set { Set(ref _DivisionFactor, value); }
        }

        private int _OverlapPixels = 10;
        public int OverlapPixels
        {
            get { return _OverlapPixels; }
            set { Set(ref _OverlapPixels, value); }
        }

        private int _FinalNumCols = 1;
        public int FinalNumCols
        {
            get { return _FinalNumCols; }
            set { Set(ref _FinalNumCols, value); }
        }

        private int _EnableLeftSide = 1;
        public int EnableLeftSide
        {
            get { return _EnableLeftSide; }
            set { Set(ref _EnableLeftSide, value); }
        }

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set { Set(ref _InputImageLinkText, value); }
        }

        [NonSerialized]
        HRegion OutRegion = new HRegion();
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as JigsawView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                if (InputImageLinkText == null || InputImageLinkText == "")
                {
                    SetDefaultLink();
                    if (InputImageLinkText == null) return;
                }
                GetDispImage(InputImageLinkText);
                view.mWindowH.DispObj(DispImage);
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
                        bool success = ExeModule();
                        var view = ModuleView as JigsawView;
                        if (view != null && view.mWindowH != null)
                        {
                            if (OutputImage != null && OutputImage.IsInitialized())
                            {
                                view.mWindowH.HobjectToHimage(OutputImage);
                            }
                        }
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
                        var view = ModuleView as JigsawView;
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
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    if (InputImageLinkText == null) return;
                    GetDispImage(InputImageLinkText);
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
        #endregion
    }
}