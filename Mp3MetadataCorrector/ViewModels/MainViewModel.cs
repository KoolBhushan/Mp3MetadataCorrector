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

public partial class MainViewModel : ObservableObject
{
    private readonly Mp3TagService _tagService;
    private readonly MetadataSearchService _searchService;
    private readonly YtDownloaderService _downloaderService;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _downloadCts;

    public ObservableCollection<TrackMetadata> Files { get; } = [];
    public ObservableCollection<SearchResult> SearchResults { get; } = [];

    [ObservableProperty]
    public partial TrackMetadata? SelectedFile { get; set; }

    [ObservableProperty]
    public partial SearchResult? SelectedSearchResult { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedProvider { get; set; } = "All";

    public List<string> AvailableProviders { get; } = ["All", "iTunes", "MusicBrainz"];

    // Editable candidate metadata
    [ObservableProperty]
    public partial string EditTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditArtist { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditAlbum { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditAlbumArtist { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditYear { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditGenre { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditTrackNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditTrackTotal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditDiscNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditLyrics { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditComment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial byte[]? EditCoverArtBytes { get; set; }

    [ObservableProperty]
    public partial string? EditCoverArtMimeType { get; set; }

    [ObservableProperty]
    public partial BitmapImage? CurrentCoverArtBitmap { get; set; }

    [ObservableProperty]
    public partial BitmapImage? EditCoverArtBitmap { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial bool RenameFileOnSave { get; set; } = false;

    // YT-DLG / Downloader properties
    [ObservableProperty]
    public partial bool IsDownloaderVisible { get; set; } = false;

    [ObservableProperty]
    public partial string DownloadUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DownloadFolder { get; set; } = @"D:\Music";

    [ObservableProperty]
    public partial bool IsDownloading { get; set; } = false;

    [ObservableProperty]
    public partial double DownloadProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial string DownloadStatusText { get; set; } = "Enter a YouTube / SoundCloud / music video URL";

    [ObservableProperty]
    public partial bool IsInstallingYtDlp { get; set; } = false;

    [ObservableProperty]
    public partial string YtDlpStatusText { get; set; } = "yt-dlp: Checking...";

    [ObservableProperty]
    public partial string FfmpegStatusText { get; set; } = "ffmpeg: Checking...";

    [ObservableProperty]
    public partial bool IsYtDlpReady { get; set; } = false;

    [ObservableProperty]
    public partial bool IsFfmpegReady { get; set; } = false;

    public bool HasSelectedFile => SelectedFile != null;
    public bool HasSearchResults => SearchResults.Count > 0;

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

    public void RefreshEngineStatus()
    {
        IsYtDlpReady = _downloaderService.IsYtDlpAvailable;
        IsFfmpegReady = _downloaderService.IsFfmpegAvailable;

        YtDlpStatusText = IsYtDlpReady ? "yt-dlp: Ready" : "yt-dlp: Not installed";
        FfmpegStatusText = IsFfmpegReady ? "ffmpeg: Ready" : "ffmpeg: Not found";
    }

    [RelayCommand]
    public void ToggleDownloader()
    {
        IsDownloaderVisible = !IsDownloaderVisible;
        if (IsDownloaderVisible)
        {
            RefreshEngineStatus();
        }
    }

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
            await _downloaderService.InstallOrUpdateYtDlpAsync(progress);
            RefreshEngineStatus();
            DownloadStatusText = "yt-dlp installed and ready!";
            ShowStatusNotification("yt-dlp installed successfully!", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"Installation error: {ex.Message}";
            ShowStatusNotification($"Failed to install yt-dlp: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsInstallingYtDlp = false;
        }
    }

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

        try
        {
            string mp3Path = await Task.Run(() =>
                _downloaderService.DownloadAudioAsMp3Async(DownloadUrl.Trim(), DownloadFolder, progress, ct), ct);

            DownloadProgressValue = 100;
            DownloadStatusText = $"Saved: {Path.GetFileName(mp3Path)}";
            ShowStatusNotification($"Downloaded '{Path.GetFileName(mp3Path)}' successfully! Now matching metadata...", InfoBarSeverity.Success);

            // Automatically load and select the newly downloaded MP3!
            await AddFileAsync(mp3Path);
            DownloadUrl = string.Empty;
        }
        catch (OperationCanceledException)
        {
            DownloadStatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"Download error: {ex.Message}";
            ShowStatusNotification($"Download error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsDownloading = false;
        }
    }

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

    public async Task AddFileAsync(string filePath)
    {
        if (!File.Exists(filePath) || !filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return;

        // Check if already in list
        foreach (var item in Files)
        {
            if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                SelectedFile = item;
                return;
            }
        }

        IsLoading = true;
        try
        {
            var meta = await Task.Run(() => _tagService.ReadMetadata(filePath));
            Files.Add(meta);
            SelectedFile = meta;
            ShowStatusNotification($"Loaded '{meta.FileName}' successfully.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatusNotification($"Failed to read MP3 metadata: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
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
            }
        }
        catch (Exception ex)
        {
            ShowStatusNotification($"Error loading files: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

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

    [RelayCommand]
    public async Task SaveMetadataAsync()
    {
        if (SelectedFile == null || string.IsNullOrWhiteSpace(SelectedFile.FilePath))
        {
            ShowStatusNotification("No file selected to save.", InfoBarSeverity.Warning);
            return;
        }

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
        }
        catch (Exception ex)
        {
            ShowStatusNotification($"Failed to update MP3: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public void RemoveCoverArt()
    {
        EditCoverArtBytes = null;
        EditCoverArtMimeType = null;
        EditCoverArtBitmap = null;
        ShowStatusNotification("Cover art removed. Click 'Save & Update MP3' to apply.", InfoBarSeverity.Informational);
    }

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
