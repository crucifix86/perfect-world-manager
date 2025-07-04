using PerfectWorldManager.Core;
using System.Collections.ObjectModel;
using System.Linq;

namespace PerfectWorldManager.Core.Models
{
    public class CharacterRoleVm : ObservableObject
    {
        public CharacterSectionVm BaseInfo { get; set; }
        public CharacterSectionVm StatusInfo { get; set; }
        public CharacterSectionVm PocketInfo { get; set; } // Will contain variables like capacity, money, AND an Items collection for pocket items
        public CharacterSectionVm Equipment { get; set; } // Will primarily use its Items collection
        public CharacterSectionVm StorehouseInfo { get; set; } // Variables for capacity, money, AND Items collection for storehouse items
        public CharacterSectionVm TaskInfo { get; set; }

        // Add other sections as needed

        public CharacterRoleVm()
        {
            BaseInfo = new CharacterSectionVm("base");
            StatusInfo = new CharacterSectionVm("status");
            PocketInfo = new CharacterSectionVm("pocket");
            Equipment = new CharacterSectionVm("equipment"); // Note: In XML, equipment items are direct <inv> under <equipment>
            StorehouseInfo = new CharacterSectionVm("storehouse");
            TaskInfo = new CharacterSectionVm("task");
        }
    }
}