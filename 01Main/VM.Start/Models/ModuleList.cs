using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using HV.Common.Provide;
using HV.Common.Enums;

namespace HV.Models
{
    [Serializable]
    public class ModuleList
    {
        /// <summary>
        /// 编号
        /// </summary>
        public int ModuleNo { get; set; }
        /// <summary>
        /// 显示的名称
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }
        /// <summary>
        /// 图标
        /// </summary>
        [field:NonSerialized]
        public ImageSource IconImage { get; set; }
        public List<VarModel> VarModels { get; set; } = new List<VarModel>();

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["ModuleNo"] = ModuleNo;
            obj["DisplayName"] = DisplayName ?? "";
            obj["Remarks"] = Remarks ?? "";
            if (VarModels != null)
            {
                JArray arr = new JArray();
                foreach (var vm in VarModels)
                {
                    arr.Add(JObject.Parse(vm.HVSerialize()));
                }
                obj["VarModels"] = arr;
            }
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["ModuleNo"] != null) ModuleNo = obj["ModuleNo"].Value<int>();
                if (obj["DisplayName"] != null) DisplayName = obj["DisplayName"].ToString();
                if (obj["Remarks"] != null) Remarks = obj["Remarks"].ToString();
                if (obj["VarModels"] != null)
                {
                    VarModels = new List<VarModel>();
                    JArray arr = (JArray)obj["VarModels"];
                    foreach (var item in arr)
                    {
                        VarModel vm = new VarModel();
                        vm.HVDeserialize(item.ToString());
                        VarModels.Add(vm);
                    }
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"ModuleList.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
