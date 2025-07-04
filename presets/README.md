# Perfect World Manager Presets

Process configuration presets are stored as individual JSON files in the `presets` folder located in the project directory.

The presets folder is located alongside the application executable or in the project root directory.

## Sharing Presets

To share a preset with others:
1. Navigate to your presets folder (path shown above)
2. Copy the `.json` file for the preset you want to share
3. Share the file with other users
4. They can place it in their presets folder and it will appear in the application

## Creating Custom Presets

You can create presets directly in the application using the "Save As..." button, or manually create JSON files following this structure:

```json
{
  "Name": "My Custom Preset",
  "Description": "Description of what this preset is for",
  "CreatedDate": "2024-01-01T00:00:00",
  "LastModifiedDate": "2024-01-01T00:00:00",
  "IsReadOnly": false,
  "Configurations": [
    {
      "Type": "LogService",
      "DisplayName": "Log Service",
      "IsEnabled": true,
      "ExecutableDir": "logservice",
      "ExecutableName": "./logservice",
      "StartArguments": "logservice.conf",
      "StatusCheckPattern": "./logservice logservice.conf",
      "MapId": null
    }
    // ... more configurations
  ]
}
```

## Default Presets

The `15x.json` preset is the default configuration and is marked as read-only. It cannot be modified or deleted through the application to ensure users always have a working baseline configuration.

## Adding New Default Presets

To add new default presets that ship with the application:
1. Create a new `.json` file in the presets folder
2. Set `"IsReadOnly": true` to prevent users from accidentally modifying it
3. The preset will be automatically loaded when users start the application

## Process Types

Valid process types for configurations:
- `LogService`
- `UniqueNamed`
- `AuthDaemon`
- `GameDbd`
- `GameAntiCheatDaemon`
- `GameFactionDaemon`
- `GameDeliveryDaemon`
- `GameLinkDaemon`
- `GameServer`
- `PwAdmin`
- `AntiCrash`