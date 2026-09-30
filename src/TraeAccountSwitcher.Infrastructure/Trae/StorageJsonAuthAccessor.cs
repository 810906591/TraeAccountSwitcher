using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TraeAccountSwitcher.Application.Repositories;
using TraeAccountSwitcher.Domain.Exceptions;
using TraeAccountSwitcher.Domain.Services;
using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Infrastructure.Trae;

/// <summary>
/// storage.json 登录态访问器实现：仅读取/替换 <see cref="AuthKeyPolicy"/> 定义的登录态键，
/// 写回采用“临时文件 + File.Replace”原子策略，避免写入中断导致配置损坏。
/// </summary>
public sealed class StorageJsonAuthAccessor : ITraeAuthAccessor
{
    private readonly TraePathLocator _paths;
    private readonly StateVscdbReader _stateReader;
    private readonly ILogger<StorageJsonAuthAccessor> _logger;

    /// <summary>创建 storage.json 访问器。</summary>
    public StorageJsonAuthAccessor(TraePathLocator paths, StateVscdbReader stateReader, ILogger<StorageJsonAuthAccessor> logger)
    {
        _paths = paths;
        _stateReader = stateReader;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsTraeDataAvailable()
        => _paths.StorageJsonPath is not null && File.Exists(_paths.StorageJsonPath);

    /// <inheritdoc />
    public Task<AuthBundle?> ReadAuthBundleAsync(CancellationToken cancellationToken = default)
    {
        var root = ReadRootObject();
        if (root is null)
        {
            return Task.FromResult<AuthBundle?>(null);
        }

        var entries = new Dictionary<string, string>();
        foreach (var (key, node) in root)
        {
            if (AuthKeyPolicy.IsAuthKey(key) && node is JsonValue value && value.TryGetValue<string>(out var raw))
            {
                entries[key] = raw;
            }
        }

        return Task.FromResult<AuthBundle?>(entries.Count == 0 ? null : new AuthBundle(entries));
    }

    /// <inheritdoc />
    public async Task WriteAuthBundleAsync(AuthBundle bundle, CancellationToken cancellationToken = default)
    {
        var storagePath = _paths.StorageJsonPath
            ?? throw new TraeAuthException("未找到 Trae 数据目录，无法写回登录态。");

        var root = ReadRootObject()
            ?? throw new TraeAuthException($"storage.json 缺失或格式非法：{storagePath}");

        // 移除全部旧登录态键，再写入目标账号的键，保证账号间登录态完全隔离
        foreach (var staleKey in AuthKeyPolicy.SelectAuthKeys(root.Select(kv => kv.Key)).ToList())
        {
            root.Remove(staleKey);
        }

        foreach (var (key, value) in bundle.Entries)
        {
            root[key] = value;
        }

        await WriteAtomicallyAsync(storagePath, root, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("登录态写回完成：{Count} 个认证键", bundle.Entries.Count);
    }

    /// <inheritdoc />
    public Task<string?> ReadCurrentEmailAsync(CancellationToken cancellationToken = default)
        => _stateReader.ReadLoginEmailAsync(cancellationToken);

    private JsonObject? ReadRootObject()
    {
        var storagePath = _paths.StorageJsonPath;
        if (storagePath is null || !File.Exists(storagePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(storagePath);
            var node = JsonNode.Parse(stream);
            return node as JsonObject;
        }
        catch (JsonException ex)
        {
            throw new TraeAuthException("storage.json 解析失败，文件可能被其他进程占用或已损坏。", ex);
        }
    }

    private static async Task WriteAtomicallyAsync(string storagePath, JsonObject root, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(storagePath)!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(storagePath)}.switcher.tmp");
        var backupPath = Path.Combine(directory, $".{Path.GetFileName(storagePath)}.switcher.bak");

        var options = new JsonWriterOptions { Indented = false };
        await using (var stream = File.Create(tempPath))
        {
            using var writer = new Utf8JsonWriter(stream, options);
            root.WriteTo(writer);
        }

        if (File.Exists(storagePath))
        {
            File.Replace(tempPath, storagePath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, storagePath, overwrite: true);
        }
    }
}
