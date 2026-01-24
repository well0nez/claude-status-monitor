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
            StatusText.Text = "Initialisiere...";
            try
            {
                await _webViewClient.InitializeAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Initialisierung fehlgeschlagen: {ex.Message}";
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
            StatusText.Text = "Aktualisiere Daten...";

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
                StatusText.Text = "Anmeldung erforderlich - bitte neu anmelden";
            }
            else
            {
                StatusText.Text = "Aktualisierung fehlgeschlagen - keine Daten empfangen";
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
            System.Diagnostics.Debug.WriteLine("[MainWindow] OnLoginSuccess aufgerufen!");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OrganizationId: {e.OrganizationId}");

            _webViewClient.SetOrganizationId(e.OrganizationId);

            if (sender is LoginWindow loginWindow)
            {
                loginWindow.Close();
            }
        }

        private void DisplayUsageData(Models.UsageResponse usage)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] DisplayUsageData aufgerufen");
            
            if (usage != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Zeige Usage-Daten an...");
                
                    // Session (5h)
                    if (usage.FiveHour != null)
                    {
                        SessionProgressBar.Value = usage.FiveHour.Utilization;
                        SessionPercent.Text = $"{usage.FiveHour.Utilization:F0}%";
                        
                        if (usage.FiveHour.ResetsAt == null)
                        {
                            SessionResetTime.Text = "Zuruecksetzung: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.FiveHour.ResetsAt.Value.ToLocalTime();
                            var minutesUntilReset = (int)(resetTimeLocal - DateTime.Now).TotalMinutes;
                            
                            if (minutesUntilReset >= 60)
                            {
                                var hours = minutesUntilReset / 60;
                                var minutes = minutesUntilReset % 60;
                                SessionResetTime.Text = $"Zuruecksetzung in {hours} Std. {minutes} Min.";
                            }
                            else
                            {
                                SessionResetTime.Text = $"Zuruecksetzung in {minutesUntilReset} Min.";
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
                            WeeklyResetTime.Text = "Zuruecksetzung: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.SevenDay.ResetsAt.Value.ToLocalTime();
                            var dayName = resetTimeLocal.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                            WeeklyResetTime.Text = $"Zuruecksetzung {dayName}., {resetTimeLocal:HH:mm}";
                        }
                    }

                    // Sonnet (7d)
                    if (usage.SevenDaySonnet != null)
                    {
                        SonnetProgressBar.Value = usage.SevenDaySonnet.Utilization;
                        SonnetPercent.Text = $"{usage.SevenDaySonnet.Utilization:F0}%";
                        
                        if (usage.SevenDaySonnet.ResetsAt == null)
                        {
                            SonnetResetTime.Text = "Zuruecksetzung: --:--";
                        }
                        else
                        {
                            var resetTimeLocal = usage.SevenDaySonnet.ResetsAt.Value.ToLocalTime();
                            var dayName = resetTimeLocal.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                            SonnetResetTime.Text = $"Zuruecksetzung {dayName}., {resetTimeLocal:HH:mm}";
                        }
                    }

                _lastUpdateTime = DateTime.Now;
                UpdateStatusText();
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Usage-Daten erfolgreich angezeigt!");
            }
        }

        private void UpdateStatusText()
        {
            var minutesSinceUpdate = (int)(DateTime.Now - _lastUpdateTime).TotalMinutes;
            if (minutesSinceUpdate < 1)
            {
                StatusText.Text = "Zuletzt aktualisiert: vor weniger als einer Minute";
            }
            else if (minutesSinceUpdate == 1)
            {
                StatusText.Text = "Zuletzt aktualisiert: vor 1 Minute";
            }
            else
            {
                StatusText.Text = $"Zuletzt aktualisiert: vor {minutesSinceUpdate} Minuten";
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

            // Position sperren/entsperren
            var lockMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = _isPositionLocked ? "Position entsperren" : "Position sperren"
            };
            lockMenuItem.Click += (s, args) =>
            {
                _isPositionLocked = !_isPositionLocked;
                StatusText.Text = _isPositionLocked ? "Position gesperrt" : "Position entsperrt";
            };
            contextMenu.Items.Add(lockMenuItem);

            // Aktualisieren
            var refreshMenuItem = new System.Windows.Controls.MenuItem { Header = "Jetzt aktualisieren" };
            refreshMenuItem.Click += async (s, args) => await UpdateUsageDataAsync();
            contextMenu.Items.Add(refreshMenuItem);

            // Neu anmelden
            var loginMenuItem = new System.Windows.Controls.MenuItem { Header = "Neu anmelden" };
            loginMenuItem.Click += async (s, args) =>
            {
                await _webViewClient.ClearSessionAsync();
                OpenLoginWindow();
            };
            contextMenu.Items.Add(loginMenuItem);

            // Separator
            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            // Beenden
            var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Beenden" };
            exitMenuItem.Click += (s, args) => Application.Current.Shutdown();
            contextMenu.Items.Add(exitMenuItem);

            contextMenu.IsOpen = true;
        }
    }
}
