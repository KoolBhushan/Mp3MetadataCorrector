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
using System.Threading;
using System.Threading.Tasks;
using Mp3MetadataCorrector.Models;

namespace Mp3MetadataCorrector.Services;

/// <summary>
/// Service responsible for querying online metadata providers (iTunes Search API and MusicBrainz) and downloading high-resolution album artwork.
/// </summary>
public class MetadataSearchService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataSearchService"/> class.
    /// </summary>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/> instance.</param>
    public MetadataSearchService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mp3MetadataCorrector/1.0 (winui-desktop-app)");
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Searches online music databases for track metadata matching the specified search query.
    /// </summary>
    /// <param name="query">The search keywords (song title, artist, or album).</param>
    /// <param name="provider">The provider to query ("All", "iTunes", or "MusicBrainz").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching <see cref="SearchResult"/> records.</returns>
    public async Task<List<SearchResult>> SearchAsync(string query, string provider = "All", CancellationToken ct = default)
    {
        var results = new List<SearchResult>();
        if (string.IsNullOrWhiteSpace(query))
            return results;

        Logger.Info($"[MetadataSearchService] Searching online metadata for '{query}' [Provider: {provider}]");
        string cleanQuery = Uri.EscapeDataString(query.Trim());

        if (provider is "All" or "iTunes")
        {
            try
            {
                var itunesResults = await SearchITunesAsync(cleanQuery, ct);
                results.AddRange(itunesResults);
                Logger.Info($"[MetadataSearchService] iTunes search returned {itunesResults.Count} match(es).");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[MetadataSearchService] iTunes search query failed for '{query}'", ex);
            }
        }

        if (provider is "All" or "MusicBrainz")
        {
            try
            {
                var mbResults = await SearchMusicBrainzAsync(cleanQuery, ct);
                results.AddRange(mbResults);
                Logger.Info($"[MetadataSearchService] MusicBrainz search returned {mbResults.Count} match(es).");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[MetadataSearchService] MusicBrainz search query failed for '{query}'", ex);
            }
        }

        Logger.Info($"[MetadataSearchService] Total search results gathered: {results.Count}");
        return results;
    }

    /// <summary>
    /// Queries the iTunes Search API for song records matching the query string.
    /// </summary>
    private async Task<List<SearchResult>> SearchITunesAsync(string escapedQuery, CancellationToken ct)
    {
        var list = new List<SearchResult>();
        string url = $"https://itunes.apple.com/search?term={escapedQuery}&entity=song&limit=15";

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return list;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var resultsElement) ||
            resultsElement.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in resultsElement.EnumerateArray())
        {
            try
            {
                string title = item.TryGetProperty("trackName", out var tn) ? tn.GetString() ?? "" : "";
                string artist = item.TryGetProperty("artistName", out var an) ? an.GetString() ?? "" : "";
                string album = item.TryGetProperty("collectionName", out var cn) ? cn.GetString() ?? "" : "";
                string genre = item.TryGetProperty("primaryGenreName", out var gn) ? gn.GetString() ?? "" : "";
                string trackId = item.TryGetProperty("trackId", out var tid) ? tid.ToString() : Guid.NewGuid().ToString();

                uint? trackNum = item.TryGetProperty("trackNumber", out var tnum) && tnum.TryGetUInt32(out var tnv) ? tnv : null;
                uint? trackCount = item.TryGetProperty("trackCount", out var tcnt) && tcnt.TryGetUInt32(out var tcv) ? tcv : null;
                uint? discNum = item.TryGetProperty("discNumber", out var dnum) && dnum.TryGetUInt32(out var dnv) ? dnv : null;

                uint? year = null;
                string releaseDateStr = string.Empty;
                if (item.TryGetProperty("releaseDate", out var rd) && rd.GetString() is { } rds && DateTime.TryParse(rds, out var dt))
                {
                    year = (uint)dt.Year;
                    releaseDateStr = dt.ToString("yyyy-MM-dd");
                }

                TimeSpan? duration = null;
                if (item.TryGetProperty("trackTimeMillis", out var ttm) && ttm.TryGetInt64(out var millis))
                {
                    duration = TimeSpan.FromMilliseconds(millis);
                }

                string? art100 = item.TryGetProperty("artworkUrl100", out var art) ? art.GetString() : null;
                string? highResArt = null;
                if (!string.IsNullOrEmpty(art100))
                {
                    // Convert 100x100 to 1000x1000 high resolution cover art
                    highResArt = art100.Replace("100x100bb.jpg", "1000x1000bb.jpg");
                }

                string? previewUrl = item.TryGetProperty("previewUrl", out var pu) ? pu.GetString() : null;

                list.Add(new SearchResult
                {
                    Source = "iTunes",
                    TrackId = trackId,
                    Title = title,
                    Artist = artist,
                    Album = album,
                    AlbumArtist = artist,
                    Genre = genre,
                    Year = year,
                    ReleaseDate = releaseDateStr,
                    TrackNumber = trackNum,
                    TrackTotal = trackCount,
                    DiscNumber = discNum,
                    Duration = duration,
                    ArtworkUrl = art100,
                    HighResArtworkUrl = highResArt,
                    PreviewUrl = previewUrl
                });
            }
            catch
            {
                // Continue with next result if one item fails parsing
            }
        }

        return list;
    }

    /// <summary>
    /// Queries the MusicBrainz web service API for recording records matching the query string.
    /// </summary>
    private async Task<List<SearchResult>> SearchMusicBrainzAsync(string escapedQuery, CancellationToken ct)
    {
        var list = new List<SearchResult>();
        string url = $"https://musicbrainz.org/ws/2/recording?query={escapedQuery}&fmt=json&limit=10";

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return list;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("recordings", out var recordings) ||
            recordings.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var rec in recordings.EnumerateArray())
        {
            try
            {
                string id = rec.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                string title = rec.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

                string artist = string.Empty;
                if (rec.TryGetProperty("artist-credit", out var artists) && artists.ValueKind == JsonValueKind.Array)
                {
                    var artistNames = new List<string>();
                    foreach (var a in artists.EnumerateArray())
                    {
                        if (a.TryGetProperty("name", out var n) && n.GetString() is { } name)
                            artistNames.Add(name);
                    }
                    artist = string.Join(", ", artistNames);
                }

                string album = string.Empty;
                uint? year = null;
                string releaseDateStr = string.Empty;
                string? artworkUrl = null;
                string? highResArtworkUrl = null;
                uint? trackNum = null;
                uint? trackTotal = null;

                if (rec.TryGetProperty("releases", out var releases) && releases.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rel in releases.EnumerateArray())
                    {
                        if (string.IsNullOrEmpty(album) && rel.TryGetProperty("title", out var relTitle))
                            album = relTitle.GetString() ?? "";

                        if (!year.HasValue && rel.TryGetProperty("date", out var relDate) && relDate.GetString() is { } rds)
                        {
                            releaseDateStr = rds;
                            if (rds.Length >= 4 && uint.TryParse(rds[..4], out var yr))
                                year = yr;
                        }

                        if (artworkUrl == null && rel.TryGetProperty("id", out var relId) && relId.GetString() is { } rid)
                        {
                            artworkUrl = $"https://coverartarchive.org/release/{rid}/front-250.jpg";
                            highResArtworkUrl = $"https://coverartarchive.org/release/{rid}/front-500.jpg";
                        }

                        if (trackNum == null && rel.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var m in media.EnumerateArray())
                            {
                                if (m.TryGetProperty("track-offset", out var to) && to.TryGetUInt32(out var tOffset))
                                    trackNum = tOffset + 1;
                                if (m.TryGetProperty("track-count", out var tc) && tc.TryGetUInt32(out var tCnt))
                                    trackTotal = tCnt;
                            }
                        }
                    }
                }

                TimeSpan? duration = null;
                if (rec.TryGetProperty("length", out var lenProp) && lenProp.TryGetInt64(out var lenMs))
                {
                    duration = TimeSpan.FromMilliseconds(lenMs);
                }

                list.Add(new SearchResult
                {
                    Source = "MusicBrainz",
                    TrackId = id,
                    Title = title,
                    Artist = artist,
                    Album = album,
                    AlbumArtist = artist,
                    Year = year,
                    ReleaseDate = releaseDateStr,
                    TrackNumber = trackNum,
                    TrackTotal = trackTotal,
                    Duration = duration,
                    ArtworkUrl = artworkUrl,
                    HighResArtworkUrl = highResArtworkUrl
                });
            }
            catch
            {
                // Continue with next recording
            }
        }

        return list;
    }

    /// <summary>
    /// Downloads image binary data from the specified artwork URL.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the raw image byte array and its detected MIME type, or null if download fails.</returns>
    public async Task<(byte[] Data, string MimeType)?> DownloadArtworkAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        Logger.Info($"[MetadataSearchService] Downloading artwork from: {url}");
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"[MetadataSearchService] Artwork download returned status: {response.StatusCode} for {url}");
                return null;
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync(ct);
            string mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            Logger.Info($"[MetadataSearchService] Downloaded artwork ({data.Length} bytes, MIME: {mime}) from: {url}");
            return (data, mime);
        }
        catch (Exception ex)
        {
            Logger.Error($"[MetadataSearchService] Failed to download artwork from: {url}", ex);
            return null;
        }
    }
}
