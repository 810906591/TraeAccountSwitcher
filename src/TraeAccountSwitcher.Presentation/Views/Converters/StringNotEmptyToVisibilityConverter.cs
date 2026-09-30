using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TraeAccountSwitcher.Presentation.Views.Converters;

/// <summary>字符串非空（非空白）→ Visible；空 → Collapsed。用于忙碌遮罩。</summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    /// <summary>转换。</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>不支持反向转换。</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
