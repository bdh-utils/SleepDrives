using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SleepDrives
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml. A thin UI shell over
    /// <see cref="DriveScheduleController"/>: it owns the tray icon and the
    /// poll timer, edits the schedule and disk selection, and reflects the
    /// controller's decisions back into the UI.
    /// </summary>
    public partial class MainWindow : Window
    {
        // How often the schedule is re-evaluated. Fine-grained enough that a
        // window boundary takes effect within half a minute.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        // Default window offered when the user adds one: a typical 22:00–07:00
        // overnight, every day.
        private const int DefaultStartMinutes = 22 * 60;
        private const int DefaultEndMinutes = 7 * 60;

        private readonly IDiskService _diskService = new WmiDiskService();
        private readonly DriveScheduleController _controller;
        private readonly ISettingsStore _settingsStore = new JsonSettingsStore();
        private readonly IStartupRegistration _startup =
            new ScheduledTaskStartup(Environment.ProcessPath ?? string.Empty);
        private readonly DispatcherTimer _timer;

        private readonly ObservableCollection<DiskRowViewModel> _driveRows = new();
        private readonly ObservableCollection<ScheduleWindowViewModel> _windowRows = new();

        private Forms.NotifyIcon? _trayIcon;
        private Drawing.Icon? _trayIconActive;
        private Drawing.Icon? _trayIconIdle;

        private bool _initialized;
        private bool _reallyExiting;
        private bool _trayTipShown;

        // Disks we've already warned the user are in use, so the tray balloon
        // fires once per drive rather than every poll.
        private HashSet<string> _warnedBusy = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();

            _controller = new DriveScheduleController(_diskService);
            _timer = new DispatcherTimer { Interval = PollInterval };
            _timer.Tick += (_, _) => Poll();
        }

        /// <summary>
        /// One-time setup driven by <see cref="App"/> at startup. Done here
        /// rather than on Loaded so it still runs when the app starts minimised
        /// and the window is never shown.
        /// </summary>
        public void Initialize()
        {
            LoadIcons();
            SetupTrayIcon();

            DrivesList.ItemsSource = _driveRows;
            WindowsList.ItemsSource = _windowRows;

            // Load saved configuration into the controller.
            var settings = _settingsStore.Load();
            _controller.Enabled = settings.Enabled;
            foreach (var id in settings.ManagedDiskIds)
            {
                _controller.ManagedDiskIds.Add(id);
            }
            foreach (var window in settings.Windows)
            {
                _controller.Windows.Add(window);
                AddWindowRow(window);
            }

            // Reflect into the UI. Handlers are gated on _initialized so these
            // assignments don't trigger saves.
            EnforceCheck.IsChecked = settings.Enabled;
            StartMinimisedCheck.IsChecked = settings.StartMinimised;
            StartupCheck.IsChecked = _startup.IsEnabled();

            RefreshDrives();
            UpdateWindowsHint();
            _initialized = true;

            // Re-evaluate when the machine resumes, since disks can come back
            // online by themselves after sleep/hibernate.
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            _timer.Start();
            Poll();

            if (settings.StartMinimised)
            {
                ShowInTaskbar = false;
            }
            else
            {
                Show();
            }
        }

        // ---- Icon / tray setup --------------------------------------------

        private void LoadIcons()
        {
            var uri = new Uri("pack://application:,,,/Resources/SleepDrives.ico", UriKind.Absolute);
            Icon = BitmapFrame.Create(uri);

            _trayIconActive = TrayIconRenderer.Create(active: true);
            _trayIconIdle = TrayIconRenderer.Create(active: false);
        }

        private void SetupTrayIcon()
        {
            var menu = new Forms.ContextMenuStrip();

            var openItem = new Forms.ToolStripMenuItem("Open SleepDrives");
            openItem.Font = new Drawing.Font(openItem.Font, Drawing.FontStyle.Bold);
            openItem.Click += (_, _) => ShowWindow();
            menu.Items.Add(openItem);

            var toggleItem = new Forms.ToolStripMenuItem("Enforce schedule");
            toggleItem.Click += (_, _) => SetEnforced(!_controller.Enabled);
            menu.Items.Add(toggleItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var aboutItem = new Forms.ToolStripMenuItem("About");
            aboutItem.Click += (_, _) => ShowAbout();
            menu.Items.Add(aboutItem);

            var exitItem = new Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => ExitApplication();
            menu.Items.Add(exitItem);

            menu.Opening += (_, _) =>
                toggleItem.Text = _controller.Enabled ? "Stop enforcing" : "Enforce schedule";

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = _trayIconIdle,
                Visible = true,
                Text = "SleepDrives",
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (_, _) => ShowWindow();
        }

        // ---- Scheduling engine --------------------------------------------

        /// <summary>One poll cycle: refresh hardware, then enforce the schedule.</summary>
        private void Poll()
        {
            RefreshDrives();
            ApplyNow();
        }

        /// <summary>Apply the schedule and reflect the resulting state in the UI.</summary>
        private void ApplyNow()
        {
            var result = _controller.Apply(DateTime.Now);
            UpdateDriveStates();
            ApplyBusyStates(result.BusyDiskIds);
            UpdateStatusUi(result);
            WarnIfNewlyBusy(result.BusyDiskIds);
        }

        private void ApplyBusyStates(IReadOnlyList<string> busyDiskIds)
        {
            var busy = new HashSet<string>(busyDiskIds, StringComparer.OrdinalIgnoreCase);
            foreach (var row in _driveRows)
            {
                row.SetBusy(busy.Contains(row.DiskId));
            }
        }

        /// <summary>
        /// Show a one-shot tray warning for any drive that has just become busy,
        /// so the user knows it was deliberately left online rather than cut.
        /// </summary>
        private void WarnIfNewlyBusy(IReadOnlyList<string> busyDiskIds)
        {
            var current = new HashSet<string>(busyDiskIds, StringComparer.OrdinalIgnoreCase);
            var newlyBusy = current.Where(id => !_warnedBusy.Contains(id)).ToList();

            if (newlyBusy.Count > 0 && _trayIcon != null)
            {
                var names = string.Join(", ", newlyBusy.Select(NameForDiskId));
                _trayIcon.ShowBalloonTip(4000, "SleepDrives",
                    $"Still in use, so left enabled for now: {names}. SleepDrives will retry.",
                    Forms.ToolTipIcon.Warning);
            }

            // Reset to the current set so a drive that frees up and later goes
            // busy again will warn again.
            _warnedBusy = current;
        }

        private string NameForDiskId(string diskId)
        {
            var row = _driveRows.FirstOrDefault(r => string.Equals(r.DiskId, diskId, StringComparison.OrdinalIgnoreCase));
            return row?.Title ?? diskId;
        }

        private void SetEnforced(bool enabled)
        {
            EnforceCheck.IsChecked = enabled; // routes through EnforceCheck_Changed
        }

        // ---- Drive list ---------------------------------------------------

        private void RefreshDrives()
        {
            var disks = _diskService.ListDisks();

            var liveIds = disks.Select(d => d.DiskId).OrderBy(x => x).ToList();
            var shownIds = _driveRows.Select(r => r.DiskId).OrderBy(x => x).ToList();

            if (!liveIds.SequenceEqual(shownIds))
            {
                BuildDriveRows(disks);
            }
            else
            {
                ApplyLiveStates(disks);
            }

            NoDrivesHint.Visibility = _driveRows.Any(r => r.IsManageable)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BuildDriveRows(IReadOnlyList<DiskInfo> disks)
        {
            foreach (var row in _driveRows)
            {
                row.SelectionChanged -= DriveRow_SelectionChanged;
            }
            _driveRows.Clear();

            foreach (var disk in disks)
            {
                var row = new DiskRowViewModel(disk);
                row.SetSelectedSilently(_controller.ManagedDiskIds.Contains(disk.DiskId));
                row.SelectionChanged += DriveRow_SelectionChanged;
                _driveRows.Add(row);
            }
        }

        private void UpdateDriveStates() => ApplyLiveStates(_diskService.ListDisks());

        private void ApplyLiveStates(IReadOnlyList<DiskInfo> disks)
        {
            var byId = disks.ToDictionary(d => d.DiskId, d => d);
            foreach (var row in _driveRows)
            {
                if (byId.TryGetValue(row.DiskId, out var info))
                {
                    row.UpdateLiveState(info);
                }
            }
        }

        private void DriveRow_SelectionChanged(object? sender, EventArgs e)
        {
            if (!_initialized || sender is not DiskRowViewModel row) return;

            if (row.IsSelected)
            {
                _controller.ManagedDiskIds.Add(row.DiskId);
            }
            else
            {
                _controller.ManagedDiskIds.Remove(row.DiskId);
            }

            SaveSettings();
            ApplyNow();
        }

        // ---- Schedule windows ---------------------------------------------

        private void AddWindowRow(ScheduleWindow model)
        {
            var vm = new ScheduleWindowViewModel(model);
            vm.Changed += Window_Changed;
            _windowRows.Add(vm);
        }

        private void Window_Changed(object? sender, EventArgs e)
        {
            if (!_initialized) return;
            SaveSettings();
            ApplyNow();
        }

        private void AddWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var model = new ScheduleWindow
            {
                Days = WeekDays.All,
                StartMinutes = DefaultStartMinutes,
                EndMinutes = DefaultEndMinutes
            };
            _controller.Windows.Add(model);
            AddWindowRow(model);

            UpdateWindowsHint();
            SaveSettings();
            ApplyNow();
        }

        private void RemoveWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: ScheduleWindowViewModel vm }) return;

            vm.Changed -= Window_Changed;
            _controller.Windows.Remove(vm.Model);
            _windowRows.Remove(vm);

            UpdateWindowsHint();
            SaveSettings();
            ApplyNow();
        }

        private void UpdateWindowsHint() =>
            NoWindowsHint.Visibility = _windowRows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        // ---- UI state -----------------------------------------------------

        private void UpdateStatusUi(ApplyResult result)
        {
            bool drivesDisabled = _controller.Enabled && result.ShouldDisable;

            if (!_controller.Enabled)
            {
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("BrandMuted");
                StatusText.Text = "Schedule is off — your drives are left untouched.";
            }
            else if (result.ShouldDisable)
            {
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("BrandAccent");
                StatusText.Text = result.HasBusyDisks
                    ? $"Active window — waiting for {result.BusyDiskIds.Count} drive(s) still in use."
                    : "Active window — managed drives are disabled.";
            }
            else
            {
                StatusDot.Fill = (System.Windows.Media.Brush)FindResource("BrandText");
                StatusText.Text = "Outside scheduled times — managed drives are enabled.";
            }

            if (_trayIcon != null)
            {
                _trayIcon.Icon = drivesDisabled ? _trayIconActive : _trayIconIdle;
                _trayIcon.Text = drivesDisabled
                    ? "SleepDrives — drives disabled"
                    : _controller.Enabled ? "SleepDrives — enforcing" : "SleepDrives — off";
            }
        }

        // ---- Window / tray interaction ------------------------------------

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            Topmost = true;   // bring to front…
            Topmost = false;  // …without staying pinned.
        }

        private void ExitApplication()
        {
            _reallyExiting = true;
            _timer.Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayIconActive?.Dispose();
            _trayIconIdle?.Dispose();

            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // The close button minimises to the tray instead of quitting, so the
            // schedule keeps being enforced in the background.
            if (!_reallyExiting)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;

                if (!_trayTipShown && _trayIcon != null)
                {
                    _trayIcon.ShowBalloonTip(2000, "SleepDrives",
                        "Still running in the tray. Right-click the icon to exit.",
                        Forms.ToolTipIcon.Info);
                    _trayTipShown = true;
                }
                return;
            }

            base.OnClosing(e);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Dispatcher.BeginInvoke(new Action(Poll));
            }
        }

        // ---- Control event handlers ---------------------------------------

        private void EnforceCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;

            _controller.Enabled = EnforceCheck.IsChecked == true;
            SaveSettings();
            ApplyNow();
        }

        private void StartupCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            _startup.SetEnabled(StartupCheck.IsChecked == true);
        }

        private void StartMinimisedCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            SaveSettings();
        }

        private void RefreshLink_Click(object sender, RoutedEventArgs e) => Poll();

        private void AboutLink_Click(object sender, RoutedEventArgs e) => ShowAbout();

        private void ShowAbout()
        {
            var about = new AboutWindow { Owner = IsVisible ? this : null };
            about.ShowDialog();
        }

        // ---- Persistence --------------------------------------------------

        private void SaveSettings()
        {
            if (!_initialized) return;

            _settingsStore.Save(new AppSettings
            {
                Enabled = _controller.Enabled,
                ManagedDiskIds = _controller.ManagedDiskIds.ToList(),
                Windows = _controller.Windows.ToList(),
                StartMinimised = StartMinimisedCheck.IsChecked == true
            });
        }
    }
}
