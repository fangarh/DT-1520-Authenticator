var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "OtpAuth.Api",
}))
.WithName("HealthCheck");

app.MapGet("/api/v1/system/info", () => Results.Ok(new
{
    service = "OtpAuth.Api",
    version = "0.1.0-scaffold",
    timestampUtc = DateTimeOffset.UtcNow,
}))
.WithName("SystemInfo");

app.Run();
