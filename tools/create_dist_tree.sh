#!/bin/bash
# 为 dist/4.0.13 和 dist/4.1.6 两条版本线各生成完整音乐文件夹树，每个文件夹放一个双语 aboutThisFolder.txt
set -e
DIST_ROOT="$(cd "$(dirname "$0")/.." && pwd)/dist"
for VER in "4.0.13" "4.1.6"; do
DIST="$DIST_ROOT/$VER/BepInEx/plugins/FavoriteMusicPlayer"
mkdir -p "$DIST"

write_note() {
  local dir="$1"; local zh="$2"; local en="$3"
  mkdir -p "$dir"
  cat > "$dir/aboutThisFolder.txt" <<EOF
[中文] $zh

[English] $en

支持的音频格式 / Supported formats: .wav .ogg .mp3 .mp2 .aiff .s3m .it .mod .xm
本说明文件不会被当作音乐加载 / This note file is ignored by the mod.
EOF
}

write_note "$DIST/CustomMenuMusic/sounds" \
  '【主菜单背景音乐】游戏主菜单循环播放的音乐。文件夹为空时使用游戏原版菜单音乐。' \
  '[Main menu music] Loops in the main menu. Falls back to the vanilla tracks when empty.'

write_note "$DIST/Soundtrack/default_soundtrack" \
  '【默认环境音乐】所有地图通用的局内背景音乐，进图后随机组成歌单，与"地图专属音乐"按配置组合。' \
  '[Default ambient soundtrack] Global in-raid background music, shuffled into a per-raid playlist and combined with map-specific music per the setting.'

write_note "$DIST/Soundtrack/combat_music" \
  '【战斗音乐】进入战斗状态时循环播放（开枪/被击中/近距离枪声/手雷爆炸触发），战斗结束后淡出。' \
  '[Combat music] Loops while in combat (triggered by firing / being hit / nearby shots / explosions), fades out after combat.'

write_note "$DIST/Soundtrack/spawn_music" \
  '【出生音乐】每次进入战局出生时随机播放一首。' \
  '[Spawn music] One random track plays on each raid spawn.'

write_note "$DIST/Soundtrack/map_specific_soundtrack/customs" \
  '【海关 Customs 专属环境音乐】游戏内地图 ID: bigmap' \
  '[Customs map music] In-game Location ID: bigmap'
write_note "$DIST/Soundtrack/map_specific_soundtrack/factory" \
  '【工厂 Factory 专属环境音乐】游戏内地图 ID: factory4_day / factory4_night' \
  '[Factory map music] In-game Location ID: factory4_day / factory4_night'
write_note "$DIST/Soundtrack/map_specific_soundtrack/ground_zero" \
  '【街区中心 Ground Zero 专属环境音乐】游戏内地图 ID: Sandbox / Sandbox_high' \
  '[Ground Zero map music] In-game Location ID: Sandbox / Sandbox_high'
write_note "$DIST/Soundtrack/map_specific_soundtrack/interchange" \
  '【立交桥 Interchange 专属环境音乐】游戏内地图 ID: Interchange' \
  '[Interchange map music] In-game Location ID: Interchange'
write_note "$DIST/Soundtrack/map_specific_soundtrack/labs" \
  '【实验室 The Lab 专属环境音乐】游戏内地图 ID: laboratory' \
  '[The Lab map music] In-game Location ID: laboratory'
write_note "$DIST/Soundtrack/map_specific_soundtrack/labyrinth" \
  '【迷宫 Labyrinth 专属环境音乐】游戏内地图 ID: Labyrinth' \
  '[Labyrinth map music] In-game Location ID: Labyrinth'
write_note "$DIST/Soundtrack/map_specific_soundtrack/lighthouse" \
  '【灯塔 Lighthouse 专属环境音乐】游戏内地图 ID: Lighthouse' \
  '[Lighthouse map music] In-game Location ID: Lighthouse'
write_note "$DIST/Soundtrack/map_specific_soundtrack/reserve" \
  '【储备站 Reserve 专属环境音乐】游戏内地图 ID: RezervBase' \
  '[Reserve map music] In-game Location ID: RezervBase'
write_note "$DIST/Soundtrack/map_specific_soundtrack/shoreline" \
  '【海岸线 Shoreline 专属环境音乐】游戏内地图 ID: Shoreline' \
  '[Shoreline map music] In-game Location ID: Shoreline'
write_note "$DIST/Soundtrack/map_specific_soundtrack/streets" \
  '【街区 Streets of Tarkov 专属环境音乐】游戏内地图 ID: TarkovStreets' \
  '[Streets of Tarkov map music] In-game Location ID: TarkovStreets'
write_note "$DIST/Soundtrack/map_specific_soundtrack/terminal" \
  '【终点站 Terminal 专属环境音乐】游戏内地图 ID: Terminal' \
  '[Terminal map music] In-game Location ID: Terminal'
write_note "$DIST/Soundtrack/map_specific_soundtrack/woods" \
  '【森林 Woods 专属环境音乐】游戏内地图 ID: Woods' \
  '[Woods map music] In-game Location ID: Woods'

write_note "$DIST/DeathMusic" \
  '【死亡音乐】角色死亡后的结算界面音乐（对应游戏内 ArenaLose 音效类型）。' \
  '[Death music] Results-screen music on death (the game ArenaLose sound slot).'

write_note "$DIST/ExtractMusic" \
  '【撤离成功音乐】成功撤离后的结算界面音乐（对应游戏内 ArenaWin 音效类型）。' \
  '[Extraction music] Results-screen music on a successful extraction (the game ArenaWin sound slot).'

write_note "$DIST/UISounds/QuestCompleted" \
  '【UI 音效：任务完成】' '[UI sound: quest completed]'
write_note "$DIST/UISounds/QuestFailed" \
  '【UI 音效：任务失败】' '[UI sound: quest failed]'
write_note "$DIST/UISounds/QuestFinished" \
  '【UI 音效：任务全部完成】' '[UI sound: quest fully finished]'
write_note "$DIST/UISounds/QuestStarted" \
  '【UI 音效：接受任务】' '[UI sound: quest accepted]'
write_note "$DIST/UISounds/QuestSubtaskComplete" \
  '【UI 音效：任务子目标完成】' '[UI sound: quest subtask complete]'
write_note "$DIST/UISounds/DeathSting" \
  '【UI 音效：死亡提示音】' '[UI sound: death sting]'
write_note "$DIST/UISounds/ErrorSound" \
  '【UI 音效：错误提示音】' '[UI sound: error]'
write_note "$DIST/UISounds/TradeSound" \
  '【UI 音效：交易完成音】' '[UI sound: trade complete]'

echo "文件夹树创建完成 / Folder tree created:"
find "$DIST" -type d | sort

done
