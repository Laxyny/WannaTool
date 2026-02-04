using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using Timer = System.Timers.Timer;

namespace WannaTool
{
    public class SystemMetrics
    {
        public float CpuUsage { get; set; }
        public long AppRamUsage { get; set; }
        public long TotalRam { get; set; }
        public long AvailableRam { get; set; }
        public float RamUsagePercent => TotalRam > 0 ? 100f * (TotalRam - AvailableRam) / TotalRam : 0;
    }

    public class SystemMetricsService
    {
        private static SystemMetricsService? _instance;
        public static SystemMetricsService Instance => _instance ??= new SystemMetricsService();

        private readonly Timer _timer;
        private PerformanceCounter? _cpuCounter;
        private Process? _currentProcess;
        private bool _isRunning;

        public event EventHandler<SystemMetrics>? MetricsUpdated;

        private SystemMetricsService()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
        }

        public void SetInterval(int seconds)
        {
            _timer.Interval = Math.Max(1000, seconds * 1000);
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                if (_cpuCounter == null)
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                }
                _cpuCounter.NextValue();
            }
            catch {}

            _currentProcess = Process.GetCurrentProcess();
            _timer.Start();
            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _timer.Stop();
            _isRunning = false;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                float cpu = 0;
                try
                {
                    cpu = _cpuCounter?.NextValue() ?? 0;
                }
                catch { }

                _currentProcess?.Refresh();
                long appRam = _currentProcess?.WorkingSet64 ?? 0;

                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    var metrics = new SystemMetrics
                    {
                        CpuUsage = cpu,
                        AppRamUsage = appRam,
                        TotalRam = (long)memStatus.ullTotalPhys,
                        AvailableRam = (long)memStatus.ullAvailPhys
                    };
                    
                    MetricsUpdated?.Invoke(this, metrics);
                }
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }
}
