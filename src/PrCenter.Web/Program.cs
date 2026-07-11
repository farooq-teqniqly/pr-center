using PrCenter.GitHub;
using PrCenter.Persistence;
using PrCenter.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Adapters bound to Core ports (composition root).
var connectionString =
    builder.Configuration.GetConnectionString("PrCenter") ?? "Data Source=prcenter.db";
var gitHubBaseAddress =
    builder.Configuration["GitHub:BaseAddress"]
    ?? throw new InvalidOperationException("GitHub:BaseAddress is not configured.");
builder
    .Services.AddGitHubAdapter()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(gitHubBaseAddress))
    .AddStandardResilienceHandler();
builder.Services.AddPersistenceAdapter(connectionString);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
