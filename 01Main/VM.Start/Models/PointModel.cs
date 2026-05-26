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
    public class PointModel : NotifyPropertyBase
    {
        private float _X;
        /// <summary>
        /// X
        /// </summary>
        public float X
        {
            get { return _X; }
            set { _X = value; RaisePropertyChanged(); }
        }
        private float _Y;
        /// <summary>
        /// Y
        /// </summary>
        public float Y
        {
            get { return _Y; }
            set { _Y = value; RaisePropertyChanged(); }
        }
        private float _Z;
        /// <summary>
        /// Z
        /// </summary>
        public float Z
        {
            get { return _Z; }
            set { _Z = value; RaisePropertyChanged(); }
        }
        private float _U;
        /// <summary>
        /// U
        /// </summary>
        public float U
        {
            get { return _U; }
            set { _U = value; RaisePropertyChanged(); }
        }
        private float _W;
        /// <summary>
        /// W
        /// </summary>
        public float W
        {
            get { return _W; }
            set { _W = value; RaisePropertyChanged(); }
        }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["X"] = X;
            obj["Y"] = Y;
            obj["Z"] = Z;
            obj["U"] = U;
            obj["W"] = W;
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
                if (obj["U"] != null) U = obj["U"].Value<float>();
                if (obj["W"] != null) W = obj["W"].Value<float>();
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"PointModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
