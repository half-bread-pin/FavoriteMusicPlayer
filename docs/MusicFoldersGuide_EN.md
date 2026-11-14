# Music Folder Guide (Which Folder Plays Which Music)

All music lives under `BepInEx\plugins\FavoriteMusicPlayer\` in your game folder. Just drop audio files into the matching folder — **no configuration needed**. Each folder accepts any number of files; the mod picks randomly.

## Master Table

| Folder | Replaces | Trigger & playback |
|---|---|---|
| `CustomMenuMusic\sounds\` | Main menu music | Loops in the main menu; shuffled into a playlist of the configured length (default 60 min) |
| `HideoutMusic\` | Hideout music (Hideout Playlist source) | Random playlist up to the configured hideout length (default 60 min) on first entry; parked on exit, resumes from the pause point on re-entry; see "Hideout Music" below |
| `Soundtrack\default_soundtrack\` | Ambient soundtrack (global pool) | Random playlist per raid, up to the configured length (default 50 min) |
| `Soundtrack\map_specific_soundtrack\<map>\` | Per-map ambient music | Only participates on that map, per the playlist-source setting |
| `Soundtrack\combat_music\` | Combat music | Loops while in combat, fades out after; a random track each combat |
| `Soundtrack\spawn_music\` | Spawn music | One random track per raid (lazy-loaded) |
| `DeathMusic\` | Death music | Results screen on death (game's `ArenaLose` slot) |
| `ExtractMusic\` | Extraction music | Results screen on successful extraction (game's `ArenaWin` slot) |
| `UISounds\QuestCompleted\` | Quest completed sound | Quest completion popup |
| `UISounds\QuestFailed\` | Quest failed sound | Quest failure popup |
| `UISounds\QuestFinished\` | Quest fully finished sound | Whole quest finished |
| `UISounds\QuestStarted\` | Quest accepted sound | Accepting a quest |
| `UISounds\QuestSubtaskComplete\` | Subtask complete sound | Quest objective done. **Note: completing the last objective of a quest fires this AND the "quest completed" sound (two independent events)** |
| `UISounds\DeathSting\` | Death sting | Short sting at death |
| `UISounds\ErrorSound\` | Error sound | Error popups |
| `UISounds\TradeSound\` | Trade sound | Trade / purchase complete |

## Per-Map Folders

Sub-folders of `Soundtrack\map_specific_soundtrack\` and their maps:

| Folder | Map | In-game Location ID |
|---|---|---|
| `customs` | Customs | `bigmap` |
| `factory` | Factory | `factory4_day` / `factory4_night` |
| `ground_zero` | Ground Zero | `Sandbox` / `Sandbox_high` |
| `interchange` | Interchange | `Interchange` |
| `labs` | The Lab | `laboratory` |
| `labyrinth` | Labyrinth | `Labyrinth` |
| `lighthouse` | Lighthouse | `Lighthouse` |
| `reserve` | Reserve | `RezervBase` |
| `shoreline` | Shoreline | `Shoreline` |
| `streets` | Streets of Tarkov | `TarkovStreets` |
| `terminal` | Terminal | `Terminal` |
| `woods` | Woods | `Woods` |

> Lookup is case-insensitive; unrecognized map IDs fall back to the default playlist (with a log warning). The hideout is not part of the per-map system — it has its own "Hideout Music" mechanism (next section).

## Hideout Music ("6. Hideout Music Settings")

Entering the hideout plays music according to the "Hideout music playlist source" setting (both sources first pause the main menu playlist; the loading screen is silent):

- **Menu Music Playlist** (default): after loading finishes, the main menu playlist **resumes from the pause point** (including the game's default menu music when you have no custom files) at the hideout music volume, eased in smoothly; on leaving, the menu music continues naturally.
- **Hideout Playlist**: the main menu playlist stays paused for the whole hideout visit while the `HideoutMusic\` folder playlist (randomly filled up to the hideout length) plays; **on exit the playlist is parked (tracks and progress retained) and resumes from the pause point on the next visit** — including after a raid in between.

Related settings:

| Setting | Default | Notes |
|---|---|---|
| Hideout music volume | 0 (= off) | 0–1 float. At 0 the vanilla hideout silence is preserved; raise it to enable |
| Hideout music playlist length (Minutes) | 60 | Advanced setting; applies to the Hideout Playlist source only. Changing it discards the currently parked playlist (rebuilt at the new length on next entry) |
| Hideout music playlist source | Menu Music Playlist | Switching the source inside the hideout takes effect immediately (smooth pause/resume transitions) |

The jukebox hotkeys also work in the hideout: with the Menu Music Playlist source they control the resuming main menu music; with the Hideout Playlist source they control the hideout music.

> The vanilla rest-area radio (the one you toggle by interacting) is a completely separate audio channel — it does not interfere with the mod technically. With both active you simply hear both at once; turn the radio off manually if you prefer.

## Ambient Playlist Modes

In-game F1 (ConfigurationManager) → "3. Ambient Soundtrack Settings" → "Ambient Soundtrack playlist selection":

- **CombinedPlaylists** (default): `default_soundtrack` + the current map's folder, shuffled together
- **MapSpecificPlaylistOnly**: only the current map's folder; falls back to default if empty
- **DefaultPlaylistOnly**: only `default_soundtrack`, ignoring map folders

## Combat Triggers (combat_music)

Any of the following raises the combat timer to the configured duration; combat music loops while it runs:

| Event | Default duration |
|---|---|
| Hit by a bullet | 20 s |
| Shot at (bullets whizzing by) | 12 s |
| Enemy fires within 15 m | 12 s |
| Grenade explodes within 20 m | 12 s |
| You fire your gun | 8 s |

Combat music fades in over 4 s and fades out over 8 s after combat ends (both configurable); ambient music is ducked while in combat ("ambient multiplier during combat", default 0 = muted).

## Jukebox Hotkeys

Shared between the menu, in-raid ambient music and hideout music (numpad defaults, rebindable); inside the hideout they follow the selected hideout music source (Menu Music Playlist → controls the continuing menu music, Hideout Playlist → controls the hideout music):

| Key | Action |
|---|---|
| Numpad 5 | Pause / resume |
| Numpad 2 | Restart current track |
| Numpad 4 | Previous track |
| Numpad 6 | Skip to next |

## Duration Limits ("5. Duration Limits")

Three optional max play times (seconds), **0 = unlimited** (default):

- **Spawn music**: fades out and stops at the limit; ambient music starts earlier as a result
- **Death / Extract music**: same, on the results screen
- **UI sounds**: one limit for all 8 sounds; changes apply immediately to subsequently triggered sounds

This is implemented as playback-time control (fade + stop the source at the deadline); files on disk are never modified. Menu, ambient and combat music are not subject to duration limits.

> Technical note: load-time audio trimming was attempted, but this game's custom engine (Unity 2022.3.43) cannot produce usable AudioClips programmatically, so timed fade-out is used instead.

## Formats & Tips

- Supported extensions: `.wav` `.ogg` `.mp3` (recommended) `.mp2` `.aiff` `.s3m` `.it` `.mod` `.xm`
- Avoid exotic formats; loudness differs between sources — normalize your tracks beforehand for a consistent experience
