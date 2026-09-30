using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace TraeAccountSwitcher.Infrastructure.Trae;

/// <summary>
/// state.vscdb 只读访问器：用于读取当前登录邮箱（iCubeSaasLoginHistory）等展示信息。
/// 采用“复制到临时目录后打开”的策略，避免与运行中的 Trae 争抢 SQLite 文件锁。
/// </summary>
public sealed class StateVscdbReader
{
    private readonly TraePathLocator _paths;
    private readonly ILogger<StateVscdbReader> _logger;

    /// <summary>创建 state.vscdb 只读访问器。</summary>
    public StateVscdbReader(TraePathLocator paths, ILogger<StateVscdbReader> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    /// <summary>读取当前登录邮箱；数据库缺失或键不存在时返回 null。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<string?> ReadLoginEmailAsync(CancellationToken cancellationToken = default)
    {
        var source = _paths.StateVscdbPath;
        if (source is null || !File.Exists(source))
        {
            return null;
        }

        var tempCopy = Path.Combine(Path.GetTempPath(), $"trae-switcher-state-{Guid.NewGuid():N}.vscdb");
        try
        {
            await Task.Run(() => File.Copy(source, tempCopy, overwrite: true), cancellationToken).ConfigureAwait(false);
            return await Task.Run(() => QueryLoginEmail(tempCopy), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException)
        {
            _logger.LogWarning(ex, "读取登录邮箱失败（不影响核心切换功能）");
            return null;
        }
        finally
        {
            TryDelete(tempCopy);
        }
    }

    private static string? QueryLoginEmail(string dbPath)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM ItemTable WHERE key = $key LIMIT 1";
        command.Parameters.AddWithValue("$key", "iCubeSaasLoginHistory");

        var raw = command.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("email", out var email) ? email.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 临时文件删除失败不影响主流程，留给系统临时目录清理
        }
    }
}
