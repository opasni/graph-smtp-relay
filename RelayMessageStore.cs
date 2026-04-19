using System.Buffers;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace GraphSmtpRelay;

public sealed class RelayMessageStore(
    ILogger<RelayMessageStore> logger,
    GraphMailSender graphMailSender,
    IConfiguration configuration) : MessageStore
{
    public override async Task<SmtpResponse> SaveAsync(
        ISessionContext context,
        IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream();
            var position = buffer.GetPosition(0);

            while (buffer.TryGet(ref position, out var memory))
            {
                await stream.WriteAsync(memory, cancellationToken);
            }

            stream.Position = 0;
            var mimeMessage = await MimeMessage.LoadAsync(stream, cancellationToken);

            var configuredSender = NormalizeAddress(configuration["Relay:Graph:SenderUser"] ?? string.Empty);
            var actualSender = NormalizeAddress(mimeMessage.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty);

            if (!string.Equals(configuredSender, actualSender, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Message From {ActualSender} does not match configured Graph sender {ConfiguredSender}", actualSender, configuredSender);
                return new SmtpResponse(SmtpReplyCode.MailboxNameNotAllowed, "Sender is not allowed");
            }

            stream.Position = 0;
            await graphMailSender.SendMimeAsync(configuredSender, stream, cancellationToken);

            var recipients = string.Join(", ", mimeMessage.To.Mailboxes.Select(x => x.Address));
            logger.LogInformation("Relayed message '{Subject}' from {Sender} to {Recipients}",
                mimeMessage.Subject, actualSender, recipients);

            return SmtpResponse.Ok;
        }
        catch (GraphMailException ex)
        {
            logger.LogError(ex, "Graph rejected message");
            return new SmtpResponse(SmtpReplyCode.TransactionFailed, $"Graph relay failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected relay failure");
            return new SmtpResponse(SmtpReplyCode.TransactionFailed, "Relay failed");
        }
    }

    private static string NormalizeAddress(string value)
    {
        return value.Trim().Trim('<', '>').ToLowerInvariant();
    }
}