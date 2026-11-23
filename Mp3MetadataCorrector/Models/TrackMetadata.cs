using System;
using System.IO;

namespace Mp3MetadataCorrector.Models;

public class TrackMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath);
    public string DirectoryPath => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public uint? Year { get; set; }
    public string Genre { get; set; } = string.Empty;
    public uint? TrackNumber { get; set; }
    public uint? TrackTotal { get; set; }
    public uint? DiscNumber { get; set; }
    public uint? DiscTotal { get; set; }
    public string Lyrics { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;

    public byte[]? CoverArtBytes { get; set; }
    public string? CoverArtMimeType { get; set; }

    public TimeSpan Duration { get; set; }
    public int BitrateKbps { get; set; }
    public int SampleRateHz { get; set; }
    public int Channels { get; set; }
    public long FileSize { get; set; }

    public bool HasCoverArt => CoverArtBytes != null && CoverArtBytes.Length > 0;

    public string FormattedDuration => Duration.TotalHours >= 1 
        ? Duration.ToString(@"hh\:mm\:ss") 
        : Duration.ToString(@"mm\:ss");

    public string FormattedFileSize
    {
        get
        {
            if (FileSize <= 0) return "0 KB";
            double mb = FileSize / (1024.0 * 1024.0);
            return mb >= 1.0 ? $"{mb:F2} MB" : $"{FileSize / 1024.0:F1} KB";
        }
    }

    public string AudioPropertiesSummary => 
        $"{BitrateKbps} kbps • {SampleRateHz / 1000.0:F1} kHz • {(Channels == 2 ? "Stereo" : Channels == 1 ? "Mono" : $"{Channels} ch")}";

    public string TrackString => (TrackNumber.HasValue, TrackTotal.HasValue) switch
    {
        (true, true) => $"{TrackNumber}/{TrackTotal}",
        (true, false) => $"{TrackNumber}",
        _ => string.Empty
    };

    public TrackMetadata Clone()
    {
        return new TrackMetadata
        {
            FilePath = FilePath,
            Title = Title,
            Artist = Artist,
            Album = Album,
            AlbumArtist = AlbumArtist,
            Year = Year,
            Genre = Genre,
            TrackNumber = TrackNumber,
            TrackTotal = TrackTotal,
            DiscNumber = DiscNumber,
            DiscTotal = DiscTotal,
            Lyrics = Lyrics,
            Comment = Comment,
            CoverArtBytes = CoverArtBytes != null ? (byte[])CoverArtBytes.Clone() : null,
            CoverArtMimeType = CoverArtMimeType,
            Duration = Duration,
            BitrateKbps = BitrateKbps,
            SampleRateHz = SampleRateHz,
            Channels = Channels,
            FileSize = FileSize
        };
    }
}
