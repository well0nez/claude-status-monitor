using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ClaudeStatusMonitor.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ClaudeStatusMonitor.Services
{
    public sealed class ClaudeWebViewClient
    {
        private readonly WebView2 _webView;
        private readonly SemaphoreSlim _requestGate = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<UsageFetchResult>? _pendingUsage;
        private bool _initialized;
        private string? _organizationId;

        public ClaudeWebViewClient(WebView2 webView)
        {
            _webView = webView;
        }

        public string? OrganizationId => _organizationId;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            var environment = await WebView2EnvironmentProvider.GetEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.Navigate("https://claude.ai/");
            _initialized = true;
        }

        public void SetOrganizationId(string? organizationId)
        {
            _organizationId = string.IsNullOrWhiteSpace(organizationId) ? null : organizationId;
        }

        public async Task<UsageFetchResult> FetchUsageAsync(TimeSpan? timeout = null)
        {
            await InitializeAsync();

            await _requestGate.WaitAsync();
            try
            {
                var tcs = new TaskCompletionSource<UsageFetchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingUsage = tcs;

                var orgLiteral = _organizationId == null ? "null" : JsonSerializer.Serialize(_organizationId);
                var script = $@"
                    (async function() {{
                        try {{
                            let orgId = {orgLiteral};
                            if (!orgId) {{
                                const orgResp = await fetch('https://claude.ai/api/organizations', {{ credentials: 'include' }});
                                if (!orgResp.ok) {{
                                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'usage', success: false, status: orgResp.status }}));
                                    return;
                                }}

                                const orgs = await orgResp.json();
                                if (!orgs || orgs.length === 0) {{
                                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'usage', success: false, error: 'no_orgs' }}));
                                    return;
                                }}

                                orgId = orgs[0].uuid || orgs[0].id;
                            }}

                            const usageResp = await fetch(`https://claude.ai/api/organizations/${{orgId}}/usage`, {{ credentials: 'include' }});
                            if (!usageResp.ok) {{
                                window.chrome.webview.postMessage(JSON.stringify({{ type: 'usage', success: false, status: usageResp.status, organizationId: orgId }}));
                                return;
                            }}

                            const data = await usageResp.json();
                            window.chrome.webview.postMessage(JSON.stringify({{ type: 'usage', success: true, organizationId: orgId, data: data }}));
                        }} catch (error) {{
                            window.chrome.webview.postMessage(JSON.stringify({{ type: 'usage', success: false, error: error.toString() }}));
                        }}
                    }})();
                ";

                await _webView.CoreWebView2.ExecuteScriptAsync(script);

                var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(12);
                using var cts = new CancellationTokenSource(effectiveTimeout);
                using var registration = cts.Token.Register(() =>
                    tcs.TrySetResult(UsageFetchResult.Timeout()));

                return await tcs.Task;
            }
            finally
            {
                _pendingUsage = null;
                _requestGate.Release();
            }
        }

        public async Task ClearSessionAsync()
        {
            if (!_initialized || _webView.CoreWebView2 == null)
            {
                _organizationId = null;
                return;
            }

            if (_webView.Dispatcher.CheckAccess())
            {
                await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
            }
            else
            {
                await _webView.Dispatcher.InvokeAsync(async () =>
                {
                    await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
                }, DispatcherPriority.Send);
            }

            _organizationId = null;
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            WebMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<WebMessage>(json);
            }
            catch
            {
                return;
            }

            if (message == null || message.type != "usage" || _pendingUsage == null)
            {
                return;
            }

            if (message.success)
            {
                if (!string.IsNullOrWhiteSpace(message.organizationId))
                {
                    _organizationId = message.organizationId;
                }

                UsageResponse? usage = null;
                if (message.data.HasValue && message.data.Value.ValueKind != JsonValueKind.Null)
                {
                    try
                    {
                        usage = JsonSerializer.Deserialize<UsageResponse>(message.data.Value.GetRawText());
                    }
                    catch
                    {
                        usage = null;
                    }
                }

                _pendingUsage.TrySetResult(new UsageFetchResult
                {
                    Success = usage != null,
                    Usage = usage,
                    Error = usage == null ? "parse_error" : null
                });
            }
            else
            {
                var requiresLogin = message.status == 401 || message.status == 403;
                if (requiresLogin)
                {
                    _organizationId = null;
                }

                _pendingUsage.TrySetResult(new UsageFetchResult
                {
                    Success = false,
                    RequiresLogin = requiresLogin,
                    Error = message.error ?? (message.status?.ToString() ?? "unknown_error")
                });
            }
        }

        private sealed class WebMessage
        {
            public string? type { get; set; }
            public bool success { get; set; }
            public int? status { get; set; }
            public string? error { get; set; }
            public string? organizationId { get; set; }
            public JsonElement? data { get; set; }
        }
    }

    public sealed class UsageFetchResult
    {
        public bool Success { get; init; }
        public bool RequiresLogin { get; init; }
        public string? Error { get; init; }
        public UsageResponse? Usage { get; init; }

        public static UsageFetchResult Timeout()
        {
            return new UsageFetchResult
            {
                Success = false,
                Error = "timeout"
            };
        }
    }
}
