using System.Reflection;
using System.Text;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Services;
using team_hub_organization.Services.Me;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Members.Invitations;
using team_hub_organization.Services.Members.Permissions;
using team_hub_organization.Services.Members.Roles;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrganizationDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        return services;
    }

    public static IServiceCollection AddOrganizationHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres");

        return services;
    }

    public static IServiceCollection AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
            });

        return services;
    }

    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddControllers();
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(0, 1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.FormatGroupName = (_, _) => "v0.1.0";
                options.SubstituteApiVersionInUrl = true;
            });
        services.AddEndpointsApiExplorer();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<Program>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddTeamHubBlobStorage(configuration);
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IOrganizationAuthorizationService, OrganizationAuthorizationService>();
        services.AddScoped<IPermissionSeedService, PermissionSeedService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationAvatarService, OrganizationAvatarService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITeamAvatarService, TeamAvatarService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IMeService, MeService>();
        services.AddGrpc();
        return services;
    }
}
