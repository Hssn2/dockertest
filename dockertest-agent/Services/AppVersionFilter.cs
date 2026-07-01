using System.Text.RegularExpressions;

namespace dockertest_agent.Services;

public static partial class AppVersionFilter
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex SemVerRegex();

    public static bool IsAppVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;
        if (tag.StartsWith("agent", StringComparison.OrdinalIgnoreCase))
            return false;
        return SemVerRegex().IsMatch(tag);
    }

    public static bool IsExactImageRepo(string repoTag, string imageName)
    {
        var colon = repoTag.LastIndexOf(':');
        if (colon <= 0)
            return false;
        var repo = repoTag[..colon];
        return string.Equals(repo, imageName, StringComparison.OrdinalIgnoreCase);
    }
}
