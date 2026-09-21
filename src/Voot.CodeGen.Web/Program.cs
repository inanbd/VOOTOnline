using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Voot.CodeGen.Domain.Identity;
using Voot.CodeGen.Infrastructure;
using Voot.CodeGen.Infrastructure.Data;
using Voot.CodeGen.Infrastructure.Identity;
using Voot.CodeGen.Infrastructure.Runs;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Web.Filters;
using Voot.CodeGen.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add<DomainExceptionFilter>();

        // Generator settings are non-nullable strings with sensible defaults, and several of
        // them are legitimately blank (procedure prefix, select-all ORDER BY). Without this,
        // nullable reference types make every one of them implicitly required.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        // The run page renders status and stage names directly, so send them as strings.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

// Identity over the Dapper stores; the stores come from infrastructure, the cookie
// pipeline is configured here because it is a web concern.
builder.Services.AddDapperIdentityStores();

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.User.RequireUniqueEmail = false;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization(options => options.AddApplicationPolicies());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
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

await InitializeAsync(app);

await app.RunAsync();

/// <summary>
/// Applies the schema, seeds the roles and first administrator, and reconciles any runs that
/// were in flight when the process last stopped.
/// </summary>
static async Task InitializeAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;

    await services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    await services.GetRequiredService<IdentitySeeder>().SeedAsync();
    await services.GetRequiredService<RunRecoveryService>().RecoverAsync();
}
