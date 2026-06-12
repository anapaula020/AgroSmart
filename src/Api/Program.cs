using System.Text;
using Api.Data;
using Api.Middleware;
using Api.Services;
using Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(15), null)
    ));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength         = 6;
    options.Password.RequireDigit           = true;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail         = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT + ApiKey - JWT como scheme padrão (sobrescreve cookie do Identity) ────
// AddIdentity define DefaultAuthenticateScheme = "Identity.Application" (cookie).
// Sobrescrevemos aqui para que [Authorize] nas API controllers use JWT por padrão,
// retornando 401 em vez de redirecionar para /Account/Login (que causaria 404).
// PolicyScheme "Combined" selects the right handler per-request:
// X-Api-Key header → ApiKey handler; otherwise → JWT Bearer.
// This ensures [Authorize] endpoints accept both schemes without requiring
// per-controller AuthenticationSchemes overrides.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Combined";
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme             = "Combined";
    })
    .AddPolicyScheme("Combined", "JWT or ApiKey", opts =>
    {
        opts.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.ContainsKey("X-Api-Key")
                ? Api.Middleware.ApiKeyAuthHandler.SchemeName
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    })
    .AddScheme<AuthenticationSchemeOptions, Api.Middleware.ApiKeyAuthHandler>(
        Api.Middleware.ApiKeyAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<IbgeService>();
builder.Services.AddScoped<OpenWeatherService>();

// ── HttpClients ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("openweather", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["OpenWeather:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5/");
    c.Timeout = TimeSpan.FromSeconds(10);
});


// ── Cookie auth for MVC pages ────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath        = "/login";
    options.LogoutPath       = "/logout";
    options.AccessDeniedPath = "/login";
    options.Cookie.HttpOnly  = true;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan   = TimeSpan.FromHours(8);
    // Não redireciona para login em requests que esperam JSON (ajax/api)
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/account") &&
            (ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
             (ctx.Request.ContentType?.Contains("application/json") == true) ||
             ctx.Request.Headers["Accept"].ToString().Contains("application/json")))
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// ── Controllers + Razor Views ────────────────────────────────────────────────
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = builder.Configuration["App:Name"] ?? "AgroSmart API",
        Version     = "v1",
        Description = "API REST para gestão agrícola - propriedades, safras, estoque, clima e workspaces.",
        Contact     = new OpenApiContact { Name = "AgroSmart Dev", Email = "dev@agrosmart.com" }
    });

    // ── Autenticação ──────────────────────────────────────────────────────────
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Description  = "JWT Bearer. Informe: **Bearer {seu_token}**",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name        = "X-Api-Key",
        Description = "API Key vinculada ao workspace ou usuário",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey
    });

    // Segurança aplicada por endpoint (Bearer OU ApiKey) via OperationFilter
    c.OperationFilter<Api.Middleware.SecurityRequirementsOperationFilter>();

    // ── Enums como strings ────────────────────────────────────────────────────
    c.SchemaFilter<Api.Middleware.StringEnumSchemaFilter>();

    // ── Agrupamento por domínio ───────────────────────────────────────────────
    c.TagActionsBy(api =>
    {
        api.ActionDescriptor.RouteValues.TryGetValue("controller", out var ctrl);
        ctrl ??= "";
        return ctrl switch
        {
            "Auth"                                                              => new[] { "Autenticação" },
            "RuralProperties"                                                   => new[] { "Propriedades Rurais" },
            "Harvests"                                                          => new[] { "Safras" },
            "Stock"                                                             => new[] { "Estoque" },
            "Weather"                                                           => new[] { "Clima" },
            "Alerts"                                                            => new[] { "Alertas" },
            "Workspaces"                                                        => new[] { "Workspaces" },
            "Cultures" or "SoilTypes" or "IrrigationTypes" or "InputProducts"  => new[] { "Tabelas de Apoio" },
            "Users" or "Profiles" or "ApiKeys"                                 => new[] { "Usuários e Permissões" },
            "Ibge"                                                              => new[] { "IBGE" },
            "Products" or "Categories"                                          => new[] { "Produtos" },
            _                                                                   => new[] { ctrl }
        };
    });

    c.OrderActionsBy(a =>
    {
        a.ActionDescriptor.RouteValues.TryGetValue("controller", out var ctrl);
        return $"{ctrl}_{a.RelativePath}_{a.HttpMethod}";
    });

    c.EnableAnnotations();
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("Default", p =>
{
    if (builder.Environment.IsDevelopment())
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    else
        p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
         .AllowAnyMethod().AllowAnyHeader();
}));

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// ── Database init + seed ──────────────────────────────────────────────────────
await InitializeDatabaseAsync(app.Services);

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AgroSmart API v1");
    c.RoutePrefix          = "swagger";
    c.DocumentTitle        = "AgroSmart API";
    c.ConfigObject.PersistAuthorization = true;
    c.DisplayRequestDuration();
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.DefaultModelsExpandDepth(-1);
});

app.UseStaticFiles();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.MapHealthChecks("/health");

app.Run();

// ── Database init ─────────────────────────────────────────────────────────────
static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    const int maxAttempts = 15;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            Log.Information("DB init attempt {Attempt}/{Max}...", attempt, maxAttempts);

            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            if (env.IsDevelopment())
                await db.Database.EnsureCreatedAsync();
            else
                await db.Database.MigrateAsync();

            foreach (var role in new[] { Api.Roles.Admin, Api.Roles.Gestor, Api.Roles.Operador, Api.Roles.Consulta, "User" })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            const string adminEmail = "admin@admin.com";
            if (await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                await userManager.CreateAsync(admin, "Admin@1234!");
                await userManager.AddToRoleAsync(admin, "Admin");
                Log.Information("Default admin created: {Email}", adminEmail);
            }

            var ibgeSvc = scope.ServiceProvider.GetRequiredService<Api.Services.IbgeService>();
            await ibgeSvc.SeedLocalidadesAsync(db);

            var seedEnabled = env.IsDevelopment() ||
                              string.Equals(Environment.GetEnvironmentVariable("SEED_DATA"), "true",
                                            StringComparison.OrdinalIgnoreCase);
            if (seedEnabled)
                await Api.Data.SeedDataService.SeedAsync(db, userManager);

            Log.Information("Database initialized successfully");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Log.Warning("DB init attempt {Attempt}/{Max} failed: {Msg}. Retrying in 10s...",
                attempt, maxAttempts, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    using var finalScope = services.CreateScope();
    await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
}
