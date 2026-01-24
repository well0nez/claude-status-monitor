using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace ClaudeStatusMonitor.Services
{
    public static class WebView2EnvironmentProvider
    {
        private static Task<CoreWebView2Environment>? _environmentTask;

        public static Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environmentTask != null)
            {
                return _environmentTask;
            }

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClaudeStatusMonitor",
                "WebView2"
            );

            Directory.CreateDirectory(userDataFolder);
            _environmentTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);
            return _environmentTask;
        }
    }
}
