using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using FansVoice.EventService.Data;
using FansVoice.EventService.Hubs;
using FansVoice.EventService.Interfaces;
using FansVoice.EventService.Services;

namespace FansVoice.EventService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add CORS
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder =>
                    builder.WithOrigins(Configuration.GetSection("AllowedOrigins").Get<string[]>())
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials());
            });

            // Add Controllers
            services.AddControllers();

            // Add SignalR
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 102400; // 100 KB
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            });

            // Add Entity Framework
            services.AddDbContext<EventDbContext>(options =>
                options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));

            // Add Redis
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse(Configuration.GetConnectionString("Redis"));
                configuration.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(configuration);
            });

            // Add Services
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IChantService, ChantService>();
            services.AddSingleton<ICacheService, CacheService>();
            services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
            services.AddSingleton<IMessageBusService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<MessageBusService>>();
                var circuitBreaker = sp.GetRequiredService<ICircuitBreakerService>();
                return new MessageBusService(
                    logger,
                    circuitBreaker,
                    Configuration["RabbitMQ:HostName"],
                    Configuration["RabbitMQ:UserName"],
                    Configuration["RabbitMQ:Password"]);
            });

            // Add Authentication
            services.AddAuthentication("Bearer")
                .AddJwtBearer(options =>
                {
                    options.Authority = Configuration["Auth:Authority"];
                    options.Audience = Configuration["Auth:Audience"];
                    options.RequireHttpsMetadata = false;

                    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            // Add Swagger
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "FansVoice Event Service API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Add Health Checks
            services.AddHealthChecks()
                .AddDbContextCheck<EventDbContext>()
                .AddRedis(Configuration.GetConnectionString("Redis"))
                .AddRabbitMQ($"amqp://{Configuration["RabbitMQ:UserName"]}:{Configuration["RabbitMQ:Password"]}@{Configuration["RabbitMQ:HostName"]}");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FansVoice Event Service API v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<ChantHub>("/hubs/chant");
                endpoints.MapHealthChecks("/health");
            });

            // Ensure database is created and migrations are applied
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<EventDbContext>();
                context.Database.Migrate();
            }
        }
    }
}