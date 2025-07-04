namespace PerfectWorldManager.Core.Services
{
    public interface IItemLookupService
    {
        string GetItemName(int itemId, string itemTxtPath);
        string GetItemIconPath(int itemId, string itemIconsBasePath); // e.g., base path to "new_icons" folder
    }
}