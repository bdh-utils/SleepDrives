using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SleepDrives
{
    /// <summary>Minimal <see cref="INotifyPropertyChanged"/> base for the view models.</summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// One row in the drive list: a physical disk the user can choose to manage.
    /// Boot/system disks are surfaced but locked (<see cref="IsManageable"/> is
    /// false) so they can never be scheduled offline.
    /// </summary>
    public sealed class DiskRowViewModel : ObservableObject
    {
        private bool _isSelected;
        private bool _isOffline;

        public DiskRowViewModel(DiskInfo info)
        {
            DiskId = info.DiskId;
            IsManageable = info.IsManageable;
            Title = $"{info.FriendlyName}";
            Detail = $"Disk {info.Number} · {info.SizeText} · {info.BusType}";
            _isOffline = info.IsOffline;
        }

        /// <summary>Raised when the user ticks or unticks this disk.</summary>
        public event EventHandler? SelectionChanged;

        public string DiskId { get; }
        public bool IsManageable { get; }
        public string Title { get; }
        public string Detail { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Update the live online/offline state without rebuilding the row.</summary>
        public void UpdateLiveState(DiskInfo info)
        {
            if (_isOffline == info.IsOffline) return;
            _isOffline = info.IsOffline;
            OnPropertyChanged(nameof(StateText));
        }

        /// <summary>Reflect the selection without raising <see cref="SelectionChanged"/>.</summary>
        public void SetSelectedSilently(bool selected)
        {
            if (_isSelected == selected) return;
            _isSelected = selected;
            OnPropertyChanged(nameof(IsSelected));
        }

        public string StateText => !IsManageable
            ? "Protected"
            : _isOffline ? "Disabled" : "Enabled";
    }

    /// <summary>
    /// Editable view of one <see cref="ScheduleWindow"/>: day toggles plus the
    /// start/end times. Edits flow straight into the wrapped model and raise
    /// <see cref="Changed"/> so the window can persist and re-apply.
    /// </summary>
    public sealed class ScheduleWindowViewModel : ObservableObject
    {
        private readonly ScheduleWindow _model;

        public ScheduleWindowViewModel(ScheduleWindow model)
        {
            _model = model;
        }

        /// <summary>Raised on any edit to the days or times.</summary>
        public event EventHandler? Changed;

        /// <summary>The underlying model, for rebuilding the controller's list.</summary>
        public ScheduleWindow Model => _model;

        public bool Monday { get => Has(WeekDays.Monday); set => SetDay(WeekDays.Monday, value); }
        public bool Tuesday { get => Has(WeekDays.Tuesday); set => SetDay(WeekDays.Tuesday, value); }
        public bool Wednesday { get => Has(WeekDays.Wednesday); set => SetDay(WeekDays.Wednesday, value); }
        public bool Thursday { get => Has(WeekDays.Thursday); set => SetDay(WeekDays.Thursday, value); }
        public bool Friday { get => Has(WeekDays.Friday); set => SetDay(WeekDays.Friday, value); }
        public bool Saturday { get => Has(WeekDays.Saturday); set => SetDay(WeekDays.Saturday, value); }
        public bool Sunday { get => Has(WeekDays.Sunday); set => SetDay(WeekDays.Sunday, value); }

        public string StartText
        {
            get => ScheduleWindow.FormatTime(_model.StartMinutes);
            set
            {
                var parsed = ScheduleWindow.ParseTime(value);
                if (parsed.HasValue && parsed.Value != _model.StartMinutes)
                {
                    _model.StartMinutes = parsed.Value;
                    Raise();
                }
                // Always re-publish so an invalid entry snaps back to the
                // last good value and the overnight note stays accurate.
                OnPropertyChanged();
                OnPropertyChanged(nameof(OvernightNote));
            }
        }

        public string EndText
        {
            get => ScheduleWindow.FormatTime(_model.EndMinutes);
            set
            {
                var parsed = ScheduleWindow.ParseTime(value);
                if (parsed.HasValue && parsed.Value != _model.EndMinutes)
                {
                    _model.EndMinutes = parsed.Value;
                    Raise();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(OvernightNote));
            }
        }

        public string OvernightNote =>
            _model.StartMinutes != _model.EndMinutes && _model.IsOvernight
                ? "Overnight — ends the following morning"
                : string.Empty;

        private bool Has(WeekDays day) => (_model.Days & day) != 0;

        private void SetDay(WeekDays day, bool on)
        {
            bool current = Has(day);
            if (current == on) return;

            if (on) _model.Days |= day;
            else _model.Days &= ~day;

            Raise();
        }

        private void Raise()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
