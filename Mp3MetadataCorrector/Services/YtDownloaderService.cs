// Author:
// Bhushan Kamble
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mp3MetadataCorrector.Models;

namespace Mp3MetadataCorrector.Services;

/// <summary>
/// Service responsible for orchestrating yt-dlp binary management and audio downloading/conversion to MP3 using ffmpeg.
/// </summary>
public class YtDownloaderService
{
    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+(\d+(?:\.\d+)?)%\s+of\s+~?([^\s]+)\s+at\s+([^\s]+)\s+ETA\s+([^\s]+)",
        RegexOptions.Compiled);

    private static readonly Regex DestinationRegex = new(
        @"(?:\[ExtractAudio\] Destination: |\[download\] Destination: )(.*)",
        RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="YtDownloaderService"/> class.
    /// </summary>
    /// <param name="httpClient">Optional <see cref="HttpClient"/> instance to use for downloads.</param>
    public YtDownloaderService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Gets the path to the internal tools directory where yt-dlp and ffmpeg binaries are located or downloaded.
    /// </summary>
    /// <returns>The absolute path of the tools directory.</returns>
    public string GetToolsDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        string toolsDir = Path.Combine(baseDir, "tools");
        if (!Directory.Exists(toolsDir))
        {
            // Also check project dir if running under bin/Debug
            string parentTools = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\tools"));
            if (Directory.Exists(parentTools))
                return parentTools;

            Directory.CreateDirectory(toolsDir);
        }
        return toolsDir;
    }

    /// <summary>
    /// Locates the yt-dlp executable by checking the application's tools directory, project root, and system PATH.
    /// </summary>
    /// <returns>The path to yt-dlp.exe if found, or null otherwise.</returns>
    public string? FindYtDlpPath()
    {
        string toolsDir = GetToolsDirectory();
        string localYt = Path.Combine(toolsDir, "yt-dlp.exe");
        if (File.Exists(localYt)) return localYt;

        // Check project root tools
        string projectTools = @"D:\Android Apps\MySpotify\Mp3MetadataCorrector\tools\yt-dlp.exe";
        if (File.Exists(projectTools)) return projectTools;

        // Check PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var p in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string target = Path.Combine(p.Trim(), "yt-dlp.exe");
                if (File.Exists(target)) return target;
            }
        }

        return null;
    }

    /// <summary>
    /// Locates the ffmpeg executable by checking the application's tools directory, Navidrome server installation, and system PATH.
    /// </summary>
    /// <returns>The path to ffmpeg.exe if found, or null otherwise.</returns>
    public string? FindFfmpegPath()
    {
        string toolsDir = GetToolsDirectory();
        string localFfmpeg = Path.Combine(toolsDir, "ffmpeg.exe");
        if (File.Exists(localFfmpeg)) return localFfmpeg;

        // Check project root tools
        string projectTools = @"D:\Android Apps\MySpotify\Mp3MetadataCorrector\tools\ffmpeg.exe";
        if (File.Exists(projectTools)) return projectTools;

        // Check Navidrome install directory
        string navidromeFfmpeg = @"C:\Program Files (x86)\Navidrome\ffmpeg.exe";
        if (File.Exists(navidromeFfmpeg)) return navidromeFfmpeg;

        // Check PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var p in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string target = Path.Combine(p.Trim(), "ffmpeg.exe");
                if (File.Exists(target)) return target;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a value indicating whether yt-dlp is available on the system.
    /// </summary>
    public bool IsYtDlpAvailable => !string.IsNullOrEmpty(FindYtDlpPath());

    /// <summary>
    /// Gets a value indicating whether ffmpeg is available on the system.
    /// </summary>
    public bool IsFfmpegAvailable => !string.IsNullOrEmpty(FindFfmpegPath());

    /// <summary>
    /// Downloads or updates the latest release of yt-dlp from its official GitHub repository into the application's tools folder.
    /// </summary>
    /// <param name="progress">Progress reporter for percentage downloaded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The file path to the installed yt-dlp executable.</returns>
    public async Task<string> InstallOrUpdateYtDlpAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string toolsDir = GetToolsDirectory();
        Directory.CreateDirectory(toolsDir);
        string targetFile = Path.Combine(toolsDir, "yt-dlp.exe");

        string downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        string tempFile = targetFile + ".tmp";

        try
        {
            using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double pct = (double)totalRead / totalBytes.Value * 100.0;
                        progress?.Report(pct);
                    }
                }
            }

            File.Move(tempFile, targetFile, overwrite: true);
            return targetFile;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    /// <summary>
    /// Downloads audio from the specified online URL and converts it to high-quality MP3 format.
    /// </summary>
    /// <param name="url">The URL of the media/audio to extract.</param>
    /// <param name="targetDirectory">The directory where the resulting MP3 will be saved.</param>
    /// <param name="progress">Progress reporter for tracking download percentage, speed, and status messages.</param>
    /// <param name="ct">Cancellation token for aborting the operation.</param>
    /// <returns>The path to the downloaded and converted MP3 file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when yt-dlp or the resulting MP3 file cannot be found.</exception>
    /// <exception cref="Exception">Thrown when the external yt-dlp process fails.</exception>
    public async Task<string> DownloadAudioAsMp3Async(
        string url,
        string targetDirectory,
        IProgress<DownloadProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        string? ytDlpPath = FindYtDlpPath();
        if (string.IsNullOrEmpty(ytDlpPath))
        {
            throw new FileNotFoundException("yt-dlp.exe not found. Please click 'Install yt-dlp' first.");
        }

        string? ffmpegPath = FindFfmpegPath();
        string ffmpegArg = string.Empty;
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            string ffmpegDir = Path.GetDirectoryName(ffmpegPath) ?? ffmpegPath;
            ffmpegArg = $"--ffmpeg-location \"{ffmpegDir}\"";
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string outputTemplate = Path.Combine(targetDirectory, "%(title)s.%(ext)s");
        string arguments = $"-x --audio-format mp3 --audio-quality 0 {ffmpegArg} --no-playlist --newline -o \"{outputTemplate}\" \"{url}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        string? downloadedMp3Path = null;
        var tcs = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            string line = e.Data.Trim();
            var report = new DownloadProgressReport { RawLine = line };

            // Check destination path
            var destMatch = DestinationRegex.Match(line);
            if (destMatch.Success)
            {
                string candidate = destMatch.Groups[1].Value.Trim();
                if (candidate.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    downloadedMp3Path = candidate;
                }
            }

            // Check progress
            var progMatch = ProgressRegex.Match(line);
            if (progMatch.Success)
            {
                if (double.TryParse(progMatch.Groups[1].Value, out double pct))
                    report.Percentage = pct;
                report.Speed = progMatch.Groups[3].Value;
                report.Eta = progMatch.Groups[4].Value;
                report.Status = $"Downloading: {report.Percentage:F1}% at {report.Speed} (ETA {report.Eta})";
            }
            else if (line.Contains("[ExtractAudio]"))
            {
                report.Status = "Converting audio to MP3 with ffmpeg...";
                report.Percentage = 95;
            }
            else
            {
                report.Status = line;
            }

            report.DownloadedFilePath = downloadedMp3Path;
            progress?.Report(report);
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                progress?.Report(new DownloadProgressReport
                {
                    RawLine = e.Data,
                    Status = e.Data
                });
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        }))
        {
            await process.WaitForExitAsync(ct);
        }

        if (process.ExitCode != 0)
        {
            throw new Exception($"yt-dlp exited with code {process.ExitCode}.");
        }

        // If exact destination wasn't regex-matched, find the newest .mp3 in targetDirectory
        if (string.IsNullOrEmpty(downloadedMp3Path) || !File.Exists(downloadedMp3Path))
        {
            var mp3Files = Directory.GetFiles(targetDirectory, "*.mp3", SearchOption.TopDirectoryOnly);
            DateTime newestTime = DateTime.MinValue;
            foreach (var f in mp3Files)
            {
                var fi = new FileInfo(f);
                if (fi.LastWriteTime > newestTime && (DateTime.Now - fi.LastWriteTime).TotalMinutes < 5)
                {
                    newestTime = fi.LastWriteTime;
                    downloadedMp3Path = f;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadedMp3Path) || !File.Exists(downloadedMp3Path))
        {
            throw new FileNotFoundException("Download completed but MP3 output file could not be located.");
        }

        progress?.Report(new DownloadProgressReport
        {
            Percentage = 100,
            Status = "Download & conversion complete!",
            IsComplete = true,
            DownloadedFilePath = downloadedMp3Path
        });

        return downloadedMp3Path;
    }
}
