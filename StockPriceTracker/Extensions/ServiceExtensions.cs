using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddSqlite(this IServiceCollection services, Action<SqliteDbContextOptionsBuilder>? configureSqliteOptions = null)
    {
        // Resolve configuration from the real container when the options are built, rather than
        // building a throwaway provider here (ASP0000). This runs lazily on first DbContext use.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=stocks.db";
            options.UseSqlite(connectionString, configureSqliteOptions);
        });

        return services;
    }

    public static IServiceCollection AddPostgreSql(this IServiceCollection services,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsqlOptions = null)
    {
        // Resolve configuration from the real container when the options are built, rather than
        // building a throwaway provider here (ASP0000). This runs lazily on first DbContext use.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, configureNpgsqlOptions);
        });

        return services;
    }

    public static IServiceCollection AddIdentityAndAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>();

        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key configuration is required. Set it in appsettings.Development.json or via the Jwt__Key environment variable.");

        var jwtIssuer = config["Jwt:Issuer"] ?? "StockPriceTracker";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = false,
                    ValidateLifetime = true
                };
            });

        services.AddAuthorization();
        services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
        services.AddSingleton(new TokenService(jwtKey, jwtIssuer));
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
