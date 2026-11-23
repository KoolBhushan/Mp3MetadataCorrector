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
using System.IO;

namespace Mp3MetadataCorrector.Models;

/// <summary>
/// Model representing the parsed metadata tags, embedded artwork, and audio properties of an MP3 file.
/// </summary>
public class TrackMetadata
{
    /// <summary>
    /// Gets or sets the absolute path to the MP3 file on disk.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets the file name and extension of the track.
    /// </summary>
    public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the parent directory path containing the MP3 file.
    /// </summary>
    public string DirectoryPath => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>
    /// Gets or sets the track title (TIT2).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lead performer or artist (TPE1).
    /// </summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album title (TALB).
    /// </summary>
    public string Album { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the band or album artist (TPE2).
    /// </summary>
    public string AlbumArtist { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release recording year (TYER / TDRC).
    /// </summary>
    public uint? Year { get; set; }

    /// <summary>
    /// Gets or sets the genre classification (TCON).
    /// </summary>
    public string Genre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the track position number (TRCK).
    /// </summary>
    public uint? TrackNumber { get; set; }

    /// <summary>
    /// Gets or sets the total track count on the album (TRCK).
    /// </summary>
    public uint? TrackTotal { get; set; }

    /// <summary>
    /// Gets or sets the disc position number (TPOS).
    /// </summary>
    public uint? DiscNumber { get; set; }

    /// <summary>
    /// Gets or sets the total disc count (TPOS).
    /// </summary>
    public uint? DiscTotal { get; set; }

    /// <summary>
    /// Gets or sets the unsynchronized lyric transcript (USLT).
    /// </summary>
    public string Lyrics { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user comment text (COMM).
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw embedded front cover artwork binary data (APIC frame).
    /// </summary>
    public byte[]? CoverArtBytes { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the embedded cover art (e.g. "image/jpeg", "image/png").
    /// </summary>
    public string? CoverArtMimeType { get; set; }

    /// <summary>
    /// Gets or sets the calculated audio playback duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the MPEG audio bitrate in kilobits per second (e.g. 320 kbps).
    /// </summary>
    public int BitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets the audio sampling rate in Hertz (e.g. 44100 Hz).
    /// </summary>
    public int SampleRateHz { get; set; }

    /// <summary>
    /// Gets or sets the number of audio channels (1 = Mono, 2 = Stereo).
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets a value indicating whether valid cover art binary data is embedded.
    /// </summary>
    public bool HasCoverArt => CoverArtBytes != null && CoverArtBytes.Length > 0;

    /// <summary>
    /// Gets a formatted duration string in mm:ss or hh:mm:ss format.
    /// </summary>
    public string FormattedDuration => Duration.TotalHours >= 1 
        ? Duration.ToString(@"hh\:mm\:ss") 
        : Duration.ToString(@"mm\:ss");

    /// <summary>
    /// Gets a human-readable file size string in MB or KB.
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            if (FileSize <= 0) return "0 KB";
            double mb = FileSize / (1024.0 * 1024.0);
            return mb >= 1.0 ? $"{mb:F2} MB" : $"{FileSize / 1024.0:F1} KB";
        }
    }

    /// <summary>
    /// Gets a formatted summary of the audio encoding quality (bitrate, sample rate, channels).
    /// </summary>
    public string AudioPropertiesSummary => 
        $"{BitrateKbps} kbps • {SampleRateHz / 1000.0:F1} kHz • {(Channels == 2 ? "Stereo" : Channels == 1 ? "Mono" : $"{Channels} ch")}";

    /// <summary>
    /// Gets a formatted track index string (e.g. "4/12" or "4").
    /// </summary>
    public string TrackString => (TrackNumber.HasValue, TrackTotal.HasValue) switch
    {
        (true, true) => $"{TrackNumber}/{TrackTotal}",
        (true, false) => $"{TrackNumber}",
        _ => string.Empty
    };

    /// <summary>
    /// Creates a deep copy clone of the current metadata object.
    /// </summary>
    /// <returns>A new <see cref="TrackMetadata"/> instance with identical values.</returns>
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
