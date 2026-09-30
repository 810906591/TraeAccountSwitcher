using System.IO;
using Microsoft.Extensions.Logging;

namespace TraeAccountSwitcher.Infrastructure.Trae;

/// <summary>
/// Trae CN 路径定位器：集中发现 Trae 数据目录、storage.json、SOLO 任务库与安装路径。
/// </summary>
public sealed class TraePathLocator
{
    /// <summary>storage.json 相对路径。</summary>
    public static readonly string StorageJsonRelative = Path.Combine("User", "globalStorage", "storage.json");

    /// <summary>SOLO 任务库相对路径。</summary>
    public static readonly string SoloDatabaseRelative = Path.Combine("ModularData", "ai-agent", "database.db");

    /// <summary>登录历史数据库相对路径（state.vscdb）。</summary>
    public static readonly string StateVscdbRelative = Path.Combine("User", "globalStorage", "state.vscdb");

    /// <summary>创建路径定位器。</summary>
    /// <param name="logger">日志。</param>
    /// <param name="dataDirectoryOverride">数据目录覆盖值（测试或自定义场景）；null 时自动探测。</param>
    /// <param name="executablePathOverride">可执行文件路径覆盖值；null 时自动探测。</param>
    public TraePathLocator(ILogger<TraePathLocator> logger, string? dataDirectoryOverride = null, string? executablePathOverride = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        TraeDataDirectory = dataDirectoryOverride ?? ResolveTraeDataDirectory();
        ExecutablePath = executablePathOverride ?? ResolveExecutablePath();
    }

    /// <summary>日志。</summary>
    public ILogger<TraePathLocator> Logger { get; }

    /// <summary>Trae 数据根目录（如 %APPDATA%\Trae CN）；未找到为 null。</summary>
    public string? TraeDataDirectory { get; }

    /// <summary>storage.json 完整路径；未找到数据目录为 null。</summary>
    public string? StorageJsonPath => TraeDataDirectory is null ? null : Path.Combine(TraeDataDirectory, StorageJsonRelative);

    /// <summary>SOLO 任务库完整路径；未找到数据目录为 null。</summary>
    public string? SoloDatabasePath => TraeDataDirectory is null ? null : Path.Combine(TraeDataDirectory, SoloDatabaseRelative);

    /// <summary>state.vscdb 完整路径；未找到数据目录为 null。</summary>
    public string? StateVscdbPath => TraeDataDirectory is null ? null : Path.Combine(TraeDataDirectory, StateVscdbRelative);

    /// <summary>Trae CN 可执行文件路径；未找到为 null。</summary>
    public string? ExecutablePath { get; }

    private static string? ResolveTraeDataDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var name in new[] { "Trae CN", "Trae" })
        {
            var dir = Path.Combine(appData, name);
            if (Directory.Exists(dir))
            {
                return dir;
            }
        }

        return null;
    }

    private static string? ResolveExecutablePath()
    {
        var localPrograms = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidate = Path.Combine(localPrograms, "Programs", "Trae CN", "Trae CN.exe");
        return File.Exists(candidate) ? candidate : null;
    }
}
