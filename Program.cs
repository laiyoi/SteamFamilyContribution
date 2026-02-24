﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using SteamContribution;
using System.Text.Json;

// 首次运行时检查配置文件
if (!ConfigManager.ConfigExists())
{
    Console.WriteLine("=== 首次运行 ===");
    Console.WriteLine("正在创建配置文件...");
    ConfigManager.CreateSampleConfig();
    Console.WriteLine("请编辑配置文件后重新运行程序，或直接在下方输入用户名密码登录");
    Console.WriteLine();
}

Console.WriteLine("=== Steam 家庭库贡献率查询工具 ===");
Console.WriteLine();
Console.WriteLine("本工具使用 SteamKit2 登录并查询家庭组信息");
Console.WriteLine();

// 添加操作选项
Console.WriteLine("请选择操作：");
Console.WriteLine("1. 完整功能（登录并查询家庭组）");
Console.WriteLine("2. 分析已保存的查询结果");
Console.WriteLine();
Console.Write("请输入选项（1 或 2，默认为 1）：");
var operationOption = Console.ReadLine()?.Trim();

if (operationOption == "2")
{
    // 分析已保存的查询结果
    AnalyzeSavedResult();
    return;
}

try
{
    await RunAsync();
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n操作已取消");
}
catch (Exception ex)
{
    Console.WriteLine($"\n发生错误：{ex.Message}");
    Console.WriteLine($"详细信息：{ex.StackTrace}");
}

async Task RunAsync()
{
    // 选择登录方式
    Console.WriteLine("请选择登录方式：");
    Console.WriteLine("1. 用户名密码登录");
    Console.WriteLine("2. 使用已保存的 Access Token");
    Console.WriteLine("3. 扫码登录");
    Console.WriteLine();
    Console.Write("请输入选项（1、2 或 3，默认为 1）：");
    var loginOption = Console.ReadLine()?.Trim();

    // 初始化 Steam 客户端
    var clientManager = new SteamClientManager();

    try
    {
        bool success;

        if (loginOption == "2")
        {
            // 使用已保存的 Access Token、Refresh Token 和用户名
            var accessToken = SteamClientManager.LoadAccessToken();
            var refreshToken = SteamClientManager.LoadRefreshToken();
            var username = SteamClientManager.LoadUsername();
            
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(username))
            {
                try
                {
                    success = await clientManager.LoginWithAccessTokenAsync(accessToken, refreshToken);
                }
                catch (Exception)
                {
                    success = false;
                }
            }
            else
            {
                success = false;
            }
        }
        else if (loginOption == "3")
        {
            // 扫码登录
            success = await clientManager.LoginWithQRAsync();
        }
        else
        {
            // 用户名密码登录
            // 尝试从配置文件读取
            var config = ConfigManager.LoadConfig();
            
            string? username = null;
            string? password = null;
            string? guardData = null;

            if (config != null && !string.IsNullOrEmpty(config.Username))
            {
                // 使用配置文件中的用户名和密码
                username = config.Username;
                password = config.Password;
                
                // 读取 GuardData
                guardData = SteamClientManager.LoadGuardData();
            }
            else
            {
                // 手动输入所有信息
                Console.Write("请输入 Steam 用户名：");
                username = Console.ReadLine()?.Trim();

                Console.Write("请输入 Steam 密码：");
                password = ReadPassword();

                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("用户名和密码不能为空！");
                return;
            }

            // 登录 Steam
            success = await clientManager.LoginAsync(username, password, guardData);
        }

        if (!success)
        {
            Console.WriteLine("登录失败，程序退出");
            return;
        }

        // 获取家庭组信息
        var familyService = new FamilyService(clientManager);
        var familyInfo = familyService.GetFullFamilyInfoAsync();

        if (familyInfo == null)
        {
            Console.WriteLine("未找到家庭组信息");
            return;
        }

        // 只调用一次 GetSharedLibraryApps 获取整个家庭库的游戏
        var libraryService = new LibraryService(clientManager);
        var allSharedApps = new List<SharedApp>();

        // 使用第一个成员的 SteamID 来获取整个家庭库的游戏
        if (familyInfo.Value.Members.Count > 0)
        {
            var firstMember = familyInfo.Value.Members.First();
            allSharedApps = libraryService.GetSharedLibraryAppsAsync(firstMember.SteamId, familyInfo.Value.FamilyGroupId);
        }
        else
        {
            Console.WriteLine("家庭组中没有成员");
        }

        // 读取配置文件获取 API Key
        var apiConfig = ConfigManager.LoadConfig();

        // 收集所有需要查询价格的游戏 AppId
        var allAppIds = allSharedApps.Select(app => app.AppId).ToList();

        // 获取游戏价格
        var countryCode = apiConfig?.CountryCode ?? "CN";
        var currency = CurrencyHelper.GetCurrencyByCountryCode(countryCode);
        var priceService = new PriceService(clientManager, countryCode);
        var priceMap = await priceService.GetBatchAppPricesAsync(allAppIds);

        // 计算每个成员的贡献
        var memberContributions = new List<MemberContribution>();
        double totalContribution = 0;

        // 准备保存的结果
        var queryResult = new QueryResult
        {
            FamilyGroupName = familyInfo.Value.Name,
            FamilyMemberCount = familyInfo.Value.Members.Count,
            Currency = currency
        };

        // 计算每个成员的贡献（不去重版本）和（去重版本）
        var memberUniqueContributions = new Dictionary<ulong, double>();
        
        foreach (var member in familyInfo.Value.Members)
        {
            // 从共享库中筛选出该成员拥有的游戏
            var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == member.SteamId).ToList();

            // 计算该成员的贡献（不去重版本）
            double memberContribution = 0;
            foreach (var app in memberOwnedApps)
            {
                if (priceMap.TryGetValue(app.AppId, out var priceInfo) && !priceInfo.IsFree)
                {
                    memberContribution += priceInfo.Price;
                }
            }

            // 计算该成员的贡献（去重版本）
            var uniqueMemberAppIds = memberOwnedApps.Select(app => app.AppId).Distinct().ToList();
            double memberUniqueContribution = 0;
            foreach (var appId in uniqueMemberAppIds)
            {
                if (priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
                {
                    memberUniqueContribution += priceInfo.Price;
                }
            }
            memberUniqueContributions[member.SteamId] = memberUniqueContribution;

            // 保存贡献信息
            memberContributions.Add(new MemberContribution
            {
                SteamId = member.SteamId,
                RoleName = member.RoleName,
                Contribution = memberContribution,
                OwnedGameCount = memberOwnedApps.Count
            });

            // 添加到查询结果
            queryResult.Members.Add(new MemberResult
            {
                SteamId = member.SteamId,
                RoleName = member.RoleName,
                Nickname = member.Nickname,
                AvatarUrl = member.AvatarUrl,
                Contribution = memberContribution,
                OwnedGameCount = memberOwnedApps.Count,
                OwnedGameIds = memberOwnedApps.Select(app => app.AppId).ToList()
            });

            totalContribution += memberContribution;
        }

        // 计算去重版本的总价值
        var uniqueAppIds = allSharedApps.Select(app => app.AppId).Distinct().ToList();
        double totalUniqueContribution = 0;
        foreach (var appId in uniqueAppIds)
        {
            if (priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
            {
                totalUniqueContribution += priceInfo.Price;
            }
        }

        // 计算贡献比率并显示汇总
        Console.WriteLine();
        Console.WriteLine("=== 贡献统计汇总 ===");
        Console.WriteLine($"共享库总价值（不去重）：{totalContribution:F2} {currency}");
        Console.WriteLine($"共享库总价值（去重）：{totalUniqueContribution:F2} {currency}");
        Console.WriteLine($"共享游戏总数：{allSharedApps.Count}");
        Console.WriteLine($"唯一游戏数量：{uniqueAppIds.Count}");
        Console.WriteLine();

        foreach (var contrib in memberContributions.OrderByDescending(c => c.Contribution))
        {
            // 尝试获取成员的昵称
            var member = familyInfo.Value.Members.FirstOrDefault(m => m.SteamId == contrib.SteamId);
            var memberName = member?.Nickname ?? contrib.SteamId.ToString();
            
            // 计算不去重版本的比率
            double ratio = totalContribution > 0 ? (contrib.Contribution / totalContribution) * 100 : 0;
            
            // 计算去重版本的比率
            double uniqueRatio = 0;
            if (memberUniqueContributions.TryGetValue(contrib.SteamId, out var uniqueContribution))
            {
                uniqueRatio = totalUniqueContribution > 0 ? (uniqueContribution / totalUniqueContribution) * 100 : 0;
            }
            
            Console.WriteLine($"成员：{memberName}");
            Console.WriteLine($"  贡献金额：{contrib.Contribution:F2} CNY");
            Console.WriteLine($"  贡献比率（不去重）：{ratio:F2}%");
            Console.WriteLine($"  贡献比率（去重）：{uniqueRatio:F2}%");
            Console.WriteLine($"  游戏数量：{contrib.OwnedGameCount}");
            Console.WriteLine();
        }

        // 填充查询结果的其他信息
        queryResult.TotalContribution = totalContribution;

        // 填充共享游戏信息
        foreach (var app in allSharedApps)
        {
            var priceInfo = priceMap.GetValueOrDefault(app.AppId);
            queryResult.SharedGames.Add(new SharedGameResult
            {
                AppId = app.AppId,
                Name = priceInfo?.Name ?? "未知游戏",
                OwnerId = app.OwnerId,
                Price = priceInfo?.Price ?? 0,
                IsFree = priceInfo?.IsFree ?? false
            });
        }

        // 保存查询结果
        ResultService.SaveQueryResult(queryResult);

        Console.WriteLine("=== 查询完成 ===");
    }
    finally
    {
        // 断开连接
        clientManager.Disconnect();
        Console.WriteLine("已断开连接");
    }
}

static void AnalyzeSavedResult()
{
    Console.WriteLine();
    Console.WriteLine("=== 分析已保存的查询结果 ===");
    Console.WriteLine();
    
    // 获取所有结果文件
    var resultFiles = ResultService.GetResultFiles();
    
    if (resultFiles.Count == 0)
    {
        Console.WriteLine("没有找到已保存的查询结果");
        return;
    }
    
    Console.WriteLine("可用的结果文件：");
    for (int i = 0; i < resultFiles.Count; i++)
    {
        var filename = Path.GetFileName(resultFiles[i]);
        Console.WriteLine($"{i + 1}. {filename}");
    }
    
    Console.WriteLine();
    Console.Write("请输入要分析的文件编号：");
    if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= resultFiles.Count)
    {
        var selectedFile = resultFiles[fileIndex - 1];
        var result = ResultService.LoadQueryResult(selectedFile);
        
        if (result != null)
        {
            ResultService.AnalyzeSavedResult(result);
        }
    }
    else
    {
        Console.WriteLine("无效的文件编号");
    }
}


static string ReadPassword()
{
    var password = "";
    while (true)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password = password[..^1];
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password += key.KeyChar;
            Console.Write("*");
        }
    }
    return password;
}
