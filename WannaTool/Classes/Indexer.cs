using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;

namespace WannaTool
{
    public static class Indexer
    {
        public class IndexEntry
        {
            public int Id { get; set; }
            public string DisplayName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public string LowerName { get; set; } = "";
            public bool IsFolder { get; set; }
            public int Score { get; set; }
        }

        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WannaTool");
        private static readonly string DatabasePath = Path.Combine(AppDataFolder, "index.db");
        
        private static LiteDatabase? _db;
        private static ILiteCollection<IndexEntry>? _collection;
        
        public static bool IsReady { get; private set; }

        private static readonly List<string> TargetFolders = new()
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".lnk", ".pdf", ".docx", ".xlsx", ".pptx", ".txt",
            ".jpg", ".png", ".mp4", ".zip", ".sln", ".cs", ".js", ".ts", ".html"
        };

        private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", "bin", "obj", ".vs", "AppData"
        };

        public static async Task InitializeAsync()
        {
            if (_db != null) return;

            try 
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                _db = new LiteDatabase($"Filename={DatabasePath};Connection=Shared");
                _collection = _db.GetCollection<IndexEntry>("entries");
                
                _collection.EnsureIndex(x => x.LowerName);
                
                if (_collection.Count() == 0)
                {
                    await PerformScanningAsync();
                }
                
                StartWatcher();

                IsReady = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Indexer Init Error: {ex.Message}");
            }
        }

        private static void StartWatcher()
        {
            foreach (var path in TargetFolders)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var watcher = new FileSystemWatcher(path);
                        watcher.IncludeSubdirectories = true;
                        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
                        watcher.Created += OnFileChanged;
                        watcher.Deleted += OnFileChanged;
                        watcher.Renamed += OnFileChanged;
                        watcher.EnableRaisingEvents = true;
                    }
                    catch { }
                }
            }
        }

        private static async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            await Task.Delay(2000); 
        }

        public static async Task BuildIndexAsync()
        {
            try
            {
                _db?.Dispose();
                _db = null;

                if (File.Exists(DatabasePath))
                {
                    try { File.Delete(DatabasePath); } catch { }
                }

                await InitializeAsync();
                await PerformScanningAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildIndex Error: {ex.Message}");
            }
        }

        private static async Task PerformScanningAsync()
        {
            try
            {
                var entries = new List<IndexEntry>();

                foreach (var root in TargetFolders)
                {
                    if (Directory.Exists(root))
                    {
                        await ScanDirectoryRecursiveAsync(root, entries);
                    }
                }

                if (_collection != null)
                {
                    _collection.DeleteAll(); 
                    _collection.InsertBulk(entries);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scanning Error: {ex.Message}");
            }
        }

        private static Task ScanDirectoryRecursiveAsync(string dirPath, List<IndexEntry> entries)
        {
            return Task.Run(() =>
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    if (IgnoredFolders.Contains(dirInfo.Name)) return;

                    entries.Add(new IndexEntry
                    {
                        DisplayName = dirInfo.Name,
                        FullPath = dirInfo.FullName,
                        LowerName = dirInfo.Name.ToLowerInvariant(),
                        IsFolder = true,
                        Score = 10
                    });

                    foreach (var file in dirInfo.EnumerateFiles())
                    {
                        if (AllowedExtensions.Contains(file.Extension))
                        {
                            int score = 1;
                            if (file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || 
                                file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                score = 100;
                            }

                            entries.Add(new IndexEntry
                            {
                                DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                                FullPath = file.FullName,
                                LowerName = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(),
                                IsFolder = false,
                                Score = score
                            });
                        }
                    }

                    foreach (var subDir in dirInfo.EnumerateDirectories())
                    {
                        if (!IgnoredFolders.Contains(subDir.Name))
                        {
                            ScanDirInternal(subDir, entries);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }
            });
        }

        private static void ScanDirInternal(DirectoryInfo dirInfo, List<IndexEntry> entries)
        {
            try
            {
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    if (AllowedExtensions.Contains(file.Extension))
                    {
                        int score = 1;
                        if (file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || 
                            file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            score = 100;
                        }

                        entries.Add(new IndexEntry
                        {
                            DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                            FullPath = file.FullName,
                            LowerName = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(),
                            IsFolder = false,
                            Score = score
                        });
                    }
                }

                foreach (var subDir in dirInfo.EnumerateDirectories())
                {
                    if (!IgnoredFolders.Contains(subDir.Name))
                    {
                        ScanDirInternal(subDir, entries);
                    }
                }
            }
            catch { }
        }

        public static List<SearchResult> Search(string query)
        {
            if (_collection == null || string.IsNullOrWhiteSpace(query)) return new List<SearchResult>();

            var q = query.Trim().ToLowerInvariant();
            
            return _collection.Find(x => x.LowerName.Contains(q))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName.Length)
                .Take(15)
                .Select(e => new SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = e.IsFolder
                })
                .ToList();
        }

        public static List<SearchResult> Search(string query, IEnumerable<string> extensions)
        {
            if (_collection == null) return new List<SearchResult>();

            var extSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
            var q = query.Trim().ToLowerInvariant();

            return _collection.Find(x => x.LowerName.Contains(q))
                .Where(x => !x.IsFolder && extSet.Contains(Path.GetExtension(x.FullPath)))
                .OrderBy(x => x.DisplayName.Length)
                .Take(15)
                .Select(e => new SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = e.IsFolder
                })
                .ToList();
        }

        public static void Dispose()
        {
            _db?.Dispose();
        }
    }
}
