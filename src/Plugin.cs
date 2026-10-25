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
    // FavoriteMusicPlayer 1.0.0（2026-10-24）：本项目由 ZCode（AI 编程助手）基于原
    // Bobby's Music Player 1.2.4（作者 BobbyRenzobbi，https://github.com/BobbyRenzobbi/Bobbys-Music-Player）
    // 编写移植并更名发布，移植历程即下列 1.3.x~1.6.x 开发日志。
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
    [BepInPlugin("halfBreadPin.FavoriteMusicPlayer", "FavoriteMusicPlayer", "1.0.0")]
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

        #endregion

        public enum ESoundtrackPlaylist
        {
            MapSpecificPlaylistOnly,
            CombinedPlaylists,
            DefaultPlaylistOnly
        }

        public enum ELanguage
        {
            English,
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
            MenuMusicJukebox.MenuMusicControls();
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

            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null || gameWorld.MainPlayer.Location == "hideout")
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
        // This method is responsible for handling the "Jukebox" controls of the Main Menu
        public static void MenuMusicControls()
        {
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
                    Plugin.PlayMenuMusic();
                }
                if (Plugin.SkipTrack.Value.IsDown())
                {
                    Audio.menuMusicAudioSource.Stop();
                    StaticManager.Instance.StopCoroutine(menuMusicCoroutine);
                    paused = false;
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

    [RequireComponent(typeof(AudioSource))]
    public class Audio : MonoBehaviour
    {
        public static AudioSource soundtrackAudioSource;
        public static AudioSource spawnAudioSource;
        public static AudioSource combatAudioSource;
        public static AudioSource menuMusicAudioSource;
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
