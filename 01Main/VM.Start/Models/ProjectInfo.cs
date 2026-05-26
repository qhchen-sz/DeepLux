using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HV.Common.Enums;
using HV.Common.Helper;
using Newtonsoft.Json.Linq;

namespace HV.Models
{
    /// <summary>
    /// 项目信息
    /// </summary>
    [Serializable]
    public class ProjectInfo:NotifyPropertyBase
    {
        /// <summary>
        /// 项目ID
        /// </summary>
        public int ProjectID { get; set; }
        /// <summary>
        /// 文件夹ID
        /// </summary>
        public int FolderID { get; set; }

        /// <summary>
        /// 项目名称
        /// </summary>
        //public string ProjectName { get; set; }
        /// <summary>
        /// 流程名称
        /// </summary>
        public string ProcessName { get; set; }

        private string _Remarks;
        /// <summary>
        /// 模块注释
        /// </summary>
        public string Remarks
        {
            get { return _Remarks; }
            set { Set(ref _Remarks, value); }
        }
        private bool _IsEncypt=false;
        /// <summary>
        /// 加密
        /// </summary>
        public bool IsEncypt
        {
            get { return _IsEncypt; }
            set { Set(ref _IsEncypt, value); }
        }


        /// <summary>
        /// 流程运行模式（主动执行/调用执行）
        /// </summary>
        private eProjectAutoRunMode _ProjectRunMode = eProjectAutoRunMode.主动执行;
        public eProjectAutoRunMode ProjectRunMode
        {
            get { return _ProjectRunMode; }
            set { Set(ref _ProjectRunMode, value); }
        }
        public eProjectType ProjectType { get; set; }
        /// <summary>
        /// 是否刷新UI
        /// </summary>
        private bool _IsRefreshUi = true;
        public bool IsRefreshUi
        {
            get { return _IsRefreshUi; }
            set { Set(ref _IsRefreshUi, value); }
        }

        public string HVSerialize()
        {
            JObject obj = new JObject();
            obj["ProjectID"] = ProjectID;
            obj["FolderID"] = FolderID;
            obj["ProcessName"] = ProcessName ?? "";
            obj["Remarks"] = Remarks ?? "";
            obj["IsEncypt"] = IsEncypt;
            obj["ProjectRunMode"] = (int)ProjectRunMode;
            obj["ProjectType"] = (int)ProjectType;
            obj["IsRefreshUi"] = IsRefreshUi;
            return obj.ToString();
        }

        public void HVDeserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JObject obj = JObject.Parse(json);
                if (obj["ProjectID"] != null) ProjectID = obj["ProjectID"].Value<int>();
                if (obj["FolderID"] != null) FolderID = obj["FolderID"].Value<int>();
                if (obj["ProcessName"] != null) ProcessName = obj["ProcessName"].ToString();
                if (obj["Remarks"] != null) Remarks = obj["Remarks"].ToString();
                if (obj["IsEncypt"] != null) IsEncypt = obj["IsEncypt"].Value<bool>();
                if (obj["ProjectRunMode"] != null) ProjectRunMode = (eProjectAutoRunMode)obj["ProjectRunMode"].Value<int>();
                if (obj["ProjectType"] != null) ProjectType = (eProjectType)obj["ProjectType"].Value<int>();
                if (obj["IsRefreshUi"] != null) IsRefreshUi = obj["IsRefreshUi"].Value<bool>();
            }
            catch 
            {
                int i = 1;
            }
        }
    }
}
