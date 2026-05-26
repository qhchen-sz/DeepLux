using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HV.Common.Provide;
using HV.Common.Enums;

namespace
   HV.Models
{
    [Serializable]
    public class CameraInfoModel
    {
        [NonSerialized]
        /// <summary>扩展信息 </summary>
        public object ExtInfo;
        /// <summary>相机名称 </summary>
        public string CamName { set; get; }
        /// <summary>相机编号 </summary>
        public string SerialNO { set; get; }
        /// <summary>相机IP </summary>
        public string CameraIP { set; get; }
        /// <summary>相机备注 </summary>
        public string MaskName { set; get; }
        /// <summary>相机链接 </summary>
        public bool Connected { set; get; }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["CamName"] = CamName ?? "";
            obj["SerialNO"] = SerialNO ?? "";
            obj["CameraIP"] = CameraIP ?? "";
            obj["MaskName"] = MaskName ?? "";
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["CamName"] != null) CamName = obj["CamName"].ToString();
                if (obj["SerialNO"] != null) SerialNO = obj["SerialNO"].ToString();
                if (obj["CameraIP"] != null) CameraIP = obj["CameraIP"].ToString();
                if (obj["MaskName"] != null) MaskName = obj["MaskName"].ToString();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"CameraInfoModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
