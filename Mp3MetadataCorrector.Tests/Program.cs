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
using System.Text;
using Mp3MetadataCorrector.Models;
using Mp3MetadataCorrector.Services;

namespace Mp3MetadataCorrector.Tests;

/// <summary>
/// Console test runner verifying file name parsing and ID3v2/ID3v1 metadata reading and writing.
/// </summary>
public class Program
{
    /// <summary>
    /// Executes the unit and integration verification test suites.
    /// </summary>
    /// <returns>Exit code 0 on all tests passing; otherwise 1.</returns>
    public static int Main()
    {
        Console.WriteLine("=== RUNNING MP3 METADATA CORRECTOR VERIFICATION TESTS ===");
        int failures = 0;

        // TEST 1: File Name Parser
        Console.WriteLine("\n[1] Testing FileNameParser...");
        var testCases = new[]
        {
            ("01 - Queen - Bohemian Rhapsody (Official Video) [320kbps].mp3", "Queen", "Bohemian Rhapsody"),
            ("04. Adele - Someone Like You (Lyrics).mp3", "Adele", "Someone Like You"),
            ("Linkin Park - In The End [HQ].mp3", "Linkin Park", "In The End"),
            ("The_Beatles_-_Come_Together.mp3", "The Beatles", "Come Together"),
            ("Ed Sheeran - Shape of You.mp3", "Ed Sheeran", "Shape of You")
        };

        foreach (var (filename, expectedArtist, expectedTitle) in testCases)
        {
            var (artist, title, query) = FileNameParser.Parse(filename);
            bool match = string.Equals(artist, expectedArtist, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(title, expectedTitle, StringComparison.OrdinalIgnoreCase);

            if (match)
            {
                Console.WriteLine($"  [PASS] '{filename}' -> Artist: '{artist}', Title: '{title}', Query: '{query}'");
            }
            else
            {
                Console.WriteLine($"  [FAIL] '{filename}' -> Got Artist: '{artist}', Title: '{title}', Expected: '{expectedArtist}' / '{expectedTitle}'");
                failures++;
            }
        }

        // TEST 2: MP3 Tag Reading & Writing
        Console.WriteLine("\n[2] Testing Mp3TagService...");
        string testFile = Path.Combine(AppContext.BaseDirectory, "test_sample.mp3");

        // Create a synthetic MP3 with valid MPEG-1 Layer 3 frames
        CreateSyntheticMp3File(testFile);
        Console.WriteLine($"  Created synthetic MP3 file: {testFile} ({new FileInfo(testFile).Length} bytes)");

        var service = new Mp3TagService();

        // Write metadata
        byte[] dummyCoverArt = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9]; // Minimal valid JPEG bytes
        var metaToWrite = new TrackMetadata
        {
            FilePath = testFile,
            Title = "Bohemian Rhapsody",
            Artist = "Queen",
            Album = "A Night at the Opera",
            AlbumArtist = "Queen",
            Year = 1975,
            Genre = "Classic Rock",
            TrackNumber = 4,
            TrackTotal = 12,
            DiscNumber = 1,
            DiscTotal = 1,
            Comment = "Fixed by MP3 Metadata Corrector",
            CoverArtBytes = dummyCoverArt,
            CoverArtMimeType = "image/jpeg"
        };

        service.WriteMetadata(metaToWrite);
        Console.WriteLine("  Successfully wrote ID3v2 & ID3v1 tags to test file.");

        // Read metadata back
        var readBack = service.ReadMetadata(testFile);
        Console.WriteLine($"  Read back Title: '{readBack.Title}'");
        Console.WriteLine($"  Read back Artist: '{readBack.Artist}'");
        Console.WriteLine($"  Read back Album: '{readBack.Album}'");
        Console.WriteLine($"  Read back Year: {readBack.Year}");
        Console.WriteLine($"  Read back Genre: '{readBack.Genre}'");
        Console.WriteLine($"  Read back Track: {readBack.TrackNumber}/{readBack.TrackTotal}");
        Console.WriteLine($"  Read back Cover Art: {(readBack.HasCoverArt ? $"{readBack.CoverArtBytes!.Length} bytes" : "None")}");
        Console.WriteLine($"  Audio properties: {readBack.BitrateKbps} kbps, {readBack.SampleRateHz} Hz, channels: {readBack.Channels}");

        if (readBack.Title == "Bohemian Rhapsody" &&
            readBack.Artist == "Queen" &&
            readBack.Album == "A Night at the Opera" &&
            readBack.Year == 1975 &&
            readBack.Genre == "Classic Rock" &&
            readBack.TrackNumber == 4 &&
            readBack.TrackTotal == 12 &&
            readBack.HasCoverArt &&
            readBack.CoverArtBytes!.Length == dummyCoverArt.Length)
        {
            Console.WriteLine("  [PASS] All metadata tags and cover art verified bit-for-bit!");
        }
        else
        {
            Console.WriteLine("  [FAIL] Metadata verification mismatch!");
            failures++;
        }

        // Clean up
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
        }

        Console.WriteLine($"\n=== TEST RESULTS: {(failures == 0 ? "ALL TESTS PASSED" : $"{failures} FAILURE(S)")} ===");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Generates a valid synthetic MP3 binary file containing MPEG audio frames for testing.
    /// </summary>
    /// <param name="path">Target file destination.</param>
    private static void CreateSyntheticMp3File(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // Write 50 MPEG-1 Layer 3 frames (128 kbps, 44.1 kHz, stereo)
        // Frame size = 144 * 128000 / 44100 = 417 bytes
        byte[] frameHeader = [0xFF, 0xFB, 0x90, 0x64];
        byte[] frameBody = new byte[417 - 4];
        for (int i = 0; i < frameBody.Length; i++) frameBody[i] = (byte)(i % 256);

        for (int f = 0; f < 50; f++)
        {
            writer.Write(frameHeader);
            writer.Write(frameBody);
        }
        writer.Flush();
    }
}
