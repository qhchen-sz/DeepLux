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
    }
}
