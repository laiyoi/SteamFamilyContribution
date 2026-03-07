namespace SteamContribution;

public static class CurrencyHelper
{
    // 国家代码到货币代码的映射
    private static readonly Dictionary<string, string> CountryToCurrencyMap = new Dictionary<string, string>
    {
        { "CN", "CNY" },   // 中国 - 人民币
        { "US", "USD" },   // 美国 - 美元
        { "EU", "EUR" },   // 欧盟 - 欧元
        { "GB", "GBP" },   // 英国 - 英镑
        { "JP", "JPY" },   // 日本 - 日元
        { "KR", "KRW" },   // 韩国 - 韩元
        { "CA", "CAD" },   // 加拿大 - 加元
        { "AU", "AUD" },   // 澳大利亚 - 澳元
        { "IN", "INR" },   // 印度 - 卢比
        { "RU", "RUB" },   // 俄罗斯 - 卢布
        { "BR", "BRL" },   // 巴西 - 雷亚尔
        { "MX", "MXN" },   // 墨西哥 - 比索
        { "DE", "EUR" },   // 德国 - 欧元
        { "FR", "EUR" },   // 法国 - 欧元
        { "IT", "EUR" },   // 意大利 - 欧元
        { "ES", "EUR" }    // 西班牙 - 欧元
    };
    
    /// <summary>
    /// 根据国家代码获取货币代码
    /// </summary>
    /// <param name="countryCode">国家代码</param>
    /// <returns>货币代码，默认返回CNY</returns>
    public static string GetCurrencyByCountryCode(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode))
        {
            return "CNY";
        }
        
        if (CountryToCurrencyMap.TryGetValue(countryCode.ToUpper(), out var currency))
        {
            return currency;
        }
        
        // 默认返回CNY
        return "CNY";
    }
}