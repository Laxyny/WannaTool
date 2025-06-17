using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
            public bool IsFolder { get; set; }
        }

        private static readonly string DatabasePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.db");
        private static readonly LiteDatabase Db;
        private static readonly ILiteCollection<IndexEntry> Collection;
        public static bool IsReady { get; private set; }

        static Indexer()
        {
            Db = new LiteDatabase(DatabasePath);
            Collection = Db.GetCollection<IndexEntry>("entries");
            Collection.EnsureIndex(e => e.FullPath, true);
            Collection.EnsureIndex(e => e.DisplayName);
        }

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".pdf", ".docx", ".xlsx", ".pptx", ".txt",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".avi", ".mkv", ".mov",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".html", ".css", ".js", ".json", ".xml", ".csv",
            ".psd", ".ai", ".blend", ".fbx", ".obj",
            ".apk", ".iso", ".bat", ".cmd"
        };

        private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".sys", ".tmp", ".log", ".bak", ".old",
            ".cache", ".msi", ".dat", ".bin", ".db", ".db-shm", ".db-wal",
            ".jsonl", ".lock", ".idx", ".thumb", ".mdmp", ".dmp"
        };

        private static readonly List<string> IgnoredFolders = new()
        {
            "C:\\Windows",
            "C:\\Program Files",
            "C:\\Program Files (x86)",
            "C:\\ProgramData",
            "C:\\$Recycle.Bin",
            "C:\\System Volume Information",
            "C:\\Users\\Default",
            "AppData",
            "node_modules",
            ".git",
            "\\bin",
            "\\obj",
            "\\.vs"
        };

        private static bool ShouldIndexFile(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension)) return false;
                if (IgnoredExtensions.Contains(extension)) return false;
                if (!AllowedExtensions.Contains(extension)) return false;
                foreach (var ignored in IgnoredFolders)
                {
                    if (filePath.Contains(ignored, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldIndexFolder(string folderPath)
        {
            foreach (var ignored in IgnoredFolders)
            {
                if (folderPath.Contains(ignored, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        public static async Task InitializeAsync()
        {
            if (Collection.Count() == 0)
            {
                await BuildIndexAsync();
            }
            else
            {
                _ = Task.Run(SyncIndexAsync);
            }

            StartWatching();
            IsReady = true;
        }

        private static async Task BuildIndexAsync()
        {
            Collection.DeleteAll();
            Utils.ShowToast("Indexing started", "WannaTool is scanning your files...");

            var roots = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName);

            foreach (var root in roots)
            {
                await ScanDirectoryRecursiveAsync(root);
            }

            Utils.ShowToast("Indexing completed", $"{Collection.Count()} entries indexed");
        }

        private static async Task ScanDirectoryRecursiveAsync(string currentFolder)
        {
            try
            {
                if (!ShouldIndexFolder(currentFolder))
                    return;

                var dirInfo = new DirectoryInfo(currentFolder);
                Collection.Upsert(new IndexEntry
                {
                    DisplayName = dirInfo.Name,
                    FullPath = dirInfo.FullName,
                    IsFolder = true
                });

                foreach (var file in dirInfo.GetFiles())
                {
                    if (ShouldIndexFile(file.FullName))
                    {
                        Collection.Upsert(new IndexEntry
                        {
                            DisplayName = file.Name,
                            FullPath = file.FullName,
                            IsFolder = false
                        });
                    }
                }

                foreach (var dir in dirInfo.GetDirectories())
                    await ScanDirectoryRecursiveAsync(dir.FullName);
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du scan du dossier {currentFolder}: {ex.Message}");
            }
        }

        private static async Task SyncIndexAsync()
        {
            var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName);

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        if (ShouldIndexFolder(dir))
                            actualPaths.Add(dir);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (PathTooLongException) { }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (ShouldIndexFile(file))
                            actualPaths.Add(file);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (PathTooLongException) { }
            }

            var toRemove = Collection.FindAll().Where(e => !actualPaths.Contains(e.FullPath)).ToList();
            foreach (var e in toRemove)
                Collection.Delete(e.Id);

            foreach (var path in actualPaths)
            {
                var isFolder = Directory.Exists(path);
                var name = Path.GetFileName(path);
                Collection.Upsert(new IndexEntry
                {
                    DisplayName = name,
                    FullPath = path,
                    IsFolder = isFolder
                });
            }
        }

        public static List<MainWindow.SearchResult> Search(string query)
        {
            var q = query.ToLowerInvariant();
            return Collection.Query()
                .Where(x => x.DisplayName.ToLower().Contains(q))
                .Limit(20)
                .ToList()
                .Select(e => new MainWindow.SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = e.IsFolder
                }).ToList();
        }

        public static List<MainWindow.SearchResult> Search(string query, IEnumerable<string> extensions)
        {
            var extSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
            var q = query.ToLowerInvariant();

            return Collection.Query()
                .Where(x => !x.IsFolder && extSet.Contains(Path.GetExtension(x.FullPath)) && x.DisplayName.ToLower().Contains(q))
                .Limit(20)
                .ToList()
                .Select(e => new MainWindow.SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = e.IsFolder
                }).ToList();
        }

        private static List<FileSystemWatcher> _watchers = new();

        private static void StartWatching()
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                try
                {
                    var watcher = new FileSystemWatcher(drive.RootDirectory.FullName)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };
                    watcher.Created += (s, e) => AddFile(e.FullPath);
                    watcher.Deleted += (s, e) => RemoveFile(e.FullPath);
                    watcher.Renamed += (s, e) => UpdateFile(e.OldFullPath, e.FullPath);
                    _watchers.Add(watcher);
                }
                catch { }
            }
        }

        private static void AddFile(string path)
        {
            try
            {
                var isFolder = Directory.Exists(path);
                if (!isFolder && !File.Exists(path)) return;
                if (!isFolder && !ShouldIndexFile(path)) return;

                Collection.Upsert(new IndexEntry
                {
                    DisplayName = Path.GetFileName(path),
                    FullPath = path,
                    IsFolder = isFolder
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ajout fichier {path}: {ex.Message}");
            }
        }

        private static void RemoveFile(string path)
        {
            var entry = Collection.FindOne(x => x.FullPath == path);
            if (entry != null) Collection.Delete(entry.Id);
        }

        private static void UpdateFile(string oldPath, string newPath)
        {
            RemoveFile(oldPath);
            AddFile(newPath);
        }

        public static void Dispose()
        {
            Db.Dispose();
        }
    }
}
