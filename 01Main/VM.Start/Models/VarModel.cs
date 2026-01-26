using System;
using System.Collections.Generic;
using VM.Script.Support;
using HV.Common.Enums;
using HV.Common.Helper;
using HV.Script;

namespace HV.Models
{
    [Serializable]
    public class VarModel : NotifyPropertyBase
    {
        public ModuleParam ModuleParam { get; set; } = new ModuleParam();
        private int _Index;

        public int Index
        {
            get { return _Index; }
            set
            {
                _Index = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 变量类型
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name { get; set; }
        private object _Value;

        /// <summary>
        /// 变量值
        /// </summary>
        public object Value
        {
            get { return this._Value; }
            set
            {
                this._Value = value;
                if (this._Value != null)
                {
                    base.RaisePropertyChanged("Value");
                    string text = "";
                    string dataType = this.DataType;
                    string text2 = dataType;
                    if (text2 != null)
                    {
                        switch (text2.Length)
                        {
                            case 3:
                                if (text2 == "int")
                                {
                                    this.Text = this._Value.ToString();
                                }
                                break;
                            case 4:
                                if (text2 == "bool")
                                {
                                    this.Text = this._Value.ToString();
                                }
                                break;
                            case 5:
                            {
                                char c = text2[0];
                                if (c != 'i')
                                {
                                    if (c == 's')
                                    {
                                        if (text2 == "short")
                                        {
                                            this.Text = this._Value.ToString();
                                        }
                                    }
                                }
                                else if (text2 == "int[]")
                                {
                                    List<int> list = (List<int>)this.Value;
                                    if (list != null)
                                    {
                                        text = string.Format("count={0} value=(", list.Count);
                                        for (int i = 0; i < list.Count; i++)
                                        {
                                            text += list[i].ToString();
                                            if (i < list.Count - 1)
                                            {
                                                text += ",";
                                            }
                                        }
                                        text += ")";
                                    }
                                    this.Text = text;
                                }
                                break;
                            }
                            case 6:
                            {
                                char c = text2[0];
                                if (c <= 'b')
                                {
                                    if (c != 'R')
                                    {
                                        if (c == 'b')
                                        {
                                            if (text2 == "bool[]")
                                            {
                                                List<bool> list2 = (List<bool>)this.Value;
                                                if (list2 != null)
                                                {
                                                    text = string.Format(
                                                        "count={0} value=(",
                                                        list2.Count
                                                    );
                                                    for (int j = 0; j < list2.Count; j++)
                                                    {
                                                        text += list2[j].ToString();
                                                        if (j < list2.Count - 1)
                                                        {
                                                            text += ",";
                                                        }
                                                    }
                                                    text += ")";
                                                }
                                                this.Text = text;
                                            }
                                        }
                                    }
                                    else if (text2 == "Region")
                                    {
                                        this.Text = "";
                                    }
                                }
                                else if (c != 'd')
                                {
                                    if (c == 's')
                                    {
                                        if (text2 == "string")
                                        {
                                            this.Text = this._Value.ToString();
                                        }
                                    }
                                }
                                else if (text2 == "double")
                                {
                                    this.Text = this._Value.ToString();
                                }
                                break;
                            }
                            case 7:
                                if (text2 == "short[]")
                                {
                                    List<short> list3 = (List<short>)this.Value;
                                    if (list3 != null)
                                    {
                                        text = string.Format("count={0} value=(", list3.Count);
                                        for (int k = 0; k < list3.Count; k++)
                                        {
                                            text += list3[k].ToString();
                                            if (k < list3.Count - 1)
                                            {
                                                text += ",";
                                            }
                                        }
                                        text += ")";
                                    }
                                    this.Text = text;
                                }
                                break;
                            case 8:
                            {
                                char c = text2[0];
                                if (c != 'd')
                                {
                                    if (c == 's')
                                    {
                                        if (text2 == "string[]")
                                        {
                                            List<string> list4 = (List<string>)this.Value;
                                            if (list4 != null)
                                            {
                                                text = string.Format(
                                                    "count={0} value=(",
                                                    list4.Count
                                                );
                                                for (int l = 0; l < list4.Count; l++)
                                                {
                                                    text += list4[l].ToString();
                                                    if (l < list4.Count - 1)
                                                    {
                                                        text += ",";
                                                    }
                                                }
                                                text += ")";
                                            }
                                            this.Text = text;
                                        }
                                    }
                                }
                                else if (text2 == "double[]")
                                {
                                    List<double> list5 = (List<double>)this.Value;
                                    if (list5 != null)
                                    {
                                        text = string.Format("count={0} value=(", list5.Count);
                                        for (int m = 0; m < list5.Count; m++)
                                        {
                                            text += list5[m].ToString();
                                            if (m < list5.Count - 1)
                                            {
                                                text += ",";
                                            }
                                        }
                                        text += ")";
                                    }
                                    this.Text = text;
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        private string _Text;
        public string Text
        {
            get { return this._Text; }
            set
            {
                this._Text = value;
                
                string dataType = this.DataType;
                string a = dataType;
                string txt = "";
                switch (a)
                {
                    case "short":
                        if(short.TryParse(this._Text,out short temp_short))
                            this._Value = temp_short;
                        else
                            this._Value = 0;
                        break;
                    case "int":
                        if (int.TryParse(this._Text, out int temp_int))
                            this._Value = temp_int;
                        else
                            this._Value = 0;
                        //this._Value = int.Parse(this._Text);
                        break;
                    case "double":
                        if (double.TryParse(this._Text, out double temp_double))
                            this._Value = temp_double;
                        else
                            this._Value = 0;
                        //this._Value = double.Parse(this._Text);
                        break;
                    case "string":
                        this._Value = this._Text;
                        break;
                    case "bool":
                        if (bool.TryParse(this._Text, out bool temp_bool))
                            this._Value = temp_bool;
                        else
                            this._Value = false;
                        ////this._Value = bool.Parse(this._Text);
                        break;
                    //case "bool[]":
                    //    List<bool> list2 = (List<bool>)this.Value;
                    //    if (list2 != null)
                    //    {
                    //        txt = string.Format("count={0} value=(", list2.Count);
                    //        for (int j = 0; j < list2.Count; j++)
                    //        {
                    //            _Text += list2[j].ToString();
                    //            if (j < list2.Count - 1)
                    //            {
                    //                _Text += ",";
                    //            }
                    //        }
                    //        txt += ")";
                    //    }
                    //    this.Text = txt;
                    //    break;
                    //case "int[]":

                    //    List<int> list = (List<int>)this.Value;
                    //    if (list != null)
                    //    {
                    //        txt = $"count={list.Count} value=(";
                    //        for (int i = 0; i < list.Count; i++)
                    //        {
                    //            _Text += list[i].ToString();
                    //            if (i < list.Count - 1)
                    //            {
                    //                txt += ",";
                    //            }
                    //        }
                    //        txt += ")";
                    //    }
                    //    this.Text = txt;
                    //    break;
                    //case "short[]":
                    //    List<short> list3 = (List<short>)this.Value;
                    //    if (list3 != null)
                    //    {
                    //        txt = string.Format("count={0} value=(", list3.Count);
                    //        for (int k = 0; k < list3.Count; k++)
                    //        {
                    //            txt += list3[k].ToString();
                    //            if (k < list3.Count - 1)
                    //            {
                    //                txt += ",";
                    //            }
                    //        }
                    //        txt += ")";
                    //    }
                    //    this.Text = txt;
                    //    break;
                    //case "string[]":
                    //    List<string> list4 = (List<string>)this.Value;
                    //    if (list4 != null)
                    //    {
                    //        txt = string.Format("count={0} value=(", list4.Count);
                    //        for (int l = 0; l < list4.Count; l++)
                    //        {
                    //            _Text += list4[l].ToString();
                    //            if (l < list4.Count - 1)
                    //            {
                    //                _Text += ",";
                    //            }
                    //        }
                    //        txt += ")";
                    //    }
                    //    this.Text = txt;
                    //    break;
                    //case "double[]":
                    //    List<double> list5 = (List<double>)this.Value;
                    //    if (list5 != null)
                    //    {
                    //        _Text = string.Format("count={0} value=(", list5.Count);
                    //        for (int m = 0; m < list5.Count; m++)
                    //        {
                    //            _Text += list5[m].ToString();
                    //            if (m < list5.Count - 1)
                    //            {
                    //                _Text += ",";
                    //            }
                    //        }
                    //        _Text += ")";
                    //    }
                    //    this.Text = _Text;
                    //    break;

                    default:
                        break;
                }
                base.RaisePropertyChanged("Text");
            }
        }

        /// <summary>
        /// 表达式
        /// </summary>
        private string _Expression = "NULL";
        public string Expression
        {
            get { return _Expression; }
            set
            {
                Set(
                    ref _Expression,
                    value,
                    new Action(() =>
                    {
                        IsCompileSuccess = false;
                    })
                );
            }
        }

        [NonSerialized]
        private ExpressionScriptSupport _m_TempScriptSupport;
        public ExpressionScriptSupport m_TempScriptSupport
        {
            get
            {
                if (_m_TempScriptSupport == null)
                {
                    _m_TempScriptSupport = new ExpressionScriptSupport();
                }
                return _m_TempScriptSupport;
            }
            set { _m_TempScriptSupport = value; }
        }

        /// <summary>
        /// 编译成功
        /// </summary>
        [field: NonSerialized]
        public bool IsCompileSuccess { get; set; }

        ///// <summary>
        ///// 变量值,存储HImage，HRegion，HXLD
        ///// </summary>
        //public HObject Value_HObject { get; set; }

        /// <summary>
        /// 注释
        /// </summary>
        public string Note { set; get; }
    }

    [Serializable]
    public class ProjectRunModeVarModel
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 运行模式
        /// </summary>
        public eProjectAutoRunMode RunMode { get; set; }

        /// <summary>
        /// 刷新界面
        /// </summary>
        public bool IsRefreshUi { get; set; }
    }
}
