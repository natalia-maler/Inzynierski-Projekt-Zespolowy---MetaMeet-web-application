using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MetaMeetDemo.Services;
using MetaMeetDemo.Handlers;
using MetaMeetDemo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var initialScopes = new[] {
    "User.Read", "User.ReadWrite", "Team.ReadBasic.All", "Calendars.Read", "Calendars.Read.Shared",
    "User.Read.All", "User.ReadBasic.All", "Calendars.ReadWrite", "OnlineMeetings.ReadWrite",
    "Organization.Read.All", "User.ReadWrite.All", "Directory.ReadWrite.All"
};

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.Scope.Clear();
        foreach (var scope in initialScopes) options.Scope.Add(scope);
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");
        options.Scope.Add("Group.Read.All");

        options.Events.OnSignedOutCallbackRedirect = context =>
        {
            context.Response.Redirect("/");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    })
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddInMemoryTokenCaches();

builder.Services.AddScoped<LicenseService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<LicenseService>>();

    var tenantId = config["AzureAd:TenantId"];
    var clientId = config["AzureAd:ClientId"];
    var clientSecret = config["AzureAd:ClientSecret"];

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    var adminClient = new GraphServiceClient(credential);

    return new LicenseService(adminClient, logger);
});

builder.Services.AddScoped<GraphServiceClient>(sp =>
{
    var tokenAcquisition = sp.GetRequiredService<ITokenAcquisition>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var httpClientHandler = new HttpClientHandler();

    var tokenHandler = new UserAccessTokenHandler(
        tokenAcquisition,
        initialScopes,
        sp.GetRequiredService<ILogger<UserAccessTokenHandler>>()
    )
    { InnerHandler = httpClientHandler };

    var httpClient = new HttpClient(tokenHandler);
    return new GraphServiceClient(httpClient);
});

builder.Services.AddScoped<UserManagementService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var db = sp.GetRequiredService<ApplicationDbContext>();
    var logger = sp.GetRequiredService<ILogger<UserManagementService>>();
    var licenseService = sp.GetRequiredService<LicenseService>();

    var tenantId = config["AzureAd:TenantId"];
    var clientId = config["AzureAd:ClientId"];
    var clientSecret = config["AzureAd:ClientSecret"];

    GraphServiceClient adminClient;

    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
    {
        adminClient = new GraphServiceClient(new ClientSecretCredential("dummy", "dummy", "dummy"));
    }
    else
    {
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        adminClient = new GraphServiceClient(credential);
    }

    return new UserManagementService(adminClient, db, logger, config, licenseService);
});

var policy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddRazorPages()
    .AddMvcOptions(options => { options.Filters.Add(new AuthorizeFilter(policy)); })
    .AddMicrosoftIdentityUI();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GraphUserService>();
builder.Services.AddScoped<GraphCalendarService>();
builder.Services.AddScoped<GraphMeetingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapControllers();

app.Run();