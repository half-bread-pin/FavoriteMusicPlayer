using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HeadsetClass = HeadphonesItemClass;

namespace FavoriteMusicPlayer.Patches
{
    // 注意：原版 1.2.4 就没有在 Plugin.Awake 中启用此补丁（VolumeSetter 每帧轮询已覆盖其功能），
    // 此文件仅保持源码对等迁移，未参与运行。
    public class HeadsetPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.UpdatePhones));
        }

        [PatchPostfix]
        private static void Postfix(Player __instance)
        {
            if (__instance == Singleton<GameWorld>.Instance?.MainPlayer)
            {
                InventoryEquipment equipment = __instance.Equipment;
                CompoundItem headwear = equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem as CompoundItem;
                HeadsetClass headset = (equipment.GetSlot(EquipmentSlot.Earpiece).ContainedItem as HeadsetClass) ?? ((headwear != null) ? headwear.GetAllItemsFromCollection().OfType<HeadsetClass>().FirstOrDefault<HeadsetClass>() : null);
                if (headset != null)
                {
                    Plugin.headsetMultiplier = Plugin.HeadsetMultiplier.Value;
                }
                else
                {
                    Plugin.headsetMultiplier = 1f;
                }
            }
        }
    }
}
