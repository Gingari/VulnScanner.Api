using System.Text.Json.Serialization;
using Api.Services;
using Api.Repositories;
using Analyzer.Scanners;
using Api.Exceptions;
using Api.Options;
using Api.Middleware;
using Analyzer.Processes;
using Analyzer.Git;
using Analyzer;

DotNetEnv.Env.NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<VulnRepository>();
builder.Services.AddSingleton<VulnService>();

builder.Services.AddSingleton<ScanRepository>();
builder.Services.AddSingleton<ScanService>();
builder.Services.AddSingleton<ScanCancelRegistry>();
builder.Services.AddSingleton<ScanQueue>();

builder.Services.AddSingleton<ICodeScanner, GitleaksScanner>();
builder.Services.AddSingleton<ICodeScanner, TrivyScanner>();
builder.Services.AddHostedService<ScanBackgroundService>();

builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<IProcessRunner>(
    services => services.GetRequiredService<ProcessRunner>());
builder.Services.AddSingleton<ReposUrlValidator>();
builder.Services.AddSingleton<IGitReposDownloader, GitReposDownloader>();
builder.Services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddOptions<ScanningOptions>()
    .Bind(builder.Configuration.GetSection("Scanning"))
    .Validate(
        options => options.MaxParallelScans > 0,
        "MaxParallelScans должен быть больше нуля.")
    .Validate(
        options => options.QueueCapacity > 0,
        "QueueCapacity должен быть больше нуля.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ReposOptions>()
    .Bind(builder.Configuration.GetSection("Repositories"))
    .Validate(
        options => options.AllowedHosts.Length > 0 &&
                   options.AllowedHosts.All(host =>
                       !string.IsNullOrWhiteSpace(host) &&
                       Uri.CheckHostName(host) == UriHostNameType.Dns),
        "Укажите разрешённые DNS-имена хостов.")
    .Validate(
        options => options.CloneTimeoutSeconds > 0,
        "CloneTimeoutSeconds должен быть больше нуля.")
    .ValidateOnStart();

var app = builder.Build();


app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program
{
}