using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using ControlzEx.Controls;
using Newtonsoft.Json;
using
   HV.Common.Provide;

namespace HV.Common.Helper
{
    /// <summary>
    /// 自定义SerializationBinder，忽略程序集版本信息，解决重新编译后版本变化导致BinaryFormatter反序列化失败的问题
    /// </summary>
    public class IgnoreVersionSerializationBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            // 提取短程序集名称（去掉版本号、公钥Token等）
            string shortAssemblyName = assemblyName.Split(',')[0].Trim();

            // 提取短类型名称（去掉嵌套的程序集信息）
            string shortTypeName = typeName;
            int commaIndex = typeName.IndexOf(", ");
            if (commaIndex > 0)
            {
                shortTypeName = typeName.Substring(0, commaIndex);
            }

            // 在所有已加载的程序集中查找匹配的程序集和类型
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmShortName = assembly.FullName.Split(',')[0].Trim();
                if (asmShortName == shortAssemblyName)
                {
                    Type type = assembly.GetType(shortTypeName);
                    if (type != null)
                        return type;
                }
            }

            // 回退：忽略程序集名称，只按类型名在所有程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(shortTypeName);
                if (type != null)
                    return type;
            }

            // 最后的回退：使用默认方式
            return Type.GetType($"{typeName}, {assemblyName}");
        }
    }

    public class SerializeHelp
    {
        public static T Deserialize<T>(string fileName,bool isLoadCopyFile = false)
        {
            T t = default(T);
            try
            {
                if (!File.Exists(fileName))
                {
                    File.Create(fileName).Close();
                }
                FileInfo fileInfo = new FileInfo(fileName);
                if (fileInfo.Length == 0 && isLoadCopyFile)//文件内容为空
                {
                    int startIndex = fileName.LastIndexOf(".");
                    string fileCopyName = fileName.Insert(startIndex, "_Copy");
                    if (File.Exists(fileCopyName))
                    {
                        File.Copy(fileCopyName, fileName,true);
                        return (T)JsonConvert.DeserializeObject<T>(File.ReadAllText(fileCopyName));
                    }
                    else
                    {
                        return t;
                    }
                }
                else
                {
                    return (T)JsonConvert.DeserializeObject<T>(File.ReadAllText(fileName));
                }
            }
            catch (Exception e)
            {
                return t;
            }
        }
        public static void SerializeAndSaveFile<T>(T obj, string fileName,bool isCreatCopyFile = false)
        {
            try
            {
                //当项目比较大的时候保存耗时较长，这个时候如果异常断电，那么项目文件会全部丢失，为解决此问题：先序列化一个临时项目文件，序列化成功后再移动替换原文件
                if (isCreatCopyFile)
                {
                    int startIndex = fileName.LastIndexOf(".");
                    string fileCopyName = fileName.Insert(startIndex, "_Copy");
                    File.WriteAllText(fileCopyName, JsonConvert.SerializeObject(obj));
                    File.Copy(fileCopyName,fileName,true);
                }
                else
                {
                    File.WriteAllText(fileName, JsonConvert.SerializeObject(obj));
                }
            }
            catch (Exception e)
            {
                Logger.GetExceptionMsg(e);
            }
        }
        public static T Clone<T>(T obj)
        {
            return (T)JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj));
        }
        public static void BinSerializeAndSaveFile<T>(T obj, string fileName)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                BinaryFormatter formatter = new BinaryFormatter();
                // 使用自定义Binder，忽略程序集版本差异，增强兼容性
                formatter.Binder = new IgnoreVersionSerializationBinder();
                formatter.Serialize(stream, obj);
                stream.Flush();
            }
            catch (Exception e)
            {
                Logger.GetExceptionMsg(e);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }
        public static T BinDeserialize<T>(string fileName)
        {
            T t = default(T);
            try
            {
                using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    // 使用自定义Binder，忽略程序集版本差异，解决重新编译后打不开的问题
                    formatter.Binder = new IgnoreVersionSerializationBinder();
                    return (T)formatter.Deserialize(stream);
                }
            }
            catch (Exception e)
            {
                Logger.GetExceptionMsg(e);
                return t;
            }
        }

    }
}
