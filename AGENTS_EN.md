# AGENTS_EN.md — Guide for AI Coding Assistants

> Written and ported by ZCode (AI coding assistant), based on the original [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) project by BobbyRenzobbi. Thanks to the original author's open-source work and the SPT community's modding docs for making this SPT 4.0.13 port possible.

This repo is FavoriteMusicPlayer (a BepInEx client plugin — the SPT 4.0.13 port of Bobby's Music Player). Context for AI assistants (or new engineers) working here.

## Hard Constraints

- The **game install folder (default `E:\ETF`) is strictly read-only**. Builds reference DLLs from it (csproj `GameDir` property, overridable); never write there. Users install by copying files manually.
- Target framework is **netstandard2.1** (SPT 4.0.x official client mod template).
- Plugin GUID `halfBreadPin.FavoriteMusicPlayer` **must not change** (breaks user configs) — and must not be changed back to the original author's GUID (conflicts with the original mod).
- Mutually exclusive with the original Bobby's Music Player (same patch targets → double playback if both installed).

## Repository Layout

```
src/                          source code (develop here)
  ├─ FavoriteMusicPlayer.csproj     SPT 4.0.13 target (GameDir=E:\ETF → dist\4.0.13\)
  ├─ FavoriteMusicPlayer.4_1.csproj SPT 4.1.x target (SPT41 define, GameDir=F:\EFT416 → dist\4.1.6\)
  ├─ Common.props                   shared build config (imported by both csproj — change shared settings here)
  ├─ Plugin.cs                plugin entry + jukebox + audio loading + duration limits + SPT version compat layer
  ├─ Patches/                 all Harmony patches (13)
  └─ Utils/OverlayDebug.cs    in-game debug overlay (DEBUG builds only)
dist/<version-line>/FavoriteMusicPlayer/  build output (gitignored, distributed via Releases)
docs/                          music folder guide (CN/EN)
tools/create_dist_tree.sh      regenerates the dist folder tree for both version lines
README.md / README_EN.md       user-facing docs (CN/EN)
```

## Build & Verify

```powershell
cd src
dotnet build FavoriteMusicPlayer.csproj -c Release       # SPT 4.0.13 build
dotnet build FavoriteMusicPlayer.4_1.csproj -c Release   # SPT 4.1.x build
```

- .NET SDK 8+ (10.0.402 on this machine); no Visual Studio needed; no NuGet dependencies (offline builds work)
- Two csproj share one folder: their obj/bin subdirectories are isolated (obj\4013 / obj\416), and `Common.props` globally excludes `obj/**;bin/**` from source globs to keep sibling outputs out — **do not break this mechanism when changing output paths**
- The `CS0618 isNetworkError/isHttpError deprecation` warnings are expected (correct behavior on the game's Unity runtime) — do NOT "fix" them to `uwr.result`
- `ilspycmd` (dotnet global tool) is installed for decompiling game assemblies

## SPT 4.1.x Compatibility Layer (verified against 4.1.6 / base 0.16.9.5, 2026-10)

SPT 4.1 massively deobfuscated the client. Version differences live in the compatibility layer at the top of `Plugin.cs` (constants + wrapper methods) and the field injection in `MenuMusicPatch`; extend that region first when new differences appear:

| 4.0.13 | 4.1.x (SPT41 branch) | Where |
|---|---|---|
| `GUISounds.method_3()` | `PlayMenuBackgroundMusic()` | MenuMusicPlayMethod constant |
| `GUISounds.method_8()` | `StopAudioCallbackCoroutine()` | MenuMusicStopCoroutineMethod constant |
| field `audioClip_0` | `_mainMenuMusic` | MenuMusicPatch Prefix injection (`___audioClip_0` / `____mainMenuMusic` — mind the underscore count) |
| `FlyingBulletSoundPlayer.method_3` | `FrameCoroutine` (same signature) | BulletWhizCoroutineMethod constant |
| `HeadphonesItemClass` | `EFT.InventoryLogic.Headphones` (family pattern XxxItemClass→Xxx) | using alias in Plugin/HeadsetPatch |

The other 12 patches plus all supporting APIs (method_7, audioSource_0/1/3/4, PlaySound, GetUIClip/GetEndGameClip, the three Player methods, GameWorld, EnvironmentType, ...) are **identical across both versions**. `FireBullet`'s ammo parameter type changed to `Ammo` in 4.1, but we bind only the parameter name `shotPosition`, so it is unaffected. **Pitfall (found in 4.1.6 testing)**: `Grenade` in 4.1.x gained a parameterless instance method `Explosion()` (it calls the static one), and by-name lookup grabbed it, breaking `grenadePosition` binding — fixed by selecting via `GetMethods().First(m => m.Name==... && m.IsStatic)`; watch for the same by-name/overload hazard after future game updates. Patch enabling in Awake is now individually try/caught (one failure no longer kills the whole plugin). The "audio trimming is impossible" conclusion holds for both versions (UnityPlayer.dll byte-identical, MD5 75b4bf8f).

## Architecture

**Plugin.cs** (BepInEx 5 BaseUnityPlugin)
- `Awake()`: create music folders → enumerate audio (extension whitelist) → bind configs (BindLocalized, bilingual) → enable 13 patches
- `Update()`: menu/raid state machine, 3 self-owned AudioSources (soundtrack/spawn/combat), combat fade (lerp), volume math (indoor × headset multipliers), duration-limit task processing (ProcessDucks / ProcessSpawnFade)
- Audio loading: `UnityWebRequestMultimedia.GetAudioClip("file://...")`
- Jukebox: `SoundtrackJukebox` / `MenuMusicJukebox`, numpad 2/4/5/6, `StaticManager.WaitSeconds` schedules the next track

**Music folder mapping** (`Plugin.MapFolderNames`, case-insensitive, unknown IDs fall back to the default playlist):
`bigmap→customs, factory4_day|night→factory, Interchange→interchange, laboratory→labs, Labyrinth→labyrinth, Lighthouse→lighthouse, RezervBase→reserve, Sandbox|Sandbox_high→ground_zero, Shoreline→shoreline, TarkovStreets→streets, Terminal→terminal, Woods→woods`

## Patch Target Manifest (verified against decompiled 4.0.13, 2026-10)

| Patch class | Target | Notes |
|---|---|---|
| MenuMusicPatch | `GUISounds.method_3()` | injects fields `audioSource_3`, `audioClip_0` |
| MenuMusicMethod8Patch | `GUISounds.method_8()` | stops the WaitSeconds coroutine |
| StopMenuMusicPatch | `GUISounds.StopMenuBackgroundMusicWithDelay(float transitionTime, ...)` | bind by name |
| UISoundsPatch | `UISoundsWrapper.GetUIClip(EUISoundType soundType)` | bind by name |
| RaidEndMusicPatch | `UISoundsWrapper.GetEndGameClip(EEndGameSoundType soundType)` | death=ArenaLose, extract=ArenaWin |
| ShotAtPatch | `FlyingBulletSoundPlayer.method_3(string, Vector3, Vector3, float)` | postfix, no injection |
| PlayerFiringPatch | `Player.OnMakingShot(IWeapon, Vector3)` | uses `__instance` only |
| DamageTakenPatch | `Player.ReceiveDamage(float, EBodyPart, EDamageType type, ...)` | binds `type` by name |
| ShotFiredNearPatch | `WeaponSoundPlayer.FireBullet(AmmoItemClass, Vector3 shotPosition, ...)` | bind by name |
| GrenadePatch | `Grenade.Explosion(Grenade, IExplosiveItem, Vector3 grenadePosition, ...)` static | bind by name |
| OnGameWorldStartPatch | `GameWorld.OnGameStarted()` | public virtual |
| OnGameWorldDisposePatch | `GameWorld.Dispose()` | public virtual |
| PlaySoundDuckPatch | `GUISounds.PlaySound(AudioClip clip, bool single, bool commonUiSound, float volume)` | non-blocking prefix; injects audioSource_0/1/4 |

Not enabled: `HeadsetPatch` (never mounted in the original either; kept for source parity).

## Verification Flow After a Game Update

Obfuscated names (`method_3/7/8`, `audioSource_0/1/3/4`, `audioClip_0`) were stable from 0.15.x → 0.16.9 but **may change in future versions**:

1. `dotnet build` — public API renames surface at compile time
2. A clean compile does not guarantee runtime correctness: `ilspycmd -t GUISounds "<Managed>\Assembly-CSharp.dll"` and compare against the manifest above (focus on **parameter names** — Harmony binds by name)
3. Have the user run the game once and read `<game folder>\BepInEx\LogOutput.log` (read-only): failed bindings show Harmony `skipped` warnings; this mod logs `找不到 ...` errors when a target is missing

## Localization Mechanism

- Config keys and cfg sections are **English-only and immutable**; Chinese overrides display only, via `ConfigurationManagerAttributes` (`DispName`/`Description`/`Category`)
- All config binds go through `Plugin.BindLocalized`; new settings must use it and preserve first-bind order 0→1→2→3→4→5 (ConfigurationManager category order = first-bind order)
- Enum dropdown values show enum member names (untranslated); member names persist into cfg — **never rename them**

## Duration Limits Mechanism (timed fade-out)

- **Load-time trimming is dead — do not retry**: on this custom engine (Unity 2022.3.43, no AudioDataLoadType), `AudioClip.Create + SetData` always yields a 0-second clip regardless of parameters (three test rounds, 2026-10; NAudio/ffmpeg third-party options were also evaluated and rejected by the user — keep the timed fade-out, do not re-propose)
- Spawn music: `ProcessSpawnFade` timer, multiplicative fade over the last 2s → Stop
- UI / death-extract sounds: `RegisterLimitedClip` registry + `PlaySoundDuckPatch` hooking `GUISounds.PlaySound` → `ProcessDucks` per frame: 2s fade → Stop → restore volume (PlayOneShot can only be controlled via its host source; other one-shots on the same source at the cut instant are affected — tiny window)
- **Single active task per source (do not allow stacking again)**: `ScheduleDuck` removes any existing task for that source, **inherits its originally captured baseline volume**, restores it immediately, then resets the window from the new trigger — this fixed a vicious cycle found in testing (2026-10): with stacked tasks, an old task's Stop() killed newer sounds, and tasks captured mid-fade volumes as their baseline, making every later UI sound progressively quieter
- Config ("5. Duration Limits"): spawn / death-extract / UI, 0 = unlimited; menu / ambient / combat deliberately excluded (user decision)
- With 0, nothing is registered or timed — zero intervention
- **UI sound limit applies live**: `UISoundsMaxLength.SettingChanged → Plugin.ReregisterUiSoundLimits()` re-registers everything (affects subsequently triggered sounds only, no retroactive cut of the one currently playing — user's decision; death/extract entries wiped along the way rebuild themselves on their next trigger)

## Known Behavior Details (read before changing anything)

- Ambient default volume 0.05 is intentionally low
- Ambient music only starts after spawn music finishes (or is time-stopped): `PlaySoundtrack` checks `spawnAudioSource.isPlaying`
- The menu playlist is rebuilt when returning from a raid; UI sounds reload at the same time
- File enumeration only accepts the `Plugin.AudioExtensions` whitelist; txt files are ignored
- Quest completion fires QuestSubTrackComplete + QuestCompleted as two independent sound events (each folder replaced independently)
- Debugging tip: `LogOutput.log` contains NUL bytes; git-bash grep needs `-a`
