using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace GraphSmtpRelay;

public sealed class RelayMailboxFilter(IConfiguration configuration, ILogger<RelayMailboxFilter> logger) : IMailboxFilter, IMailboxFilterFactory
{
    public IMailboxFilter CreateInstance(ISessionContext context) => this;

    public Task<bool> CanAcceptFromAsync(
        ISessionContext context,
        IMailbox from,
        int size,
        CancellationToken cancellationToken)
    {
        var sender = MailboxAddress(from);

        var allowedSenders = GetConfiguredList("Relay:AllowedSenders");
        if (allowedSenders.Count == 0)
        {
            logger.LogWarning("No Relay:AllowedSenders configured. Rejecting sender {Sender}", sender);
            return Task.FromResult(false);
        }

        if (allowedSenders.Contains(sender, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogInformation("Accepted MAIL FROM {Sender}", sender);
            return Task.FromResult(true);
        }

        logger.LogWarning("Rejected MAIL FROM {Sender}", sender);
        return Task.FromResult(false);
    }

    public Task<bool> CanDeliverToAsync(
        ISessionContext context,
        IMailbox to,
        IMailbox from,
        CancellationToken cancellationToken)
    {
        var recipient = MailboxAddress(to);

        var allowedRecipientDomains = GetConfiguredList("Relay:AllowedRecipientDomains");

        if (allowedRecipientDomains.Count == 0)
        {
            logger.LogInformation("Accepted RCPT TO {Recipient}", recipient);
            return Task.FromResult(true);
        }

        var domain = to.Host?.Trim().ToLowerInvariant() ?? string.Empty;

        if (allowedRecipientDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogInformation("Accepted RCPT TO {Recipient}", recipient);
            return Task.FromResult(true);
        }

        logger.LogWarning("Rejected RCPT TO {Recipient}", recipient);
        return Task.FromResult(false);
    }

    private HashSet<string> GetConfiguredList(string key)
    {
        var values = configuration.GetSection(key).Get<string[]>() ?? Array.Empty<string>();
        return values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeAddress)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeAddress(string value)
    {
        return value.Trim().Trim('<', '>').ToLowerInvariant();
    }

    private static string MailboxAddress(IMailbox mailbox)
    {
        var user = mailbox.User ?? string.Empty;
        var host = mailbox.Host ?? string.Empty;
        return $"{user}@{host}".Trim().ToLowerInvariant();
    }
}