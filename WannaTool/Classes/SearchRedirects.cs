using System;
using System.Linq;

namespace WannaTool
{
    public static class SearchRedirects
    {
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

            if (query.Trim().Equals("!settings", StringComparison.OrdinalIgnoreCase))
            {
                return new SearchResult
                {
                    DisplayName = "Open Settings",
                    FullPath = "!settings"
                };
            }

            int colon = query.IndexOf(':');
            if (colon <= 0 || colon == query.Length - 1) return null;

            string prefix = query.Substring(0, colon).ToLower();
            string searchTerms = query[(colon + 1)..].Trim();

            var redirect = SettingsManager.Current.Redirects
                .FirstOrDefault(r => r.Trigger.Equals(prefix, StringComparison.OrdinalIgnoreCase));

            if (redirect != null && !string.IsNullOrWhiteSpace(searchTerms))
            {
                string url = string.Format(redirect.UrlTemplate, Uri.EscapeDataString(searchTerms));
                return new SearchResult
                {
                    DisplayName = $"{redirect.Name}: \"{searchTerms}\"",
                    FullPath = url
                };
            }

            return null;
        }
    }
}
