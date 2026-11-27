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
using System.Reflection;
using System.Threading.Tasks;

namespace Mp3MetadataCorrector.Cli;

/// <summary>
/// Main CLI entry point for AI agents, terminal scripts, and automated batch workflows.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes the requested CLI command and returns an exit code.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v" or "version")
        {
            string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            Console.WriteLine($"mp3corrector v{ver}");
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        string[] cmdArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        try
        {
            return command switch
            {
                "read" or "info" => CliCommands.HandleRead(cmdArgs),
                "search" or "find" => await CliCommands.HandleSearchAsync(cmdArgs),
                "parse" => CliCommands.HandleParse(cmdArgs),
                "write" or "set" => await CliCommands.HandleWriteAsync(cmdArgs),
                "auto-fix" or "fix" => await CliCommands.HandleAutoFixAsync(cmdArgs),
                "download" or "get" => await CliCommands.HandleDownloadAsync(cmdArgs),
                "schema" => CliCommands.HandleSchema(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[FATAL] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Unknown command: '{command}'. Run 'mp3corrector --help' for available commands.");
        Console.ResetColor();
        return 1;
    }

    private static void PrintHelp()
    {
        string ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
 __  __           ____  __  __      _            _       _          
|  \/  |_ __ ____|___ \|  \/  | ___| |_ __ _  __| | __ _| |_ __ _   
| |\/| | '_ \_  /  __) | |\/| |/ _ \ __/ _` |/ _` |/ _` | __/ _` |  
| |  | | |_) / /  / __/| |  | |  __/ || (_| | (_| | (_| | || (_| |  
|_|  |_| .__/___|_____||_|  |_|\___|\__\__,_|\__,_|\__,_|\__\__,_|  
       |_|   MP3 Metadata Corrector CLI (AI Agent Edition)          
");
        Console.ResetColor();
        Console.WriteLine($"Version: {ver}\n");
        Console.WriteLine("Usage: mp3corrector <command> [arguments] [options]\n");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("COMMANDS:");
        Console.ResetColor();
        Console.WriteLine("  read <file|glob>           Read and display metadata & audio properties.");
        Console.WriteLine("  write <file> [options]     Write/update ID3v2/ID3v1 tags and cover artwork.");
        Console.WriteLine("  search <query>             Search online metadata (iTunes & MusicBrainz).");
        Console.WriteLine("  auto-fix <file|dir>        Auto match, fetch high-res artwork, and update tags.");
        Console.WriteLine("  download <url>             Download audio from YouTube/Web via yt-dlp to MP3.");
        Console.WriteLine("  parse <filename>           Parse dirty filename into clean artist and title.");
        Console.WriteLine("  schema                     Output full machine-readable JSON tool schema.");
        Console.WriteLine("  version                    Display CLI version.");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nGLOBAL OPTIONS:");
        Console.ResetColor();
        Console.WriteLine("  --json                     Output structured JSON for AI agent integration.");
        Console.WriteLine("  --help, -h                 Show this help screen.");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nWRITE OPTIONS:");
        Console.ResetColor();
        Console.WriteLine("  --title <text>             Set track title.");
        Console.WriteLine("  --artist <text>            Set artist name.");
        Console.WriteLine("  --album <text>             Set album title.");
        Console.WriteLine("  --album-artist <text>      Set album artist.");
        Console.WriteLine("  --year <num>               Set release year.");
        Console.WriteLine("  --genre <text>             Set music genre.");
        Console.WriteLine("  --track <num>              Set track number.");
        Console.WriteLine("  --track-total <num>        Set total tracks in album.");
        Console.WriteLine("  --disc <num>               Set disc number.");
        Console.WriteLine("  --lyrics <text>            Set lyrics.");
        Console.WriteLine("  --comment <text>           Set comment.");
        Console.WriteLine("  --cover <file|url>         Embed local image or download image from URL.");
        Console.WriteLine("  --remove-cover             Remove embedded cover artwork.");
        Console.WriteLine("  --rename                   Rename file to 'Artist - Title.mp3'.");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nAUTO-FIX OPTIONS:");
        Console.ResetColor();
        Console.WriteLine("  --provider <all|itunes|mb> Choose online provider (default: All).");
        Console.WriteLine("  --no-art                   Skip artwork download.");
        Console.WriteLine("  --rename                   Rename files to 'Artist - Title.mp3'.");
        Console.WriteLine("  --dry-run                  Simulate changes without saving to disk.");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nEXAMPLES FOR AI AGENTS & SCRIPTS:");
        Console.ResetColor();
        Console.WriteLine("  # Introspect tool schema");
        Console.WriteLine("  mp3corrector schema\n");
        Console.WriteLine("  # Read file metadata as JSON");
        Console.WriteLine("  mp3corrector read \"track.mp3\" --json\n");
        Console.WriteLine("  # Parse dirty filename");
        Console.WriteLine("  mp3corrector parse \"01 - Queen - Bohemian Rhapsody [320kbps].mp3\" --json\n");
        Console.WriteLine("  # Search iTunes & MusicBrainz");
        Console.WriteLine("  mp3corrector search \"Queen Bohemian Rhapsody\" --json\n");
        Console.WriteLine("  # Update tags and embed artwork from URL");
        Console.WriteLine("  mp3corrector write \"song.mp3\" --artist \"Queen\" --title \"Bohemian Rhapsody\" --cover \"https://example.com/cover.jpg\" --json\n");
        Console.WriteLine("  # Batch auto-fix an entire folder of MP3s");
        Console.WriteLine("  mp3corrector auto-fix \"D:\\Music\\Unsorted\" --rename --json\n");
        Console.WriteLine("  # Download YouTube URL to MP3 with automatic tagging");
        Console.WriteLine("  mp3corrector download \"https://youtu.be/...\" --output-dir \"D:\\Music\" --auto-fix --rename --json");
    }
}
