using EventMgrLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HV.Common;
using HV.Common.Helper;
using HV.Events;
using HV.Models;
using HV.ViewModels;
using HV.Views;
using Plugin.CreatePoints.ViewModels;

namespace Plugin.CreatePoints.Model
{
    
    [Serializable]
    public class PointsParamModel:NotifyPropertyBase
    {
        private int _ID;
        /// <summary>
        /// ID
        /// </summary>
        public int ID
        {
            get { return _ID; }
            set { Set(ref _ID, value); }
        }
        private double _PointX = 10;
        /// <summary>
        /// X坐标
        /// </summary>
        public double PointX
        {
            get { return _PointX; }
            set
            {
                Set(ref _PointX, value);
            }
        }
        private double _PointY = 20;
        /// <summary>
        /// Y坐标
        /// </summary>
        public double PointY
        {
            get { return _PointY; }
            set
            {
                Set(ref _PointY, value);    
            }
        }


    }
}
