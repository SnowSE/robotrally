using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace RobotRally.Services;

/// <summary>Works out the LAN URL kids should type (override with the "JoinUrl" setting).</summary>
public sealed class JoinInfo(IServer server, IConfiguration config)
{
    private static readonly string[] SkipPrefixes = ["lo", "docker", "br-", "virbr", "veth", "podman", "cni", "tailscale", "zt", "wg", "vmnet", "vboxnet"];

    public IReadOnlyList<string> Urls => _urls ??= Compute();
    public string Primary => Urls.FirstOrDefault() ?? "http://localhost:5000";
    /// <summary>Primary URL without the scheme, for big display.</summary>
    public string Display => Primary.Replace("http://", "");

    private IReadOnlyList<string>? _urls;

    private IReadOnlyList<string> Compute()
    {
        if (config["JoinUrl"] is { Length: > 0 } fixedUrl) return [fixedUrl.TrimEnd('/')];

        var port = server.Features.Get<IServerAddressesFeature>()?.Addresses
            .Select(a => Uri.TryCreate(a.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost").Replace("+", "localhost").Replace("*", "localhost"), UriKind.Absolute, out var u) ? u.Port : 0)
            .FirstOrDefault(p => p > 0) ?? 5000;

        var ips = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && !SkipPrefixes.Any(p => n.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .Distinct()
            .ToList();

        return ips.Count > 0 ? ips.Select(ip => $"http://{ip}:{port}").ToList() : [$"http://localhost:{port}"];
    }
}
