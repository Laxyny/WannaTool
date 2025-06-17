using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using Timer = System.Timers.Timer;

namespace WannaTool
{
    public static class Indexer
    {
        // Struct léger pour l'index en mémoire
        public readonly record struct IndexEntry(string DisplayName, string FullPath, bool IsFolder);
        private static readonly string IndexFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.json.gz");
        private static List<IndexEntry> _index = new List<IndexEntry>();
        public static bool IsReady { get; set; } = false;

        public static void MarkAsReady() => IsReady = true;

        private static readonly Timer _saveTimer = new Timer(5000)
        {
            AutoReset = false
        };

        static Indexer()
        {
            // À la fin des 5 s sans nouvel appel, on sauve.
            _saveTimer.Elapsed += async (s, e) => {
                await SaveIndexAsync();
            };
        }

        // Extensions autorisées
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".pdf", ".docx", ".xlsx", ".pptx", ".txt",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".avi", ".mkv", ".mov",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".html", ".css", ".js", ".json", ".xml", ".csv",
            ".psd", ".ai", ".blend", ".fbx", ".obj",
            ".apk", ".iso", ".bat", ".cmd"
        };

        // Extensions ignorées
        private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".sys", ".tmp", ".log", ".bak", ".old",
            ".cache", ".msi", ".dat", ".bin", ".db", ".db-shm", ".db-wal",
            ".jsonl", ".lock", ".idx", ".thumb", ".mdmp", ".dmp"
        };

        // Dossiers à ignorer
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

        // Initialisation de l'index
        public static async Task InitializeAsync()
        {
            if (File.Exists(IndexFilePath))
            { 
                await LoadIndexAsync();
                _ = SyncIndexAsync();
            }
            else
            {
                await BuildIndexAsync();
                await SaveIndexAsync();
            }

            StartWatching();
            IsReady = true;
        }

        // Chargement de l'index depuis le fichier compressé
        public static async Task LoadIndexAsync()
        {
            try
            {
                using var fileStream = new FileStream(IndexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
                _index = await JsonSerializer.DeserializeAsync<List<IndexEntry>>(gzip) ?? new();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Aucun index existant trouvé. Reconstruction...");
                await BuildIndexAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de l'index: {ex.Message}");
            }
        }

        public static async Task SyncIndexAsync()
        {
            // Construire l'ensemble (HashSet) des chemins actuels
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                GetDownloadsFolderPath()
            };
            var actualPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                // Dossiers
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        if (ShouldIndexFolder(dir))
                            actualPaths.Add(dir);
                    }
                }
                catch (UnauthorizedAccessException) { /* skip this branch */ }
                catch (PathTooLongException) { /* skip */ }

                // Fichiers
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (ShouldIndexFile(file))
                            actualPaths.Add(file);
                    }
                }
                catch (UnauthorizedAccessException) { /* skip this branch */ }
                catch (PathTooLongException) { /* skip */ }
            }

            // Supprimer les entrées obsolètes
            _index.RemoveAll(e => !actualPaths.Contains(e.FullPath));

            // Ajouter les nouveaux chemins
            var knownPaths = new HashSet<string>(_index.Select(e => e.FullPath), StringComparer.OrdinalIgnoreCase);
            foreach (var path in actualPaths)
            {
                if (knownPaths.Add(path))
                {
                    var name = Path.GetFileName(path);
                    var isFolder = Directory.Exists(path);
                    _index.Add(new IndexEntry(name, path, isFolder));
                }
            }

            // Sauvegarde immédiate
            await SaveIndexAsync();
        }

        // Efface l'index en mémoire
        public static void ClearIndex()
        {
            _index.Clear();
            IsReady = false;
        }

        // Sauvegarde de l'index dans un .gz
        public static async Task SaveIndexAsync()
        {
            try
            {
                using var fileStream = new FileStream(IndexFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var gzip = new GZipStream(fileStream, CompressionMode.Compress);
                await JsonSerializer.SerializeAsync(gzip, _index);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde de l'index: {ex.Message}");
            }
        }

        // Construction initiale de l'index
        private static async Task BuildIndexAsync()
        {
            var rootFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                GetDownloadsFolderPath()
            };
            var entries = new List<IndexEntry>();
            Utils.ShowToast("Indexing started", "WannaTool is scanning your files...");

            foreach (var root in rootFolders)
            {
                if (Directory.Exists(root))
                    await ScanDirectoryRecursiveAsync(root, entries);
            }

            _index = entries;
            Utils.ShowToast("Indexing completed", $"{_index.Count} entrées indexées");
            Console.WriteLine($"Indexation terminée avec {_index.Count} entrées.");
        }

        // Parcours récursif et ajout d'entrées légères
        private static async Task ScanDirectoryRecursiveAsync(string currentFolder, List<IndexEntry> entries)
        {
            try
            {
                if (!ShouldIndexFolder(currentFolder))
                    return;

                var dirInfo = new DirectoryInfo(currentFolder);
                entries.Add(new IndexEntry(dirInfo.Name, dirInfo.FullName, true));

                foreach (var file in dirInfo.GetFiles())
                {
                    if (ShouldIndexFile(file.FullName))
                        entries.Add(new IndexEntry(file.Name, file.FullName, false));
                }

                foreach (var dir in dirInfo.GetDirectories())
                    await ScanDirectoryRecursiveAsync(dir.FullName, entries);
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du scan du dossier {currentFolder}: {ex.Message}");
            }
        }

        private static List<FileSystemWatcher> _watchers = new();

        // Surveillance des dossiers clés
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

        private static void AddFile(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    _index.Add(new IndexEntry(Path.GetFileName(path), path, true));
                }
                else if (File.Exists(path) && ShouldIndexFile(path))
                {
                    _index.Add(new IndexEntry(Path.GetFileName(path), path, false));
                }
                ScheduleSave();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ajout fichier {path}: {ex.Message}");
            }
        }

        private static void RemoveFile(string path)
        {
            _index.RemoveAll(e => e.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
            ScheduleSave();
        }

        private static void UpdateFile(string oldPath, string newPath)
        {
            RemoveFile(oldPath);
            AddFile(newPath);
            ScheduleSave();
        }

        // Recherche : on recrée les objets WPF uniquement pour l'affichage
        public static List<MainWindow.SearchResult> Search(string query)
        {
            return _index
                .Where(e => e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .Select(e => new MainWindow.SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = e.IsFolder,
                })
                .ToList();
        }

        public static List<MainWindow.SearchResult> Search(string query, IEnumerable<string> extensions)
        {
            var extSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
            return _index
                .Where(e =>
                {
                    if (e.IsFolder) return false;
                    var ext = Path.GetExtension(e.FullPath);
                    return extSet.Contains(ext)
                        && e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
                })
                .Take(20)
                .Select(e => new MainWindow.SearchResult
                {
                    DisplayName = e.DisplayName,
                    FullPath = e.FullPath,
                    IsFolder = false
                })
                .ToList();
        }

        private static void ScheduleSave()
        {
            // À chaque événement, on redémarre le timer
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private static string GetDownloadsFolderPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}