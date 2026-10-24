using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
#if DEBUG
using FavoriteMusicPlayer.Utils;
#endif

namespace FavoriteMusicPlayer.Patches
{
    public class OnGameWorldStartPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var method = typeof(GameWorld).GetMethod(
                "OnGameStarted",
                BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                Plugin.LogSource.LogError("OnGameWorldStartPatch: 找不到 GameWorld.OnGameStarted，进图检测补丁未生效");
            }
            return method;
        }

        [PatchPostfix]
        static void PostFix()
        {
#if DEBUG
            OverlayDebug.Instance.Enable();
#endif
            Plugin.InRaid = true;
        }
    }
}
