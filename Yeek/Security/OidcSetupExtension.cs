using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Yeek.Security;

public static class OidcSetupExtension
{
    public static void AddOidc(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<LoginHandler>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie("Cookies", options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromHours(1);
                options.LoginPath = builder.Configuration["Auth:LoginPath"] ?? "/login";
                options.LogoutPath = builder.Configuration["Auth:LogoutPath"] ?? "/logout";
                options.ReturnUrlParameter = "returnUrl";
                options.AccessDeniedPath = "/access-denied";
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;;

                options.Authority = builder.Configuration["Auth:Authority"];
                options.ClientId = builder.Configuration["Auth:ClientId"];
                options.ClientSecret = builder.Configuration["Auth:ClientSecret"];
                options.MetadataAddress = builder.Configuration["Auth:MetadataAddress"];
                options.SaveTokens = true;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.GetClaimsFromUserInfoEndpoint = true;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = builder.Configuration["Server:RoleClaim"] ?? "role";

                options.Events.OnTokenValidated = async ctx =>
                {
                    var handler = ctx.HttpContext.RequestServices.GetRequiredService<LoginHandler>();
                    await handler.HandleTokenValidated(ctx);
                };

                options.Events.OnUserInformationReceived = async ctx =>
                {
                    var handler = ctx.HttpContext.RequestServices.GetRequiredService<LoginHandler>();
                    await handler.HandleUserDataUpdate(ctx);
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
    }

    public static void MapAuthEndpoint(this WebApplication app)
    {
        app.MapGet("/login", (string? returnUrl) => Results.Challenge(new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/",
        }, [OpenIdConnectDefaults.AuthenticationScheme]));

        app.MapGet("/logout", async (string? returnUrl, HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect(returnUrl ?? context.Request.PathBase.Add("/"));
        });
    }
}