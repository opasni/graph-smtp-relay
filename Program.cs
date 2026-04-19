using Azure.Core;
using Azure.Identity;
using GraphSmtpRelay;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RelayOptions>(builder.Configuration.GetSection("Relay"));
builder.Services.AddSingleton<TokenCredential>(_ =>
{
    var tenantId = builder.Configuration["Relay:TenantId"] ?? throw new InvalidOperationException("Relay:TenantId is missing");
    var clientId = builder.Configuration["Relay:ClientId"] ?? throw new InvalidOperationException("Relay:ClientId is missing");
    var clientSecret = builder.Configuration["Relay:ClientSecret"] ?? throw new InvalidOperationException("Relay:ClientSecret is missing");

    return new ClientSecretCredential(tenantId, clientId, clientSecret);
});

builder.Services.AddHttpClient<GraphMailSender>(client =>
{
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<RelayMailboxFilter>();
builder.Services.AddSingleton<RelayMessageStore>();
builder.Services.AddHostedService<SmtpRelayHostedService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();