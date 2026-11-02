# FavoriteMusicPlayer (Custom Music Player for SPT — Escape from Tarkov Offline)

> Written and ported by ZCode (AI coding assistant), based on the original [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) project by BobbyRenzobbi.
> Special thanks to the original author for open-sourcing his work, and to the SPT community's client modding documentation — this project would not exist without them. The original targets SPT 3.11.x; this project is a full port to SPT 4.0.13 / 4.1.6 with several improvements.

[中文版本](README.md)

## Overview

A BepInEx client plugin (not a server mod) that replaces the game's music and UI sounds with your own audio files. Built for **SPT 4.0.13 and 4.1.6** (base game 0.16.9) — two version lines released separately, built from one source tree.

| Feature | Description |
|---|---|
| Main menu music | Loops in the main menu |
| Ambient soundtrack | Random playlist per raid; global pool + per-map folders for 14 maps |
| Spawn music | One random track per raid (lazy-loaded) |
| Combat music | Triggered by firing / being hit / nearby shots / explosions, with fade in/out |
| Death / Extract music | Results screen music |
| UI sounds | 8 interface sounds (quests, trading, errors, ...) |
| Jukebox | Numpad 2/4/5/6: restart / previous / next / pause (menu and in-raid) |
| Duration limits | Optional max play time for spawn / results / UI sounds with automatic fade-out |
| Bilingual config panel | English / Chinese switch in ConfigurationManager |

## Installation

1. **Requirements**: an SPT 4.0.13 or 4.1.6 installation (BepInEx 5 included; lower 4.1.x builds untested)
2. **Remove old versions**: if you have the original Bobby's Music Player or any other music-replacement mod installed, delete its plugin folder first — running two music mods together causes patch conflicts / doubled playback
3. **Download & extract**: download the FavoriteMusicPlayer archive **matching your SPT version** (4.0.13 / 4.1.6 are released as separate version lines) from this repo's **Releases** page, and extract its **entire contents** into your SPT root folder (the one containing `EscapeFromTarkov.exe`)
4. **Add music**: drop audio files into the matching folders (see the table below, or the aboutThisFolder.txt in each folder)
5. **Launch**: press F1 (F12 on some setups) in the main menu — a "halfbreadpin-FavoriteMusicPlayer" section in ConfigurationManager means it works

Layout after extraction:

```
BepInEx\plugins\FavoriteMusicPlayer\
├── FavoriteMusicPlayer.dll
├── CustomMenuMusic\
├── DeathMusic\
├── ExtractMusic\
├── Soundtrack\
└── UISounds\
```

> Tip: the mod auto-creates the full music folder tree on first launch (even if you only extracted the DLL).
> The archive includes a LICENSE (MIT) file.
> Uninstall: delete `BepInEx\plugins\FavoriteMusicPlayer\` and `BepInEx\config\com.halfbreadpin.favoritemusicplayer.cfg`.

## Music Folder Reference

Music lives under `BepInEx\plugins\FavoriteMusicPlayer\` (full guide: [docs/MusicFoldersGuide_EN.md](docs/MusicFoldersGuide_EN.md)):

```
FavoriteMusicPlayer\
├── CustomMenuMusic\sounds\          ← main menu music
├── Soundtrack\
│   ├── default_soundtrack\          ← ambient music (all maps)
│   ├── combat_music\                ← combat music
│   ├── spawn_music\                 ← spawn music
│   └── map_specific_soundtrack\     ← per-map ambient music
│       ├── customs, factory, ground_zero, interchange, labs, labyrinth,
│       ├── lighthouse, reserve, shoreline, streets, terminal, woods
├── DeathMusic\                      ← death results music
├── ExtractMusic\                    ← successful extraction music
└── UISounds\                        ← interface sounds (one folder per sound)
```

**Supported formats**: `.wav` `.ogg` `.mp3` (recommended) plus whitelisted `.mp2` `.aiff` etc. `.flac` is not supported (Unity engine limitation) — convert to ogg/mp3.

## Configuration

Open ConfigurationManager in-game (F1 / F12):

- "0. Language Settings": switch EN/中文 display
- Volumes, playlist source (combined / map-only / default-only), playlist length, combat triggers and durations, indoor/headset multipliers, jukebox hotkeys
- "5. Duration Limits": max play time (seconds) for spawn / results / UI sounds, 0 = unlimited

Config file: `BepInEx\config\com.halfbreadpin.favoritemusicplayer.cfg`.

## Building

One source tree, two version targets (`src/Common.props` holds shared config; version differences live in the compatibility layer at the top of `Plugin.cs`):

```powershell
cd src
dotnet build FavoriteMusicPlayer.csproj -c Release       # SPT 4.0.13 → dist\4.0.13\BepInEx\plugins\FavoriteMusicPlayer\
dotnet build FavoriteMusicPlayer.4_1.csproj -c Release   # SPT 4.1.6  → dist\4.1.6\BepInEx\plugins\FavoriteMusicPlayer\
```

- Requires .NET SDK 8+ (no NuGet dependencies, builds offline)
- The 4.0.13 target references `E:\ETF` and the 4.1.6 target references `F:\EFT416` by default (this machine's game folders); override with `-p:GameDir=...`. Builds only **read** game DLLs and never write to the game folder
- Name release archives per version line (e.g. `FavoriteMusicPlayer-1.0.0-4.0.13.zip`) so users can pick the one matching their SPT version
- The repo contains no game files. After a game update, verify patch target signatures first (see AGENTS_EN.md)

## Troubleshooting

Log file: `<game folder>\BepInEx\LogOutput.log`:

| Symptom | Fix |
|---|---|
| No custom music at all | Search the log for `FavoriteMusicPlayer` — check `plugin loaded!`; search `找不到` for failed patches |
| Quest completion plays my music **and** the system chime | By design: completing a quest fires two separate sound events; put music (or a silent file) into `UISounds\QuestSubtaskComplete\` too |
| A file fails to load | Log shows `Failed To Fetch Audio Clip` — usually a format issue, convert to wav/mp3 |
| Ambient music too quiet | 0.05 default volume is intentional; raise it in the F1 panel |
| No map-specific music | Check playlist source setting and that the map folder has audio files |

## Credits & License

- Original project: [BobbyRenzobbi/Bobbys-Music-Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) (v1.2.4, SPT 3.11.x) — all core mechanics originate from it
- The [SPT modding wiki](https://wiki.sp-tushonka.com/modding/Modding_Resources) and Jehree's client mod template
- License: MIT (see [LICENSE](LICENSE)). The original project ships without a license; this port is released under MIT in the spirit of community sharing with full attribution, and will be taken down or adjusted upon the original author's request
