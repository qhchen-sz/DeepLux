using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VM.Halcon;
using
   HV.Dialogs.Views;
using HV.Services;
using VM.Halcon.Config;

namespace HV.Script
{
    //脚本内的方法信息
    class ScriptMethodInfo
    {
        public string Name;
        public string Description;
        public string Category;
        public string DisplyName; // 格式"string key, int value, bool addFlag = false"
    }

    public class ScriptMethods
    {
        public int ProjectID { get; set; } = 0; //脚本所在项目的id /执行run方法的时候 赋值
        public string ModuleName { get; set; } //脚本所在对应的模块名称

        /// <summary>
        /// 弹窗显示
        /// </summary>
        /// <param name="str"></param>
        public void Show(string str)
        {
            MessageView.Ins.MessageBoxShow(str);
        }

        public Object GetObject(string linkStr)
        {
            Project prj = Solution.Ins.GetProjectById(ProjectID);
            var var = prj.GetParamByName(linkStr);
            if (var == null)
            {
                return null;
            }
            object obj = var.Value;
            return obj;
        }

        public double getDouble(string linkStr)
        {
            
            if (linkStr.Contains("["))
            {
                string[] array = linkStr.Split('[');
                int.TryParse(array[1].Split(new char[] { ']' })[0], out int index);
                 List<double>  doubles = (List<double>)GetObject(array[0]);
                if (index >= doubles.Count)
                    return 0;
                return doubles[index];
            }
            else 
                return Convert.ToDouble(GetObject(linkStr).ToString());
        }

        public int getInt(string linkStr)
        {
            if (linkStr.Contains("["))
            {

                string[] array = linkStr.Split('[');
                int.TryParse(array[1].Split(new char[] { ']' })[0], out int index);
                List<int> ints = (List<int>)GetObject(array[0]);
                if (index >= ints.Count)
                    return 0;
                return ints[index];
            }
            else
                return Convert.ToInt32(GetObject(linkStr).ToString());
        }

        public bool getBool(string linkStr)
        {
            if (linkStr.Contains("["))
            {

                string[] array = linkStr.Split('[');
                int.TryParse(array[1].Split(new char[] { ']' })[0], out int index);
                List<bool> bools = (List<bool>)GetObject(array[0]);
                if (index >= bools.Count)
                    return false;
                return bools[index];
            }
            else
                return GetObject(linkStr).ToString().ToLower() == "true" ? true : false;
        }

        public string getString(string linkStr)
        {
            if (linkStr.Contains("["))
            {

                string[] array = linkStr.Split('[');
                int.TryParse(array[1].Split(new char[] { ']' })[0], out int index);
                List<string> strings = (List<string>)GetObject(array[0]);
                if (index >= strings.Count)
                    return "";
                return strings[index];
            }
            else
                return GetObject(linkStr)?.ToString() ?? "";
        }
        public RImage getImage(string linkStr)
        {
            var image = GetObject(linkStr) as RImage;
            return new RImage( image);
        }
        public List<double> getListDouble(string linkStr)
        {
            List<double> temp = (List<double>)GetObject(linkStr) ;

            return temp;
        }

        public List<int> getListInt(string linkStr)
        {

            List<int> temp = (List<int>)GetObject(linkStr);

            return temp;
        }

        public List<bool> getListBool(string linkStr)
        {
            List<bool> temp = (List<bool>)GetObject(linkStr);

            return temp;
        }

        public List<string> getListString(string linkStr)
        {
            List<string> temp = (List<string>)GetObject(linkStr);

            return temp;
        }

    }
}
