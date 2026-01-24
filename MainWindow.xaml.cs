using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClaudeStatusMonitor.Models;
using ClaudeStatusMonitor.Services;

namespace ClaudeStatusMonitor
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly ClaudeWebViewClient _webViewClient;
        private bool _isPositionLocked = false;
        private DateTime _lastUpdateTime;
        private bool _isLoginWindowOpen = false;
        private bool _manualLoginActive = false;
        private bool _isUpdating = false;

        public MainWindow()
        {
            InitializeComponent();

            _webViewClient = new ClaudeWebViewClient(BackgroundWebView);

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(SecureStorageService.GetRefreshInterval());
            _refreshTimer.Tick += RefreshTimer_Tick;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Initializing...";
            try
            {
                await _webViewClient.InitializeAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Initialization failed: {ex.Message}";
                return;
            }

            await UpdateUsageDataAsync();
        }

        private async Task UpdateUsageDataAsync()
        {
            if (_manualLoginActive || _isUpdating)
            {
                return;
            }

            _isUpdating = true;
            StatusText.Text = "Updating data...";

            UsageFetchResult result;
            try
            {
                result = await _webViewClient.FetchUsageAsync();
            }
            catch (Exception ex)
            {
                result = new UsageFetchResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            _isUpdating = false;

            if (result.Success && result.Usage != null)
            {
                DisplayUsageData(result.Usage);
            }
            else if (result.RequiresLogin)
            {
                StatusText.Text = "Login required - please sign in again";
            }
            else
            {
                StatusText.Text = "Update failed - no data received";
            }

            ResetRefreshTimer();
        }

        private void ResetRefreshTimer()
        {
            if (_manualLoginActive)
            {
                return;
            }

            _refreshTimer.Stop();
            _refreshTimer.Interval = TimeSpan.FromMinutes(SecureStorageService.GetRefreshInterval());
            _refreshTimer.Start();
        }

        private void OpenLoginWindow()
        {
            if (_isLoginWindowOpen)
            {
                return;
            }

            _manualLoginActive = true;
            _isLoginWindowOpen = true;
            _refreshTimer.Stop();

            var loginWindow = new LoginWindow(silentMode: false);
            loginWindow.LoginSuccess += OnLoginSuccess;
            loginWindow.Closed += async (s, e) =>
            {
                _isLoginWindowOpen = false;
                _manualLoginActive = false;
                await UpdateUsageDataAsync();
            };
            loginWindow.Show();
        }

        private void OnLoginSuccess(object? sender, LoginSuccessEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] OnLoginSuccess called!");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OrganizationId: {e.OrganizationId}");

            _webViewClient.SetOrganizationId(e.OrganizationId);

            if (sender is LoginWindow loginWindow)
            {
                loginWindow.Close();
            }
        }

        private void DisplayUsageData(Models.UsageResponse usage)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] DisplayUsageData called");
            
            if (usage != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Rendering usage data...");
                
                    // Session (5h)
                    if (usage.FiveHour != null)
                    {
                        SessionProgressBar.Value = usage.FiveHour.Utilization;
                        SessionPercent.Text = $"{usage.FiveHour.Utilization:F0}%";
                        
                        if (usage.FiveHour.ResetsAt == null)
                        {
                            SessionResetTime.Text = "Reset: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.FiveHour.ResetsAt.Value.ToLocalTime();
                            var minutesUntilReset = (int)(resetTimeLocal - DateTime.Now).TotalMinutes;
                            
                            if (minutesUntilReset >= 60)
                            {
                                var hours = minutesUntilReset / 60;
                                var minutes = minutesUntilReset % 60;
                                SessionResetTime.Text = $"Reset in {hours} h {minutes} min";
                            }
                            else
                            {
                                SessionResetTime.Text = $"Reset in {minutesUntilReset} min";
                            }
                        }
                    }

                    // Weekly (7d)
                    if (usage.SevenDay != null)
                    {
                        WeeklyProgressBar.Value = usage.SevenDay.Utilization;
                        WeeklyPercent.Text = $"{usage.SevenDay.Utilization:F0}%";
                        
                        if (usage.SevenDay.ResetsAt == null)
                        {
                            WeeklyResetTime.Text = "Reset: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.SevenDay.ResetsAt.Value.ToLocalTime();
                            var dayName = resetTimeLocal.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                            WeeklyResetTime.Text = $"Reset {dayName}, {resetTimeLocal:HH:mm}";
                        }
                    }

                    // Sonnet (7d)
                    if (usage.SevenDaySonnet != null)
                    {
                        SonnetProgressBar.Value = usage.SevenDaySonnet.Utilization;
                        SonnetPercent.Text = $"{usage.SevenDaySonnet.Utilization:F0}%";
                        
                        if (usage.SevenDaySonnet.ResetsAt == null)
                        {
                            SonnetResetTime.Text = "Reset: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.SevenDaySonnet.ResetsAt.Value.ToLocalTime();
                            var dayName = resetTimeLocal.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                            SonnetResetTime.Text = $"Reset {dayName}, {resetTimeLocal:HH:mm}";
                        }
                    }

                _lastUpdateTime = DateTime.Now;
                UpdateStatusText();
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Usage data rendered.");
            }
        }

        private void UpdateStatusText()
        {
            var minutesSinceUpdate = (int)(DateTime.Now - _lastUpdateTime).TotalMinutes;
            if (minutesSinceUpdate < 1)
            {
                StatusText.Text = "Last updated: less than a minute ago";
            }
            else if (minutesSinceUpdate == 1)
            {
                StatusText.Text = "Last updated: 1 minute ago";
            }
            else
            {
                StatusText.Text = $"Last updated: {minutesSinceUpdate} minutes ago";
            }
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            await UpdateUsageDataAsync();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPositionLocked)
            {
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // Ignore exception if drag fails
                }
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();

            // Lock position
            var lockMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = _isPositionLocked ? "Unlock position" : "Lock position"
            };
            lockMenuItem.Click += (s, args) =>
            {
                _isPositionLocked = !_isPositionLocked;
                StatusText.Text = _isPositionLocked ? "Position locked" : "Position unlocked";
            };
            contextMenu.Items.Add(lockMenuItem);

            // Refresh
            var refreshMenuItem = new System.Windows.Controls.MenuItem { Header = "Refresh now" };
            refreshMenuItem.Click += async (s, args) => await UpdateUsageDataAsync();
            contextMenu.Items.Add(refreshMenuItem);

            // Re-login
            var loginMenuItem = new System.Windows.Controls.MenuItem { Header = "Re-login" };
            loginMenuItem.Click += async (s, args) =>
            {
                await _webViewClient.ClearSessionAsync();
                OpenLoginWindow();
            };
            contextMenu.Items.Add(loginMenuItem);

            // Separator
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            // Exit
            var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitMenuItem.Click += (s, args) => Application.Current.Shutdown();
            contextMenu.Items.Add(exitMenuItem);

            contextMenu.IsOpen = true;
        }
    }
}
