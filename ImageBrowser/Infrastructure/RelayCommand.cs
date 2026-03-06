using System.Windows.Input;

namespace ImageBrowser.Infrastructure
{
    // A simple implementation of ICommand that takes an Action to execute.
    public class RelayCommand : ICommand
    {
        // The action to execute when the command is invoked
        private readonly Action<object?> _execute;

        // Constructor that takes the action to execute
        public RelayCommand(Action<object?> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        // This wires the command into WPF's automatic requery system
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        // Always executable in this simplified version
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}