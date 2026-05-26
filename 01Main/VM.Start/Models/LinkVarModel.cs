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
    public class LinkVarModel : NotifyPropertyBase
    {
        public Action TextChanged;
        private string _Text = "";
        /// <summary>
        /// 显示文本
        /// </summary>
        public string Text
        {
            get { return _Text; }
            set
            {
                _Text = value;
                RaisePropertyChanged();
                if (_Text.StartsWith("&"))
                {
                    IsReadOnly = true;
                }
                else
                {
                    Value = _Text;
                    IsReadOnly = false;
                }
                TextChanged?.Invoke();
            }
        }
        private bool _IsReadOnly = false;
        /// <summary>
        /// 只读
        /// </summary>
        public bool IsReadOnly
        {
            get { return _IsReadOnly; }
            set
            {
                _IsReadOnly = value;
                RaisePropertyChanged();
            }
        }
        private object _Value;
        /// <summary>
        /// 值
        /// </summary>
        public object Value
        {
            get { return _Value; }
            set
            {
                if (value==null || value.ToString().StartsWith("&")) return;
                _Value = value;
                RaisePropertyChanged();
            }
        }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Text"] = Text ?? "";
            obj["IsReadOnly"] = IsReadOnly;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["Text"] != null) Text = obj["Text"].ToString();
                if (obj["IsReadOnly"] != null) IsReadOnly = obj["IsReadOnly"].Value<bool>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"LinkVarModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
