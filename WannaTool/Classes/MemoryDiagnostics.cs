using System.Diagnostics;

namespace WannaTool
{
    internal static class MemoryDiagnostics
    {
#if DEBUG
        private static bool _enabled = true;
        public static bool Enabled { get => _enabled; set => _enabled = value; }
#else
        public static bool Enabled => false;
#endif

        public static void Report(int resultCount)
        {
#if DEBUG
            if (!_enabled) return;

            try
            {
                using var process = Process.GetCurrentProcess();
                long workingSet = process.WorkingSet64;
                long privateBytes = process.PrivateMemorySize64;
                long gcTotal = GC.GetTotalMemory(false);
                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                int iconCount = IconLoader.GetCacheCount();
                long indexCount = Indexer.GetEntryCount();

                Debug.WriteLine($"[MemoryDiagnostics] WorkingSet64={workingSet / 1024} KB PrivateMemory={privateBytes / 1024} KB GCHeap={gcTotal / 1024} KB Gen0={gen0} Gen1={gen1} Gen2={gen2} Results={resultCount} IconCache={iconCount} IndexEntries={indexCount}");
            }
            catch { }
#endif
        }
    }
}
