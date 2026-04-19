using SmtpServer;
using SmtpServer.Storage;

namespace GraphSmtpRelay;

public sealed class SmtpRelayHostedService(
    ILogger<SmtpRelayHostedService> logger,
    IConfiguration configuration,
    RelayMessageStore messageStore,
    RelayMailboxFilter mailboxFilter) : BackgroundService
{
    private SmtpServer.SmtpServer? _smtpServer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hostName = configuration["Relay:Smtp:ServerName"] ?? "graph-smtp-relay";
        var port = int.TryParse(configuration["Relay:Smtp:Port"], out var parsedPort) ? parsedPort : 2525;

        var options = new SmtpServerOptionsBuilder()
            .ServerName(hostName)
            .Port(port, isSecure: false)
            .Build();

        var services = new SmtpServer.ComponentModel.ServiceProvider();
        services.Add(messageStore);
        services.Add((IMailboxFilter)mailboxFilter);
        services.Add((IMailboxFilterFactory)mailboxFilter);

        _smtpServer = new SmtpServer.SmtpServer(options, services);

        logger.LogInformation("Starting SMTP relay on port {Port}", port);
        await _smtpServer.StartAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping SMTP relay");
        if (_smtpServer is not null)
        {
            await _smtpServer.ShutdownTask.WaitAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}