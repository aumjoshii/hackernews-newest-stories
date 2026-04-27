using HackerNews.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Service registrations -------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();

builder.Services.Configure<HackerNewsServiceOptions>(
    builder.Configuration.GetSection("HackerNews"));

// Typed HttpClient so the service gets a properly-pooled client and
// can be swapped out (or mocked) via IHackerNewsService.
builder.Services.AddHttpClient<IHackerNewsService, HackerNewsService>((sp, client) =>
{
    var opts = builder.Configuration.GetSection("HackerNews").Get<HackerNewsServiceOptions>()
               ?? new HackerNewsServiceOptions();
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

const string CorsPolicy = "AllowAngularApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---- Pipeline --------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthorization();
app.MapControllers();

// Lightweight health endpoint that's handy for Azure App Service health probes.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program visible to the test project for WebApplicationFactory<Program>.
public partial class Program { }
