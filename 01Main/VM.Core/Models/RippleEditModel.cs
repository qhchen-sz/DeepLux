using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HV.Common;
using HV.Common.Enums;
using HV.Common.Helper;

namespace HV.Models
{
    public class RippleEditModel : NotifyPropertyBase
    {
        private int _ID;
        /// <summary>
        /// ID
        /// </summary>
        public int ID
        {
            get { return _ID; }
            set { _ID = value; this.RaisePropertyChanged(); }
        }
        /// <summary>
        /// 程序类型
        /// </summary>
        public eRippleEditProgramType ProgramType { get; set; } = eRippleEditProgramType.WAIT;
        /// <summary>
        /// 时间
        /// </summary>
        public uint Time { get; set; } = 0;
        /// <summary>
        /// 功率
        /// </summary>
        public float Power { get; set; } = 0;
        /// <summary>
        /// 角度  
        /// </summary>
        public double Angle { get; set; } = 0;



    }
}
