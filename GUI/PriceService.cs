using System.Text.Json;
using SteamKit2;

namespace SteamContribution;

public class PriceService
{
    private readonly SteamClientManager _clientManager;
    private readonly string _countryCode;
    
    // 价格缓存
    private static readonly Dictionary<uint, AppPriceInfo> _priceCache = new Dictionary<uint, AppPriceInfo>();
    private static DateTime _lastCacheClear = DateTime.Now;
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24); // 缓存24小时
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientManager">Steam 客户端管理器</param>
    /// <param name="countryCode">国家代码，默认值：CN</param>
    public PriceService(SteamClientManager clientManager, string countryCode = "CN")
    {
        _clientManager = clientManager;
        _countryCode = countryCode;
    }
    
    /// <summary>
    /// 清理过期缓存
    /// </summary>
    private static void ClearExpiredCache()
    {
        if (DateTime.Now - _lastCacheClear > _cacheExpiry)
        {
            _priceCache.Clear();
            _lastCacheClear = DateTime.Now;
            Logger.Info("[PriceService] 缓存已清理");
        }
    }
    
    /// <summary>
    /// 获取单个游戏的价格信息
    /// 使用 API: IStoreBrowseService.GetItems
    /// </summary>
    public async Task<AppPriceInfo?> GetSingleAppPriceAsync(uint appId)
    {
        // 获取货币代码
        var currency = CurrencyHelper.GetCurrencyByCountryCode(_countryCode);
        
        // 检查缓存
        ClearExpiredCache();
        if (_priceCache.TryGetValue(appId, out var cachedPrice))
        {
            return cachedPrice;
        }
        
        // 直接查询单个游戏价格
        var priceMap = await GetBatchAppPricesAsync(new List<uint> { appId });
        
        if (priceMap.TryGetValue(appId, out var priceInfo))
        {
            return priceInfo;
        }
        
        return null;
    }
    
    /// <summary>
    /// 批量获取游戏价格
    /// 使用 API: IStoreBrowseService.GetItems 批量查询
    /// 每次最多查询20个游戏，避免API限制
    /// </summary>
    public async Task<Dictionary<uint, AppPriceInfo>> GetBatchAppPricesAsync(List<uint> appIds)
    {
        // 获取货币代码
        var currency = CurrencyHelper.GetCurrencyByCountryCode(_countryCode);
        
        var priceMap = new Dictionary<uint, AppPriceInfo>();
        // 检查缓存，获取未缓存的游戏ID
        ClearExpiredCache();
        var uncachedAppIds = appIds.Where(appId => !_priceCache.ContainsKey(appId)).ToList();
        
        // 如果所有游戏都在缓存中，直接返回缓存结果
        if (uncachedAppIds.Count == 0)
        {
            foreach (var appId in appIds)
            {
                if (_priceCache.TryGetValue(appId, out var cachedPrice))
                {
                    priceMap[appId] = cachedPrice;
                }
            }
            return priceMap;
        }
        
        // 分批处理，每次最多查询20个游戏
        const int batchSize = 20;
        var batches = new List<List<uint>>();
        
        for (int i = 0; i < uncachedAppIds.Count; i += batchSize)
        {
            var batch = uncachedAppIds.Skip(i).Take(batchSize).ToList();
            batches.Add(batch);
        }
        
        // 处理每一批游戏
        foreach (var batch in batches)
        {
            var batchResult = await GetBatchAppPricesInternalAsync(batch, currency);
            foreach (var kvp in batchResult)
            {
                priceMap[kvp.Key] = kvp.Value;
            }
        }
        
        // 获取缓存中的游戏价格
        foreach (var appId in appIds)
        {
            if (!priceMap.ContainsKey(appId) && _priceCache.TryGetValue(appId, out var cachedPrice))
            {
                priceMap[appId] = cachedPrice;
            }
        }
        
        return priceMap;
    }
    
    /// <summary>
    /// 内部批量查询方法
    /// 处理单个批次的游戏价格查询
    /// </summary>
    private async Task<Dictionary<uint, AppPriceInfo>> GetBatchAppPricesInternalAsync(List<uint> appIds, string currency)
    {
        var priceMap = new Dictionary<uint, AppPriceInfo>();
        
        // 重试设置
        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // 使用 SteamKit2 的 WebAPI 接口进行批量查询
                using (dynamic storeBrowseService = WebAPI.GetInterface("IStoreBrowseService"))
                {
                    // 设置超时
                    storeBrowseService.Timeout = TimeSpan.FromSeconds(15);
                    
                    // 构建 input_json 参数
                    var idsJson = string.Join(",", appIds.Select(appId => $"{{\"appid\":{appId}}}"));
                    var inputJson = $"{{\"ids\":[{idsJson}],\"context\":{{\"country_code\":\"{_countryCode}\"}}}}";

                    // 调用 GetItems 方法获取游戏信息
                    KeyValue kvResult;
                    
                    // 检查 AccessToken 是否存在
                    if (string.IsNullOrEmpty(_clientManager.AccessToken))
                    {
                        // 尝试无 token 调用
                        kvResult = storeBrowseService.GetItems(
                            input_json: inputJson
                        );
                    }
                    else
                    {
                        // 使用 Access Token 调用
                        kvResult = storeBrowseService.GetItems(
                            input_json: inputJson,
                            access_token: _clientManager.AccessToken
                        );
                    }
                    
                    // 解析结果
                    if (kvResult == null)
                    {
                        break;
                    }
                    
                    // 尝试从不同可能的路径获取数据
                    List<KeyValue> appDataList = new List<KeyValue>();
                    
                    // 从 store_items 路径获取数据
                    if (kvResult["store_items"] != null)
                    {
                        appDataList.AddRange(kvResult["store_items"].Children);
                    }
                    else
                    {
                        return priceMap;
                    }
                    
                    // 处理每个游戏的数据
                    foreach (var appData in appDataList)
                    {
                        if (appData == null)
                            continue;
                        
                        // 提取游戏ID
                        uint appId = 0;
                        if (appData["appid"] != null)
                        {
                            appId = (uint)appData["appid"].AsUnsignedInteger();
                        }
                        
                        if (appId == 0)
                            continue;
                        
                        // 提取游戏名称
                        string appName = appData["name"].AsString() ?? "Unknown Game";
                        
                        // 提取价格信息
                        AppPriceInfo priceInfo;
                        
                        // 检查是否有 best_purchase_option
                        if (appData["best_purchase_option"] != null)
                        {
                            // 付费游戏
                            double price = 0;
                            
                            // 从 best_purchase_option 获取最终价格
                            if (appData["best_purchase_option"]["final_price_in_cents"] != null)
                            {
                                var priceInCents = appData["best_purchase_option"]["final_price_in_cents"].AsString();
                                if (double.TryParse(priceInCents, out double priceValue))
                                {
                                    price = priceValue / 100.0;
                                }
                            }
                            
                            priceInfo = new AppPriceInfo
                            {
                                AppId = appId,
                                Name = appName,
                                IsFree = false,
                                Price = price,
                                Currency = currency
                            };
                        }
                        else
                        {
                            // 免费游戏或无价格信息
                            priceInfo = new AppPriceInfo
                            {
                                AppId = appId,
                                Name = appName,
                                IsFree = true,
                                Price = 0,
                                Currency = currency
                            };
                        }
                        
                        // 存入缓存
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
                    // 网络错误，进行重试
                    int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs);
                    continue;
                }
                
                break;
            }
        }
        
        // 如果批量查询失败，返回空结果
        return priceMap;
    }
    
}

// 游戏价格信息类
public class AppPriceInfo
{
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public bool IsFree { get; set; }
    public double Price { get; set; }
    public string Currency { get; set; } = "CNY";
}
