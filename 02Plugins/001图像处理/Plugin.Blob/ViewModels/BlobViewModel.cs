using EventMgrLib;
using HalconDotNet;
using HandyControl.Controls;
using Plugin.Blob.Models;
using Plugin.Blob.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VM.Halcon;
using HV.Attributes;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Core;
using HV.Events;
using HV.ViewModels;
using VM.Halcon.Config;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using HandyControl.Tools.Extension;
using Newtonsoft.Json.Linq;

namespace Plugin.Blob.ViewModels
{
    #region enum
    public enum eLinkCommand
    {
        InputImageLink,
        Roi
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
        矩形,
        圆形,
    }
    public enum eRoiType
    {
        全图,
        ROI链接,
    }
    #endregion

    [Category("图像处理")]
    [DisplayName("斑点分析")]
    [ModuleImageName("Blob")]
    [Serializable]
    public class BlobViewModel : ModuleBase
    {

        public BlobViewModel()
        {
            //确保 ResultRegion 底层有效
            ResultRegion = new HRegion();
            ResultRegion.GenEmptyObj();
        }

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
                #region 输出数据初始化
                ResultRegion = new HRegion(); ResultRegion.GenEmptyObj();
                OutPutCount = 0;
                OutPutAreaAll = 0;
                OutPutArea = new List<double>();
                OutPutX = new List<double>();
                OutPutY = new List<double>();
                OutPutRoundness = new List<double>();
                OutPutRectangularity = new List<double>();
                OutPutWidth = new List<double>();
                OutPutHeight = new List<double>();
                OutPutPhi = new List<double>();
                if (IsOpenWindows)
                    RegionResultParams = new ObservableCollection<RegionResultParams>();
                #endregion

                // 1. 检查输入图像链接是否为空
                if (string.IsNullOrEmpty(InputImageLinkText))
                {
                    Logger.AddLog("斑点分析模块：输入图像链接为空");
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                // 2. 获取图像
                GetDispImage(InputImageLinkText);

                // 3. 验证图像对象有效性（关键：避免 HALCON #4056 异常）
                if (DispImage == null)
                {
                    Logger.AddLog($"斑点分析模块：获取到的图像对象为空，链接：{InputImageLinkText}");
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                try
                {
                    HTuple width, height;
                    DispImage.GetImageSize(out width, out height);
                    if (width.I <= 0 || height.I <= 0)
                        throw new Exception("图像尺寸无效");
                }
                catch (Exception ex)
                {
                   // Logger.AddLog($"斑点分析模块：输入图像无效（无法获取尺寸），链接：{InputImageLinkText}，错误：{ex.Message}");
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                if (m_PretreatHelp == null)
                    m_PretreatHelp = new PretreatHelp();

                HImage TempImage;
                HRegion region = new HRegion();
                HRegion regionThreshold = new HRegion();

                // 获取 ROI 区域（同样需要验证 ROI 链接的有效性，但 GetLinkValue 可能返回 null，需保护）
                try
                {
                    if (SelectedROIType == eRoiType.ROI链接 && !string.IsNullOrEmpty(InputRoiLinkText))
                    {
                        var roiObj = GetLinkValue(InputRoiLinkText);
                        if (roiObj is HRegion roiRegion && roiRegion != null && roiRegion.IsInitialized())
                            region = roiRegion;
                        else
                            region = DispImage.GetDomain();
                    }
                    else
                    {
                        region = DispImage.GetDomain();
                    }
                }
                catch (Exception ex)
                {
                    //Logger.AddLog($"斑点分析模块：获取图像区域失败，图像无效，链接：{InputImageLinkText}，错误：{ex.Message}");
                    ChangeModuleRunStatus(eRunStatus.NG);
                    return false;
                }

                TempImage = DispImage.ReduceDomain(region);
                regionThreshold = TempImage.Threshold((double)ThresholdMin, (double)ThresholdMax);

                if (DispImage != null && DispImage.IsInitialized())
                {
                    int i = 0;

                    HRegion outRegion = new HRegion();
                    if (m_ToolList.Count != 0)
                    {
                        foreach (var item in m_ToolList)
                        {
                            if (!item.m_enable)
                                continue;
                            int indexRegion = item.m_id - 1;
                            switch (item.m_name)
                            {
                                case eOperatorType.连通:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.Connection(regionThreshold, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.Connection(m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.合并:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.Union1(regionThreshold, out outRegion);
                                    }
                                    else if (item.m_Union1Index == "上一个区域")
                                    {
                                        m_PretreatHelp.Union1(m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.Union1(m_ToolList[Convert.ToInt32(item.m_Union1Index)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.孔洞填充:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.FillUp(regionThreshold, out outRegion);
                                    }
                                    else if (item.m_FillIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.FillUp(m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.FillUp(m_ToolList[Convert.ToInt32(item.m_FillIndex)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.开运算:
                                    #region
                                    if (item.m_StructuralElements == eStructuralElements.矩形)
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.OpeningRectangle1(regionThreshold, item.m_OpenWidth, item.m_OpenHeight, out outRegion);
                                        }
                                        else if (item.m_OpenIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.OpeningRectangle1(m_ToolList[indexRegion].ResultRegion, item.m_OpenWidth, item.m_OpenHeight, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.OpeningRectangle1(m_ToolList[Convert.ToInt32(item.m_OpenIndex)].ResultRegion, item.m_OpenWidth, item.m_OpenHeight, out outRegion);
                                        }
                                    }
                                    else
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.OpeningCircle(regionThreshold, item.m_OpenRadius, out outRegion);
                                        }
                                        else if (item.m_OpenIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.OpeningCircle(m_ToolList[indexRegion].ResultRegion, item.m_OpenRadius, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.OpeningCircle(m_ToolList[Convert.ToInt32(item.m_OpenIndex)].ResultRegion, item.m_OpenRadius, out outRegion);
                                        }
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.特征筛选:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.SelectShape(regionThreshold, item.m_FilterConditions,
                                            item.m_ConditionalRelationship, item.m_FeaturesMin, item.m_FeaturesMax, out outRegion);
                                    }
                                    else if (item.m_FeaturesIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.SelectShape(m_ToolList[indexRegion].ResultRegion, item.m_FilterConditions,
                                            item.m_ConditionalRelationship, item.m_FeaturesMin, item.m_FeaturesMax, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.SelectShape(m_ToolList[Convert.ToInt32(item.m_FeaturesIndex)].ResultRegion, item.m_FilterConditions,
                                            item.m_ConditionalRelationship, item.m_FeaturesMin, item.m_FeaturesMax, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.相交:
                                    #region
                                    if (indexRegion < 0 || (item.m_IntersectionIndex == "上一个区域" && item.m_IntersectionIndex2 == "上一个区域"))
                                    {
                                        item.ResultRegion = new HRegion(regionThreshold);
                                        continue;
                                    }
                                    else if (item.m_IntersectionIndex == "上一个区域" && item.m_IntersectionIndex2 != "上一个区域")
                                    {
                                        m_PretreatHelp.Intersection(m_ToolList[indexRegion].ResultRegion, m_ToolList[Convert.ToInt32(item.m_IntersectionIndex2)].ResultRegion, out outRegion);
                                    }
                                    else if (item.m_IntersectionIndex != "上一个区域" && item.m_IntersectionIndex2 == "上一个区域")
                                    {
                                        m_PretreatHelp.Intersection(m_ToolList[Convert.ToInt32(item.m_IntersectionIndex)].ResultRegion, m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.Intersection(m_ToolList[Convert.ToInt32(item.m_IntersectionIndex)].ResultRegion, m_ToolList[Convert.ToInt32(item.m_IntersectionIndex2)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.相减:
                                    #region
                                    if (indexRegion < 0 || (item.m_DifferenceIndex == "上一个区域" && item.m_DifferenceIndex2 == "上一个区域"))
                                    {
                                        item.ResultRegion = new HRegion(regionThreshold);
                                        continue;
                                    }
                                    else if (item.m_DifferenceIndex == "上一个区域" && item.m_DifferenceIndex2 != "上一个区域")
                                    {
                                        m_PretreatHelp.Difference(m_ToolList[indexRegion].ResultRegion, m_ToolList[Convert.ToInt32(item.m_DifferenceIndex2)].ResultRegion, out outRegion);
                                    }
                                    else if (item.m_DifferenceIndex != "上一个区域" && item.m_DifferenceIndex2 == "上一个区域")
                                    {
                                        m_PretreatHelp.Difference(m_ToolList[Convert.ToInt32(item.m_DifferenceIndex)].ResultRegion, m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.Difference(m_ToolList[Convert.ToInt32(item.m_DifferenceIndex)].ResultRegion, m_ToolList[Convert.ToInt32(item.m_DifferenceIndex2)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.矩形分割:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.PartitionRectangle(regionThreshold, item.m_DivisionWidth, item.m_DivisionHeight, out outRegion);
                                    }
                                    else if (item.m_DivisionIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.PartitionRectangle(m_ToolList[indexRegion].ResultRegion, item.m_DivisionWidth, item.m_DivisionHeight, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.PartitionRectangle(m_ToolList[Convert.ToInt32(item.m_DivisionIndex)].ResultRegion, item.m_DivisionWidth, item.m_DivisionHeight, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.腐蚀:
                                    #region
                                    // 修复：原代码中错误调用了 OpeningCircle/ErosionRectangle1，现已更正
                                    if (item.m_StructuralElements == eStructuralElements.矩形)
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.ErosionRectangle1(regionThreshold, item.m_ErosionWidth, item.m_ErosionHeight, out outRegion);
                                        }
                                        else if (item.m_ErosionIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.ErosionRectangle1(m_ToolList[indexRegion].ResultRegion, item.m_ErosionWidth, item.m_ErosionHeight, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.ErosionRectangle1(m_ToolList[Convert.ToInt32(item.m_ErosionIndex)].ResultRegion, item.m_ErosionWidth, item.m_ErosionHeight, out outRegion);
                                        }
                                    }
                                    else
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.ErosionCircle(regionThreshold, item.m_ErosionRadius, out outRegion);
                                        }
                                        else if (item.m_ErosionIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.ErosionCircle(m_ToolList[indexRegion].ResultRegion, item.m_ErosionRadius, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.ErosionCircle(m_ToolList[Convert.ToInt32(item.m_ErosionIndex)].ResultRegion, item.m_ErosionRadius, out outRegion);
                                        }
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.膨胀:
                                    #region
                                    // 修复：原代码中错误调用了 OpeningCircle/ErosionRectangle1，现已更正
                                    if (item.m_StructuralElements == eStructuralElements.矩形)
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.DilationRectangle1(regionThreshold, item.m_DilationWidth, item.m_DilationHeight, out outRegion);
                                        }
                                        else if (item.m_DilationIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.DilationRectangle1(m_ToolList[indexRegion].ResultRegion, item.m_DilationWidth, item.m_DilationHeight, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.DilationRectangle1(m_ToolList[Convert.ToInt32(item.m_DilationIndex)].ResultRegion, item.m_DilationWidth, item.m_DilationHeight, out outRegion);
                                        }
                                    }
                                    else
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.DilationCircle(regionThreshold, item.m_DilationRadius, out outRegion);
                                        }
                                        else if (item.m_DilationIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.DilationCircle(m_ToolList[indexRegion].ResultRegion, item.m_DilationRadius, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.DilationCircle(m_ToolList[Convert.ToInt32(item.m_DilationIndex)].ResultRegion, item.m_DilationRadius, out outRegion);
                                        }
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.获取最大区域:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.SelectShapeStd(regionThreshold, out outRegion);
                                    }
                                    else if (item.m_ShapeStdIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.SelectShapeStd(m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.SelectShapeStd(m_ToolList[Convert.ToInt32(item.m_ShapeStdIndex)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.补集:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.Complement(regionThreshold, out outRegion);
                                    }
                                    else if (item.m_ComplementIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.Complement(m_ToolList[indexRegion].ResultRegion, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.Complement(m_ToolList[Convert.ToInt32(item.m_ComplementIndex)].ResultRegion, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.转换:
                                    #region
                                    if (indexRegion < 0)
                                    {
                                        m_PretreatHelp.ShapeTrans(regionThreshold, item.m_ConversionType, out outRegion);
                                    }
                                    else if (item.m_ConversionIndex == "上一个区域")
                                    {
                                        m_PretreatHelp.ShapeTrans(m_ToolList[indexRegion].ResultRegion, item.m_ConversionType, out outRegion);
                                    }
                                    else
                                    {
                                        m_PretreatHelp.ShapeTrans(m_ToolList[Convert.ToInt32(item.m_ConversionIndex)].ResultRegion, item.m_ConversionType, out outRegion);
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                                case eOperatorType.闭运算:
                                    #region
                                    if (item.m_StructuralElements == eStructuralElements.矩形)
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.ClosingRectangle1(regionThreshold, item.m_CloseWidth, item.m_CloseHeight, out outRegion);
                                        }
                                        else if (item.m_CloseIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.ClosingRectangle1(m_ToolList[indexRegion].ResultRegion, item.m_CloseWidth, item.m_CloseHeight, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.ClosingRectangle1(m_ToolList[Convert.ToInt32(item.m_CloseIndex)].ResultRegion, item.m_CloseWidth, item.m_CloseHeight, out outRegion);
                                        }
                                    }
                                    else
                                    {
                                        if (indexRegion < 0)
                                        {
                                            m_PretreatHelp.ClosingCircle(regionThreshold, item.m_CloseRadius, out outRegion);
                                        }
                                        else if (item.m_CloseIndex == "上一个区域")
                                        {
                                            m_PretreatHelp.ClosingCircle(m_ToolList[indexRegion].ResultRegion, item.m_CloseRadius, out outRegion);
                                        }
                                        else
                                        {
                                            m_PretreatHelp.ClosingCircle(m_ToolList[Convert.ToInt32(item.m_CloseIndex)].ResultRegion, item.m_CloseRadius, out outRegion);
                                        }
                                    }
                                    item.ResultRegion = new HRegion(outRegion);
                                    #endregion
                                    break;
                            }
                            i++;
                        }

                        // 查找最后一个启用的工具，输出其结果
                        for (int J = m_ToolList.Count - 1; J >= 0; J--)
                        {
                            if (m_ToolList[J].m_enable)
                            {
                                HTuple hTuple = new HTuple();
                                List<string> list = new List<string>()
                                {
                                    "area",
                                    "row",
                                    "column",
                                    "roundness",
                                    "rectangularity",
                                    "width",
                                    "height",
                                    "phi",
                                };
                                foreach (var item in list)
                                {
                                    hTuple.Append(item);
                                }

                                HTuple hTuple1 = m_ToolList[J].ResultRegion.RegionFeatures(hTuple);
                                double[] doubles = hTuple1.ToDArr();
                                int index = doubles.Length / 8;
                                if (doubles.Length != 0)
                                {
                                    for (int j = 0; j < index; j++)
                                    {
                                        OutPutArea.Add(Math.Round(doubles[0 + 8 * j], 3));
                                        OutPutX.Add(Math.Round(doubles[2 + 8 * j], 3));
                                        OutPutY.Add(Math.Round(doubles[1 + 8 * j], 3));
                                        OutPutRoundness.Add(Math.Round(doubles[3 + 8 * j], 3));
                                        OutPutRectangularity.Add(Math.Round(doubles[4 + 8 * j], 3));
                                        OutPutWidth.Add(Math.Round(doubles[5 + 8 * j], 3));
                                        OutPutHeight.Add(Math.Round(doubles[6 + 8 * j], 3));
                                        OutPutPhi.Add(Math.Round(doubles[7 + 8 * j] * Math.PI / 180, 3));
                                        if (IsOpenWindows)
                                        {
                                            RegionResultParams.Add(new RegionResultParams()
                                            {
                                                ID = j,
                                                Area = Math.Round(doubles[0 + 8 * j], 3),
                                                Y = Math.Round(doubles[1 + 8 * j], 3),
                                                X = Math.Round(doubles[2 + 8 * j], 3),
                                                Roundness = Math.Round(doubles[3 + 8 * j], 3),
                                                Rectangularity = Math.Round(doubles[4 + 8 * j], 3),
                                                Width = Math.Round(doubles[5 + 8 * j], 3),
                                                Height = Math.Round(doubles[6 + 8 * j], 3),
                                                Phi = Math.Round(doubles[7 + 8 * j] * Math.PI / 180, 3),
                                            });
                                        }
                                    }
                                    ShowHRoi(
                                    new HRoi(
                                        ModuleParam.ModuleEncode,
                                        ModuleParam.ModuleName,
                                        ModuleParam.Remarks,
                                        HRoiType.检测结果,
                                        "red",
                                        new HObject(m_ToolList[J].ResultRegion)
                                    ));
                                    OutPutCount = m_ToolList[J].ResultRegion.CountObj();
                                    OutPutAreaAll = Math.Round(m_ToolList[J].ResultRegion.Union1().RegionFeatures("area"), 3);
                                    ResultRegion = new HRegion(m_ToolList[J].ResultRegion);
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 没有工具时直接输出阈值分割结果
                        HTuple hTuple = new HTuple();
                        List<string> list = new List<string>()
                        {
                            "area",
                            "row",
                            "column",
                            "roundness",
                            "rectangularity",
                            "width",
                            "height",
                            "phi",
                        };
                        foreach (var item in list)
                        {
                            hTuple.Append(item);
                        }
                        HTuple hTuple1 = regionThreshold.RegionFeatures(hTuple);
                        double[] doubles = hTuple1.ToDArr();
                        if (doubles.Length == 8)
                        {
                            OutPutArea.Add(Math.Round(doubles[0], 3));
                            OutPutX.Add(Math.Round(doubles[2], 3));
                            OutPutY.Add(Math.Round(doubles[1], 3));
                            OutPutRoundness.Add(Math.Round(doubles[3], 3));
                            OutPutRectangularity.Add(Math.Round(doubles[4], 3));
                            OutPutWidth.Add(Math.Round(doubles[5], 3));
                            OutPutHeight.Add(Math.Round(doubles[6], 3));
                            OutPutPhi.Add(Math.Round(doubles[7] * Math.PI / 180, 3));
                            OutPutCount = regionThreshold.CountObj();
                            OutPutAreaAll = Math.Round(doubles[0], 3);
                            ResultRegion = new HRegion(regionThreshold);
                            if (IsOpenWindows)
                            {
                                RegionResultParams.Add(new RegionResultParams()
                                {
                                    ID = 0,
                                    Area = Math.Round(doubles[0], 3),
                                    Y = Math.Round(doubles[1], 3),
                                    X = Math.Round(doubles[2], 3),
                                    Roundness = Math.Round(doubles[3], 3),
                                    Rectangularity = Math.Round(doubles[4], 3),
                                    Width = Math.Round(doubles[5], 3),
                                    Height = Math.Round(doubles[6], 3),
                                    Phi = Math.Round(doubles[7] * Math.PI / 180, 3),
                                });
                            }
                        }
                        ShowHRoi(
                        new HRoi(
                            ModuleParam.ModuleEncode,
                            ModuleParam.ModuleName,
                            ModuleParam.Remarks,
                            HRoiType.检测结果,
                            "red",
                            new HObject(regionThreshold)
                        ));
                    }

                    ShowHRoi();
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

        public override void AddOutputParams()
        {
            base.AddOutputParams();

            AddOutputParam("区域", "HRegion", ResultRegion);
            AddOutputParam("区域个数", "int", OutPutCount);
            AddOutputParam("区域总面积", "double", OutPutAreaAll);
            AddOutputParam("面积", "double[]", OutPutArea);
            AddOutputParam("X", "double[]", OutPutX);
            AddOutputParam("Y", "double[]", OutPutY);
            AddOutputParam("圆度", "double[]", OutPutRoundness);
            AddOutputParam("矩形度", "double[]", OutPutRectangularity);
            AddOutputParam("宽度", "double[]", OutPutWidth);
            AddOutputParam("高度", "double[]", OutPutHeight);
            AddOutputParam("角度", "double[]", OutPutPhi);

            // 添加结果区域最小外接矩形中心点
            try
            {
                if (ResultRegion != null && ResultRegion.IsInitialized() && ResultRegion.Area > 0)
                {
                    HRegion unionRegion;
                    m_PretreatHelp.Union1(ResultRegion, out unionRegion);
                    HTuple row1, col1, row2, col2;
                    unionRegion.SmallestRectangle1(out row1, out col1, out row2, out col2);
                    if (row1.Length > 0 && col1.Length > 0)
                    {
                        double centerRow = Math.Round((row1.D + row2.D) / 2, 3);
                        double centerCol = Math.Round((col1.D + col2.D) / 2, 3);
                        AddOutputParam("结果中心X", "double", centerCol);
                        AddOutputParam("结果中心Y", "double", centerRow);
                    }
                    else
                    {
                        AddOutputParam("结果中心X", "double", 0.0);
                        AddOutputParam("结果中心Y", "double", 0.0);
                    }
                    if (unionRegion != null) unionRegion.Dispose();
                }
                else
                {
                    AddOutputParam("结果中心X", "double", 0.0);
                    AddOutputParam("结果中心Y", "double", 0.0);
                }
            }
            catch (Exception ex)
            {
                Logger.GetExceptionMsg(ex);
                AddOutputParam("结果中心X", "double", 0.0);
                AddOutputParam("结果中心Y", "double", 0.0);
            }
        }

        #region Prop
        public PretreatHelp m_PretreatHelp = new PretreatHelp();
        public HRegion ResultRegion = new HRegion();
        public ObservableCollection<ModelData> m_ToolList { get; set; } = new ObservableCollection<ModelData>();

        private string _InputImageLinkText;
        public string InputImageLinkText
        {
            get { return _InputImageLinkText; }
            set
            {
                _InputImageLinkText = value;
                RaisePropertyChanged();
                GetDispImage(InputImageLinkText);
            }
        }

        private ModelData _SelectedText = new ModelData();
        public ModelData SelectedText
        {
            get { return _SelectedText; }
            set { Set(ref _SelectedText, value); }
        }

        private int _SelectedIndex;
        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set { Set(ref _SelectedIndex, value); }
        }

        private bool _IsDisp = false;
        public bool IsDisp
        {
            get { return _IsDisp; }
            set { Set(ref _IsDisp, value); }
        }

        private bool _ShowSearchRegion = true, _ShowResultRegion = true;
        public bool ShowSearchRegion
        {
            get { return _ShowSearchRegion; }
            set { Set(ref _ShowSearchRegion, value); }
        }
        public bool ShowResultRegion
        {
            get { return _ShowResultRegion; }
            set { Set(ref _ShowResultRegion, value); }
        }

        private int _ThresholdMin = 0, _ThresholdMax = 255;
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

        private bool _IsAdjustingMinThreshold = true;
        public bool IsAdjustingMinThreshold
        {
            get { return _IsAdjustingMinThreshold; }
            set { Set(ref _IsAdjustingMinThreshold, value); }
        }

        private ObservableCollection<RegionResultParams> _RegionResultParams = new ObservableCollection<RegionResultParams>();
        public ObservableCollection<RegionResultParams> RegionResultParams
        {
            get { return _RegionResultParams; }
            set { _RegionResultParams = value; RaisePropertyChanged(); }
        }

        private int _CurrentRow;
        public int CurrentRow
        {
            get { return _CurrentRow; }
            set { _CurrentRow = value; }
        }

        private eRoiType _SelectedROIType = eRoiType.全图;
        public eRoiType SelectedROIType
        {
            get { return _SelectedROIType; }
            set
            {
                Set(ref _SelectedROIType, value, new Action(() =>
                {
                    if (value == eRoiType.ROI链接 && InputRoiLinkText != null)
                    {
                        HRegion region = (HRegion)GetLinkValue(InputRoiLinkText);
                        if (ShowSearchRegion)
                            ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                        ShowHRoi();
                    }
                    else
                    {
                        var view = ModuleView as PerProcessingView;
                        view.mWindowH.HobjectToHimage(DispImage);
                    }
                }));
            }
        }

        private string _InputRoiLinkText;
        public string InputRoiLinkText
        {
            get { return _InputRoiLinkText; }
            set
            {
                Set(ref _InputRoiLinkText, value);
                HRegion region = (HRegion)GetLinkValue(_InputRoiLinkText);
                if (ShowSearchRegion)
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                ShowHRoi();
            }
        }

        public List<double> OutPutArea = new List<double>();
        public List<double> OutPutX = new List<double>();
        public List<double> OutPutY = new List<double>();
        public List<double> OutPutRoundness = new List<double>();
        public List<double> OutPutRectangularity = new List<double>();
        public List<double> OutPutWidth = new List<double>();
        public List<double> OutPutHeight = new List<double>();
        public List<double> OutPutPhi = new List<double>();
        public int OutPutCount = 0;
        public double OutPutAreaAll = 0;
        #endregion

        public override string HVSerialize()
        {
            JObject obj = JObject.Parse(base.HVSerialize());
            obj["InputImageLinkText"] = InputImageLinkText ?? "";
            obj["SelectedIndex"] = SelectedIndex;
            obj["IsDisp"] = IsDisp;
            obj["ShowSearchRegion"] = ShowSearchRegion;
            obj["ShowResultRegion"] = ShowResultRegion;
            obj["ThresholdMin"] = ThresholdMin;
            obj["ThresholdMax"] = ThresholdMax;
            obj["IsAdjustingMinThreshold"] = IsAdjustingMinThreshold;
            obj["CurrentRow"] = CurrentRow;
            obj["SelectedROIType"] = (int)SelectedROIType;
            obj["InputRoiLinkText"] = InputRoiLinkText ?? "";
            JArray arr = new JArray();
            if (m_ToolList != null)
            {
                foreach (var item in m_ToolList)
                {
                    JObject itemObj = new JObject();
                    itemObj["m_enable"] = item.m_enable;
                    itemObj["m_name"] = (int)item.m_name;
                    itemObj["m_id"] = item.m_id;
                    itemObj["m_ConditionalRelationship"] = (int)item.m_ConditionalRelationship;
                    itemObj["m_FilterConditions"] = (int)item.m_FilterConditions;
                    itemObj["m_StructuralElements"] = (int)item.m_StructuralElements;
                    itemObj["m_Union1Index"] = item.m_Union1Index ?? "上一个区域";
                    itemObj["m_ComplementIndex"] = item.m_ComplementIndex ?? "上一个区域";
                    itemObj["m_DifferenceIndex"] = item.m_DifferenceIndex ?? "上一个区域";
                    itemObj["m_DifferenceIndex2"] = item.m_DifferenceIndex2 ?? "上一个区域";
                    itemObj["m_IntersectionIndex"] = item.m_IntersectionIndex ?? "上一个区域";
                    itemObj["m_IntersectionIndex2"] = item.m_IntersectionIndex2 ?? "上一个区域";
                    itemObj["m_FillIndex"] = item.m_FillIndex ?? "上一个区域";
                    itemObj["m_OpenIndex"] = item.m_OpenIndex ?? "上一个区域";
                    itemObj["m_OpenWidth"] = item.m_OpenWidth;
                    itemObj["m_OpenHeight"] = item.m_OpenHeight;
                    itemObj["m_OpenRadius"] = item.m_OpenRadius;
                    itemObj["m_CloseIndex"] = item.m_CloseIndex ?? "上一个区域";
                    itemObj["m_CloseWidth"] = item.m_CloseWidth;
                    itemObj["m_CloseHeight"] = item.m_CloseHeight;
                    itemObj["m_CloseRadius"] = item.m_CloseRadius;
                    itemObj["m_ErosionIndex"] = item.m_ErosionIndex ?? "上一个区域";
                    itemObj["m_ErosionWidth"] = item.m_ErosionWidth;
                    itemObj["m_ErosionHeight"] = item.m_ErosionHeight;
                    itemObj["m_ErosionRadius"] = item.m_ErosionRadius;
                    itemObj["m_DilationIndex"] = item.m_DilationIndex ?? "上一个区域";
                    itemObj["m_DilationWidth"] = item.m_DilationWidth;
                    itemObj["m_DilationHeight"] = item.m_DilationHeight;
                    itemObj["m_DilationRadius"] = item.m_DilationRadius;
                    itemObj["m_FeaturesIndex"] = item.m_FeaturesIndex ?? "上一个区域";
                    itemObj["m_FeaturesMin"] = item.m_FeaturesMin;
                    itemObj["m_FeaturesMax"] = item.m_FeaturesMax;
                    itemObj["m_ConversionIndex"] = item.m_ConversionIndex ?? "上一个区域";
                    itemObj["m_ConversionType"] = (int)item.m_ConversionType;
                    itemObj["m_ShapeStdIndex"] = item.m_ShapeStdIndex ?? "上一个区域";
                    itemObj["m_DivisionIndex"] = item.m_DivisionIndex ?? "上一个区域";
                    itemObj["m_DivisionWidth"] = item.m_DivisionWidth;
                    itemObj["m_DivisionHeight"] = item.m_DivisionHeight;
                    arr.Add(itemObj);
                }
            }
            obj["m_ToolList"] = arr;
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
                if (obj["SelectedIndex"] != null) SelectedIndex = obj["SelectedIndex"].Value<int>();
                if (obj["IsDisp"] != null) IsDisp = obj["IsDisp"].Value<bool>();
                if (obj["ShowSearchRegion"] != null) ShowSearchRegion = obj["ShowSearchRegion"].Value<bool>();
                if (obj["ShowResultRegion"] != null) ShowResultRegion = obj["ShowResultRegion"].Value<bool>();
                if (obj["ThresholdMin"] != null) ThresholdMin = obj["ThresholdMin"].Value<int>();
                if (obj["ThresholdMax"] != null) ThresholdMax = obj["ThresholdMax"].Value<int>();
                if (obj["IsAdjustingMinThreshold"] != null) IsAdjustingMinThreshold = obj["IsAdjustingMinThreshold"].Value<bool>();
                if (obj["CurrentRow"] != null) CurrentRow = obj["CurrentRow"].Value<int>();
                if (obj["SelectedROIType"] != null) SelectedROIType = (eRoiType)obj["SelectedROIType"].Value<int>();
                if (obj["InputRoiLinkText"] != null) InputRoiLinkText = obj["InputRoiLinkText"].ToString();
                if (obj["m_ToolList"] != null)
                {
                    JArray arr = (JArray)obj["m_ToolList"];
                    m_ToolList.Clear();
                    foreach (var item in arr)
                    {
                        Blob.Models.ModelData md = new Blob.Models.ModelData();
                        if (item["m_enable"] != null) md.m_enable = item["m_enable"].Value<bool>();
                        if (item["m_name"] != null) md.m_name = (Blob.Models.eOperatorType)item["m_name"].Value<int>();
                        if (item["m_id"] != null) md.m_id = item["m_id"].Value<int>();
                        if (item["m_ConditionalRelationship"] != null) md.m_ConditionalRelationship = (Blob.Models.eConditionalRelationship)item["m_ConditionalRelationship"].Value<int>();
                        if (item["m_FilterConditions"] != null) md.m_FilterConditions = (Blob.Models.eFilterConditions)item["m_FilterConditions"].Value<int>();
                        if (item["m_StructuralElements"] != null) md.m_StructuralElements = (Blob.Models.eStructuralElements)item["m_StructuralElements"].Value<int>();
                        if (item["m_Union1Index"] != null) md.m_Union1Index = item["m_Union1Index"].ToString();
                        if (item["m_ComplementIndex"] != null) md.m_ComplementIndex = item["m_ComplementIndex"].ToString();
                        if (item["m_DifferenceIndex"] != null) md.m_DifferenceIndex = item["m_DifferenceIndex"].ToString();
                        if (item["m_DifferenceIndex2"] != null) md.m_DifferenceIndex2 = item["m_DifferenceIndex2"].ToString();
                        if (item["m_IntersectionIndex"] != null) md.m_IntersectionIndex = item["m_IntersectionIndex"].ToString();
                        if (item["m_IntersectionIndex2"] != null) md.m_IntersectionIndex2 = item["m_IntersectionIndex2"].ToString();
                        if (item["m_FillIndex"] != null) md.m_FillIndex = item["m_FillIndex"].ToString();
                        if (item["m_OpenIndex"] != null) md.m_OpenIndex = item["m_OpenIndex"].ToString();
                        if (item["m_OpenWidth"] != null) md.m_OpenWidth = item["m_OpenWidth"].Value<int>();
                        if (item["m_OpenHeight"] != null) md.m_OpenHeight = item["m_OpenHeight"].Value<int>();
                        if (item["m_OpenRadius"] != null) md.m_OpenRadius = item["m_OpenRadius"].Value<int>();
                        if (item["m_CloseIndex"] != null) md.m_CloseIndex = item["m_CloseIndex"].ToString();
                        if (item["m_CloseWidth"] != null) md.m_CloseWidth = item["m_CloseWidth"].Value<int>();
                        if (item["m_CloseHeight"] != null) md.m_CloseHeight = item["m_CloseHeight"].Value<int>();
                        if (item["m_CloseRadius"] != null) md.m_CloseRadius = item["m_CloseRadius"].Value<int>();
                        if (item["m_ErosionIndex"] != null) md.m_ErosionIndex = item["m_ErosionIndex"].ToString();
                        if (item["m_ErosionWidth"] != null) md.m_ErosionWidth = item["m_ErosionWidth"].Value<int>();
                        if (item["m_ErosionHeight"] != null) md.m_ErosionHeight = item["m_ErosionHeight"].Value<int>();
                        if (item["m_ErosionRadius"] != null) md.m_ErosionRadius = item["m_ErosionRadius"].Value<int>();
                        if (item["m_DilationIndex"] != null) md.m_DilationIndex = item["m_DilationIndex"].ToString();
                        if (item["m_DilationWidth"] != null) md.m_DilationWidth = item["m_DilationWidth"].Value<int>();
                        if (item["m_DilationHeight"] != null) md.m_DilationHeight = item["m_DilationHeight"].Value<int>();
                        if (item["m_DilationRadius"] != null) md.m_DilationRadius = item["m_DilationRadius"].Value<int>();
                        if (item["m_FeaturesIndex"] != null) md.m_FeaturesIndex = item["m_FeaturesIndex"].ToString();
                        if (item["m_FeaturesMin"] != null) md.m_FeaturesMin = item["m_FeaturesMin"].Value<double>();
                        if (item["m_FeaturesMax"] != null) md.m_FeaturesMax = item["m_FeaturesMax"].Value<double>();
                        if (item["m_ConversionIndex"] != null) md.m_ConversionIndex = item["m_ConversionIndex"].ToString();
                        if (item["m_ConversionType"] != null) md.m_ConversionType = (Blob.Models.eConversionType)item["m_ConversionType"].Value<int>();
                        if (item["m_ShapeStdIndex"] != null) md.m_ShapeStdIndex = item["m_ShapeStdIndex"].ToString();
                        if (item["m_DivisionIndex"] != null) md.m_DivisionIndex = item["m_DivisionIndex"].ToString();
                        if (item["m_DivisionWidth"] != null) md.m_DivisionWidth = item["m_DivisionWidth"].Value<int>();
                        if (item["m_DivisionHeight"] != null) md.m_DivisionHeight = item["m_DivisionHeight"].Value<int>();
                        m_ToolList.Add(md);
                    }
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"BlobViewModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }

        #region command
        public override void InitModule()
        {
            foreach (var item in m_ToolList)
            {
                item.ResultRegion = new HRegion();
            }
        }

        public override void Loaded()
        {
            base.Loaded();
            var view = ModuleView as PerProcessingView;
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
                    if (string.IsNullOrEmpty(InputImageLinkText)) return;
                }
                GetDispImage(InputImageLinkText);
                if (DispImage == null)
                {
                    Logger.AddLog("斑点分析模块加载失败：输入图像无效");
                    return;
                }
                try
                {
                    HTuple width, height;
                    DispImage.GetImageSize(out width, out height);
                    if (width.I <= 0 || height.I <= 0)
                        throw new Exception();
                }
                catch (Exception ex)
                {
                    Logger.GetExceptionMsg(ex);
                    Logger.AddLog("斑点分析模块：输入图像无效或未初始化");
                    return;
                }
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
        private CommandBase _ComposeCommand;
        public CommandBase ComposeCommand
        {
            get
            {
                if (_ComposeCommand == null)
                {
                    _ComposeCommand = new CommandBase((obj) =>
                    {
                        // 预留
                    });
                }
                return _ComposeCommand;
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
                        var view = this.ModuleView as PerProcessingView;
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
        private CommandBase _DataOperateCommand;
        public CommandBase DataOperateCommand
        {
            get
            {
                if (_DataOperateCommand == null)
                {
                    _DataOperateCommand = new CommandBase((obj) =>
                    {
                        string[] sArray = obj.ToString().Split('_');
                        if (sArray.Length == 2)
                        {
                            Add((eOperatorType)Enum.Parse(typeof(eOperatorType), sArray[1]));
                        }
                        else
                        {
                            switch (sArray[0])
                            {
                                case "remove":
                                    if (SelectedText == null) return;
                                    m_ToolList.Remove(SelectedText);
                                    if (m_ToolList.Count == 0)
                                        IsDisp = false;
                                    upset();
                                    break;
                                case "up":
                                    if (SelectedText == null) return;
                                    int i = m_ToolList.IndexOf(SelectedText);
                                    if (i > 0)
                                        m_ToolList.Move(i, i - 1);
                                    upset();
                                    break;
                                case "down":
                                    if (SelectedText == null) return;
                                    int j = m_ToolList.IndexOf(SelectedText);
                                    if (j + 1 < m_ToolList.Count)
                                        m_ToolList.Move(j, j + 1);
                                    upset();
                                    break;
                                default:
                                    break;
                            }
                        }
                        SelectedIndex = m_ToolList.Count - 1;
                    });
                }
                return _DataOperateCommand;
            }
        }

        private void upset()
        {
            List<string> list = new List<string>() { "上一个区域" };
            for (int i = 0; i < m_ToolList.Count; i++)
            {
                list = new List<string>() { "上一个区域" };
                m_ToolList[i].m_id = i;
                for (int j = 0; j < i; j++)
                {
                    list.Add(j.ToString());
                }
                switch (m_ToolList[i].m_name)
                {
                    case eOperatorType.连通:
                        break;
                    case eOperatorType.合并:
                        m_ToolList[i].Union1Index = list.ToArray();
                        m_ToolList[i].m_Union1Index = "上一个区域";
                        break;
                    case eOperatorType.补集:
                        m_ToolList[i].ComplementIndex = list.ToArray();
                        m_ToolList[i].m_ComplementIndex = "上一个区域";
                        break;
                    case eOperatorType.相减:
                        m_ToolList[i].DifferenceIndex = list.ToArray();
                        m_ToolList[i].m_DifferenceIndex = "上一个区域";
                        m_ToolList[i].DifferenceIndex2 = list.ToArray();
                        m_ToolList[i].m_DifferenceIndex2 = "上一个区域";
                        break;
                    case eOperatorType.相交:
                        m_ToolList[i].IntersectionIndex = list.ToArray();
                        m_ToolList[i].m_IntersectionIndex = "上一个区域";
                        m_ToolList[i].IntersectionIndex2 = list.ToArray();
                        m_ToolList[i].m_IntersectionIndex2 = "上一个区域";
                        break;
                    case eOperatorType.孔洞填充:
                        m_ToolList[i].FillIndex = list.ToArray();
                        m_ToolList[i].m_FillIndex = "上一个区域";
                        break;
                    case eOperatorType.开运算:
                        m_ToolList[i].OpenIndex = list.ToArray();
                        m_ToolList[i].m_OpenIndex = "上一个区域";
                        break;
                    case eOperatorType.闭运算:
                        m_ToolList[i].CloseIndex = list.ToArray();
                        m_ToolList[i].m_CloseIndex = "上一个区域";
                        break;
                    case eOperatorType.腐蚀:
                        m_ToolList[i].ErosionIndex = list.ToArray();
                        m_ToolList[i].m_ErosionIndex = "上一个区域";
                        break;
                    case eOperatorType.膨胀:
                        m_ToolList[i].DilationIndex = list.ToArray();
                        m_ToolList[i].m_DilationIndex = "上一个区域";
                        break;
                    case eOperatorType.特征筛选:
                        m_ToolList[i].FeaturesIndex = list.ToArray();
                        m_ToolList[i].m_FeaturesIndex = "上一个区域";
                        break;
                    case eOperatorType.转换:
                        m_ToolList[i].ConversionIndex = list.ToArray();
                        m_ToolList[i].m_ConversionIndex = "上一个区域";
                        break;
                    case eOperatorType.矩形分割:
                        m_ToolList[i].DivisionIndex = list.ToArray();
                        m_ToolList[i].m_DivisionIndex = "上一个区域";
                        break;
                    case eOperatorType.获取最大区域:
                        break;
                    default:
                        break;
                }
            }
        }

        private void Add(eOperatorType type)
        {
            List<string> list = new List<string>() { "上一个区域" };
            for (int i = 0; i < m_ToolList.Count; i++)
            {
                list.Add(i.ToString());
            }
            switch (type)
            {
                case eOperatorType.连通:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.合并:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        Union1Index = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.补集:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        ComplementIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.相减:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        DifferenceIndex = list.ToArray(),
                        DifferenceIndex2 = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.相交:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        IntersectionIndex = list.ToArray(),
                        IntersectionIndex2 = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.孔洞填充:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        FillIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.开运算:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        OpenIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.闭运算:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        CloseIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.腐蚀:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        ErosionIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.膨胀:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        DilationIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.特征筛选:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        FeaturesIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.转换:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        ConversionIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.矩形分割:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        DivisionIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                case eOperatorType.获取最大区域:
                    m_ToolList.Add(new ModelData()
                    {
                        m_name = type,
                        m_id = m_ToolList.Count(),
                        ShapeStdIndex = list.ToArray(),
                        ResultRegion = new HRegion()
                    });
                    break;
                default:
                    break;
            }
            IsDisp = true;
        }

        private void OnVarChanged(VarChangedEventParamModel obj)
        {
            switch (obj.SendName.Split(',')[1])
            {
                case "InputImageLink":
                    InputImageLinkText = obj.LinkName;
                    break;
                case "InputRoiLink":
                    InputRoiLinkText = obj.LinkName;
                    HRegion region = (HRegion)GetLinkValue(InputRoiLinkText);
                    ShowHRoi(new HRoi(ModuleParam.ModuleEncode, ModuleParam.ModuleName, ModuleParam.Remarks, HRoiType.搜索范围, "green", new HObject(region)));
                    ShowHRoi();
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
                            case eLinkCommand.Roi:
                                CommonMethods.GetModuleList(ModuleParam, VarLinkViewModel.Ins.Modules, "HRegion");
                                EventMgr.Ins.GetEvent<OpenVarLinkViewEvent>().Publish($"{ModuleGuid},InputRoiLink");
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

    [Serializable]
    public class RegionResultParams
    {
        public int ID { get; set; }
        public double Area { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Roundness { get; set; }
        public double Rectangularity { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Phi { get; set; }
    }
}