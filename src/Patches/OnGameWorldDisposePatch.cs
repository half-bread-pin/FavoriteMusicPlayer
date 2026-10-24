using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
#if DEBUG
using FavoriteMusicPlayer.Utils;
#endif

namespace FavoriteMusicPlayer.Patches
{
    public class OnGameWorldDisposePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var method = typeof(GameWorld).GetMethod(
                "Dispose",
                BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                Plugin.LogSource.LogError("OnGameWorldDisposePatch: 找不到 GameWorld.Dispose，出图检测补丁未生效");
            }
            return method;
        }

        [PatchPrefix]
        static void Prefix()
        {
#if DEBUG
            OverlayDebug.Instance.Disable();
#endif
            Plugin.InRaid = false;
        }
    }
}
