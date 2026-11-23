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
        ViewModel = new MainViewModel();
        this.InitializeComponent();

        ConfigureWindow();
    }

    /// <summary>
    /// Configures the window dimensions, title, and initial center screen placement.
    /// </summary>
    private void ConfigureWindow()
    {
        Title = "MP3 Metadata Corrector";

        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1220, 850));

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centeredPosition = appWindow.Position;
                centeredPosition.X = ((displayArea.WorkArea.Width - 1220) / 2);
                centeredPosition.Y = ((displayArea.WorkArea.Height - 850) / 2);
                appWindow.Move(centeredPosition);
            }
        }
    }

    /// <summary>
    /// Handles clicking the Download MP3 button to toggle the downloader panel.
    /// </summary>
    private void OnToggleDownloaderClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleDownloader();
    }

    /// <summary>
    /// Triggers download execution when the Enter key is pressed in the URL input box.
    /// </summary>
    private async void OnDownloadUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.DownloadMp3Async();
        }
    }

    /// <summary>
    /// Displays a folder picker dialog to select the destination folder for downloaded MP3s.
    /// </summary>
    private async void OnPickDownloadFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new FolderPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(folderPicker, hWnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.DownloadFolder = folder.Path;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pick download folder error: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays a file picker dialog allowing the user to select one or more MP3 files.
    /// </summary>
    private async void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add(".mp3");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
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
            System.Diagnostics.Debug.WriteLine($"File pick error: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays a folder picker dialog to scan and import all MP3 files within a chosen folder.
    /// </summary>
    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new FolderPicker();
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(folderPicker, hWnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                string folderPath = folder.Path;
                if (Directory.Exists(folderPath))
                {
                    string[] mp3Files = Directory.GetFiles(folderPath, "*.mp3", SearchOption.TopDirectoryOnly);
                    if (mp3Files.Length > 0)
                    {
                        await ViewModel.AddFilesAsync(mp3Files);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Folder pick error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the currently loaded file queue.
    /// </summary>
    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Files.Clear();
        ViewModel.SelectedFile = null;
    }

    /// <summary>
    /// Displays an image file picker to select custom artwork to embed in the MP3.
    /// </summary>
    private async void OnChangeCoverArtClick(object sender, RoutedEventArgs e)
    {
        try
        {
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
            System.Diagnostics.Debug.WriteLine($"Change cover art error: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes the current candidate cover artwork.
    /// </summary>
    private void OnRemoveCoverArtClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveCoverArt();
    }

    /// <summary>
    /// Applies the selected online search match to the editable metadata fields.
    /// </summary>
    private async void OnApplyResultClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchResult result })
        {
            await ViewModel.ApplySearchResultAsync(result);
        }
    }

    /// <summary>
    /// Initiates online metadata search when Enter is pressed in the search textbox.
    /// </summary>
    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchAsync();
        }
    }

    /// <summary>
    /// Handles drag over event to indicate copy acceptance for dropped files.
    /// </summary>
    private void OnGridDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop .mp3 files here";
        e.DragUIOverride.IsContentVisible = true;
    }

    /// <summary>
    /// Handles dropping MP3 files or folders onto the window to automatically add them to the queue.
    /// </summary>
    private async void OnGridDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = new System.Collections.Generic.List<string>();

            foreach (var item in items)
            {
                if (item is StorageFile file && file.FileType.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(file.Path);
                }
                else if (item is StorageFolder folder)
                {
                    string[] folderMp3s = Directory.GetFiles(folder.Path, "*.mp3", SearchOption.TopDirectoryOnly);
                    paths.AddRange(folderMp3s);
                }
            }

            if (paths.Count > 0)
            {
                await ViewModel.AddFilesAsync(paths);
            }
        }
    }
}
