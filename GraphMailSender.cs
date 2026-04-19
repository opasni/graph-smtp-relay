using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;

namespace GraphSmtpRelay;

public sealed class GraphMailSender(
    HttpClient httpClient,
    TokenCredential tokenCredential,
    ILogger<GraphMailSender> logger)
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    public async Task SendMimeAsync(string senderUserPrincipalName, Stream mimeStream, CancellationToken cancellationToken)
    {
        var token = await tokenCredential.GetTokenAsync(
            new TokenRequestContext(GraphScopes),
            cancellationToken);

        mimeStream.Position = 0;
        using var memory = new MemoryStream();
        await mimeStream.CopyToAsync(memory, cancellationToken);

        var mimeBase64 = Convert.ToBase64String(memory.ToArray());

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"users/{Uri.EscapeDataString(senderUserPrincipalName)}/sendMail");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = new StringContent(mimeBase64, Encoding.UTF8, "text/plain");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Graph sendMail failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);

        throw new GraphMailException($"HTTP {(int)response.StatusCode}");
    }
}