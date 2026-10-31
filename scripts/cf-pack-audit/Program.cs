using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var mode = args[0];
var project = Path.GetFullPath(args[1]);
var source = Path.GetFullPath(args[2]);
var output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);
var options = new JsonSerializerOptions { WriteIndented = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, options));
if (mode == "inventory")
{
    var instances = new List<object>();
    var unique = new Dictionary<string, object>();
    var count = 0;
    foreach (var folder in Directory.EnumerateDirectories(source).Order())
    {
        var name = Path.GetFileName(folder);
        var rows = new List<object>();
        var modDir = Path.Combine(folder, "mods");
        foreach (var path in Directory.Exists(modDir) ? Directory.EnumerateFiles(modDir).Order() : Enumerable.Empty<string>())
        {
            if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)) continue;
            var info = new FileInfo(path);
            var bytes = File.ReadAllBytes(path);
            var sha1 = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
            uint fingerprint;
            if (unique.TryGetValue(sha1, out var old))
                fingerprint = JsonSerializer.SerializeToNode(old)!["fingerprint"]!.GetValue<uint>();
            else
            {
                var length = 0;
                foreach (var b in bytes) if (b is not (9 or 10 or 13 or 32)) bytes[length++] = b;
                fingerprint = Murmur(bytes.AsSpan(0, length));
                unique[sha1] = new { sha1, fingerprint, size = info.Length, path, filename = info.Name };
            }
            rows.Add(new { sha1, fingerprint, size = info.Length, filename = info.Name,
                disabled = info.Name.EndsWith(".disabled"), lastWriteUtc = info.LastWriteTimeUtc.ToString("O") });
            if (++count % 250 == 0) Console.WriteLine($"Hashed {count} local files; unique={unique.Count}");
        }
        JsonNode? manifest = null;
        var manifestPath = Path.Combine(folder, "manifest.json");
        if (File.Exists(manifestPath)) manifest = JsonNode.Parse(File.ReadAllText(manifestPath));
        string? mc = null;
        var setup = Path.Combine(folder, "PCL", "Setup.ini");
        if (File.Exists(setup)) mc = File.ReadLines(setup).FirstOrDefault(l => l.StartsWith("VersionVanillaName:"))?.Split(':', 2)[1];
        instances.Add(new { name, minecraftVersion = mc, files = rows, manifest });
        Console.WriteLine($"Inventory {name}: {rows.Count} files; manifest={manifest != null}");
    }
    Save("inventory.json", new { source, collectedAt = DateTimeOffset.UtcNow, instances, uniqueFiles = unique.Values });
    Console.WriteLine($"Inventory complete: {instances.Count} instances, {count} files, {unique.Count} unique SHA1s");
    return;
}

var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".local", "service-config.json")))!;
var key = settings["CURSEFORGE_API_KEY"]!.GetValue<string>();
var network = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".local", "network-config.json")))!;
var proxy = network["proxy"]?.GetValue<string>();
var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = !string.IsNullOrWhiteSpace(proxy) };
if (handler.UseProxy) handler.Proxy = new WebProxy(proxy!);
using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
client.DefaultRequestHeaders.UserAgent.ParseAdd("PigLauncher-CFAudit/1.0 (github.com/xcy1221/PigLauncher)");
Directory.CreateDirectory(Path.Combine(output, "api"));
async Task<JsonNode?> Api(string suffix, object? body, string cacheName)
{
    var cache = Path.Combine(output, "api", cacheName + ".json");
    if (File.Exists(cache)) return JsonNode.Parse(await File.ReadAllTextAsync(cache));
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            using var req = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post,
                "https://api.curseforge.com/v1/" + suffix);
            req.Headers.Add("x-api-key", key);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(req);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
            var json = JsonNode.Parse(text);
            await File.WriteAllTextAsync(cache, text);
            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine($"API {cacheName} attempt={attempt}: {e.GetType().Name}: {e.Message}");
            if (attempt == 3) throw;
            await Task.Delay(1000 * attempt);
        }
    }
    return null;
}

if (mode == "query")
{
    var inventory = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "inventory.json")))!;
    var uniques = inventory["uniqueFiles"]!.AsArray();
    var fingerprints = uniques.Select(f => f!["fingerprint"]!.GetValue<uint>()).Distinct().ToArray();
    var matches = new List<JsonNode>();
    var batchIndex = 0;
    foreach (var batch in fingerprints.Chunk(200))
    {
        var result = await Api("fingerprints/432", new { fingerprints = batch }, $"fingerprints-{batchIndex++:D3}");
        matches.AddRange(result!["data"]!["exactMatches"]!.AsArray().Where(x => x != null).Select(x => x!.DeepClone()));
        Console.WriteLine($"Fingerprint batches {batchIndex}; exact matches so far={matches.Count}");
    }
    var bySha = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
    foreach (var match in matches)
    {
        var file = match["file"]!;
        var sha = file["hashes"]!.AsArray().FirstOrDefault(h => h!["algo"]!.GetValue<int>() == 1)?["value"]?.GetValue<string>();
        if (sha != null) bySha[sha] = file;
    }
    var localMatches = uniques.Where(f => bySha.ContainsKey(f!["sha1"]!.GetValue<string>()))
        .Select(f => new { sha1 = f!["sha1"]!.GetValue<string>(), file = bySha[f!["sha1"]!.GetValue<string>()] }).ToList();
    Save("fingerprint-matches.json", localMatches);
    var ids = localMatches.Select(m => m.file["id"]!.GetValue<int>()).ToHashSet();
    foreach (var instance in inventory["instances"]!.AsArray())
        foreach (var file in instance!["manifest"]?["files"]?.AsArray() ?? [])
            if (file?["fileID"] != null) ids.Add(file["fileID"]!.GetValue<int>());
    var files = new List<JsonNode>();
    batchIndex = 0;
    foreach (var batch in ids.Order().Chunk(100))
    {
        var result = await Api("mods/files", new { fileIds = batch }, $"files-{batchIndex++:D3}");
        files.AddRange(result!["data"]!.AsArray().Where(f => f != null).Select(f => f!.DeepClone()));
        Console.WriteLine($"File batches {batchIndex}; returned={files.Count}");
    }
    Save("files.json", files);
    var projectIds = files.Select(f => f["modId"]!.GetValue<int>()).Distinct().Order().ToArray();
    var projects = new List<JsonNode>();
    batchIndex = 0;
    foreach (var batch in projectIds.Chunk(100))
    {
        var result = await Api("mods", new { modIds = batch }, $"projects-{batchIndex++:D3}");
        projects.AddRange(result!["data"]!.AsArray().Where(f => f != null).Select(f => f!.DeepClone()));
    }
    Save("projects.json", projects);
    Save("query-summary.json", new { checkedAt = DateTimeOffset.UtcNow, route = proxy == null ? "direct" : "project-proxy",
        uniqueLocal = uniques.Count, matchedLocal = localMatches.Count, requestedFiles = ids.Count, returnedFiles = files.Count,
        nullUrls = files.Count(f => string.IsNullOrWhiteSpace(f["downloadUrl"]?.GetValue<string>())),
        returnedProjects = projects.Count, projectDistributionDisabled = projects.Count(p => p["allowModDistribution"]?.GetValue<bool>() == false) });
    Console.WriteLine(File.ReadAllText(Path.Combine(output, "query-summary.json")));
    return;
}

if (mode == "diagnose")
{
    var files = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "files.json")))!.AsArray()
        .Where(f => string.IsNullOrWhiteSpace(f!["downloadUrl"]?.GetValue<string>())).ToArray();
    Directory.CreateDirectory(Path.Combine(output, "diagnostics"));
    var index = 0;
    foreach (var batch in files.Chunk(100))
        await Api("mods/files", new { fileIds = batch.Select(f => f!["id"]!.GetValue<int>()).ToArray() }, $"null-recheck-{index++:D3}");
    await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (file, ct) =>
    {
        var id = file!["id"]!.GetValue<int>();
        var modId = file["modId"]!.GetValue<int>();
        var path = Path.Combine(output, "diagnostics", id + ".json");
        if (File.Exists(path)) return;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.curseforge.com/v1/mods/{modId}/files/{id}/download-url");
            req.Headers.Add("x-api-key", key);
            using var response = await client.SendAsync(req, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonNode? body = null;
            try { body = JsonNode.Parse(text); } catch (JsonException) { }
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { id, modId,
                checkedAt = DateTimeOffset.UtcNow, status = (int)response.StatusCode, body,
                nonJson = body == null ? text[..Math.Min(text.Length, 400)] : null }, options), ct);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { id, modId, error = ex.Message }, options), ct);
        }
    });
    Console.WriteLine($"Diagnosed {files.Length} null URLs");
    return;
}

if (mode is "probe" or "probe-get")
{
    var ranged = mode == "probe";
    var probeDirectory = Path.Combine(output, ranged ? "probes" : "get-probes");
    var files = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "files.json")))!.AsArray();
    var inventory = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "inventory.json")))!;
    var localPaths = inventory["uniqueFiles"]!.AsArray().ToDictionary(f => f!["sha1"]!.GetValue<string>(),
        f => f!["path"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
    var matched = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "fingerprint-matches.json")))!.AsArray()
        .ToDictionary(f => f!["sha1"]!.GetValue<string>(), f => f!["file"]!["id"]!.GetValue<int>());
    var indexedFiles = files.ToDictionary(f => f!["id"]!.GetValue<int>(), f => f!);
    var controls = new HashSet<int>();
    foreach (var instance in inventory["instances"]!.AsArray())
    {
        var candidate = instance!["files"]!.AsArray().Select(f => f!["sha1"]!.GetValue<string>())
            .Where(matched.ContainsKey).Select(sha => indexedFiles[matched[sha]])
            .Where(f => !string.IsNullOrWhiteSpace(f["downloadUrl"]?.GetValue<string>())
                && f["fileLength"]!.GetValue<long>() >= 10000)
            .OrderBy(f => f["fileLength"]!.GetValue<long>()).FirstOrDefault();
        if (candidate != null) controls.Add(candidate["id"]!.GetValue<int>());
    }
    Save("full-download-controls.json", controls.Order());
    Directory.CreateDirectory(probeDirectory);
    var completed = 0;
    await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (f, ct) =>
    {
        var id = f!["id"]!.GetValue<int>();
        var cachePath = Path.Combine(probeDirectory, id + ".json");
        if (File.Exists(cachePath)) { Interlocked.Increment(ref completed); return; }
        var modId = f["modId"]!.GetValue<int>();
        var name = f["fileName"]!.GetValue<string>();
        var apiUrl = f["downloadUrl"]?.GetValue<string>();
        var missing = string.IsNullOrWhiteSpace(apiUrl);
        var fullDownload = missing || controls.Contains(id);
        var rawId = id.ToString();
        var url = missing ? $"https://edge.forgecdn.net/files/{rawId[..4]}/{int.Parse(rawId[4..])}/{Uri.EscapeDataString(name)}" : apiUrl!;
        var attempts = new List<object>();
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var uri = new Uri(url);
                var redirects = new List<string>();
                HttpResponseMessage? response = null;
                for (var hop = 0; hop < 6; hop++)
                {
                    if (uri.Scheme != "https" || uri.Host is not ("edge.forgecdn.net" or "mediafilez.forgecdn.net" or "media.forgecdn.net"))
                        throw new InvalidOperationException("Unapproved download host: " + uri.Host);
                    using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                    req.Headers.Add("x-api-key", key);
                    if (!fullDownload && ranged) req.Headers.Range = new RangeHeaderValue(0, 1023);
                    response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                    {
                        var location = response.Headers.Location ?? throw new IOException("Missing redirect location");
                        uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                        redirects.Add(uri.ToString());
                        response.Dispose(); response = null;
                        continue;
                    }
                    break;
                }
                using (response)
                {
                    if (response == null) throw new IOException("Too many redirects");
                    var status = (int)response.StatusCode;
                    long bytesRead = 0;
                    string? actualSha1 = null;
                    if (response.IsSuccessStatusCode)
                    {
                        using var stream = await response.Content.ReadAsStreamAsync(ct);
                        if (fullDownload)
                        {
                            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                            var buffer = new byte[131072];
                            int read;
                            while ((read = await stream.ReadAsync(buffer, ct)) > 0) { sha.AppendData(buffer, 0, read); bytesRead += read; }
                            actualSha1 = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
                        }
                        else
                        {
                            var buffer = new byte[1024];
                            while (bytesRead < 1024)
                            {
                                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)(1024 - bytesRead)), ct);
                                if (read == 0) break;
                                bytesRead += read;
                            }
                            actualSha1 = Convert.ToHexString(SHA1.HashData(buffer.AsSpan(0, (int)bytesRead))).ToLowerInvariant();
                        }
                    }
                    var expectedSha = f["hashes"]!.AsArray().FirstOrDefault(h => h!["algo"]!.GetValue<int>() == 1)?["value"]?.GetValue<string>();
                    var expectedBytes = f["fileLength"]!.GetValue<long>();
                    string? localPrefixSha1 = null;
                    if (!fullDownload && expectedSha != null && localPaths.TryGetValue(expectedSha, out var localPath))
                    {
                        using var local = File.OpenRead(localPath);
                        var buffer = new byte[Math.Min(1024, expectedBytes)];
                        local.ReadExactly(buffer);
                        localPrefixSha1 = Convert.ToHexString(SHA1.HashData(buffer)).ToLowerInvariant();
                    }
                    var success = response.IsSuccessStatusCode && (fullDownload
                        ? actualSha1 == expectedSha && bytesRead == expectedBytes
                        : bytesRead == Math.Min(1024, expectedBytes) && (localPrefixSha1 == null || actualSha1 == localPrefixSha1));
                    attempts.Add(new { attempt, status, bytesRead, expectedBytes, actualSha1, expectedSha,
                        fullDownload, localPrefixSha1, success, elapsedMs = timer.ElapsedMilliseconds,
                        contentRange = response.Content.Headers.ContentRange?.ToString(), redirects });
                    if (success || status is 401 or 403 or 404 or 410) break;
                }
            }
            catch (Exception ex)
            {
                attempts.Add(new { attempt, error = ex.GetType().Name + ": " + ex.Message, success = false,
                    elapsedMs = timer.ElapsedMilliseconds });
            }
            if (attempt == 1) await Task.Delay(1000, ct);
        }
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(new { id, modId, filename = name,
            missingApiUrl = missing, fullDownloadControl = controls.Contains(id), requestMode = ranged ? "range" : "ordinary-get",
            url, checkedAt = DateTimeOffset.UtcNow, attempts }, options), ct);
        var done = Interlocked.Increment(ref completed);
        if (missing || done % 100 == 0) Console.WriteLine($"Probe {done}/{files.Count}: file={id}; fallback={missing}");
    });
    Console.WriteLine($"Probes complete: {completed}");
    return;
}
throw new ArgumentException("Unknown mode");

static uint Murmur(ReadOnlySpan<byte> data)
{
    unchecked
    {
        const uint m = 0x5bd1e995;
        uint h = 1u ^ (uint)data.Length;
        var i = 0;
        for (; i + 4 <= data.Length; i += 4)
        {
            var k = BinaryPrimitives.ReadUInt32LittleEndian(data[i..]);
            k *= m; k ^= k >> 24; k *= m; h *= m; h ^= k;
        }
        switch (data.Length - i)
        {
            case 3: h ^= (uint)data[i + 2] << 16; goto case 2;
            case 2: h ^= (uint)data[i + 1] << 8; goto case 1;
            case 1: h ^= data[i]; h *= m; break;
        }
        h ^= h >> 13; h *= m; h ^= h >> 15;
        return h;
    }
}
