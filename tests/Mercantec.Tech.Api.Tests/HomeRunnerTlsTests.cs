using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mercantec.Tech.Api.HomeGame;

namespace Mercantec.Tech.Api.Tests;

public sealed class HomeRunnerTlsTests
{
    [Theory]
    [InlineData("localhost", false, "matching", true)]
    [InlineData("localhost", false, "wrong", false)]
    [InlineData("localhost", false, "malformed", false)]
    [InlineData("localhost", false, "none", false)]
    [InlineData("localhost", true, "matching", false)]
    [InlineData("different.example", false, "matching", false)]
    public async Task WorkerTlsRequiresThePinAndAValidCertificateForTheRequestedName(
        string dnsName, bool expired, string pinKind, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        certificateRequest.CertificateExtensions.Add(san.Build());
        certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var generatedCertificate = certificateRequest.CreateSelfSigned(now.AddDays(-2),
            expired ? now.AddDays(-1) : now.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pkcs12),
            null, X509KeyStorageFlags.DefaultKeySet);
        var pin = pinKind switch
        {
            "matching" => certificate.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant(),
            "wrong" => new string('0', 64),
            "malformed" => new string('G', 64),
            _ => null
        };

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = ServeOnceAsync(listener, certificate, expected, timeout.Token);
        try
        {
            using var client = new HttpClient(HomeRunnerClient.CreateHttpHandler(pin));
            var address = $"https://localhost:{port}/health";
            if (expected)
            {
                using var response = await client.GetAsync(address, timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            else
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(address, timeout.Token));
            }
        }
        finally
        {
            await timeout.CancelAsync();
            await server;
        }
    }

    private static async Task ServeOnceAsync(TcpListener listener, X509Certificate2 certificate, bool expected, CancellationToken token)
    {
        try
        {
            using var socket = await listener.AcceptTcpClientAsync(token);
            await using var tls = new SslStream(socket.GetStream());
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, token);
            using var reader = new StreamReader(tls);
            while (await reader.ReadLineAsync(token) is { Length: > 0 }) { }
            await tls.WriteAsync("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), token);
            await tls.FlushAsync(token);
        }
        catch (Exception error) when (!expected && error is AuthenticationException or IOException or OperationCanceledException)
        {
            // A client rejecting the certificate ends the handshake without an HTTP request.
        }
    }
}
