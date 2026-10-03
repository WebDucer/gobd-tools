using System.Windows.Input;

namespace GoBd.Reader.Ui;

/// <summary>A command that runs an action, when the reader is in a state where it can.</summary>
/// <remarks>
/// A native menu item takes a command as well as a click handler, and shows the item disabled
/// while the command cannot run. The menus use commands, so that a test can choose an entry the
/// way the menu does, from the menu itself.
/// </remarks>
internal sealed class ActionCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            execute();
        }
    }

    /// <summary>Says that whether the command can run may have changed.</summary>
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
