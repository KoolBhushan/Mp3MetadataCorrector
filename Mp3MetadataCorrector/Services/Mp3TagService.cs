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
using System.Text;
using Mp3MetadataCorrector.Models;

namespace Mp3MetadataCorrector.Services;

/// <summary>
/// Service responsible for reading and writing ID3v2.3, ID3v2.4, and ID3v1 metadata tags, embedded APIC artwork, and MPEG audio header properties.
/// </summary>
public class Mp3TagService
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// Reads ID3 metadata tags, embedded front cover art, and audio encoding specs from an MP3 file.
    /// </summary>
    /// <param name="filePath">The absolute path to the MP3 file.</param>
    /// <returns>A populated <see cref="TrackMetadata"/> instance.</returns>
    public TrackMetadata ReadMetadata(string filePath)
    {
        Logger.Info($"[Mp3TagService] Reading metadata from: '{filePath}'");
        var meta = new TrackMetadata
        {
            FilePath = filePath
        };

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                Logger.Warn($"[Mp3TagService] File does not exist: '{filePath}'");
                return meta;
            }

            meta.FileSize = fileInfo.Length;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            long id3v2Size = 0;
            bool hasId3v2 = ReadId3v2(reader, meta, out id3v2Size);

            // Fallback to ID3v1 if fields are missing
            if (string.IsNullOrWhiteSpace(meta.Title) || string.IsNullOrWhiteSpace(meta.Artist))
            {
                ReadId3v1(reader, meta);
            }

            // Parse MPEG Audio frames for Bitrate, Duration, SampleRate
            ParseAudioProperties(reader, meta, id3v2Size);

            // If title is still missing, fallback to filename
            if (string.IsNullOrWhiteSpace(meta.Title))
            {
                var parsed = FileNameParser.Parse(filePath);
                if (!string.IsNullOrWhiteSpace(parsed.Title))
                    meta.Title = parsed.Title;
                if (!string.IsNullOrWhiteSpace(parsed.Artist) && string.IsNullOrWhiteSpace(meta.Artist))
                    meta.Artist = parsed.Artist;
            }

            Logger.Info($"[Mp3TagService] Read complete. Title: '{meta.Title}', Artist: '{meta.Artist}', Album: '{meta.Album}', HasArt: {meta.HasCoverArt}");
            return meta;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Mp3TagService] Error reading metadata from: '{filePath}'", ex);
            throw;
        }
    }

    /// <summary>
    /// Reads and decodes ID3v2.2, ID3v2.3, and ID3v2.4 headers and frames.
    /// </summary>
    private static bool ReadId3v2(BinaryReader reader, TrackMetadata meta, out long totalHeaderSize)
    {
        totalHeaderSize = 0;
        if (reader.BaseStream.Length < 10)
            return false;

        reader.BaseStream.Position = 0;
        byte[] headerBytes = reader.ReadBytes(10);
        if (headerBytes.Length < 10 ||
            headerBytes[0] != 'I' || headerBytes[1] != 'D' || headerBytes[2] != '3')
        {
            return false;
        }

        byte majorVersion = headerBytes[3];
        byte flags = headerBytes[5];
        bool hasExtendedHeader = (flags & 0x40) != 0;
        bool hasFooter = (flags & 0x10) != 0;

        int tagDataSize = DecodeSynchsafe(headerBytes, 6);
        totalHeaderSize = 10 + tagDataSize + (hasFooter ? 10 : 0);

        if (tagDataSize <= 0 || totalHeaderSize > reader.BaseStream.Length)
            return false;

        byte[] tagData = reader.ReadBytes(tagDataSize);
        int offset = 0;

        // Skip extended header if present
        if (hasExtendedHeader && offset + 4 <= tagData.Length)
        {
            int extSize = majorVersion == 4
                ? DecodeSynchsafe(tagData, offset)
                : ReadBigEndianInt32(tagData, offset);
            offset += extSize;
        }

        while (offset + 10 <= tagData.Length)
        {
            // Frame ID
            string frameId = Latin1.GetString(tagData, offset, 4);
            if (string.IsNullOrWhiteSpace(frameId) || tagData[offset] == 0)
                break; // Padding reached

            int frameSize = majorVersion == 4
                ? DecodeSynchsafe(tagData, offset + 4)
                : ReadBigEndianInt32(tagData, offset + 4);

            offset += 10;
            if (frameSize <= 0 || offset + frameSize > tagData.Length)
                break;

            byte[] framePayload = new byte[frameSize];
            Array.Copy(tagData, offset, framePayload, 0, frameSize);
            offset += frameSize;

            ParseFrame(frameId, framePayload, meta);
        }

        return true;
    }

    /// <summary>
    /// Parses an individual ID3 frame according to its 4-character identifier.
    /// </summary>
    private static void ParseFrame(string frameId, byte[] payload, TrackMetadata meta)
    {
        if (payload.Length == 0) return;

        switch (frameId)
        {
            case "TIT2": // Title
                meta.Title = DecodeTextFrame(payload);
                break;
            case "TPE1": // Artist
                meta.Artist = DecodeTextFrame(payload);
                break;
            case "TALB": // Album
                meta.Album = DecodeTextFrame(payload);
                break;
            case "TPE2": // Album Artist
                meta.AlbumArtist = DecodeTextFrame(payload);
                break;
            case "TYER": // Year v2.3
            case "TDRC": // Recording Date v2.4
                string yearStr = DecodeTextFrame(payload);
                if (yearStr.Length >= 4 && uint.TryParse(yearStr[..4], out uint yearVal))
                    meta.Year = yearVal;
                break;
            case "TCON": // Genre
                meta.Genre = CleanGenre(DecodeTextFrame(payload));
                break;
            case "TRCK": // Track Number (e.g. "5" or "5/12")
                string trackStr = DecodeTextFrame(payload);
                ParseTrackNumber(trackStr, out uint? trkNum, out uint? trkTot);
                meta.TrackNumber = trkNum;
                meta.TrackTotal = trkTot;
                break;
            case "TPOS": // Disc Number
                string discStr = DecodeTextFrame(payload);
                ParseTrackNumber(discStr, out uint? discNum, out uint? discTot);
                meta.DiscNumber = discNum;
                meta.DiscTotal = discTot;
                break;
            case "COMM": // Comment
                meta.Comment = DecodeCommentOrLyrics(payload);
                break;
            case "USLT": // Lyrics
                meta.Lyrics = DecodeCommentOrLyrics(payload);
                break;
            case "APIC": // Attached Picture
                ParseApicFrame(payload, meta);
                break;
        }
    }

    /// <summary>
    /// Parses an APIC attached picture frame to extract cover artwork binary data.
    /// </summary>
    private static void ParseApicFrame(byte[] payload, TrackMetadata meta)
    {
        if (payload.Length < 4) return;

        byte encoding = payload[0];
        int idx = 1;

        // Read MIME type (Latin1 null-terminated)
        int mimeEnd = Array.IndexOf(payload, (byte)0, idx);
        if (mimeEnd < 0) return;

        string mime = Latin1.GetString(payload, idx, mimeEnd - idx);
        idx = mimeEnd + 1;

        if (idx >= payload.Length) return;
        byte pictureType = payload[idx++]; // 0x03 is front cover

        // Description is null-terminated according to encoding
        idx = SkipNullTerminator(payload, idx, encoding);
        if (idx >= payload.Length) return;

        int imageLength = payload.Length - idx;
        if (imageLength > 0)
        {
            byte[] imageBytes = new byte[imageLength];
            Array.Copy(payload, idx, imageBytes, 0, imageLength);
            meta.CoverArtBytes = imageBytes;
            meta.CoverArtMimeType = string.IsNullOrWhiteSpace(mime) ? "image/jpeg" : mime;
        }
    }

    /// <summary>
    /// Skips null-terminators based on text encoding.
    /// </summary>
    private static int SkipNullTerminator(byte[] data, int start, byte encoding)
    {
        if (encoding == 1 || encoding == 2) // UTF-16
        {
            for (int i = start; i < data.Length - 1; i += 2)
            {
                if (data[i] == 0 && data[i + 1] == 0)
                    return i + 2;
            }
        }
        else // Latin1 or UTF-8
        {
            for (int i = start; i < data.Length; i++)
            {
                if (data[i] == 0)
                    return i + 1;
            }
        }
        return data.Length;
    }

    /// <summary>
    /// Decodes raw text frame payload bytes respecting encoding prefixes and trimming BOM characters.
    /// </summary>
    private static string DecodeTextFrame(byte[] payload)
    {
        if (payload.Length < 1) return string.Empty;
        byte encodingByte = payload[0];
        int dataLen = payload.Length - 1;
        if (dataLen <= 0) return string.Empty;

        Encoding enc = encodingByte switch
        {
            1 => Encoding.Unicode,
            2 => Encoding.BigEndianUnicode,
            3 => Encoding.UTF8,
            _ => Latin1
        };

        string text = enc.GetString(payload, 1, dataLen);
        return text.Trim('\0', ' ', '\r', '\n', '\uFEFF');
    }

    /// <summary>
    /// Decodes COMM or USLT frame payloads containing language codes and descriptions.
    /// </summary>
    private static string DecodeCommentOrLyrics(byte[] payload)
    {
        if (payload.Length < 5) return string.Empty;
        byte encodingByte = payload[0];
        // 3 bytes language
        int idx = 4;
        idx = SkipNullTerminator(payload, idx, encodingByte);
        if (idx >= payload.Length) return string.Empty;

        Encoding enc = encodingByte switch
        {
            1 => Encoding.Unicode,
            2 => Encoding.BigEndianUnicode,
            3 => Encoding.UTF8,
            _ => Latin1
        };

        return enc.GetString(payload, idx, payload.Length - idx).Trim('\0', ' ', '\r', '\n', '\uFEFF');
    }

    /// <summary>
    /// Parses track number strings in formats like "4" or "4/12".
    /// </summary>
    private static void ParseTrackNumber(string str, out uint? num, out uint? total)
    {
        num = null;
        total = null;
        if (string.IsNullOrWhiteSpace(str)) return;

        int slash = str.IndexOf('/');
        if (slash >= 0)
        {
            if (uint.TryParse(str[..slash].Trim(), out uint n)) num = n;
            if (uint.TryParse(str[(slash + 1)..].Trim(), out uint t)) total = t;
        }
        else if (uint.TryParse(str.Trim(), out uint n))
        {
            num = n;
        }
    }

    /// <summary>
    /// Cleans ID3v1 numeric genre notations like "(17)Rock".
    /// </summary>
    private static string CleanGenre(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (raw.StartsWith('(') && raw.Contains(')'))
        {
            int close = raw.IndexOf(')');
            string code = raw[1..close];
            if (int.TryParse(code, out int genreIndex) && genreIndex >= 0 && genreIndex < Id3v1Genres.Length)
            {
                string rest = raw[(close + 1)..].Trim();
                return string.IsNullOrWhiteSpace(rest) ? Id3v1Genres[genreIndex] : rest;
            }
        }
        return raw;
    }

    /// <summary>
    /// Reads legacy ID3v1 tags from the last 128 bytes of an MP3 file.
    /// </summary>
    private static void ReadId3v1(BinaryReader reader, TrackMetadata meta)
    {
        if (reader.BaseStream.Length < 128) return;

        reader.BaseStream.Position = reader.BaseStream.Length - 128;
        byte[] v1 = reader.ReadBytes(128);
        if (v1.Length < 128 || v1[0] != 'T' || v1[1] != 'A' || v1[2] != 'G')
            return;

        if (string.IsNullOrWhiteSpace(meta.Title))
            meta.Title = Latin1.GetString(v1, 3, 30).Trim('\0', ' ');

        if (string.IsNullOrWhiteSpace(meta.Artist))
            meta.Artist = Latin1.GetString(v1, 33, 30).Trim('\0', ' ');

        if (string.IsNullOrWhiteSpace(meta.Album))
            meta.Album = Latin1.GetString(v1, 63, 30).Trim('\0', ' ');

        if (!meta.Year.HasValue)
        {
            string yr = Latin1.GetString(v1, 93, 4).Trim('\0', ' ');
            if (uint.TryParse(yr, out uint y)) meta.Year = y;
        }

        if (v1[125] == 0 && v1[126] != 0 && !meta.TrackNumber.HasValue)
        {
            meta.TrackNumber = v1[126];
        }

        if (string.IsNullOrWhiteSpace(meta.Genre))
        {
            byte genreCode = v1[127];
            if (genreCode < Id3v1Genres.Length)
                meta.Genre = Id3v1Genres[genreCode];
        }
    }

    /// <summary>
    /// Parses MPEG-1 Layer 3 frame sync headers to calculate bitrate, sample rate, channels, and duration.
    /// </summary>
    private static void ParseAudioProperties(BinaryReader reader, TrackMetadata meta, long id3v2Size)
    {
        try
        {
            long fileLength = reader.BaseStream.Length;
            long searchLimit = Math.Min(fileLength, id3v2Size + 65536);
            reader.BaseStream.Position = id3v2Size;

            int syncByte1 = -1;
            while (reader.BaseStream.Position < searchLimit)
            {
                syncByte1 = reader.ReadByte();
                if (syncByte1 == 0xFF)
                {
                    int syncByte2 = reader.ReadByte();
                    if ((syncByte2 & 0xE0) == 0xE0) // 11 sync bits
                    {
                        int mpegVer = (syncByte2 >> 3) & 0x03; // 3 = MPEG1, 2 = MPEG2, 0 = MPEG2.5
                        int layer = (syncByte2 >> 1) & 0x03;   // 1 = Layer III (MP3)

                        if (mpegVer == 3 && layer == 1) // MPEG 1 Layer 3
                        {
                            int byte3 = reader.ReadByte();
                            int byte4 = reader.ReadByte();

                            int bitrateIdx = (byte3 >> 4) & 0x0F;
                            int sampleRateIdx = (byte3 >> 2) & 0x03;
                            int channelMode = (byte4 >> 6) & 0x03;

                            int bitrate = GetMpeg1Layer3Bitrate(bitrateIdx);
                            int sampleRate = GetMpeg1SampleRate(sampleRateIdx);
                            int channels = channelMode == 3 ? 1 : 2;

                            meta.BitrateKbps = bitrate;
                            meta.SampleRateHz = sampleRate;
                            meta.Channels = channels;

                            long audioBytes = fileLength - id3v2Size - (fileLength >= 128 ? 128 : 0);
                            if (bitrate > 0 && audioBytes > 0)
                            {
                                double seconds = (audioBytes * 8.0) / (bitrate * 1000.0);
                                meta.Duration = TimeSpan.FromSeconds(seconds);
                            }
                            return;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore audio property parsing errors
        }
    }

    /// <summary>
    /// Safely updates an MP3 file with new ID3v2.3 tags, APIC artwork, and ID3v1 fallback, preserving audio bitstream.
    /// </summary>
    /// <param name="meta">The metadata values to write.</param>
    /// <param name="newFilePath">Optional new target file path if renaming.</param>
    public void WriteMetadata(TrackMetadata meta, string? newFilePath = null)
    {
        string targetPath = string.IsNullOrEmpty(newFilePath) ? meta.FilePath : newFilePath;
        if (string.IsNullOrEmpty(targetPath))
            throw new ArgumentException("Target file path cannot be empty.");

        Logger.Info($"[Mp3TagService] Writing metadata to: '{targetPath}' (source: '{meta.FilePath}')");
        try
        {
            // Build new ID3v2.3 tag buffer
            byte[] newId3v2 = BuildId3v2Tag(meta);

            // Build new ID3v1 tag buffer
            byte[] newId3v1 = BuildId3v1Tag(meta);

            // Write safely to a temp file, then atomic move
            string tempFile = targetPath + ".tmp_" + Guid.NewGuid().ToString("N")[..8];
            try
            {
                using (var fs = new FileStream(meta.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs))
                using (var outFs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    long startPos = 0;
                    if (fs.Length >= 10)
                    {
                        byte[] head = reader.ReadBytes(10);
                        if (head[0] == 'I' && head[1] == 'D' && head[2] == '3')
                        {
                            int tagSize = DecodeSynchsafe(head, 6);
                            bool hasFooter = (head[5] & 0x10) != 0;
                            startPos = 10 + tagSize + (hasFooter ? 10 : 0);
                        }
                    }

                    // Strip ID3v1 at the tail if present
                    long endPos = fs.Length;
                    if (fs.Length >= 128)
                    {
                        fs.Position = fs.Length - 128;
                        byte[] v1Head = reader.ReadBytes(3);
                        if (v1Head[0] == 'T' && v1Head[1] == 'A' && v1Head[2] == 'G')
                        {
                            endPos = fs.Length - 128;
                        }
                    }

                    long audioLen = Math.Max(0, endPos - startPos);

                    // 1. Write new ID3v2 tag
                    outFs.Write(newId3v2, 0, newId3v2.Length);

                    // 2. Stream audio stream in 64 KB chunks without buffering full file into RAM
                    fs.Position = startPos;
                    byte[] buffer = new byte[65536];
                    long remaining = audioLen;
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        int read = fs.Read(buffer, 0, toRead);
                        if (read <= 0) break;
                        outFs.Write(buffer, 0, read);
                        remaining -= read;
                    }

                    // 3. Write new ID3v1 tag
                    outFs.Write(newId3v1, 0, newId3v1.Length);
                    outFs.Flush();
                }

                if (File.Exists(targetPath) && !string.Equals(meta.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(targetPath);
                }

                File.Move(tempFile, targetPath, overwrite: true);

                if (!string.Equals(meta.FilePath, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(meta.FilePath))
                {
                    File.Delete(meta.FilePath);
                }

                meta.FilePath = targetPath;
                meta.FileSize = new FileInfo(targetPath).Length;
                Logger.Info($"[Mp3TagService] Successfully wrote tags and artwork to '{targetPath}'. Final size: {meta.FileSize} bytes.");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Mp3TagService] Failed to write metadata to: '{targetPath}'", ex);
            throw;
        }
    }

    /// <summary>
    /// Builds an ID3v2.3 byte array with UTF-16 BOM text frames and APIC artwork frame.
    /// </summary>
    private static byte[] BuildId3v2Tag(TrackMetadata meta)
    {
        using var ms = new MemoryStream();

        // Write frames
        WriteTextFrame(ms, "TIT2", meta.Title);
        WriteTextFrame(ms, "TPE1", meta.Artist);
        WriteTextFrame(ms, "TALB", meta.Album);
        WriteTextFrame(ms, "TPE2", meta.AlbumArtist);
        if (meta.Year.HasValue)
            WriteTextFrame(ms, "TYER", meta.Year.Value.ToString());
        WriteTextFrame(ms, "TCON", meta.Genre);
        if (meta.TrackNumber.HasValue)
        {
            string trk = meta.TrackTotal.HasValue ? $"{meta.TrackNumber}/{meta.TrackTotal}" : $"{meta.TrackNumber}";
            WriteTextFrame(ms, "TRCK", trk);
        }
        if (meta.DiscNumber.HasValue)
        {
            string dsc = meta.DiscTotal.HasValue ? $"{meta.DiscNumber}/{meta.DiscTotal}" : $"{meta.DiscNumber}";
            WriteTextFrame(ms, "TPOS", dsc);
        }
        if (!string.IsNullOrWhiteSpace(meta.Comment))
            WriteCommentFrame(ms, "COMM", meta.Comment);
        if (!string.IsNullOrWhiteSpace(meta.Lyrics))
            WriteCommentFrame(ms, "USLT", meta.Lyrics);

        // Write Cover Art APIC frame
        if (meta.CoverArtBytes != null && meta.CoverArtBytes.Length > 0)
        {
            WriteApicFrame(ms, meta.CoverArtBytes, meta.CoverArtMimeType ?? "image/jpeg");
        }

        byte[] frameData = ms.ToArray();
        int paddingSize = 2048;
        int totalTagDataSize = frameData.Length + paddingSize;

        byte[] header = new byte[10];
        header[0] = (byte)'I';
        header[1] = (byte)'D';
        header[2] = (byte)'3';
        header[3] = 3; // ID3v2.3
        header[4] = 0;
        header[5] = 0; // Flags
        EncodeSynchsafe(totalTagDataSize, header, 6);

        byte[] fullTag = new byte[10 + totalTagDataSize];
        Array.Copy(header, 0, fullTag, 0, 10);
        Array.Copy(frameData, 0, fullTag, 10, frameData.Length);
        return fullTag;
    }

    /// <summary>
    /// Writes a UTF-16 encoded text frame with BOM.
    /// </summary>
    private static void WriteTextFrame(Stream stream, string frameId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        byte[] textBytes = Encoding.Unicode.GetBytes(text);
        byte[] payload = new byte[1 + 2 + textBytes.Length];
        payload[0] = 1; // UTF-16 with BOM
        payload[1] = 0xFF; // BOM LE
        payload[2] = 0xFE;
        Array.Copy(textBytes, 0, payload, 3, textBytes.Length);

        WriteFrame(stream, frameId, payload);
    }

    /// <summary>
    /// Writes a comment (COMM) or lyrics (USLT) frame.
    /// </summary>
    private static void WriteCommentFrame(Stream stream, string frameId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        byte[] textBytes = Encoding.Unicode.GetBytes(text);
        byte[] payload = new byte[1 + 3 + 2 + 2 + textBytes.Length];
        payload[0] = 1; // UTF-16
        payload[1] = (byte)'e';
        payload[2] = (byte)'n';
        payload[3] = (byte)'g';
        payload[4] = 0;
        payload[5] = 0;
        payload[6] = 0xFF; // BOM
        payload[7] = 0xFE;
        Array.Copy(textBytes, 0, payload, 8, textBytes.Length);

        WriteFrame(stream, frameId, payload);
    }

    /// <summary>
    /// Writes an attached picture (APIC) front cover frame.
    /// </summary>
    private static void WriteApicFrame(Stream stream, byte[] imageBytes, string mimeType)
    {
        byte[] mimeBytes = Latin1.GetBytes(mimeType);
        byte[] payload = new byte[1 + mimeBytes.Length + 1 + 1 + 1 + imageBytes.Length];
        int idx = 0;
        payload[idx++] = 0; // ISO-8859-1 for mime & desc
        Array.Copy(mimeBytes, 0, payload, idx, mimeBytes.Length);
        idx += mimeBytes.Length;
        payload[idx++] = 0;
        payload[idx++] = 0x03; // Front cover
        payload[idx++] = 0;
        Array.Copy(imageBytes, 0, payload, idx, imageBytes.Length);

        WriteFrame(stream, "APIC", payload);
    }

    /// <summary>
    /// Writes frame ID, size header, and payload into stream.
    /// </summary>
    private static void WriteFrame(Stream stream, string frameId, byte[] payload)
    {
        byte[] idBytes = Latin1.GetBytes(frameId);
        stream.Write(idBytes, 0, 4);

        byte[] sizeBytes = new byte[4];
        WriteBigEndianInt32(payload.Length, sizeBytes, 0);
        stream.Write(sizeBytes, 0, 4);

        stream.WriteByte(0);
        stream.WriteByte(0);

        stream.Write(payload, 0, payload.Length);
    }

    /// <summary>
    /// Builds a 128-byte legacy ID3v1 tag.
    /// </summary>
    private static byte[] BuildId3v1Tag(TrackMetadata meta)
    {
        byte[] tag = new byte[128];
        tag[0] = (byte)'T';
        tag[1] = (byte)'A';
        tag[2] = (byte)'G';

        WriteFixedAscii(tag, 3, 30, meta.Title);
        WriteFixedAscii(tag, 33, 30, meta.Artist);
        WriteFixedAscii(tag, 63, 30, meta.Album);
        if (meta.Year.HasValue)
            WriteFixedAscii(tag, 93, 4, meta.Year.Value.ToString());
        WriteFixedAscii(tag, 97, 28, meta.Comment);

        tag[125] = 0;
        tag[126] = (byte)(meta.TrackNumber ?? 0);
        tag[127] = 255;

        return tag;
    }

    private static void WriteFixedAscii(byte[] dest, int offset, int length, string? val)
    {
        if (string.IsNullOrEmpty(val)) return;
        byte[] ascii = Latin1.GetBytes(val);
        int copyLen = Math.Min(ascii.Length, length);
        Array.Copy(ascii, 0, dest, offset, copyLen);
    }

    private static int DecodeSynchsafe(byte[] bytes, int offset) =>
        ((bytes[offset] & 0x7F) << 21) |
        ((bytes[offset + 1] & 0x7F) << 14) |
        ((bytes[offset + 2] & 0x7F) << 7) |
        (bytes[offset + 3] & 0x7F);

    private static void EncodeSynchsafe(int value, byte[] bytes, int offset)
    {
        bytes[offset] = (byte)((value >> 21) & 0x7F);
        bytes[offset + 1] = (byte)((value >> 14) & 0x7F);
        bytes[offset + 2] = (byte)((value >> 7) & 0x7F);
        bytes[offset + 3] = (byte)(value & 0x7F);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

    private static void WriteBigEndianInt32(int value, byte[] bytes, int offset)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }

    private static int GetMpeg1Layer3Bitrate(int index) => index switch
    {
        1 => 32, 2 => 40, 3 => 48, 4 => 56, 5 => 64, 6 => 80, 7 => 96,
        8 => 112, 9 => 128, 10 => 160, 11 => 192, 12 => 224, 13 => 256, 14 => 320,
        _ => 0
    };

    private static int GetMpeg1SampleRate(int index) => index switch
    {
        0 => 44100,
        1 => 48000,
        2 => 32000,
        _ => 44100
    };

    private static readonly string[] Id3v1Genres = [
        "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop",
        "Jazz", "Metal", "New Age", "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock",
        "Techno", "Industrial", "Alternative", "Ska", "Death Metal", "Pranks", "Soundtrack",
        "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk", "Fusion", "Trance",
        "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise",
        "AlternRock", "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop",
        "Instrumental Rock", "Ethnic", "Gothic", "Darkwave", "Techno-Industrial", "Electronic",
        "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy", "Cult", "Gangsta", "Top 40",
        "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave",
        "Psychadelic", "Rave", "Showtunes", "Trailer", "Lo-Fi", "Tribal", "Acid Punk",
        "Acid Jazz", "Polka", "Retro", "Musical", "Rock & Roll", "Hard Rock"
    ];
}
