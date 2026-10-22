using Api.Options;
using Microsoft.Extensions.Options;

namespace Analyzer.Git;

public sealed class ReposUrlValidator
{
    private readonly HashSet<string> _allowedHosts;

    public ReposUrlValidator(
        IOptions<ReposOptions> options)
    {
        _allowedHosts = new HashSet<string>(
            options.Value.AllowedHosts,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.Port == 443
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && uri.AbsolutePath != "/"
            && _allowedHosts.Contains(uri.IdnHost);
    }
}