# AGENTS_EN.md — Guide for AI Coding Assistants

> Written and ported by ZCode (AI coding assistant), based on the original [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) project by BobbyRenzobbi. Thanks to the original author's open-source work and the SPT community's modding docs for making this SPT 4.0.13 port possible.

This repo is FavoriteMusicPlayer (a BepInEx client plugin — the SPT 4.0.13 port of Bobby's Music Player). Context for AI assistants (or new engineers) working here.

## Hard Constraints

- The **game install folder (default `E:\ETF`) is strictly read-only**. Builds reference DLLs from it (csproj `GameDir` property, overridable); never write there. Users install by copying files manually.
- Target framework is **netstandard2.1** (SPT 4.0.x official client mod template).
- Plugin GUID `com.halfbreadpin.favoritemusicplayer` **must not change** (the cfg file is named after the GUID — changing it breaks user configs; conflict detection with the original mod also relies on it — never set it back to the original author's GUID). **Plugin name (BepInPlugin Name), user decision 2026-11: author prefix dropped, now `FavoriteMusicPlayer`** (the ConfigurationManager panel title = Name+Version, i.e. "FavoriteMusicPlayer 1.1.0", following future version bumps automatically — a bump only touches three places: the BepInPlugin Version in `Plugin.cs` plus Version/AssemblyVersion/FileVersion in `Common.props`; cfg unaffected); the old "plugin name must not change" constraint is void. The BepInEx log source follows the plugin name as `FavoriteMusicPlayer` (troubleshooting greps unaffected). The sp-mods/The Forge `Username-ModName` convention continues to apply to **release archive naming** (independent of the in-game title).
- Mutually exclusive with the original Bobby's Music Player (same patch targets → double playback if both installed).

## Repository Layout

```
src/                          source code (develop here)
  ├─ FavoriteMusicPlayer.csproj     SPT 4.0.13 target (GameDir=E:\ETF → dist\4.0.13\)
  ├─ FavoriteMusicPlayer.4_1.csproj SPT 4.1.6 target (SPT41 define, GameDir=F:\EFT416 → dist\4.1.6\)
  ├─ Common.props                   shared build config (imported by both csproj — change shared settings here)
  ├─ Plugin.cs                plugin entry + jukebox + audio loading + duration limits + SPT version compat layer
  ├─ Patches/                 all Harmony patches (13)
  └─ Utils/OverlayDebug.cs    in-game debug overlay (DEBUG builds only)
dist/<version-line>/BepInEx/plugins/FavoriteMusicPlayer/  build output (gitignored, distributed via Releases; the archive extracts straight into the SPT root)
docs/                          music folder guide (CN/EN)
tools/create_dist_tree.sh      regenerates the dist folder tree for both version lines
README.md / README_EN.md       user-facing docs (CN/EN)
```

## Build & Verify

```powershell
cd src
dotnet build FavoriteMusicPlayer.csproj -c Release       # SPT 4.0.13 build
dotnet build FavoriteMusicPlayer.4_1.csproj -c Release   # SPT 4.1.6 build
```

- .NET SDK 8+ (10.0.402 on this machine); no Visual Studio needed; no NuGet dependencies (offline builds work)
- **Release flow**: `dotnet build` (CopyToDist copies only DLL+LICENSE) → run `tools/create_dist_tree.sh` to generate the full music folder tree with a bilingual `aboutThisFolder.txt` in each folder (any new music folder must be added to that script in sync — done for HideoutMusic in v1.7.0) → zip. Skipping the script ships a DLL-only, incomplete archive
- Two csproj share one folder: their obj/bin subdirectories are isolated (obj\4013 / obj\416), and `Common.props` globally excludes `obj/**;bin/**` from source globs to keep sibling outputs out — **do not break this mechanism when changing output paths**
- The `CS0618 isNetworkError/isHttpError deprecation` warnings are expected (correct behavior on the game's Unity runtime) — do NOT "fix" them to `uwr.result`
- `ilspycmd` (dotnet global tool) is installed for decompiling game assemblies

## SPT 4.1.6 Compatibility Layer (verified against 4.1.6 / base 0.16.9.5, 2026-10)

SPT 4.1 massively deobfuscated the client. Version differences live in the compatibility layer at the top of `Plugin.cs` (constants + wrapper methods) and the field injection in `MenuMusicPatch`; extend that region first when new differences appear:

| 4.0.13 | 4.1.6 (SPT41 branch) | Where |
|---|---|---|
| `GUISounds.method_3()` | `PlayMenuBackgroundMusic()` | MenuMusicPlayMethod constant |
| `GUISounds.method_8()` | `StopAudioCallbackCoroutine()` | MenuMusicStopCoroutineMethod constant |
| field `audioClip_0` | `_mainMenuMusic` | MenuMusicPatch Prefix injection (`___audioClip_0` / `____mainMenuMusic` — mind the underscore count) |
| `FlyingBulletSoundPlayer.method_3` | `FrameCoroutine` (same signature) | BulletWhizCoroutineMethod constant |
| `HeadphonesItemClass` | `EFT.InventoryLogic.Headphones` (family pattern XxxItemClass→Xxx) | using alias in Plugin/HeadsetPatch |

The other 12 patches plus all supporting APIs (method_7, audioSource_0/1/3/4, PlaySound, GetUIClip/GetEndGameClip, the three Player methods, GameWorld, EnvironmentType, ...) are **identical across both versions**. `FireBullet`'s ammo parameter type changed to `Ammo` in 4.1, but we bind only the parameter name `shotPosition`, so it is unaffected. **Pitfall (found in 4.1.6 testing)**: `Grenade` in 4.1.6 gained a parameterless instance method `Explosion()` (it calls the static one), and by-name lookup grabbed it, breaking `grenadePosition` binding — fixed by selecting via `GetMethods().First(m => m.Name==... && m.IsStatic)`; watch for the same by-name/overload hazard after future game updates. Patch enabling in Awake is now individually try/caught (one failure no longer kills the whole plugin). The "audio trimming is impossible" conclusion holds for both versions (UnityPlayer.dll byte-identical, MD5 75b4bf8f).

## Architecture

**Plugin.cs** (BepInEx 5 BaseUnityPlugin)
- `Awake()`: create music folders → enumerate audio (extension whitelist) → bind configs (BindLocalized, bilingual) → enable 13 patches
- `Update()`: menu/raid/**hideout** state machine, 3 self-owned raid AudioSources (soundtrack/spawn/combat) + 1 hideout AudioSource, combat fade (lerp), volume math (indoor × headset multipliers), duration-limit task processing (ProcessDucks / ProcessSpawnFade)
- Audio loading: `UnityWebRequestMultimedia.GetAudioClip("file://...")`
- Jukebox: `SoundtrackJukebox` / `MenuMusicJukebox` / `HideoutMusicJukebox`, numpad 2/4/5/6, `StaticManager.WaitSeconds` schedules the next track
- New references (Common.props): `DOTween.dll` (hideout menu-continue mode kills the game's volume fade tween)

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
- All config binds go through `Plugin.BindLocalized`; new settings must use it and preserve first-bind order 0→1→2→3→4→5→6 (ConfigurationManager category order = first-bind order)
- **Enum dropdown localization (finalized in v1.7.2 — do not change the approach)**: the native ConfigurationManager enum dropdown (`SettingFieldDrawer.ObjectToGuiContent`) already reads `System.ComponentModel.DescriptionAttribute` from enum members and displays its `Description` text (the official entry point — verified against the decompiled in-game CM.dll; the attribute emitted by a netstandard2.1 plugin is type-identical to what CM reads, via the System.dll forwarder). The mod simply attaches `Plugin.LocalizedDescriptionAttribute` (a DescriptionAttribute subclass whose `override Description` returns en/zh by current language) to enum members — **zero custom drawing, zero reflection**. Display timing: the dropdown button label is recomputed every frame (language switch applies immediately); the popup list items are cached by CM's `_comboBoxCache` and re-localize after reopening the CM window (consistent with the existing "reopen the panel after switching language" hint). Member names still persist into cfg — never rename them (display and persistence decoupled). **Rejected routes on record (do not retry)**: v1.7.0 `GUILayout.Toolbar` button rows (interaction rejected by the user); v1.7.1 reflection-injecting `Utilities.ComboBox.CurrentDropdownDrawer` to replicate the native dropdown (failed in testing — that hook is re-armed every frame by the native `DrawComboboxField` and nulled after each CM invocation, so a one-shot assignment only flashes for one frame). The custom engine's Unity modules have no GenericMenu; attaching a `CustomDrawer` bypasses the native DrawEnumField entirely (the precondition of both v1.7.x failures)

## Duration Limits Mechanism (timed fade-out)

- **Load-time trimming is dead — do not retry**: on this custom engine (Unity 2022.3.43, no AudioDataLoadType), `AudioClip.Create + SetData` always yields a 0-second clip regardless of parameters (three test rounds, 2026-10; NAudio/ffmpeg third-party options were also evaluated and rejected by the user — keep the timed fade-out, do not re-propose)
- Spawn music: `ProcessSpawnFade` timer, multiplicative fade over the last 2s → Stop
- UI / death-extract sounds: `RegisterLimitedClip` registry + `PlaySoundDuckPatch` hooking `GUISounds.PlaySound` → `ProcessDucks` per frame: 2s fade → Stop → restore volume (PlayOneShot can only be controlled via its host source; other one-shots on the same source at the cut instant are affected — tiny window)
- **Single active task per source (do not allow stacking again)**: `ScheduleDuck` removes any existing task for that source, **inherits its originally captured baseline volume**, restores it immediately, then resets the window from the new trigger — this fixed a vicious cycle found in testing (2026-10): with stacked tasks, an old task's Stop() killed newer sounds, and tasks captured mid-fade volumes as their baseline, making every later UI sound progressively quieter
- Config ("5. Duration Limits"): spawn / death-extract / UI, 0 = unlimited; menu / ambient / combat deliberately excluded (user decision)
- With 0, nothing is registered or timed — zero intervention
- **UI sound limit applies live**: `UISoundsMaxLength.SettingChanged → Plugin.ReregisterUiSoundLimits()` re-registers everything (affects subsequently triggered sounds only, no retroactive cut of the one currently playing — user's decision; death/extract entries wiped along the way rebuild themselves on their next trigger)

## Hideout Music Mechanism (introduced v1.7.0; root-cause fix + dual-playlist pause/resume in v1.7.2; works on both 4.0.13 / 4.1.6, no version branches)

Game-side facts (verified against decompiled code + runtime logs — read before touching this):
- **`GameWorld.OnGameStarted()` is NEVER called on the hideout path** (identical in both versions): all 4 call sites are raid/network paths → **`Plugin.InRaid` is always false in the hideout**. The hideout branch in `Update()` must therefore come BEFORE the `!InRaid` branch (v1.7.0 placed it after → hideout music never played)
- **Normal hideout exit NEVER unloads the hideout world** (the second root cause, fixed in v1.7.2, log-confirmed): the exit button only calls `TarkovApplication.GClass2301.HideHideout` (flushes the server queue + destroys the camera); the GameWorld/AbstractGame singletons are deliberately **cached for fast re-entry** (that's why re-entering has no loading screen) — `LocationId=="hideout"` and `MainPlayer.Location=="hideout"` stay true **forever after returning to the main menu** and cannot serve as the in/out signal (in v1.7.0/1.7.1 the post-exit menu period kept being treated as hideout: the playlist kept playing, the menu channel was pushed to 0 each frame fighting the game's `DOFade(1)`). The singletons are released only by the `UnloadHideout` flows (raid start / logout / reconnect / profile reload / backend recreate, via CompositeDisposable → `gameWorld.Dispose()` + scene unload)
- **The reliable in/out detection formula (v1.7.2)**: `worldInHideout = GameWorld.LocationId=="hideout"` && (`GUISounds.HideoutSoundActive` (public property, same name in both versions — 4.0.13 decompile + 4.1.6 binary double-verified; true while the hideout UI is shown, set false when `MenuScreen` shows, i.e. returning to the menu) ‖ `GameStatus.Running` (the loading-screen transitional state set by `BaseLocalGame.method_4` — first entry (with loading screen) tests true from the moment the hideout is clicked; `HideoutGame.vmethod_6` later sets `Started`)). Cached re-entry flips `HideoutSoundActive` to true immediately; the `MainPlayer.Location` fallback was removed (it lingers too)
- **The game NEVER restarts menu music after leaving the hideout** (crucial, decompile-verified): exit → menu only runs `MenuScreen`'s `DOFade` back to volume 1; `GUISounds.method_3` (menu-music restart) is only called after login / after raids (`TarkovApplication.method_54→method_19→method_20`). Only raid deploy fully stops it (`StopMenuBackgroundMusicWithDelay`, restarted post-raid) → **a playlist the mod paused must be resumed by the mod itself**
- Entering the hideout only DOTween-fades the menu channel volume to 0 (both versions `audioSource_3.DOFade(x, 2f)` — `DOTween.Kill` works on both); playback and the WaitSeconds chain continue. The raid `Audio.RadioSystem` is dormant in the hideout (both versions' globals.json disable all stations); `StaticManager` is a `HideFlags.DontSave` persistent singleton (survives scene changes — a safe coroutine host)
- The rest-area radio = scene `Appliance` + `EFT.Hideout.AudioAmbiance` (`ContinuingPlaybackSettings.TrackList` random playback, mutes without generator power, state persisted in PlayerPrefs) through its own 3D `AudioArray` sources — zero overlap with any mod channel, **no technical interference**; the only effect is audible layering (user decision: no automatic handling — README tells users to toggle it manually)

Mod implementation (all in Plugin.cs; detection at the top of `Update()` + enter/exit edges + `UpdateHideoutMusic` invariants):
- Config group "6. Hideout Music Settings / 6. 藏身处音乐设置": source enum `EHideoutMusicPlaylist{MenuMusicPlaylist, HideoutPlaylist}`, volume float 0–1 **default 0 (= off)**, playlist length int 0–600 default 60 (isAdvanced; **applies to the Hideout Playlist source only**). Folder `HideoutMusic\`; the loader mirrors the menu loader (do-while, at least one track)
- **Enter/exit edges (one-shot actions)**: `OnHideoutEntered` = `MenuMusicJukebox.PauseForHideout()` (pause the menu playlist: Pause + stop chain + record remaining; no-op when not playing or user-paused); `OnHideoutLeft` = `ParkHideoutPlaylist()` (park the hideout playlist) + `MenuMusicJukebox.ResumeForHideout()` (resume the menu playlist) + reset the Kill flag (the exit `DOFade(1)` must stay alive to restore menu volume)
- **Per-frame inside the hideout (`UpdateHideoutMusic`, after `Status==Started`)**: Hideout Playlist source = menu playlist stays paused + lazily create `Audio.hideoutAudioSource` (not routed into the game mixer) + `PrepareHideoutAudioClips` random fill + volume straight from config (**deliberately no indoor/headset multipliers**) + `ResumeIfParked` (re-entry continues) + WaitSeconds chain playback; Menu Music Playlist source = `ResumeForHideout` (resume from pause point after loading completes, idempotent) + `FadeMenuMusicInHideout` (one-shot `DOTween.Kill` of the game fade + per-frame `MoveTowards` to the hideout volume). The loading phase (`Running`) stays silent (user spec)
- **Parking semantics**: `ParkHideoutPlaylist` pauses the source + stops the chain + records the remaining time; the track array / cursor / AudioClips are all retained (the AudioSource lives on the plugin GameObject and survives scene changes) — re-entering (even after a raid in between) continues from the pause point; a user hotkey pause is absorbed as parked state too. `HideoutMusicLength.SettingChanged → ResetHideoutPlaylist()` discards the parked playlist and rebuilds at the new length
- Jukebox routing: while `Plugin.HideoutMusicActive` (in hideout + Hideout Playlist source), `MenuMusicControls` yields to `HideoutMusicJukebox`; with the Menu source the hotkeys control the continuing menu music. Manual jukebox actions clear the hideout-pause state (the user takes over playback)
- `ProcessDucks` also runs in the hideout (UI sound duration limits are effective there)
- **Memory residency (deliberate, not a leak)**: the parked hideout playlist's AudioClips stay resident across exits/entries (bounded by the playlist-length setting, same order of magnitude as the menu playlist — bounded reuse); coroutines are always stopped before re-arming — no dangling hosts

## Known Behavior Details (read before changing anything)

- Ambient default volume 0.05 is intentionally low
- Hideout music volume default 0 (= off, matching vanilla hideout silence) is deliberately conservative; hideout playlist length 0 = one random track (same semantics as the menu playlist length); changing the playlist length discards the parked playlist (rebuilt on next entry — not a bug)
- **Routing of the game's system "Music volume" setting** (decompile-verified): it only adjusts the MasterMixer "MusicVolume" parameter → affects only the "Music" mixer group → the only game channel in that group is `GUISounds.audioSource_3` (main menu music, including the mod's replaced menu music and the hideout menu-continuation). The mod's own sources (ambient/spawn/combat/hideout playlist) set no outputAudioMixerGroup and go straight to the AudioListener — **unaffected by any game volume setting**; death/extract music goes through the UI group (controlled by the game's sound-effect settings). Hence the knob for menu music volume is the game's own "Music volume" — the mod deliberately does not duplicate it (user decision 2026-11: it does not control all in-game music)
- Ambient music only starts after spawn music finishes (or is time-stopped): `PlaySoundtrack` checks `spawnAudioSource.isPlaying`
- The menu playlist is rebuilt when returning from a raid; UI sounds reload at the same time
- File enumeration only accepts the `Plugin.AudioExtensions` whitelist; txt files are ignored
- Quest completion fires QuestSubTrackComplete + QuestCompleted as two independent sound events (each folder replaced independently)
- Debugging tip: `LogOutput.log` contains NUL bytes; git-bash grep needs `-a`
