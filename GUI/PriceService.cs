using System.Text.Json;
using SteamKit2;

namespace SteamContribution;

public class PriceService
{
    private readonly SteamClientManager _clientManager;
    private readonly string _countryCode;
    
    private static readonly Dictionary<uint, AppPriceInfo> _priceCache = new();
    private static DateTime _lastCacheClear = DateTime.Now;
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);
    
    public PriceService(SteamClientManager clientManager, string countryCode = "CN")
    {
        _clientManager = clientManager;
        _countryCode = countryCode;
    }
    
    private static void ClearExpiredCache()
    {
        if (DateTime.Now - _lastCacheClear > _cacheExpiry)
        {
            _priceCache.Clear();
            _lastCacheClear = DateTime.Now;
            Logger.Info("[PriceService] 缓存已清理");
        }
    }
    
    public async Task<AppPriceInfo?> GetSingleAppPriceAsync(uint appId)
    {
        var currency = CurrencyHelper.GetCurrencyByCountryCode(_countryCode);
        ClearExpiredCache();
        
        if (_priceCache.TryGetValue(appId, out var cachedPrice))
            return cachedPrice;
        
        var priceMap = await GetBatchAppPricesAsync(new List<uint> { appId });
        return priceMap.TryGetValue(appId, out var priceInfo) ? priceInfo : null;
    }
    
    public async Task<Dictionary<uint, AppPriceInfo>> GetBatchAppPricesAsync(List<uint> appIds)
    {
        var currency = CurrencyHelper.GetCurrencyByCountryCode(_countryCode);
        var priceMap = new Dictionary<uint, AppPriceInfo>();
        
        ClearExpiredCache();
        var uncachedAppIds = appIds.Where(appId => !_priceCache.ContainsKey(appId)).ToList();
        
        if (uncachedAppIds.Count == 0)
        {
            foreach (var appId in appIds)
            {
                if (_priceCache.TryGetValue(appId, out var cachedPrice))
                    priceMap[appId] = cachedPrice;
            }
            return priceMap;
        }
        
        const int batchSize = 20;
        var batches = new List<List<uint>>();
        
        for (int i = 0; i < uncachedAppIds.Count; i += batchSize)
        {
            var batch = uncachedAppIds.Skip(i).Take(batchSize).ToList();
            batches.Add(batch);
        }
        
        foreach (var batch in batches)
        {
            var batchResult = await GetBatchAppPricesInternalAsync(batch, currency);
            foreach (var kvp in batchResult)
                priceMap[kvp.Key] = kvp.Value;
        }
        
        foreach (var appId in appIds)
        {
            if (!priceMap.ContainsKey(appId) && _priceCache.TryGetValue(appId, out var cachedPrice))
                priceMap[appId] = cachedPrice;
        }
        
        return priceMap;
    }
    
    private async Task<Dictionary<uint, AppPriceInfo>> GetBatchAppPricesInternalAsync(List<uint> appIds, string currency)
    {
        var priceMap = new Dictionary<uint, AppPriceInfo>();
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using (dynamic storeBrowseService = WebAPI.GetInterface("IStoreBrowseService"))
                {
                    storeBrowseService.Timeout = TimeSpan.FromSeconds(15);
                    
                    var idsJson = string.Join(",", appIds.Select(appId => $"{{\"appid\":{appId}}}"));
                    var inputJson = $"{{\"ids\":[{idsJson}],\"context\":{{\"country_code\":\"{_countryCode}\"}}}}";

                    KeyValue kvResult = string.IsNullOrEmpty(_clientManager.AccessToken)
                        ? storeBrowseService.GetItems(input_json: inputJson)
                        : storeBrowseService.GetItems(input_json: inputJson, access_token: _clientManager.AccessToken);
                    
                    if (kvResult == null || kvResult["store_items"] == null)
                        break;
                    
                    foreach (var appData in kvResult["store_items"].Children)
                    {
                        if (appData == null || appData["appid"] == null)
                            continue;
                        
                        uint appId = (uint)appData["appid"].AsUnsignedInteger();
                        if (appId == 0) continue;
                        
                        string appName = appData["name"].AsString() ?? "Unknown Game";
                        
                        AppPriceInfo priceInfo = appData["best_purchase_option"] != null
                            ? new AppPriceInfo
                            {
                                AppId = appId,
                                Name = appName,
                                IsFree = false,
                                Price = ParsePrice(appData["best_purchase_option"]["final_price_in_cents"].AsString()),
                                Currency = currency
                            }
                            : new AppPriceInfo
                            {
                                AppId = appId,
                                Name = appName,
                                IsFree = true,
                                Price = 0,
                                Currency = currency
                            };
                        
                        _priceCache[appId] = priceInfo;
                        priceMap[appId] = priceInfo;
                    }
                    
                    return priceMap;
                }
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs);
                    continue;
                }
                break;
            }
        }
        
        return priceMap;
    }
    
    private static double ParsePrice(string? priceInCents)
    {
        if (double.TryParse(priceInCents, out double priceValue))
            return priceValue / 100.0;
        return 0;
    }
}

public class AppPriceInfo
{
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public bool IsFree { get; set; }
    public double Price { get; set; }
    public string Currency { get; set; } = "CNY";
}
