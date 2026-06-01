using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Core;
using HV.Attributes;

namespace Plugin.GrabImage.ViewModels
{
    [Category("系统工具")]
    [DisplayName("文件夹")]
    [ModuleImageName("Folder")]
    [Serializable]
    public class FolderViewModel : ModuleBase
    {
        public override bool ExeModule()
        {
            ChangeModuleRunStatus(HV.Common.Enums.eRunStatus.OK);
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
