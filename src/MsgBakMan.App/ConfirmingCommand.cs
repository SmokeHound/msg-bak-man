using System;
using System.Windows;
using System.Windows.Input;

namespace MsgBakMan.App;

public sealed class ConfirmingCommand : ICommand
{
    private readonly ICommand _inner;
    private readonly Window _owner;
    private readonly string _title;
    private readonly string _message;

    public ConfirmingCommand(ICommand inner, Window owner, string title, string message)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _title = title ?? string.Empty;
        _message = message ?? string.Empty;

        _inner.CanExecuteChanged += Inner_CanExecuteChanged;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _inner.CanExecute(parameter);

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        var result = System.Windows.MessageBox.Show(_owner, _message, _title, System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.OK)
            return;

        _inner.Execute(parameter);
    }

    private void Inner_CanExecuteChanged(object? sender, EventArgs e)
    {
        CanExecuteChanged?.Invoke(this, e);
    }
}
