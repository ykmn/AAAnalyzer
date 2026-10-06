using System.Windows.Input;

namespace SystemAudioAnalyzer.App.ViewModels;

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute();

    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(false);

    public Task ExecuteAsync() => canExecute() ? execute() : Task.CompletedTask;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
