using PerfectWorldManager.Core;

namespace PerfectWorldManager.Core.Models
{
    public class CharacterVariableVm : ObservableObject
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _type;
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        // Helper for XAML to determine if a TextArea should be used
        public bool IsLongText => Value?.Length > 50;

        public CharacterVariableVm(string name, string type, string value)
        {
            Name = name;
            Type = type;
            Value = value;
        }
    }
}