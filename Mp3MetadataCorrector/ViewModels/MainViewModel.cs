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
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Mp3MetadataCorrector.Helpers;
using Mp3MetadataCorrector.Models;
using Mp3MetadataCorrector.Services;

namespace Mp3MetadataCorrector.ViewModels;

/// <summary>
/// Primary ViewModel for the application coordinating file selection, tag reading/writing,
/// online metadata search queries, and audio downloading via yt-dlp.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly Mp3TagService _tagService;
    private readonly MetadataSearchService _searchService;
    private readonly YtDownloaderService _downloaderService;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _downloadCts;

    /// <summary>
    /// Gets the collection of loaded MP3 files.
    /// </summary>
    public ObservableCollection<TrackMetadata> Files { get; } = [];

    /// <summary>
    /// Gets the collection of search results returned from online metadata queries.
    /// </summary>
    public ObservableCollection<SearchResult> SearchResults { get; } = [];

    /// <summary>
    /// The currently selected MP3 file in the library list.
    /// </summary>
    [ObservableProperty]
    public partial TrackMetadata? SelectedFile { get; set; }

    /// <summary>
    /// The currently selected search result item in the search results list.
    /// </summary>
    [ObservableProperty]
    public partial SearchResult? SelectedSearchResult { get; set; }

    /// <summary>
    /// The search query text entered by the user or auto-formulated from file analysis.
    /// </summary>
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// The selected metadata provider filter ("All", "iTunes", or "MusicBrainz").
    /// </summary>
    [ObservableProperty]
    public partial string SelectedProvider { get; set; } = "All";

    /// <summary>
    /// Gets the list of available metadata search providers.
    /// </summary>
    public List<string> AvailableProviders { get; } = ["All", "iTunes", "MusicBrainz"];

    // Editable candidate metadata

    /// <summary>
    /// The edited track title to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditTitle { get; set; } = string.Empty;

    /// <summary>
    /// The edited artist name to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditArtist { get; set; } = string.Empty;

    /// <summary>
    /// The edited album name to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditAlbum { get; set; } = string.Empty;

    /// <summary>
    /// The edited album artist name to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditAlbumArtist { get; set; } = string.Empty;

    /// <summary>
    /// The edited release year string to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditYear { get; set; } = string.Empty;

    /// <summary>
    /// The edited music genre to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditGenre { get; set; } = string.Empty;

    /// <summary>
    /// The edited track index number string to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditTrackNumber { get; set; } = string.Empty;

    /// <summary>
    /// The edited total track count string to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditTrackTotal { get; set; } = string.Empty;

    /// <summary>
    /// The edited disc number string to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditDiscNumber { get; set; } = string.Empty;

    /// <summary>
    /// The edited unsynchronized song lyrics to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditLyrics { get; set; } = string.Empty;

    /// <summary>
    /// The edited user comment string to be saved.
    /// </summary>
    [ObservableProperty]
    public partial string EditComment { get; set; } = string.Empty;

    /// <summary>
    /// Raw byte array of the new cover artwork to be written to the file.
    /// </summary>
    [ObservableProperty]
    public partial byte[]? EditCoverArtBytes { get; set; }

    /// <summary>
    /// MIME type of the cover artwork (e.g., image/jpeg or image/png).
    /// </summary>
    [ObservableProperty]
    public partial string? EditCoverArtMimeType { get; set; }

    /// <summary>
    /// XAML BitmapImage representing the current cover artwork of the selected file.
    /// </summary>
    [ObservableProperty]
    public partial BitmapImage? CurrentCoverArtBitmap { get; set; }

    /// <summary>
    /// XAML BitmapImage representing the candidate/edited cover artwork.
    /// </summary>
    [ObservableProperty]
    public partial BitmapImage? EditCoverArtBitmap { get; set; }

    /// <summary>
    /// Indicates whether a metadata search query is currently executing.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    /// <summary>
    /// Indicates whether metadata is currently being saved to disk.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    /// <summary>
    /// Indicates whether audio files are currently being loaded and parsed.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>
    /// Text message displayed in the application's status InfoBar notification.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the application's status InfoBar notification.
    /// </summary>
    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    /// <summary>
    /// Controls whether the status InfoBar notification is open/visible.
    /// </summary>
    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    /// <summary>
    /// Indicates whether the physical file should be automatically renamed to "Artist - Title.mp3" when saved.
    /// </summary>
    [ObservableProperty]
    public partial bool RenameFileOnSave { get; set; } = false;

    // YT-DLG / Downloader properties

    /// <summary>
    /// Indicates whether the built-in media downloader panel is expanded and visible.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDownloaderVisible { get; set; } = false;

    /// <summary>
    /// Media URL to download from (e.g., YouTube, SoundCloud).
    /// </summary>
    [ObservableProperty]
    public partial string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Destination folder path where downloaded MP3 files will be stored.
    /// </summary>
    [ObservableProperty]
    public partial string DownloadFolder { get; set; } = @"D:\Music";

    /// <summary>
    /// Indicates whether a media download and conversion is currently active.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDownloading { get; set; } = false;

    /// <summary>
    /// Numerical progress (0-100) of the active media download.
    /// </summary>
    [ObservableProperty]
    public partial double DownloadProgressValue { get; set; } = 0;

    /// <summary>
    /// Status text showing real-time speed, ETA, or conversion progress of the downloader.
    /// </summary>
    [ObservableProperty]
    public partial string DownloadStatusText { get; set; } = "Enter a YouTube / SoundCloud / music video URL";

    /// <summary>
    /// Indicates whether yt-dlp is currently being downloaded or updated from GitHub.
    /// </summary>
    [ObservableProperty]
    public partial bool IsInstallingYtDlp { get; set; } = false;

    /// <summary>
    /// Status description of the yt-dlp executable availability.
    /// </summary>
    [ObservableProperty]
    public partial string YtDlpStatusText { get; set; } = "yt-dlp: Checking...";

    /// <summary>
    /// Status description of the ffmpeg executable availability.
    /// </summary>
    [ObservableProperty]
    public partial string FfmpegStatusText { get; set; } = "ffmpeg: Checking...";

    /// <summary>
    /// Indicates whether yt-dlp is present and ready for execution.
    /// </summary>
    [ObservableProperty]
    public partial bool IsYtDlpReady { get; set; } = false;

    /// <summary>
    /// Indicates whether ffmpeg is present and ready for audio conversion.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFfmpegReady { get; set; } = false;

    /// <summary>
    /// Gets a value indicating whether a file is currently selected.
    /// </summary>
    public bool HasSelectedFile => SelectedFile != null;

    /// <summary>
    /// Gets a value indicating whether online search results are currently available.
    /// </summary>
    public bool HasSearchResults => SearchResults.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        _tagService = new Mp3TagService();
        _searchService = new MetadataSearchService();
        _downloaderService = new YtDownloaderService();

        // Default folder check: prefer D:\Music if exists
        if (!Directory.Exists(DownloadFolder))
        {
            DownloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        }

        RefreshEngineStatus();
    }

    /// <summary>
    /// Refreshes the detection status of yt-dlp and ffmpeg binaries.
    /// </summary>
    public void RefreshEngineStatus()
    {
        IsYtDlpReady = _downloaderService.IsYtDlpAvailable;
        IsFfmpegReady = _downloaderService.IsFfmpegAvailable;

        YtDlpStatusText = IsYtDlpReady ? "yt-dlp: Ready" : "yt-dlp: Not installed";
        FfmpegStatusText = IsFfmpegReady ? "ffmpeg: Ready" : "ffmpeg: Not found";
    }

    /// <summary>
    /// Toggles the visibility of the media downloader panel.
    /// </summary>
    [RelayCommand]
    public void ToggleDownloader()
    {
        IsDownloaderVisible = !IsDownloaderVisible;
        if (IsDownloaderVisible)
        {
            RefreshEngineStatus();
        }
    }

    /// <summary>
    /// Downloads and installs or updates the latest yt-dlp executable.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task InstallYtDlpAsync()
    {
        IsInstallingYtDlp = true;
        DownloadStatusText = "Installing latest yt-dlp.exe from official release...";
        var progress = new Progress<double>(pct =>
        {
            DownloadProgressValue = pct;
            DownloadStatusText = $"Downloading yt-dlp: {pct:F0}%";
        });

        try
        {
            Logger.Info("[MainViewModel] Installing or updating yt-dlp...");
            await _downloaderService.InstallOrUpdateYtDlpAsync(progress);
            RefreshEngineStatus();
            DownloadStatusText = "yt-dlp installed and ready!";
            ShowStatusNotification("yt-dlp installed successfully!", InfoBarSeverity.Success);
            Logger.Info("[MainViewModel] yt-dlp installation succeeded.");
        }
        catch (Exception ex)
        {
            Logger.Error("[MainViewModel] yt-dlp installation failed", ex);
            DownloadStatusText = $"Installation error: {ex.Message}";
            ShowStatusNotification($"Failed to install yt-dlp: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsInstallingYtDlp = false;
        }
    }

    /// <summary>
    /// Downloads audio from the specified URL and automatically imports the resulting MP3 into the editor.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task DownloadMp3Async()
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl))
        {
            ShowStatusNotification("Please enter a YouTube or audio URL.", InfoBarSeverity.Warning);
            return;
        }

        if (!IsYtDlpReady)
        {
            ShowStatusNotification("yt-dlp engine is not installed. Click 'Install yt-dlp' first.", InfoBarSeverity.Warning);
            return;
        }

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        IsDownloading = true;
        DownloadProgressValue = 0;
        DownloadStatusText = "Starting download...";

        var progress = new Progress<DownloadProgressReport>(report =>
        {
            if (report.Percentage > 0)
                DownloadProgressValue = report.Percentage;
            if (!string.IsNullOrEmpty(report.Status))
                DownloadStatusText = report.Status;
        });

        Logger.Info($"[MainViewModel] Starting download from URL: '{DownloadUrl}' to folder: '{DownloadFolder}'");
        try
        {
            string mp3Path = await Task.Run(() =>
                _downloaderService.DownloadAudioAsMp3Async(DownloadUrl.Trim(), DownloadFolder, progress, ct), ct);

            DownloadProgressValue = 100;
            DownloadStatusText = $"Saved: {Path.GetFileName(mp3Path)}";
            ShowStatusNotification($"Downloaded '{Path.GetFileName(mp3Path)}' successfully! Now matching metadata...", InfoBarSeverity.Success);
            Logger.Info($"[MainViewModel] Download completed successfully: '{mp3Path}'. Loading into editor.");

            // Automatically load and select the newly downloaded MP3!
            await AddFileAsync(mp3Path);
            DownloadUrl = string.Empty;
        }
        catch (OperationCanceledException)
        {
            Logger.Info("[MainViewModel] Download operation cancelled by user or system.");
            DownloadStatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error("[MainViewModel] Error during MP3 download and conversion", ex);
            DownloadStatusText = $"Download error: {ex.Message}";
            ShowStatusNotification($"Download error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>
    /// Handles updates when a new track is selected in the file list.
    /// </summary>
    /// <param name="value">The selected <see cref="TrackMetadata"/> instance.</param>
    partial void OnSelectedFileChanged(TrackMetadata? value)
    {
        OnPropertyChanged(nameof(HasSelectedFile));
        if (value == null)
        {
            ResetEditFields();
            return;
        }

        // Populate edit fields from selected file
        EditTitle = value.Title;
        EditArtist = value.Artist;
        EditAlbum = value.Album;
        EditAlbumArtist = value.AlbumArtist;
        EditYear = value.Year?.ToString() ?? string.Empty;
        EditGenre = value.Genre;
        EditTrackNumber = value.TrackNumber?.ToString() ?? string.Empty;
        EditTrackTotal = value.TrackTotal?.ToString() ?? string.Empty;
        EditDiscNumber = value.DiscNumber?.ToString() ?? string.Empty;
        EditLyrics = value.Lyrics;
        EditComment = value.Comment;
        EditCoverArtBytes = value.CoverArtBytes != null ? (byte[])value.CoverArtBytes.Clone() : null;
        EditCoverArtMimeType = value.CoverArtMimeType;

        // Load images
        _ = UpdateCurrentCoverArtImageAsync(value.CoverArtBytes);
        _ = UpdateEditCoverArtImageAsync(EditCoverArtBytes);

        // Auto-formulate smart search query
        string query = string.Empty;
        if (!string.IsNullOrWhiteSpace(value.Artist) && !string.IsNullOrWhiteSpace(value.Title))
        {
            query = $"{value.Artist} - {value.Title}";
        }
        else if (!string.IsNullOrWhiteSpace(value.Title))
        {
            query = value.Title;
        }
        else
        {
            var parsed = FileNameParser.Parse(value.FilePath);
            query = !string.IsNullOrWhiteSpace(parsed.CleanQuery) ? parsed.CleanQuery : value.FileName;
        }

        SearchQuery = query;

        // Auto-trigger search if query is valid
        if (!string.IsNullOrWhiteSpace(query))
        {
            _ = SearchAsync();
        }
    }

    /// <summary>
    /// Clears all editable form fields and cover images.
    /// </summary>
    private void ResetEditFields()
    {
        EditTitle = string.Empty;
        EditArtist = string.Empty;
        EditAlbum = string.Empty;
        EditAlbumArtist = string.Empty;
        EditYear = string.Empty;
        EditGenre = string.Empty;
        EditTrackNumber = string.Empty;
        EditTrackTotal = string.Empty;
        EditDiscNumber = string.Empty;
        EditLyrics = string.Empty;
        EditComment = string.Empty;
        EditCoverArtBytes = null;
        EditCoverArtMimeType = null;
        CurrentCoverArtBitmap = null;
        EditCoverArtBitmap = null;
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));
    }

    /// <summary>
    /// Loads a single MP3 file into the collection and selects it.
    /// </summary>
    /// <param name="filePath">Absolute path of the MP3 file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddFileAsync(string filePath)
    {
        if (!File.Exists(filePath) || !filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn($"[MainViewModel] AddFileAsync skipped: path does not exist or is not an .mp3: '{filePath}'");
            return;
        }

        // Check if already in list
        foreach (var item in Files)
        {
            if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info($"[MainViewModel] File already in queue, selecting it: '{filePath}'");
                SelectedFile = item;
                return;
            }
        }

        Logger.Info($"[MainViewModel] Loading MP3 file: '{filePath}'");
        IsLoading = true;
        try
        {
            var meta = await Task.Run(() => _tagService.ReadMetadata(filePath));
            Files.Add(meta);
            SelectedFile = meta;
            ShowStatusNotification($"Loaded '{meta.FileName}' successfully.", InfoBarSeverity.Success);
            Logger.Info($"[MainViewModel] Successfully loaded '{meta.FileName}'. Total files: {Files.Count}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[MainViewModel] Failed to read MP3 metadata: '{filePath}'", ex);
            ShowStatusNotification($"Failed to read MP3 metadata: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads multiple MP3 files into the collection.
    /// </summary>
    /// <param name="filePaths">Collection of MP3 file paths.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        Logger.Info("[MainViewModel] Batch loading files into queue...");
        IsLoading = true;
        TrackMetadata? first = null;
        int count = 0;

        try
        {
            foreach (var path in filePaths)
            {
                if (!File.Exists(path) || !path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool exists = false;
                foreach (var f in Files)
                {
                    if (string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists) continue;

                var meta = await Task.Run(() => _tagService.ReadMetadata(path));
                Files.Add(meta);
                count++;
                first ??= meta;
            }

            if (first != null)
            {
                SelectedFile = first;
                ShowStatusNotification($"Loaded {count} MP3 file(s).", InfoBarSeverity.Success);
                Logger.Info($"[MainViewModel] Batch load complete. Added {count} new file(s). Total queue size: {Files.Count}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[MainViewModel] Error during batch file import", ex);
            ShowStatusNotification($"Error loading files: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an online search query across configured providers using the current search term.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        SearchResults.Clear();
        OnPropertyChanged(nameof(HasSearchResults));

        try
        {
            var results = await _searchService.SearchAsync(SearchQuery, SelectedProvider, ct);
            if (!ct.IsCancellationRequested)
            {
                foreach (var r in results)
                {
                    SearchResults.Add(r);
                }
                OnPropertyChanged(nameof(HasSearchResults));

                if (results.Count > 0)
                {
                    ShowStatusNotification($"Found {results.Count} match(es) for '{SearchQuery}'.", InfoBarSeverity.Informational);
                }
                else
                {
                    ShowStatusNotification($"No results found for '{SearchQuery}'. Try adjusting your search term.", InfoBarSeverity.Warning);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowStatusNotification($"Search error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Applies metadata and fetches high-resolution cover art from the chosen search result.
    /// </summary>
    /// <param name="result">The selected search result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ApplySearchResultAsync(SearchResult? result)
    {
        if (result == null) return;

        EditTitle = result.Title;
        EditArtist = result.Artist;
        EditAlbum = result.Album;
        EditAlbumArtist = !string.IsNullOrWhiteSpace(result.AlbumArtist) ? result.AlbumArtist : result.Artist;
        EditYear = result.Year?.ToString() ?? string.Empty;
        EditGenre = result.Genre;
        EditTrackNumber = result.TrackNumber?.ToString() ?? string.Empty;
        EditTrackTotal = result.TrackTotal?.ToString() ?? string.Empty;
        EditDiscNumber = result.DiscNumber?.ToString() ?? string.Empty;

        // Download high-resolution cover art
        string? artUrl = result.HighResArtworkUrl ?? result.ArtworkUrl;
        if (!string.IsNullOrWhiteSpace(artUrl))
        {
            ShowStatusNotification("Downloading high-res artwork...", InfoBarSeverity.Informational);
            var downloaded = await _searchService.DownloadArtworkAsync(artUrl);
            if (downloaded.HasValue)
            {
                EditCoverArtBytes = downloaded.Value.Data;
                EditCoverArtMimeType = downloaded.Value.MimeType;
                await UpdateEditCoverArtImageAsync(EditCoverArtBytes);
                ShowStatusNotification($"Applied metadata and cover art from {result.Source}!", InfoBarSeverity.Success);
                return;
            }
        }

        ShowStatusNotification($"Applied metadata from {result.Source}.", InfoBarSeverity.Success);
    }

    /// <summary>
    /// Writes the edited metadata tags and cover artwork to the selected MP3 file.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveMetadataAsync()
    {
        if (SelectedFile == null || string.IsNullOrWhiteSpace(SelectedFile.FilePath))
        {
            ShowStatusNotification("No file selected to save.", InfoBarSeverity.Warning);
            return;
        }

        Logger.Info($"[MainViewModel] SaveMetadataAsync triggered for: '{SelectedFile.FilePath}'. RenameOnSave={RenameFileOnSave}");
        IsSaving = true;
        try
        {
            var updated = SelectedFile.Clone();
            updated.Title = EditTitle.Trim();
            updated.Artist = EditArtist.Trim();
            updated.Album = EditAlbum.Trim();
            updated.AlbumArtist = EditAlbumArtist.Trim();
            updated.Genre = EditGenre.Trim();
            updated.Year = uint.TryParse(EditYear, out uint y) ? y : null;
            updated.TrackNumber = uint.TryParse(EditTrackNumber, out uint tn) ? tn : null;
            updated.TrackTotal = uint.TryParse(EditTrackTotal, out uint tt) ? tt : null;
            updated.DiscNumber = uint.TryParse(EditDiscNumber, out uint dn) ? dn : null;
            updated.Lyrics = EditLyrics.Trim();
            updated.Comment = EditComment.Trim();
            updated.CoverArtBytes = EditCoverArtBytes;
            updated.CoverArtMimeType = EditCoverArtMimeType;

            string? newFilePath = null;
            if (RenameFileOnSave && !string.IsNullOrWhiteSpace(updated.Artist) && !string.IsNullOrWhiteSpace(updated.Title))
            {
                string safeArtist = CleanPathString(updated.Artist);
                string safeTitle = CleanPathString(updated.Title);
                string dir = Path.GetDirectoryName(SelectedFile.FilePath) ?? "";
                string newName = $"{safeArtist} - {safeTitle}.mp3";
                newFilePath = Path.Combine(dir, newName);
                Logger.Info($"[MainViewModel] Rename active. Target filename: '{newName}'");
            }

            await Task.Run(() => _tagService.WriteMetadata(updated, newFilePath));

            // Reload metadata from disk to verify
            var reloaded = await Task.Run(() => _tagService.ReadMetadata(updated.FilePath));

            // Update in collection
            int idx = Files.IndexOf(SelectedFile);
            if (idx >= 0)
            {
                Files[idx] = reloaded;
            }
            SelectedFile = reloaded;

            ShowStatusNotification($"Successfully updated '{reloaded.FileName}'!", InfoBarSeverity.Success);
            Logger.Info($"[MainViewModel] Successfully updated metadata for '{reloaded.FileName}'.");
        }
        catch (Exception ex)
        {
            Logger.Error($"[MainViewModel] Error saving metadata for '{SelectedFile.FilePath}'", ex);
            ShowStatusNotification($"Failed to update MP3: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Removes the cover artwork from the current edit buffer.
    /// </summary>
    [RelayCommand]
    public void RemoveCoverArt()
    {
        EditCoverArtBytes = null;
        EditCoverArtMimeType = null;
        EditCoverArtBitmap = null;
        ShowStatusNotification("Cover art removed. Click 'Save & Update MP3' to apply.", InfoBarSeverity.Informational);
    }

    /// <summary>
    /// Sets custom cover art from an image file picked by the user.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes.</param>
    /// <param name="mimeType">MIME type of the image.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomCoverArtAsync(byte[] imageBytes, string mimeType)
    {
        EditCoverArtBytes = imageBytes;
        EditCoverArtMimeType = mimeType;
        await UpdateEditCoverArtImageAsync(imageBytes);
        ShowStatusNotification("Custom cover art loaded. Click 'Save & Update MP3' to apply.", InfoBarSeverity.Informational);
    }

    private async Task UpdateCurrentCoverArtImageAsync(byte[]? bytes)
    {
        CurrentCoverArtBitmap = await ImageHelper.ByteArrayToBitmapImageAsync(bytes);
    }

    private async Task UpdateEditCoverArtImageAsync(byte[]? bytes)
    {
        EditCoverArtBitmap = await ImageHelper.ByteArrayToBitmapImageAsync(bytes);
    }

    private static string CleanPathString(string input)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input.Trim();
    }

    private void ShowStatusNotification(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }
}
