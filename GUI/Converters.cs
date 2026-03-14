using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SteamContribution;

public class ImageUrlConverter : IValueConverter
{
    public static readonly ImageUrlConverter Instance = new();
    private static readonly HttpClient _httpClient = new HttpClient();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string urlString && !string.IsNullOrEmpty(urlString))
        {
            // 不阻塞 UI 线程，返回 null，让 Avalonia 稍后异步加载
            // 或者使用占位图
            return null;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// 异步加载图片（可选调用）
    /// </summary>
    public static async Task<Bitmap?> LoadBitmapAsync(string url)
    {
        try
        {
            var data = await _httpClient.GetByteArrayAsync(url);
            using var ms = new MemoryStream(data);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}
