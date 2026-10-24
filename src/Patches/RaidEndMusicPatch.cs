using SPT.Reflection.Patching;
using EFT.UI;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace FavoriteMusicPlayer.Patches
{
    public class RaidEndMusicPatch : ModulePatch
    {
        internal static List<string> deathMusicList = new List<string>();
        internal static List<string> extractMusicList = new List<string>();
        private static Dictionary<EEndGameSoundType, List<string>> raidEndDictionary;
        private static AudioClip raidEndClip;

        protected override MethodBase GetTargetMethod()
        {
            raidEndDictionary = new Dictionary<EEndGameSoundType, List<string>>
            {
                [EEndGameSoundType.ArenaLose] = deathMusicList,
                [EEndGameSoundType.ArenaWin] = extractMusicList
            };
            var method = AccessTools.Method(typeof(UISoundsWrapper), nameof(UISoundsWrapper.GetEndGameClip));
            if (method == null)
            {
                Plugin.LogSource.LogError("RaidEndMusicPatch: 找不到 UISoundsWrapper.GetEndGameClip，死亡/撤离音乐补丁未生效");
            }
            return method;
        }

        private static void LoadNextTrack(EEndGameSoundType soundType)
        {
            string raidEndTrack = raidEndDictionary[soundType][Plugin.rand.Next(raidEndDictionary[soundType].Count)];
            raidEndClip = Plugin.RequestAudioClip(raidEndTrack);
            Plugin.RegisterLimitedClip(raidEndClip, Plugin.RaidEndMusicMaxLength.Value);
            string trackName = Path.GetFileName(raidEndTrack);
            Plugin.LogSource.LogInfo(trackName + " assigned to " + soundType);
        }

        [PatchPrefix]
        static bool Prefix(ref AudioClip __result, EEndGameSoundType soundType)
        {
            if (!raidEndDictionary.ContainsKey(soundType) || raidEndDictionary[soundType].IsNullOrEmpty())
            {
                return true;
            }
            LoadNextTrack(soundType);
            if (raidEndClip != null)
            {
                __result = raidEndClip;
                return false;
            }
            return true;
        }
    }
}
