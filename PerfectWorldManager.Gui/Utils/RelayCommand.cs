using System;
using System.Windows.Input;

// Adjust this namespace if you place it in a different subfolder like Utils
namespace PerfectWorldManager.Gui.Utils
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            // If _canExecute is null, the command can always execute.
            // Otherwise, evaluate the predicate.
            return _canExecute == null || _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            // Delegate the event subscription to the CommandManager.RequerySuggested event.
            // This ensures that WPF controls that are bound to this command are
            // automatically updated when the CommandManager believes conditions might have changed.
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        // Optional: A method to manually trigger a re-evaluation of CanExecute.
        // This can be useful in scenarios where CommandManager.RequerySuggested might not
        // pick up a change immediately (e.g., a property change in the ViewModel that
        // isn't directly tied to UI focus changes).
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}