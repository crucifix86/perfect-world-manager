using PerfectWorldManagerDaemon.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using PerfectWorldManager.Core; // Ensure this using directive is present

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.json for Kestrel, logging, and paths used directly by IConfiguration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables();

// Configure Kestrel based on appsettings.json
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var enableHttp2 = builder.Configuration.GetValue<bool>("Kestrel:EnableHttp2", true);
    var httpsPort = builder.Configuration.GetValue<int>("Kestrel:HttpsPort", 5001);
    var httpPort = builder.Configuration.GetValue<int>("Kestrel:HttpPort", 5000);

    if (enableHttp2)
    {
        serverOptions.ListenAnyIP(httpPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
        // Optional HTTPS listener
        // serverOptions.ListenAnyIP(httpsPort, listenOptions =>
        // {
        //     listenOptions.UseHttps(); // Requires certificate setup
        //     listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        // });
    }
    else
    {
        serverOptions.ListenAnyIP(httpPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
        });
    }
});

// Add services to the container.

// 1. Load Settings using SettingsManager
// This will load from pwm_settings.json, which is presumably updated by the GUI.
var settings = SettingsManager.LoadSettings();
if (settings == null)
{
    Console.WriteLine("[ERROR] Failed to load settings from pwm_settings.json. Using default/empty settings. Database functionality may be affected.");
    settings = new PerfectWorldManager.Core.Settings();
}
else
{
    Console.WriteLine($"Loaded MySqlHost from pwm_settings.json: {settings.MySqlHost}");
}

// 2. Register the loaded Settings instance as a singleton
builder.Services.AddSingleton(settings);

// 3. Add gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<ManagerServiceImpl>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();