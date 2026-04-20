using ObjLoader.Localization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace ObjLoader.Plugin.Utilities
{
    public static class VersionChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static int _checkState = 0;

        public static void CheckVersion()
        {
            if (Interlocked.CompareExchange(ref _checkState, 1, 0) != 0)
                return;

            _ = ExecuteCheckAsync();
        }

        private static async Task ExecuteCheckAsync()
        {
            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null)
                    return;

                var latestVersionTag = await GetLatestVersionTagAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(latestVersionTag))
                    return;

                var latestVersion = ParseVersion(latestVersionTag);
                if (latestVersion <= currentVersion)
                    return;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                await dispatcher.InvokeAsync(() =>
                {
                    if (MessageBox.Show(
                            $"{string.Format(Texts.UpdateNotificationMessage, latestVersion)}{Environment.NewLine}{Texts.UpdateNotificationPrompt}",
                            Texts.UpdateNotificationTitle,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://github.com/routersys/YMM4-ObjLoader/releases",
                            UseShellExecute = true
                        });
                    }
                });
            }
            catch
            {
            }
        }

        private static async Task<string?> GetLatestVersionTagAsync()
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/routersys/YMM4-ObjLoader/releases/latest");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("YMM4-ObjLoader", assemblyVersion));

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                    if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                        return tag.GetString();
                }
            }
            catch { }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://manjubox.net/api/ymm4plugins/github/detail/routersys/YMM4-ObjLoader");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("YMM4-ObjLoader", assemblyVersion));

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                    if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                        return tag.GetString();
                }
            }
            catch { }

            return null;
        }

        private static Version ParseVersion(string tag)
        {
            var v = tag.TrimStart('v', 'V');
            if (Version.TryParse(v, out var version))
                return version;
            return new Version(0, 0, 0);
        }
    }
}