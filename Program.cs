using CountryCurrencyApi.Data;
using CountryCurrencyApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration: Get connection string from env or appsettings
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
           ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
           ?? throw new Exception("Database connection string not provided. Set 'ConnectionStrings__DefaultConnection' or CONNECTION_STRING env var.");

// Add DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn))
);

// HttpClient
builder.Services.AddHttpClient<ExternalApiClient>();

// Services
builder.Services.AddScoped<CountryRefreshService>();
builder.Services.AddSingleton<ImageGenerator>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // customize validation response
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = new Dictionary<string, string?>();
            foreach (var kvp in context.ModelState)
            {
                var field = kvp.Key;
                var state = kvp.Value;
                var firstError = state.Errors.FirstOrDefault()?.ErrorMessage;
                if (!string.IsNullOrEmpty(firstError))
                    errors[field] = firstError;
            }
            var result = new BadRequestObjectResult(new { error = "Validation failed", details = errors });
            result.ContentTypes.Add("application/json");
            return result;
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// Ensure database migrations applied on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
