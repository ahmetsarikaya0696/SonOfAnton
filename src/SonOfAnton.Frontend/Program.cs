using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using SonOfAnton.Frontend;
using SonOfAnton.Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Backend API URL (development)
var backendUrl = builder.Configuration.GetValue<string>("BackendUrl") ?? "https://localhost:5002";

// ─── OIDC Authentication ────────────────────────────────────────
builder.Services.AddOidcAuthentication(options =>
{
    var oidcConfig = builder.Configuration.GetSection("Oidc");
    options.ProviderOptions.Authority = oidcConfig["Authority"]!;
    options.ProviderOptions.ClientId = oidcConfig["ClientId"]!;
    options.ProviderOptions.ResponseType = oidcConfig["ResponseType"] ?? "code";
    
    // Default scopes
    options.ProviderOptions.DefaultScopes.Clear();
    var scopes = oidcConfig.GetSection("DefaultScopes").Get<string[]>() ?? ["openid", "profile", "email"];
    foreach (var scope in scopes)
    {
        options.ProviderOptions.DefaultScopes.Add(scope);
    }

    // Keycloak post-logout redirect
    options.ProviderOptions.PostLogoutRedirectUri = "/";
    
    // Keycloak rol claim mapping
    options.UserOptions.RoleClaim = "roles";
    options.UserOptions.NameClaim = "preferred_username";
});

// ─── HttpClient with Auth Token ─────────────────────────────────
// Authenticated HttpClient — API çağrılarına otomatik Bearer token ekler
builder.Services.AddHttpClient("BackendApi",
    client => client.BaseAddress = new Uri(backendUrl))
    .AddHttpMessageHandler(sp =>
    {
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
            .ConfigureHandler(
                authorizedUrls: [backendUrl],
                scopes: ["openid", "profile", "email"]);
        return handler;
    });

// Default HttpClient (auth'suz — public endpoint'ler için)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(backendUrl)
});

// IHttpClientFactory'den kolayca erişim için
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("BackendApi"));

// Application Services
builder.Services.AddScoped<WorkflowApiService>();
builder.Services.AddScoped<LogStreamService>();
builder.Services.AddSingleton<DiagramStateService>();
builder.Services.AddSingleton<YamlSyncService>();
builder.Services.AddSingleton<ChatDiagramBridge>();

// Node Editor Services (JS Interop)
builder.Services.AddScoped<NodeEditorInteropService>();
builder.Services.AddScoped<SseClientService>();

await builder.Build().RunAsync();
