using System.IO;
using System.Text.Json;
using System.IO.Compression;

namespace WannaTool
{
    public static class Indexer
    {
        private static readonly string IndexFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.json.gz");
        private static List<MainWindow.SearchResult> _index = new List<MainWindow.SearchResult>();
        private static FileSystemWatcher _watcher;
        public static bool IsReady { get; set; } = false;
        private static bool _saveScheduled = false;

        public static async Task InitializeAsync()
        {
            if (File.Exists(IndexFilePath))
            {
                await LoadIndexAsync();
            }
            else
            {
                await BuildIndexAsync();
                await SaveIndexAsync();
            }

            StartWatching();
            IsReady = true;
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

        public static void MarkAsReady()
        {
            IsReady = true;
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

        public static async Task LoadIndexAsync()
        {
            try
            {
                using var fileStream = new FileStream(IndexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
                _index = await JsonSerializer.DeserializeAsync<List<MainWindow.SearchResult>>(gzip) ?? new();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Aucun index existant trouvé. Reconstruction nécessaire.");
                _index = new List<MainWindow.SearchResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de l'index: {ex.Message}");
                _index = new List<MainWindow.SearchResult>();
            }
        }

        public static void ClearIndex()
        {
            _index.Clear();
            IsReady = false;
        }

        private static async Task SaveIndexAsync()
        {
            try
            {
                using var fileStream = new FileStream(IndexFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var gzip = new GZipStream(fileStream, CompressionMode.Compress);
                await JsonSerializer.SerializeAsync(gzip, _index);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Erreur SaveIndexAsync: {ex.Message}");
                // Ici tu peux logger ou faire un retry léger si tu veux
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur générale SaveIndexAsync: {ex.Message}");
            }
        }

        private static async Task BuildIndexAsync()
        {
            var rootFolders = new[]
            {
        "C:\\"
    };

            var results = new List<MainWindow.SearchResult>();

            foreach (var root in rootFolders)
            {
                if (!Directory.Exists(root)) continue;
                await ScanDirectoryRecursiveAsync(root, results);
            }

            _index = results;
            Console.WriteLine($"Indexation terminée avec {results.Count} fichiers indexés.");
        }

        private static async Task ScanDirectoryRecursiveAsync(string currentFolder, List<MainWindow.SearchResult> results)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(currentFolder);

                if (!ShouldIndexFolder(directoryInfo.FullName))
                    return;

                // Ajouter ce dossier dans l'index
                results.Add(new MainWindow.SearchResult
                {
                    DisplayName = directoryInfo.Name,
                    FullPath = directoryInfo.FullName,
                    Icon = null,
                    IsFolder = true
                });

                // Parcourir les fichiers
                foreach (var file in directoryInfo.GetFiles())
                {
                    try
                    {
                        if (ShouldIndexFile(file.FullName))
                        {
                            results.Add(new MainWindow.SearchResult
                            {
                                DisplayName = file.Name,
                                FullPath = file.FullName,
                                Icon = null,
                                IsFolder = false
                            });
                        }
                    }
                    catch
                    {
                        // Skip file access errors
                    }
                }

                // Parcourir les sous-dossiers
                foreach (var dir in directoryInfo.GetDirectories())
                {
                    try
                    {
                        await ScanDirectoryRecursiveAsync(dir.FullName, results);
                    }
                    catch
                    {
                        // Skip folder access errors
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip unauthorized folders
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur inattendue : {ex.Message}");
            }
        }

        private static List<FileSystemWatcher> _watchers = new();

        private static void StartWatching()
        {
            var foldersToWatch = new[]
            {
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        GetDownloadsFolderPath()
    };

            foreach (var folder in foldersToWatch)
            {
                if (!Directory.Exists(folder)) continue;

                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Created += (s, e) => AddFile(e.FullPath);
                watcher.Deleted += (s, e) => RemoveFile(e.FullPath);
                watcher.Renamed += (s, e) => UpdateFile(e.OldFullPath, e.FullPath);

                _watchers.Add(watcher);
            }
        }


        private static string GetDownloadsFolderPath()
        {
            // Remplacement de l'utilisation de Environment.SpecialFolder.Downloads  
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        private static void AddFile(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    _index.Add(new MainWindow.SearchResult
                    {
                        DisplayName = Path.GetFileName(path),
                        FullPath = path,
                        Icon = null,
                        IsFolder = true
                    });
                    ScheduleSave();
                }
                else if (File.Exists(path) && ShouldIndexFile(path))
                {
                    _index.Add(new MainWindow.SearchResult
                    {
                        DisplayName = Path.GetFileName(path),
                        FullPath = path,
                        Icon = null,
                        IsFolder = false
                    });
                    ScheduleSave();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'ajout de fichier {path}: {ex.Message}");
            }
        }

        private static void RemoveFile(string path)
        {
            _index.RemoveAll(x => x.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
            ScheduleSave();
        }

        private static void UpdateFile(string oldPath, string newPath)
        {
            var existing = _index.FirstOrDefault(x => x.FullPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.DisplayName = Path.GetFileName(newPath);
                existing.FullPath = newPath;
                existing.IsFolder = Directory.Exists(newPath);
                existing.Icon = null; // Placeholder for icon
            }
            else
            {
                AddFile(newPath);
            }

            ScheduleSave();
        }


        public static List<MainWindow.SearchResult> Search(string query)
        {
            return _index
                .Where(f => f.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
        }

        private static void ScheduleSave()
        {
            if (_saveScheduled) return;
            _saveScheduled = true;

            Task.Delay(30000).ContinueWith(async _ =>
            {
                await SaveIndexAsync();
                _saveScheduled = false;
            });
        }
    }
}
