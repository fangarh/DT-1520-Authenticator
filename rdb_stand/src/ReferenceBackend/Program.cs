using Dt1520.Authenticator.AspNetCore;
using Dt1520.Authenticator.ReferenceBackend;

var builder = WebApplication.CreateBuilder(args);

if (LiveRunPreflightCommand.ShouldRun(args))
{
    Environment.ExitCode = LiveRunPreflightCommand.Run(builder.Configuration, Console.Out);
    return;
}

builder.Services.AddReferenceBackend(builder.Configuration);
builder.Services.AddDt1520Authenticator(builder.Configuration.GetSection("Dt1520Authenticator"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapReferenceBackendEndpoints();

app.Run();

public partial class Program;
