using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace FavoriteMusicPlayer.Patches
{
    // UI 音效与死亡/撤离音乐的最终汇聚点（GUISounds.PlaySound，内部 PlayOneShot 到
    // audioSource_0/1/4）。Prefix 不拦截原方法，只对登记了时长上限的剪辑安排
    // "到点淡出 + 停止"（PlayOneShot 无法单独停止，只能操作所在声道）。
    public class PlaySoundDuckPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var method = AccessTools.Method(typeof(GUISounds), nameof(GUISounds.PlaySound));
            if (method == null)
            {
                Plugin.LogSource.LogError("PlaySoundDuckPatch: 找不到 GUISounds.PlaySound，时长限制对 UI/结算音效不生效");
            }
            return method;
        }

        [PatchPrefix]
        private static bool Prefix(AudioClip clip, bool single, bool commonUiSound,
            AudioSource ___audioSource_0, AudioSource ___audioSource_1, AudioSource ___audioSource_4)
        {
            if (Plugin.TryGetClipLimit(clip, out float maxSeconds) && maxSeconds > 0f && clip.length > maxSeconds)
            {
                // 与 PlaySound 内部的声道选择逻辑保持一致
                AudioSource target = single ? ___audioSource_4 : (commonUiSound ? ___audioSource_1 : ___audioSource_0);
                Plugin.ScheduleDuck(target, maxSeconds);
            }
            return true;
        }
    }
}
