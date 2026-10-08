using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Security;
using Renderset.Core.Tenancy;
using Renderset.Web.Email;

namespace Renderset.Web.Identity;

/// <summary>
/// Usuarios, sesión y tenants de Web (fase B de SEGURIDAD.md).
///
/// - Identity con email y contraseña, correo confirmado obligatorio y
///   bloqueo tras 5 intentos.
/// - Registro cerrado: se entra por invitación (o la del propietario
///   inicial, ver BootstrapOwner).
/// - El tenant elegido va en la cookie; la pertenencia y el rol se leen de
///   TenantMembers en cada petición (caché de 30 s) y en el circuito cada
///   minuto.
/// - Cada llamada a la API lleva un token firmado con usuario, tenant y rol.
/// </summary>
public static class IdentitySetup
{
    public static WebApplicationBuilder AddRendersetIdentity(
        this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var connectionString = configuration.GetConnectionString("RenderSet");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta 'ConnectionStrings:RenderSet' en Web: los usuarios y la pertenencia a tenants viven en la base de datos de RenderSet.");

        // El contexto con ámbito lo usa Identity (UserManager, SignInManager).
        // TenantDirectory abre uno propio por operación con la factoría: en
        // un circuito de Blazor varios componentes (menú, AuthorizeView,
        // la página) consultan la pertenencia a la vez, y un DbContext no
        // admite dos operaciones simultáneas.
        services.AddDbContext<IdentityDb>(
            options => options.UseSqlServer(connectionString),
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddDbContextFactory<IdentityDb>(options =>
            options.UseSqlServer(connectionString));

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddCascadingAuthenticationState();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services
            .AddIdentityCore<RendersetUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<IdentityDb>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // El restablecimiento de contraseña caduca en 2 horas, no en 1 día.
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(2));

        // El sello de seguridad se comprueba cada minuto (lo mismo que el
        // circuito): cambiar la contraseña saca de todas las sesiones. Al
        // rehacer la identidad se conservan el tenant elegido y el nombre.
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(1);
            options.OnRefreshingPrincipal = context =>
            {
                var current = context.CurrentPrincipal;
                var identity = context.NewPrincipal?.Identity as ClaimsIdentity;

                if (current is null || identity is null)
                    return Task.CompletedTask;

                foreach (var type in new[] { RendersetClaimTypes.TenantId, RendersetClaimTypes.TenantName, RendersetClaimTypes.DisplayName })
                {
                    if (current.FindFirst(type) is { } claim && identity.FindFirst(type) is null)
                        identity.AddClaim(new Claim(type, claim.Value));
                }

                return Task.CompletedTask;
            };
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "rs.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;

            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
            options.AccessDeniedPath = "/account/denied";

            options.ExpireTimeSpan = TimeSpan.FromHours(10);
            options.SlidingExpiration = true;

            // Además del sello de seguridad, en cada petición: ¿sigue siendo
            // miembro del tenant de la cookie? Si no, fuera.
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);

                if (context.Principal?.Identity?.IsAuthenticated != true)
                    return;

                var directory = context.HttpContext.RequestServices.GetRequiredService<TenantDirectory>();

                var membership =
                    await directory.GetMembershipAsync(
                        context.Principal.FindFirstValue(ClaimTypes.NameIdentifier),
                        context.Principal.FindFirstValue(RendersetClaimTypes.TenantId),
                        context.HttpContext.RequestAborted);

                if (membership is null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });

        services.AddScoped<AuthenticationStateProvider, TenantRevalidatingAuthenticationStateProvider>();

        services.AddAuthorizationBuilder()
            .AddPolicy(TenantPolicies.Member, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement(TenantRole.Member)))
            .AddPolicy(TenantPolicies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement(TenantRole.Admin)))
            .AddPolicy(TenantPolicies.Owner, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantRoleRequirement(TenantRole.Owner)));

        services.AddScoped<IAuthorizationHandler, TenantRoleHandler>();

        services.AddScoped<TenantDirectory>();
        services.AddScoped<TenantSignIn>();
        services.AddScoped<UserCurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<UserCurrentTenant>());
        services.AddScoped<TenantSession>();
        services.TryAddSingletonTimeProvider();

        // ---------------------------------------------------------- correo

        var email =
            configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (email.IsConfigured)
        {
            services.AddSingleton(email);
            services.AddSingleton<IAppEmailSender, AcsEmailSender>();
        }
        else
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Falta la configuración de correo (Email:Sender y Email:Endpoint o Email:ConnectionString): " +
                    "sin ella no se pueden mandar invitaciones ni restablecer contraseñas.");
            }

            services.AddSingleton<IAppEmailSender, LoggingEmailSender>();
        }

        // Los enlaces de los correos (invitación, contraseña) se construyen
        // con App:PublicUrl. Sin ella saldrían de la cabecera Host de la
        // petición, que puede manipular quien la envía.
        if (string.IsNullOrWhiteSpace(configuration["App:PublicUrl"]) &&
            !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Falta 'App:PublicUrl' (URL pública de Web, p. ej. https://app.renderset.miempresa.com): " +
                "con ella se construyen los enlaces de invitación y de restablecer la contraseña.");
        }

        // ---------------------------------------------------------- token para la API

        var privateKey = configuration["Security:UserTokenPrivateKey"];

        if (!string.IsNullOrWhiteSpace(privateKey))
        {
            services.AddSingleton(new TenantTokenSigner(privateKey));
        }
        else if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Falta 'Security:UserTokenPrivateKey' (clave privada de los tokens de usuario). " +
                "Sin ella la API rechaza todas las llamadas de Web.");
        }

        services.AddScoped<TenantTokenFactory>();

        services.AddHostedService<BootstrapOwner>();

        return builder;
    }

    private static void TryAddSingletonTimeProvider(
        this IServiceCollection services)
    {
        if (services.All(x => x.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
