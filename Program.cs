using System.Net.Http.Headers;
using System.Text.Json;
using CustomAppTemplate.Middleware;
using CustomAppTemplate.Models;
using CustomAppTemplate.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuration sections
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));

// Controllers with explicit JSON serialization settings
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Preserve [JsonPropertyName] attributes as-is; disable auto camelCase
        opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Typed HttpClient for OpenAI — base address and Bearer auth configured once
builder.Services.AddHttpClient<IOpenAIService, OpenAIService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", opts.ApiKey);
});

// Message transformer singleton — register transforms here
builder.Services.AddSingleton(sp =>
{
    var transformer = new MessageTransformer();
    // transformer.AddTransform(MessageTransformers.ReplaceSystemMessages("You are a helpful assistant."));
    transformer.AddTransform(MessageTransformers.LimitMessageHistory(20));
    return transformer;
});

// Middleware registered for DI (IMiddleware pattern requires explicit registration)
builder.Services.AddTransient<AuthMiddleware>();
builder.Services.AddTransient<ErrorMiddleware>();

var app = builder.Build();

// Warn if OpenAI key is missing
var openAIOptions = app.Services.GetRequiredService<IOptions<OpenAIOptions>>().Value;
if (string.IsNullOrEmpty(openAIOptions.ApiKey))
    Console.WriteLine("WARNING: OpenAI__ApiKey environment variable is not set");

// Configure listen port from settings
var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
app.Urls.Clear();
app.Urls.Add($"http://localhost:{serverOptions.Port}");

// Middleware pipeline (order matters — error handler wraps auth)
app.UseMiddleware<ErrorMiddleware>();
app.UseMiddleware<AuthMiddleware>();

// Root health-check route
app.MapGet("/", () => "Down the Rabbit Hole");

app.MapControllers();

var authOptions = app.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Server is running on http://localhost:{serverOptions.Port}");
    Console.WriteLine($"API Authentication: {(authOptions.Enabled ? "Enabled" : "Disabled")}");
});

app.Run();
