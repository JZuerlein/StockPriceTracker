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
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=stocks.db";

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        
        return services;
    }

    public static IServiceCollection AddPostgreSql(this IServiceCollection services,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsqlOptions = null)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        
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
        services.AddAntiforgery();
        services.AddSingleton(new TokenService(jwtKey, jwtIssuer));
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
