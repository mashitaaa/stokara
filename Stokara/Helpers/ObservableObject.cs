using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Stokara.Helpers;

/// <summary>Basis INotifyPropertyChanged untuk Model yang ditampilkan dan ViewModel.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// Basis ViewModel: notifikasi properti, status sibuk, dan validasi inline
/// melalui INotifyDataErrorInfo (pesan tampil di bawah field pada form).
/// </summary>
public abstract class ViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return _errors.SelectMany(e => e.Value).ToList();
        return _errors.TryGetValue(propertyName, out var list) ? list : Enumerable.Empty<string>();
    }

    protected void SetError(string propertyName, string message)
    {
        _errors[propertyName] = new List<string> { message };
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }

    protected void ClearError(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    protected void ClearAllErrors()
    {
        foreach (var key in _errors.Keys.ToList()) ClearError(key);
    }

    /// <summary>Menetapkan atau menghapus error sesuai kondisi; mengembalikan true bila valid.</summary>
    protected bool Validate(string propertyName, bool isValid, string message)
    {
        if (isValid) ClearError(propertyName);
        else SetError(propertyName, message);
        return isValid;
    }

    /// <summary>Dipanggil saat halaman dibuka.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    private System.Windows.Threading.DispatcherTimer _reloadTimer;
    private Func<Task> _reloadAction;

    /// <summary>
    /// Menjadwalkan muat ulang data sesaat setelah filter (mis. tanggal) berubah.
    /// Perubahan beruntun dalam 350 ms digabung menjadi satu kali muat ulang.
    /// </summary>
    protected void ScheduleReload(Func<Task> action)
    {
        _reloadAction = action;
        if (_reloadTimer == null)
        {
            _reloadTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _reloadTimer.Tick += async (_, _) =>
            {
                _reloadTimer.Stop();
                try { if (_reloadAction != null) await _reloadAction(); }
                catch (Exception ex) { Stokara.Infrastructure.ErrorHandler.Handle(ex); }
            };
        }
        _reloadTimer.Stop();
        _reloadTimer.Start();
    }
}
