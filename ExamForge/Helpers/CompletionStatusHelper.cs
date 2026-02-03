using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ExamForge.Helpers;

public class CompletionStatusHelper : INotifyPropertyChanged
{
    private bool _isComplete;

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            if (_isComplete != value)
            {
                _isComplete = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public string StatusText => IsComplete ? "✓ Complete" : "○ Incomplete";
    public string StatusColor => IsComplete ? "#27AE60" : "#E74C3C";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void UpdateStatus(bool hasRequiredData)
    {
        IsComplete = hasRequiredData;
    }
}