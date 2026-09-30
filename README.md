# TRAE 账号切换器（TraeAccountSwitcher）

在同一台 Windows 计算机上实现两个 TRAE（Trae CN）账号的轮流登录，并确保 **SOLO 任务列表数据在本机持久化存储、不受账号切换影响**。

## 核心原理

| 数据 | 位置 | 本工具的处理方式 |
|------|------|------------------|
| 登录凭据 | `%APPDATA%\Trae CN\User\globalStorage\storage.json` 中 `iCubeAuthInfo://*`、`iCubeServerData://*` 键 | **切换时仅替换这些键**（原子写入 + DPAPI 加密快照兜底） |
| SOLO 任务库 | `%APPDATA%\Trae CN\ModularData\ai-agent\database.db` | **全程不触碰**；切换前后以 SHA-256 指纹校验未被误改 |
| 会话草稿/UI 设置 | `%APPDATA%\Trae CN\User\globalStorage\state.vscdb` | 不触碰（不同账号的数据按用户 ID 命名空间共存） |

由于 SOLO 任务库与登录态物理分离，切换账号后 Trae 重新打开时加载的是**同一份本地任务库**——任务列表无缝显示、完全一致。

## 用户操作指引

### 首次使用（绑定两个账号，约 3 分钟）

1. 打开本工具，确认顶部状态条显示 Trae 数据目录已找到。
2. 在 Trae 中正常登录**账号 A**（当前已登录则跳过此步）。
3. 回到本工具，在「① 捕获 / 绑定账号」中输入档案名称（如“账号A”），点击 **捕获当前账号**。
4. 点击 **启动 Trae（去登录）**，在 Trae 中 **退出当前账号 → 登录账号 B**。
5. 回到本工具，输入档案名称（如“账号B”），再次点击 **捕获当前账号**。
6. 点击 **重建基线**：记录当前 SOLO 任务库的 SHA-256 指纹（首次必做，用于后续完整性防线）。
7. （可选但建议）点击 **立即备份**：对 SOLO 任务库做一次带校验的完整备份。

### 日常切换（两个账号轮流登录）

1. 保持 Trae 可处于运行状态（工具会自动关闭并重启）。
2. 在「② 账号列表」中点击目标账号卡片上的 **切换到此账号**。
3. 工具自动执行：校验 SOLO 完整性 → 快照当前登录态 → 优雅关闭 Trae → 仅替换登录凭据键 → 校验写回 → 重启 Trae。
4. 切换完成后 Trae 自动打开，登录身份已切换，**SOLO 任务列表与切换前完全一致**。
5. 再次切换回另一账号：重复步骤 2 即可，来回次数不限。

### 切换过程中的注意事项（保持 SOLO 任务列表不变的关键）

- 切换进行中（界面有遮罩提示）请**不要手动打开或关闭 Trae**。
- 切换**不会**删除、移动或修改任何任务数据；两个账号共享同一份本地任务库。
- 若提示“SOLO 任务库与基线不一致”：这是 Trae 正常使用导致任务库变化（属预期）。请先在 Trae 中确认任务数据正常，再点击 **重建基线** 确认新指纹。
- 若切换中途断电/异常退出：重新打开工具再执行一次切换即可，工具会从「切换前自动快照」回滚登录态；任务库自始至终未被写入，不存在损坏风险。

### 数据安全与完整性机制

- **登录凭据**：以 DPAPI（当前 Windows 用户作用域）加密存储于 `%LOCALAPPDATA%\TraeAccountSwitcher\profiles\`，其他 Windows 账户无法解密。
- **切换前快照**：每次切换自动加密快照当前登录态，失败自动回滚。
- **SOLO 指纹基线**：切换前比对 SHA-256，检测任务库是否被意外改动。
- **SOLO 备份**：`%LOCALAPPDATA%\TraeAccountSwitcher\backups\`，保留最近 5 份，每份附 SHA-256 边车校验。
- **原子写回**：storage.json 采用“临时文件 + File.Replace”写入，中断不会产生半成品配置。
- **操作日志**：`%LOCALAPPDATA%\TraeAccountSwitcher\logs\`，保留 14 天。

## 构建与运行

```bash
dotnet build TraeAccountSwitcher.sln -c Debug
dotnet test  tests/TraeAccountSwitcher.Tests/TraeAccountSwitcher.Tests.csproj
# 运行
src/TraeAccountSwitcher.Presentation/bin/Debug/net8.0-windows/TraeAccountSwitcher.exe
```

依赖：.NET 8 Desktop Runtime（net8.0-windows）、Windows 10/11、本机已安装 Trae CN。

## 项目结构（DDD 四层）

```
src/
├── TraeAccountSwitcher.Domain/          # 领域层：实体（AccountProfile）、值对象（AuthBundle/SoloDataManifest/BackupRecord）、
│                                        #   领域策略（AuthKeyPolicy）、领域异常 —— 零外部依赖
├── TraeAccountSwitcher.Application/     # 应用层：仓储接口、用例（捕获/切换/校验/备份/总览）
├── TraeAccountSwitcher.Infrastructure/  # 基础设施层：storage.json 访问、DPAPI 档案仓储、快照备份、
│                                        #   SOLO 完整性守卫、Trae 进程管理、Autofac 模块
└── TraeAccountSwitcher.Presentation/    # 呈现层：WPF + ReactiveUI（账号卡片/切换/SOLO 守护/操作指引/日志）
tests/TraeAccountSwitcher.Tests/         # 单元测试（28 项，覆盖键提取回写、档案加密存储、备份校验、切换与回滚流程）
```

依赖方向：Presentation → Application → Domain，Infrastructure 实现 Application 仓储接口，DI 全部构造器注入（Autofac 按层分组 Module）。

## 已知边界

- 仅支持 Trae 国内版（Trae CN）；国际版目录结构相同，可按需扩展 `TraePathLocator`。
- TRAE 登录页 URL 由服务端动态下发（`GetLoginGuidanceForBytedance`），令牌交换走 Trae 自身后端，外部 WebView 无法伪造登录——因此“绑定第二个账号”采用 **工具引导 + Trae 原生登录 + 自动捕获** 方案，可靠性等同官方登录。
- 工具自身数据（档案/备份/基线/日志）位于 `%LOCALAPPDATA%\TraeAccountSwitcher`，与 Trae 数据完全隔离。
