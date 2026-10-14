using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Glide.Platform;

/// <summary>
/// Finds newer releases and installs them. Releases come from a public GitHub repository (the
/// source can stay private): its latest release has an installer per architecture and a
/// SHA256SUMS.txt that every download is checked against before it runs.
/// </summary>
internal sealed class Updater
{
    /// <summary>A newer release, ready to install.</summary>
    public sealed record Update(Version Version, string Tag, Uri Installer, Uri? Checksums);

    private sealed record Release([property: JsonPropertyName("tag_name")] string Tag,
                                  [property: JsonPropertyName("draft")] bool Draft,
                                  [property: JsonPropertyName("prerelease")] bool Prerelease,
                                  [property: JsonPropertyName("assets")] List<Asset> Assets);

    private sealed record Asset([property: JsonPropertyName("name")] string Name,
                                [property: JsonPropertyName("browser_download_url")] Uri Url);

    // Before the HTTP client, which puts it in its user agent: static fields initialize in order.
    public static Version Current { get; } =
        Version.TryParse(FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion?.Split('+')[0], out var version) ? version : new Version(0, 1, 0);

    private static readonly HttpClient http = CreateClient();
    private bool reportedMissing;

    /// <summary>"x64" or "arm64": the installer that fits this PC (an Arm PC gets the Arm build, even if this copy runs emulated).</summary>
    private static string Architecture => RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Glide-Windows/{Current.ToString(3)}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>The latest release of <paramref name="repository"/> ("owner/name") if it's newer than this copy.</summary>
    public async Task<Update?> CheckAsync(string repository)
    {
        try
        {
            using var response = await http.GetAsync($"https://api.github.com/repos/{repository}/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No public release yet (or a private repository, which needs a login).
                if (!reportedMissing) Log.Info($"Updates: no public release at {repository}");
                reportedMissing = true;
                return null;
            }
            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<Release>();
            if (release is null || release.Draft || release.Prerelease) return null;
            if (!Version.TryParse(release.Tag.TrimStart('v', 'V'), out var version) || version <= Current) return null;
            var installer = release.Assets.FirstOrDefault(a => a.Name.EndsWith($"-{Architecture}-Setup.exe", StringComparison.OrdinalIgnoreCase));
            if (installer is null)
            {
                Log.Info($"Updates: {release.Tag} has no {Architecture} installer");
                return null;
            }
            var checksums = release.Assets.FirstOrDefault(a => a.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            Log.Info($"Updates: {release.Tag} is available (this is {Current})");
            return new Update(version, release.Tag, installer.Url, checksums?.Url);
        }
        catch (Exception e)
        {
            // Offline, rate-limited, or anything else: try again next time.
            Log.Info($"Updates: couldn't check ({e.GetType().Name}: {e.Message})");
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer, checks it against the release's checksums, and runs it silently.
    /// The installer asks this Glide to put windows back and quit, updates it, and starts the new one.
    /// </summary>
    public async Task<bool> InstallAsync(Update update)
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "Glide-update");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, Path.GetFileName(update.Installer.LocalPath));
            await using (var file = File.Create(path))
            await using (var download = await http.GetStreamAsync(update.Installer))
                await download.CopyToAsync(file);

            if (update.Checksums is null)
            {
                Log.Error("Updates: the release has no checksums; not installing an unverified download");
                return false;
            }
            var sums = await http.GetStringAsync(update.Checksums);
            var expected = sums.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
                .FirstOrDefault(parts => parts.Length == 2 && parts[1].Trim().TrimStart('*').Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase))?[0];
            await using (var file = File.OpenRead(path))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
                if (expected is null || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error($"Updates: {Path.GetFileName(path)} doesn't match its checksum; not installing");
                    File.Delete(path);
                    return false;
                }
            }
            Log.Info($"Updates: installing {update.Tag}");
            Process.Start(new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /UPDATE=1") { UseShellExecute = true });
            return true;
        }
        catch (Exception e)
        {
            Log.Error("Updates: installing failed", e);
            return false;
        }
    }
}
