using TraeAccountSwitcher.Domain.ValueObjects;

namespace TraeAccountSwitcher.Application.Models;

/// <summary>
/// SOLO 数据完整性检查报告。
/// </summary>
/// <param name="Current">当前清单（任务库不存在时为 null）。</param>
/// <param name="Baseline">基线清单（尚未建立时为 null）。</param>
/// <param name="State">总体状态。</param>
public sealed record SoloIntegrityReport(
    SoloDataManifest? Current,
    SoloDataManifest? Baseline,
    SoloIntegrityState State);

/// <summary>完整性状态。</summary>
public enum SoloIntegrityState
{
    /// <summary>任务库文件不存在。</summary>
    NotFound,

    /// <summary>尚未建立基线（首次运行）。</summary>
    NoBaseline,

    /// <summary>与基线一致。</summary>
    Ok,

    /// <summary>与基线不一致（任务库发生变化，需要用户确认后重建基线）。</summary>
    Changed
}
