using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Mp3MetadataCorrector.Helpers;

public static class ImageHelper
{
    public static async Task<BitmapImage?> ByteArrayToBitmapImageAsync(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
