using HalconDotNet;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VM.Halcon.Config;
using HV.Common;
using HV.Common.Enums;
using System.Drawing;
using Newtonsoft.Json.Linq;

using HV.Common.Provide;
namespace HV.Services
{
    /// <summary>
    /// 自定义结构体
    /// </summary>
    [Serializable]
    public abstract class ROIBase
    {
        public string sColor { get; set; } = "cyan";
        public abstract HRegion genRegion();
        public abstract HXLDCont genXLD();
        public abstract HTuple getTuple();

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["sColor"] = sColor;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["sColor"] != null) sColor = obj["sColor"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"ROIBase.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 直线信息
    /// </summary>
    [Serializable]
    public class Line_Info : ROIBase, ICloneable
    {
        public bool Status;
        public double StartX;//起点列坐标
        public double StartY;//起点行坐标
        public double EndX;//终点列坐标
        public double EndY; //终点行坐标
        public double MidX;//中间点列坐标
        public double MidY;//中间点行坐标
        public double Phi;//方向
        public double Dist;//距离
        public double Nx;//列向量
        public double Ny;//行向量
        public double[] X;//列向量
        public double[] Y;//行向量
        public Line_Info()
        {
        }
        public Line_Info(double m_start_Row, double m_start_Col, double m_end_Row, double m_end_Col)
        {
            this.StartY = m_start_Row;
            this.StartX = m_start_Col;
            this.EndY = m_end_Row;
            this.EndX = m_end_Col;
            this.Ny = m_start_Col - m_end_Col;
            this.Nx = m_end_Row - m_start_Row;
            this.Dist = m_start_Col * m_end_Row - m_end_Col * m_start_Row;
            Phi = HMisc.AngleLx(StartY, StartX, EndY, EndX);
            MidY = (StartY + EndY) / 2;
            MidX = (StartX + EndX) / 2;
            Status = true;
        }
        public override HRegion genRegion()
        {
            HRegion h = new HRegion();
            h.GenRegionLine(StartY, StartX, EndY, EndX);
            return h;
        }
        public override HXLDCont genXLD()
        {
            HXLDCont xld = new HXLDCont();
            Gen.GenArrow(out xld, StartY, StartX, EndY, EndX, 10, 10);
            return xld;
        }
        public override HTuple getTuple()
        {
            double[] line = new double[] { StartY, StartX, EndY, EndX };
            return new HTuple(line);
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["StartX"] = StartX;
            obj["StartY"] = StartY;
            obj["EndX"] = EndX;
            obj["EndY"] = EndY;
            obj["MidX"] = MidX;
            obj["MidY"] = MidY;
            obj["Phi"] = Phi;
            obj["Dist"] = Dist;
            obj["Nx"] = Nx;
            obj["Ny"] = Ny;
            if (X != null) obj["X"] = JArray.FromObject(X);
            if (Y != null) obj["Y"] = JArray.FromObject(Y);
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["StartX"] != null) StartX = obj["StartX"].Value<double>();
                if (obj["StartY"] != null) StartY = obj["StartY"].Value<double>();
                if (obj["EndX"] != null) EndX = obj["EndX"].Value<double>();
                if (obj["EndY"] != null) EndY = obj["EndY"].Value<double>();
                if (obj["MidX"] != null) MidX = obj["MidX"].Value<double>();
                if (obj["MidY"] != null) MidY = obj["MidY"].Value<double>();
                if (obj["Phi"] != null) Phi = obj["Phi"].Value<double>();
                if (obj["Dist"] != null) Dist = obj["Dist"].Value<double>();
                if (obj["Nx"] != null) Nx = obj["Nx"].Value<double>();
                if (obj["Ny"] != null) Ny = obj["Ny"].Value<double>();
                if (obj["X"] != null) X = obj["X"].ToObject<double[]>();
                if (obj["Y"] != null) Y = obj["Y"].ToObject<double[]>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Line_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    };
    /// <summary>
    /// 面信息
    /// </summary>
    [Serializable]
    public struct Plane_Info
    {
        public double x, y, z;     //The distance from the origin to the centroid, as Measd along the x-axis.
        public double ax, by, cz, d;//Z + A*x + B*y + C =0  z's coefficient is just 1
        public double Angle;
        public double xAn, yAn, zAn;
        public double Flat, MinFlat, MaxFlat;
        public double MinZ, MaxZ;

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["x"] = x;
            obj["y"] = y;
            obj["z"] = z;
            obj["ax"] = ax;
            obj["by"] = by;
            obj["cz"] = cz;
            obj["d"] = d;
            obj["Angle"] = Angle;
            obj["xAn"] = xAn;
            obj["yAn"] = yAn;
            obj["zAn"] = zAn;
            obj["Flat"] = Flat;
            obj["MinFlat"] = MinFlat;
            obj["MaxFlat"] = MaxFlat;
            obj["MinZ"] = MinZ;
            obj["MaxZ"] = MaxZ;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["x"] != null) x = obj["x"].Value<double>();
                if (obj["y"] != null) y = obj["y"].Value<double>();
                if (obj["z"] != null) z = obj["z"].Value<double>();
                if (obj["ax"] != null) ax = obj["ax"].Value<double>();
                if (obj["by"] != null) by = obj["by"].Value<double>();
                if (obj["cz"] != null) cz = obj["cz"].Value<double>();
                if (obj["d"] != null) d = obj["d"].Value<double>();
                if (obj["Angle"] != null) Angle = obj["Angle"].Value<double>();
                if (obj["xAn"] != null) xAn = obj["xAn"].Value<double>();
                if (obj["yAn"] != null) yAn = obj["yAn"].Value<double>();
                if (obj["zAn"] != null) zAn = obj["zAn"].Value<double>();
                if (obj["Flat"] != null) Flat = obj["Flat"].Value<double>();
                if (obj["MinFlat"] != null) MinFlat = obj["MinFlat"].Value<double>();
                if (obj["MaxFlat"] != null) MaxFlat = obj["MaxFlat"].Value<double>();
                if (obj["MinZ"] != null) MinZ = obj["MinZ"].Value<double>();
                if (obj["MaxZ"] != null) MaxZ = obj["MaxZ"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Plane_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    };
    [Serializable]
    public struct TagVector
    {
        public double a, b, c;

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["a"] = a;
            obj["b"] = b;
            obj["c"] = c;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["a"] != null) a = obj["a"].Value<double>();
                if (obj["b"] != null) b = obj["b"].Value<double>();
                if (obj["c"] != null) c = obj["c"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"TagVector.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    };
    /// <summary>
    /// 圆信息
    /// </summary>
    [Serializable]
    public class Circle_Info : ROIBase, ICloneable
    {
        public bool Status;
        public double CenterY, CenterX, Radius;
        public double StartPhi = 0.0, EndPhi = Math.PI * 2;
        public string PointOrder = "positive";
        public Circle_Info()
        {
        }
        public Circle_Info(double m_Row_center, double m_Col_center, double m_Radius)
        {
            this.CenterY = m_Row_center;
            this.CenterX = m_Col_center;
            this.Radius = m_Radius;
            Status = true;
        }
        public Circle_Info(double m_Row_center, double m_Col_center, double m_Radius, double m_StartPhi, double m_EndPhi, string m_PointOrder)
        {
            this.CenterY = m_Row_center;
            this.CenterX = m_Col_center;
            this.Radius = m_Radius;
            this.StartPhi = m_StartPhi;
            this.EndPhi = m_EndPhi;
        }
        public override HRegion genRegion()
        {
            HRegion h = new HRegion();
            h.GenCircle(CenterY, CenterX, Radius);
            return h;
        }
        public override HXLDCont genXLD()
        {
            HXLDCont xld = new HXLDCont();
            xld.GenCircleContourXld(CenterY, CenterX, Radius, StartPhi, EndPhi, PointOrder, 1.0);
            return xld;
        }
        public override HTuple getTuple()
        {
            double[] circle = new double[] { CenterY, CenterX, Radius };
            return new HTuple(circle);
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["CenterY"] = CenterY;
            obj["CenterX"] = CenterX;
            obj["Radius"] = Radius;
            obj["StartPhi"] = StartPhi;
            obj["EndPhi"] = EndPhi;
            obj["PointOrder"] = PointOrder;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["CenterY"] != null) CenterY = obj["CenterY"].Value<double>();
                if (obj["CenterX"] != null) CenterX = obj["CenterX"].Value<double>();
                if (obj["Radius"] != null) Radius = obj["Radius"].Value<double>();
                if (obj["StartPhi"] != null) StartPhi = obj["StartPhi"].Value<double>();
                if (obj["EndPhi"] != null) EndPhi = obj["EndPhi"].Value<double>();
                if (obj["PointOrder"] != null) PointOrder = obj["PointOrder"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Circle_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 椭圆信息
    /// </summary>
    [Serializable]
    public class Ellipse_Info : ROIBase, ICloneable
    {
        public bool Status;
        public double CenterY, CenterX, Phi, Radius1, Radius2;
        double StartPhi = 0.0, EndPhi = Math.PI * 2;
        public string PointOrder = "positive";
        public Ellipse_Info()
        {
        }
        public Ellipse_Info(double m_Row_center, double m_Col_center, double m_Phi, double m_Radius1, double m_Radius2)
        {
            this.CenterY = m_Row_center;
            this.CenterX = m_Col_center;
            this.Phi = m_Phi;
            this.Radius1 = m_Radius1;
            this.Radius2 = m_Radius2;
            Status = true;
        }
        public override HRegion genRegion()
        {
            HRegion h = new HRegion();
            h.GenEllipse(CenterY, CenterX, Phi, Radius1, Radius2);
            return h;
        }
        public override HXLDCont genXLD()
        {
            HXLDCont xld = new HXLDCont();
            xld.GenEllipseContourXld(CenterY, CenterX, Phi, Radius1, Radius2, StartPhi, EndPhi, PointOrder, 1.0);
            return xld;
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }
        public override HTuple getTuple()
        {
            double[] ellipse = new double[] { CenterY, CenterX, Phi, Radius1, Radius2 };
            return new HTuple(ellipse);
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["CenterY"] = CenterY;
            obj["CenterX"] = CenterX;
            obj["Phi"] = Phi;
            obj["Radius1"] = Radius1;
            obj["Radius2"] = Radius2;
            obj["StartPhi"] = StartPhi;
            obj["EndPhi"] = EndPhi;
            obj["PointOrder"] = PointOrder;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["CenterY"] != null) CenterY = obj["CenterY"].Value<double>();
                if (obj["CenterX"] != null) CenterX = obj["CenterX"].Value<double>();
                if (obj["Phi"] != null) Phi = obj["Phi"].Value<double>();
                if (obj["Radius1"] != null) Radius1 = obj["Radius1"].Value<double>();
                if (obj["Radius2"] != null) Radius2 = obj["Radius2"].Value<double>();
                if (obj["StartPhi"] != null) StartPhi = obj["StartPhi"].Value<double>();
                if (obj["EndPhi"] != null) EndPhi = obj["EndPhi"].Value<double>();
                if (obj["PointOrder"] != null) PointOrder = obj["PointOrder"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Ellipse_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 添加自定义形状
    /// </summary>
    [Serializable]
    public class DrawRoi_Info : ROIBase
    {
        HRegion mHRegion;
        public DrawRoi_Info()
        {
        }
        public DrawRoi_Info(HRegion hregion)
        {
            mHRegion = hregion;
        }
        public override HRegion genRegion()
        {
            return mHRegion;
        }
        public override HXLDCont genXLD()
        {
            if (mHRegion != null && mHRegion.IsInitialized())
            {
                return mHRegion.GenContourRegionXld("border_holes");
            }
            else
            {
                return new HXLDCont();
            }
        }
        public override HTuple getTuple()
        {
            throw new NotImplementedException();
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"DrawRoi_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 矩形信息
    /// </summary>
    [Serializable]
    public class Rect_Info : ROIBase
    {
        public bool Status;
        public double StartY, StartX, EndY, EndX;
        public Rect_Info()
        {
        }
        public Rect_Info(double m_Row_Start, double m_Col_Start, double m_Row_End, double m_Column_End)
        {
            this.StartY = m_Row_Start;
            this.StartX = m_Col_Start;
            this.EndY = m_Row_End;
            this.EndX = m_Column_End;
            Status = true;
        }
        public override HRegion genRegion()
        {
            HRegion h = new HRegion();
            h.GenRectangle1(StartY, StartX, EndY, EndX);
            return h;
        }
        public override HXLDCont genXLD()
        {
            HXLDCont xld = new HXLDCont();
            HTuple row = new HTuple(StartY, EndY, EndY, StartY, StartY);
            HTuple col = new HTuple(StartX, StartX, EndX, EndX, StartX);
            xld.GenContourPolygonXld(row, col);
            return xld;
        }
        public override HTuple getTuple()
        {
            double[] rect1 = new double[] { StartY, StartX, EndY, EndX };
            return new HTuple(rect1);
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["StartY"] = StartY;
            obj["StartX"] = StartX;
            obj["EndY"] = EndY;
            obj["EndX"] = EndX;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["StartY"] != null) StartY = obj["StartY"].Value<double>();
                if (obj["StartX"] != null) StartX = obj["StartX"].Value<double>();
                if (obj["EndY"] != null) EndY = obj["EndY"].Value<double>();
                if (obj["EndX"] != null) EndX = obj["EndX"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Rect_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 旋转矩形信息
    /// </summary>
    [Serializable]
    public class Rect2_Info : ROIBase, ICloneable
    {
        public bool Status;
        public double CenterY;
        public double CenterX;
        public double Phi;
        public double Length1;
        public double Length2;
        public Rect2_Info()
        {
        }
        public Rect2_Info(double m_Row_center, double m_Col_center, double m_Phi, double m_Length1, double m_Length2)
        {
            this.CenterY = m_Row_center;
            this.CenterX = m_Col_center;
            this.Phi = m_Phi;
            this.Length1 = m_Length1;
            this.Length2 = m_Length2;
            Status = true;
        }
        public override HRegion genRegion()
        {
            HRegion h = new HRegion();
            h.GenRectangle2(CenterY, CenterX, Phi, Length1, Length2);
            return h;
        }
        public override HXLDCont genXLD()
        {
            HXLDCont xld = new HXLDCont();
            xld.GenRectangle2ContourXld(CenterY, CenterX, Phi, Length1, Length2);
            return xld;
        }
        public override HTuple getTuple()
        {
            double[] rect2 = new double[] { CenterY, CenterX, Phi, Length1, Length2 };
            return new HTuple(rect2);
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["CenterY"] = CenterY;
            obj["CenterX"] = CenterX;
            obj["Phi"] = Phi;
            obj["Length1"] = Length1;
            obj["Length2"] = Length2;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["CenterY"] != null) CenterY = obj["CenterY"].Value<double>();
                if (obj["CenterX"] != null) CenterX = obj["CenterX"].Value<double>();
                if (obj["Phi"] != null) Phi = obj["Phi"].Value<double>();
                if (obj["Length1"] != null) Length1 = obj["Length1"].Value<double>();
                if (obj["Length2"] != null) Length2 = obj["Length2"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Rect2_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 3D点数据
    /// </summary>
    [Serializable]
    public struct Point3DF //3D点数据
    {
        public float X;
        public float Y;
        public float Z;
        public Point3DF(float _x, float _y, float _z)
        {
            this.X = _x;
            this.Y = _y;
            this.Z = _z;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["X"] = X;
            obj["Y"] = Y;
            obj["Z"] = Z;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["X"] != null) X = obj["X"].Value<float>();
                if (obj["Y"] != null) Y = obj["Y"].Value<float>();
                if (obj["Z"] != null) Z = obj["Z"].Value<float>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Point3DF.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 矩形阵列返回的信息
    /// </summary>
    [Serializable]
    public struct RectRoiInfo
    {
        public bool Status;
        public double X;//mm坐标
        public double Y;//mm坐标
        public double Value_Avg;///均值
        public double Value_Median;///中指
        public double Value_Max;///最大值
        public double Value_Min;///最小值
        public List<double> X_List;//x mm坐标
        public List<double> Y_List;//y mm坐标
        public List<double> Value_List;
        public RectRoiInfo(double _x, double _y, double _avg, double _median, double _max, double _min, List<double> _xList, List<double> _yList, List<double> _valueList)
        {
            X = _x;
            Y = _y;
            Value_Avg = _avg;
            Value_Median = _median;
            Value_Max = _max;
            Value_Min = _min;
            X_List = _xList;
            Y_List = _yList;
            Value_List = _valueList;
            Status = true;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["X"] = X;
            obj["Y"] = Y;
            obj["Value_Avg"] = Value_Avg;
            obj["Value_Median"] = Value_Median;
            obj["Value_Max"] = Value_Max;
            obj["Value_Min"] = Value_Min;
            if (X_List != null) obj["X_List"] = JArray.FromObject(X_List);
            if (Y_List != null) obj["Y_List"] = JArray.FromObject(Y_List);
            if (Value_List != null) obj["Value_List"] = JArray.FromObject(Value_List);
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["X"] != null) X = obj["X"].Value<double>();
                if (obj["Y"] != null) Y = obj["Y"].Value<double>();
                if (obj["Value_Avg"] != null) Value_Avg = obj["Value_Avg"].Value<double>();
                if (obj["Value_Median"] != null) Value_Median = obj["Value_Median"].Value<double>();
                if (obj["Value_Max"] != null) Value_Max = obj["Value_Max"].Value<double>();
                if (obj["Value_Min"] != null) Value_Min = obj["Value_Min"].Value<double>();
                if (obj["X_List"] != null) X_List = obj["X_List"].ToObject<List<double>>();
                if (obj["Y_List"] != null) Y_List = obj["Y_List"].ToObject<List<double>>();
                if (obj["Value_List"] != null) Value_List = obj["Value_List"].ToObject<List<double>>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"RectRoiInfo.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 十字坐标信息
    /// </summary>
    [Serializable]
    public struct Coord_Info
    {
        public bool Status;
        public double Y, X, Phi,Score;
        public Coord_Info(double _row, double _col, double _phi ,double _score)
        {
            Y = _row;
            X = _col;
            Phi = _phi;//坐标系X轴与图像X轴正方向的夹角
            Score = _score;
            Status = true;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Y"] = Y;
            obj["X"] = X;
            obj["Phi"] = Phi;
            obj["Score"] = Score;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["Y"] != null) Y = obj["Y"].Value<double>();
                if (obj["X"] != null) X = obj["X"].Value<double>();
                if (obj["Phi"] != null) Phi = obj["Phi"].Value<double>();
                if (obj["Score"] != null) Score = obj["Score"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Coord_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 标定信息
    /// </summary>
    [Serializable]
    public struct Cal_Info
    {
        public bool Status;
        /// <summary>平移X</summary>
        public double ParallelX;
        /// <summary>平移Y</summary>
        public double ParallelY;
        /// <summary>像素当量X</summary>
        public double PixelX;
        /// <summary>像素当量Y</summary>
        public double PixelY;
        /// <summary>旋转角度</summary>
        public double RotationAngle;
        /// <summary>倾斜角度</summary>
        public double TiltAngle;
        /// <summary>RMS平分</summary>
        public double RMS;
        /// <summary>旋转中心X</summary>
        public double RrotationCenterX;
        /// <summary>旋转中心Y</summary>
        public double RrotationCenterY;
        /// <summary>旋转启用</summary>
        public bool RotatingEnabled;
        /// <summary>方向一致</summary>
        public bool SameDirection;
        /// <summary>相机固定</summary>
        public bool CameraFix;
        /// <summary>MarkX</summary>
        public double MarkX;
        /// <summary>MarkY</summary>
        public double MarkY;
        /// <summary>基准X</summary>
        public double BaselineX;
        /// <summary>基准Y</summary>
        public double BaselineY;
        /// <summary>基准角度</summary>
        public double BaselineAngel;
        public string GetName()
        {
            return "ParallelX";
        }
        /// <summary>
        /// 标定信息
        /// 方便流程取数据：平移X,平移Y,像素当量X,像素当量Y,旋转角度,倾斜角度,RMS,旋转中心X,旋转中心Y,旋转启用,方向一致,相机固定,MarkX,MarkY,基准X,基准Y,基准角度
        /// </summary>
        /// <param name="_ParallelX">平移X</param>
        /// <param name="_ParallelY">平移Y</param>
        /// <param name="_PixelX">像素当量X</param>
        /// <param name="_PixelY">像素当量Y</param>
        /// <param name="_RotationAngle">旋转角度</param>
        /// <param name="_TiltAngle">倾斜角度</param>
        /// <param name="_RMS">RMS平分</param>
        /// <param name="_RrotationCenterX">旋转中心X</param>
        /// <param name="_RrotationCenterY">旋转中心Y</param>
        /// <param name="_RotatingEnabled">旋转启用</param>
        /// <param name="_SameDirection">方向一致</param>
        public Cal_Info(double _ParallelX, double _ParallelY, double _PixelX, double _PixelY, double _RotationAngle, double _TiltAngle, double _RMS, double _RrotationCenterX, double _RrotationCenterY, bool _RotatingEnabled, bool _SameDirection,
            bool _CameraFix, double _MarkX, double _MarkY, double _BaselineX, double _BaselineY, double _BaselineAngel)
        {
            ParallelX = _ParallelX;
            ParallelY = _ParallelY;
            PixelX = _PixelX;
            PixelY = _PixelY;
            RotationAngle = _RotationAngle;
            TiltAngle = _TiltAngle;
            RMS = _RMS;
            RrotationCenterX = _RrotationCenterX;
            RrotationCenterY = _RrotationCenterY;
            RotatingEnabled = _RotatingEnabled;
            SameDirection = _SameDirection;
            CameraFix = _CameraFix;
            MarkX = _MarkX;
            MarkY = _MarkY;
            BaselineX = _BaselineX;
            BaselineY = _BaselineY;
            BaselineAngel = _BaselineAngel;
            Status = true;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["ParallelX"] = ParallelX;
            obj["ParallelY"] = ParallelY;
            obj["PixelX"] = PixelX;
            obj["PixelY"] = PixelY;
            obj["RotationAngle"] = RotationAngle;
            obj["TiltAngle"] = TiltAngle;
            obj["RMS"] = RMS;
            obj["RrotationCenterX"] = RrotationCenterX;
            obj["RrotationCenterY"] = RrotationCenterY;
            obj["RotatingEnabled"] = RotatingEnabled;
            obj["SameDirection"] = SameDirection;
            obj["CameraFix"] = CameraFix;
            obj["MarkX"] = MarkX;
            obj["MarkY"] = MarkY;
            obj["BaselineX"] = BaselineX;
            obj["BaselineY"] = BaselineY;
            obj["BaselineAngel"] = BaselineAngel;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["ParallelX"] != null) ParallelX = obj["ParallelX"].Value<double>();
                if (obj["ParallelY"] != null) ParallelY = obj["ParallelY"].Value<double>();
                if (obj["PixelX"] != null) PixelX = obj["PixelX"].Value<double>();
                if (obj["PixelY"] != null) PixelY = obj["PixelY"].Value<double>();
                if (obj["RotationAngle"] != null) RotationAngle = obj["RotationAngle"].Value<double>();
                if (obj["TiltAngle"] != null) TiltAngle = obj["TiltAngle"].Value<double>();
                if (obj["RMS"] != null) RMS = obj["RMS"].Value<double>();
                if (obj["RrotationCenterX"] != null) RrotationCenterX = obj["RrotationCenterX"].Value<double>();
                if (obj["RrotationCenterY"] != null) RrotationCenterY = obj["RrotationCenterY"].Value<double>();
                if (obj["RotatingEnabled"] != null) RotatingEnabled = obj["RotatingEnabled"].Value<bool>();
                if (obj["SameDirection"] != null) SameDirection = obj["SameDirection"].Value<bool>();
                if (obj["CameraFix"] != null) CameraFix = obj["CameraFix"].Value<bool>();
                if (obj["MarkX"] != null) MarkX = obj["MarkX"].Value<double>();
                if (obj["MarkY"] != null) MarkY = obj["MarkY"].Value<double>();
                if (obj["BaselineX"] != null) BaselineX = obj["BaselineX"].Value<double>();
                if (obj["BaselineY"] != null) BaselineY = obj["BaselineY"].Value<double>();
                if (obj["BaselineAngel"] != null) BaselineAngel = obj["BaselineAngel"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Cal_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 测量信息- 长/2,宽/2,阈值,间隔,参数名,参数值,点顺序 (0位默认，1 顺时针，2 逆时针)
    /// </summary>
    [Serializable]
    public struct Meas_Info
    {
        /// <summary>长/2</summary>
        public double Length1;
        /// <summary>宽/2</summary>
        public double Length2;
        /// <summary>阈值</summary>
        public double Threshold;
        /// <summary>间隔</summary>
        public double MeasDis;
        /// <summary>参数名</summary>
        public HTuple ParamName;
        /// <summary>参数值</summary>
        public HTuple ParamValue;
        /// <summary>点顺序 0位默认,1顺时针,2逆时针</summary>
        public int PointsOrder;
        public Meas_Info(double _length1, double _length2, double _threshold, double _MeasDis, HTuple _paraName, HTuple _paraValue, int _pointsOrder)
        {
            Length1 = _length1;
            Length2 = _length2;
            Threshold = _threshold;
            MeasDis = _MeasDis;
            ParamName = _paraName;
            ParamValue = _paraValue;
            PointsOrder = _pointsOrder;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Length1"] = Length1;
            obj["Length2"] = Length2;
            obj["Threshold"] = Threshold;
            obj["MeasDis"] = MeasDis;
            obj["PointsOrder"] = PointsOrder;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Length1"] != null) Length1 = obj["Length1"].Value<double>();
                if (obj["Length2"] != null) Length2 = obj["Length2"].Value<double>();
                if (obj["Threshold"] != null) Threshold = obj["Threshold"].Value<double>();
                if (obj["MeasDis"] != null) MeasDis = obj["MeasDis"].Value<double>();
                if (obj["PointsOrder"] != null) PointsOrder = obj["PointsOrder"].Value<int>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Meas_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 测量信息- 长/2,宽/2,阈值,间隔,参数名,参数值,点顺序 (0位默认，1 顺时针，2 逆时针)
    /// </summary>
    [Serializable]
    public class Text_Info
    {
        /// <summary>名称</summary>
        public string Name { set; get; }
        /// <summary>链接</summary>
        public string Likes { set; get; }
        /// <summary>值</summary>
        public string Value { set; get; }
        public Text_Info(string _Name, string _Likes, string _Value)
        {
            Name = _Name;
            Likes = _Likes;
            Value = _Value;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Name"] = Name;
            obj["Likes"] = Likes;
            obj["Value"] = Value;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Name"] != null) Name = obj["Name"].Value<string>();
                if (obj["Likes"] != null) Likes = obj["Likes"].Value<string>();
                if (obj["Value"] != null) Value = obj["Value"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Text_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 返回信息-
    /// </summary>
    [Serializable]
    public class Rtn_Info
    {
        public bool Status;
        /// <summary>值</summary>
        public string Value;
        public Rtn_Info() { }
        public Rtn_Info(string _Value)
        {
            Value = _Value;
            Status = true;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Value"] = Value;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["Value"] != null) Value = obj["Value"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Rtn_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>显示信息- </summary>
    [Serializable]
    public class Set_Info
    {
        /// <summary>状态</summary>
        public string Status;
        /// <summary>对齐</summary>
        public eAlignMode Align;
        /// <summary>位置</summary>
        public int X, Y;
        /// <summary>内容</summary>
        public string Msg;
        /// <summary>前缀</summary>
        public string Prefix;
        /// <summary>后缀</summary>
        public string Suffix;
        /// <summary>OK颜色</summary>
        public Color OK;
        /// <summary>NG颜色</summary>
        public Color NG;
        public Set_Info() { }
        public Set_Info(string _Status, eAlignMode _Align, int _X, int _Y, string _Msg, string _Prefix, string _Suffix, Color _OK, Color _NG)
        {
            Status = _Status;
            Align = _Align;
            X = _X;
            Y = _Y;
            Msg = _Msg;
            Prefix = _Prefix;
            Suffix = _Suffix;
            OK = _OK;
            NG = _NG;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Align"] = (int)Align;
            obj["X"] = X;
            obj["Y"] = Y;
            obj["Msg"] = Msg;
            obj["Prefix"] = Prefix;
            obj["Suffix"] = Suffix;
            obj["OK"] = OK.ToArgb();
            obj["NG"] = NG.ToArgb();
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<string>();
                if (obj["Align"] != null) Align = (eAlignMode)obj["Align"].Value<int>();
                if (obj["X"] != null) X = obj["X"].Value<int>();
                if (obj["Y"] != null) Y = obj["Y"].Value<int>();
                if (obj["Msg"] != null) Msg = obj["Msg"].Value<string>();
                if (obj["Prefix"] != null) Prefix = obj["Prefix"].Value<string>();
                if (obj["Suffix"] != null) Suffix = obj["Suffix"].Value<string>();
                if (obj["OK"] != null) OK = Color.FromArgb(obj["OK"].Value<int>());
                if (obj["NG"] != null) NG = Color.FromArgb(obj["NG"].Value<int>());
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Set_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>亮度信息- </summary>
    [Serializable]
    public class Luma_Info
    {
        public bool Status;
        /// <summary>面积</summary>
        public double Area;
        /// <summary>平均</summary>
        public double Mean;
        /// <summary>最小</summary>
        public double Min;
        /// <summary>最大</summary>
        public double Max;
        /// <summary>范围</summary>
        public double Range;
        public Luma_Info() { }
        public Luma_Info(double _Area, double _Mean, double _Min, double _Max, double _Range)
        {
            Area = _Area;
            Mean = _Mean;
            Min = _Min;
            Max = _Max;
            Range = _Range;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Area"] = Area;
            obj["Mean"] = Mean;
            obj["Min"] = Min;
            obj["Max"] = Max;
            obj["Range"] = Range;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["Area"] != null) Area = obj["Area"].Value<double>();
                if (obj["Mean"] != null) Mean = obj["Mean"].Value<double>();
                if (obj["Min"] != null) Min = obj["Min"].Value<double>();
                if (obj["Max"] != null) Max = obj["Max"].Value<double>();
                if (obj["Range"] != null) Range = obj["Range"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Luma_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }


    /// <summary>保存信息- </summary>
    [Serializable]
    public class Save_Info
    {
        /// <summary>索引</summary>
        public int Index { set; get; }
        /// <summary>名称</summary>
        public string Name { set; get; }
        /// <summary>内容</summary>
        public string Msg { set; get; }
        /// <summary>注释</summary>
        public string Remark { set; get; }
        public Save_Info() { }
        public Save_Info(int _Index, string _Name, string _Msg, string _Remark)
        {
            Index = _Index;
            Name = _Name;
            Msg = _Msg;
            Remark = _Remark;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Index"] = Index;
            obj["Name"] = Name;
            obj["Msg"] = Msg;
            obj["Remark"] = Remark;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Index"] != null) Index = obj["Index"].Value<int>();
                if (obj["Name"] != null) Name = obj["Name"].Value<string>();
                if (obj["Msg"] != null) Msg = obj["Msg"].Value<string>();
                if (obj["Remark"] != null) Remark = obj["Remark"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Save_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>保存信息- </summary>
    [Serializable]
    public class Char_Info
    {
        public bool Status = false;
        /// <summary>索引</summary>
        public int Index { set; get; }
        /// <summary>名称</summary>
        public string Name { set; get; }
        /// <summary>链接1</summary>
        public string Link1 { set; get; }
        /// <summary>运算符号</summary>
        public string CharType { set; get; }
        /// <summary>链接2</summary>
        public string Link2 { set; get; }

        /// <summary>结果</summary>
        public string Result { set; get; }
        /// <summary>注释</summary>
        public string Remark { set; get; }
        public Char_Info() { }
        public Char_Info(int _Index, string _Name, string _Link1, string _CharType, string _Link2, string _Result, string _Remark)
        {
            Index = _Index;
            Name = _Name;
            Link1 = _Link1;
            CharType = _CharType;
            Link2 = _Link2;
            Result = _Result;
            Remark = _Remark;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Index"] = Index;
            obj["Name"] = Name;
            obj["Link1"] = Link1;
            obj["CharType"] = CharType;
            obj["Link2"] = Link2;
            obj["Result"] = Result;
            obj["Remark"] = Remark;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["Index"] != null) Index = obj["Index"].Value<int>();
                if (obj["Name"] != null) Name = obj["Name"].Value<string>();
                if (obj["Link1"] != null) Link1 = obj["Link1"].Value<string>();
                if (obj["CharType"] != null) CharType = obj["CharType"].Value<string>();
                if (obj["Link2"] != null) Link2 = obj["Link2"].Value<string>();
                if (obj["Result"] != null) Result = obj["Result"].Value<string>();
                if (obj["Remark"] != null) Remark = obj["Remark"].Value<string>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Char_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>点到点信息保存- </summary>
    [Serializable]
    public class PtoP_Info
    {
        public bool Status = false;
        /// <summary>名称</summary>
        public string Name;
        /// <summary>中心X</summary>
        public double CentreX;
        /// <summary>中心Y</summary>
        public double CentreY;
        /// <summary>角度</summary>
        public double Angle;
        /// <summary>距离</summary>
        public double Dis;
        public PtoP_Info() { }
        /// <summary>
        /// 点到点信息
        /// </summary>
        /// <param name="_Name">名称</param>
        /// <param name="_CentreX">中心X</param>
        /// <param name="_CentreY">中心Y</param>
        /// <param name="_Angle">角度</param>
        /// <param name="_Dis">距离</param>
        public PtoP_Info(string _Name, double _CentreX, double _CentreY, double _Angle, double _Dis)
        {
            Name = _Name;
            CentreX = _CentreX;
            CentreY = _CentreY;
            Angle = _Angle;
            Dis = _Dis;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["Name"] = Name;
            obj["CentreX"] = CentreX;
            obj["CentreY"] = CentreY;
            obj["Angle"] = Angle;
            obj["Dis"] = Dis;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["Name"] != null) Name = obj["Name"].Value<string>();
                if (obj["CentreX"] != null) CentreX = obj["CentreX"].Value<double>();
                if (obj["CentreY"] != null) CentreY = obj["CentreY"].Value<double>();
                if (obj["Angle"] != null) Angle = obj["Angle"].Value<double>();
                if (obj["Dis"] != null) Dis = obj["Dis"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"PtoP_Info.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    /// <summary>
    /// 九点信息
    /// 索引 像素X,Y;机械X,Y
    /// </summary>
    [Serializable]
    public class NPoint
    {
        /// <summary>索引</summary>
        public int Index { set; get; }
        /// <summary>像素X</summary>
        public double ImageX { set; get; }
        /// <summary>像素Y</summary>
        public double ImageY { set; get; }
        /// <summary>机械X</summary>
        public double RobotX { set; get; }
        /// <summary>机械Y</summary>
        public double RobotY { set; get; }
        /// <summary>
        /// 九点信息
        /// 索引 像素X,Y;机械X,Y
        /// </summary>
        public NPoint() { }
        /// <summary>
        /// 九点信息
        /// 索引 像素X,Y;机械X,Y
        /// </summary>
        public NPoint(int _Index, double _ImageX, double _ImageY, double _RobotX, double _RobotY)
        {
            Index = _Index;
            ImageX = _ImageX;
            ImageY = _ImageY;
            RobotX = _RobotX;
            RobotY = _RobotY;
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Index"] = Index;
            obj["ImageX"] = ImageX;
            obj["ImageY"] = ImageY;
            obj["RobotX"] = RobotX;
            obj["RobotY"] = RobotY;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Index"] != null) Index = obj["Index"].Value<int>();
                if (obj["ImageX"] != null) ImageX = obj["ImageX"].Value<double>();
                if (obj["ImageY"] != null) ImageY = obj["ImageY"].Value<double>();
                if (obj["RobotX"] != null) RobotX = obj["RobotX"].Value<double>();
                if (obj["RobotY"] != null) RobotY = obj["RobotY"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"NPoint.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
    [Serializable]
    public class RPoint
    {
        public bool Status = false;
        public double X;
        public double Y;
        public double R;
        public double[] X1;
        public double[] Y1;
        public RPoint() { }
        public RPoint(double x, double y, double r)
        {
            this.X = x;
            this.Y = y;
            this.R = r;
        }
        public RPoint(double[] x, double[] y)
        {
            this.X1 = x;
            this.Y1 = y;
        }
        /// <summary>重写点</summary>
        public static RPoint operator -(RPoint p1, RPoint p2)
        {
            return new RPoint(p1.X - p2.X, p1.Y - p2.Y, p1.R - p2.R);
        }
        /// <summary>获得点矢量长度</summary>
        public double GetDistance
        {
            get
            {
                return Math.Sqrt(X * X + Y * Y);
            }
        }
        public string ToShowTip()
        {
            return X.ToString() + " | " + Y.ToString();
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Status"] = Status;
            obj["X"] = X;
            obj["Y"] = Y;
            obj["R"] = R;
            if (X1 != null) obj["X1"] = JArray.FromObject(X1);
            if (Y1 != null) obj["Y1"] = JArray.FromObject(Y1);
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Status"] != null) Status = obj["Status"].Value<bool>();
                if (obj["X"] != null) X = obj["X"].Value<double>();
                if (obj["Y"] != null) Y = obj["Y"].Value<double>();
                if (obj["R"] != null) R = obj["R"].Value<double>();
                if (obj["X1"] != null) X1 = obj["X1"].ToObject<double[]>();
                if (obj["Y1"] != null) Y1 = obj["Y1"].ToObject<double[]>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"RPoint.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
    }
}
