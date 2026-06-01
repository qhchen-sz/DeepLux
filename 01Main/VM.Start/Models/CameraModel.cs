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
    public class CameraModel : NotifyPropertyBase
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
        private bool _IsConnected;
        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected
        {
            get { return _IsConnected; }
            set { Set(ref _IsConnected, value); }
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
                if (obj["Name"] != null) Name = obj["Name"].ToString();
                if (obj["Remarks"] != null) Remarks = obj["Remarks"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"CameraModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
