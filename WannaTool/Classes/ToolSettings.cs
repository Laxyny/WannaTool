using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WannaTool
{
    public class WebRedirect
    {
        public string Name { get; set; } = "";
        public string Trigger { get; set; } = "";
        public string UrlTemplate { get; set; } = "";
    }

    public class ToolSettings
    {
        public bool AutoStart { get; set; } = false;
        public List<WebRedirect> Redirects { get; set; } = new List<WebRedirect>();

        public ToolSettings()
        {
            Redirects = new List<WebRedirect>
            {
                new WebRedirect { Name = "Google", Trigger = "google", UrlTemplate = "https://www.google.com/search?q={0}" },
                new WebRedirect { Name = "Bing", Trigger = "bing", UrlTemplate = "https://www.bing.com/search?q={0}" },
                new WebRedirect { Name = "DuckDuckGo", Trigger = "ddg", UrlTemplate = "https://duckduckgo.com/?q={0}" },
                new WebRedirect { Name = "Brave", Trigger = "brave", UrlTemplate = "https://search.brave.com/search?q={0}" },
                new WebRedirect { Name = "YouTube", Trigger = "youtube", UrlTemplate = "https://www.youtube.com/results?search_query={0}" },
                new WebRedirect { Name = "Wikipedia", Trigger = "wiki", UrlTemplate = "https://en.wikipedia.org/wiki/Special:Search?search={0}" },
                new WebRedirect { Name = "StackOverflow", Trigger = "stackoverflow", UrlTemplate = "https://stackoverflow.com/search?q={0}" }
            };
        }
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        public static ToolSettings Current { get; private set; } = new ToolSettings();

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<ToolSettings>(json);
                    if (settings != null)
                    {
                        Current = settings;
                        return;
                    }
                }
                catch { }
            }
            Current = new ToolSettings();
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
