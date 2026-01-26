using EventMgrLib;
using
   HV.Models;

namespace HV.Events
{
    public class CurrentUserChangedEvent : PubSubEvent<UserModel>
    {
    }
}
