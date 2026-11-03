using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Services;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Infrastructure.Identity;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;
using Xunit;

namespace SonOfAnton.Tests;

// Requires an isolated PostgreSQL + pgvector database; never point at a real user's database.
public sealed class ApplicationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _alice = null!, _bob = null!, _viewer = null!, _workspaceViewer = null!, _stranger = null!;
    private const string Issuer = "https://issuer.invalid/realms/test";
    private static readonly SymmetricSecurityKey SigningKey = new(System.Text.Encoding.UTF8.GetBytes("integration-tests-only-signing-key-64-characters-never-production!!"));
    private Guid _workspace;
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("SONOFANTON_TEST_DB")
            ?? throw new InvalidOperationException("Set SONOFANTON_TEST_DB to an isolated PostgreSQL/pgvector database.");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connection);
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["Features:AI"] = "false",
                ["Authentication:DefaultAuthority"] = "https://issuer.invalid/realms/test",
                ["Authentication:Audience"] = "sonofanton-backend"
            }))
            .ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                options.Configuration.SigningKeys.Add(SigningKey);
                options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            })));
        _alice = _app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        _bob = _app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        _workspaceViewer = _app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        _stranger = _app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        _viewer = _app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        _workspace = await Seed(db, "a"+suffix, _alice, [Permissions.WorkflowsRead,Permissions.WorkflowsEdit,Permissions.WorkflowsExecute,Permissions.WorkspacesRead]);
        await Seed(db, "b"+suffix, _bob, [Permissions.WorkflowsRead,Permissions.WorkflowsEdit,Permissions.WorkflowsExecute]);
        await Seed(db, "v"+suffix, _viewer, [Permissions.WorkflowsRead]);
        await Seed(db, "a"+suffix, _workspaceViewer, [Permissions.WorkflowsRead, Permissions.WorkflowsEdit, Permissions.WorkflowsExecute, Permissions.WorkspacesRead], false, _workspace);
        await Seed(db, "a"+suffix, _stranger, [Permissions.WorkflowsRead, Permissions.WorkflowsEdit, Permissions.WorkflowsExecute, Permissions.WorkspacesRead], false);
    }
    private static async Task<Guid> Seed(SonOfAntonDbContext db, string tenant, HttpClient client, string[] permissions, bool createTenant = true, Guid? viewerWorkspace = null)
    {
        if (createTenant) db.Tenants.Add(new TenantEntity { Id=tenant,Name=tenant,IsActive=true });
        var user = new UserEntity { ExternalIdentityId=Guid.NewGuid().ToString(),Email=Guid.NewGuid()+"@example.test",TenantId=tenant,IsActive=true };
        db.Users.Add(user);
        var workspace = new WorkspaceEntity { Name="Tests-"+user.Id,TenantId=tenant,OwnerUserId=user.Id };
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new WorkspaceMemberEntity { WorkspaceId=workspace.Id,UserId=user.Id,Role=WorkspaceMemberRole.Owner });
        if (viewerWorkspace.HasValue) db.WorkspaceMembers.Add(new WorkspaceMemberEntity { WorkspaceId=viewerWorkspace.Value, UserId=user.Id, Role=WorkspaceMemberRole.Viewer });
        var key = Guid.NewGuid().ToString("N");
        db.ApiKeys.Add(new ApiKeyEntity { Name="test",KeyPrefix="test",KeyHash=ApiKeyAuthenticationHandler.HashApiKey(key),
            TenantId=tenant,CreatedByUserId=user.Id,IsActive=true,Scopes=JsonSerializer.SerializeToDocument(permissions),RateLimitPerMinute=1000 });
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Add("X-Api-Key",key);
        return workspace.Id;
    }
    public Task DisposeAsync() { _alice.Dispose(); _bob.Dispose(); _viewer.Dispose(); _workspaceViewer.Dispose(); _stranger.Dispose(); _app.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Core_workflow_persists_runs_and_is_isolated()
    {
        var trigger = Guid.NewGuid(); var set=Guid.NewGuid(); var debug=Guid.NewGuid();
        var graph=new WorkflowGraphDto { Nodes=[
            new(){Id=trigger,Type="trigger.manual",Name="Start"},
            new(){Id=set,Type="transform.set",Name="Set",Parameters=new(){["values"]="{\"message\":\"hello\"}"}},
            new(){Id=debug,Type="debug",Name="Debug"}],
            Connections=[new(){SourceNodeId=trigger,TargetNodeId=set},new(){SourceNodeId=set,TargetNodeId=debug}] };
        var create=await _alice.PostAsJsonAsync("/api/workflows",new SaveWorkflowRequest { Name="Integration workflow",WorkspaceId=_workspace,Graph=graph });
        var body=await create.Content.ReadAsStringAsync(); Assert.True(create.IsSuccessStatusCode,body);
        var saved=JsonSerializer.Deserialize<WorkflowDto>(body,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(HttpStatusCode.NotFound,(await _bob.GetAsync($"/api/workflows/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await _bob.PostAsJsonAsync($"/api/workflows/{saved.Id}/execute",new {})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await _viewer.PostAsJsonAsync("/api/workflows",new SaveWorkflowRequest{Name="Denied"})).StatusCode);
        var execute=await _alice.PostAsJsonAsync($"/api/workflows/{saved.Id}/execute",new StartExecutionRequest{InitialData=new(){["input"]="hello"}});
        Assert.True(execute.IsSuccessStatusCode,await execute.Content.ReadAsStringAsync());
        var run=await execute.Content.ReadFromJsonAsync<ExecutionDto>();
        ExecutionDto? status=null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        do
        {
            await Task.Delay(100,timeout.Token);
            status=await _alice.GetFromJsonAsync<ExecutionDto>($"/api/executions/{run!.Id}",timeout.Token);
        } while(status!.Status is ExecutionStatus.Queued or ExecutionStatus.Running);
        Assert.True(status.Status==ExecutionStatus.Completed,status.ErrorMessage);
        var logs=await _alice.GetFromJsonAsync<List<LogEventDto>>($"/api/executions/{run!.Id}/logs");
        Assert.NotEmpty(logs!);
        Assert.Equal(HttpStatusCode.NotFound,(await _bob.GetAsync($"/api/executions/{run.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await _bob.GetAsync($"/api/logs/{run.Id}")).StatusCode);
        Assert.Empty((await _bob.GetFromJsonAsync<List<WorkflowDto>>("/api/workflows"))!);
        Assert.Equal(HttpStatusCode.OK,(await _workspaceViewer.GetAsync($"/api/workflows/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await _workspaceViewer.PostAsJsonAsync($"/api/workflows/{saved.Id}/execute",new {})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await _workspaceViewer.PostAsJsonAsync("/api/workflows",new SaveWorkflowRequest { Name="Denied", Id=saved.Id, WorkspaceId=_workspace, Graph=graph })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await _stranger.GetAsync($"/api/workflows/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await _stranger.GetAsync($"/api/workspaces/{_workspace}/members")).StatusCode);
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
            var record = await db.Executions.Include(e=>e.Workflow).SingleAsync(e=>e.Id==run.Id);
            Assert.Equal("hello",record.Memory!.RootElement.GetProperty($"node_{trigger}").GetProperty("input").GetString());
            Assert.Equal("hello",record.Memory.RootElement.GetProperty($"node_{set}").GetProperty("input").GetString());
            var before = await db.ExecutionLogs.CountAsync(e=>e.ExecutionId==run.Id);
            await scope.ServiceProvider.GetRequiredService<WorkflowRunner>().RunAsync(run.Id,record.Workflow.TenantId!,record.Workflow.CreatedByUserId!.Value);
            Assert.Equal(before,await db.ExecutionLogs.CountAsync(e=>e.ExecutionId==run.Id));
            var service = scope.ServiceProvider.GetRequiredService<WorkflowJobService>();
            await service.StartRecurringAsync(saved.Id,record.Workflow.TenantId!,record.Workflow.CreatedByUserId.Value,CancellationToken.None);
            await service.StartRecurringAsync(saved.Id,record.Workflow.TenantId!,record.Workflow.CreatedByUserId.Value,CancellationToken.None);
            Assert.Equal(3,await db.Executions.CountAsync(e=>e.WorkflowId==saved.Id));
        }
        var streams = await Task.WhenAll(_alice.GetStringAsync($"/api/logs/{run.Id}"),_alice.GetStringAsync($"/api/logs/{run.Id}"));
        Assert.All(streams,text=>{ Assert.Contains("event: complete",text); Assert.Contains("id: ",text); });
        Assert.Equal(streams[0],streams[1]);
        Assert.Contains("workflow-completed",await _alice.GetStringAsync($"/api/workflow/execution/{run.Id}/stream"));
        Assert.Equal(HttpStatusCode.NotFound,(await _bob.GetAsync($"/api/workflow/execution/{run.Id}/stream")).StatusCode);
        var scheduled=await _alice.PostAsJsonAsync($"/api/executions/schedule/{saved.Id}?delaySeconds=3600",new{});
        scheduled.EnsureSuccessStatusCode();
        var queuedId=(await scheduled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("executionId").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await _alice.PostAsJsonAsync($"/api/executions/{queuedId}/cancel",new{})).StatusCode);
        Assert.Equal(ExecutionStatus.Cancelled,(await _alice.GetFromJsonAsync<ExecutionDto>($"/api/executions/{queuedId}"))!.Status);
        graph.Nodes[0].Name="Updated start";
        var update=new SaveWorkflowRequest { Id=saved.Id,Name="Updated",WorkspaceId=_workspace,Graph=graph };
        var updates=await Task.WhenAll(_alice.PostAsJsonAsync("/api/workflows",update),_alice.PostAsJsonAsync("/api/workflows",update));
        Assert.All(updates,response=>Assert.Equal(HttpStatusCode.OK,response.StatusCode));
        using(var scope=_app.Services.CreateScope()) Assert.Equal(2,await scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>().WorkflowRevisions.CountAsync(v=>v.WorkflowId==saved.Id));
        graph.Nodes[0].Type="code.javascript";
        Assert.Equal(HttpStatusCode.BadRequest,(await _alice.PostAsJsonAsync("/api/workflows",new SaveWorkflowRequest {Name="Unsupported",WorkspaceId=_workspace,Graph=graph})).StatusCode);
        // A second client/request sees the saved graph, proving database persistence rather than static lists.
        Assert.Equal(3,(await _alice.GetFromJsonAsync<WorkflowDto>($"/api/workflows/{saved.Id}"))!.Graph!.Nodes.Count);
    }

    [Fact]
    public async Task Step_mode_waits_between_nodes_and_resume_finishes_remaining_nodes()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        var graph = new WorkflowGraphDto { Nodes = [
            new() { Id=first, Type="trigger.manual", Name="Start" },
            new() { Id=second, Type="debug", Name="Middle" },
            new() { Id=third, Type="debug", Name="End" }],
            Connections = [new() { SourceNodeId=first, TargetNodeId=second }, new() { SourceNodeId=second, TargetNodeId=third }] };
        var saved = await _alice.PostAsJsonAsync("/api/workflows", new SaveWorkflowRequest { Name="Step acceptance", WorkspaceId=_workspace, Graph=graph });
        saved.EnsureSuccessStatusCode();
        var workflow = (await saved.Content.ReadFromJsonAsync<WorkflowDto>())!;
        var response = await _alice.PostAsJsonAsync($"/api/workflows/{workflow.Id}/test-run", new StartExecutionRequest());
        response.EnsureSuccessStatusCode();
        var run = (await response.Content.ReadFromJsonAsync<ExecutionDto>())!;
        await WaitForStatus(run.Id, ExecutionStatus.Paused);
        Assert.Equal(HttpStatusCode.NotFound, (await _bob.PostAsync($"/api/workflows/execution/{run.Id}/step", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _alice.PostAsync($"/api/workflows/execution/{run.Id}/step", null)).StatusCode);
        // Wait for the first node to finish, not merely the initial paused status.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var logs = await _alice.GetFromJsonAsync<List<LogEventDto>>($"/api/executions/{run.Id}/logs", timeout.Token);
            if (logs!.Any(l => l.NodeId == first)) break;
            await Task.Delay(100, timeout.Token);
        }
        await WaitForStatus(run.Id, ExecutionStatus.Paused);
        Assert.Equal(HttpStatusCode.OK, (await _alice.PostAsync($"/api/workflows/execution/{run.Id}/resume", null)).StatusCode);
        await WaitForStatus(run.Id, ExecutionStatus.Completed);
        Assert.Equal(HttpStatusCode.BadRequest, (await _alice.PostAsync($"/api/workflows/execution/{run.Id}/step", null)).StatusCode);
        var cancelledResponse = await _alice.PostAsJsonAsync($"/api/workflows/{workflow.Id}/test-run", new StartExecutionRequest());
        cancelledResponse.EnsureSuccessStatusCode();
        var cancelled = (await cancelledResponse.Content.ReadFromJsonAsync<ExecutionDto>())!;
        await WaitForStatus(cancelled.Id, ExecutionStatus.Paused);
        Assert.Equal(HttpStatusCode.OK, (await _alice.PostAsync($"/api/workflows/execution/{cancelled.Id}/cancel", null)).StatusCode);
        await WaitForStatus(cancelled.Id, ExecutionStatus.Cancelled);
        Assert.Equal(HttpStatusCode.BadRequest, (await _alice.PostAsync($"/api/workflows/execution/{cancelled.Id}/resume", null)).StatusCode);
    }

    private async Task WaitForStatus(Guid id, ExecutionStatus expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var status = await _alice.GetFromJsonAsync<ExecutionDto>($"/api/executions/{id}", timeout.Token);
            if (status!.Status == expected) return;
            Assert.NotEqual(ExecutionStatus.Failed, status.Status);
            await Task.Delay(100, timeout.Token);
        }
    }

    [Fact]
    public async Task Anonymous_and_experimental_surfaces_are_closed()
    {
        using var anonymous=_app.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/workflows")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync("/health/live")).StatusCode);
        foreach(var path in new[]{"/api/workflow","/api/chatbots","/api/marketplace","/api/published/example","/hangfire"})
            Assert.Equal(HttpStatusCode.NotFound,(await _alice.GetAsync(path)).StatusCode);
        _alice.DefaultRequestHeaders.Add("X-Tenant-ID","another-tenant");
        Assert.Equal(HttpStatusCode.Forbidden,(await _alice.GetAsync("/api/workflows")).StatusCode);
    }
    [Fact]
    public async Task Signed_tokens_validate_issuer_audience_expiry_and_provision_once()
    {
        using var client = _app.CreateClient(new() { BaseAddress=new Uri("https://localhost") });
        var subject=Guid.NewGuid().ToString();
        string Token(string issuer=Issuer,string audience="sonofanton-backend",bool expired=false) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer,audience,
                [new Claim("sub",subject),new Claim("email",subject+"@example.test"),new Claim("roles","editor")],
                DateTime.UtcNow.AddHours(-1),expired?DateTime.UtcNow.AddMinutes(-5):DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(SigningKey,SecurityAlgorithms.HmacSha256)));
        foreach(var token in new[]{Token(issuer:"https://wrong.invalid"),Token(audience:"wrong"),Token(expired:true)})
        {
            client.DefaultRequestHeaders.Authorization=new("Bearer",token);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/workflows")).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization=new("Bearer",Token());
        var responses=await Task.WhenAll(client.GetAsync("/api/workspaces"),client.GetAsync("/api/workspaces"));
        Assert.All(responses,response=>Assert.Equal(HttpStatusCode.OK,response.StatusCode));
        using var scope=_app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
        Assert.Equal(1,await db.Users.CountAsync(u=>u.ExternalIdentityId==subject));
        Assert.Equal(1,await db.Workspaces.CountAsync(w=>w.OwnerUser!.ExternalIdentityId==subject));
        var hash=ApiKeyAuthenticationHandler.HashApiKey(_alice.DefaultRequestHeaders.GetValues("X-Api-Key").Single());
        var key=await db.ApiKeys.FirstAsync(k=>k.KeyHash==hash);
        key.RateLimitPerMinute=2;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK,(await _alice.GetAsync("/api/nodes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await _alice.GetAsync("/api/nodes")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await _alice.GetAsync("/api/nodes")).StatusCode);
    }

}
