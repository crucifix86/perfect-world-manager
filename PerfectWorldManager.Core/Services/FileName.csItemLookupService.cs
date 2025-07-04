using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PerfectWorldManager.Core.Services
{
    public class ItemLookupService : IItemLookupService
    {
        // Simple cache for item names to avoid re-reading the file constantly for the same ID
        private Dictionary<string, Dictionary<int, string>> _itemNamesCache = new Dictionary<string, Dictionary<int, string>>();
        private DateTime _itemTxtFileWriteTime = DateTime.MinValue;


        public string GetItemName(int itemId, string itemTxtPath)
        {
            if (string.IsNullOrEmpty(itemTxtPath) || !File.Exists(itemTxtPath))
            {
                return $"Item ID: {itemId} (item.txt not found or path not set)";
            }

            try
            {
                FileInfo fileInfo = new FileInfo(itemTxtPath);
                if (!_itemNamesCache.ContainsKey(itemTxtPath) || fileInfo.LastWriteTimeUtc > _itemTxtFileWriteTime)
                {
                    // Populate or refresh cache
                    var names = new Dictionary<int, string>();
                    string[] lines = File.ReadAllLines(itemTxtPath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Regex based on JSP logic: ID "Name"
                        // Example line from JSP context: if (line.matches("\\S+\\s\".*\"")) { String[] parts = line.split("\\s", 2); ... }
                        var match = Regex.Match(line.Trim(), @"^(\d+)\s+\""([^\""]+)\"""); // Match ID then "Name"
                        if (!match.Success)
                        {
                            // Try matching ID ItemName (without quotes, if that's a possibility)
                            match = Regex.Match(line.Trim(), @"^(\S+)\s+\""([^\""]+)\"""); // Original JSP style: any non-space ID then "Name"
                        }


                        if (match.Success && int.TryParse(match.Groups[1].Value, out int idFromFile))
                        {
                            names[idFromFile] = match.Groups[2].Value;
                        }
                    }
                    _itemNamesCache[itemTxtPath] = names;
                    _itemTxtFileWriteTime = fileInfo.LastWriteTimeUtc;
                }

                if (_itemNamesCache[itemTxtPath].TryGetValue(itemId, out string name))
                {
                    return name;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading item.txt ({itemTxtPath}): {ex.Message}");
                _itemNamesCache.Remove(itemTxtPath); // Clear cache on error to retry next time
                return $"Item ID: {itemId} (Error reading item.txt)";
            }

            return $"Item ID: {itemId} (Name not found)";
        }

        public string GetItemIconPath(int itemId, string itemIconsBasePath)
        {
            if (string.IsNullOrEmpty(itemIconsBasePath))
            {
                return null; // Or a path to a truly default question mark icon if you have one as an embedded resource
            }
            // The JSP uses "new_icons" subfolder
            // We assume itemIconsBasePath might be the root of many icon sets, or directly to "new_icons".
            // For flexibility, let's try a direct combination first, then with "new_icons".
            // This path structure should match what you have in `AppSettings.ItemIconsPath`.
            // If AppSettings.ItemIconsPath is "C:\PW\element\surfaces\iconlist_new", then it's already specific.

            string iconFileName = $"{itemId}.png";
            string directPath = Path.Combine(itemIconsBasePath, iconFileName);

            if (File.Exists(directPath))
            {
                return directPath;
            }

            // Fallback check for "new_icons" subfolder if base path doesn't already include it
            // This is a heuristic, ideally ItemIconsPath is precise.
            if (!itemIconsBasePath.EndsWith("new_icons", StringComparison.OrdinalIgnoreCase) && !itemIconsBasePath.EndsWith("new_icons/", StringComparison.OrdinalIgnoreCase))
            {
                string pathWithNewIcons = Path.Combine(itemIconsBasePath, "new_icons", iconFileName);
                if (File.Exists(pathWithNewIcons))
                {
                    return pathWithNewIcons;
                }
            }

            // Fallback to default icon "1.png" as per JSP logic
            string defaultIconPath = Path.Combine(itemIconsBasePath, "1.png");
            if (!itemIconsBasePath.EndsWith("new_icons", StringComparison.OrdinalIgnoreCase) && !itemIconsBasePath.EndsWith("new_icons/", StringComparison.OrdinalIgnoreCase) && !File.Exists(defaultIconPath))
            {
                defaultIconPath = Path.Combine(itemIconsBasePath, "new_icons", "1.png");
            }


            return File.Exists(defaultIconPath) ? defaultIconPath : null;
        }
    }
}