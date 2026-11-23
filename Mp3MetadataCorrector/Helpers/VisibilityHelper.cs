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

using Microsoft.UI.Xaml;

namespace Mp3MetadataCorrector.Helpers;

/// <summary>
/// Static helper functions for type-safe compiled x:Bind visibility bindings.
/// </summary>
public static class VisibilityHelper
{
    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> if the condition is true; otherwise <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <param name="value">Boolean flag.</param>
    /// <returns>Visibility enumeration.</returns>
    public static Visibility VisibleIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Returns <see cref="Visibility.Collapsed"/> if the condition is true; otherwise <see cref="Visibility.Visible"/>.
    /// </summary>
    /// <param name="value">Boolean flag.</param>
    /// <returns>Visibility enumeration.</returns>
    public static Visibility VisibleIfFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> if the object is not null; otherwise <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <param name="value">Target object.</param>
    /// <returns>Visibility enumeration.</returns>
    public static Visibility VisibleIfNotNull(object? value) => value != null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> if the object is null; otherwise <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <param name="value">Target object.</param>
    /// <returns>Visibility enumeration.</returns>
    public static Visibility VisibleIfNull(object? value) => value == null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Inverts a boolean value for use in direct boolean bindings.
    /// </summary>
    /// <param name="value">Input boolean.</param>
    /// <returns>Negated boolean.</returns>
    public static bool Not(bool value) => !value;
}
