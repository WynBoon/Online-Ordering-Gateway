using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;

namespace StoreDevice.App;

internal static class GatewayHttp
{
    public static IHttpClientBuilder AddGatewayDeviceClient(this IServiceCollection services) =>
        services.AddHttpClient<GatewayDeviceClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                client.DefaultRequestHeaders.ExpectContinue = false;
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);

    private static HttpMessageHandler CreateHandler()
    {
        // AndroidMessageHandler (OkHttp) often never raises HttpClient.Timeout and
        // never honors CancellationToken — the UI sits on "Sending HTTP request"
        // forever. SocketsHttpHandler + IPv4 + a hard timeout always returns.
        return new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(12),
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = ConnectIPv4Async,
            SslOptions =
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }
        };
    }

    /// <summary>
    /// Azure App Service publishes AAAA records. The Android emulator's IPv6
    /// path commonly black-holes TLS; Windows Machine on the same PC is fine.
    /// </summary>
    private static async ValueTask<Stream> ConnectIPv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host,
            AddressFamily.InterNetwork,
            cancellationToken);
        if (addresses.Length == 0)
        {
            throw new HttpRequestException($"No IPv4 address for {context.DnsEndPoint.Host}.");
        }

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        try
        {
            await socket.ConnectAsync(
                new IPEndPoint(addresses[0], context.DnsEndPoint.Port),
                cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
