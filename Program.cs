using System.Text.Json.Serialization;

using Api.Services;
using Api.Repositories;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddSingleton<VulnRepository>();
builder.Services.AddSingleton<VulnService>();
builder.Services.AddSingleton<ScanRepository>();
builder.Services.AddSingleton<ScanService>();
builder.Services.AddSingleton<ScanCancelRegistry>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Пока HTTPS не настроен:
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();