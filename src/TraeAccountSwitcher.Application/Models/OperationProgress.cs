namespace TraeAccountSwitcher.Application.Models;

/// <summary>
/// 操作进度消息：用于向界面反馈长耗时操作的当前步骤。
/// </summary>
/// <param name="Message">进度描述（中文）。</param>
/// <param name="Percent">完成百分比（0-100）；无法估计时为 null。</param>
public sealed record OperationProgress(string Message, int? Percent = null);
