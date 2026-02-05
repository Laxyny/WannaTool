using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WannaTool
{
    public static class ProcessHelper
    {
        public static List<SearchResult> GetTopProcesses(int limit = 10, bool killMode = false)
        {
            var results = new List<SearchResult>();
            Process[]? processes = null;
            try
            {
                processes = Process.GetProcesses();
                var top = processes
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(limit)
                    .ToList();

                foreach (var p in top)
                {
                    try
                    {
                        string name = p.ProcessName;
                        long ramMb = p.WorkingSet64 / 1024 / 1024;
                        string title = !string.IsNullOrEmpty(p.MainWindowTitle) ? $" - {p.MainWindowTitle}" : "";
                        string? path = GetProcessPath(p);

                        results.Add(new SearchResult
                        {
                            DisplayName = $"{name} (PID: {p.Id}) - {ramMb} MB RAM{title}",
                            FullPath = killMode ? $"kill:{p.Id}|{name}" : $"focus:{p.Id}",
                            IsFolder = false,
                            Icon = !string.IsNullOrEmpty(path) ? IconLoader.GetIcon(path, false) : null
                        });
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                if (processes != null)
                {
                    foreach (var p in processes) p.Dispose();
                }
            }
            return results;
        }

        public static List<SearchResult> GetKillCandidates(string query)
        {
            var results = new List<SearchResult>();
            Process[]? all = null;
            try
            {
                all = Process.GetProcesses();
                var processes = all
                    .Where(p => p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(10)
                    .ToList();

                foreach (var p in processes)
                {
                    try
                    {
                        string name = p.ProcessName;
                        long ramMb = p.WorkingSet64 / 1024 / 1024;
                        string? path = GetProcessPath(p);

                        results.Add(new SearchResult
                        {
                            DisplayName = $"Kill: {name} (PID: {p.Id}) - {ramMb} MB RAM",
                            FullPath = $"kill:{p.Id}|{name}",
                            IsFolder = false,
                            Icon = !string.IsNullOrEmpty(path) ? IconLoader.GetIcon(path, false) : null
                        });
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                if (all != null)
                {
                    foreach (var p in all) p.Dispose();
                }
            }
            return results;
        }

        private static string? GetProcessPath(Process p)
        {
            try
            {
                return p.MainModule?.FileName;
            }
            catch 
            {
                return null;
            }
        }

        public static void KillProcess(int pid)
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
        }

        public static void FocusProcess(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    if (IsIconic(p.MainWindowHandle))
                        ShowWindow(p.MainWindowHandle, 9);
                    SetForegroundWindow(p.MainWindowHandle);
                }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
