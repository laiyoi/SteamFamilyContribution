using System;

namespace SteamContribution;

public static class FontHelper
{
    public static void InitializeFonts()
    {
        // 字体会在需要时由系统自动加载
        System.Diagnostics.Debug.WriteLine("[Font] 字体初始化完成");
    }
}
