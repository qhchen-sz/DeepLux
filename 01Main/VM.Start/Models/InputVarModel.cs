using EventMgrLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using
   HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Events;
using HV.ViewModels;
using HV.Common.Provide;

namespace HV.Models
{
    [Serializable]
    public class InputVarModel : NotifyPropertyBase
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
        public Array Types { get; set; } = Enum.GetValues(typeof(eTypes));
        private LinkVarModel _Var=new LinkVarModel();

        public LinkVarModel Var
        {
            get { return _Var; }
            set { _Var = value; RaisePropertyChanged(); }
        }
        public CommandBase LinkCommand { get; set; }

        #region 序列化
        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["Name"] = Name ?? "";
            obj["Type"] = (int)Type;
            if (Var != null)
                obj["Var"] = Var != null ? JObject.Parse(Var.HVSerialize()) : null;
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
                if (obj["Var"] != null)
                {
                    Var = new LinkVarModel();
                    Var.HVDeserialize(obj["Var"].ToString());
                }
            }
            catch (Exception ex)

            {

                  Logger.AddLog($"InputVarModel.HVDeserialize 异常: {ex.Message}", eMsgType.Error);

            }
        }
        #endregion
    }
}
