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
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Mp3MetadataCorrector.Models;
using Mp3MetadataCorrector.Services;

namespace Mp3MetadataCorrector.Cli;

/// <summary>
/// Implements CLI commands designed for AI agents and script automation.
/// </summary>
public static class CliCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Mp3TagService TagService = new();
    private static readonly MetadataSearchService SearchService = new();
    private static readonly YtDownloaderService DownloaderService = new();

    /// <summary>
    /// Reads and outputs metadata for one or more MP3 files.
    /// </summary>
    public static int HandleRead(string[] args)
    {
        bool json = HasFlag(args, "--json");
        string? extractArtPath = GetOption(args, "--extract-art");
        string? targetPath = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return ErrorResponse("Missing file path argument. Usage: mp3corrector read <file-or-pattern> [--json] [--extract-art <path>]", json);
        }

        var files = ResolveFiles(targetPath);
        if (files.Count == 0)
        {
            return ErrorResponse($"No MP3 files found matching: '{targetPath}'", json);
        }

        var results = new List<object>();

        foreach (var file in files)
        {
            try
            {
                var meta = TagService.ReadMetadata(file);

                if (!string.IsNullOrEmpty(extractArtPath) && meta.HasCoverArt && meta.CoverArtBytes != null)
                {
                    string outArt = files.Count == 1 ? extractArtPath : Path.Combine(Path.GetDirectoryName(extractArtPath) ?? "", $"{Path.GetFileNameWithoutExtension(file)}_cover.jpg");
                    File.WriteAllBytes(outArt, meta.CoverArtBytes);
                    if (!json) Console.WriteLine($"Saved cover artwork to: {outArt}");
                }

                var dto = ToMetadataDto(meta);
                results.Add(dto);

                if (!json)
                {
                    PrintHumanReadableMetadata(dto);
                }
            }
            catch (Exception ex)
            {
                if (json)
                {
                    results.Add(new { file, error = ex.Message });
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error reading '{file}': {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(results.Count == 1 ? results[0] : results, JsonOptions));
        }

        return 0;
    }

    /// <summary>
    /// Searches online metadata sources (iTunes and MusicBrainz).
    /// </summary>
    public static async Task<int> HandleSearchAsync(string[] args)
    {
        bool json = HasFlag(args, "--json");
        string provider = GetOption(args, "--provider") ?? "All";
        string? query = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResponse("Missing search query. Usage: mp3corrector search \"Artist - Title\" [--provider <all|itunes|musicbrainz>] [--json]", json);
        }

        try
        {
            var results = await SearchService.SearchAsync(query, provider);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));
                return 0;
            }

            Console.WriteLine($"\nFound {results.Count} result(s) for query: \"{query}\" [Provider: {provider}]\n");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{i + 1}] {r.Title} - {r.Artist}");
                Console.ResetColor();
                Console.WriteLine($"    Album: {r.Album} ({r.Year?.ToString() ?? "Unknown Year"}) | Genre: {r.Genre}");
                if (r.TrackNumber.HasValue) Console.WriteLine($"    Track: {r.TrackNumber}/{(r.TrackTotal?.ToString() ?? "?")}");
                Console.WriteLine($"    Source: {r.Source} | Art: {(string.IsNullOrEmpty(r.ArtworkUrl) ? "None" : r.ArtworkUrl)}");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            return ErrorResponse($"Search query failed: {ex.Message}", json);
        }
    }

    /// <summary>
    /// Parses a dirty filename into clean artist, title, and search query.
    /// </summary>
    public static int HandleParse(string[] args)
    {
        bool json = HasFlag(args, "--json");
        string? input = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(input))
        {
            return ErrorResponse("Missing filename argument. Usage: mp3corrector parse \"01. Artist - Song [Official Video].mp3\" [--json]", json);
        }

        var (artist, title, cleanQuery) = FileNameParser.Parse(input);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                original = input,
                artist,
                title,
                cleanQuery
            }, JsonOptions));
            return 0;
        }

        Console.WriteLine($"Original:    {input}");
        Console.WriteLine($"Artist:      {artist}");
        Console.WriteLine($"Title:       {title}");
        Console.WriteLine($"Clean Query: {cleanQuery}");
        return 0;
    }

    /// <summary>
    /// Writes or updates metadata tags on an existing MP3 file.
    /// </summary>
    public static async Task<int> HandleWriteAsync(string[] args)
    {
        bool json = HasFlag(args, "--json");
        string? filePath = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return ErrorResponse($"Target file does not exist: '{filePath}'. Usage: mp3corrector write <file.mp3> [options] [--json]", json);
        }

        try
        {
            var meta = TagService.ReadMetadata(filePath);

            // Apply overrides
            if (GetOption(args, "--title") is { } title) meta.Title = title;
            if (GetOption(args, "--artist") is { } artist) meta.Artist = artist;
            if (GetOption(args, "--album") is { } album) meta.Album = album;
            if (GetOption(args, "--album-artist") is { } albumArtist) meta.AlbumArtist = albumArtist;
            if (GetOption(args, "--genre") is { } genre) meta.Genre = genre;
            if (GetOption(args, "--year") is { } yearStr && uint.TryParse(yearStr, out var year)) meta.Year = year;
            if (GetOption(args, "--track") is { } trackStr && uint.TryParse(trackStr, out var track)) meta.TrackNumber = track;
            if (GetOption(args, "--track-total") is { } ttStr && uint.TryParse(ttStr, out var tt)) meta.TrackTotal = tt;
            if (GetOption(args, "--disc") is { } discStr && uint.TryParse(discStr, out var disc)) meta.DiscNumber = disc;
            if (GetOption(args, "--lyrics") is { } lyrics) meta.Lyrics = lyrics;
            if (GetOption(args, "--comment") is { } comment) meta.Comment = comment;

            if (HasFlag(args, "--remove-cover"))
            {
                meta.CoverArtBytes = null;
                meta.CoverArtMimeType = null;
            }
            else if (GetOption(args, "--cover") is { } coverSource)
            {
                if (File.Exists(coverSource))
                {
                    meta.CoverArtBytes = await File.ReadAllBytesAsync(coverSource);
                    meta.CoverArtMimeType = coverSource.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                }
                else if (coverSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         coverSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var artDownload = await SearchService.DownloadArtworkAsync(coverSource);
                    if (artDownload.HasValue)
                    {
                        meta.CoverArtBytes = artDownload.Value.Data;
                        meta.CoverArtMimeType = artDownload.Value.MimeType;
                    }
                }
            }

            string? newPath = null;
            if (HasFlag(args, "--rename") && !string.IsNullOrWhiteSpace(meta.Artist) && !string.IsNullOrWhiteSpace(meta.Title))
            {
                string safeArtist = CleanPath(meta.Artist);
                string safeTitle = CleanPath(meta.Title);
                string dir = Path.GetDirectoryName(meta.FilePath) ?? "";
                newPath = Path.Combine(dir, $"{safeArtist} - {safeTitle}.mp3");
            }

            TagService.WriteMetadata(meta, newPath);

            // Re-read updated file
            var updated = TagService.ReadMetadata(meta.FilePath);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath = updated.FilePath,
                    updatedMetadata = ToMetadataDto(updated)
                }, JsonOptions));
                return 0;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Metadata updated for: {updated.FilePath}");
            Console.ResetColor();
            PrintHumanReadableMetadata(ToMetadataDto(updated));
            return 0;
        }
        catch (Exception ex)
        {
            return ErrorResponse($"Failed to write metadata: {ex.Message}", json);
        }
    }

    /// <summary>
    /// Automatically searches online databases and fixes ID3 tags & artwork for a file or directory.
    /// </summary>
    public static async Task<int> HandleAutoFixAsync(string[] args)
    {
        bool json = HasFlag(args, "--json");
        bool dryRun = HasFlag(args, "--dry-run");
        bool rename = HasFlag(args, "--rename");
        bool noArt = HasFlag(args, "--no-art");
        string provider = GetOption(args, "--provider") ?? "All";
        string? targetPath = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return ErrorResponse("Missing file or folder path. Usage: mp3corrector auto-fix <file-or-dir> [--provider <all|itunes|musicbrainz>] [--rename] [--no-art] [--dry-run] [--json]", json);
        }

        var files = ResolveFiles(targetPath);
        if (files.Count == 0)
        {
            return ErrorResponse($"No MP3 files found at: '{targetPath}'", json);
        }

        var results = new List<object>();

        foreach (var file in files)
        {
            try
            {
                var existing = TagService.ReadMetadata(file);

                // Determine query
                string query;
                if (!string.IsNullOrWhiteSpace(existing.Artist) && !string.IsNullOrWhiteSpace(existing.Title))
                {
                    query = $"{existing.Artist} - {existing.Title}";
                }
                else
                {
                    var parsed = FileNameParser.Parse(file);
                    query = !string.IsNullOrWhiteSpace(parsed.CleanQuery) ? parsed.CleanQuery : Path.GetFileNameWithoutExtension(file);
                }

                if (!json) Console.WriteLine($"\n[Processing] '{Path.GetFileName(file)}' (Query: \"{query}\")...");

                var searchResults = await SearchService.SearchAsync(query, provider);
                if (searchResults.Count == 0)
                {
                    if (!json) Console.WriteLine($"  [SKIP] No online metadata match found for '{query}'.");
                    results.Add(new { file, status = "no_match_found", query });
                    continue;
                }

                // Pick first high quality match
                var best = searchResults[0];
                if (!json) Console.WriteLine($"  [MATCH] {best.Title} by {best.Artist} ({best.Album}) [{best.Source}]");

                var updated = existing.Clone();
                updated.Title = best.Title;
                updated.Artist = best.Artist;
                updated.Album = best.Album;
                updated.AlbumArtist = !string.IsNullOrWhiteSpace(best.AlbumArtist) ? best.AlbumArtist : best.Artist;
                if (best.Year.HasValue) updated.Year = best.Year;
                if (!string.IsNullOrWhiteSpace(best.Genre)) updated.Genre = best.Genre;
                if (best.TrackNumber.HasValue) updated.TrackNumber = best.TrackNumber;
                if (best.TrackTotal.HasValue) updated.TrackTotal = best.TrackTotal;
                if (best.DiscNumber.HasValue) updated.DiscNumber = best.DiscNumber;

                // Artwork
                if (!noArt)
                {
                    string? artUrl = best.HighResArtworkUrl ?? best.ArtworkUrl;
                    if (!string.IsNullOrEmpty(artUrl))
                    {
                        var downloaded = await SearchService.DownloadArtworkAsync(artUrl);
                        if (downloaded.HasValue)
                        {
                            updated.CoverArtBytes = downloaded.Value.Data;
                            updated.CoverArtMimeType = downloaded.Value.MimeType;
                            if (!json) Console.WriteLine($"  [ARTWORK] Downloaded {downloaded.Value.Data.Length} bytes cover art.");
                        }
                    }
                }

                string? newPath = null;
                if (rename && !string.IsNullOrWhiteSpace(updated.Artist) && !string.IsNullOrWhiteSpace(updated.Title))
                {
                    string safeArtist = CleanPath(updated.Artist);
                    string safeTitle = CleanPath(updated.Title);
                    string dir = Path.GetDirectoryName(updated.FilePath) ?? "";
                    newPath = Path.Combine(dir, $"{safeArtist} - {safeTitle}.mp3");
                }

                if (!dryRun)
                {
                    TagService.WriteMetadata(updated, newPath);
                    if (!json)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [SUCCESS] Updated tags{(newPath != null ? $" and renamed to '{Path.GetFileName(newPath)}'" : "")}.");
                        Console.ResetColor();
                    }
                }
                else
                {
                    if (!json) Console.WriteLine("  [DRY-RUN] Would update metadata without saving.");
                }

                results.Add(new
                {
                    file,
                    status = dryRun ? "dry_run" : "updated",
                    matched = new
                    {
                        title = best.Title,
                        artist = best.Artist,
                        album = best.Album,
                        year = best.Year,
                        source = best.Source
                    },
                    renamedTo = newPath
                });
            }
            catch (Exception ex)
            {
                results.Add(new { file, status = "error", error = ex.Message });
                if (!json) Console.WriteLine($"  [ERROR] {ex.Message}");
            }
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));
        }

        return 0;
    }

    /// <summary>
    /// Downloads an audio track from a URL via yt-dlp and optionally runs auto-fix.
    /// </summary>
    public static async Task<int> HandleDownloadAsync(string[] args)
    {
        bool json = HasFlag(args, "--json");
        bool autoFix = HasFlag(args, "--auto-fix");
        bool rename = HasFlag(args, "--rename");
        string outDir = GetOption(args, "--output-dir") ?? Environment.CurrentDirectory;
        string? url = GetFirstPositional(args);

        if (string.IsNullOrWhiteSpace(url))
        {
            return ErrorResponse("Missing media URL. Usage: mp3corrector download <url> [--output-dir <path>] [--auto-fix] [--rename] [--json]", json);
        }

        if (!DownloaderService.IsYtDlpAvailable)
        {
            if (!json) Console.WriteLine("yt-dlp is not installed. Installing latest yt-dlp release...");
            await DownloaderService.InstallOrUpdateYtDlpAsync();
        }

        var progress = new Progress<DownloadProgressReport>(r =>
        {
            if (!json && !string.IsNullOrEmpty(r.Status))
            {
                Console.Write($"\r{r.Status.PadRight(80)}");
            }
        });

        try
        {
            string mp3Path = await DownloaderService.DownloadAudioAsMp3Async(url, outDir, progress);
            if (!json) Console.WriteLine($"\n[DOWNLOAD COMPLETE] Saved to: {mp3Path}");

            object? fixedResult = null;
            if (autoFix)
            {
                if (!json) Console.WriteLine("Running auto-fix on downloaded file...");
                string[] fixArgs = rename ? [mp3Path, "--rename", "--json"] : [mp3Path, "--json"];
                var existing = TagService.ReadMetadata(mp3Path);
                var parsed = FileNameParser.Parse(mp3Path);
                string q = !string.IsNullOrWhiteSpace(parsed.CleanQuery) ? parsed.CleanQuery : Path.GetFileNameWithoutExtension(mp3Path);
                var search = await SearchService.SearchAsync(q);
                if (search.Count > 0)
                {
                    var best = search[0];
                    var u = existing.Clone();
                    u.Title = best.Title;
                    u.Artist = best.Artist;
                    u.Album = best.Album;
                    u.AlbumArtist = best.AlbumArtist ?? best.Artist;
                    u.Year = best.Year;
                    u.Genre = best.Genre;
                    u.TrackNumber = best.TrackNumber;

                    string? artUrl = best.HighResArtworkUrl ?? best.ArtworkUrl;
                    if (!string.IsNullOrEmpty(artUrl))
                    {
                        var downloaded = await SearchService.DownloadArtworkAsync(artUrl);
                        if (downloaded.HasValue)
                        {
                            u.CoverArtBytes = downloaded.Value.Data;
                            u.CoverArtMimeType = downloaded.Value.MimeType;
                        }
                    }

                    string? targetRename = null;
                    if (rename && !string.IsNullOrWhiteSpace(u.Artist) && !string.IsNullOrWhiteSpace(u.Title))
                    {
                        string safeArtist = CleanPath(u.Artist);
                        string safeTitle = CleanPath(u.Title);
                        targetRename = Path.Combine(outDir, $"{safeArtist} - {safeTitle}.mp3");
                    }

                    TagService.WriteMetadata(u, targetRename);
                    mp3Path = u.FilePath;
                    fixedResult = new { title = u.Title, artist = u.Artist, album = u.Album, year = u.Year };
                    if (!json) Console.WriteLine($"[AUTO-FIX COMPLETE] {u.Artist} - {u.Title} ({u.Album})");
                }
            }

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    filePath = mp3Path,
                    autoFixed = fixedResult
                }, JsonOptions));
            }

            return 0;
        }
        catch (Exception ex)
        {
            return ErrorResponse($"Download failed: {ex.Message}", json);
        }
    }

    /// <summary>
    /// Outputs the JSON schema of all CLI commands, arguments, and options for AI agent integration.
    /// </summary>
    public static int HandleSchema()
    {
        var schema = new
        {
            name = "mp3corrector",
            description = "AI-friendly CLI for inspecting, searching, downloading, and correcting MP3 ID3 tags and album artwork.",
            version = "1.0.0",
            commands = new object[]
            {
                new
                {
                    command = "read",
                    description = "Read ID3v1/ID3v2 tags, audio properties, and cover art info from an MP3 file or pattern.",
                    arguments = new[] { new { name = "file", type = "string", required = true, description = "File path or glob pattern (e.g. song.mp3 or *.mp3)" } },
                    options = new[]
                    {
                        new { name = "--json", type = "boolean", description = "Output clean structured JSON." },
                        new { name = "--extract-art", type = "string", description = "Extract embedded cover art to the specified image file path." }
                    }
                },
                new
                {
                    command = "search",
                    description = "Search online metadata providers (iTunes and MusicBrainz) for track information.",
                    arguments = new[] { new { name = "query", type = "string", required = true, description = "Song title, artist, or search keywords." } },
                    options = new object[]
                    {
                        new { name = "--provider", type = "string", enumValues = new[] { "All", "iTunes", "MusicBrainz" }, description = "Metadata provider to query." },
                        new { name = "--json", type = "boolean", enumValues = Array.Empty<string>(), description = "Output array of results in JSON format." }
                    }
                },
                new
                {
                    command = "parse",
                    description = "Parse dirty audio filenames (with track numbers, bitrates, video tags) into clean artist, title, and query.",
                    arguments = new[] { new { name = "filename", type = "string", required = true, description = "Audio filename or path." } },
                    options = new[] { new { name = "--json", type = "boolean", description = "Output parsed components in JSON format." } }
                },
                new
                {
                    command = "write",
                    description = "Write or update ID3v2.3 and ID3v1 metadata tags and embedded cover artwork on an MP3 file.",
                    arguments = new[] { new { name = "file", type = "string", required = true, description = "Target MP3 file path." } },
                    options = new[]
                    {
                        new { name = "--title", type = "string", description = "Track title." },
                        new { name = "--artist", type = "string", description = "Artist name." },
                        new { name = "--album", type = "string", description = "Album title." },
                        new { name = "--album-artist", type = "string", description = "Album artist." },
                        new { name = "--year", type = "integer", description = "Release year." },
                        new { name = "--genre", type = "string", description = "Music genre." },
                        new { name = "--track", type = "integer", description = "Track number." },
                        new { name = "--track-total", type = "integer", description = "Total tracks in album." },
                        new { name = "--disc", type = "integer", description = "Disc number." },
                        new { name = "--lyrics", type = "string", description = "Unsynchronized song lyrics." },
                        new { name = "--comment", type = "string", description = "Comment tag." },
                        new { name = "--cover", type = "string", description = "Local image file path OR image URL to download and embed." },
                        new { name = "--remove-cover", type = "boolean", description = "Remove existing cover artwork." },
                        new { name = "--rename", type = "boolean", description = "Rename file to 'Artist - Title.mp3' upon saving." },
                        new { name = "--json", type = "boolean", description = "Output result in JSON format." }
                    }
                },
                new
                {
                    command = "auto-fix",
                    description = "Automated AI metadata pipeline: parses dirty filename, searches online, matches metadata, downloads high-res artwork, and writes tags.",
                    arguments = new[] { new { name = "target", type = "string", required = true, description = "MP3 file path or directory containing MP3 files." } },
                    options = new object[]
                    {
                        new { name = "--provider", type = "string", description = "Metadata provider to query (All, iTunes, MusicBrainz)." },
                        new { name = "--no-art", type = "boolean", description = "Skip cover artwork download." },
                        new { name = "--rename", type = "boolean", description = "Rename files to 'Artist - Title.mp3'." },
                        new { name = "--dry-run", type = "boolean", description = "Simulate operations without writing changes to disk." },
                        new { name = "--json", type = "boolean", description = "Output array of results in JSON format." }
                    }
                },
                new
                {
                    command = "download",
                    description = "Download audio from web URL (YouTube, SoundCloud, etc.) using yt-dlp & ffmpeg to MP3.",
                    arguments = new[] { new { name = "url", type = "string", required = true, description = "Web media URL." } },
                    options = new[]
                    {
                        new { name = "--output-dir", type = "string", description = "Target download directory." },
                        new { name = "--auto-fix", type = "boolean", description = "Immediately run auto-fix on the downloaded file." },
                        new { name = "--rename", type = "boolean", description = "Rename to 'Artist - Title.mp3' when auto-fixed." },
                        new { name = "--json", type = "boolean", description = "Output result in JSON format." }
                    }
                },
                new
                {
                    command = "schema",
                    description = "Output this complete JSON tool schema for AI agent introspection.",
                    options = Array.Empty<object>()
                }
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(schema, JsonOptions));
        return 0;
    }

    private static object ToMetadataDto(TrackMetadata m)
    {
        return new
        {
            filePath = m.FilePath,
            fileName = m.FileName,
            fileSize = m.FileSize,
            title = m.Title,
            artist = m.Artist,
            album = m.Album,
            albumArtist = m.AlbumArtist,
            year = m.Year,
            genre = m.Genre,
            trackNumber = m.TrackNumber,
            trackTotal = m.TrackTotal,
            discNumber = m.DiscNumber,
            discTotal = m.DiscTotal,
            lyrics = m.Lyrics,
            comment = m.Comment,
            bitrateKbps = m.BitrateKbps,
            sampleRateHz = m.SampleRateHz,
            channels = m.Channels,
            duration = m.Duration.ToString(@"hh\:mm\:ss"),
            durationSeconds = m.Duration.TotalSeconds,
            hasCoverArt = m.HasCoverArt,
            coverArtBytes = m.CoverArtBytes?.Length,
            coverArtMimeType = m.CoverArtMimeType
        };
    }

    private static void PrintHumanReadableMetadata(dynamic d)
    {
        Console.WriteLine("------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"File: {d.fileName}");
        Console.ResetColor();
        Console.WriteLine($"Path:    {d.filePath}");
        Console.WriteLine($"Title:   {d.title}");
        Console.WriteLine($"Artist:  {d.artist}");
        Console.WriteLine($"Album:   {d.album} ({d.year})");
        Console.WriteLine($"Genre:   {d.genre}");
        Console.WriteLine($"Track:   {d.trackNumber}/{d.trackTotal} | Disc: {d.discNumber}/{d.discTotal}");
        Console.WriteLine($"Audio:   {d.bitrateKbps} kbps, {d.sampleRateHz} Hz, {d.channels} channels, {d.duration}");
        Console.WriteLine($"Cover:   {(d.hasCoverArt ? $"{d.coverArtBytes} bytes ({d.coverArtMimeType})" : "None")}");
        Console.WriteLine("------------------------------------------------------------");
    }

    private static List<string> ResolveFiles(string target)
    {
        var list = new List<string>();
        if (File.Exists(target))
        {
            list.Add(Path.GetFullPath(target));
            return list;
        }

        if (Directory.Exists(target))
        {
            list.AddRange(Directory.GetFiles(target, "*.mp3", SearchOption.AllDirectories));
            return list;
        }

        // Wildcard check
        string dir = Path.GetDirectoryName(target) ?? ".";
        string pattern = Path.GetFileName(target);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        if (Directory.Exists(dir))
        {
            list.AddRange(Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly));
        }

        return list;
    }

    private static int ErrorResponse(string message, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = message }, JsonOptions));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
        return 1;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var a in args)
        {
            if (a.Equals(flag, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? GetOption(string[] args, string option)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static string? GetFirstPositional(string[] args)
    {
        bool skipNext = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (args[i].StartsWith("--"))
            {
                if (args[i] is "--provider" or "--output-dir" or "--extract-art" or "--title" or "--artist" or
                    "--album" or "--album-artist" or "--year" or "--genre" or "--track" or "--track-total" or
                    "--disc" or "--lyrics" or "--comment" or "--cover")
                {
                    skipNext = true;
                }
                continue;
            }

            return args[i];
        }
        return null;
    }

    private static string CleanPath(string input)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input.Trim();
    }
}
