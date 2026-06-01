using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HV.Common.Provide;
using HV.Common.Enums;
using
   HV.Common.Helper;

namespace HV.Models
{
    [Serializable]
    public class CommunicationModel : NotifyPropertyBase
    {
        private int _ID;
        /// <summary>
        /// ID
        /// </summary>
        public int ID
        {
            get { return _ID; }
            set { Set(ref _ID, value); }
        }
        private bool _IsEnable;
        /// <summary>
        /// 使能
        /// </summary>
        public bool IsEnable
        {
            get { return _IsEnable; }
            set { Set(ref _IsEnable, value); }
        }
        private string _Name;
        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { Set(ref _Name, value); }
        }
        private string _Remarks;
        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks
        {
            get { return _Remarks; }
            set { Set(ref _Remarks, value); }
        }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["ID"] = ID;
            obj["IsEnable"] = IsEnable;
            obj["Name"] = Name ?? "";
            obj["Remarks"] = Remarks ?? "";
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["ID"] != null) ID = obj["ID"].Value<int>();
                if (obj["IsEnable"] != null) IsEnable = obj["IsEnable"].Value<bool>();
                if (obj["Name"] != null) Name = obj["Name"].ToString();
                if (obj["Remarks"] != null) Remarks = obj["Remarks"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"CommunicationModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
