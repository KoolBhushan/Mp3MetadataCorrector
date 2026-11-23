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

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        this.InitializeComponent();

        ConfigureWindow();
    }

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

    private void OnToggleDownloaderClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleDownloader();
    }

    private async void OnDownloadUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.DownloadMp3Async();
        }
    }

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

    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Files.Clear();
        ViewModel.SelectedFile = null;
    }

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

    private void OnRemoveCoverArtClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveCoverArt();
    }

    private async void OnApplyResultClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchResult result })
        {
            await ViewModel.ApplySearchResultAsync(result);
        }
    }

    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchAsync();
        }
    }

    private void OnGridDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop .mp3 files here";
        e.DragUIOverride.IsContentVisible = true;
    }

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
