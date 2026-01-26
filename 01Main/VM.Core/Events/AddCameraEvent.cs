using EventMgrLib;
using HV.Common.Enums;
using HV.Core;
using HV.Models;

namespace HV.Events
{
    public class AddCameraEvent : PubSubEvent<AddCameraEventParamModel>
    {
    }
    public class AddCameraEventParamModel
    {
        public CameraBase Camera { get; set; }
        public eOperateType OperateType { get; set; }
    }
}
