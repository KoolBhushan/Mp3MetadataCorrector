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

namespace Mp3MetadataCorrector.Models;

/// <summary>
/// Represents a normalized audio track match retrieved from an online metadata provider (iTunes or MusicBrainz).
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Gets or sets the name of the online provider source (e.g. "iTunes", "MusicBrainz").
    /// </summary>
    public string Source { get; set; } = "iTunes";

    /// <summary>
    /// Gets or sets the unique external track or recording identifier.
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the track title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary artist name.
    /// </summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album collection title.
    /// </summary>
    public string Album { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album artist name.
    /// </summary>
    public string AlbumArtist { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    public uint? Year { get; set; }

    /// <summary>
    /// Gets or sets the formatted release date string.
    /// </summary>
    public string ReleaseDate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the genre classification.
    /// </summary>
    public string Genre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the track position number within the release.
    /// </summary>
    public uint? TrackNumber { get; set; }

    /// <summary>
    /// Gets or sets the total number of tracks on the release.
    /// </summary>
    public uint? TrackTotal { get; set; }

    /// <summary>
    /// Gets or sets the disc position number for multi-disc releases.
    /// </summary>
    public uint? DiscNumber { get; set; }

    /// <summary>
    /// Gets or sets the total number of discs on the release.
    /// </summary>
    public uint? DiscTotal { get; set; }

    /// <summary>
    /// Gets or sets the duration of the recording.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the URL of the standard resolution album artwork.
    /// </summary>
    public string? ArtworkUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL of the high-resolution (up to 1000x1000) album artwork.
    /// </summary>
    public string? HighResArtworkUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional preview audio stream URL.
    /// </summary>
    public string? PreviewUrl { get; set; }

    /// <summary>
    /// Gets a formatted subtitle string combining artist, album, and year for list item display.
    /// </summary>
    public string DisplaySubtitle => $"{Artist} — {Album} ({Year?.ToString() ?? "Unknown Year"})";

    /// <summary>
    /// Gets a formatted track index summary (e.g. "Track 4/12").
    /// </summary>
    public string TrackSummary => TrackNumber.HasValue 
        ? (TrackTotal.HasValue ? $"Track {TrackNumber}/{TrackTotal}" : $"Track {TrackNumber}") 
        : string.Empty;

    /// <summary>
    /// Gets the formatted duration string (mm:ss or hh:mm:ss).
    /// </summary>
    public string DurationFormatted => Duration.HasValue 
        ? (Duration.Value.TotalHours >= 1 ? Duration.Value.ToString(@"hh\:mm\:ss") : Duration.Value.ToString(@"mm\:ss"))
        : string.Empty;
}
