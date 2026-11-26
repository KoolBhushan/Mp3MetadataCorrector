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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Mp3MetadataCorrector.Helpers;

/// <summary>
/// Provides utility methods for converting raw image binary data to WinUI BitmapImage objects.
/// </summary>
public static class ImageHelper
{
    /// <summary>
    /// Safely creates an <see cref="ImageSource"/> from an image URL string, returning null if empty or invalid.
    /// Prevents XAML binding exceptions when null or invalid URLs are passed to an Image control.
    /// </summary>
    /// <param name="url">The image URL string.</param>
    /// <returns>A <see cref="BitmapImage"/> if the URL is a valid absolute HTTP/HTTPS address; otherwise null.</returns>
    public static ImageSource? SafeImageSource(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            try
            {
                return new BitmapImage(uri);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a byte array of image data into a WinUI <see cref="BitmapImage"/> asynchronously.
    /// </summary>
    /// <param name="bytes">Raw byte array containing image data (JPEG or PNG format).</param>
    /// <returns>A <see cref="BitmapImage"/> instance ready for UI display, or null if conversion fails or data is empty.</returns>
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
