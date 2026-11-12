using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace RobotRally.Services;

/// <summary>Simple shared-password admin login. Tokens live in memory (and the admin's localStorage).</summary>
public sealed class AdminAuth(IConfiguration config)
{
    private readonly string _password =
        config["ROBOTRALLY_ADMIN_PASSWORD"] is { Length: > 0 } env ? env : config["Admin:Password"] ?? "rally";
    private readonly ConcurrentDictionary<string, byte> _tokens = new();

    public bool IsDefaultPassword => _password == "rally";

    public string? Login(string? password)
    {
        if (password is null) return null;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password.Trim()), Encoding.UTF8.GetBytes(_password));
        if (!ok) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _tokens[token] = 0;
        return token;
    }

    /// <summary>A token without the password, for the desktop app's own window (it runs the server, so it is the admin).</summary>
    public string IssueToken()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _tokens[token] = 0;
        return token;
    }

    public bool IsValid(string? token) => token is not null && _tokens.ContainsKey(token);
}
