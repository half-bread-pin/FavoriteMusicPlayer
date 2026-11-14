# AGENTS.md — AI 编程助手指南

> 根据原 [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) 项目（作者 BobbyRenzobbi）由 ZCode（AI 编程助手）编写移植。感谢原作者的开源分享与 SPT 社区的开发文档，本项目得以完成向 SPT 4.0.13 的迁移。

本仓库是 FavoriteMusicPlayer（BepInEx 客户端插件，原 Bobby's Music Player 的 SPT 4.0.13 移植版）。给在此仓库工作的 AI 助手（或新工程师）的上下文。

## 硬性约束

- **游戏安装目录（默认 `E:\ETF`）绝对只读**。构建时从中引用 DLL（csproj 的 `GameDir` 属性可覆盖），绝不写入。发布物由用户手动复制进游戏。
- 产物目标框架是 **netstandard2.1**（SPT 4.0.x 官方客户端 mod 模板要求）。
- 插件 GUID `com.halfbreadpin.favoritemusicplayer` **不可更改**（cfg 配置文件按 GUID 命名，改了会导致用户配置失效；与原版 mod 冲突检测也靠 GUID——不得改回原作者 GUID）。**插件名（BepInPlugin Name）2026-11 用户决策：去掉作者前缀改为 `FavoriteMusicPlayer`**（ConfigurationManager 面板标题 = Name+Version，即 "FavoriteMusicPlayer 1.1.0"，版本号升级自动跟随——升版本只改 `Plugin.cs` 的 BepInPlugin Version 与 `Common.props` 的 Version/AssemblyVersion/FileVersion 三处，cfg 不受影响）；原"插件名 halfbreadpin-FavoriteMusicPlayer 不可更改"约束作废。BepInEx 日志源随插件名变为 `FavoriteMusicPlayer`（排障 grep 不受影响）。sp-mods/The Forge 的 `Username-ModName` 规范继续用于**发布压缩包命名**（与游戏内标题无关）。
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
dist/<版本线>/BepInEx/plugins/FavoriteMusicPlayer/  构建产物（已 gitignore，不入库；经 Releases 分发，压缩包解压即入 SPT 根目录）
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
- **发布流程**：`dotnet build`（CopyToDist 只复制 DLL+LICENSE）→ 运行 `tools/create_dist_tree.sh` 生成完整音乐文件夹树与各文件夹的双语 `aboutThisFolder.txt`（新增音乐文件夹必须同步加进该脚本，v1.7.0 加 HideoutMusic 时已同步）→ 打压缩包。漏跑脚本会发布出"只有 DLL"的残缺压缩包
- 双 csproj 同目录共存：各自的 obj/bin 子目录隔离（obj\4013 / obj\416），`Common.props` 里已统一排除 `obj/**;bin/**` 防止兄弟项目产物混入源码通配——**改版本文本输出路径时勿破坏此机制**
- `CS0618 isNetworkError/isHttpError 弃用` 警告是预期的（游戏 Unity 运行时下行为正确），不要"修复"成 `uwr.result`
- 已安装 `ilspycmd`（dotnet global tool）用于反编译游戏程序集

## 架构

**Plugin.cs**（BepInEx 5 BaseUnityPlugin）
- `Awake()`：创建音乐文件夹 → 枚举音频（白名单扩展名）→ 绑定配置（BindLocalized 双语）→ 启用 13 个补丁
- `Update()`：菜单/局内/**藏身处**状态机、3 个 raid 自建 AudioSource（soundtrack/spawn/combat）+ 1 个藏身处 AudioSource、战斗淡入淡出（lerp）、音量计算（室内 × 耳机倍率）、时长限制任务处理（ProcessDucks/ProcessSpawnFade）
- 音频加载：`UnityWebRequestMultimedia.GetAudioClip("file://...")`
- 点唱机：`SoundtrackJukebox` / `MenuMusicJukebox` / `HideoutMusicJukebox`，小键盘 2/4/5/6，`StaticManager.WaitSeconds` 排下一首
- 新增引用（Common.props）：`DOTween.dll`（藏身处主菜单续播 Kill 音量渐隐 tween）

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
- 所有配置绑定走 `Plugin.BindLocalized`；新增配置必须用它，并保持绑定顺序满足 0→1→2→3→4→5→6 首次出现（ConfigurationManager 分类显示顺序 = 首次绑定顺序）
- **枚举下拉本地化（v1.7.2 定稿，勿再改路线）**：原生 ConfigurationManager 的枚举下拉框（`SettingFieldDrawer.ObjectToGuiContent`）本就支持读取成员上的 `System.ComponentModel.DescriptionAttribute` 并显示其 `Description` 文本（官方入口，已对游戏内 CM.dll 反编译核验；netstandard2.1 插件发出的该属性与 CM 读取的类型经 System.dll 转发器身份一致）。模组用 `Plugin.LocalizedDescriptionAttribute`（DescriptionAttribute 子类，`override Description` 按当前语言返回 en/zh）挂在枚举成员上即可——**零自绘、零反射**。显示时机：下拉按钮标签逐帧重算（切语言立即生效）；弹出列表项由 CM 的 `_comboBoxCache` 缓存，重开 CM 窗口后重本地化（与"切换语言后重新打开面板"提示一致）。成员名仍持久化进 cfg 不可改名（显示与持久化解耦）。**否决路线存档（勿再尝试）**：v1.7.0 `GUILayout.Toolbar` 横排按钮（交互被用户否决）；v1.7.1 反射注入 `Utilities.ComboBox.CurrentDropdownDrawer` 复刻原生下拉（实测失效——该钩子由原生 `DrawComboboxField` 每帧重挂、CM 调用后置空，一次性赋值只闪现一帧）。该定制引擎的 Unity 模块无 GenericMenu；`CustomDrawer` 挂上即整体绕过原生 DrawEnumField（这也是 v1.7.x 出问题的前提）

## 时长限制机制（定时淡出）

- **裁剪方案已判死，勿再尝试**：该定制引擎（Unity 2022.3.43，无 AudioDataLoadType）上 `AudioClip.Create+SetData` 无论参数一律产出 0 秒副本（2026-10 三轮实测；NAudio/ffmpeg 第三方方案也已论证并被用户否决——维持定时淡出，勿再提议）
- 出生音乐：`ProcessSpawnFade` 计时，最后 2s 乘性淡出 → Stop
- UI/死亡撤离：`RegisterLimitedClip` 登记 + `PlaySoundDuckPatch` 钩 `GUISounds.PlaySound` → `ProcessDucks` 每帧：2s 淡出 → Stop → 恢复音量（PlayOneShot 只能动所在声道；掐断瞬间同声道其它一次性音效会被波及，窗口极小）
- **同声道单任务制（勿改回可叠加）**：`ScheduleDuck` 对已有任务的声道先移除旧任务、**继承最初基准音量**并立即恢复，再以新触发重置计时窗口——修复"快速连续交易时旧任务 Stop() 掐死新音效 + 把淡出音量误当基准导致后续音效逐单变哑"的恶性循环（2026-10 用户实测发现）
- 配置（"5. Duration Limits"）：出生/死亡撤离/UI，0=不限；主菜单/环境/战斗**故意不加**（用户决策）
- 0 = 时不登记/不计时，零干预
- **UI 音效时长即时生效**：`UISoundsMaxLength.SettingChanged → Plugin.ReregisterUiSoundLimits()` 整体重登记（仅对之后触发的音效生效，不追溯正在播放的——用户决策不做追溯掐短；顺带清掉的死亡/撤离登记下次触发自动重建）

## 藏身处音乐机制（v1.7.0 引入；v1.7.2 根因修复 + 双歌单暂停/恢复；4.0.13 / 4.1.6 通用，无版本分支）

游戏侧事实（反编译 + 日志核验，改动前必读）：
- **`GameWorld.OnGameStarted()` 在藏身处从不被调用**（两版一致）：全部 4 个调用点都在 raid/联网路径 → `Plugin.InRaid` 在藏身处恒为 false。因此 `Update()` 里藏身处分支必须放在 `!InRaid` 分支之前（v1.7.0 曾放错序导致藏身处音乐完全不播）
- **正常退出藏身处从不卸载藏身处世界**（v1.7.2 修复的第二个根因，日志实锤）：退出按钮只调 `TarkovApplication.GClass2301.HideHideout`（刷服务器队列+毁相机），GameWorld/AbstractGame 单例被刻意**缓存供快速重进**（再次进入无加载画面即此设计）——`LocationId=="hideout"`、`MainPlayer.Location=="hideout"` 在回到主菜单后**永远为 true**，不能作为进出信号（v1.7.0/1.7.1 退出后菜单期仍被当作藏身处：歌单不停、菜单声道被持续压 0 与游戏 `DOFade(1)` 打架）。单例只在 `UnloadHideout` 流程释放（开 raid/登出/重连/档案重载/后端重建，经 CompositeDisposable → `gameWorld.Dispose()` + 场景卸载）
- **可靠的进出检测公式（v1.7.2）**：`worldInHideout = GameWorld.LocationId=="hideout"` && (`GUISounds.HideoutSoundActive`（公有属性，两版同名——4.0.13 反编译 + 4.1.6 二进制双重核验；藏身处界面显示时 true，`MenuScreen` 显示即回主菜单时置 false）‖ `GameStatus.Running`（`BaseLocalGame.method_4` 加载画面期间的过渡态，首次进入从点击藏身处起即判定 true；`HideoutGame.vmethod_6` 完成后置 `Started`））。缓存式重进由 `HideoutSoundActive` 翻 true 即刻生效；`MainPlayer.Location` 兜底已删除（它同样残留）
- **游戏从不在藏身处退出后重启菜单音乐**（关键！反编译核验）：退出→回菜单只 `MenuScreen` 的 `DOFade` 音量渐隐回 1；`GUISounds.method_3`（重启菜单音乐）只在登录后/对战后调用（`TarkovApplication.method_54→method_19→method_20`）。raid 部署才会 `StopMenuBackgroundMusicWithDelay` 全停（之后由对战后流程重启）→ **模组自己暂停的菜单歌单必须由模组自己恢复**
- 进入藏身处时游戏对菜单声道做 DOTween 音量渐隐到 0（两版同为 `audioSource_3.DOFade(x,2f)`——`DOTween.Kill` 两版通用有效），播放与排曲链不断。raid 的 `Audio.RadioSystem` 在藏身处休眠（`StartBroadcast` 只在 raid 路径；两版 globals.json 所有电台默认禁用）；`StaticManager` 为 `HideFlags.DontSave` 持久单例（跨场景存活，协程宿主安全）
- 休息区收音机 = 场景 `Appliance` + `EFT.Hideout.AudioAmbiance`（`ContinuingPlaybackSettings.TrackList` 随机播放、断电静音、状态存 PlayerPrefs），走自己的 3D `AudioArray` 声道——与模组所有声道零交集，**技术上互不干扰**，仅存在"同时可听"的叠加（用户决策：不自动处理，README 说明手动开关）

模组实现（全部在 Plugin.cs；`Update()` 顶部检测 + 进出边沿 + `UpdateHideoutMusic` 不变式）：
- 配置分组 "6. Hideout Music Settings / 6. 藏身处音乐设置"：来源枚举 `EHideoutMusicPlaylist{MenuMusicPlaylist, HideoutPlaylist}`、音量 float 0–1 **默认 0（=关闭）**、歌单长度 int 0–600 默认 60（isAdvanced，**仅对藏身处歌单来源生效**）。文件夹 `HideoutMusic\`，加载镜像主菜单加载器（do-while 至少 1 首）
- **进出边沿（一次性动作）**：`OnHideoutEntered` = `MenuMusicJukebox.PauseForHideout()`（暂停主菜单歌单：Pause+停链+记剩余；未在播/用户已自暂停时 no-op）；`OnHideoutLeft` = `ParkHideoutPlaylist()`（停靠藏身处歌单）+ `MenuMusicJukebox.ResumeForHideout()`（恢复主菜单歌单）+ 重置 Kill 标志（退出时的 `DOFade(1)` 必须活着以恢复菜单音量）
- **藏身处内每帧（`UpdateHideoutMusic`，`Status==Started` 之后）**：藏身处歌单来源 = 菜单歌单保持暂停 + 懒建 `Audio.hideoutAudioSource`（不进游戏 mixer）+ `PrepareHideoutAudioClips` 随机填充 + 音量直连配置值（**刻意不乘室内/耳机倍率**）+ `ResumeIfParked`（重进续播）+ WaitSeconds 链播放；主菜单歌单来源 = `ResumeForHideout`（加载完成后从暂停处续播，幂等）+ `FadeMenuMusicInHideout`（一次性 `DOTween.Kill` 游戏渐隐 + 每帧 `MoveTowards` 到藏身处音量）。加载期（`Running`）维持静音（用户规格）
- **停靠语义**：`ParkHideoutPlaylist` 暂停声道+停链+记剩余，曲目数组/游标/AudioClip 全保留（AudioSource 挂在插件 GameObject 上跨场景存活）——再次进入（含开过 raid 再回来）从暂停处续播；用户快捷键暂停的也按停靠吸收。`HideoutMusicLength.SettingChanged → ResetHideoutPlaylist()` 丢弃停靠歌单按新长度重建
- 点唱机路由：`Plugin.HideoutMusicActive`（在藏身处且来源=藏身处歌单）时 `MenuMusicControls` 让位给 `HideoutMusicJukebox`；主菜单来源时快捷键控制续播中的主菜单音乐。手动点唱机操作会清掉藏身处暂停态（用户接管播放）
- `ProcessDucks` 在藏身处也运行（UI 音效时长限制在藏身处实际生效）
- **内存驻留（刻意，非泄露）**：藏身处歌单 AudioClip 跨进出停靠驻留（受歌单长度约束、与主菜单歌单同量级有界复用）；协程一律先 Stop 后重挂，无悬空宿主

## 已知行为细节（改动前必读）

- 环境音乐默认音量 0.05 是刻意压低的
- 藏身处音乐音量默认 0（=关闭，与原版藏身处静音一致）是刻意保守的；藏身处歌单长度 0 = 随机播 1 首（与主菜单歌单长度语义一致）；改歌单长度会丢弃停靠的歌单（下次进入重建，非 bug）
- **游戏系统设置"音乐音量"的路由**（反编译核验）：它只调 MasterMixer 的 "MusicVolume" 参数 → 只作用于 "Music" 混音器组 → 该组里唯一的游戏声道是 `GUISounds.audioSource_3`（主菜单音乐，含模组替换的菜单音乐与藏身处主菜单续播）。模组自建声道（环境/出生/战斗/藏身处歌单）不设 outputAudioMixerGroup、直达 AudioListener，**不受任何游戏音量设置控制**；死亡/撤离音乐走 UI 组（受游戏音效类设置控制）。因此菜单音乐音量的调节入口 = 游戏自带"音乐音量"，模组侧不重复提供（用户决策 2026-11：因其并非控制游戏内所有音乐）
- 出生音乐播完（或限时停止）才允许环境音乐开始（`PlaySoundtrack` 检查 `spawnAudioSource.isPlaying`）
- 菜单歌单在 raid 结束返回菜单时重建；UI 音效在此时重新加载
- 音乐枚举只认 `Plugin.AudioExtensions` 白名单，文件夹里的 txt 被忽略
- 任务完成同时触发 QuestSubTrackComplete + QuestCompleted 两个独立音效事件（各文件夹独立"有音乐才替换"）
- 排障：`LogOutput.log` 含 NUL 字节，git-bash 的 grep 需 `-a`
