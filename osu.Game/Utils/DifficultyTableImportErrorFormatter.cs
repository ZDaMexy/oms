// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace osu.Game.Utils
{
    public static class DifficultyTableImportErrorFormatter
    {
        public static string Format(Exception exception)
        {
            Exception baseException = exception.GetBaseException();

            return tryFormat(baseException) ?? fallback(baseException);
        }

        private static string? tryFormat(Exception exception)
        {
            switch (exception)
            {
                case TaskCanceledException:
                    return "连接难度表超时。请稍后重试，或先在浏览器确认该链接可以正常打开。";

                case HttpRequestException httpException:
                    return formatHttpError(httpException);

                case FileNotFoundException:
                    return "找不到难度表路径或文件。请确认导入的是有效的目录、index.html、header.json，或可访问的在线链接。";
            }

            return formatKnownManagerMessage(exception.Message);
        }

        private static string formatHttpError(HttpRequestException exception)
        {
            if (!exception.StatusCode.HasValue)
                return "连接难度表站点失败。请检查网络连接，或确认该链接当前可访问。";

            return exception.StatusCode.Value switch
            {
                HttpStatusCode.NotFound => "找不到难度表文件。请检查链接是否指向正确的 index.html、header.json 或表体 json。",
                HttpStatusCode.Forbidden => "难度表站点拒绝访问当前链接。请稍后重试，或确认该地址是否仍然可公开访问。",
                HttpStatusCode.TooManyRequests => "难度表站点请求过于频繁。请稍后再试。",
                HttpStatusCode.RequestTimeout => "难度表站点响应超时。请稍后重试。",
                _ when (int)exception.StatusCode.Value >= 500 => "难度表站点暂时不可用。请稍后重试。",
                _ => $"难度表站点返回 HTTP {(int)exception.StatusCode.Value}。请确认该链接当前可访问。",
            };
        }

        private static string? formatKnownManagerMessage(string message)
        {
            if (message.Contains("Could not find difficulty table source", StringComparison.OrdinalIgnoreCase))
                return "找不到已保存的难度表来源。请刷新设置页后重试。";

            if (message.Contains("has no local path configured", StringComparison.OrdinalIgnoreCase))
                return "这个难度表当前没有可刷新的来源地址。";

            if (message.Contains("No supported BMS difficulty table files were found", StringComparison.OrdinalIgnoreCase))
                return "所选目录里没有找到可识别的难度表文件。请导入目录、index.html、header.json，或表体 json。";

            if (message.Contains("No bmstable meta tag was found", StringComparison.OrdinalIgnoreCase))
                return "该 HTML 不是有效的 bmstable 入口页，缺少 bmstable meta 标签。请改用 index.html 或 header.json。";

            if (message.Contains("missing data_url", StringComparison.OrdinalIgnoreCase))
                return "难度表 header.json 缺少 data_url，无法继续读取表体数据。";

            if (message.Contains("must be a JSON array", StringComparison.OrdinalIgnoreCase))
                return "难度表表体格式不正确，数据文件应为 JSON 数组。";

            if (message.Contains("Unsupported BMS difficulty table JSON format", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Unsupported BMS difficulty table JSON token", StringComparison.OrdinalIgnoreCase))
                return "难度表 JSON 格式不受支持。请确认导入的是 bmstable 的 header.json 或表体 json。";

            if (message.Contains("did not contain any valid chart entries", StringComparison.OrdinalIgnoreCase))
                return "难度表文件里没有解析到有效谱面条目。";

            if (message.Contains("Unsupported difficulty table URL scheme", StringComparison.OrdinalIgnoreCase))
                return "链接协议不受支持。请使用 http、https，或本地文件路径。";

            if (message.Contains("Could not resolve base directory", StringComparison.OrdinalIgnoreCase))
                return "无法解析难度表文件的相对路径。请确认链接或本地目录结构完整。";

            return null;
        }

        private static string fallback(Exception exception)
        {
            string message = exception.Message.Trim();

            if (containsChinese(message))
                return message;

            return string.IsNullOrWhiteSpace(message)
                ? "发生未预期错误，请查看日志后重试。"
                : $"发生未预期错误：{message}";
        }

        private static bool containsChinese(string message)
            => message.Any(c => c >= 0x4E00 && c <= 0x9FFF);
    }
}
