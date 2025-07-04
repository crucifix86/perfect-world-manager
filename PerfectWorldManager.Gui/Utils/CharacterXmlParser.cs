using PerfectWorldManager.Core;
using PerfectWorldManager.Core.Models;
using PerfectWorldManager.Core.Services; // For IItemLookupService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PerfectWorldManager.Gui.Utils
{
    public static class CharacterXmlParser
    {
        public static CharacterRoleVm Parse(string xmlData, Settings settings, IItemLookupService itemLookupService)
        {
            if (string.IsNullOrWhiteSpace(xmlData)) return null;

            // Fix for "data at the root level is invalid" error by trimming leading non-XML text.
            // This can happen in some game client versions where the XML data is not clean.
            int xmlStartIndex = xmlData.IndexOf("<role");
            if (xmlStartIndex > 0)
            {
                xmlData = xmlData.Substring(xmlStartIndex);
            }

            var roleVm = new CharacterRoleVm();
            XDocument doc = XDocument.Parse(xmlData);
            XElement roleElement = doc.Root;

            if (roleElement == null || roleElement.Name != "role") return null;

            ParseSection(roleElement.Element("base"), roleVm.BaseInfo, settings, itemLookupService);
            ParseSection(roleElement.Element("status"), roleVm.StatusInfo, settings, itemLookupService);
            ParseSection(roleElement.Element("pocket"), roleVm.PocketInfo, settings, itemLookupService);
            ParseEquipment(roleElement.Element("equipment"), roleVm.Equipment, settings, itemLookupService);
            ParseSection(roleElement.Element("storehouse"), roleVm.StorehouseInfo, settings, itemLookupService);
            ParseSection(roleElement.Element("task"), roleVm.TaskInfo, settings, itemLookupService);

            // Load display data for all collected items
            roleVm.PocketInfo.Items.ToList().ForEach(item => item.LoadDisplayData(settings, itemLookupService));
            roleVm.Equipment.Items.ToList().ForEach(item => item.LoadDisplayData(settings, itemLookupService));
            roleVm.StorehouseInfo.Items.ToList().ForEach(item => item.LoadDisplayData(settings, itemLookupService));

            return roleVm;
        }

        private static void ParseSection(XElement sectionElement, CharacterSectionVm sectionVm, Settings settings, IItemLookupService itemLookupService)
        {
            if (sectionElement == null || sectionVm == null) return;

            // Parse direct <variable> children of the section (e.g., capacity, money, timestamp for <pocket>)
            foreach (XElement varElement in sectionElement.Elements("variable"))
            {
                sectionVm.Variables.Add(new CharacterVariableVm(
                    varElement.Attribute("name")?.Value,
                    varElement.Attribute("type")?.Value,
                    varElement.Value
                ));
            }

            // Parse item data if this section is "pocket" or "storehouse"
            // In the new XML, each actual inventory item is an <items> tag directly under <pocket> or <storehouse>
            if (sectionElement.Name == "pocket" || sectionElement.Name == "storehouse")
            {
                // Check for both "items" and "inv" tags as different servers might use different formats
                var itemNodes = sectionElement.Elements("items").Concat(sectionElement.Elements("inv")).ToList();
                System.Diagnostics.Debug.WriteLine($"Found {itemNodes.Count} items in {sectionElement.Name}");
                
                foreach (XElement itemNode in itemNodes) // Each <items> or <inv> tag is an individual item
                {
                    var invItemVm = new InventoryItemVm();
                    // The variables of the item are direct children of this <items> node
                    foreach (XElement itemVarElement in itemNode.Elements("variable"))
                    {
                        invItemVm.Variables.Add(new CharacterVariableVm(
                            itemVarElement.Attribute("name")?.Value,
                            itemVarElement.Attribute("type")?.Value,
                            itemVarElement.Value
                        ));
                    }
                    invItemVm.UpdateItemId(); // Crucial to set ItemId from parsed variables
                    sectionVm.Items.Add(invItemVm);
                }
            }
            // Note: If other sections (like "base" or "status") could also have item lists
            // with a different structure, further conditions or separate parsing methods might be needed.
            // For now, this specifically targets the <items> structure under <pocket> and <storehouse>.
        }

        private static void ParseEquipment(XElement equipmentElement, CharacterSectionVm equipmentVm, Settings settings, IItemLookupService itemLookupService)
        {
            if (equipmentElement == null || equipmentVm == null) return;

            // Equipment items are direct <inv> children
            var invElements = equipmentElement.Elements("inv").ToList();
            System.Diagnostics.Debug.WriteLine($"Found {invElements.Count} equipment items");
            
            foreach (XElement invElement in invElements)
            {
                var invItemVm = new InventoryItemVm();
                foreach (XElement varElement in invElement.Elements("variable"))
                {
                    invItemVm.Variables.Add(new CharacterVariableVm(
                        varElement.Attribute("name")?.Value,
                        varElement.Attribute("type")?.Value,
                        varElement.Value
                    ));
                }
                invItemVm.UpdateItemId();
                equipmentVm.Items.Add(invItemVm);
            }
        }

        public static string Serialize(CharacterRoleVm roleVm)
        {
            if (roleVm == null) return string.Empty;

            XElement roleElement = new XElement("role",
                SerializeSection(roleVm.BaseInfo, "base"),
                SerializeSection(roleVm.StatusInfo, "status"),
                SerializeSectionWithItems(roleVm.PocketInfo, "pocket"), // Handles pocket items as <items>
                SerializeEquipment(roleVm.Equipment, "equipment"),     // Handles equipment items as <inv>
                SerializeSectionWithItems(roleVm.StorehouseInfo, "storehouse"), // Handles storehouse items as <items>
                SerializeSection(roleVm.TaskInfo, "task")
            );

            var variablesToRemove = roleElement.Descendants("variable")
                .Where(el => {
                    string name = el.Attribute("name")?.Value;
                    if (name == null) return false;
                    return name.StartsWith("reserved") &&
                           int.TryParse(name.Substring("reserved".Length), out int num) &&
                           num >= 1 && num <= 10;
                }).ToList();
            variablesToRemove.ForEach(v => v.Remove());

            return roleElement.ToString(SaveOptions.None);
        }

        private static XElement SerializeSection(CharacterSectionVm sectionVm, string sectionName)
        {
            if (sectionVm == null) return new XElement(sectionName);

            var sectionElement = new XElement(sectionName);
            // Add direct variables of the section
            foreach (var v in sectionVm.Variables)
            {
                sectionElement.Add(new XElement("variable",
                                    new XAttribute("name", v.Name ?? ""),
                                    new XAttribute("type", v.Type ?? ""),
                                    v.Value ?? ""));
            }
            return sectionElement;
        }

        private static XElement SerializeEquipment(CharacterSectionVm equipmentVm, string sectionName)
        {
            if (equipmentVm == null) return new XElement(sectionName);
            var sectionElement = new XElement(sectionName,
                equipmentVm.Items.Select(item =>
                    new XElement("inv", // Equipment items are directly <inv>
                        item.Variables.Select(v =>
                            new XElement("variable",
                                new XAttribute("name", v.Name ?? ""),
                                new XAttribute("type", v.Type ?? ""),
                                v.Value ?? ""
                            )
                        )
                    )
                )
            );
            // Add any direct variables for the equipment section itself if they exist (though unlikely for equipment)
            foreach (var v in equipmentVm.Variables)
            {
                sectionElement.Add(new XElement("variable",
                                   new XAttribute("name", v.Name ?? ""),
                                   new XAttribute("type", v.Type ?? ""),
                                   v.Value ?? ""));
            }
            return sectionElement;
        }

        private static XElement SerializeSectionWithItems(CharacterSectionVm sectionVm, string sectionName)
        {
            if (sectionVm == null) return new XElement(sectionName);

            var sectionElement = new XElement(sectionName);

            // Add main variables of the section (e.g. pocket capacity, money)
            foreach (var v in sectionVm.Variables)
            {
                sectionElement.Add(new XElement("variable",
                                    new XAttribute("name", v.Name ?? ""),
                                    new XAttribute("type", v.Type ?? ""),
                                    v.Value ?? ""));
            }

            // Add items. For "pocket" and "storehouse", each item VM in sectionVm.Items 
            // should become an <items> tag containing its respective variables.
            if ((sectionName == "pocket" || sectionName == "storehouse") && sectionVm.Items.Any())
            {
                foreach (var itemVm in sectionVm.Items)
                {
                    // Each itemVm corresponds to one <items> tag in the XML
                    var itemElement = new XElement("items");
                    foreach (var v in itemVm.Variables)
                    {
                        itemElement.Add(new XElement("variable",
                                        new XAttribute("name", v.Name ?? ""),
                                        new XAttribute("type", v.Type ?? ""),
                                        v.Value ?? ""));
                    }
                    sectionElement.Add(itemElement);
                }
            }
            return sectionElement;
        }
    }
}