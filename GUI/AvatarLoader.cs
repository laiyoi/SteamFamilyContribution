using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace SteamContribution;

public static class AvatarLoader
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly ConcurrentDictionary<string, Bitmap> _cache = new();
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> _loadingTasks = new();

    public static async Task<Bitmap?> GetAvatarAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return null;

        // 检查缓存
        if (_cache.TryGetValue(url, out var cachedBitmap))
            return cachedBitmap;

        // 检查是否正在加载
        if (_loadingTasks.TryGetValue(url, out var loadingTask))
            return await loadingTask;

        // 开始加载
        var loadTask = LoadAvatarInternalAsync(url);
        _loadingTasks.TryAdd(url, loadTask);

        try
        {
            var bitmap = await loadTask;
            if (bitmap != null)
            {
                _cache.TryAdd(url, bitmap);
            }
            return bitmap;
        }
        finally
        {
            _loadingTasks.TryRemove(url, out _);
        }
    }

    private static async Task<Bitmap?> LoadAvatarInternalAsync(string url)
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
            System.Diagnostics.Debug.WriteLine($"[AvatarLoader] 加载失败：{url}, 错误：{ex.Message}");
            return null;
        }
    }

    public static void ClearCache()
    {
        foreach (var kvp in _cache)
        {
            kvp.Value.Dispose();
        }
        _cache.Clear();
    }
}
