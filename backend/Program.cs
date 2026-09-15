using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Data;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 0. CLOUD PORT
//    Render (and most cloud hosts) do not let the app choose its own port:
//    they pass one in the PORT environment variable and expect us to listen
//    on it. Locally PORT is not set, so the normal settings are used.
// ---------------------------------------------------------------------------
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(cloudPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudPort}");

// ---------------------------------------------------------------------------
// 1. DATABASE  (Cloud Database)
//    Locally this is a PostgreSQL server on this machine.
//    In production the same code talks to Neon PostgreSQL in the cloud -
//    only the connection string changes, and it comes from an environment
//    variable (ConnectionStrings__DefaultConnection), never from the source code.
// ---------------------------------------------------------------------------
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException(
        "No database connection string. Set ConnectionStrings__DefaultConnection.");

// Neon (and Render) hand out the connection string as a URL:
//     postgresql://user:password@host/database?sslmode=require
// Npgsql expects the "Host=...;Username=...;" style, so we translate it here.
connectionString = NormalizePostgresConnectionString(connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

static string NormalizePostgresConnectionString(string value)
{
    if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
        return value;   // already in the Key=Value format

    var uri = new Uri(value);
    var userInfo = uri.UserInfo.Split(':', 2);

    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        SslMode = Npgsql.SslMode.Require
    };

    return builder.ConnectionString;
}

// ---------------------------------------------------------------------------
// 2. OUR OWN SERVICES
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<BookingService>();

// ---------------------------------------------------------------------------
// 3. AUTHENTICATION  (who are you?)  and  AUTHORIZATION  (what may you do?)
//    Every protected endpoint expects the header:  Authorization: Bearer <token>
// ---------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("No JWT key. Set Jwt__Key.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ResourceBookingApi";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// 4. CORS - lets the frontend (a different origin) call this API from the browser.
//    The allowed origins are configuration, so the production frontend URL can
//    be added on Render without touching the code.
// ---------------------------------------------------------------------------
const string FrontendCors = "FrontendCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5500" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ---------------------------------------------------------------------------
// 5. CONTROLLERS + JSON settings
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Write DateOnly / TimeOnly as plain readable strings ("2026-09-20", "10:00:00").
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// Turn model-validation failures into the simple { "message": "..." } shape
// that the frontend understands.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var firstError = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .SelectMany(kv => kv.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault() ?? "The submitted data is not valid.";

        // An upload bigger than the limit fails while ASP.NET is still reading
        // the request, and its built-in message mentions internal byte counts.
        // Replace it with something the user can act on.
        if (firstError.Contains("Request body too large", StringComparison.OrdinalIgnoreCase)
            || firstError.Contains("Failed to read the request form", StringComparison.OrdinalIgnoreCase))
        {
            return new Microsoft.AspNetCore.Mvc.ObjectResult(
                new { message = "The image is too large. Please choose a file smaller than 2 MB." })
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge
            };
        }

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { message = firstError });
    };
});

// ---------------------------------------------------------------------------
// 6. SWAGGER - interactive documentation at /swagger
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "College Resource Booking API",
        Version = "v1",
        Description = "Cloud Computing case study: book classrooms, labs and equipment."
    });

    // Adds the "Authorize" button so tokens can be tested from Swagger.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// 7. GLOBAL ERROR HANDLING
//    The user never sees a stack trace - only a friendly message.
// ---------------------------------------------------------------------------
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    app.Logger.LogError(feature?.Error, "Unhandled error on {Path}", context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(
        JsonSerializer.Serialize(new { message = "Something went wrong on the server. Please try again." }));
}));

// Swagger stays on in production too, because this is a teaching project and
// we want to demonstrate the API after deployment.
app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "College Resource Booking API v1");
    o.DocumentTitle = "Resource Booking API";
});

app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// A tiny endpoint so we (and Render) can check that the API is alive.
app.MapGet("/", () => Results.Ok(new
{
    name = "College Resource Booking API",
    status = "running",
    docs = "/swagger"
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// ---------------------------------------------------------------------------
// 8. APPLY MIGRATIONS + SEED DEMO DATA ON STARTUP
//    This is what makes the app work immediately after deploying to the cloud:
//    the tables are created in Neon automatically the first time it starts.
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, config, hasher);
    app.Logger.LogInformation("Database is ready and demo data is seeded.");
}

app.Run();
