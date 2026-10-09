using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

public sealed class UrlLibraryViewModel : INotifyPropertyChanged
{
    private UrlLibraryEntry? _selectedEntry;
    private string _editName = string.Empty;
    private string _editUrl = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised whenever <see cref="Entries"/> changes so the owner can persist it.</summary>
    public event EventHandler? EntriesChanged;

    /// <summary>Raised when a library row is activated (double-click) with its URL.</summary>
    public event EventHandler<string>? UrlActivated;

    public ObservableCollection<UrlLibraryEntry> Entries { get; } = [];

    public UrlLibraryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetField(ref _selectedEntry, value) && value is not null)
            {
                EditName = value.Name;
                EditUrl = value.Url;
            }
        }
    }

    public string EditName
    {
        get => _editName;
        set => SetField(ref _editName, value ?? string.Empty);
    }

    public string EditUrl
    {
        get => _editUrl;
        set => SetField(ref _editUrl, value ?? string.Empty);
    }

    public void Load(IEnumerable<UrlLibraryEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries) Entries.Add(entry);
    }

    public bool TryAdd()
    {
        if (!TryBuildEntry(out var entry)) return false;
        Entries.Add(entry);
        ClearEdit();
        RaiseEntriesChanged();
        return true;
    }

    public bool TrySaveEdit()
    {
        if (SelectedEntry is null || !TryBuildEntry(out var entry)) return false;
        var index = Entries.IndexOf(SelectedEntry);
        if (index < 0) return false;
        Entries[index] = entry;
        SelectedEntry = entry;
        RaiseEntriesChanged();
        return true;
    }

    public void Remove()
    {
        if (SelectedEntry is null) return;
        Entries.Remove(SelectedEntry);
        SelectedEntry = null;
        ClearEdit();
        RaiseEntriesChanged();
    }

    public void MoveUp()
    {
        if (SelectedEntry is null) return;
        var index = Entries.IndexOf(SelectedEntry);
        if (index <= 0) return;
        Entries.Move(index, index - 1);
        RaiseEntriesChanged();
    }

    public void MoveDown()
    {
        if (SelectedEntry is null) return;
        var index = Entries.IndexOf(SelectedEntry);
        if (index < 0 || index >= Entries.Count - 1) return;
        Entries.Move(index, index + 1);
        RaiseEntriesChanged();
    }

    public void ClearEdit()
    {
        SelectedEntry = null;
        EditName = string.Empty;
        EditUrl = string.Empty;
    }

    public void Activate(UrlLibraryEntry entry) => UrlActivated?.Invoke(this, entry.Url);

    public int ImportPlaylist(IEnumerable<UrlLibraryEntry> imported)
    {
        var existingUrls = new HashSet<string>(Entries.Select(entry => entry.Url), StringComparer.Ordinal);
        var added = 0;
        foreach (var entry in imported)
        {
            if (!existingUrls.Add(entry.Url)) continue;
            Entries.Add(entry);
            added++;
        }

        if (added > 0) RaiseEntriesChanged();
        return added;
    }

    private bool TryBuildEntry(out UrlLibraryEntry entry)
    {
        entry = default!;
        var name = EditName.Trim();
        var url = EditUrl.Trim();
        if (name.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out _)) return false;
        entry = new UrlLibraryEntry(name, url);
        return true;
    }

    private void RaiseEntriesChanged() => EntriesChanged?.Invoke(this, EventArgs.Empty);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
