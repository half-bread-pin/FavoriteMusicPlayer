using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FavoriteMusicPlayer.Patches;
using SPT.Reflection.Patching;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if DEBUG
using FavoriteMusicPlayer.Utils;
#endif
using UnityEngine;
using UnityEngine.Networking;
#if SPT41
using HeadsetClass = EFT.InventoryLogic.Headphones;
#else
using HeadsetClass = HeadphonesItemClass;
#endif

namespace FavoriteMusicPlayer
{
    // FavoriteMusicPlayer 1.1.0（2026-11-13）：对外发布版本号 1.0.0 → 1.1.0（藏身处音乐
    // 系列，对应开发日志 1.7.x）；BepInPlugin Version 与程序集版本（Common.props）同步更新，
    // CM 面板标题随之显示 "FavoriteMusicPlayer 1.1.0"（cfg 按 GUID 命名，升版本不影响用户配置）。
    // 1.0.0（2026-10-24）：本项目由 ZCode（AI 编程助手）基于原
    // Bobby's Music Player 1.2.4（作者 BobbyRenzobbi，https://github.com/BobbyRenzobbi/Bobbys-Music-Player）
    // 编写移植并更名发布，移植历程即下列 1.3.x~1.7.x 开发日志。
    // 1.7.2：藏身处音乐改为"双歌单暂停/恢复"机制并修复退出根因：正常退出藏身处从不
    //       卸载藏身处（游戏把藏身处 GameWorld 缓存供快速重进），LocationId=="hideout"
    //       在回到主菜单后永远为 true——v1.7.0/1.7.1 的轮询因此把退出后的菜单期继续
    //       当作藏身处（歌单不停、菜单声道被持续压 0 与游戏 DOFade(1) 打架）。进出检测
    //       改为 GameWorld.LocationId + GUISounds.HideoutSoundActive（公有属性，两版同名）
    //       ‖ GameStatus.Running（首次进入的加载期）。行为按用户规格重写：进藏身处
    //       暂停主菜单歌单（藏身处歌单来源全程静默菜单链，主菜单来源仅加载期静音、
    //       加载完成后从暂停处续播）；退出停靠藏身处歌单（保留曲目与进度，再次进入
    //       从暂停处续播）并恢复主菜单歌单。游戏从不重启藏身处退出后的菜单音乐
    //       （重启只发生在登录后/对战后），暂停/恢复由模组自理。
    //       枚举下拉本地化改走原生路线：ConfigurationManager 原生下拉框本就支持
    //       [Description] 属性（ObjectToGuiContent 读取），新增 LocalizedDescriptionAttribute
    //       子类按当前语言返回 en/zh 标签，删除 v1.7.1 整套反射复刻 ComboBox 的自绘
    //       机械（实测失效）。插件名去作者前缀改为 FavoriteMusicPlayer（CM 标题=
    //       Name+Version="FavoriteMusicPlayer 1.0.0"；GUID/Version 不动，cfg 不受影响）。
    // 1.7.1：修复藏身处音乐完全不播放的 bug：藏身处路径从不调用 GameWorld.OnGameStarted
    //       （4.0.13/4.1.6 反编译核验：4 个调用点全在 raid/联网路径），InRaid 在藏身处恒为
    //       false——v1.7.0 把藏身处分支放在 !InRaid 之后，导致每帧走菜单分支把藏身处状态
    //       重置掉。改为藏身处分支最先执行，检测改用 GameWorld.LocationId=="hideout"
    //       （游戏自身判定，BaseLocalGame.method_4 建图期间即赋值，加载画面时已生效）。
    //       主菜单续播模式的音量接管提前到加载期间开始，实现点击藏身处起的真正无缝续播。
    //       枚举下拉改复刻 ConfigurationManager 原生 ComboBox（按钮+延迟绘制钩子弹层），
    //       替换掉被否决的 GUILayout.Toolbar 横排按钮；两游戏 CM.dll 字节级相同，钩子名已核验。
    // 1.7.0：新增藏身处音乐（"6. Hideout Music Settings / 6. 藏身处音乐设置"，4.0.13/4.1.6 通用）：
    //       来源=主菜单歌单时进藏身处不停止主菜单音乐——游戏只对菜单声道做 DOTween 音量渐隐、
    //       播放链不断，故一次性 DOTween.Kill 该 tween 后接管音量平滑拉到藏身处音量；
    //       来源=藏身处歌单时用 HideoutMusic 文件夹按长度随机填充、走独立声道播放。
    //       小键盘点唱机在藏身处按来源路由（主菜单来源控制菜单音乐，藏身处歌单来源控制藏身处音乐）。
    //       枚举下拉经 ConfigurationManagerAttributes.CustomDrawer 本地化：语言项恒显 简体中文，
    //       中文模式下环境音乐歌单来源/藏身处歌单来源显示中文标签（成员名不动，cfg 兼容）。
    //       藏身处 UI 音效时长限制（ProcessDucks）从"被跳过"变为正常生效。
    //       休息区原版收音机（场景 Appliance+AudioAmbiance，自带 TrackList 独立 3D 声道）
    //       与本模组零交集，技术上互不干扰；仅存在"同时可听"的叠加，README 已说明。
    // 1.6.0：时长限制改为"定时淡出"机制——该定制引擎上 AudioClip.Create+SetData 无法生成可用副本
    //       （1.5.1/1.5.2 两轮实测判死裁剪方案）。出生音乐=自有声道计时淡出+Stop；
    //       UI 音效/死亡撤离音乐=新增 PlaySoundDuckPatch 钩 GUISounds.PlaySound，对登记了
    //       上限的剪辑安排到点淡出+停止游戏声道并恢复音量。配置项不变，无需迁移。
    // 1.5.2：裁剪副本改为多策略创建（stream:false / streaming / AudioDataLoadType），
    //       每步校验 SetData 返回值与产物长度——1.5.1 诊断确认参数正常但单一创建方式在
    //       该 Unity 2022.3 运行时上无声失败（产物 length=0）。
    // 1.5.1：修复 1.5.0 裁剪 bug——部分解码剪辑 frequency/channels 元数据异常导致生成 0 秒空音频
    //       （出生/撤离/UI 音乐"没声音"的根因）；TrimClip 全面加固：samples 兜底、GetData 返回值检查、
    //       产物长度校验，任何异常都降级为播放完整原曲，绝不再产生空音频。
    // 1.5.0：新增音乐时长限制（出生/死亡撤离/UI 三类，加载时内存裁剪+结尾淡出，0=不限）；
    //       出生音乐改为懒加载（每局随机选一首、只加载它）。
    // 1.4.1：语言项独立为 "0. Language Settings / 0. 语言设置" 分组（原来挂在 1. General 下导致中文模式出现两个"1."分组）。
    // 1.4.0：新增配置面板中英双语（"Language / 语言"下拉项）；配置键名保持英文，切换语言不丢设置。
    // 1.3.1：修复 ConfigurationManager 菜单分类显示顺序（改为按 1→2→3→4 首次绑定）。
    // 1.3.0：移植到 SPT 4.0.13（基座 0.16.9）。相比 1.2.4 的变化：
    // - 音乐文件夹缺失不再崩溃，首次启动自动创建完整文件夹结构
    // - 地图歌单查找大小写不敏感，未知地图回退到默认歌单
    // - 新增 terminal（终点站）、labyrinth（迷宫）地图专属音乐文件夹
    // - 音频枚举只认白名单扩展名，文件夹里的说明文件不会被当作音乐加载
    // 插件名 2026-11 用户决策去掉作者前缀（CM 面板标题 = Name+Version = "FavoriteMusicPlayer 1.0.0"）；
    // GUID/Version 不动：cfg 文件按 GUID 命名，用户设置不受影响，与原版 mod 冲突检测靠 GUID。
    [BepInPlugin("com.halfbreadpin.favoritemusicplayer", "FavoriteMusicPlayer", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        // ===== SPT 版本兼容层 =====
        // SPT 4.1.x 对客户端做了大规模反混淆（详见 AGENTS.md 差异表），
        // 同一套源码经双 csproj 条件编译产出两个版本：
        //   FavoriteMusicPlayer.csproj      → SPT 4.0.13（默认，GameDir=E:\ETF）
        //   FavoriteMusicPlayer.4_1.csproj  → SPT 4.1.x（SPT41 常量，GameDir=F:\EFT416）
        // 版本差异集中在下面的常量/包装方法与 MenuMusicPatch 的字段注入处，
        // 新增版本差异时优先扩展本区域，避免 #if 散落各文件。
#if SPT41
        internal const string MenuMusicPlayMethod = nameof(GUISounds.PlayMenuBackgroundMusic);
        internal const string MenuMusicStopCoroutineMethod = nameof(GUISounds.StopAudioCallbackCoroutine);
#else
        internal const string MenuMusicPlayMethod = nameof(GUISounds.method_3);
        internal const string MenuMusicStopCoroutineMethod = nameof(GUISounds.method_8);
#endif
        internal const string BulletWhizCoroutineMethod =
#if SPT41
            nameof(FlyingBulletSoundPlayer.FrameCoroutine);
#else
            nameof(FlyingBulletSoundPlayer.method_3);
#endif

        internal static void PlayMenuMusic()
        {
#if SPT41
            Singleton<GUISounds>.Instance.PlayMenuBackgroundMusic();
#else
            Singleton<GUISounds>.Instance.method_3();
#endif
        }

        internal static void StopMenuMusicCoroutine()
        {
#if SPT41
            Singleton<GUISounds>.Instance.StopAudioCallbackCoroutine();
#else
            Singleton<GUISounds>.Instance.method_8();
#endif
        }
        public static bool InRaid { get; set; } = false;
        // 在藏身处（见 Update() 顶部的进出检测公式，v1.7.2）；藏身处音乐分支用
        public static bool InHideout { get; set; } = false;
        // 在藏身处且藏身处音乐来源=藏身处歌单：小键盘点唱机此时控制藏身处音乐而非主菜单音乐
        public static bool HideoutMusicActive { get; set; } = false;

#if DEBUG
        #region Debug

        public static ConfigEntry<float> PositionXDebug { get; set; }
        public static ConfigEntry<float> PositionYDebug { get; set; }
        public static ConfigEntry<int> FontSizeDebug { get; set; }

        #endregion
#endif
        #region Config

        public static ConfigEntry<float> SoundtrackVolume { get; set; }
        public static ConfigEntry<float> SpawnMusicVolume { get; set; }
        public static ConfigEntry<int> SoundtrackLength { get; set; }
        public static ConfigEntry<int> CustomMenuMusicLength { get; set; }
        public static ConfigEntry<ESoundtrackPlaylist> SoundtrackPlaylist;
        public static ConfigEntry<KeyboardShortcut> RestartTrack { get; set; }
        public static ConfigEntry<KeyboardShortcut> SkipTrack { get; set; }
        public static ConfigEntry<KeyboardShortcut> PreviousTrack { get; set; }
        public static ConfigEntry<KeyboardShortcut> PauseTrack { get; set; }
        public static ConfigEntry<float> AmbientCombatMultiplier { get; set; }
        public static ConfigEntry<float> CombatAttackedEntryTime { get; set; }
        public static ConfigEntry<float> CombatDangerEntryTime { get; set; }
        public static ConfigEntry<float> CombatGrenadeEntryTime { get; set; }
        public static ConfigEntry<float> CombatFireEntryTime { get; set; }
        public static ConfigEntry<float> CombatHitEntryTime { get; set; }
        public static ConfigEntry<float> CombatMusicVolume { get; set; }
        public static ConfigEntry<float> CombatInFader { get; set; }
        public static ConfigEntry<float> CombatOutFader { get; set; }
        public static ConfigEntry<float> ShotNearCutoff { get; set; }
        public static ConfigEntry<float> GrenadeNearCutoff { get; set; }
        public static ConfigEntry<float> IndoorMultiplier { get; set; }
        public static ConfigEntry<float> HeadsetMultiplier { get; set; }
        public static ConfigEntry<ELanguage> Language { get; set; }
        public static ConfigEntry<float> SpawnMusicMaxLength { get; set; }
        public static ConfigEntry<float> RaidEndMusicMaxLength { get; set; }
        public static ConfigEntry<float> UISoundsMaxLength { get; set; }
        public static ConfigEntry<EHideoutMusicPlaylist> HideoutMusicPlaylist { get; set; }
        public static ConfigEntry<float> HideoutMusicVolume { get; set; }
        public static ConfigEntry<int> HideoutMusicLength { get; set; }

        #endregion

        // ===== 枚举下拉本地化（v1.7.2，原生路线）=====
        // ConfigurationManager 的原生枚举下拉框（SettingFieldDrawer.ObjectToGuiContent）会读取
        // 成员上的 System.ComponentModel.DescriptionAttribute 并显示其 Description 文本——
        // 这是官方支持的本地化入口（已对游戏内 CM.dll 反编译核验；netstandard2.1 插件发出的
        // 该属性与 CM 读取的类型经 System.dll 转发器身份一致）。此子类按当前语言返回 en/zh，
        // 按钮标签逐帧重算（切语言立即生效）；弹出列表项由 CM 缓存，重开 CM 窗口后重本地化。
        // 成员名持久化进 cfg 不可改名（AGENTS.md 硬性约束）。
        [AttributeUsage(AttributeTargets.Field)]
        private sealed class LocalizedDescriptionAttribute : System.ComponentModel.DescriptionAttribute
        {
            private readonly string zh;

            public LocalizedDescriptionAttribute(string en, string zh) : base(en)
            {
                this.zh = zh;
            }

            // 覆盖 Description 属性按当前语言实时返回：CM 每帧经反射读取（无缓存该属性值的路径）
            public override string Description
            {
                get { return Language != null && Language.Value == ELanguage.Chinese ? zh : base.Description; }
            }
        }

        public enum ESoundtrackPlaylist
        {
            [LocalizedDescription("Map Specific Playlist Only", "仅地图歌单")]
            MapSpecificPlaylistOnly,
            [LocalizedDescription("Combined Playlists", "混合歌单")]
            CombinedPlaylists,
            [LocalizedDescription("Default Playlist Only", "仅默认歌单")]
            DefaultPlaylistOnly
        }

        // 藏身处音乐歌单来源（无混合选项）。成员名持久化进 cfg，不可改名；
        // 中文显示文本经 LocalizedDescription 属性提供（原生下拉框自动读取）。
        public enum EHideoutMusicPlaylist
        {
            // 主菜单歌单：进入藏身处暂停主菜单歌单（加载期静音），加载完成后从暂停处续播
            [LocalizedDescription("Menu Music Playlist", "主菜单歌单")]
            MenuMusicPlaylist,
            // 藏身处歌单：HideoutMusic 文件夹独立歌单，随机填充后播放；退出停靠、重进续播
            [LocalizedDescription("Hideout Playlist", "藏身处歌单")]
            HideoutPlaylist
        }

        public enum ELanguage
        {
            [LocalizedDescription("English", "English")]
            English,
            // 语言项恒显中文原生名（两种语言下都是"简体中文"）
            [LocalizedDescription("简体中文", "简体中文")]
            Chinese
        }

        internal static System.Random rand = new System.Random();

        #region 路径与音频文件枚举

        internal static readonly string PluginDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "plugins", "FavoriteMusicPlayer");
        private static readonly string MenuMusicDir = Path.Combine(PluginDir, "CustomMenuMusic", "sounds");
        private static readonly string DefaultSoundtrackDir = Path.Combine(PluginDir, "Soundtrack", "default_soundtrack");
        private static readonly string CombatMusicDir = Path.Combine(PluginDir, "Soundtrack", "combat_music");
        private static readonly string SpawnMusicDir = Path.Combine(PluginDir, "Soundtrack", "spawn_music");
        private static readonly string MapSpecificDir = Path.Combine(PluginDir, "Soundtrack", "map_specific_soundtrack");
        private static readonly string DeathMusicDir = Path.Combine(PluginDir, "DeathMusic");
        private static readonly string ExtractMusicDir = Path.Combine(PluginDir, "ExtractMusic");
        private static readonly string UiSoundsDir = Path.Combine(PluginDir, "UISounds");
        private static readonly string HideoutMusicDir = Path.Combine(PluginDir, "HideoutMusic");

        // 与 UnityWebRequestMultimedia 支持的 AudioType 一一对应的扩展名白名单。
        // 文件夹里的说明 txt 等非音频文件不会被枚举，避免加载失败。
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".ogg", ".mp2", ".mp3", ".aiff",
            ".s3m", ".it", ".mod", ".xm", ".xma", ".vag"
        };

        private static Dictionary<string, AudioType> audioType = new Dictionary<string, AudioType>
        {
            [".wav"] = AudioType.WAV,
            [".ogg"] = AudioType.OGGVORBIS,
            [".mp2"] = AudioType.MPEG,
            [".mp3"] = AudioType.MPEG,
            [".aiff"] = AudioType.AIFF,
            [".s3m"] = AudioType.S3M,
            [".it"] = AudioType.IT,
            [".mod"] = AudioType.MOD,
            [".xm"] = AudioType.XM,
            [".xma"] = AudioType.XMA,
            [".vag"] = AudioType.VAG
        };

        // 文件夹不存在时返回空列表而不是抛异常
        internal static List<string> GetAudioFiles(string dir)
        {
            var result = new List<string>();
            if (!Directory.Exists(dir))
            {
                return result;
            }
            foreach (string file in Directory.GetFiles(dir))
            {
                if (AudioExtensions.Contains(Path.GetExtension(file)))
                {
                    result.Add(file);
                }
            }
            return result;
        }

        // 游戏内 Location ID（大小写不敏感） -> map_specific_soundtrack 下的子文件夹名。
        // 4.0.13（基座 0.16.9）新增 terminal / labyrinth 两张图。
        private static readonly Dictionary<string, string> MapFolderNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bigmap"] = "customs",
                ["factory4_day"] = "factory",
                ["factory4_night"] = "factory",
                ["Interchange"] = "interchange",
                ["laboratory"] = "labs",
                ["Labyrinth"] = "labyrinth",
                ["Lighthouse"] = "lighthouse",
                ["RezervBase"] = "reserve",
                ["Sandbox"] = "ground_zero",
                ["Sandbox_high"] = "ground_zero",
                ["Shoreline"] = "shoreline",
                ["TarkovStreets"] = "streets",
                ["Terminal"] = "terminal",
                ["Woods"] = "woods"
            };

        // 首次启动时创建全部音乐文件夹，玩家只需往里拷文件
        private static void CreateFolderStructure()
        {
            try
            {
                Directory.CreateDirectory(MenuMusicDir);
                Directory.CreateDirectory(DefaultSoundtrackDir);
                Directory.CreateDirectory(CombatMusicDir);
                Directory.CreateDirectory(SpawnMusicDir);
                foreach (string folder in MapFolderNames.Values.Distinct())
                {
                    Directory.CreateDirectory(Path.Combine(MapSpecificDir, folder));
                }
                Directory.CreateDirectory(DeathMusicDir);
                Directory.CreateDirectory(ExtractMusicDir);
                Directory.CreateDirectory(HideoutMusicDir);
                foreach (string dir in UISoundsPatch.uiSoundsDir)
                {
                    Directory.CreateDirectory(Path.Combine(UiSoundsDir, dir));
                }
            }
            catch (Exception e)
            {
                LogSource.LogError("创建音乐文件夹结构失败: " + e.Message);
            }
        }

        #endregion

        // ============ 时长限制（定时淡出机制，v1.6.0） ============
        // 结论（1.5.x 两轮实测）：BSG 定制引擎（Unity 2022.3.43，无 AudioDataLoadType）
        // 上 AudioClip.Create+SetData 无论 stream:false/true 都产出 0 秒副本，
        // 程序化裁剪彻底不可行。改为播放期控制，不动音频数据：
        // - mod 自有声道（出生音乐）：ProcessSpawnFade 计时到点淡出并 Stop
        // - 游戏声道（UI 音效/死亡撤离音乐）：加载时登记上限（LimitedClips），
        //   PlaySoundDuckPatch 在 GUISounds.PlaySound 播到这些剪辑时登记
        //   DuckTask，ProcessDucks 每帧处理（最后 2s 淡出 → Stop → 恢复原音量）

        // 受限剪辑注册表：clip 引用 → 最长播放秒数
        private static readonly Dictionary<AudioClip, float> LimitedClips = new Dictionary<AudioClip, float>();

        internal static void RegisterLimitedClip(AudioClip clip, float maxSeconds)
        {
            if (clip == null || maxSeconds <= 0f)
            {
                return;
            }
            // 死亡/撤离音乐每次触发都重新加载新剪辑实例，注册表会持续增长；
            // 超过条数上限直接清空（下次加载时会重新登记，无功能损失）
            if (LimitedClips.Count > 64)
            {
                LimitedClips.Clear();
            }
            LimitedClips[clip] = maxSeconds;
        }

        internal static bool TryGetClipLimit(AudioClip clip, out float maxSeconds)
        {
            maxSeconds = 0f;
            return clip != null && LimitedClips.TryGetValue(clip, out maxSeconds);
        }

        // UI 音效是启动时预加载的，时长登记随加载时的配置值固定；
        // 此方法在配置变化时（SettingChanged）按新值整体重登记，使修改即时生效
        //（对之后触发的音效生效，正在播放中的不受追溯）。
        // 顺带清掉的死亡/撤离登记会在各自下次触发时自动重建，无功能损失。
        internal static void ReregisterUiSoundLimits()
        {
            LimitedClips.Clear();
            float maxSeconds = UISoundsMaxLength.Value;
            if (maxSeconds <= 0f)
            {
                LogSource.LogInfo("UI 音效时长限制已关闭（0=不限），已清除全部登记");
                return;
            }
            foreach (List<AudioClip> clips in UISoundsPatch.uiSoundsClips)
            {
                if (clips == null)
                {
                    continue;
                }
                foreach (AudioClip clip in clips)
                {
                    if (clip != null)
                    {
                        LimitedClips[clip] = maxSeconds;
                    }
                }
            }
            LogSource.LogInfo("UI 音效时长限制已更新为 " + maxSeconds.ToString("F0") + "s，对之后触发的音效即时生效");
        }

        internal class DuckTask
        {
            public AudioSource Source;
            public float OriginalVolume;
            public float EndTime;
        }

        private static readonly List<DuckTask> duckTasks = new List<DuckTask>();

        internal static void ScheduleDuck(AudioSource source, float maxSeconds)
        {
            if (source == null)
            {
                return;
            }
            // 同声道单任务制：新触发时移除旧任务、继承最初捕获的基准音量并立即恢复，
            // 避免旧任务到期的 Stop() 掐死新音效、以及把淡出中的音量误当基准逐单递减
            float baseline = source.volume;
            for (int i = duckTasks.Count - 1; i >= 0; i--)
            {
                if (duckTasks[i].Source == source)
                {
                    baseline = duckTasks[i].OriginalVolume;
                    duckTasks.RemoveAt(i);
                }
            }
            source.volume = baseline;
            duckTasks.Add(new DuckTask { Source = source, OriginalVolume = baseline, EndTime = Time.time + maxSeconds });
            LogSource.LogInfo("限时播放登记: 音效将在 " + maxSeconds.ToString("F0") + "s 后淡出停止");
        }

        private static void ProcessDucks()
        {
            for (int i = duckTasks.Count - 1; i >= 0; i--)
            {
                DuckTask task = duckTasks[i];
                if (task.Source == null)
                {
                    duckTasks.RemoveAt(i);
                    continue;
                }
                float remaining = task.EndTime - Time.time;
                if (remaining <= 0f)
                {
                    task.Source.Stop();
                    task.Source.volume = task.OriginalVolume;
                    duckTasks.RemoveAt(i);
                    LogSource.LogInfo("限时播放结束: 已停止并恢复该声道音量");
                }
                else if (remaining < 2f)
                {
                    task.Source.volume = task.OriginalVolume * (remaining / 2f);
                }
            }
        }

        // 出生音乐计时（mod 自有声道）
        private static bool spawnTimerActive = false;
        private static float spawnEndTime = 0f;

        private void ProcessSpawnFade()
        {
            if (!spawnTimerActive || Audio.spawnAudioSource == null || !Audio.spawnAudioSource.isPlaying)
            {
                spawnTimerActive = false;
                return;
            }
            float remaining = spawnEndTime - Time.time;
            if (remaining <= 0f)
            {
                Audio.spawnAudioSource.Stop();
                spawnTimerActive = false;
                LogSource.LogInfo("出生音乐限时结束，已停止（环境音乐将随之开始）");
            }
            else if (remaining < 2f)
            {
                // 在 VolumeSetter/CombatLerp 每帧设定的音量基础上乘性淡出
                Audio.spawnAudioSource.volume *= remaining / 2f;
            }
        }

        internal static async Task<AudioClip> AsyncRequestAudioClip(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            if (!audioType.ContainsKey(extension))
            {
                LogSource.LogError("不支持的音频格式: " + path);
                return null;
            }
            UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType[extension]);
            UnityWebRequestAsyncOperation sendWeb = uwr.SendWebRequest();

            while (!sendWeb.isDone)
                await Task.Yield();
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                LogSource.LogError("Soundtrack: Failed To Fetch Audio Clip (" + path + ")");
                return null;
            }
            else
            {
                AudioClip audioclip = DownloadHandlerAudioClip.GetContent(uwr);
                return audioclip;
            }
        }

        internal static AudioClip RequestAudioClip(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            if (!audioType.ContainsKey(extension))
            {
                LogSource.LogError("不支持的音频格式: " + path);
                return null;
            }
            UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType[extension]);
            UnityWebRequestAsyncOperation sendWeb = uwr.SendWebRequest();

            while (!sendWeb.isDone)
                if (uwr.isNetworkError || uwr.isHttpError)
                {
                    LogSource.LogError("Soundtrack: Failed To Fetch Audio Clip (" + path + ")");
                    return null;
                }
            AudioClip audioclip = DownloadHandlerAudioClip.GetContent(uwr);
            return audioclip;
        }

        #region 本地化（ConfigurationManager 显示文本）

        // 配置键名/文件 Section 一律英文（cfg 文件稳定，切换语言不丢设置）。
        // 中文仅通过 ConfigurationManagerAttributes 的 DispName/Description/Category
        // 覆盖显示层（null = 回落英文默认），已对游戏内 ConfigurationManager.dll
        // 反编译核实这三个字段会被反射读取并生效。
        private class LocalizedEntry
        {
            public ConfigurationManagerAttributes Attrs;
            public string ZhName;
            public string ZhDesc;
            public string ZhSection;
        }

        private static readonly List<LocalizedEntry> LocalizedEntries = new List<LocalizedEntry>();

        private ConfigEntry<T> BindLocalized<T>(string enSection, string zhSection, string key, string zhName,
            T defaultValue, string enDesc, string zhDesc,
            AcceptableValueBase acceptable = null, int? order = null, bool? isAdvanced = null)
        {
            var attrs = new ConfigurationManagerAttributes();
            if (order != null)
            {
                attrs.Order = order;
            }
            if (isAdvanced != null)
            {
                attrs.IsAdvanced = isAdvanced;
            }
            ConfigEntry<T> entry = Config.Bind(enSection, key, defaultValue, new ConfigDescription(enDesc, acceptable, attrs));
            LocalizedEntries.Add(new LocalizedEntry { Attrs = attrs, ZhName = zhName, ZhDesc = zhDesc, ZhSection = zhSection });
            ApplyEntryLanguage(LocalizedEntries[LocalizedEntries.Count - 1]);
            return entry;
        }

        private static void ApplyEntryLanguage(LocalizedEntry loc)
        {
            bool zh = Language != null && Language.Value == ELanguage.Chinese;
            loc.Attrs.DispName = zh ? loc.ZhName : null;
            loc.Attrs.Description = zh ? loc.ZhDesc : null;
            loc.Attrs.Category = zh ? loc.ZhSection : null;
        }

        // 枚举下拉本地化（v1.7.2 定稿）：原生 CM 下拉框自动读取枚举成员上的
        // LocalizedDescriptionAttribute（见枚举定义处）显示本地化标签，无需任何自绘。
        // 否决路线存档（勿再尝试）：
        // - v1.7.0 GUILayout.Toolbar 横排按钮：交互被用户否决；
        // - v1.7.1 反射注入 Utilities.ComboBox.CurrentDropdownDrawer 复刻原生下拉：实测失效
        //   （该钩子由原生 DrawComboboxField 每帧重挂；一次性赋值只闪现一帧）。

        #endregion

        internal static List<AudioClip> ambientTrackArray = new List<AudioClip>();
        internal static ManualLogSource LogSource;
        private static List<string> defaultTrackList = new List<string>();
        private static Dictionary<EnvironmentType, float> environmentDict = new Dictionary<EnvironmentType, float>();
        internal static float lerp = 0f;
        internal static float combatTimer = 0f;
        internal static float soundtrackVolume = 0f;
        internal static float spawnMusicVolume = 0f;
        internal static float combatMusicVolume = 0f;
        internal static float headsetMultiplier = 1f;
        private static List<string> combatMusicTrackList = new List<string>();
        private static List<AudioClip> combatMusicClipList = new List<AudioClip>();
        private static List<string> ambientTrackListToPlay = new List<string>();
        private static List<string> spawnTrackList = new List<string>();
        // 懒加载：每局只随机选一首出生音乐，且只加载选中的这一个文件
        private static AudioClip spawnTrackClip;
        private static bool HasStartedLoadingAudio = false;
        internal static bool HasFinishedLoadingAudio = false;
        internal static List<string> ambientTrackNamesArray = new List<string>();
        private static bool spawnTrackHasPlayed = false;

        // ===== 藏身处音乐状态（v1.7.0；v1.7.2 改为停靠/恢复语义）=====
        private static bool prevInHideout = false;
        private static List<string> hideoutTrackList = new List<string>();
        internal static List<AudioClip> hideoutTrackArray = new List<AudioClip>();
        internal static List<string> hideoutTrackNamesArray = new List<string>();
        private static List<string> hideoutTrackListToPlay = new List<string>();
        private static bool HasStartedLoadingHideoutAudio = false;
        internal static bool HasFinishedLoadingHideoutAudio = false;
        // 主菜单续播模式：是否已 Kill 游戏对菜单声道的音量渐隐 tween（每次藏身处访问一次性执行）
        private static bool hideoutMenuTweenKilled = false;
        // 主菜单声道缺失只警告一次（MenuMusicPatch 未生效/未捕获时避免每帧刷日志）
        private static bool hideoutMenuVolumeWarned = false;

        internal void Awake()
        {
            LogSource = Logger;

            #region LoadAudioPaths

            CreateFolderStructure();

            MenuMusicPatch.menuTrackList.AddRange(GetAudioFiles(MenuMusicDir));
            // 兼容旧版音乐包的安装位置（CustomMenuMusic / Soundtrack 是老 mod 的目录名）
            if (MenuMusicPatch.menuTrackList.IsNullOrEmpty()
                && Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\CustomMenuMusic\\sounds"))
            {
                MenuMusicPatch.menuTrackList.AddRange(GetAudioFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\CustomMenuMusic\\sounds"));
            }
            defaultTrackList.AddRange(GetAudioFiles(DefaultSoundtrackDir));
            if (defaultTrackList.IsNullOrEmpty()
                && Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Soundtrack\\sounds"))
            {
                defaultTrackList.AddRange(GetAudioFiles(AppDomain.CurrentDomain.BaseDirectory + "\\BepInEx\\plugins\\Soundtrack\\sounds"));
            }
            combatMusicTrackList.AddRange(GetAudioFiles(CombatMusicDir));
            spawnTrackList.AddRange(GetAudioFiles(SpawnMusicDir));
            RaidEndMusicPatch.deathMusicList.AddRange(GetAudioFiles(DeathMusicDir));
            RaidEndMusicPatch.extractMusicList.AddRange(GetAudioFiles(ExtractMusicDir));
            hideoutTrackList.AddRange(GetAudioFiles(HideoutMusicDir));
            int counter = 0;
            foreach (var dir in UISoundsPatch.uiSoundsDir)
            {
                // Each element of uiSounds is a List of strings so that users can add as few or as many sounds as they want to a given folder
                UISoundsPatch.uiSounds[counter] = new List<string>();
                UISoundsPatch.uiSounds[counter].AddRange(GetAudioFiles(Path.Combine(UiSoundsDir, dir)));
                counter++;
            }

            #endregion

            string languageSettings = "0. Language Settings";
            string generalSettings = "1. General Settings";
            string ambientSoundtrackSettings = "3. Ambient Soundtrack Settings";
            string customMenuMusicSettings = "2. Custom Menu Music Settings";
            string dynamicSoundtrackSettings = "4. Dynamic Soundtrack Settings";
            string durationLimitsSettings = "5. Duration Limits";
            string languageSettingsZh = "0. 语言设置";
            string generalSettingsZh = "1. 通用设置";
            string ambientSoundtrackSettingsZh = "3. 环境音乐设置";
            string customMenuMusicSettingsZh = "2. 主菜单音乐设置";
            string dynamicSoundtrackSettingsZh = "4. 战斗音乐设置";
            string durationLimitsSettingsZh = "5. 时长限制设置";
            string hideoutMusicSettings = "6. Hideout Music Settings";
            string hideoutMusicSettingsZh = "6. 藏身处音乐设置";

#if DEBUG
            PositionXDebug = Config.Bind<float>("DEBUG", "PositionXDebug", 10f, new ConfigDescription("PositionXDebug", new AcceptableValueRange<float>(-2000f, 2000f), new ConfigurationManagerAttributes { Order = 99 }));
            PositionYDebug = Config.Bind<float>("DEBUG", "PositionYDebug", -10f, new ConfigDescription("PositionYDebug", new AcceptableValueRange<float>(-2000f, 2000f), new ConfigurationManagerAttributes { Order = 99 }));
            FontSizeDebug = Config.Bind<int>("DEBUG", "FontSizeDebug", 20, new ConfigDescription("FontSizeDebug", new AcceptableValueRange<int>(0, 200), new ConfigurationManagerAttributes { Order = 99 }));

            PositionXDebug.SettingChanged += (_, __) =>
            {
                OverlayDebug.Instance.SetOverlayPosition(new Vector2(PositionXDebug.Value, PositionYDebug.Value));
            };

            PositionYDebug.SettingChanged += (_, __) =>
            {
                OverlayDebug.Instance.SetOverlayPosition(new Vector2(PositionXDebug.Value, PositionYDebug.Value));
            };

            FontSizeDebug.SettingChanged += (_, __) =>
            {
                OverlayDebug.Instance.SetFontSize(FontSizeDebug.Value);
            };
#endif
            // 注意：ConfigurationManager 按各分类"首次绑定"的顺序显示分类，
            // 绑定顺序必须保持 1 → 2 → 3 → 4 首次出现，否则菜单分组乱序。
            Language = BindLocalized(languageSettings, languageSettingsZh, "Language", "Language / 语言",
                ELanguage.English,
                "Display language of this plugin's settings panel (English / 中文). Reopen the panel to see the change — no game restart needed.",
                "本插件设置面板的显示语言。切换后重新打开面板即可看到新语言，无需重启游戏。");
            Language.SettingChanged += (_, __) =>
            {
                foreach (var loc in LocalizedEntries)
                {
                    ApplyEntryLanguage(loc);
                }
                LogSource.LogInfo("语言切换为 " + (Language.Value == ELanguage.Chinese ? "中文" : "English") + "，重新打开配置面板即可看到");
            };

            RestartTrack = BindLocalized(generalSettings, generalSettingsZh, "Restart track button", "重播当前曲目（快捷键）",
                new KeyboardShortcut(KeyCode.Keypad2), "Jukebox: restart the current track", "点唱机：重播当前曲目");
            SkipTrack = BindLocalized(generalSettings, generalSettingsZh, "Skip track button", "下一首（快捷键）",
                new KeyboardShortcut(KeyCode.Keypad6), "Jukebox: skip to the next track", "点唱机：跳到下一首");
            PreviousTrack = BindLocalized(generalSettings, generalSettingsZh, "Previous track button", "上一首（快捷键）",
                new KeyboardShortcut(KeyCode.Keypad4), "Jukebox: go back to the previous track", "点唱机：回到上一首");
            PauseTrack = BindLocalized(generalSettings, generalSettingsZh, "Pause track button", "暂停 / 继续（快捷键）",
                new KeyboardShortcut(KeyCode.Keypad5), "Jukebox: pause or resume", "点唱机：暂停 / 继续播放");
            IndoorMultiplier = BindLocalized(generalSettings, generalSettingsZh, "In-Raid Soundtrack volume - Indoor multiplier", "局内音乐音量 — 室内倍率",
                0.75f, "When indoors, all in-raid music volume will be multiplied by this value.\nI recommend setting this somewhere between 0 and 1, since the game is much noisier outdoors than indoors",
                "处于室内时，所有局内音乐音量会乘以此值。\n建议设在 0~1 之间：室外环境比室内嘈杂，室内时音乐可相应调低。",
                new AcceptableValueRange<float>(0f, 2f));
            HeadsetMultiplier = BindLocalized(generalSettings, generalSettingsZh, "In-Raid Soundtrack volume - Active headset multiplier", "局内音乐音量 — 戴耳机倍率",
                0.75f, "When wearing an active headset, all in-raid music volume will be multiplied by this value.\nI recommend setting this somewhere between 0 and 1, since the game is much noisier without an active headset",
                "佩戴已开启的耳机时，所有局内音乐音量会乘以此值。\n建议设在 0~1 之间：不戴耳机时游戏声音更嘈杂。",
                new AcceptableValueRange<float>(0f, 2f));
            CustomMenuMusicLength = BindLocalized(customMenuMusicSettings, customMenuMusicSettingsZh, "Menu Music playlist length (Minutes)", "主菜单歌单长度（分钟）",
                60, "The length of the playlist created for the main menu.\nNote: This setting's changes will take place either on game restart, or after a raid.",
                "为主菜单生成的歌单总长度（分钟）。\n注意：此设置在重启游戏或完成一局对战后生效。",
                new AcceptableValueRange<int>(0, 600), order: 0, isAdvanced: true);
            SoundtrackVolume = BindLocalized(ambientSoundtrackSettings, ambientSoundtrackSettingsZh, "Ambient Soundtrack volume", "环境音乐音量",
                0.05f, "Volume of the Ambient Soundtrack", "局内环境音乐的音量。",
                new AcceptableValueRange<float>(0f, 1f), order: 3);
            SpawnMusicVolume = BindLocalized(ambientSoundtrackSettings, ambientSoundtrackSettingsZh, "Spawn music volume", "出生音乐音量",
                0.05f, "Volume of the music played on spawn", "每次进图出生时播放音乐的音量。",
                new AcceptableValueRange<float>(0f, 1f), order: 2);
            SoundtrackPlaylist = BindLocalized(ambientSoundtrackSettings, ambientSoundtrackSettingsZh, "Ambient Soundtrack playlist selection", "环境音乐歌单来源",
                ESoundtrackPlaylist.CombinedPlaylists,
                "- Map Specific Playlist Only: Playlist will only use music from the map's soundtrack folder. If it is empty, the default soundtrack folder will be used instead.\n- Combined Playlists: Playlist will combine music from the map's soundtrack folder and the default soundtrack folder.\n- Default Playlist Only: Playlist will only use music from the default soundtrack folder.",
                "- 仅地图专属歌单：只使用当前地图文件夹里的音乐；该文件夹为空时回退默认歌单。\n- 混合歌单（默认）：默认歌单与地图专属歌单一起随机。\n- 仅默认歌单：只使用默认歌单文件夹里的音乐。",
                order: 1);
            SoundtrackLength = BindLocalized(ambientSoundtrackSettings, ambientSoundtrackSettingsZh, "Ambient Soundtrack playlist length (Minutes)", "环境音乐歌单长度（分钟）",
                50, "The length of the Ambient playlist created for each raid.\nYou should keep this around 50 unless you have modified raid times.",
                "每局为环境音乐生成的歌单长度（分钟）。\n除非修改过对局时间，否则保持 50 左右即可。",
                new AcceptableValueRange<int>(0, 600), order: 0, isAdvanced: true);
            CombatAttackedEntryTime = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat duration when shot at (Seconds)", "被射击时战斗状态时长（秒）",
                12f, "The duration of the combat state when the player is shot at\nMake sure this is set less than \"Combat duration when hit (Seconds)\"",
                "被子弹射击（子弹从身边掠过）时战斗状态的持续秒数。\n请确保此值小于「被击中时战斗状态时长（秒）」。",
                new AcceptableValueRange<float>(0f, 600f));
            CombatDangerEntryTime = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat duration when a shot is fired closeby (Seconds)", "附近开枪时战斗状态时长（秒）",
                12f, "The duration of the combat state when a gun is fired within the distance set by \"Shot distance combat trigger (meters)\"",
                "敌人在「枪声触发战斗的距离（米）」内开枪时，战斗状态的持续秒数。",
                new AcceptableValueRange<float>(0f, 600f));
            CombatGrenadeEntryTime = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat duration when a grenade explodes closeby (Seconds)", "附近手雷爆炸时战斗状态时长（秒）",
                12f, "The duration of the combat state when a gun is fired within the distance set by \"Shot distance combat trigger (meters)\"",
                "手雷在「爆炸触发战斗的距离（米）」内爆炸时，战斗状态的持续秒数。",
                new AcceptableValueRange<float>(0f, 600f));
            CombatHitEntryTime = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat duration when hit (Seconds)", "被击中时战斗状态时长（秒）",
                20f, "The duration of the combat state when the player is hit\nMake sure this is greater than \"Combat duration when shot at (Seconds)\"",
                "被子弹/爆炸/近战等伤害命中时战斗状态的持续秒数。\n请确保此值大于「被射击时战斗状态时长（秒）」。",
                new AcceptableValueRange<float>(0f, 600f));
            CombatFireEntryTime = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat duration when firing (Seconds)", "自己开枪时战斗状态时长（秒）",
                8f, "The duration of the combat state when the player fires their gun\nIf using a door breaching mod, consider reducing this value potentially down to 0",
                "自己开枪时战斗状态的持续秒数。\n如果使用了破门类 mod，可考虑降低此值，甚至设为 0。",
                new AcceptableValueRange<float>(0f, 600f));
            CombatMusicVolume = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat music volume", "战斗音乐音量",
                0.05f, "Volume of the music played in combat", "战斗音乐的音量。",
                new AcceptableValueRange<float>(0f, 1f), order: 8);
            CombatInFader = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat entry fader", "战斗音乐淡入时间（秒）",
                4f, "The transition time from normal soundtrack to combat music", "从环境音乐过渡到战斗音乐所需的淡入时间（秒）。",
                new AcceptableValueRange<float>(0.1f, 30f), isAdvanced: true);
            CombatOutFader = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Combat exit fader", "战斗音乐淡出时间（秒）",
                8f, "The transition time from combat music to normal soundtrack\nNote: This transition begins after the combat state ends",
                "战斗状态结束后，从战斗音乐淡出回到环境音乐所需的时间（秒）。",
                new AcceptableValueRange<float>(0.1f, 120f), isAdvanced: true);
            ShotNearCutoff = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Shot distance combat trigger (meters)", "枪声触发战斗的距离（米）",
                15f, "If an enemy fires within this distance, it will trigger a combat state", "敌人在此距离（米）内开枪会触发战斗状态。",
                new AcceptableValueRange<float>(0f, 150f));
            GrenadeNearCutoff = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Explosion distance combat trigger (meters)", "爆炸触发战斗的距离（米）",
                20f, "If a grenade explodes within this distance, it will trigger a combat state", "手雷在此距离（米）内爆炸会触发战斗状态。",
                new AcceptableValueRange<float>(0f, 150f));
            AmbientCombatMultiplier = BindLocalized(dynamicSoundtrackSettings, dynamicSoundtrackSettingsZh, "Ambient Soundtrack volume multiplier during combat", "战斗中环境音乐音量倍率",
                0f, "During combat, the Ambient Soundtrack's volume will be multiplied by this value\nSetting this to 0 means your Ambient Soundtrack will be muted in combat.\nSetting this to 1 means that your Ambient Soundtrack volume is independent of your combat state.\nSpawn music volume is also affected",
                "战斗期间，环境音乐音量会乘以此值。\n设为 0 = 战斗中环境音乐静音；设为 1 = 环境音乐音量不受战斗影响。\n出生音乐同样受此倍率影响。",
                new AcceptableValueRange<float>(0f, 2f), order: 0);
            SpawnMusicMaxLength = BindLocalized(durationLimitsSettings, durationLimitsSettingsZh, "Spawn music max length (Seconds)", "出生音乐最长时长（秒）",
                0f, "Maximum playback length of the spawn music in seconds. 0 = unlimited. When a track exceeds this length it fades out and stops at the limit, so the ambient soundtrack starts earlier.",
                "出生音乐的最长播放时长（秒）。0 = 不限制。超过上限的曲目会在到时自动淡出并停止，环境音乐随之更早开始。",
                new AcceptableValueRange<float>(0f, 3600f));
            RaidEndMusicMaxLength = BindLocalized(durationLimitsSettings, durationLimitsSettingsZh, "Death and Extract music max length (Seconds)", "死亡/撤离音乐最长时长（秒）",
                0f, "Maximum playback length of the death / extract results music in seconds. 0 = unlimited. When a track exceeds this length it fades out and stops at the limit.",
                "死亡/撤离结算音乐的最长播放时长（秒）。0 = 不限制。超过上限的曲目会在到时自动淡出并停止。",
                new AcceptableValueRange<float>(0f, 3600f));
            UISoundsMaxLength = BindLocalized(durationLimitsSettings, durationLimitsSettingsZh, "UI sounds max length (Seconds)", "UI 音效最长时长（秒）",
                0f, "Maximum playback length of the UI sounds in seconds. 0 = unlimited. When a sound exceeds this length it fades out and stops at the limit. Changes apply immediately to subsequently triggered sounds.",
                "界面音效的最长播放时长（秒）。0 = 不限制。超过上限的音效会在到时自动淡出并停止。修改即时生效（对之后触发的音效生效）。",
                new AcceptableValueRange<float>(0f, 3600f));
            UISoundsMaxLength.SettingChanged += (_, __) => ReregisterUiSoundLimits();

            // "6. 藏身处音乐设置"：在 5 之后首次绑定，维持 0→1→2→3→4→5→6 的分类显示顺序。
            // 默认音量 0 = 关闭（不覆盖原版藏身处静音效果，用户调高后即生效）。
            HideoutMusicPlaylist = BindLocalized(hideoutMusicSettings, hideoutMusicSettingsZh, "Hideout music playlist source", "藏身处音乐歌单来源",
                EHideoutMusicPlaylist.MenuMusicPlaylist,
                "- Menu Music Playlist: entering the hideout pauses the main menu playlist; after loading finishes it resumes from the pause point at the Hideout music volume. Leaving the hideout returns to the menu music naturally.\n- Hideout Playlist: the main menu playlist is paused for the whole hideout visit while a separate HideoutMusic folder playlist (randomly filled up to the playlist length) plays; on leaving, it is parked and resumes from the same point on the next hideout visit.",
                "- 主菜单歌单：进入藏身处时暂停主菜单歌单，加载完成后从暂停处按藏身处音乐音量继续播放；退出藏身处后主菜单音乐自然延续。\n- 藏身处歌单：藏身处期间主菜单歌单保持暂停，改播 HideoutMusic 文件夹的独立歌单（按歌单长度随机填充）；退出时停靠，下次进入藏身处从暂停处继续。",
                order: 1);
            HideoutMusicVolume = BindLocalized(hideoutMusicSettings, hideoutMusicSettingsZh, "Hideout music volume", "藏身处音乐音量",
                0f, "Volume of the music played inside the hideout. 0 = off (default). Changes apply immediately.\nNote: with the \"Menu Music Playlist\" source this scales the resuming main menu music; with the \"Hideout Playlist\" source it is the dedicated hideout source's volume.",
                "藏身处内播放音乐的音量。0 = 关闭（默认）。修改即时生效。\n注意：「主菜单歌单」来源时缩放藏身处内续播的主菜单音乐；「藏身处歌单」来源时为独立藏身处声道音量。",
                new AcceptableValueRange<float>(0f, 1f), order: 2);
            HideoutMusicLength = BindLocalized(hideoutMusicSettings, hideoutMusicSettingsZh, "Hideout music playlist length (Minutes)", "藏身处音乐歌单长度（分钟）",
                60, "The length of the playlist created for the hideout (HideoutMusic folder). Changing it discards the current parked hideout playlist; the new one is built on the next hideout visit.\nOnly applies to the \"Hideout Playlist\" source; the \"Menu Music Playlist\" source keeps using the main menu playlist and its own length setting.",
                "为藏身处生成的歌单总长度（分钟）。修改会丢弃当前停靠的藏身处歌单，下次进入藏身处按新长度重建。\n仅对「藏身处歌单」来源生效；「主菜单歌单」来源沿用主菜单歌单及其长度设置。",
                new AcceptableValueRange<int>(0, 600), order: 0, isAdvanced: true);
            // 修改歌单长度 = 丢弃停靠的藏身处歌单与加载状态，下次进入按新长度重建（v1.7.2 行为）
            HideoutMusicLength.SettingChanged += (_, __) => ResetHideoutPlaylist();


            environmentDict[EnvironmentType.Outdoor] = 1f;

            // 单个补丁启用失败（如游戏更新导致目标绑定不上）只记错误并继续，
            // 绝不能让一个补丁炸断整个 Awake——否则后续补丁、音频加载全部缺失
            ModulePatch[] patches =
            {
                new MenuMusicPatch(),
                new RaidEndMusicPatch(),
                new UISoundsPatch(),
                new ShotAtPatch(),
                new PlayerFiringPatch(),
                new DamageTakenPatch(),
                new ShotFiredNearPatch(),
                new GrenadePatch(),
                new MenuMusicMethod8Patch(),
                new StopMenuMusicPatch(),
                new OnGameWorldStartPatch(),
                new OnGameWorldDisposePatch(),
                new PlaySoundDuckPatch()
            };
            foreach (ModulePatch patch in patches)
            {
                try
                {
                    patch.Enable();
                }
                catch (Exception e)
                {
                    LogSource.LogError("补丁 " + patch.GetType().Name + " 启用失败（该功能不生效，其余不受影响）: " + e.Message);
                }
            }
            MenuMusicPatch.LoadAudioClips();
            UISoundsPatch.LoadUIClips();

            LogSource.LogInfo("plugin loaded!");
            LogSource.LogInfo("音乐文件夹位置: " + PluginDir);
        }

        internal void Update()
        {
#if DEBUG
            OverlayDebug.Instance.UpdateOverlay();
#endif
            // ===== 藏身处进出检测（v1.7.2 根因修复）=====
            // 游戏事实（4.0.13/4.1.6 反编译核验）：正常退出藏身处只调 HideHideout（关 UI），
            // 从不卸载藏身处世界——GameWorld 被缓存供快速重进（再次进入无加载画面即此设计）。
            // 因此 LocationId=="hideout" 在回到主菜单后永远为 true，不能单独作为信号
            // （v1.7.0/1.7.1 的 bug：退出后菜单期仍被当作藏身处，歌单不停、菜单声道被压 0）。
            // 可靠公式：世界是藏身处 && (藏身处界面已显示 ‖ 藏身处游戏正在加载)：
            //   HideoutSoundActive —— GUISounds 公有属性（两版同名）：藏身处界面显示时 true，
            //     MenuScreen 显示（回主菜单）时置 false —— 正常退出的可靠翻转信号；
            //   Status==Running —— BaseLocalGame.method_4 加载画面期间的过渡态：首次进入
            //     （有加载画面）从点击藏身处起即判定 true，界面显示后由 HideoutSoundActive 接管。
            // 缓存式重进（无加载画面）由 HideoutSoundActive 翻 true 即刻生效；开 raid/登出/
            // 重连等 UnloadHideout 流程销毁世界 → worldInHideout 为 false。OnGameStarted 在
            // 藏身处从不触发（InRaid 恒 false），藏身处分支必须先于 !InRaid / 局内分支。
            var gameWorld = Singleton<GameWorld>.Instance;
            bool worldInHideout = gameWorld != null && gameWorld.LocationId == "hideout";
            var hideoutGame = Singleton<AbstractGame>.Instance;
            bool hideoutLoading = worldInHideout && hideoutGame != null && hideoutGame.Status == GameStatus.Running;
            bool inHideout = worldInHideout
                && (hideoutLoading || (Singleton<GUISounds>.Instance != null && Singleton<GUISounds>.Instance.HideoutSoundActive));
            InHideout = inHideout;
            HideoutMusicActive = inHideout && HideoutMusicPlaylist != null
                && HideoutMusicPlaylist.Value == EHideoutMusicPlaylist.HideoutPlaylist;

            // ===== 进出边沿（一次性动作；藏身处内每帧行为由 UpdateHideoutMusic 的不变式维持）=====
            if (inHideout && !prevInHideout)
            {
                OnHideoutEntered();
            }
            else if (!inHideout && prevInHideout)
            {
                OnHideoutLeft();
            }
            prevInHideout = inHideout;

            MenuMusicJukebox.MenuMusicControls();
            // 藏身处：走藏身处音乐逻辑（主菜单续播或独立歌单）。必须先于 !InRaid / 局内分支——
            // 后两者会各自重置状态（菜单分支重置 raid 装载标志，局内分支启动 raid 音频）
            if (inHideout)
            {
                UpdateHideoutMusic();
                return;
            }
            if (!InRaid)
            {
                if (!MenuMusicPatch.HasReloadedAudio)
                {
                    MenuMusicPatch.LoadAudioClips();
                    UISoundsPatch.LoadUIClips();
                }
                SoundtrackJukebox.soundtrackCalled = false;
                HasStartedLoadingAudio = false;
                spawnTrackHasPlayed = false;
                spawnTrackClip = null;
                spawnTimerActive = false;
                ProcessDucks();
                return;
            }

            if (gameWorld == null || gameWorld.MainPlayer == null)
            {
                return;
            }

            if (Audio.soundtrackAudioSource == null)
            {
                Audio.soundtrackAudioSource = gameObject.AddComponent<AudioSource>();
                Audio.spawnAudioSource = gameObject.AddComponent<AudioSource>();
                Audio.combatAudioSource = gameObject.AddComponent<AudioSource>();
                LogSource.LogWarning("AudioSources added to game");
            }
            MenuMusicPatch.HasReloadedAudio = false;
            PrepareRaidAudioClips();

            var currentGame = Singleton<AbstractGame>.Instance;
            if (currentGame == null || currentGame.Status != GameStatus.Started)
            {
                return;
            }

            CombatMusic();
            VolumeSetter();
            PlaySpawnMusic();
            ProcessSpawnFade();
            SoundtrackJukebox.SoundtrackControls();
            SoundtrackJukebox.soundtrackCalled = true;
            SoundtrackJukebox.PlaySoundtrack();
            ProcessDucks();
        }

        private async void LoadAmbientSoundtrackClips()
        {
            float totalLength = 0f;
            HasFinishedLoadingAudio = false;
            ambientTrackArray.Clear();
            ambientTrackNamesArray.Clear();
            ambientTrackListToPlay.Clear();
            float targetLength = 60f * SoundtrackLength.Value;
            string location = Singleton<GameWorld>.Instance.MainPlayer.Location;
            LogSource.LogInfo("Map is " + location + ".");

            // 大小写不敏感查表；未知地图回退默认歌单而不是崩溃
            string[] mapTracks = Array.Empty<string>();
            if (MapFolderNames.TryGetValue(location, out string mapFolder))
            {
                mapTracks = GetAudioFiles(Path.Combine(MapSpecificDir, mapFolder)).ToArray();
            }
            else
            {
                LogSource.LogWarning("未识别的地图 ID \"" + location + "\"，使用默认歌单。");
            }

            if (mapTracks.IsNullOrEmpty() || SoundtrackPlaylist.Value == ESoundtrackPlaylist.DefaultPlaylistOnly)
            {
                ambientTrackListToPlay.AddRange(defaultTrackList);
            }
            else if (SoundtrackPlaylist.Value == ESoundtrackPlaylist.CombinedPlaylists)
            {
                ambientTrackListToPlay.AddRange(defaultTrackList);
                ambientTrackListToPlay.AddRange(mapTracks);
            }
            else if (SoundtrackPlaylist.Value == ESoundtrackPlaylist.MapSpecificPlaylistOnly)
            {
                ambientTrackListToPlay.AddRange(mapTracks);
            }
            while ((totalLength < targetLength) && (!ambientTrackListToPlay.IsNullOrEmpty()))
            {
                int nextRandom = rand.Next(ambientTrackListToPlay.Count);
                string track = ambientTrackListToPlay[nextRandom];
                string trackName = Path.GetFileName(track);
                AudioClip unityAudioClip = await AsyncRequestAudioClip(track);
                if (unityAudioClip == null)
                {
                    ambientTrackListToPlay.Remove(track);
                    continue;
                }
                ambientTrackArray.Add(unityAudioClip);
                ambientTrackNamesArray.Add(trackName);
                ambientTrackListToPlay.Remove(track);
                // Adding the length of each track to totalLength makes sure that the mod loads the minimum number of random tracks to meet the target length.
                totalLength += ambientTrackArray.Last().length;
                LogSource.LogInfo(trackName + " has been loaded and added to playlist");
            }
            HasFinishedLoadingAudio = true;
        }

        // ============ 藏身处音乐（v1.7.0；v1.7.2 双歌单暂停/恢复 + 进出根因修复）============
        // 游戏事实（反编译核验）：藏身处路径不调用 GameWorld.OnGameStarted（InRaid 恒 false）；
        // 正常退出藏身处不卸载世界（缓存供快速重进），游戏也不在退出后重启菜单音乐（只 DOFade
        // 音量回来；重启只发生在登录后/对战后）→ 菜单歌单的暂停/恢复由模组自理（边沿驱动）。
        // 进入藏身处时游戏对菜单声道做 DOTween 音量渐隐（两版同为 audioSource_3.DOFade 2 秒），
        // 播放与排曲链不断；raid 电台系统在藏身处休眠；休息区收音机（场景 Appliance+
        // AudioAmbiance，自带 TrackList 独立 3D 声道）与模组声道零交集。

        /// <summary>进入藏身处边沿：暂停主菜单歌单（两种来源统一——藏身处歌单来源全程静默菜单链，
        /// 主菜单歌单来源在加载期静音、加载完成后由 UpdateHideoutMusic 从暂停处恢复）</summary>
        private static void OnHideoutEntered()
        {
            MenuMusicJukebox.PauseForHideout();
            LogSource.LogInfo("进入藏身处：主菜单歌单已暂停（来源=" + (HideoutMusicActive ? "藏身处歌单" : "主菜单歌单") + "）");
        }

        /// <summary>离开藏身处边沿：停靠藏身处歌单（保留曲目与进度，下次进入从暂停处续播）
        /// + 恢复主菜单歌单 + 重置菜单音量接管状态（退出时游戏的 DOFade(1) 必须活着以恢复音量）</summary>
        private static void OnHideoutLeft()
        {
            ParkHideoutPlaylist();
            MenuMusicJukebox.ResumeForHideout();
            hideoutMenuTweenKilled = false;
            hideoutMenuVolumeWarned = false;
            LogSource.LogInfo("离开藏身处：藏身处歌单已停靠，主菜单歌单已恢复");
        }

        private void UpdateHideoutMusic()
        {
            // UI 音效时长限制在藏身处内正常处理（v1.7.0 之前藏身处走菜单分支被跳过）
            ProcessDucks();

            var currentGame = Singleton<AbstractGame>.Instance;
            if (currentGame == null || currentGame.Status != GameStatus.Started)
            {
                // 首次进入的加载期（Status==Running）：主菜单歌单维持进入时暂停（静音），藏身处歌单也不播
                return;
            }

            if (HideoutMusicActive)
            {
                // 来源=藏身处歌单：主菜单歌单保持暂停（若因藏身处内中途切换来源而在播：暂停之）
                MenuMusicJukebox.PauseForHideout();
                if (Audio.hideoutAudioSource == null)
                {
                    Audio.hideoutAudioSource = gameObject.AddComponent<AudioSource>();
                    LogSource.LogWarning("藏身处音乐 AudioSource added to game");
                }
                PrepareHideoutAudioClips();
                // 藏身处声道音量直连配置值（刻意不乘室内/耳机倍率：藏身处是安静的自有空间）。
                // 菜单声道不写音量：菜单歌单已暂停，游戏自己的进/出渐隐自然兜底
                Audio.AdjustVolume(Audio.hideoutAudioSource, HideoutMusicVolume.Value);
                // 停靠恢复：上次退出时停靠的藏身处歌单从暂停处继续（无停靠时为 no-op）
                HideoutMusicJukebox.ResumeIfParked();
                HideoutMusicJukebox.HideoutControls();
                HideoutMusicJukebox.hideoutCalled = true;
                HideoutMusicJukebox.PlayHideoutMusic();
            }
            else
            {
                // 来源=主菜单歌单：加载完成（或缓存式重进界面显示）后从暂停处恢复菜单歌单
                // （已恢复后为 no-op）；藏身处内中途切回该来源时先停靠藏身处歌单（平时 no-op）
                ParkHideoutPlaylist();
                MenuMusicJukebox.ResumeForHideout();
                FadeMenuMusicInHideout(HideoutMusicVolume.Value);
            }
        }

        /// <summary>
        /// 藏身处内主菜单音乐的音量接管。游戏进入藏身处时对菜单声道做 DOTween 音量渐隐
        /// （播放链不断），本方法一次性 Kill 该 tween，随后每帧把音量按约 2 秒平滑拉到目标值。
        /// 离开藏身处无需手动恢复：游戏回主菜单时会再 DOFade 回 1。
        /// </summary>
        private static void FadeMenuMusicInHideout(float target)
        {
            if (Audio.menuMusicAudioSource == null)
            {
                if (!hideoutMenuVolumeWarned)
                {
                    hideoutMenuVolumeWarned = true;
                    LogSource.LogWarning("主菜单音乐声道尚未捕获（MenuMusicPatch 未生效或尚未触发过），藏身处内无法续播主菜单音乐");
                }
                return;
            }
            if (!hideoutMenuTweenKilled)
            {
                hideoutMenuTweenKilled = true;
                // 杀掉游戏对菜单声道的全部音量渐隐 tween（进入藏身处时的 DOFade），
                // 否则它会在 2 秒内持续覆盖我们写入的音量；已完成的 tween 自动移除，Kill 幂等
                DG.Tweening.DOTween.Kill(Audio.menuMusicAudioSource);
                LogSource.LogInfo("藏身处内接管主菜单音乐音量（已终止游戏的音量渐隐 tween）");
            }
            // 0.5/秒 ≈ 2 秒完成过渡，与游戏自身渐隐节奏一致；目标变化（改音量/改来源）也平滑跟随
            Audio.menuMusicAudioSource.volume = Mathf.MoveTowards(Audio.menuMusicAudioSource.volume, target, Time.deltaTime * 0.5f);
        }

        /// <summary>
        /// 停靠藏身处歌单（离开藏身处 / 藏身处内切到主菜单歌单来源时调用）：暂停声道、停排曲协程、
        /// 记录剩余时长——曲目数组、曲目游标、AudioClip 全部保留，下次进入藏身处从暂停处续播。
        /// 各步自带守卫，重复调用与"从未播过/已停靠"场景均为 no-op。
        /// </summary>
        private static void ParkHideoutPlaylist()
        {
            if (Audio.hideoutAudioSource == null)
            {
                return;
            }
            // 用户在藏身处用快捷键暂停的：同样视为停靠（剩余时长沿用其暂停位置）
            if (Audio.hideoutAudioSource.isPlaying || HideoutMusicJukebox.UserPaused)
            {
                if (Audio.hideoutAudioSource.isPlaying)
                {
                    Audio.hideoutAudioSource.Pause();
                    HideoutMusicJukebox.ParkedRemaining = Audio.hideoutAudioSource.clip.length - Audio.hideoutAudioSource.time;
                }
                else
                {
                    HideoutMusicJukebox.ParkedRemaining = HideoutMusicJukebox.UserPausedRemaining;
                }
                HideoutMusicJukebox.Parked = true;
                LogSource.LogInfo("藏身处歌单已停靠（剩余 " + HideoutMusicJukebox.ParkedRemaining.ToString("F0") + "s，下次进入藏身处续播）");
            }
            if (HideoutMusicJukebox.hideoutCoroutine != null)
            {
                StaticManager.Instance.StopCoroutine(HideoutMusicJukebox.hideoutCoroutine);
                HideoutMusicJukebox.hideoutCoroutine = null;
            }
            HideoutMusicJukebox.hideoutCalled = false;
            HideoutMusicJukebox.ClearUserPauseState();
        }

        /// <summary>
        /// 修改藏身处歌单长度时整体丢弃当前藏身处歌单（含停靠态），下次进入按新长度重建
        /// </summary>
        private static void ResetHideoutPlaylist()
        {
            if (Audio.hideoutAudioSource != null)
            {
                Audio.hideoutAudioSource.Stop();
                Audio.hideoutAudioSource.clip = null;
            }
            if (HideoutMusicJukebox.hideoutCoroutine != null)
            {
                StaticManager.Instance.StopCoroutine(HideoutMusicJukebox.hideoutCoroutine);
                HideoutMusicJukebox.hideoutCoroutine = null;
            }
            HideoutMusicJukebox.hideoutCalled = false;
            HideoutMusicJukebox.ResetPauseState();
            HasStartedLoadingHideoutAudio = false;
            HasFinishedLoadingHideoutAudio = false;
            hideoutTrackArray.Clear();
            hideoutTrackNamesArray.Clear();
            hideoutTrackListToPlay.Clear();
            LogSource.LogInfo("藏身处歌单长度已修改：停靠的歌单已丢弃，下次进入藏身处按新长度重建");
        }

        /// <summary>
        /// 进入藏身处（来源=藏身处歌单）时一次性准备音频：按歌单长度随机填充
        /// </summary>
        private void PrepareHideoutAudioClips()
        {
            if (!HasStartedLoadingHideoutAudio)
            {
                HasStartedLoadingHideoutAudio = true;
                LoadHideoutMusicClips();
            }
        }

        private async void LoadHideoutMusicClips()
        {
            float totalLength = 0f;
            HasFinishedLoadingHideoutAudio = false;
            hideoutTrackArray.Clear();
            hideoutTrackNamesArray.Clear();
            hideoutTrackListToPlay.Clear();
            hideoutTrackListToPlay.AddRange(hideoutTrackList);
            if (hideoutTrackListToPlay.IsNullOrEmpty())
            {
                LogSource.LogWarning("藏身处歌单为空（HideoutMusic 文件夹没有音频文件），藏身处内不播放音乐。文件夹位置见启动日志。");
                HasFinishedLoadingHideoutAudio = true;
                return;
            }
            float targetLength = 60f * HideoutMusicLength.Value;
            LogSource.LogInfo("藏身处歌单加载开始（目标长度 " + HideoutMusicLength.Value + " 分钟，候选 " + hideoutTrackListToPlay.Count + " 首）");
            // do-while：至少装载一首（歌单长度 0 时等价于随机播 1 首），与主菜单歌单行为一致
            do
            {
                int nextRandom = rand.Next(hideoutTrackListToPlay.Count);
                string track = hideoutTrackListToPlay[nextRandom];
                string trackName = Path.GetFileName(track);
                AudioClip unityAudioClip = await AsyncRequestAudioClip(track);
                if (unityAudioClip == null)
                {
                    hideoutTrackListToPlay.Remove(track);
                    continue;
                }
                hideoutTrackArray.Add(unityAudioClip);
                hideoutTrackNamesArray.Add(trackName);
                hideoutTrackListToPlay.Remove(track);
                totalLength += unityAudioClip.length;
                LogSource.LogInfo(trackName + " has been loaded and added to hideout playlist");
            } while ((totalLength < targetLength) && (!hideoutTrackListToPlay.IsNullOrEmpty()));
            HasFinishedLoadingHideoutAudio = true;
        }

        private void CombatLerp()
        {
            Audio.AdjustVolume(Audio.combatAudioSource, Mathf.Lerp(0f, combatMusicVolume, lerp));
            Audio.AdjustVolume(Audio.soundtrackAudioSource, Mathf.Lerp(soundtrackVolume, AmbientCombatMultiplier.Value * soundtrackVolume, lerp));
            Audio.AdjustVolume(Audio.spawnAudioSource, Mathf.Lerp(spawnMusicVolume, AmbientCombatMultiplier.Value * spawnMusicVolume, lerp));
        }

        private void CombatMusic()
        {
            if (!combatMusicTrackList.IsNullOrEmpty())
            {
                if (combatTimer > 0)
                {
                    if (!Audio.combatAudioSource.isPlaying && Audio.combatAudioSource.loop == false)
                    {
                        Audio.combatAudioSource.loop = true;
                        Audio.combatAudioSource.Play();
                    }
                    if (lerp <= 1)
                    {
                        CombatLerp();
                        lerp += Time.deltaTime / CombatInFader.Value;
                    }
                    combatTimer -= Time.deltaTime;
                }
                // When the combat timer runs out, the AudioSource will only stop playing once it's done fading out.
                // If the player re-enters combat in the middle of fading out, the music will smoothly fade back in from the volume it faded out to.
                else if (combatTimer <= 0)
                {
                    if (Audio.combatAudioSource.isPlaying)
                    {
                        combatTimer = 0f;
                        CombatLerp();
                        lerp -= Time.deltaTime / CombatOutFader.Value;
                        if (lerp <= 0)
                        {
                            Audio.combatAudioSource.loop = false;
                            Audio.combatAudioSource.Stop();
                            // The combat AudioSource's clip will be randomly selected each time the combat music stops
                            Audio.combatAudioSource.clip = combatMusicClipList[rand.Next(combatMusicClipList.Count)];
                        }
                    }
                }
            }
        }

        private void VolumeSetter()
        {
            // Next two lines are taken from Fontaine's Realism Mod. Credit to him
            CompoundItem headwear = Singleton<GameWorld>.Instance.MainPlayer.Equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem as CompoundItem;
            HeadsetClass headset = Singleton<GameWorld>.Instance.MainPlayer.Equipment.GetSlot(EquipmentSlot.Earpiece).ContainedItem as HeadsetClass ?? ((headwear != null) ? headwear.GetAllItemsFromCollection().OfType<HeadsetClass>().FirstOrDefault<HeadsetClass>() : null);
            if (headset != null)
            {
                headsetMultiplier = HeadsetMultiplier.Value;
            }
            else
            {
                headsetMultiplier = 1f;
            }

            // Each of the in-raid AudioSources' volumes are calculated by multiplying their configurable volumes with the indoor multiplier and the headset multiplier.
            environmentDict[EnvironmentType.Indoor] = IndoorMultiplier.Value;

            soundtrackVolume = SoundtrackVolume.Value * environmentDict[Singleton<GameWorld>.Instance.MainPlayer.Environment] * headsetMultiplier;
            spawnMusicVolume = SpawnMusicVolume.Value * environmentDict[Singleton<GameWorld>.Instance.MainPlayer.Environment] * headsetMultiplier;
            combatMusicVolume = CombatMusicVolume.Value * environmentDict[Singleton<GameWorld>.Instance.MainPlayer.Environment] * headsetMultiplier;

            // We check if the combat AudioSource is playing so that the CombatLerp method can do its job adjusting the ambient soundtrack and spawn music AudioSources
            if (!Audio.combatAudioSource.isPlaying)
            {
                Audio.AdjustVolume(Audio.soundtrackAudioSource, soundtrackVolume);
                Audio.AdjustVolume(Audio.spawnAudioSource, spawnMusicVolume);
            }
            if (lerp >= 1)
            {
                Audio.AdjustVolume(Audio.combatAudioSource, combatMusicVolume);
            }
        }

        /// <summary>
        /// This method gets called once when loading into a raid. It makes sure every AudioClip is ready to play in the raid
        /// </summary>
        private async void PrepareRaidAudioClips()
        {
            if (!HasStartedLoadingAudio)
            {
                HasStartedLoadingAudio = true;
                if (!defaultTrackList.IsNullOrEmpty() || MapFolderNames.Count > 0)
                {
                    LoadAmbientSoundtrackClips();
                }
                if (!spawnTrackList.IsNullOrEmpty())
                {
                    // 懒加载：先随机选一首，只加载选中的文件
                    string spawnTrack = spawnTrackList[rand.Next(spawnTrackList.Count)];
                    spawnTrackClip = await AsyncRequestAudioClip(spawnTrack);
                    LogSource.LogInfo("懒加载出生音乐: " + Path.GetFileName(spawnTrack));
                    spawnTrackHasPlayed = false;
                }
                if (!combatMusicTrackList.IsNullOrEmpty())
                {
                    LogSource.LogWarning("Load music to combat");
                    // The next 4 lines prevent any issues that could be caused by exiting a raid before the combat timer ends
                    combatTimer = 0f;
                    lerp = 0;
                    Audio.combatAudioSource.Stop();
                    Audio.combatAudioSource.loop = false;
                    combatMusicClipList.Clear();
                    foreach (var track in combatMusicTrackList)
                    {
                        combatMusicClipList.Add(await AsyncRequestAudioClip(track));
                    }
                    if (!combatMusicClipList.IsNullOrEmpty())
                    {
                        Audio.combatAudioSource.clip = combatMusicClipList[rand.Next(combatMusicClipList.Count)];
                        LogSource.LogWarning($"Music in combat loaded! {Audio.combatAudioSource.clip.length}");
                    }
                }
            }
        }

        private void PlaySpawnMusic()
        {
            if (!spawnTrackHasPlayed && spawnTrackClip != null)
            {
                Audio.spawnAudioSource.clip = spawnTrackClip;
                LogSource.LogInfo("spawnAudioSource.clip assigned (lazy loaded)");
                Audio.spawnAudioSource.Play();
                LogSource.LogInfo("spawnAudioSource playing");
                if (SpawnMusicMaxLength.Value > 0f && spawnTrackClip.length > SpawnMusicMaxLength.Value)
                {
                    spawnTimerActive = true;
                    spawnEndTime = Time.time + SpawnMusicMaxLength.Value;
                    LogSource.LogInfo("出生音乐限时 " + SpawnMusicMaxLength.Value.ToString("F0") + "s（曲长 " + spawnTrackClip.length.ToString("F0") + "s）");
                }
                spawnTrackHasPlayed = true;
            }
        }
    }

    public class MenuMusicJukebox : MonoBehaviour
    {
        internal static Coroutine menuMusicCoroutine;
        private static bool paused = false;
        private static float pausedTime = 0f;

        // ===== 藏身处进出的菜单歌单暂停/恢复（v1.7.2）=====
        // 与用户快捷键暂停（paused）相互独立：模组因进出藏身处驱动的暂停，恢复时若用户
        // 自暂停过（paused==true）则尊重不恢复（用户在菜单用快捷键自行继续即可）。
        // 手动点唱机操作（重播/上一首/下一首）会清掉本暂停态——用户接管了播放控制。
        private static bool hideoutPaused = false;
        private static float hideoutPausedRemaining = 0f;

        /// <summary>进入藏身处：暂停主菜单歌单（未在播/用户已自暂停时为 no-op）</summary>
        internal static void PauseForHideout()
        {
            if (hideoutPaused || Audio.menuMusicAudioSource == null
                || !Audio.menuMusicAudioSource.isPlaying || paused)
            {
                return;
            }
            Audio.menuMusicAudioSource.Pause();
            if (menuMusicCoroutine != null)
            {
                StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                menuMusicCoroutine = null;
            }
            hideoutPausedRemaining = Audio.menuMusicAudioSource.clip.length - Audio.menuMusicAudioSource.time;
            hideoutPaused = true;
        }

        /// <summary>离开藏身处（或主菜单来源加载完成后）：从暂停处恢复主菜单歌单并重挂排曲链</summary>
        internal static void ResumeForHideout()
        {
            if (!hideoutPaused || Audio.menuMusicAudioSource == null)
            {
                return;
            }
            Audio.menuMusicAudioSource.UnPause();
            menuMusicCoroutine = StaticManager.Instance.WaitSeconds(hideoutPausedRemaining, new Action(Plugin.PlayMenuMusic));
            Plugin.LogSource.LogInfo("主菜单歌单从暂停处继续（剩余 " + hideoutPausedRemaining.ToString("F0") + "s）");
            hideoutPaused = false;
            hideoutPausedRemaining = 0f;
        }

        // This method is responsible for handling the "Jukebox" controls of the Main Menu
        public static void MenuMusicControls()
        {
            // 藏身处内且歌单来源=藏身处歌单：小键盘快捷键让位给藏身处音乐（HideoutMusicJukebox）
            if (Plugin.HideoutMusicActive)
            {
                return;
            }
            if (!SoundtrackJukebox.soundtrackCalled && Audio.menuMusicAudioSource != null)
            {
                if (Plugin.PauseTrack.Value.IsDown() && Audio.menuMusicAudioSource.isPlaying)
                {
                    Audio.menuMusicAudioSource.Pause();
                    StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                    pausedTime = Audio.menuMusicAudioSource.clip.length - Audio.menuMusicAudioSource.time;
                    paused = true;
                }
                else if (Plugin.PauseTrack.Value.IsDown() && paused)
                {
                    Audio.menuMusicAudioSource.UnPause();
                    menuMusicCoroutine = StaticManager.Instance.WaitSeconds(pausedTime, new Action(Plugin.PlayMenuMusic));
                    paused = false;
                }
                if (Plugin.RestartTrack.Value.IsDown())
                {
                    Audio.menuMusicAudioSource.Stop();
                    if (MenuMusicPatch.trackCounter != 0)
                    {
                        MenuMusicPatch.trackCounter--;
                    }
                    else
                    {
                        MenuMusicPatch.trackCounter = MenuMusicPatch.trackArray.Count - 1;
                    }
                    StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                    paused = false;
                    hideoutPaused = false;
                    Plugin.PlayMenuMusic();
                }
                if (Plugin.PreviousTrack.Value.IsDown())
                {
                    Audio.menuMusicAudioSource.Stop();
                    MenuMusicPatch.trackCounter -= 2;
                    if (MenuMusicPatch.trackCounter < 0)
                    {
                        MenuMusicPatch.trackCounter = MenuMusicPatch.trackArray.Count + (MenuMusicPatch.trackCounter);
                    }
                    StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                    paused = false;
                    hideoutPaused = false;
                    Plugin.PlayMenuMusic();
                }
                if (Plugin.SkipTrack.Value.IsDown())
                {
                    Audio.menuMusicAudioSource.Stop();
                    StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                    paused = false;
                    hideoutPaused = false;
                    Plugin.PlayMenuMusic();
                }
            }
        }
    }

    public class SoundtrackJukebox : MonoBehaviour
    {
        internal static bool soundtrackCalled = false;
        internal static bool inRaid = false;
        private static int trackCounter = 0;
        internal static Coroutine soundtrackCoroutine;
        private static bool paused = false;
        private static float pausedTime = 0f;
        public static void PlaySoundtrack()
        {
            if (!soundtrackCalled || Audio.soundtrackAudioSource.isPlaying || paused || Audio.spawnAudioSource.isPlaying || Plugin.ambientTrackArray.IsNullOrEmpty() || !Plugin.HasFinishedLoadingAudio)
            {
                return;
            }
            if (Plugin.ambientTrackArray.Count == 1)
            {
                trackCounter = 0;
            }
            Audio.soundtrackAudioSource.clip = Plugin.ambientTrackArray[trackCounter];
            Audio.soundtrackAudioSource.Play();
            Plugin.LogSource.LogInfo("Playing " + Plugin.ambientTrackNamesArray[trackCounter]);
            trackCounter++;
            soundtrackCoroutine = StaticManager.Instance.WaitSeconds(Audio.soundtrackAudioSource.clip.length, new Action(PlaySoundtrack));
            if (trackCounter >= Plugin.ambientTrackArray.Count)
            {
                trackCounter = 0;
            }
        }

        // This method is responsible for handling the "Jukebox" controls of the Ambient Soundtrack
        public static void SoundtrackControls()
        {
            if (Audio.spawnAudioSource.isPlaying)
            {
                return;
            }
            if (Plugin.PauseTrack.Value.IsDown() && Audio.soundtrackAudioSource.isPlaying)
            {
                Audio.soundtrackAudioSource.Pause();
                StaticManager.Instance.StopCoroutine(soundtrackCoroutine);
                pausedTime = Audio.soundtrackAudioSource.clip.length - Audio.soundtrackAudioSource.time;
                paused = true;
            }
            else if (Plugin.PauseTrack.Value.IsDown() && paused)
            {
                Audio.soundtrackAudioSource.UnPause();
                soundtrackCoroutine = StaticManager.Instance.WaitSeconds(pausedTime, new Action(PlaySoundtrack));
                paused = false;
            }
            if (Plugin.RestartTrack.Value.IsDown())
            {
                Audio.soundtrackAudioSource.Stop();
                if (trackCounter != 0)
                {
                    trackCounter--;
                }
                else
                {
                    trackCounter = Plugin.ambientTrackArray.Count - 1;
                }
                StaticManager.Instance.StopCoroutine(soundtrackCoroutine);
                paused = false;
                PlaySoundtrack();
            }
            if (Plugin.PreviousTrack.Value.IsDown())
            {
                Audio.soundtrackAudioSource.Stop();
                trackCounter -= 2;
                if (trackCounter < 0)
                {
                    trackCounter = Plugin.ambientTrackArray.Count + (trackCounter);
                }
                StaticManager.Instance.StopCoroutine(soundtrackCoroutine);
                paused = false;
                PlaySoundtrack();
            }
            if (Plugin.SkipTrack.Value.IsDown())
            {
                Audio.soundtrackAudioSource.Stop();
                StaticManager.Instance.StopCoroutine(soundtrackCoroutine);
                paused = false;
                PlaySoundtrack();
            }
        }
    }

    // 藏身处音乐点唱机（v1.7.0；v1.7.2 增停靠/恢复）：镜像 SoundtrackJukebox 的暂停/重播/
    // 上一首/下一首，仅在藏身处且来源=藏身处歌单时生效（MenuMusicControls 检测到
    // HideoutMusicActive 时让位）。Parked=离开藏身处时停靠的暂停态：曲目数组、曲目游标、
    // AudioSource 均保留，ResumeIfParked 从暂停处续播并重挂排曲链。
    public class HideoutMusicJukebox : MonoBehaviour
    {
        internal static bool hideoutCalled = false;
        private static int trackCounter = 0;
        internal static Coroutine hideoutCoroutine;
        // 用户快捷键暂停（在播时按小键盘 5）
        internal static bool UserPaused = false;
        internal static float UserPausedRemaining = 0f;
        // 停靠态（离开藏身处时由 Plugin.ParkHideoutPlaylist 置位；重进时 ResumeIfParked 消费）
        internal static bool Parked = false;
        internal static float ParkedRemaining = 0f;

        public static void PlayHideoutMusic()
        {
            if (!hideoutCalled || Audio.hideoutAudioSource.isPlaying || UserPaused || Parked
                || Plugin.hideoutTrackArray.IsNullOrEmpty() || !Plugin.HasFinishedLoadingHideoutAudio)
            {
                return;
            }
            if (Plugin.hideoutTrackArray.Count == 1)
            {
                trackCounter = 0;
            }
            Audio.hideoutAudioSource.clip = Plugin.hideoutTrackArray[trackCounter];
            Audio.hideoutAudioSource.Play();
            Plugin.LogSource.LogInfo("Playing (hideout) " + Plugin.hideoutTrackNamesArray[trackCounter]);
            trackCounter++;
            hideoutCoroutine = StaticManager.Instance.WaitSeconds(Audio.hideoutAudioSource.clip.length, new Action(PlayHideoutMusic));
            if (trackCounter >= Plugin.hideoutTrackArray.Count)
            {
                trackCounter = 0;
            }
        }

        // This method is responsible for handling the "Jukebox" controls of the Hideout music
        public static void HideoutControls()
        {
            if (Plugin.PauseTrack.Value.IsDown() && Audio.hideoutAudioSource.isPlaying)
            {
                Audio.hideoutAudioSource.Pause();
                if (hideoutCoroutine != null)
                {
                    StaticManager.Instance.StopCoroutine(hideoutCoroutine);
                }
                UserPausedRemaining = Audio.hideoutAudioSource.clip.length - Audio.hideoutAudioSource.time;
                UserPaused = true;
            }
            else if (Plugin.PauseTrack.Value.IsDown() && UserPaused)
            {
                Audio.hideoutAudioSource.UnPause();
                hideoutCoroutine = StaticManager.Instance.WaitSeconds(UserPausedRemaining, new Action(PlayHideoutMusic));
                UserPaused = false;
            }
            if (Plugin.RestartTrack.Value.IsDown())
            {
                Audio.hideoutAudioSource.Stop();
                if (trackCounter != 0)
                {
                    trackCounter--;
                }
                else
                {
                    trackCounter = Plugin.hideoutTrackArray.Count - 1;
                }
                if (hideoutCoroutine != null)
                {
                    StaticManager.Instance.StopCoroutine(hideoutCoroutine);
                }
                UserPaused = false;
                Parked = false;
                PlayHideoutMusic();
            }
            if (Plugin.PreviousTrack.Value.IsDown())
            {
                Audio.hideoutAudioSource.Stop();
                trackCounter -= 2;
                if (trackCounter < 0)
                {
                    trackCounter = Plugin.hideoutTrackArray.Count + trackCounter;
                }
                if (hideoutCoroutine != null)
                {
                    StaticManager.Instance.StopCoroutine(hideoutCoroutine);
                }
                UserPaused = false;
                Parked = false;
                PlayHideoutMusic();
            }
            if (Plugin.SkipTrack.Value.IsDown())
            {
                Audio.hideoutAudioSource.Stop();
                if (hideoutCoroutine != null)
                {
                    StaticManager.Instance.StopCoroutine(hideoutCoroutine);
                }
                UserPaused = false;
                Parked = false;
                PlayHideoutMusic();
            }
        }

        /// <summary>重进藏身处时从停靠处恢复：续播暂停的声道并重挂排曲链（无停靠时 no-op）</summary>
        internal static void ResumeIfParked()
        {
            if (!Parked || Audio.hideoutAudioSource == null)
            {
                return;
            }
            Audio.hideoutAudioSource.UnPause();
            hideoutCoroutine = StaticManager.Instance.WaitSeconds(ParkedRemaining, new Action(PlayHideoutMusic));
            Plugin.LogSource.LogInfo("藏身处歌单从停靠处恢复（剩余 " + ParkedRemaining.ToString("F0") + "s）");
            Parked = false;
            ParkedRemaining = 0f;
        }

        // 停靠时清用户暂停挂起态（其暂停位置已被吸收进 ParkedRemaining；曲目游标保留）
        internal static void ClearUserPauseState()
        {
            UserPaused = false;
            UserPausedRemaining = 0f;
        }

        // ResetHideoutPlaylist（歌单长度变更）调用：连停靠态与曲目游标一起清零
        internal static void ResetPauseState()
        {
            UserPaused = false;
            UserPausedRemaining = 0f;
            Parked = false;
            ParkedRemaining = 0f;
            trackCounter = 0;
        }
    }

    [RequireComponent(typeof(AudioSource))]
    public class Audio : MonoBehaviour
    {
        public static AudioSource soundtrackAudioSource;
        public static AudioSource spawnAudioSource;
        public static AudioSource combatAudioSource;
        public static AudioSource menuMusicAudioSource;
        public static AudioSource hideoutAudioSource;
        public static void SetClip(AudioSource audiosource, AudioClip clip)
        {
            audiosource.clip = clip;
        }
        public static void AdjustVolume(AudioSource audiosource, float volume)
        {
            audiosource.volume = volume;
        }
    }
}
