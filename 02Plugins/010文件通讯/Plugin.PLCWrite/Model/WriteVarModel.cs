using HV.Common.Enums;
using HV.Common.Helper;
using HV.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.PLCWrite.ViewModels
{
    [Serializable]
    public class WriteVarModel : VarModel
    {
        private string _Addr;

        public string Addr
        {
            get { return _Addr; }
            set { _Addr = value; RaisePropertyChanged(); }
        }
        private PLCDataWriteReadTypeEnum _DataType;

        public PLCDataWriteReadTypeEnum DataTypePlc
        {
            get { return _DataType; }
            set { _DataType = value; RaisePropertyChanged(); }
        }
        private string _Name;

        public string NamePlc
        {
            get { return _Name; }
            set { _Name = value; RaisePropertyChanged(); }
        }
        //private string _Value;

        //public string ValuePlc
        //{
        //    get { return _Value; }
        //    set { _Value = value; RaisePropertyChanged(); }
        //}
        private string _Remarks, _Note;

        public string Remarks
        {
            get { return _Remarks; }
            set { _Remarks = value; RaisePropertyChanged(); }
        }
        public string NotePlc
        {
            get { return _Note; }
            set { _Note = value; RaisePropertyChanged(); }
        }
    }
}
