using Mahkias.Api.Security;
using Mahkias.Api.Services;
using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Data;
using Mahkias.Data.Helpers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var builder = WebApplication.CreateBuilder(args);

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:Origins is not configured.");
}

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new TypeFilterAttribute(typeof(RequireApiKeyFilter)));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(MyAllowSpecificOrigins,
    policy =>
    {
        policy
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(corsOrigins);
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API key sent in the X-Api-Key header.",
        Name = RequireApiKeyFilter.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<MahkiasDbContext>(options =>
    options.UseSqlServer(connectionString, x => x.MigrationsAssembly("Mahkias.Data")));

builder.Services.AddSingleton<RepositoryFactories>();
builder.Services.AddScoped<IRepositoryProvider, RepositoryProvider>();

builder.Services.AddScoped<IUow, Uow>((factory) =>
{
    return new Uow(factory.GetRequiredService<IRepositoryProvider>(), builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddSingleton<IGoogleDriveService, GoogleDriveService>();
builder.Services.AddScoped<ProjectDriveFolders>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MahkiasDbContext>();
    await DbInitialiser.SeedAsync(db);
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase) != true)
    {
        await next();
        return;
    }

    var originalBody = context.Response.Body;
    using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    await next();
    context.Response.Body = originalBody;

    if (context.Response.StatusCode != StatusCodes.Status200OK)
    {
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
        return;
    }

    // Swagger UI builds from before 5.19 reject the 3.0.4 version Microsoft.OpenApi writes.
    var json = Encoding.UTF8.GetString(buffer.ToArray())
        .Replace("\"openapi\": \"3.0.4\"", "\"openapi\": \"3.0.1\"", StringComparison.Ordinal);
    context.Response.Headers.ContentLength = Encoding.UTF8.GetByteCount(json);
    await context.Response.WriteAsync(json);
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(MyAllowSpecificOrigins);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapControllers();

app.Run();
