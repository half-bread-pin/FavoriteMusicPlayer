using System.Linq;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FavoriteMusicPlayer.Patches
{
    // The following patches each represent a different trigger for the combat state,
    // but they can never decrease the value of the combat timer.
    public class ShotAtPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var method = AccessTools.Method(typeof(FlyingBulletSoundPlayer), Plugin.BulletWhizCoroutineMethod);
            if (method == null)
            {
                Plugin.LogSource.LogError("ShotAtPatch: 找不到子弹掠过音协程（" + Plugin.BulletWhizCoroutineMethod + "），被击中音效战斗触发补丁未生效");
            }
            return method;
        }

        [PatchPostfix]
        private static void Postfix()
        {
            if (Plugin.combatTimer < Plugin.CombatAttackedEntryTime.Value)
            {
                Plugin.combatTimer = Plugin.CombatAttackedEntryTime.Value;
                Plugin.LogSource.LogInfo("Player shot at. Combat Timer set to " + Plugin.combatTimer);
            }
            else
            {
                Plugin.LogSource.LogInfo("Player shot at");
            }
        }
    }

    public class PlayerFiringPatch : ModulePatch
    {
        internal static bool playerFired = false;

        protected override MethodBase GetTargetMethod()
        {
            var method = AccessTools.Method(typeof(Player), nameof(Player.OnMakingShot));
            if (method == null)
            {
                Plugin.LogSource.LogError("PlayerFiringPatch: 找不到 Player.OnMakingShot，开枪战斗触发补丁未生效");
            }
            return method;
        }

        [PatchPrefix]
        private static bool Prefix(Player __instance)
        {
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || __instance != player)
            {
                return true;
            }
            playerFired = true;
            if (Plugin.combatTimer < Plugin.CombatFireEntryTime.Value)
            {
                Plugin.combatTimer = Plugin.CombatFireEntryTime.Value;
                Plugin.LogSource.LogInfo("Player fired. Combat timer set to " + Plugin.CombatFireEntryTime.Value);
            }
            else
            {
                Plugin.LogSource.LogInfo("Player fired");
            }
            return true;
        }
    }

    public class DamageTakenPatch : ModulePatch
    {
        private static List<string> damageTypeList = new List<string>()
        {
            "Explosion", "Blunt", "Sniper", "Bullet", "Melee", "Landmine"
        };

        protected override MethodBase GetTargetMethod()
        {
            var method = AccessTools.Method(typeof(Player), nameof(Player.ReceiveDamage));
            if (method == null)
            {
                Plugin.LogSource.LogError("DamageTakenPatch: 找不到 Player.ReceiveDamage，受击战斗触发补丁未生效");
            }
            return method;
        }

        [PatchPrefix]
        private static bool Prefix(Player __instance, EDamageType type)
        {
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || __instance != player)
            {
                return true;
            }
            if (damageTypeList.Contains(type.ToString()))
            {
                if (Plugin.combatTimer < Plugin.CombatHitEntryTime.Value)
                {
                    Plugin.combatTimer = Plugin.CombatHitEntryTime.Value;
                    Plugin.LogSource.LogInfo("Player hit. Combat timer set to " + Plugin.CombatHitEntryTime.Value);
                }
                else
                {
                    Plugin.LogSource.LogInfo("Player hit");
                }
            }
            return true;
        }
    }

    public class ShotFiredNearPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var method = AccessTools.Method(typeof(WeaponSoundPlayer), nameof(WeaponSoundPlayer.FireBullet));
            if (method == null)
            {
                Plugin.LogSource.LogError("ShotFiredNearPatch: 找不到 WeaponSoundPlayer.FireBullet，近距枪声战斗触发补丁未生效");
            }
            return method;
        }

        [PatchPrefix]
        private static bool Prefix(Vector3 shotPosition)
        {
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || player.PlayerBones == null)
            {
                return true;
            }
            float distance = Vector3.Distance(player.PlayerBones.BodyTransform.position, shotPosition);
            if (distance < Plugin.ShotNearCutoff.Value)
            {
                if (PlayerFiringPatch.playerFired == true)
                {
                    PlayerFiringPatch.playerFired = false;
                    return true;
                }
                if (Plugin.combatTimer < Plugin.CombatDangerEntryTime.Value)
                {
                    Plugin.combatTimer = Plugin.CombatDangerEntryTime.Value;
                    Plugin.LogSource.LogInfo("Player shot near. Combat Timer set to " + Plugin.combatTimer);
                }
            }
            else
            {
                Plugin.LogSource.LogInfo("Enemy shot fired past cutoff distance");
            }
            return true;
        }
    }

    public class GrenadePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 4.1.x 的 Grenade 额外有一个同名无参实例方法 Explosion()（内部再调静态版），
            // 按名查找会拿到第一个匹配（无参的那个），导致 Prefix 的 grenadePosition 绑定失败。
            // 两个版本里真正带 Vector3 参数的目标都是 static，故按 IsStatic 挑选。
            var method = typeof(Grenade).GetMethods()
                .FirstOrDefault(m => m.Name == nameof(Grenade.Explosion) && m.IsStatic);
            if (method == null)
            {
                Plugin.LogSource.LogError("GrenadePatch: 找不到 Grenade.Explosion（static），手雷爆炸战斗触发补丁未生效");
            }
            return method;
        }

        [PatchPrefix]
        private static bool Prefix(Vector3 grenadePosition)
        {
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null || player.PlayerBones == null)
            {
                return true;
            }
            float distance = Vector3.Distance(player.PlayerBones.BodyTransform.position, grenadePosition);
            if (distance < Plugin.GrenadeNearCutoff.Value)
            {
                if (PlayerFiringPatch.playerFired == true)
                {
                    PlayerFiringPatch.playerFired = false;
                    return true;
                }
                if (Plugin.combatTimer < Plugin.CombatGrenadeEntryTime.Value)
                {
                    Plugin.combatTimer = Plugin.CombatGrenadeEntryTime.Value;
                    Plugin.LogSource.LogInfo("Grenade explosion near. Combat timer set to " + Plugin.combatTimer);
                }
            }
            else
            {
                Plugin.LogSource.LogInfo("Grenade explosion past cutoff distance");
            }
            return true;
        }
    }
}
