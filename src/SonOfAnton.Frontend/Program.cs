using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SonOfAnton.Frontend;
using SonOfAnton.Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Backend API URL (development)
var backendUrl = builder.Configuration.GetValue<string>("BackendUrl") ?? "https://localhost:5002";

builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(backendUrl) 
});

// Application Services
builder.Services.AddScoped<WorkflowApiService>();
builder.Services.AddScoped<LogStreamService>();
builder.Services.AddSingleton<DiagramStateService>();

// Node Editor Services (JS Interop)
builder.Services.AddScoped<NodeEditorInteropService>();
builder.Services.AddScoped<SseClientService>();

await builder.Build().RunAsync();


