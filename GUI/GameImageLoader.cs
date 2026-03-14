using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace SteamContribution;

public static class GameImageLoader
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> _loadingTasks = new();

    static GameImageLoader()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public static async Task<Bitmap?> GetImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        if (_cache.TryGetValue(url, out var cachedBitmap))
        {
            Logger.Debug($"[图片] 命中缓存: {url}");
            return cachedBitmap;
        }

        if (_loadingTasks.TryGetValue(url, out var loadingTask))
        {
            Logger.Debug($"[图片] 等待中: {url}");
            return await loadingTask;
        }

        Logger.Debug($"[图片] 开始请求: {url}");
        
        var loadTask = LoadImageInternalAsync(url);
        _loadingTasks.TryAdd(url, loadTask);

        try
        {
            var bitmap = await loadTask;
            if (bitmap != null)
            {
                _cache.TryAdd(url, bitmap);
                Logger.Debug($"[图片] 加载成功: {url} ({(bitmap.PixelSize.Width)}x{bitmap.PixelSize.Height})");
            }
            else
            {
                Logger.Debug($"[图片] 加载失败: {url}");
            }
            return bitmap;
        }
        finally
        {
            _loadingTasks.TryRemove(url, out _);
        }
    }

    private static async Task<Bitmap?> LoadImageInternalAsync(string url)
    {
        try
        {
            var data = await _httpClient.GetByteArrayAsync(url);
            using var ms = new MemoryStream(data);
            var bitmap = new Bitmap(ms);
            return bitmap;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[图片] HTTP请求失败: {url}, 错误: {ex.Message}");
            return null;
        }
    }

    public static void ClearCache()
    {
        foreach (var kvp in _cache)
        {
            kvp.Value?.Dispose();
        }
        _cache.Clear();
    }
}
