// PerfectWorldManager.Core/Models/CharacterSectionVm.cs
using PerfectWorldManager.Core;
using System.Collections.ObjectModel;
using System.Linq; // Required for FirstOrDefault
using System; // Required for StringComparison

namespace PerfectWorldManager.Core.Models
{
    public class CharacterSectionVm : ObservableObject
    {
        private string _sectionName;
        public string SectionName
        {
            get => _sectionName;
            set => SetProperty(ref _sectionName, value);
        }

        public ObservableCollection<CharacterVariableVm> Variables { get; } = new ObservableCollection<CharacterVariableVm>();
        public ObservableCollection<InventoryItemVm> Items { get; } = new ObservableCollection<InventoryItemVm>(); // For sections with items

        public CharacterSectionVm(string name)
        {
            SectionName = name;
        }

        // START --- ADDED CODE ---
        public string Money
        {
            get
            {
                var moneyVar = Variables.FirstOrDefault(v => v.Name.Equals("money", StringComparison.OrdinalIgnoreCase));
                return moneyVar?.Value;
            }
            set
            {
                var moneyVar = Variables.FirstOrDefault(v => v.Name.Equals("money", StringComparison.OrdinalIgnoreCase));
                if (moneyVar != null)
                {
                    if (moneyVar.Value != value)
                    {
                        moneyVar.Value = value; // This will trigger OnPropertyChanged in CharacterVariableVm
                        OnPropertyChanged(nameof(Money)); // Notify that Money property itself has changed (optional, but good practice)
                    }
                }
                else
                {
                    // If 'money' variable doesn't exist, create it.
                    // Assuming type "int" as commonly used for money. Adjust if "string" or other.
                    var newMoneyVar = new CharacterVariableVm("money", "int", value);
                    Variables.Add(newMoneyVar);
                    OnPropertyChanged(nameof(Money));
                }
            }
        }
        // END --- ADDED CODE ---
    }
}