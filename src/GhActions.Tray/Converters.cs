using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GhActions.Tray;

public sealed class BoolToVisibility : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}

public sealed class BoolToArrow : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is true ? "▾" : "▸";

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotSupportedException();
}
