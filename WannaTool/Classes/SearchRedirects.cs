using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WannaTool
{
    public static class SearchRedirects
    {
        private static readonly Dictionary<string, string> EngineUrls = new(StringComparer.OrdinalIgnoreCase)
        {
            ["google"] = "https://www.google.com/search?q={0}",
            ["bing"] = "https://www.bing.com/search?q={0}",
            ["ddg"] = "https://duckduckgo.com/?q={0}",
            ["brave"] = "https://search.brave.com/search?q={0}",
            ["youtube"] = "https://www.youtube.com/results?search_query={0}",
            ["wiki"] = "https://en.wikipedia.org/wiki/Special:Search?search={0}",
            ["stackoverflow"] = "https://stackoverflow.com/search?q={0}",
            ["youtube"] = "https://www.youtube.com/results?search_query={0}"
        };

        public static SearchResult? TryParseRedirect(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;

            if (query.Trim().Equals("!help", StringComparison.OrdinalIgnoreCase))
            {
                return new SearchResult
                {
                    DisplayName = "WannaTool Help",
                    FullPath = "!help"
                };
            }

            if (query.Trim().Equals("!exit", StringComparison.OrdinalIgnoreCase))
            {
                return new SearchResult
                {
                    DisplayName = "Exit WannaTool",
                    FullPath = "!exit"
                };
            }

            int colon = query.IndexOf(':');
            if (colon <= 0 || colon == query.Length - 1) return null;

            string prefix = query.Substring(0, colon).ToLower();
            string searchTerms = query[(colon + 1)..].Trim();

            if (EngineUrls.TryGetValue(prefix, out var template) && !string.IsNullOrWhiteSpace(searchTerms))
            {
                string url = string.Format(template, Uri.EscapeDataString(searchTerms));
                return new SearchResult
                {
                    DisplayName = $"Redirect to {prefix} : \"{searchTerms}\"",
                    FullPath = url
                };
            }

            return null;
        }
    }
}
