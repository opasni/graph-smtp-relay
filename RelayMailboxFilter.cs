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
        var allowedSenders = GetConfiguredList("Relay:AllowedSenders");
        if (allowedSenders.Count == 0)
        {
            logger.LogWarning("No Relay:AllowedSenders configured. Rejecting sender {Sender}", from.ToString());
            return Task.FromResult(false);
        }

        var sender = NormalizeAddress(from.ToString() ?? string.Empty);
        var isAllowed = allowedSenders.Contains(sender, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            logger.LogWarning("Rejected MAIL FROM {Sender}", sender);
        }

        return Task.FromResult(isAllowed);
    }

    public Task<bool> CanDeliverToAsync(
        ISessionContext context,
        IMailbox to,
        IMailbox from,
        CancellationToken cancellationToken)
    {
        var allowedRecipientDomains = GetConfiguredList("Relay:AllowedRecipientDomains");

        if (allowedRecipientDomains.Count == 0)
        {
            return Task.FromResult(true);
        }

        var recipient = NormalizeAddress(to.ToString() ?? string.Empty);
        var domain = recipient.Split('@').LastOrDefault() ?? string.Empty;
        var isAllowed = allowedRecipientDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            logger.LogWarning("Rejected RCPT TO {Recipient}", recipient);
        }

        return Task.FromResult(isAllowed);
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
}