using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ClaudeStatusMonitor.Services;

namespace ClaudeStatusMonitor
{
    public partial class LoginWindow : Window
    {
        private bool _isLoginSuccessful = false;
        private bool _isSilentMode = false;
        private bool _isCheckingLogin = false;
        private const int SILENT_LOGIN_TIMEOUT = 15000; // 15 Sekunden
        private TaskCompletionSource<LoginCheckResult>? _loginCheckTcs;
        private DispatcherTimer? _loginCheckTimer;

        public event EventHandler<LoginSuccessEventArgs>? LoginSuccess;

        public LoginWindow(bool silentMode = false)
        {
            InitializeComponent();
            _isSilentMode = silentMode;
            
            if (_isSilentMode)
            {
                this.ShowInTaskbar = false;
                this.WindowStyle = WindowStyle.None;
                this.Width = 0;
                this.Height = 0;
                this.Opacity = 0;
                this.Left = -10000;
                this.Top = -10000;
            }
            
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                var environment = await WebView2EnvironmentProvider.GetEnvironmentAsync();
                await WebView.EnsureCoreWebView2Async(environment);

                WebView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                WebView.CoreWebView2.Navigate("https://claude.ai/");
                
                System.Diagnostics.Debug.WriteLine($"[LoginWindow] WebView2 ready, silentMode: {_isSilentMode}");
                
                // Nur EINMALIGER Login-Check (KEIN Timer mehr!)
                await CheckLoginStatus();

                if (!_isSilentMode)
                {
                    StartLoginCheckTimer();
                }
                
                // Silent Login Timeout
                if (_isSilentMode)
                {
                    await Task.Delay(SILENT_LOGIN_TIMEOUT);
                    if (!_isLoginSuccessful)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoginWindow] Silent login timeout, closing window");
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (!_isSilentMode)
            {
                await Task.Delay(800);
                await CheckLoginStatus();
            }
        }

        private async Task CheckLoginStatus()
        {
            if (_isLoginSuccessful || _isCheckingLogin)
            {
                return;
            }

            try
            {
                if (WebView?.CoreWebView2 == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginWindow] WebView2 ist noch nicht bereit");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[LoginWindow] Starting login check...");
                _isCheckingLogin = true;

                var tcs = new TaskCompletionSource<LoginCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _loginCheckTcs = tcs;

                var startScript = @"
                    (async function() {
                        try {
                            const orgResponse = await fetch('https://claude.ai/api/organizations', {
                                credentials: 'include'
                            });

                            if (!orgResponse.ok) {
                                window.chrome.webview.postMessage(JSON.stringify({ type: 'login', success: false, status: orgResponse.status }));
                                return;
                            }

                            const organizations = await orgResponse.json();
                            if (!organizations || organizations.length === 0) {
                                window.chrome.webview.postMessage(JSON.stringify({ type: 'login', success: false, error: 'no_orgs' }));
                                return;
                            }

                            const orgId = organizations[0].uuid || organizations[0].id;
                            window.chrome.webview.postMessage(JSON.stringify({ type: 'login', success: true, organizationId: orgId }));
                        } catch (error) {
                            window.chrome.webview.postMessage(JSON.stringify({ type: 'login', success: false, error: error.toString() }));
                        }
                    })();
                ";

                await WebView.CoreWebView2.ExecuteScriptAsync(startScript);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var reg = cts.Token.Register(() =>
                    tcs.TrySetResult(new LoginCheckResult { success = false, error = "timeout" }));

                var result = await tcs.Task;
                if (result != null && result.success)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginWindow] Login successful! OrgId: {result.organizationId}");
                    _isLoginSuccessful = true;
                    StopLoginCheckTimer();

                    LoginSuccess?.Invoke(this, new LoginSuccessEventArgs
                    {
                        SessionKey = string.Empty,
                        OrganizationId = result.organizationId ?? string.Empty,
                        UsageDataJson = null
                    });

                    if (_isSilentMode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LoginWindow] Silent mode: closing window");
                        this.Close();
                    }
                }
                else if (result != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginWindow] Login check failed: {result.error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginWindow] Error in CheckLoginStatus: {ex.Message}");
            }
            finally
            {
                _loginCheckTcs = null;
                _isCheckingLogin = false;
            }
        }

        private void StartLoginCheckTimer()
        {
            if (_loginCheckTimer != null)
            {
                return;
            }

            _loginCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _loginCheckTimer.Tick += async (s, e) => await CheckLoginStatus();
            _loginCheckTimer.Start();
        }

        private void StopLoginCheckTimer()
        {
            if (_loginCheckTimer == null)
            {
                return;
            }

            _loginCheckTimer.Stop();
            _loginCheckTimer = null;
        }

        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(json) || _loginCheckTcs == null)
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

            if (message == null || message.type != "login")
            {
                return;
            }

            _loginCheckTcs.TrySetResult(new LoginCheckResult
            {
                success = message.success,
                organizationId = message.organizationId,
                error = message.error ?? (message.status?.ToString() ?? string.Empty)
            });
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            StopLoginCheckTimer();
            System.Diagnostics.Debug.WriteLine($"[LoginWindow] Window closed");
        }

        private class LoginCheckResult
        {
            public bool success { get; set; }
            public string? organizationId { get; set; }
            public string? error { get; set; }
        }

        private class WebMessage
        {
            public string? type { get; set; }
            public bool success { get; set; }
            public int? status { get; set; }
            public string? error { get; set; }
            public string? organizationId { get; set; }
        }
    }

    public class LoginSuccessEventArgs : EventArgs
    {
        public string SessionKey { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public string? UsageDataJson { get; set; } = null;
    }
}
