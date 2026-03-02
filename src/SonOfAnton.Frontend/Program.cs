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
    var scopes = oidcConfig.GetSection("DefaultScopes").Get<string[]>() ?? new[] { "openid", "profile", "email" };
    foreach (var scope in scopes)
    {
        options.ProviderOptions.DefaultScopes.Add(scope);
    }

    // Optionally request an API scope if configured (Oidc:ApiScope).
    var apiScope = oidcConfig["ApiScope"];
    if (!string.IsNullOrWhiteSpace(apiScope) && !options.ProviderOptions.DefaultScopes.Contains(apiScope))
    {
        options.ProviderOptions.DefaultScopes.Add(apiScope);
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
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
        
        // Configure handler to attach the token to all requests to the backend URL,
        // regardless of specific scopes, to prevent it from silently dropping the token.
        handler.ConfigureHandler(
            authorizedUrls: new[] { backendUrl });

        return handler;
    });

// ─── Typed HttpClients — tüm API servisleri authenticated "BackendApi" client kullanır ───
builder.Services.AddHttpClient<WorkflowApiService>("BackendApi");
builder.Services.AddHttpClient<NotificationApiService>("BackendApi");
builder.Services.AddHttpClient<QuotaApiService>("BackendApi");
builder.Services.AddHttpClient<ApiKeyService>("BackendApi");
builder.Services.AddHttpClient<CredentialService>("BackendApi");
builder.Services.AddHttpClient<LinkedAccountService>("BackendApi");
builder.Services.AddHttpClient<LogStreamService>("BackendApi");
builder.Services.AddHttpClient<DataTableClientService>("BackendApi");
builder.Services.AddHttpClient<DataFormClientService>("BackendApi");
builder.Services.AddHttpClient<ChatbotApiService>("BackendApi");
builder.Services.AddHttpClient<PlanningApiService>("BackendApi");
builder.Services.AddSingleton<DiagramStateService>();
builder.Services.AddSingleton<YamlSyncService>();
builder.Services.AddSingleton<ChatDiagramBridge>();

// Node Editor Services (JS Interop)
builder.Services.AddScoped<NodeEditorInteropService>();
builder.Services.AddScoped<SseClientService>();

await builder.Build().RunAsync();
