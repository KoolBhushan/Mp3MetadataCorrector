using Microsoft.UI.Xaml;

namespace Mp3MetadataCorrector.Helpers;

public static class VisibilityHelper
{
    public static Visibility VisibleIfTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisibleIfFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleIfNotNull(object? value) => value != null ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisibleIfNull(object? value) => value == null ? Visibility.Visible : Visibility.Collapsed;
    public static bool Not(bool value) => !value;
}
