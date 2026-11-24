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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Mp3MetadataCorrector.Models;
using Mp3MetadataCorrector.Services;
using Mp3MetadataCorrector.ViewModels;
using WinRT.Interop;

namespace Mp3MetadataCorrector;

/// <summary>
/// Main application window providing the user interface for MP3 metadata correction and downloading.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Gets the primary ViewModel instance managing the UI state and operations.
    /// </summary>
    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        Logger.Info("Initializing MainWindow components and ViewModel...");
        ViewModel = new MainViewModel();
        this.InitializeComponent();

        ConfigureWindow();
    }

    /// <summary>
    /// Configures the window dimensions, title, icon, and initial center screen placement.
    /// </summary>
    private void ConfigureWindow()
    {
        try
        {
            Title = "MP3 Metadata Corrector";

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                // Set window dimensions and center on screen
                appWindow.Resize(new Windows.Graphics.SizeInt32(1220, 850));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centeredPosition = appWindow.Position;
                    centeredPosition.X = ((displayArea.WorkArea.Width - 1220) / 2);
                    centeredPosition.Y = ((displayArea.WorkArea.Height - 850) / 2);
                    appWindow.Move(centeredPosition);
                }

                // Apply Application Icon
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (!File.Exists(iconPath))
                {
                    // Fallback to dev project structure if running from bin
                    string devIcon = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\Assets\app.ico"));
                    if (File.Exists(devIcon))
                    {
                        iconPath = devIcon;
                    }
                }

                if (File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                    Logger.Info($"AppWindow icon successfully set from: {iconPath}");
                }
                else
                {
                    Logger.Warn($"App icon file not found at expected path: {iconPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error configuring window appearance or icon", ex);
        }
    }

    /// <summary>
    /// Opens the application log file for inspection.
    /// </summary>
    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        Logger.Info("User clicked 'Logs' button; opening active log file.");
        Logger.OpenLogFile();
    }

    /// <summary>
    /// Handles clicking the Download MP3 button to toggle the downloader panel.
    /// </summary>
    private void OnToggleDownloaderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Toggling downloader panel visibility.");
            ViewModel.ToggleDownloader();
        }
        catch (Exception ex)
        {
            Logger.Error("Error toggling downloader", ex);
        }
    }

    /// <summary>
    /// Triggers download execution when the Enter key is pressed in the URL input box.
    /// </summary>
    private async void OnDownloadUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            try
            {
                Logger.Info($"User pressed Enter in download URL box. URL: {ViewModel.DownloadUrl}");
                await ViewModel.DownloadMp3Async();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnDownloadUrlKeyDown", ex);
            }
        }
    }

    /// <summary>
    /// Displays a folder picker dialog to select the destination folder for downloaded MP3s.
    /// </summary>
    private async void OnPickDownloadFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Opening FolderPicker for download directory...");
            var folderPicker = new FolderPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(folderPicker, hWnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.DownloadFolder = folder.Path;
                Logger.Info($"Selected download destination folder: {folder.Path}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error picking download folder", ex);
        }
    }

    /// <summary>
    /// Displays a file picker dialog allowing the user to select one or more MP3 files.
    /// </summary>
    private async void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Opening FileOpenPicker for MP3 selection...");
            var picker = new FileOpenPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add(".mp3");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                Logger.Info($"User selected {files.Count} file(s) from picker.");
                var paths = new string[files.Count];
                for (int i = 0; i < files.Count; i++)
                {
                    paths[i] = files[i].Path;
                }
                await ViewModel.AddFilesAsync(paths);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error during file open picker operation", ex);
        }
    }

    /// <summary>
    /// Displays a folder picker dialog to scan and import all MP3 files within a chosen folder.
    /// </summary>
    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Opening FolderPicker for batch MP3 import...");
            var folderPicker = new FolderPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(folderPicker, hWnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                string folderPath = folder.Path;
                Logger.Info($"User selected folder: {folderPath}");
                if (Directory.Exists(folderPath))
                {
                    string[] mp3Files = Directory.GetFiles(folderPath, "*.mp3", SearchOption.TopDirectoryOnly);
                    Logger.Info($"Discovered {mp3Files.Length} .mp3 file(s) in '{folderPath}'.");
                    if (mp3Files.Length > 0)
                    {
                        await ViewModel.AddFilesAsync(mp3Files);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error during folder open picker operation", ex);
        }
    }

    /// <summary>
    /// Clears the currently loaded file queue.
    /// </summary>
    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Clearing file queue.");
            ViewModel.Files.Clear();
            ViewModel.SelectedFile = null;
        }
        catch (Exception ex)
        {
            Logger.Error("Error clearing file list", ex);
        }
    }

    /// <summary>
    /// Displays an image file picker to select custom artwork to embed in the MP3.
    /// </summary>
    private async void OnChangeCoverArtClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Opening FileOpenPicker for custom cover artwork...");
            var picker = new FileOpenPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                Logger.Info($"Selected artwork image: {file.Path} ({file.FileType})");
                byte[] bytes = File.ReadAllBytes(file.Path);
                string mime = file.FileType.ToLowerInvariant() switch
                {
                    ".png" => "image/png",
                    _ => "image/jpeg"
                };
                await ViewModel.SetCustomCoverArtAsync(bytes, mime);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error changing cover art via file picker", ex);
        }
    }

    /// <summary>
    /// Removes the current candidate cover artwork.
    /// </summary>
    private void OnRemoveCoverArtClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Info("Removing current candidate cover art.");
            ViewModel.RemoveCoverArt();
        }
        catch (Exception ex)
        {
            Logger.Error("Error removing cover art", ex);
        }
    }

    /// <summary>
    /// Applies the selected online search match to the editable metadata fields.
    /// </summary>
    private async void OnApplyResultClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: SearchResult result })
            {
                Logger.Info($"User clicked apply on search result: '{result.Title}' by '{result.Artist}' [{result.Source}].");
                await ViewModel.ApplySearchResultAsync(result);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in OnApplyResultClick", ex);
        }
    }

    /// <summary>
    /// Initiates online metadata search when Enter is pressed in the search textbox.
    /// </summary>
    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            try
            {
                Logger.Info($"User pressed Enter in search query box: '{ViewModel.SearchQuery}'.");
                await ViewModel.SearchAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnSearchBoxKeyDown", ex);
            }
        }
    }

    /// <summary>
    /// Applies the selected online search match to the editable metadata fields.
    /// </summary>
    private async void OnSearchResultItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is SearchResult result)
            {
                Logger.Info($"User clicked search result: '{result.Title}' by '{result.Artist}' [{result.Source}].");
                await ViewModel.ApplySearchResultAsync(result);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error applying clicked search result", ex);
        }
    }

    /// <summary>
    /// Handles drag over event to indicate copy acceptance for dropped files.
    /// </summary>
    private void OnGridDragOver(object sender, DragEventArgs e)
    {
        try
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop .mp3 files here";
            e.DragUIOverride.IsContentVisible = true;
        }
        catch (Exception ex)
        {
            Logger.Error("Error in OnGridDragOver", ex);
        }
    }

    /// <summary>
    /// Handles dropping MP3 files or folders onto the window to automatically add them to the queue.
    /// </summary>
    private async void OnGridDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = new List<string>();

                foreach (var item in items)
                {
                    if (item is StorageFile file && file.FileType.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                    {
                        paths.Add(file.Path);
                    }
                    else if (item is StorageFolder folder)
                    {
                        try
                        {
                            string[] folderMp3s = Directory.GetFiles(folder.Path, "*.mp3", SearchOption.TopDirectoryOnly);
                            paths.AddRange(folderMp3s);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Failed to scan dropped folder '{folder.Path}'", ex);
                        }
                    }
                }

                if (paths.Count > 0)
                {
                    Logger.Info($"Processing {paths.Count} dropped file path(s).");
                    await ViewModel.AddFilesAsync(paths);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in OnGridDrop", ex);
        }
    }
}
