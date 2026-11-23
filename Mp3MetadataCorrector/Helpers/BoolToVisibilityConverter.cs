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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Mp3MetadataCorrector.Helpers;

/// <summary>
/// Converts boolean values to UI <see cref="Visibility"/> enumerations and supports inversion.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether to invert the conversion result (true = Collapsed, false = Visible).
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// Converts a boolean value to a <see cref="Visibility"/> value.
    /// </summary>
    /// <param name="value">The source data being passed to the target.</param>
    /// <param name="targetType">The type of the target property.</param>
    /// <param name="parameter">An optional parameter to be used in the converter logic.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns><see cref="Visibility.Visible"/> if true (or false when inverted), otherwise <see cref="Visibility.Collapsed"/>.</returns>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Converts a <see cref="Visibility"/> value back to a boolean.
    /// </summary>
    /// <param name="value">The target data being passed to the source.</param>
    /// <param name="targetType">The type of the target property.</param>
    /// <param name="parameter">An optional parameter to be used in the converter logic.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>True if Visible (or false if inverted), otherwise false.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        bool visible = value is Visibility v && v == Visibility.Visible;
        return Invert ? !visible : visible;
    }
}
