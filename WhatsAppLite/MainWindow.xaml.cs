using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WhatsAppLite;

public partial class MainWindow : Window
{
    private List<TabInfo> _tabs = new List<TabInfo>();
    private TabInfo? _selectedTab;
    private bool _isInitialized;
    private bool _isRetrying;
    private int _initializingCount;
    private int _nextTabNumber = 1;
    private const string BaseUserDataFolder = "WhatsAppLiteProfile";
    private const string AllowedHost = "web.whatsapp.com";

    public MainWindow()
    {
        InitializeComponent();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        InitializeFirstTabAsync();
    }

    private async void InitializeFirstTabAsync()
    {
        ShowLoading("Initializing...", 0.1);
        _initializingCount = 0;
        await AddTabAsync();
    }

    private async Task AddTabAsync()
    {
        if (_isRetrying) return;
        _isRetrying = true;
        _initializingCount++;

        try
        {
            ShowLoading($"Opening new tab...", 0.2 + (_tabs.Count * 0.1));

            var webView = new WebView2();
            webView.HorizontalAlignment = HorizontalAlignment.Stretch;
            webView.VerticalAlignment = VerticalAlignment.Stretch;

            var tabInfo = new TabInfo { WebView = webView };
            _tabs.Add(tabInfo);

            webView.Loaded += async (wvSender, wvArgs) =>
            {
                try
                {
                    var userDataFolder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        BaseUserDataFolder);

                    var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await webView.EnsureCoreWebView2Async(environment);

                    ConfigureWebView(webView);
                    RegisterTabEventHandlers(webView);

                    await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                        "Emulation.setIdleOverride",
                        "{\"isUserActive\":true,\"isScreenUnlocked\":true}");

                    webView.Source = new Uri("https://web.whatsapp.com");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        Debug.WriteLine($"[ERROR] WebView2 init failed: {ex.Message}");
                    });
                }
            };

            CreateTabButton(tabInfo);
            SelectTab(tabInfo);

            _initializingCount--;
            if (_initializingCount == 0)
            {
                ShowBrowser();
            }
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _initializingCount--;
            if (_initializingCount == 0)
            {
                ShowError("WebView2 Runtime Not Found",
                    "The Microsoft Edge WebView2 Runtime is required but not installed.\n\nDownload it from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                    ErrorType.Runtime);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _initializingCount--;
            if (_initializingCount == 0)
            {
                ShowError("Access Denied",
                    "The application does not have permission to write to the user data folder.\n\nTry running the application as administrator.",
                    ErrorType.Permission);
            }
        }
        catch (Exception ex)
        {
            _initializingCount--;
            if (_initializingCount == 0)
            {
                ShowError("Unexpected Error", $"An unexpected error occurred:\n\n{ex.Message}", ErrorType.Unknown);
            }
        }
        finally
        {
            _isRetrying = false;
        }
    }

    private void CreateTabButton(TabInfo tabInfo)
    {
        string tabName = $"Tab {_nextTabNumber++}";

        var tabButton = new Button
        {
            Style = (Style)FindResource("TabButtonStyle"),
            Content = tabName,
            DataContext = tabInfo
        };

        tabButton.Click += TabButton_Click;

        TabButtonsPanel.Children.Add(tabButton);
        tabInfo.TabButton = tabButton;
        tabInfo.TabName = tabName;
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TabInfo tabInfo)
        {
            SelectTab(tabInfo);
        }
    }

    private void SelectTab(TabInfo tabInfo)
    {
        var normalStyle = (Style)FindResource("TabButtonStyle");
        var selectedStyle = (Style)FindResource("SelectedTabButtonStyle");

        foreach (var tab in _tabs)
        {
            if (tab.TabButton != null)
            {
                bool isSelected = (tab == tabInfo);
                tab.TabButton.Style = isSelected ? selectedStyle : normalStyle;
            }
        }

        _selectedTab = tabInfo;
        TabContentPresenter.Content = tabInfo.WebView;
    }

    private void TabClose_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        var tabInfo = FindTabFromElement(sender as DependencyObject);
        if (tabInfo != null)
        {
            CloseTab(tabInfo);
        }
    }

    private TabInfo? FindTabFromElement(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is Button tabButton && tabButton.DataContext is TabInfo tabInfo)
            {
                return tabInfo;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void CloseTab(TabInfo tabInfo)
    {
        if (_tabs.Count <= 1) return;

        var tabIndex = _tabs.IndexOf(tabInfo);
        tabInfo.WebView.Dispose();

        if (tabInfo.TabButton != null)
        {
            TabButtonsPanel.Children.Remove(tabInfo.TabButton);
        }
        _tabs.Remove(tabInfo);

        if (_selectedTab == tabInfo)
        {
            var newIndex = Math.Min(tabIndex, _tabs.Count - 1);
            SelectTab(_tabs[newIndex]);
        }
    }

    private void ConfigureWebView(WebView2 webView)
    {
        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
        webView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
    }

    private void RegisterTabEventHandlers(WebView2 webView)
    {
        webView.CoreWebView2.ProcessFailed += (s, e) => OnTabProcessFailed(webView, e);
        webView.CoreWebView2.NavigationCompleted += (s, e) => OnTabNavigationCompleted(webView, e);
        webView.CoreWebView2.NavigationStarting += (s, e) => OnTabNavigationStarting(webView, e);
        webView.CoreWebView2.SourceChanged += (s, e) => OnTabSourceChanged(webView, e);
    }

    private void OnTabNavigationCompleted(WebView2 webView, CoreWebView2NavigationCompletedEventArgs e)
    {
        var tabInfo = _tabs.FirstOrDefault(t => t.WebView == webView);
        if (tabInfo == null) return;

        if (e.IsSuccess)
        {
            Dispatcher.Invoke(() =>
            {
                _isInitialized = true;
                ShowBrowser();
            });
        }
        else
        {
            Dispatcher.Invoke(() =>
            {
                ShowError("Connection Failed",
                    "Could not load WhatsApp Web.\n\nCheck your internet connection and try again.",
                    ErrorType.Network);
            });
        }
    }

    private void OnTabProcessFailed(WebView2 webView, CoreWebView2ProcessFailedEventArgs e)
    {
        var (title, message, errorType, shouldAutoReload) = e.ProcessFailedKind switch
        {
            CoreWebView2ProcessFailedKind.BrowserProcessExited =>
            ("Browser Process Crashed",
             "The WebView2 browser process has exited unexpectedly.\n\nThe application will attempt to recover automatically.",
             ErrorType.Crash, true),
            CoreWebView2ProcessFailedKind.RenderProcessExited =>
            ("Renderer Process Crashed",
             "The page renderer has crashed. This is usually temporary.\n\nPlease wait while the page reloads.",
             ErrorType.Crash, true),
            CoreWebView2ProcessFailedKind.RenderProcessUnresponsive =>
            ("Page Unresponsive",
             "WhatsApp Web has become unresponsive.\n\nThe page will be reloaded automatically.",
             ErrorType.Crash, true),
            CoreWebView2ProcessFailedKind.FrameRenderProcessExited =>
            ("Frame Renderer Crashed",
             "A frame within the page has crashed. The page will be reloaded.\n\nIf this keeps happening, try clearing the app data.",
             ErrorType.Crash, true),
            _ =>
            ("Unexpected Process Failure",
             "An unexpected error occurred in the browser process.\n\nPlease try reloading the page.",
             ErrorType.Unknown, false)
        };

        Dispatcher.Invoke(() => ShowError(title, message, errorType));

        if (shouldAutoReload && webView.CoreWebView2 != null)
        {
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                Dispatcher.Invoke(() => webView.CoreWebView2?.Reload());
            });
        }
    }

    private void OnTabNavigationStarting(WebView2 webView, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("https://web.whatsapp.com") ||
            e.Uri.StartsWith("https://accounts.google.com") ||
            e.Uri.StartsWith("https://s.whatsapp.net") ||
            e.Uri == "about:blank")
        {
            return;
        }

        if (e.Uri.StartsWith("http") && !e.Uri.Contains(AllowedHost))
        {
            e.Cancel = true;
            webView.CoreWebView2.ExecuteScriptAsync($"window.open('{e.Uri}', '_blank');");
        }
    }

    private void OnTabSourceChanged(WebView2 webView, CoreWebView2SourceChangedEventArgs e)
    {
        var tabInfo = _tabs.FirstOrDefault(t => t.WebView == webView);
        if (tabInfo == null) return;

        if (webView.CoreWebView2?.Source == null) return;

        var sourceUri = webView.CoreWebView2.Source;
        if (!sourceUri.Contains(AllowedHost) && sourceUri != "about:blank")
        {
            Dispatcher.Invoke(() => webView.Source = new Uri("https://web.whatsapp.com"));
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.IsAvailable && _isInitialized && _selectedTab?.WebView?.CoreWebView2 != null)
            {
                if (TabContentPresenter.Content == null || LoadingScreen.Visibility == Visibility.Visible)
                {
                    ShowLoading("Connection restored. Reconnecting...", 0.5);
                }
                _selectedTab.WebView.Source = new Uri("https://web.whatsapp.com");
            }
            else if (!e.IsAvailable && TabContentPresenter.Content != null)
            {
                ShowError("Connection Lost",
                    "Your internet connection has been lost.\n\nThe app will automatically reconnect when the connection is restored.",
                    ErrorType.Network);
            }
        });
    }

    private void ShowLoading(string status, double? progressPercent = null)
    {
        LoadingScreen.Visibility = Visibility.Visible;
        ErrorScreen.Visibility = Visibility.Collapsed;
        TabContentPresenter.Visibility = Visibility.Collapsed;
        LoadingStatus.Text = status;

        if (progressPercent.HasValue)
        {
            var progressAnimation = new DoubleAnimation
            {
                To = progressPercent.Value * 200,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBarFill.BeginAnimation(WidthProperty, progressAnimation);
        }
        else
        {
            var progressAnimation = new DoubleAnimation
            {
                To = 200,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBarFill.BeginAnimation(WidthProperty, progressAnimation);
        }

        var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
        LoadingScreen.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void ShowError(string title, string message, ErrorType errorType)
    {
        if (LoadingScreen.Visibility == Visibility.Visible)
        {
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                LoadingScreen.Visibility = Visibility.Collapsed;
                ErrorScreen.Visibility = Visibility.Visible;
                TabContentPresenter.Visibility = Visibility.Collapsed;
                ShowErrorContent(title, message, errorType);
            };
            LoadingScreen.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            LoadingScreen.Visibility = Visibility.Collapsed;
            ErrorScreen.Visibility = Visibility.Visible;
            TabContentPresenter.Visibility = Visibility.Collapsed;
            ShowErrorContent(title, message, errorType);
        }
    }

    private void ShowErrorContent(string title, string message, ErrorType errorType)
    {
        ErrorTitle.Text = title;
        ErrorMessage.Text = message;

        ErrorIcon.Text = errorType switch
        {
            ErrorType.Network => "\uE704",
            ErrorType.Runtime => "\uE946",
            ErrorType.Permission => "\uE72E",
            ErrorType.Crash => "\uEDEC",
            ErrorType.FileSystem => "\uE946",
            _ => "\uE7BA"
        };

        ErrorIcon.Foreground = errorType switch
        {
            ErrorType.Network => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB900")),
            ErrorType.Crash => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"))
        };

        DownloadRuntimeButton.Visibility = errorType == ErrorType.Runtime ? Visibility.Visible : Visibility.Collapsed;

        ErrorScreen.Opacity = 0;
        ErrorScreen.Visibility = Visibility.Visible;
        var fadeInError = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
        ErrorScreen.BeginAnimation(OpacityProperty, fadeInError);
    }

    private void ShowBrowser()
    {
        var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) =>
        {
            LoadingScreen.Visibility = Visibility.Collapsed;
            ErrorScreen.Visibility = Visibility.Collapsed;
            TabContentPresenter.Visibility = Visibility.Visible;

            TabContentPresenter.Opacity = 0;
            var fadeInBrowser = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300));
            TabContentPresenter.BeginAnimation(OpacityProperty, fadeInBrowser);
        };

        if (LoadingScreen.Visibility == Visibility.Visible)
        {
            LoadingScreen.BeginAnimation(OpacityProperty, fadeOut);
        }
        else if (ErrorScreen.Visibility == Visibility.Visible)
        {
            ErrorScreen.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            LoadingScreen.Visibility = Visibility.Collapsed;
            ErrorScreen.Visibility = Visibility.Collapsed;
            TabContentPresenter.Visibility = Visibility.Visible;
            TabContentPresenter.Opacity = 0;
            var fadeInBrowser = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300));
            TabContentPresenter.BeginAnimation(OpacityProperty, fadeInBrowser);
        }
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRetrying) return;
        _isRetrying = true;

        try
        {
            if (_selectedTab?.WebView?.CoreWebView2 != null)
            {
                ShowLoading("Reconnecting...", 0.5);
                _selectedTab.WebView.Source = new Uri("https://web.whatsapp.com");
            }
            else
            {
                InitializeFirstTabAsync();
            }
        }
        finally
        {
            _isRetrying = false;
        }
    }

    private void DownloadRuntimeButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
            UseShellExecute = true
        });
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        _ = AddTabAsync();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        foreach (var tab in _tabs)
        {
            tab.WebView.Dispose();
        }
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.T)
        {
            _ = AddTabAsync();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.W)
        {
            if (_selectedTab != null) CloseTab(_selectedTab);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Tab)
        {
            if (_tabs.Count > 1)
            {
                var currentIndex = _tabs.IndexOf(_selectedTab!);
                var nextIndex = (currentIndex + 1) % _tabs.Count;
                SelectTab(_tabs[nextIndex]);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _selectedTab?.WebView?.CoreWebView2?.Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.F12 || (e.Key == Key.I && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            e.Handled = true;
        }
    }

    private enum ErrorType
    {
        Network,
        Runtime,
        Permission,
        FileSystem,
        Configuration,
        Crash,
        Unknown
    }

    private class TabInfo
    {
        public WebView2 WebView { get; set; } = null!;
        public Button? TabButton { get; set; }
        public string TabName { get; set; } = "";
    }
}
