using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FansVoice.EventService.Data;
using FansVoice.EventService.Interfaces;
using FansVoice.EventService.Services;
using FansVoice.EventService.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();

    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddSignalR().AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"));

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Add DbContext
    builder.Services.AddDbContext<EventDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add Services
    builder.Services.AddScoped<IEventService, EventService>();
    builder.Services.AddScoped<IChantService, ChantService>();
    builder.Services.AddSingleton<IMessageBusService, MessageBusService>();
    builder.Services.AddScoped<ICacheService, CacheService>();
    builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();

    // Add Redis Cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = builder.Configuration["Redis:InstanceName"];
    });

    // Configure Redis Retry Policy
    builder.Services.Configure<RedisCacheOptions>(options =>
    {
        options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 30000,
            ConnectRetry = int.Parse(builder.Configuration["Redis:RetryCount"] ?? "3")
        };
    });

    // Add JWT Authentication
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT:Key is not configured")))
            };
        });

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials(); // SignalR için gerekli
            });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ChantHub>("/chanthub"); // SignalR Hub'ı için endpoint

    // Automatically apply migrations
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();
        db.Database.Migrate();
    }

    Log.Information("Starting FansVoice Event Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
