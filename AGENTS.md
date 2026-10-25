# AGENTS.md — AI 编程助手指南

> 根据原 [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) 项目（作者 BobbyRenzobbi）由 ZCode（AI 编程助手）编写移植。感谢原作者的开源分享与 SPT 社区的开发文档，本项目得以完成向 SPT 4.0.13 的迁移。

本仓库是 FavoriteMusicPlayer（BepInEx 客户端插件，原 Bobby's Music Player 的 SPT 4.0.13 移植版）。给在此仓库工作的 AI 助手（或新工程师）的上下文。

## 硬性约束

- **游戏安装目录（默认 `E:\ETF`）绝对只读**。构建时从中引用 DLL（csproj 的 `GameDir` 属性可覆盖），绝不写入。发布物由用户手动复制进游戏。
- 产物目标框架是 **netstandard2.1**（SPT 4.0.x 官方客户端 mod 模板要求）。
- 插件 GUID `halfBreadPin.FavoriteMusicPlayer` **不可更改**——改了会导致用户配置文件失效，且不得改回原作者 GUID（与原版 mod 冲突）。
- 与原版 Bobby's Music Player **互斥**（补丁目标相同，同时安装会双重播放）。

## 仓库结构

```
src/                          源码（在此开发）
  ├─ FavoriteMusicPlayer.csproj     4.0.13 构建目标（GameDir=E:\ETF → dist\4.0.13\）
  ├─ FavoriteMusicPlayer.4_1.csproj 4.1.6 构建目标（SPT41 常量，GameDir=F:\EFT416 → dist\4.1.6\）
  ├─ Common.props                   公共构建配置（双 csproj 导入，勿在单侧 csproj 里改公共项）
  ├─ Plugin.cs                BepInEx 插件入口 + 点唱机 + 音频加载 + 时长限制 + SPT 版本兼容层
  ├─ Patches/                 全部 Harmony 补丁（13 个）
  └─ Utils/OverlayDebug.cs    DEBUG 构建的游戏内调试覆盖层
dist/<版本线>/FavoriteMusicPlayer/  构建产物（已 gitignore，不入库；经 Releases 分发）
docs/                          音乐文件夹对照文档（中/英）
tools/create_dist_tree.sh      重新生成两条版本线的 dist 文件夹树
README.md / README_EN.md       面向用户的说明（中/英）
```

## 构建与验证

```powershell
cd src
dotnet build FavoriteMusicPlayer.csproj -c Release       # SPT 4.0.13 版
dotnet build FavoriteMusicPlayer.4_1.csproj -c Release   # SPT 4.1.6 版
```

- .NET SDK 8+（当前机器 10.0.402），无需 Visual Studio，无 NuGet 包依赖（离线可构建）
- 双 csproj 同目录共存：各自的 obj/bin 子目录隔离（obj\4013 / obj\416），`Common.props` 里已统一排除 `obj/**;bin/**` 防止兄弟项目产物混入源码通配——**改版本文本输出路径时勿破坏此机制**
- `CS0618 isNetworkError/isHttpError 弃用` 警告是预期的（游戏 Unity 运行时下行为正确），不要"修复"成 `uwr.result`
- 已安装 `ilspycmd`（dotnet global tool）用于反编译游戏程序集

## 架构

**Plugin.cs**（BepInEx 5 BaseUnityPlugin）
- `Awake()`：创建音乐文件夹 → 枚举音频（白名单扩展名）→ 绑定配置（BindLocalized 双语）→ 启用 13 个补丁
- `Update()`：菜单/局内状态机、3 个自建 AudioSource（soundtrack/spawn/combat）、战斗淡入淡出（lerp）、音量计算（室内 × 耳机倍率）、时长限制任务处理（ProcessDucks/ProcessSpawnFade）
- 音频加载：`UnityWebRequestMultimedia.GetAudioClip("file://...")`
- 点唱机：`SoundtrackJukebox` / `MenuMusicJukebox`，小键盘 2/4/5/6，`StaticManager.WaitSeconds` 排下一首

**音乐文件夹映射**（`Plugin.MapFolderNames`，大小写不敏感，未知 ID 回退默认歌单）：
`bigmap→customs, factory4_day|night→factory, Interchange→interchange, laboratory→labs, Labyrinth→labyrinth, Lighthouse→lighthouse, RezervBase→reserve, Sandbox|Sandbox_high→ground_zero, Shoreline→shoreline, TarkovStreets→streets, Terminal→terminal, Woods→woods`

## SPT 4.1.6 兼容层（2026-10 对 4.1.6 / 基座 0.16.9.5 核验）

4.1 做了大规模客户端反混淆，版本差异集中在 `Plugin.cs` 顶部的兼容层（常量 + 包装方法）与 `MenuMusicPatch` 的字段注入处，新增版本差异优先扩展该区域：

| 4.0.13 | 4.1.6（SPT41 分支） | 位置 |
|---|---|---|
| `GUISounds.method_3()` | `PlayMenuBackgroundMusic()` | MenuMusicPlayMethod 常量 |
| `GUISounds.method_8()` | `StopAudioCallbackCoroutine()` | MenuMusicStopCoroutineMethod 常量 |
| 字段 `audioClip_0` | `_mainMenuMusic` | MenuMusicPatch Prefix 注入（`___audioClip_0` / `____mainMenuMusic`，注意下划线数量） |
| `FlyingBulletSoundPlayer.method_3` | `FrameCoroutine`（签名同） | BulletWhizCoroutineMethod 常量 |
| `HeadphonesItemClass` | `EFT.InventoryLogic.Headphones`（家族规律 XxxItemClass→Xxx） | Plugin/HeadsetPatch 的 using 别名 |

其余 12 个补丁 + 全部支撑 API（method_7、audioSource_0/1/3/4、PlaySound、GetUIClip/GetEndGameClip、Player 三方法、GameWorld、EnvironmentType 等）**两版完全同名**。`FireBullet` 的 ammo 参数类型 4.1 变为 `Ammo`，但我们只按参数名绑定 `shotPosition`，不受影响。**坑（4.1.6 实测）**：`Grenade` 在 4.1.6 多了一个同名无参实例方法 `Explosion()`（内部调静态版），`AccessTools.Method` 按名取会拿到它导致 `grenadePosition` 绑定失败——已改为 `GetMethods().First(m => m.Name==... && m.IsStatic)` 按 static 筛选；同类风险（按名取方法遇新增重载）在游戏更新后需警惕。Awake 中的补丁启用已改为逐个 try/catch（单个失败不炸断整个插件，那曾让菜单音乐竞态雪上加霜）。裁剪不可行的结论两版通用（UnityPlayer.dll 逐字节相同，MD5 75b4bf8f）。

## 补丁目标清单（2026-10 已对 4.0.13 反编译逐一核验）

| 补丁类 | 目标方法 | 要点 |
|---|---|---|
| MenuMusicPatch | `GUISounds.method_3()` | 注入字段 `audioSource_3`、`audioClip_0` |
| MenuMusicMethod8Patch | `GUISounds.method_8()` | 停 WaitSeconds 协程 |
| StopMenuMusicPatch | `GUISounds.StopMenuBackgroundMusicWithDelay(float transitionTime, ...)` | 按名绑定 |
| UISoundsPatch | `UISoundsWrapper.GetUIClip(EUISoundType soundType)` | 按名绑定 |
| RaidEndMusicPatch | `UISoundsWrapper.GetEndGameClip(EEndGameSoundType soundType)` | 死亡=ArenaLose，撤离=ArenaWin |
| ShotAtPatch | `FlyingBulletSoundPlayer.method_3(string, Vector3, Vector3, float)` | Postfix 无注入 |
| PlayerFiringPatch | `Player.OnMakingShot(IWeapon, Vector3)` | 只用 `__instance` |
| DamageTakenPatch | `Player.ReceiveDamage(float, EBodyPart, EDamageType type, ...)` | 按名绑定 `type` |
| ShotFiredNearPatch | `WeaponSoundPlayer.FireBullet(AmmoItemClass, Vector3 shotPosition, ...)` | 按名绑定 |
| GrenadePatch | `Grenade.Explosion(Grenade, IExplosiveItem, Vector3 grenadePosition, ...)` static | 按名绑定 |
| OnGameWorldStartPatch | `GameWorld.OnGameStarted()` | public virtual |
| OnGameWorldDisposePatch | `GameWorld.Dispose()` | public virtual |
| PlaySoundDuckPatch | `GUISounds.PlaySound(AudioClip clip, bool single, bool commonUiSound, float volume)` | Prefix 不拦截；注入 audioSource_0/1/4 |

未启用：`HeadsetPatch`（原版即未挂载，源码对等保留）。

## 游戏更新到新 SPT 版本时的核验流程

混淆名（`method_3/7/8`、`audioSource_0/1/3/4`、`audioClip_0`）在 0.15.x→0.16.9 间稳定，但**不保证下个版本不变**：

1. `dotnet build` — 公共 API 改名编译期即可发现
2. 编译过不代表运行时对：`ilspycmd -t GUISounds "<Managed>\Assembly-CSharp.dll"` 比对上表签名（重点是**参数名**，Harmony 按名绑定）
3. 让用户跑一次游戏，读 `<游戏目录>\BepInEx\LogOutput.log`（只读）：绑定失败有 Harmony `skipped` 警告；本 mod 找不到目标会打 `找不到 ...` 错误

## 本地化机制

- 配置键名与 cfg Section **一律英文且不可改**；中文只经 `ConfigurationManagerAttributes` 的 `DispName`/`Description`/`Category` 覆盖显示层
- 所有配置绑定走 `Plugin.BindLocalized`；新增配置必须用它，并保持绑定顺序满足 0→1→2→3→4→5 首次出现（ConfigurationManager 分类显示顺序 = 首次绑定顺序）
- 枚举下拉值显示枚举成员名，未翻译；枚举成员名会持久化进 cfg，**不能**改名

## 时长限制机制（定时淡出）

- **裁剪方案已判死，勿再尝试**：该定制引擎（Unity 2022.3.43，无 AudioDataLoadType）上 `AudioClip.Create+SetData` 无论参数一律产出 0 秒副本（2026-10 三轮实测；NAudio/ffmpeg 第三方方案也已论证并被用户否决——维持定时淡出，勿再提议）
- 出生音乐：`ProcessSpawnFade` 计时，最后 2s 乘性淡出 → Stop
- UI/死亡撤离：`RegisterLimitedClip` 登记 + `PlaySoundDuckPatch` 钩 `GUISounds.PlaySound` → `ProcessDucks` 每帧：2s 淡出 → Stop → 恢复音量（PlayOneShot 只能动所在声道；掐断瞬间同声道其它一次性音效会被波及，窗口极小）
- **同声道单任务制（勿改回可叠加）**：`ScheduleDuck` 对已有任务的声道先移除旧任务、**继承最初基准音量**并立即恢复，再以新触发重置计时窗口——修复"快速连续交易时旧任务 Stop() 掐死新音效 + 把淡出音量误当基准导致后续音效逐单变哑"的恶性循环（2026-10 用户实测发现）
- 配置（"5. Duration Limits"）：出生/死亡撤离/UI，0=不限；主菜单/环境/战斗**故意不加**（用户决策）
- 0 = 时不登记/不计时，零干预
- **UI 音效时长即时生效**：`UISoundsMaxLength.SettingChanged → Plugin.ReregisterUiSoundLimits()` 整体重登记（仅对之后触发的音效生效，不追溯正在播放的——用户决策不做追溯掐短；顺带清掉的死亡/撤离登记下次触发自动重建）

## 已知行为细节（改动前必读）

- 环境音乐默认音量 0.05 是刻意压低的
- 出生音乐播完（或限时停止）才允许环境音乐开始（`PlaySoundtrack` 检查 `spawnAudioSource.isPlaying`）
- 菜单歌单在 raid 结束返回菜单时重建；UI 音效在此时重新加载
- 音乐枚举只认 `Plugin.AudioExtensions` 白名单，文件夹里的 txt 被忽略
- 任务完成同时触发 QuestSubTrackComplete + QuestCompleted 两个独立音效事件（各文件夹独立"有音乐才替换"）
- 排障：`LogOutput.log` 含 NUL 字节，git-bash 的 grep 需 `-a`
