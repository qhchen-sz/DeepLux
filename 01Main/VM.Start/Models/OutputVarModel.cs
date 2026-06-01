using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VM.Halcon.Helper;
using HV.Common.Provide;
using HV.Common.Enums;
using
   HV.Common.Enums;

namespace HV.Models
{
    [Serializable]
    public class OutputVarModel : NotifyPropertyBase
    {
        private string _Name;

        public string Name
        {
            get { return _Name; }
            set { _Name = value; RaisePropertyChanged(); }
        }
        private eTypes _Type;

        public eTypes Type
        {
            get { return _Type; }
            set { _Type = value; RaisePropertyChanged(); }
        }
        private object _Value ;

        public object Value
        {
            get { return _Value; }
            set { _Value = value; RaisePropertyChanged(); }
        }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Name"] = Name ?? "";
            obj["Type"] = (int)Type;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Name"] != null) Name = obj["Name"].ToString();
                if (obj["Type"] != null) Type = (eTypes)obj["Type"].Value<int>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"OutputVarModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
