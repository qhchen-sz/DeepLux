using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NLog;

namespace HV.Common.Helper
{
    /// <summary>
    /// 硬件资源监控服务，每分钟记录一次电脑资源使用情况到日志
    /// </summary>
    public static class HardwareMonitorService
    {
        private static readonly Logger _logger = LogManager.GetLogger("hardwarefile");
        private static readonly Process _process = Process.GetCurrentProcess();
        private static readonly int _processorCount = Environment.ProcessorCount;

        #region P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        #endregion

        // CPU 差值计算所需的前一次采样值
        private static DateTime _prevTime;
        private static TimeSpan _prevProcTime;
        private static ulong _prevSysIdle;
        private static ulong _prevSysKernel;
        private static ulong _prevSysUser;
        private static bool _firstCpuSample = true;

        /// <summary>
        /// 启动硬件监控，后台独立线程每分钟采集一次
        /// </summary>
        public static void Start()
        {
            Task.Run(async () =>
            {
                // 先采集一次基线值，确保首次日志就有有效的 CPU 数据
                TakeBaseline();

                while (true)
                {
                    await Task.Delay(60000);
                    try
                    {
                        LogHardwareInfo();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "硬件监控采集异常");
                    }
                }
            });
        }

        /// <summary>
        /// 采集基线 CPU 时间用于差值计算
        /// </summary>
        private static void TakeBaseline()
        {
            _process.Refresh();
            _prevTime = DateTime.UtcNow;
            _prevProcTime = _process.TotalProcessorTime;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                _prevSysIdle = ToUInt64(idle);
                _prevSysKernel = ToUInt64(kernel);
                _prevSysUser = ToUInt64(user);
            }
            _firstCpuSample = false;
        }

        private static ulong ToUInt64(FILETIME ft)
        {
            return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        }

        private static void LogHardwareInfo()
        {
            var now = DateTime.UtcNow;
            _process.Refresh();

            // --- 系统内存 ---
            var memStatus = default(MEMORYSTATUSEX);
            memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref memStatus);

            // --- 软件内存 ---
            double procMemMB = _process.WorkingSet64 / 1048576.0;
            double totalPhysMB = memStatus.ullTotalPhys / 1048576.0;
            double procMemPercent = totalPhysMB > 0 ? procMemMB / totalPhysMB * 100.0 : 0;
            uint totalMemPercent = memStatus.dwMemoryLoad;

            // --- CPU 使用率 ---
            double procCpuPercent = 0;
            double sysCpuPercent = 0;

            if (!_firstCpuSample)
            {
                double elapsedSec = (now - _prevTime).TotalSeconds;
                if (elapsedSec > 0.001)
                {
                    // 进程 CPU：处理器时间差值 / 经过时间 / 核心数
                    double procDeltaSec = (_process.TotalProcessorTime - _prevProcTime).TotalSeconds;
                    procCpuPercent = procDeltaSec / elapsedSec / _processorCount * 100.0;

                    // 系统 CPU：GetSystemTimes 差值计算
                    if (GetSystemTimes(out var idle, out var kernel, out var user))
                    {
                        ulong curIdle = ToUInt64(idle);
                        ulong curKernel = ToUInt64(kernel);
                        ulong curUser = ToUInt64(user);

                        double idleDelta = (double)(curIdle - _prevSysIdle);
                        double kernelDelta = (double)(curKernel - _prevSysKernel);
                        double userDelta = (double)(curUser - _prevSysUser);

                        // Kernel 时间包含 Idle 时间
                        double totalDelta = kernelDelta + userDelta;
                        if (totalDelta > 0.001)
                        {
                            sysCpuPercent = (totalDelta - idleDelta) / totalDelta * 100.0;
                        }
                    }
                }
            }
            else
            {
                // 安全兜底：首次采集时不计算 CPU
                _firstCpuSample = false;
            }

            // 更新前一次采样值供下一分钟差值计算
            _prevTime = now;
            _prevProcTime = _process.TotalProcessorTime;
            if (GetSystemTimes(out var idle2, out var kernel2, out var user2))
            {
                _prevSysIdle = ToUInt64(idle2);
                _prevSysKernel = ToUInt64(kernel2);
                _prevSysUser = ToUInt64(user2);
            }

            // --- 虚拟内存 ---
            double procVirtMB = _process.VirtualMemorySize64 / 1048576.0;
            double totalAllocVirtMB = memStatus.ullTotalPageFile / 1048576.0;
            double usedVirtMB = (memStatus.ullTotalPageFile - memStatus.ullAvailPageFile) / 1048576.0;
            double procVirtPercent = totalAllocVirtMB > 0 ? procVirtMB / totalAllocVirtMB * 100.0 : 0;
            double totalVirtPercent = totalAllocVirtMB > 0 ? usedVirtMB / totalAllocVirtMB * 100.0 : 0;

            // --- 格式化输出 ---
            string msg = $"软件内存:{procMemMB:F0}MB  |  " +
                         $"软件内存使用率:{procMemPercent:F2}%  |  " +
                         $"总内存使用率:{totalMemPercent}%  |  " +
                         $"软件CPU使用率:{procCpuPercent:F0}%  |  " +
                         $"CPU使用率:{sysCpuPercent:F0}%  |  " +
                         $"软件虚拟内存:{procVirtMB:F0}MB  |  " +
                         $"软件虚拟内存使用率:{procVirtPercent:F2}%  |  " +
                         $"总使用虚拟内存:{usedVirtMB:F0}MB  |  " +
                         $"总虚拟内存使用率:{totalVirtPercent:F1}%  |  " +
                         $"总分配虚拟内存:{totalAllocVirtMB:F0}MB -- 定时记录";

            _logger.Info(msg);
        }
    }
}
