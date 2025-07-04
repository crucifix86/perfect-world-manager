using PerfectWorldManager.Core;
using System.Collections.ObjectModel;
using System.IO; // For Path
using System;     // For Uri

// Removed: using System.Windows.Media;
// Removed: using System.Windows.Media.Imaging;

namespace PerfectWorldManager.Core.Models
{
    public class InventoryItemVm : ObservableObject
    {
        // Raw variables from XML for this item
        public ObservableCollection<CharacterVariableVm> Variables { get; } = new ObservableCollection<CharacterVariableVm>();

        private int _itemId;
        public int ItemId
        {
            get => _itemId;
            private set => SetProperty(ref _itemId, value); // Set privately after parsing
        }

        private string _itemName = "Unknown Item";
        public string ItemName
        {
            get => _itemName;
            set => SetProperty(ref _itemName, value);
        }

        // Changed ImageSource Icon to string IconPath
        private string _iconPath;
        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public InventoryItemVm() { }

        public void UpdateItemId()
        {
            foreach (var variable in Variables)
            {
                if (variable.Name == "id" && int.TryParse(variable.Value, out int id))
                {
                    ItemId = id;
                    break;
                }
            }
        }

        public void LoadDisplayData(Settings settings, Services.IItemLookupService itemLookupService)
        {
            if (ItemId > 0)
            {
                ItemName = itemLookupService.GetItemName(ItemId, settings.ItemTxtPath);
                IconPath = itemLookupService.GetItemIconPath(ItemId, settings.ItemIconsPath);
                // The actual loading of BitmapImage from IconPath will now happen in XAML via a converter.
                System.Diagnostics.Debug.WriteLine($"Item {ItemId}: Name='{ItemName}', IconPath='{IconPath}'");
            }
        }
    }
}