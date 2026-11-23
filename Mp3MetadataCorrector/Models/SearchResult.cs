using System;

namespace Mp3MetadataCorrector.Models;

public class SearchResult
{
    public string Source { get; set; } = "iTunes";
    public string TrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public uint? Year { get; set; }
    public string ReleaseDate { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public uint? TrackNumber { get; set; }
    public uint? TrackTotal { get; set; }
    public uint? DiscNumber { get; set; }
    public uint? DiscTotal { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ArtworkUrl { get; set; }
    public string? HighResArtworkUrl { get; set; }
    public string? PreviewUrl { get; set; }

    public string DisplaySubtitle => $"{Artist} — {Album} ({Year?.ToString() ?? "Unknown Year"})";
    
    public string TrackSummary => TrackNumber.HasValue 
        ? (TrackTotal.HasValue ? $"Track {TrackNumber}/{TrackTotal}" : $"Track {TrackNumber}") 
        : string.Empty;

    public string DurationFormatted => Duration.HasValue 
        ? (Duration.Value.TotalHours >= 1 ? Duration.Value.ToString(@"hh\:mm\:ss") : Duration.Value.ToString(@"mm\:ss"))
        : string.Empty;
}
