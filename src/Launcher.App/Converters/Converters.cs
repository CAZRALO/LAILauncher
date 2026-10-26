using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Launcher.SDK;
using Launcher.App.Services;

namespace Launcher.App.Converters;

public class ResultToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            return IconExtractor.GetIcon(path);
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));

        if (Invert)
        {
            return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ResultTypeToColorConverter : IValueConverter
{
    // Light theme brushes (Deep saturated colors for crisp readability on light backgrounds)
    private static readonly SolidColorBrush L_App = Freeze(new SolidColorBrush(Color.FromRgb(0x43, 0x38, 0xCA)));     // Indigo 700
    private static readonly SolidColorBrush L_Calc = Freeze(new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57)));    // Emerald 700
    private static readonly SolidColorBrush L_File = Freeze(new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09)));    // Amber 700
    private static readonly SolidColorBrush L_Folder = Freeze(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));  // Orange 700
    private static readonly SolidColorBrush L_AI = Freeze(new SolidColorBrush(Color.FromRgb(0xA2, 0x1C, 0xAF)));      // Fuchsia 700
    private static readonly SolidColorBrush L_Url = Freeze(new SolidColorBrush(Color.FromRgb(0x03, 0x69, 0xA1)));     // Sky 700
    private static readonly SolidColorBrush L_Command = Freeze(new SolidColorBrush(Color.FromRgb(0xBE, 0x12, 0x3C))); // Rose 700
    private static readonly SolidColorBrush L_Plugin = Freeze(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));  // Orange 700
    private static readonly SolidColorBrush L_Clip = Freeze(new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E)));    // Teal 700
    private static readonly SolidColorBrush L_Sys = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)));     // Red 700
    private static readonly SolidColorBrush L_Bookmark = Freeze(new SolidColorBrush(Color.FromRgb(0xA1, 0x62, 0x07)));// Gold 700
    private static readonly SolidColorBrush L_Process = Freeze(new SolidColorBrush(Color.FromRgb(0x93, 0x33, 0xEA))); // Purple 600
    private static readonly SolidColorBrush L_Setting = Freeze(new SolidColorBrush(Color.FromRgb(0x0E, 0x74, 0x90))); // Cyan 700
    private static readonly SolidColorBrush L_Default = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))); // Slate 700

    // Dark / Glass theme brushes (Luminous vibrant pastels for dark backgrounds)
    private static readonly SolidColorBrush D_App = Freeze(new SolidColorBrush(Color.FromRgb(0xA5, 0xB4, 0xFC)));     // Indigo 300
    private static readonly SolidColorBrush D_Calc = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0xE7, 0xB7)));    // Emerald 300
    private static readonly SolidColorBrush D_File = Freeze(new SolidColorBrush(Color.FromRgb(0xFC, 0xD3, 0x4D)));    // Amber 300
    private static readonly SolidColorBrush D_Folder = Freeze(new SolidColorBrush(Color.FromRgb(0xFD, 0xBA, 0x74)));  // Orange 300
    private static readonly SolidColorBrush D_AI = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6)));      // Pink 400
    private static readonly SolidColorBrush D_Url = Freeze(new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFC)));     // Sky 300
    private static readonly SolidColorBrush D_Command = Freeze(new SolidColorBrush(Color.FromRgb(0xFD, 0xA4, 0xAF))); // Rose 300
    private static readonly SolidColorBrush D_Plugin = Freeze(new SolidColorBrush(Color.FromRgb(0xFB, 0x92, 0x3C)));  // Orange 400
    private static readonly SolidColorBrush D_Clip = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xEA, 0xD4)));    // Teal 300
    private static readonly SolidColorBrush D_Sys = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)));     // Red 400
    private static readonly SolidColorBrush D_Bookmark = Freeze(new SolidColorBrush(Color.FromRgb(0xFD, 0xE0, 0x47)));// Gold 300
    private static readonly SolidColorBrush D_Process = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0x79, 0xF9))); // Fuchsia 400
    private static readonly SolidColorBrush D_Setting = Freeze(new SolidColorBrush(Color.FromRgb(0x67, 0xE8, 0xF9))); // Cyan 300
    private static readonly SolidColorBrush D_Default = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC))); // Crisp Light White

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = ThemeManager.IsLightMode;

        if (value is ResultType type)
        {
            if (isLight)
            {
                return type switch
                {
                    ResultType.App => L_App,
                    ResultType.Calculator => L_Calc,
                    ResultType.File => L_File,
                    ResultType.Folder => L_Folder,
                    ResultType.AI => L_AI,
                    ResultType.Url => L_Url,
                    ResultType.Command => L_Command,
                    ResultType.Plugin => L_Plugin,
                    ResultType.Clipboard => L_Clip,
                    ResultType.SystemCommand => L_Sys,
                    ResultType.Bookmark => L_Bookmark,
                    ResultType.Process => L_Process,
                    ResultType.Setting => L_Setting,
                    _ => L_Default
                };
            }

            return type switch
            {
                ResultType.App => D_App,
                ResultType.Calculator => D_Calc,
                ResultType.File => D_File,
                ResultType.Folder => D_Folder,
                ResultType.AI => D_AI,
                ResultType.Url => D_Url,
                ResultType.Command => D_Command,
                ResultType.Plugin => D_Plugin,
                ResultType.Clipboard => D_Clip,
                ResultType.SystemCommand => D_Sys,
                ResultType.Bookmark => D_Bookmark,
                ResultType.Process => D_Process,
                ResultType.Setting => D_Setting,
                _ => D_Default
            };
        }
        return isLight ? L_Default : D_Default;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ResultTypeToBackgroundConverter : IValueConverter
{
    // Light Mode subtle soft background fills
    private static readonly SolidColorBrush L_AppBg = Freeze(new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xFF)));
    private static readonly SolidColorBrush L_CalcBg = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5)));
    private static readonly SolidColorBrush L_FileBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)));
    private static readonly SolidColorBrush L_FolderBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)));
    private static readonly SolidColorBrush L_AIBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFD, 0xF4, 0xFF)));
    private static readonly SolidColorBrush L_UrlBg = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xFF)));
    private static readonly SolidColorBrush L_CommandBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xF2)));
    private static readonly SolidColorBrush L_PluginBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)));
    private static readonly SolidColorBrush L_ClipBg = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xFA)));
    private static readonly SolidColorBrush L_SysBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)));
    private static readonly SolidColorBrush L_BookmarkBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xFC, 0xE8)));
    private static readonly SolidColorBrush L_ProcessBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xF5, 0xFF)));
    private static readonly SolidColorBrush L_SettingBg = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xFE, 0xFF)));
    private static readonly SolidColorBrush L_DefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)));

    // Dark & Glass translucent rich dark fills
    private static readonly SolidColorBrush D_AppBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x43, 0x38, 0xCA)));
    private static readonly SolidColorBrush D_CalcBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x05, 0x96, 0x69)));
    private static readonly SolidColorBrush D_FileBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xB4, 0x53, 0x09)));
    private static readonly SolidColorBrush D_FolderBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xD9, 0x77, 0x06)));
    private static readonly SolidColorBrush D_AIBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xA2, 0x1C, 0xAF)));
    private static readonly SolidColorBrush D_UrlBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x02, 0x84, 0xC7)));
    private static readonly SolidColorBrush D_CommandBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xBE, 0x12, 0x3C)));
    private static readonly SolidColorBrush D_PluginBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xC2, 0x41, 0x0C)));
    private static readonly SolidColorBrush D_ClipBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x0D, 0x94, 0x88)));
    private static readonly SolidColorBrush D_SysBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xDC, 0x26, 0x26)));
    private static readonly SolidColorBrush D_BookmarkBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xCA, 0x8A, 0x04)));
    private static readonly SolidColorBrush D_ProcessBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0xC0, 0x26, 0xD3)));
    private static readonly SolidColorBrush D_SettingBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x08, 0x91, 0xB2)));
    private static readonly SolidColorBrush D_DefaultBg = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x3F, 0x3F, 0x46)));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = ThemeManager.IsLightMode;

        if (value is ResultType type)
        {
            if (isLight)
            {
                return type switch
                {
                    ResultType.App => L_AppBg,
                    ResultType.Calculator => L_CalcBg,
                    ResultType.File => L_FileBg,
                    ResultType.Folder => L_FolderBg,
                    ResultType.AI => L_AIBg,
                    ResultType.Url => L_UrlBg,
                    ResultType.Command => L_CommandBg,
                    ResultType.Plugin => L_PluginBg,
                    ResultType.Clipboard => L_ClipBg,
                    ResultType.SystemCommand => L_SysBg,
                    ResultType.Bookmark => L_BookmarkBg,
                    ResultType.Process => L_ProcessBg,
                    ResultType.Setting => L_SettingBg,
                    _ => L_DefaultBg
                };
            }

            return type switch
            {
                ResultType.App => D_AppBg,
                ResultType.Calculator => D_CalcBg,
                ResultType.File => D_FileBg,
                ResultType.Folder => D_FolderBg,
                ResultType.AI => D_AIBg,
                ResultType.Url => D_UrlBg,
                ResultType.Command => D_CommandBg,
                ResultType.Plugin => D_PluginBg,
                ResultType.Clipboard => D_ClipBg,
                ResultType.SystemCommand => D_SysBg,
                ResultType.Bookmark => D_BookmarkBg,
                ResultType.Process => D_ProcessBg,
                ResultType.Setting => D_SettingBg,
                _ => D_DefaultBg
            };
        }
        return isLight ? L_DefaultBg : D_DefaultBg;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ResultTypeToBorderConverter : IValueConverter
{
    // Light Mode subtle elegant border colors
    private static readonly SolidColorBrush L_AppBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC7, 0xD2, 0xFE)));
    private static readonly SolidColorBrush L_CalcBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xA7, 0xF3, 0xD0)));
    private static readonly SolidColorBrush L_FileBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFD, 0xE6, 0x8A)));
    private static readonly SolidColorBrush L_FolderBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xD7, 0xAA)));
    private static readonly SolidColorBrush L_AIBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0xFE)));
    private static readonly SolidColorBrush L_UrlBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xBA, 0xE6, 0xFD)));
    private static readonly SolidColorBrush L_CommandBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xCD, 0xD3)));
    private static readonly SolidColorBrush L_PluginBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xD7, 0xAA)));
    private static readonly SolidColorBrush L_ClipBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x99, 0xF6, 0xE4)));
    private static readonly SolidColorBrush L_SysBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA)));
    private static readonly SolidColorBrush L_BookmarkBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF0, 0x8A)));
    private static readonly SolidColorBrush L_ProcessBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE8, 0xFF)));
    private static readonly SolidColorBrush L_SettingBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xA5, 0xF3, 0xFC)));
    private static readonly SolidColorBrush L_DefaultBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));

    // Dark & Glass border colors
    private static readonly SolidColorBrush D_AppBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x63, 0x66, 0xF1)));
    private static readonly SolidColorBrush D_CalcBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0xB9, 0x81)));
    private static readonly SolidColorBrush D_FileBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xF5, 0x9E, 0x0B)));
    private static readonly SolidColorBrush D_FolderBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xD9, 0x77, 0x06)));
    private static readonly SolidColorBrush D_AIBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xEC, 0x48, 0x99)));
    private static readonly SolidColorBrush D_UrlBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x06, 0xB6, 0xD4)));
    private static readonly SolidColorBrush D_CommandBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xF4, 0x3F, 0x5E)));
    private static readonly SolidColorBrush D_PluginBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xF9, 0x73, 0x16)));
    private static readonly SolidColorBrush D_ClipBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x14, 0xB8, 0xA6)));
    private static readonly SolidColorBrush D_SysBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xEF, 0x44, 0x44)));
    private static readonly SolidColorBrush D_BookmarkBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xEA, 0xB3, 0x08)));
    private static readonly SolidColorBrush D_ProcessBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0xD9, 0x46, 0xEF)));
    private static readonly SolidColorBrush D_SettingBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x06, 0xB6, 0xD4)));
    private static readonly SolidColorBrush D_DefaultBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x52, 0x52, 0x5B)));

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = ThemeManager.IsLightMode;

        if (value is ResultType type)
        {
            if (isLight)
            {
                return type switch
                {
                    ResultType.App => L_AppBorder,
                    ResultType.Calculator => L_CalcBorder,
                    ResultType.File => L_FileBorder,
                    ResultType.Folder => L_FolderBorder,
                    ResultType.AI => L_AIBorder,
                    ResultType.Url => L_UrlBorder,
                    ResultType.Command => L_CommandBorder,
                    ResultType.Plugin => L_PluginBorder,
                    ResultType.Clipboard => L_ClipBorder,
                    ResultType.SystemCommand => L_SysBorder,
                    ResultType.Bookmark => L_BookmarkBorder,
                    ResultType.Process => L_ProcessBorder,
                    ResultType.Setting => L_SettingBorder,
                    _ => L_DefaultBorder
                };
            }

            return type switch
            {
                ResultType.App => D_AppBorder,
                ResultType.Calculator => D_CalcBorder,
                ResultType.File => D_FileBorder,
                ResultType.Folder => D_FolderBorder,
                ResultType.AI => D_AIBorder,
                ResultType.Url => D_UrlBorder,
                ResultType.Command => D_CommandBorder,
                ResultType.Plugin => D_PluginBorder,
                ResultType.Clipboard => D_ClipBorder,
                ResultType.SystemCommand => D_SysBorder,
                ResultType.Bookmark => D_BookmarkBorder,
                ResultType.Process => D_ProcessBorder,
                ResultType.Setting => D_SettingBorder,
                _ => D_DefaultBorder
            };
        }
        return isLight ? L_DefaultBorder : D_DefaultBorder;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ResultTypeToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Application.Current == null) return null;

        if (value is QueryResult item)
        {
            var title = item.Title?.ToLowerInvariant() ?? "";
            if (title.Contains("google")) return Application.Current.TryFindResource("GeoGoogle");
            if (title.Contains("youtube")) return Application.Current.TryFindResource("GeoYouTube");
            if (title.Contains("github")) return Application.Current.TryFindResource("GeoGitHub");

            string key = item.Type switch
            {
                ResultType.Calculator => "GeoCalculator",
                ResultType.AI => "GeoSparkle",
                ResultType.Command => "GeoTerminal",
                ResultType.Url => "GeoGlobe",
                ResultType.Bookmark => "GeoBookmark",
                ResultType.Process => "GeoActivity",
                ResultType.Setting => "GeoSettings",
                ResultType.Folder => "GeoFolder",
                ResultType.File => "GeoCopy",
                ResultType.Clipboard => "GeoCopy",
                ResultType.SystemCommand => "GeoLightning",
                _ => "GeoSparkle"
            };

            if (Application.Current.TryFindResource(key) is Geometry geo)
            {
                return geo;
            }
        }
        else if (value is ResultType type)
        {
            string key = type switch
            {
                ResultType.Calculator => "GeoCalculator",
                ResultType.AI => "GeoSparkle",
                ResultType.Command => "GeoTerminal",
                ResultType.Url => "GeoGlobe",
                ResultType.Bookmark => "GeoBookmark",
                ResultType.Process => "GeoActivity",
                ResultType.Setting => "GeoSettings",
                ResultType.Folder => "GeoFolder",
                ResultType.File => "GeoCopy",
                ResultType.Clipboard => "GeoCopy",
                ResultType.SystemCommand => "GeoLightning",
                _ => "GeoSparkle"
            };

            if (Application.Current.TryFindResource(key) is Geometry geo)
            {
                return geo;
            }
        }
        return Application.Current?.TryFindResource("GeoSparkle");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ResultTypeToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ResultType type)
        {
            return type switch
            {
                ResultType.App => "Ứng dụng",
                ResultType.Calculator => "Tính toán",
                ResultType.AI => "Trợ lý AI",
                ResultType.Command => "Lệnh Shell",
                ResultType.Url => "Trang web",
                ResultType.Bookmark => "Dấu trang",
                ResultType.Process => "Tiến trình",
                ResultType.Setting => "Cài đặt",
                ResultType.Folder => "Thư mục",
                ResultType.File => "Tệp tin",
                ResultType.Clipboard => "Khay nhớ tạm",
                ResultType.SystemCommand => "Hệ thống",
                ResultType.Plugin => "Tiện ích",
                _ => type.ToString()
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

