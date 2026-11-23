using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mp3MetadataCorrector.Models;

namespace Mp3MetadataCorrector.Services;

public class YtDownloaderService
{
    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+(\d+(?:\.\d+)?)%\s+of\s+~?([^\s]+)\s+at\s+([^\s]+)\s+ETA\s+([^\s]+)",
        RegexOptions.Compiled);

    private static readonly Regex DestinationRegex = new(
        @"(?:\[ExtractAudio\] Destination: |\[download\] Destination: )(.*)",
        RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public YtDownloaderService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

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

    public bool IsYtDlpAvailable => !string.IsNullOrEmpty(FindYtDlpPath());
    public bool IsFfmpegAvailable => !string.IsNullOrEmpty(FindFfmpegPath());

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
