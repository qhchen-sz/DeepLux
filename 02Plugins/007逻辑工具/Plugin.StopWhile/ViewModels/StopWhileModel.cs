using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using HV.Attributes;
using HV.Common.Enums;
using HV.Core;

namespace Plugin.StopWhile.ViewModels
{
    [Category("逻辑工具")]
    [DisplayName("停止循环")]
    [ModuleImageName("StopWhile")]
    [Serializable]
    public class StopWhileModel : ModuleBase
    {
        public override bool ExeModule()
        {
            ChangeModuleRunStatus(eRunStatus.OK);
            return true;
        }

        public override string HVSerialize()
        {
            return base.HVSerialize();
        }

        public override void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            base.HVDeserialize(json);
        }
    }
}
