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
using System.Text.RegularExpressions;

namespace Mp3MetadataCorrector.Services;

/// <summary>
/// Provides heuristic parsing and sanitization routines to extract artist, title, and search queries from noisy audio filenames.
/// </summary>
public static class FileNameParser
{
    private static readonly Regex TrackNumberPrefixRegex = new(
        @"^(\d{1,3}[\s.\-_]+)+",
        RegexOptions.Compiled);

    private static readonly Regex JunkSuffixRegex = new(
        @"(?i)\s*[\(\[]\s*(official\s*(music\s*)?video|video|lyrics|audio|visualizer|hq|hd|1080p|720p|4k|320kbps|128kbps|remaster(ed)?(\s*\d{4})?|live(\s*at\s*[^\]\)]+)?|bonus\s*track|explicit|clean|original\s*mix)\s*[\)\]]",
        RegexOptions.Compiled);

    private static readonly Regex QualityJunkRegex = new(
        @"(?i)\[\s*(320|256|192|128)\s*k(bps)?\s*\]|\(\s*(320|256|192|128)\s*k(bps)?\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex WebsiteTagsRegex = new(
        @"(?i)(www\.[a-z0-9\-]+\.[a-z]+|\[[a-z0-9\-]+\.[a-z]+\])",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a filename or file path, stripping common prefixes, suffixes, and noise tags to extract the artist, title, and clean query.
    /// </summary>
    /// <param name="filePathOrName">The raw file path or file name.</param>
    /// <returns>A tuple containing the extracted Artist, Title, and CleanQuery strings.</returns>
    public static (string Artist, string Title, string CleanQuery) Parse(string filePathOrName)
    {
        string raw = Path.GetFileNameWithoutExtension(filePathOrName);
        if (string.IsNullOrWhiteSpace(raw))
            return (string.Empty, string.Empty, string.Empty);

        // Replace underscores with spaces if there are no hyphens
        string cleaned = raw.Replace('_', ' ');

        // Strip website tags like www.example.com
        cleaned = WebsiteTagsRegex.Replace(cleaned, string.Empty);

        // Strip known noise suffixes repeatedly
        for (int i = 0; i < 3; i++)
        {
            cleaned = JunkSuffixRegex.Replace(cleaned, string.Empty);
            cleaned = QualityJunkRegex.Replace(cleaned, string.Empty);
        }

        // Strip leading track numbers like "01 - " or "02. " or "03 "
        cleaned = TrackNumberPrefixRegex.Replace(cleaned, string.Empty).Trim();

        // Normalize spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        // Check if there is an "Artist - Title" separator
        string[] separators = [" - ", " – ", " — "];
        foreach (var sep in separators)
        {
            int sepIndex = cleaned.IndexOf(sep, StringComparison.Ordinal);
            if (sepIndex > 0 && sepIndex + sep.Length < cleaned.Length)
            {
                string artistPart = cleaned[..sepIndex].Trim();
                string titlePart = cleaned[(sepIndex + sep.Length)..].Trim();

                // Further clean title part of lingering parentheses or brackets at end
                titlePart = Regex.Replace(titlePart, @"\s*[\(\[][^\)\]]*\b(video|lyrics|audio|remaster)\b[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase).Trim();

                string query = $"{artistPart} {titlePart}".Trim();
                return (artistPart, titlePart, query);
            }
        }

        // If no separator, the whole string is likely the title or query
        return (string.Empty, cleaned, cleaned);
    }
}
