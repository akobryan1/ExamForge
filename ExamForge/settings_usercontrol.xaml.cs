using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Shapes;
using ExamForge.Services;

namespace ExamForge
{
    public partial class settings_usercontrol : UserControl
    {
        private string _currentUsername = string.Empty;
        private string _currentEmail = string.Empty;
        private bool _isApplyingTheme;

        public settings_usercontrol()
        {
            InitializeComponent();
        }

        private async void Settings_UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateWindowSizes();
            SyncWindowSizeSelection();
            SyncThemeSelection();
            await LoadAccountInfoAsync();
        }

        private void PopulateWindowSizes()
        {
            WindowSizeComboBox.Items.Clear();
            WindowSizeComboBox.Items.Add(new ComboBoxItem { Content = "Small (1024 x 720)", Tag = "1024x720" });
            WindowSizeComboBox.Items.Add(new ComboBoxItem { Content = "Medium (1280 x 800)", Tag = "1280x800" });
            WindowSizeComboBox.Items.Add(new ComboBoxItem { Content = "Large (1440 x 900)", Tag = "1440x900" });
            WindowSizeComboBox.Items.Add(new ComboBoxItem { Content = "Full HD (1920 x 1080)", Tag = "1920x1080" });
            WindowSizeComboBox.Items.Add(new ComboBoxItem { Content = "Maximized", Tag = "MAX" });
        }

        private void SyncWindowSizeSelection()
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null || WindowSizeComboBox.Items.Count == 0) return;

            if (mainWindow.WindowState == WindowState.Maximized)
            {
                WindowSizeComboBox.SelectedIndex = WindowSizeComboBox.Items.Count - 1;
                return;
            }

            var current = $"{(int)Math.Round(mainWindow.Width)}x{(int)Math.Round(mainWindow.Height)}";
            for (var i = 0; i < WindowSizeComboBox.Items.Count; i++)
            {
                if (WindowSizeComboBox.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), current, StringComparison.Ordinal))
                {
                    WindowSizeComboBox.SelectedIndex = i;
                    return;
                }
            }

            WindowSizeComboBox.SelectedIndex = 1;
        }

        private void ApplyWindowSize_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;
            if (WindowSizeComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            var selectedTag = selectedItem.Tag?.ToString() ?? string.Empty;
            if (string.Equals(selectedTag, "MAX", StringComparison.Ordinal))
            {
                mainWindow.WindowState = WindowState.Maximized;
                return;
            }

            var parts = selectedTag.Split('x', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;

            if (double.TryParse(parts[0], out var width) && double.TryParse(parts[1], out var height))
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Width = width;
                mainWindow.Height = height;
                mainWindow.Left = Math.Max(0, (SystemParameters.WorkArea.Width - width) / 2);
                mainWindow.Top = Math.Max(0, (SystemParameters.WorkArea.Height - height) / 2);
            }
        }

        private void SyncThemeSelection()
        {
            var appBackground = Application.Current.Resources["AppBackgroundBrush"] as SolidColorBrush;
            var isDark = appBackground != null && appBackground.Color.R < 80;
            LightModeRadio.IsChecked = !isDark;
            DarkModeRadio.IsChecked = isDark;
        }

        private async void ApplyThemeChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_isApplyingTheme) return;

            var useDarkMode = DarkModeRadio.IsChecked == true;
            await ApplyThemeWithTransitionAsync(useDarkMode);
        }

        private async Task ApplyThemeWithTransitionAsync(bool isDarkMode)
        {
            _isApplyingTheme = true;
            try
            {
                var mainWindow = Window.GetWindow(this);
                var target = (DependencyObject?)mainWindow ?? this;

                var fadeOut = new DoubleAnimation(1.0, 0.68, TimeSpan.FromMilliseconds(160));
                if (target is UIElement targetElement)
                {
                    targetElement.BeginAnimation(OpacityProperty, fadeOut);
                }
                await Task.Delay(170);

                ApplyTheme(isDarkMode);
                RefreshWindowThemeVisuals();

                var fadeIn = new DoubleAnimation(0.68, 1.0, TimeSpan.FromMilliseconds(180));
                if (target is UIElement targetElementIn)
                {
                    targetElementIn.BeginAnimation(OpacityProperty, fadeIn);
                }
                await Task.Delay(190);
            }
            finally
            {
                _isApplyingTheme = false;
            }
        }

        private void RefreshWindowThemeVisuals()
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;

            if (mainWindow.FindName("MainContentHost") is ContentControl contentHost)
            {
                var currentContent = contentHost.Content;
                if (currentContent != null)
                {
                    contentHost.Content = CreateRefreshedView(currentContent);
                }
            }

            if (mainWindow.Content is Grid rootGrid)
            {
                var oldSidebar = rootGrid.Children.OfType<sidebar_usercontrol>().FirstOrDefault();
                if (oldSidebar != null)
                {
                    var sidebarIndex = rootGrid.Children.IndexOf(oldSidebar);
                    var sidebarColumn = Grid.GetColumn(oldSidebar);
                    var sidebarRow = Grid.GetRow(oldSidebar);

                    rootGrid.Children.Remove(oldSidebar);

                    var refreshedSidebar = new sidebar_usercontrol();
                    Grid.SetColumn(refreshedSidebar, sidebarColumn);
                    Grid.SetRow(refreshedSidebar, sidebarRow);

                    if (sidebarIndex >= 0 && sidebarIndex <= rootGrid.Children.Count)
                    {
                        rootGrid.Children.Insert(sidebarIndex, refreshedSidebar);
                    }
                    else
                    {
                        rootGrid.Children.Add(refreshedSidebar);
                    }
                }
            }
        }

        private static object CreateRefreshedView(object currentContent)
        {
            if (currentContent is settings_usercontrol)
            {
                return new settings_usercontrol();
            }

            var contentType = currentContent.GetType();
            var parameterlessCtor = contentType.GetConstructor(Type.EmptyTypes);
            if (parameterlessCtor != null)
            {
                var recreated = Activator.CreateInstance(contentType);
                if (recreated != null) return recreated;
            }

            return currentContent;
        }

        private static void SetBrushColor(string key, Color color)
        {
            var resources = Application.Current.Resources;
            if (resources[key] is SolidColorBrush brush)
            {
                if (!brush.IsFrozen)
                {
                    brush.Color = color;
                    return;
                }

                var clone = brush.Clone();
                clone.Color = color;
                resources[key] = clone;
                return;
            }

            resources[key] = new SolidColorBrush(color);
        }

        private void ApplyTheme(bool isDarkMode)
        {
            var themeFile = isDarkMode ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count == 0)
            {
                merged.Add(new ResourceDictionary { Source = new Uri(themeFile, UriKind.Relative) });
            }
            else
            {
                merged[0].Source = new Uri(themeFile, UriKind.Relative);
            }

            if (!isDarkMode)
            {
                SetBrushColor("BackgroundDark", (Color)ColorConverter.ConvertFromString("#F3F5F7"));
                SetBrushColor("SurfaceDark", (Color)ColorConverter.ConvertFromString("#FFFFFF"));
                SetBrushColor("SurfaceLight", (Color)ColorConverter.ConvertFromString("#F8FAFB"));
                SetBrushColor("TextPrimary", (Color)ColorConverter.ConvertFromString("#2E2F33"));
                SetBrushColor("TextSecondary", (Color)ColorConverter.ConvertFromString("#6B7280"));
                SetBrushColor("TextMuted", (Color)ColorConverter.ConvertFromString("#9AA3AF"));
                SetBrushColor("AccentGold", (Color)ColorConverter.ConvertFromString("#3AAE9E"));
                SetBrushColor("AccentBlue", (Color)ColorConverter.ConvertFromString("#3AAE9E"));
                SetBrushColor("AccentGoldGradient", (Color)ColorConverter.ConvertFromString("#3AAE9E"));
                return;
            }

            // Keep legacy color keys in sync for views still bound to old resource names
            SetBrushColor("BackgroundDark", (Color)ColorConverter.ConvertFromString("#0B1522"));
            SetBrushColor("SurfaceDark", (Color)ColorConverter.ConvertFromString("#18293E"));
            SetBrushColor("SurfaceLight", (Color)ColorConverter.ConvertFromString("#1D314A"));
            SetBrushColor("TextPrimary", (Color)ColorConverter.ConvertFromString("#EAF4FF"));
            SetBrushColor("TextSecondary", (Color)ColorConverter.ConvertFromString("#BDD2E7"));
            SetBrushColor("TextMuted", (Color)ColorConverter.ConvertFromString("#8EA8C1"));
            SetBrushColor("AccentGold", (Color)ColorConverter.ConvertFromString("#52C9FF"));
            SetBrushColor("AccentBlue", (Color)ColorConverter.ConvertFromString("#52C9FF"));
            SetBrushColor("AccentGoldGradient", (Color)ColorConverter.ConvertFromString("#52C9FF"));
        }

        private async Task LoadAccountInfoAsync()
        {
            var auth = App.SupabaseAuth;
            if (auth == null)
            {
                UsernameValueText.Text = "Not signed in";
                EmailValueText.Text = "N/A";
                UserIdValueText.Text = "N/A";
                return;
            }

            _currentEmail = auth.Email ?? string.Empty;
            var userId = auth.UserId ?? string.Empty;

            _currentUsername = auth.CurrentUser?.UserMetadata?.TryGetValue("username", out var usernameObj) == true
                ? usernameObj?.ToString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(_currentUsername) && !string.IsNullOrWhiteSpace(userId))
            {
                try
                {
                    var lookupService = new SupabaseAuthService();
                    await lookupService.InitializeAsync();
                    _currentUsername = await lookupService.GetUsernameByUserIdAsync(userId);
                }
                catch
                {
                    _currentUsername = string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(_currentUsername) && !string.IsNullOrWhiteSpace(_currentEmail))
            {
                _currentUsername = _currentEmail.Split('@')[0];
            }

            UsernameValueText.Text = string.IsNullOrWhiteSpace(_currentUsername) ? "Unknown" : _currentUsername;
            EmailValueText.Text = string.IsNullOrWhiteSpace(_currentEmail) ? "Unknown" : _currentEmail;
            UserIdValueText.Text = string.IsNullOrWhiteSpace(userId) ? "Unknown" : userId;
        }

        private async void SendRecoveryEmail_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentUsername) || string.IsNullOrWhiteSpace(_currentEmail))
            {
                MessageBox.Show("Account details are incomplete. Refresh account info first.", "Recovery", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var auth = App.SupabaseAuth;
                if (auth == null)
                {
                    MessageBox.Show("Authentication service is not available.", "Recovery", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (success, message) = await auth.ResetPasswordByEmailAsync(_currentEmail);
                MessageBox.Show(message, success ? "Recovery" : "Recovery Failed", MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send recovery email: {ex.Message}", "Recovery", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshAccount_Click(object sender, RoutedEventArgs e)
        {
            await LoadAccountInfoAsync();
            MessageBox.Show("Account details refreshed.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
