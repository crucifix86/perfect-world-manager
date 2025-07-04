using System;
using System.Collections.Generic;

namespace PerfectWorldManager.Core
{
    public class ProcessConfigurationPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        public bool IsReadOnly { get; set; } = false; // For built-in presets like "15x"
        public List<ProcessConfiguration> Configurations { get; set; } = new List<ProcessConfiguration>();

        // Deep clone method to create a copy of the preset
        public ProcessConfigurationPreset Clone()
        {
            var clone = new ProcessConfigurationPreset
            {
                Name = this.Name,
                Description = this.Description,
                CreatedDate = this.CreatedDate,
                LastModifiedDate = this.LastModifiedDate,
                IsReadOnly = this.IsReadOnly,
                Configurations = new List<ProcessConfiguration>()
            };

            foreach (var config in this.Configurations)
            {
                clone.Configurations.Add(new ProcessConfiguration
                {
                    Type = config.Type,
                    DisplayName = config.DisplayName,
                    IsEnabled = config.IsEnabled,
                    ExecutableDir = config.ExecutableDir,
                    ExecutableName = config.ExecutableName,
                    StartArguments = config.StartArguments,
                    StatusCheckPattern = config.StatusCheckPattern,
                    MapId = config.MapId
                });
            }

            return clone;
        }
    }
}