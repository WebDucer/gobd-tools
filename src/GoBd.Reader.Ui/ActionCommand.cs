using System.Windows.Input;

namespace GoBd.Reader.Ui;

/// <summary>A command that runs an action and can always run.</summary>
/// <remarks>
/// A native menu item takes a command as well as a click handler. The Help menu uses commands, so
/// that a test can choose an entry the way the menu does, from the menu itself.
/// </remarks>
internal sealed class ActionCommand(Action execute) : ICommand
{
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) => execute();
}
