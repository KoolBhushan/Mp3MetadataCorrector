# MP3 Metadata Corrector & YouTube MP3 Downloader (WinUI 3 Desktop App)

A modern Windows 11 desktop application built with **WinUI 3 (Windows App SDK)** and **.NET 10** that allows you to:
1. **Download MP3s directly from YouTube, SoundCloud, or media URLs** (powered by integrated `yt-dlp` / `yt-dlg` and `ffmpeg`).
2. **Automatically clean file names and search online** for accurate metadata (iTunes & MusicBrainz).
3. **Embed ultra-crisp 1000x1000 album cover artwork** and ID3v2.3 tags directly into the MP3 file.
4. **Seamlessly update your local music library** (e.g. Navidrome at `D:\Music`).

---

## 🌟 Key Features

### 1. 📥 Integrated yt-dlg / YouTube to MP3 Downloader
- **URL Input**: Paste any YouTube, SoundCloud, or streaming audio link.
- **Save Destination**: Defaults to `D:\Music` (or pick any folder).
- **One-Click Audio Extraction**: Extracts the highest-quality audio stream directly to `.mp3` (320 kbps VBR/CBR quality) using bundled `ffmpeg`.
- **Integrated Tool Management**: Bundled with `yt-dlp.exe` and `ffmpeg.exe`, with an in-app "Install / Update yt-dlp" engine updater.
- **Auto-Queue & Auto-Tag**: Once downloaded, the `.mp3` immediately loads into the queue and automatically triggers metadata search!

### 2. 🔍 Smart Query Parsing & Metadata Search
- Automatically cleans noisy filenames (e.g., `01 - Queen - Bohemian Rhapsody (Official Video) [320kbps].mp3` ➔ Artist: `Queen`, Title: `Bohemian Rhapsody`).
- **iTunes Search API**: Returns verified track metadata and transforms artwork into high-resolution (1000x1000) images.
- **MusicBrainz API**: Open music database for comprehensive recording credits.

### 3. 🖼️ Side-by-Side Metadata & Cover Art Comparison
- **Original File Card**: Shows current tags, embedded art, bitrate, sample rate, channels, file size, and duration.
- **New Metadata Card**: Editable fields (Title, Artist, Album, Album Artist, Year, Genre, Track #/Total, Disc #, Comment) before saving.
- **Cover Art Controls**: High-res front cover preview, custom image picker (`Browse...`), and remove option.

### 4. 💾 Non-Destructive Safe Tag Writing
- Safe **ID3v2.3** (UTF-16 BOM) and **ID3v1** tagging.
- Leaves audio MPEG stream bit-for-bit intact.
- Writes to temp file first to prevent corruption.
- Optional automatic file renaming (`Artist - Title.mp3`).

---

## 🚀 How to Run

### Direct Executable (Pre-compiled Release)
```powershell
& "D:\Android Apps\MySpotify\Mp3MetadataCorrector\bin\Release\net10.0-windows10.0.26100.0\Mp3MetadataCorrector.exe"
```

### Or using the .NET CLI
```powershell
cd "D:\Android Apps\MySpotify\Mp3MetadataCorrector"
dotnet run -c Release
```

---

## 🛠️ Tooling & Architecture

- **`tools/` Directory**:
  - `yt-dlp.exe`: Latest official executable for audio stream fetching.
  - `ffmpeg.exe` & `ffprobe.exe`: Fast audio re-muxing and MP3 encoding.
- **`Services/YtDownloaderService.cs`**: Background worker process orchestrator with live percentage, speed, ETA, and target path detection.
- **`Services/Mp3TagService.cs`**: Zero-dependency ID3v2.3 & ID3v1 tagger and APIC image embedder.
- **`Services/MetadataSearchService.cs`**: iTunes and MusicBrainz search provider.
