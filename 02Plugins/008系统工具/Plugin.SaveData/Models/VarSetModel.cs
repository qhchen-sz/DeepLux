using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HV.Common.Helper;
using HV.Script;

namespace Plugin.SaveData.Models
{
    [Serializable]
    public class VarSetModel : NotifyPropertyBase
    {
        private int _Index;


        private string _Link;


        [NonSerialized]
        private ExpressionScriptSupport _m_TempScriptSupport;

        public int Index
        {
            get { return _Index; }
            set
            {
                _Index = value;
                RaisePropertyChanged("Index");
            }
        }

        public string DataType { get; set; }

        public string Name { get; set; }


        public string Link
        {
            get { return _Link; }
            set
            {
                _Link = value;
                RaisePropertyChanged("Link");
            }
        }

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

        [field: NonSerialized]
        public bool IsCompileSuccess { get; set; }

    }
    [Serializable]
    public class ReadOnlyFileWriter : IDisposable
    {
        private readonly string _filePath;
        private FileStream _writeStream;
        private readonly object _lock = new object();

        public ReadOnlyFileWriter(string filePath)
        {
            _filePath = filePath;

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            // 先以正常模式打开/创建文件
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Dispose();
            }

            // 移除只读属性（如果有）
            RemoveReadOnlyAttribute(filePath);

            // 以独占写入模式打开文件
            _writeStream = new FileStream(filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);  // 只允许其他进程读取

            // 将文件设置为只读（对其他程序而言）
            SetReadOnlyAttribute(filePath);
        }

        /// <summary>
        /// 写入一行数据
        /// </summary>
        public void WriteLine(string line)
        {
            lock (_lock)
            {
                // 移到文件末尾
                _writeStream.Seek(0, SeekOrigin.End);

                byte[] data = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                _writeStream.Write(data, 0, data.Length);
                _writeStream.Flush();
            }
        }

        /// <summary>
        /// 写入多行数据
        /// </summary>
        public void WriteLines(IEnumerable<string> lines)
        {
            lock (_lock)
            {
                _writeStream.Seek(0, SeekOrigin.End);

                foreach (var line in lines)
                {
                    byte[] data = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                    _writeStream.Write(data, 0, data.Length);
                }
                _writeStream.Flush();
            }
        }

        /// <summary>
        /// 移除只读属性
        /// </summary>
        private void RemoveReadOnlyAttribute(string filePath)
        {
            try
            {
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移除只读属性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置只读属性
        /// </summary>
        private void SetReadOnlyAttribute(string filePath)
        {
            try
            {
                var attributes = File.GetAttributes(filePath);
                File.SetAttributes(filePath, attributes | FileAttributes.ReadOnly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置只读属性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _writeStream?.Dispose();

                // 释放时移除只读属性（可选）
                if (File.Exists(_filePath))
                {
                    RemoveReadOnlyAttribute(_filePath);
                }
            }
        }
    }
}
