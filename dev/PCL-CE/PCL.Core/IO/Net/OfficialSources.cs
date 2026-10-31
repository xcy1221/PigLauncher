using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App;

namespace PCL.Core.IO.Net;

public static class OfficialSources
{
    public static List<string> SelectDownloads(IEnumerable<string> urls)
    {
        var candidates = urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().ToList();
        var community = candidates.Any(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Host is "cdn.modrinth.com" or "edge.forgecdn.net" or "mediafilez.forgecdn.net" or "mod.mcimirror.top");
        var source = community ? Config.Download.Comp.CompSourceSolution : Config.Download.FileSource;
        return source switch
        {
            0 => candidates.OrderBy(url => IsMirror(url) ? 0 : 1).ToList(),
            1 => candidates,
            _ => candidates.Where(url => !IsMirror(url)).ToList()
        };
    }

    public static bool IsMirror(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Host.Contains("bmclapi", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("mcimirror.top", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("alist.8mi.tech", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("mcbbs.net", StringComparison.OrdinalIgnoreCase);
    }
}
