using EventMgrLib;
using HalconDotNet;
using Plugin.ShowImage.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
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
using HV.ViewModels;
using HV.Views.Dock;

namespace Plugin.ShowImage.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        nImageIndex,
        InputImageLink,
    }
    #endregion

    [Category("图像处理")]
    [DisplayName("图像显示")]
    [ModuleImageName("ShowImage")]
    [Serializable]
    public class ShowImageViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            Stopwatch.Restart();
            try
            {
                if (nImageIndex == null || ImageParam.Count <= 0)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                bool bImage = false;
                int nIndex = Convert.ToInt32(GetLinkValue(nImageIndex));
                for (int i = 0; i < ImageParam.Count; i++)
                {
                    if (nIndex == ImageParam[i].Index && ImageParam[i].InputImage.Text != "")
                    {
                        GetDispImage(ImageParam[i].InputImage.Text,true);
                        bImage = true;
                    }
                }
                if (DispImage == null || !DispImage.IsInitialized() || bImage == false)
                {
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }
                //if (DispImage.Type == "3D")
                //{
                //    var view = ModuleView as ShowImageView;

                //    VMHWindowControl mWindowH;
                //    if (view == null || view.IsClosed)
                //    {
                //        mWindowH = ViewDic.GetView(DispViewID);
                //    }
                //    else
                //    {
                //        mWindowH = view.mWindowH;
                //    }
                //    //mWindowH.ClearWindow();
                //    //mWindowH.Image = new RImage(DispImage);
                //    //int count = DispImage.CountChannels();
                //    //if (IsOpenWindows)
                //    //{

                //    //    if (count == 1)
                //    //    {
                //    //        Create3DRGB(DispImage, DispImage, out HObject multimage, "精细");
                //    //        mWindowH.DispObj(multimage);

                //    //    }
                //    //    else if (count == 2)
                //    //    {

                //    //        HImage Heightimage = DispImage.Decompose2(out HImage Grayimage);
                //    //        Create3DRGB(Heightimage, Grayimage, out HObject multimage, "精细");
                //    //        mWindowH.DispObj(multimage);

                //    //    }
                //    //}
                //    //else
                //    //{
                //    //    if (count == 1)
                //    //    {
                //    //        Create3DRGB(DispImage, DispImage, out HObject multimage, "精细");
                //    //        mWindowH.DispObj(multimage);

                //    //    }
                //    //    else if (count == 2)
                //    //    {

                //    //        HImage Heightimage = DispImage.Decompose2(out HImage Grayimage);
                //    //        Create3DRGB(Heightimage, Grayimage, out HObject multimage, "精细");
                //    //        mWindowH.DispObj(multimage);

                //    //    }
                //    //}


                //}
                ShowHRoi();
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
            if (DispImage == null)
                DispImage = new RImage(new HImage("byte",100,100));
            if (!DispImage.IsInitialized())
                DispImage.GenEmptyObj();
            base.AddOutputParams();
            AddOutputParam("图像", "HImage", DispImage);
        }
        #region Prop
        private LinkVarModel _nImageIndex = new LinkVarModel() { Text = "0" };
        /// <summary>
        /// 图像索引
        /// </summary>
        public LinkVarModel nImageIndex
        {
            get { return _nImageIndex; }
            set { _nImageIndex = value; RaisePropertyChanged(); }
        }

        private ObservableCollection<ImageParams> _ImageParam = new ObservableCollection<ImageParams>();
        /// <summary>
        /// 定义图像参数
        /// </summary>
        public ObservableCollection<ImageParams> ImageParam
        {
            get { return _ImageParam; }
            set { _ImageParam = value; RaisePropertyChanged(); }
        }

        private int _nSelectIndex;
        public int nSelectIndex
        {
            get { return _nSelectIndex; }
            set { Set(ref _nSelectIndex, value); }
        }

        /// <summary>显示结果区域</summary>
        private bool _ShowResultRoi = true;
        public bool ShowResultRoi
        {
            get { return _ShowResultRoi; }
            set { Set(ref _ShowResultRoi, value); }
        }
        private bool _ShowImage = true;
        /// <summary>
        /// 覆盖图像
        /// </summary>
        public bool ShowImage
        {
            get { return _ShowImage; }
            set
            {
                Set(ref _ShowImage, value);
            }
        }
        private bool _ShowOkLog;
        /// <summary>
        /// 显示OK日志
        /// </summary>
        public bool ShowOkLog
        {
            get { return _ShowOkLog; }
            set
            {
                Set(ref _ShowOkLog, value);
            }
        }
        private bool _ShowNgLog;
        /// <summary>
        /// 显示NG日志
        /// </summary>
        public bool ShowNgLog
        {
            get { return _ShowNgLog; }
            set
            {
                Set(ref _ShowNgLog, value);
            }
        }
        #endregion

        #region Command
        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as ShowImageView;
            if (view != null)
            {
                ClosedView = true;
                if (view.mWindowH == null)
                {
                    view.mWindowH = new VMHWindowControl();
                    view.winFormHost.Child = view.mWindowH;
                }
                foreach (var item in ImageParam)
                {
                    item.LinkCommand = LinkCommand;
                }
                //if (DispImage != null && DispImage.IsInitialized())
                //{
                //    ShowHRoi();
                //}
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
                        var view = this.ModuleView as ShowImageView;
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
                case "nImageIndex":
                    nImageIndex.Text = obj.LinkName;
                    break;
                case "InputImageLink":
                    ImageParam[nSelectIndex].InputImage.Text = obj.LinkName;
                    ExeModule();
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
                            case eLinkCommand.nImageIndex:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "int");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},nImageIndex");
                                break;
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
                                ImageParam.Add(new ImageParams()
                                {
                                    Index = ImageParam.Count,
                                    LinkCommand = LinkCommand
                                });
                                break;
                            case "Delete":
                                if (nSelectIndex < 0) return;
                                ImageParam.RemoveAt(nSelectIndex);
                                break;
                            default:
                                break;
                        }
                    });
                }
                return _DataOperateCommand;
            }
        }
        #endregion
        #region Method
        public override void ShowHRoi()
        {
            int windowsW = 594;
            int windowsH = 583;
            HTuple width = new HTuple(), height = new HTuple();
            double scale = 1;

            var view = ModuleView as ShowImageView;

            VMHWindowControl mWindowH;
            if (view == null || view.IsClosed)
            {
                mWindowH = ViewDic.GetView(DispViewID);
            }
            else
            {
                mWindowH = view.mWindowH;
            }
            if (DispImage != null)
            {
                HOperatorSet.GetImageSize(DispImage, out width, out height);
                windowsW = mWindowH.hControl.Width;
                windowsH = mWindowH.hControl.Height;
                double scaleX = width.D / windowsW;
                double scaleY = height.D / windowsH;
                //scale = Math.Min(scaleX, scaleY);
                scale = scaleX ;
            }




            if (mWindowH != null)
            {
                mWindowH.ClearWindow();
                //mWindowH.ClearROI();
                //mWindowH.DispText.Clear();
                mWindowH.Image = new RImage(DispImage);
                //mWindowH.HobjectToHimage(DispImage);
                    //if (DispImage.Type == "3D")
                    //{
                    //    int count = DispImage.CountChannels();
                    //    if (IsOpenWindows)
                    //    {
                            
                    //        if (count == 1)
                    //        {
                    //            Create3DRGB(DispImage, DispImage, out HObject multimage, "精细");
                    //            mWindowH.DispObj(multimage);
                                
                    //        }
                    //        else if (count == 2)
                    //        {

                    //            HImage Heightimage = DispImage.Decompose2(out HImage Grayimage);
                    //            Create3DRGB(Heightimage, Grayimage, out HObject multimage, "精细");
                    //            mWindowH.DispObj(multimage);

                    //        }
                    //    }
                    //    else
                    //    {
                    //        if (count == 1)
                    //        {
                    //            Create3DRGB(DispImage, DispImage, out HObject multimage, "精细");
                    //            mWindowH.DispObj(multimage);

                    //        }
                    //        else if (count == 2)
                    //        {

                    //            HImage Heightimage = DispImage.Decompose2(out HImage Grayimage);
                    //            Create3DRGB(Heightimage, Grayimage, out HObject multimage, "精细");
                    //            mWindowH.DispObj(multimage);

                    //        }
                    //    }


                    //}


                    foreach (HRoi roi in DispImage.mHRoi)
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
                        //roiText.size = 1;
                        var size = Math.Ceiling(roiText.size / scale);
                        HText hText = new HText(roiText.drawColor, roiText.text, roiText.row, roiText.col, (int)size);
                            mWindowH.WindowH.DispText(hText);
                        }
                        else
                        {
                            mWindowH.WindowH.DispHobject(roi.hobject, roi.drawColor, roi.IsFillDisp);
                        }
                    }
                }
            }
        
        #endregion
        public  void Create3DRGB(HObject ho_HeightImage, HObject ho_GrayImage, out HObject ho_MultiChannelImage,
HTuple hv_DispGrade)
        {




            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_imageOut = null, ho_Region = null, ho_Region1 = null;
            HObject ho_ObjectSelectedB = null, ho_ObjectSelectedG = null;
            HObject ho_ObjectSelectedR = null, ho_ImageCleared = null, ho_ImageResult_R = null;
            HObject ho_ImageResult_G = null, ho_ImageResult_B = null, ho_MultiChannelImage1 = null;
            HObject ho_ImageReduced = null;

            // Local copy input parameter variables 
            HObject ho_GrayImage_COPY_INP_TMP;
            ho_GrayImage_COPY_INP_TMP = new HObject(ho_GrayImage);



            // Local control variables 

            HTuple hv_Channels = new HTuple(), hv_Min = new HTuple();
            HTuple hv_Max = new HTuple(), hv_Range = new HTuple();
            HTuple hv_step = new HTuple(), hv_Sequence1 = new HTuple();
            HTuple hv_Sequence2 = new HTuple(), hv_Number = new HTuple();
            HTuple hv_Sequence_B = new HTuple(), hv_Sequence_G = new HTuple();
            HTuple hv_Sequence_R = new HTuple(), hv_Number_R = new HTuple();
            HTuple hv_Number_G = new HTuple(), hv_Number_B = new HTuple();
            HTuple hv_R = new HTuple(), hv_G1 = new HTuple(), hv_G2 = new HTuple();
            HTuple hv_G = new HTuple(), hv_B = new HTuple(), hv_Type = new HTuple();
            HTuple hv_Exception = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_MultiChannelImage);
            HOperatorSet.GenEmptyObj(out ho_imageOut);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_Region1);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedB);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedG);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelectedR);
            HOperatorSet.GenEmptyObj(out ho_ImageCleared);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_R);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_G);
            HOperatorSet.GenEmptyObj(out ho_ImageResult_B);
            HOperatorSet.GenEmptyObj(out ho_MultiChannelImage1);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced);
            try
            {
                try
                {
                    hv_Channels.Dispose();
                    HOperatorSet.CountChannels(ho_HeightImage, out hv_Channels);
                    if ((int)(new HTuple(hv_Channels.TupleGreater(1))) != 0)
                    {
                        ho_imageOut.Dispose(); ho_GrayImage_COPY_INP_TMP.Dispose();
                        HOperatorSet.Decompose2(ho_HeightImage, out ho_imageOut, out ho_GrayImage_COPY_INP_TMP
                            );
                    }
                    else
                    {
                        ho_imageOut.Dispose();
                        ho_imageOut = new HObject(ho_HeightImage);
                    }


                    hv_Min.Dispose(); hv_Max.Dispose(); hv_Range.Dispose();
                    HOperatorSet.MinMaxGray(ho_imageOut, ho_imageOut, 0, out hv_Min, out hv_Max,
                        out hv_Range);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_Region.Dispose();
                        HOperatorSet.Threshold(ho_imageOut, out ho_Region, hv_Min + 0.001, hv_Max);
                    }

                    hv_Min.Dispose(); hv_Max.Dispose(); hv_Range.Dispose();
                    HOperatorSet.MinMaxGray(ho_Region, ho_imageOut, 0, out hv_Min, out hv_Max,
                        out hv_Range);
                    if ((int)(new HTuple(hv_DispGrade.TupleEqual("精细"))) != 0)
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / 255;
                        }
                    }
                    else if ((int)(new HTuple(hv_DispGrade.TupleEqual("适中"))) != 0)
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / (255 / 2);
                        }
                    }
                    else
                    {
                        hv_step.Dispose();
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            hv_step = hv_Range / (255 / 10);
                        }
                    }

                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence1.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Min, hv_Max + hv_step, hv_step, out hv_Sequence1);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence2.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Min + hv_step, hv_Max + (2 * hv_step), hv_step,
                            out hv_Sequence2);
                    }
                    if ((int)(new HTuple((new HTuple(hv_Sequence1.TupleLength())).TupleGreater(
                        new HTuple(hv_Sequence2.TupleLength())))) != 0)
                    {
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            HTuple ExpTmpOutVar_0;
                            HOperatorSet.TupleRemove(hv_Sequence1, (new HTuple(hv_Sequence1.TupleLength()
                                )) - 1, out ExpTmpOutVar_0);
                            hv_Sequence1.Dispose();
                            hv_Sequence1 = ExpTmpOutVar_0;
                        }
                    }
                    else if ((int)(new HTuple((new HTuple(hv_Sequence1.TupleLength()
                        )).TupleLess(new HTuple(hv_Sequence2.TupleLength())))) != 0)
                    {
                        using (HDevDisposeHelper dh = new HDevDisposeHelper())
                        {
                            HTuple ExpTmpOutVar_0;
                            HOperatorSet.TupleRemove(hv_Sequence2, (new HTuple(hv_Sequence2.TupleLength()
                                )) - 1, out ExpTmpOutVar_0);
                            hv_Sequence2.Dispose();
                            hv_Sequence2 = ExpTmpOutVar_0;
                        }
                    }
                    ho_Region1.Dispose();
                    HOperatorSet.Threshold(ho_imageOut, out ho_Region1, hv_Sequence1, hv_Sequence2);


                    hv_Number.Dispose();
                    HOperatorSet.CountObj(ho_Region1, out hv_Number);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_B.Dispose();
                        HOperatorSet.TupleGenSequence(1, hv_Number / 2, 1, out hv_Sequence_B);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_G.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Number / 4, 3 * (hv_Number / 4), 1, out hv_Sequence_G);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_Sequence_R.Dispose();
                        HOperatorSet.TupleGenSequence(hv_Number / 2, hv_Number, 1, out hv_Sequence_R);
                    }
                    ho_ObjectSelectedB.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedB, hv_Sequence_B);
                    ho_ObjectSelectedG.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedG, hv_Sequence_G);
                    ho_ObjectSelectedR.Dispose();
                    HOperatorSet.SelectObj(ho_Region1, out ho_ObjectSelectedR, hv_Sequence_R);
                    hv_Number_R.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedR, out hv_Number_R);
                    hv_Number_G.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedG, out hv_Number_G);
                    hv_Number_B.Dispose();
                    HOperatorSet.CountObj(ho_ObjectSelectedB, out hv_Number_B);
                    ho_ImageCleared.Dispose();
                    HOperatorSet.GenImageProto(ho_imageOut, out ho_ImageCleared, 0);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_R.Dispose();
                        HOperatorSet.TupleGenSequence(0, 255, 255 / hv_Number_R, out hv_R);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G1.Dispose();
                        HOperatorSet.TupleGenSequence(0, 255, 255 / (hv_Number_G / 2), out hv_G1);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G2.Dispose();
                        HOperatorSet.TupleGenSequence(255, 0, -255 / (hv_Number_G / 2), out hv_G2);
                    }
                    hv_G.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_G = new HTuple();
                        hv_G = hv_G.TupleConcat(hv_G1, hv_G2);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_B.Dispose();
                        HOperatorSet.TupleGenSequence(255, 0, -255 / hv_Number_B, out hv_B);
                    }


                    ho_ImageResult_R.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedR, ho_ImageCleared, out ho_ImageResult_R,
                        hv_R, "fill");

                    ho_ImageResult_G.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedG, ho_ImageCleared, out ho_ImageResult_G,
                        hv_G, "fill");

                    ho_ImageResult_B.Dispose();
                    HOperatorSet.PaintRegion(ho_ObjectSelectedB, ho_ImageCleared, out ho_ImageResult_B,
                        hv_B, "fill");


                    ho_MultiChannelImage.Dispose();
                    HOperatorSet.Compose3(ho_ImageResult_R, ho_ImageResult_G, ho_ImageResult_B,
                        out ho_MultiChannelImage);
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.ConvertImageType(ho_MultiChannelImage, out ExpTmpOutVar_0, "byte");
                        ho_MultiChannelImage.Dispose();
                        ho_MultiChannelImage = ExpTmpOutVar_0;
                    }
                    hv_Type.Dispose();
                    HOperatorSet.GetImageType(ho_GrayImage_COPY_INP_TMP, out hv_Type);
                    if ((int)(new HTuple(hv_Type.TupleEqual("byte"))) != 0)
                    {
                        ho_MultiChannelImage1.Dispose();
                        HOperatorSet.Compose3(ho_GrayImage_COPY_INP_TMP, ho_GrayImage_COPY_INP_TMP,
                            ho_GrayImage_COPY_INP_TMP, out ho_MultiChannelImage1);
                        ho_ImageReduced.Dispose();
                        HOperatorSet.ReduceDomain(ho_MultiChannelImage1, ho_Region, out ho_ImageReduced
                            );
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.AddImage(ho_ImageReduced, ho_MultiChannelImage, out ExpTmpOutVar_0,
                                0.5, 30);
                            ho_MultiChannelImage.Dispose();
                            ho_MultiChannelImage = ExpTmpOutVar_0;
                        }
                    }
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                }


                ho_GrayImage_COPY_INP_TMP.Dispose();
                ho_imageOut.Dispose();
                ho_Region.Dispose();
                ho_Region1.Dispose();
                ho_ObjectSelectedB.Dispose();
                ho_ObjectSelectedG.Dispose();
                ho_ObjectSelectedR.Dispose();
                ho_ImageCleared.Dispose();
                ho_ImageResult_R.Dispose();
                ho_ImageResult_G.Dispose();
                ho_ImageResult_B.Dispose();
                ho_MultiChannelImage1.Dispose();
                ho_ImageReduced.Dispose();

                hv_Channels.Dispose();
                hv_Min.Dispose();
                hv_Max.Dispose();
                hv_Range.Dispose();
                hv_step.Dispose();
                hv_Sequence1.Dispose();
                hv_Sequence2.Dispose();
                hv_Number.Dispose();
                hv_Sequence_B.Dispose();
                hv_Sequence_G.Dispose();
                hv_Sequence_R.Dispose();
                hv_Number_R.Dispose();
                hv_Number_G.Dispose();
                hv_Number_B.Dispose();
                hv_R.Dispose();
                hv_G1.Dispose();
                hv_G2.Dispose();
                hv_G.Dispose();
                hv_B.Dispose();
                hv_Type.Dispose();
                hv_Exception.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_GrayImage_COPY_INP_TMP.Dispose();
                ho_imageOut.Dispose();
                ho_Region.Dispose();
                ho_Region1.Dispose();
                ho_ObjectSelectedB.Dispose();
                ho_ObjectSelectedG.Dispose();
                ho_ObjectSelectedR.Dispose();
                ho_ImageCleared.Dispose();
                ho_ImageResult_R.Dispose();
                ho_ImageResult_G.Dispose();
                ho_ImageResult_B.Dispose();
                ho_MultiChannelImage1.Dispose();
                ho_ImageReduced.Dispose();

                hv_Channels.Dispose();
                hv_Min.Dispose();
                hv_Max.Dispose();
                hv_Range.Dispose();
                hv_step.Dispose();
                hv_Sequence1.Dispose();
                hv_Sequence2.Dispose();
                hv_Number.Dispose();
                hv_Sequence_B.Dispose();
                hv_Sequence_G.Dispose();
                hv_Sequence_R.Dispose();
                hv_Number_R.Dispose();
                hv_Number_G.Dispose();
                hv_Number_B.Dispose();
                hv_R.Dispose();
                hv_G1.Dispose();
                hv_G2.Dispose();
                hv_G.Dispose();
                hv_B.Dispose();
                hv_Type.Dispose();
                hv_Exception.Dispose();

                throw HDevExpDefaultException;
            }
        }
    }
    [Serializable]
    public class ImageParams : NotifyPropertyBase
    {
        public int Index { get; set; }
        private LinkVarModel _InputImage = new LinkVarModel();
        public LinkVarModel InputImage
        {
            get { return _InputImage; }
            set { _InputImage = value; RaisePropertyChanged(); }
        }
        public CommandBase LinkCommand { get; set; }
    }


}
