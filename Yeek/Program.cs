using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Yeek.Components;
using Yeek.Configuration;
using Yeek.Core.Repositories;
using Yeek.Database;
using Yeek.FileHosting;
using Yeek.FileHosting.Repositories;
using Yeek.Security;
using Yeek.Security.Repositories;
using Yeek.WebDAV;

var builder = WebApplication.CreateBuilder(args);

#region Configuration

var env = builder.Environment;
builder.Configuration.AddYamlFile("appsettings.yml", false, true);
builder.Configuration.AddYamlFile($"appsettings.{env.EnvironmentName}.yml", true, true);
builder.Configuration.AddYamlFile("appsettings.Secret.yml", true, true);

builder.Services.Configure<ServerConfiguration>(builder.Configuration.GetSection(ServerConfiguration.Name));

#endregion

#region Server
var serverConfiguration = new ServerConfiguration();
builder.Configuration.Bind(ServerConfiguration.Name, serverConfiguration);

//Cors
if (serverConfiguration.CorsOrigins != null)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(serverConfiguration.CorsOrigins.ToArray());
            policy.AllowCredentials();
        });
    });
}

//Forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
});

//Logging
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
builder.Logging.AddSerilog();

//Systemd Support
builder.Host.UseSystemd();


#endregion

#region Database

builder.Services.AddSingleton<ApplicationDbContext>();
builder.Services.AddHostedService<ApplicationDbContextWorker>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IModerationRepository, ModerationRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();

#endregion

builder.AddOidc();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ModerationService>();

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<WebDavManager>();
builder.Services.AddHostedService<WebDavBackgroundWorker>();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging(o =>
{
    o.GetLevel = HttpContextExtension.GetRequestLogLevel;
    o.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
    };
});

app.UseAuthentication();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.MapAuthEndpoint();

app.UseFileHosting();
app.UseModerationHosting();

app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

app.Run();

return 0;