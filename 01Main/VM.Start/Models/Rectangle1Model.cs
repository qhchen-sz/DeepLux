using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HV.Common.Helper;
using HV.Common.Provide;
using HV.Common.Enums;

namespace HV.Models
{
    [Serializable]
    public class Rectangle1Model:NotifyPropertyBase
    {
        private double _Row1;

        public double Row1
        {
            get { return _Row1; }
            set 
            {
                if (value<0)
                {
                    value = 0;
                }
                _Row1 = value;
                this.RaisePropertyChanged();
            }
        }
        private double _Col1;

        public double Col1
        {
            get { return _Col1; }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                _Col1 = value;
                this.RaisePropertyChanged();
            }
        }
        private double _Row2;

        public double Row2
        {
            get { return _Row2; }
            set { Set(ref _Row2, value); }
        }
        private double _Col2;

        public double Col2
        {
            get { return _Col2; }
            set { Set(ref _Col2, value); }
        }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Row1"] = Row1;
            obj["Col1"] = Col1;
            obj["Row2"] = Row2;
            obj["Col2"] = Col2;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Row1"] != null) Row1 = obj["Row1"].Value<double>();
                if (obj["Col1"] != null) Col1 = obj["Col1"].Value<double>();
                if (obj["Row2"] != null) Row2 = obj["Row2"].Value<double>();
                if (obj["Col2"] != null) Col2 = obj["Col2"].Value<double>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"Rectangle1Model.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
