# FavoriteMusicPlayer（塔科夫离线版自定义音乐播放器）

> 根据原 [Bobby's Music Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player) 项目（作者 BobbyRenzobbi）由 ZCode（AI 编程助手）编写移植。
> 特别感谢原作者 BobbyRenzobbi 的开源分享，以及 SPT 社区编写的客户端 mod 开发文档——没有这些基础就没有本项目。原项目基于 SPT 3.11.x，本项目将其完整移植到 SPT 4.0.13 / 4.1.6 并新增多项改进。

[English version](README_EN.md)

## 简介

BepInEx 客户端插件（非服务器 mod），把游戏里的各类音乐/音效替换成你自己的音乐文件，适用于 **SPT（逃离塔科夫离线版）4.0.13 与 4.1.6**（基座游戏 0.16.9）——两条版本线各自发布安装包，同一套源码构建。

| 功能 | 说明 |
|---|---|
| 主菜单音乐 | 主菜单循环播放 |
| 局内环境音乐 | 进图随机组成歌单；支持全图通用 + 14 张地图各自专属 |
| 出生音乐 | 每局出生随机一首（懒加载，只加载选中的那首） |
| 战斗音乐 | 开枪/被击中/近距枪声/手雷爆炸触发，自动淡入淡出 |
| 死亡/撤离音乐 | 结算界面音乐 |
| UI 音效 | 任务、交易、错误提示等 8 种界面音效 |
| 点唱机 | 小键盘 2/4/5/6：重播/上一首/下一首/暂停（菜单与局内通用） |
| 时长限制 | 出生/死亡撤离/UI 音效可设最长播放时长，到时自动淡出停止 |
| 双语配置面板 | ConfigurationManager 中英文切换（Language / 语言） |

## 安装

1. **前置要求**：SPT 4.0.13 或 4.1.6 游戏安装（自带 BepInEx 5；更低的 4.1.x 未测试）
2. **卸载旧版**：如果你装过原版 Bobby's Music Player 或其它名称的音乐替换 mod，请先删除其插件文件夹——两个音乐 mod 同时运行会导致补丁冲突/双重播放
3. **下载并复制文件**：从本仓库的 **Releases** 页面下载**与你 SPT 版本对应**的 FavoriteMusicPlayer 压缩包（4.0.13 / 4.1.6 两条版本线各自发布）并解压，把里面的**全部内容**复制到 `游戏目录\BepInEx\plugins\FavoriteMusicPlayer\`
4. **放音乐**：把音频文件放进对应文件夹（对照下面的表格，或看每个文件夹里的 aboutThisFolder.txt）
5. **启动游戏**：主菜单按 F1（部分安装为 F12）打开 ConfigurationManager，能看到 "FavoriteMusicPlayer" 分节即安装成功

复制后的目录结构（注意 DLL 要直接位于 `FavoriteMusicPlayer\` 下，不要多套一层文件夹）：

```
BepInEx\plugins\FavoriteMusicPlayer\
├── FavoriteMusicPlayer.dll
├── CustomMenuMusic\
├── DeathMusic\
├── ExtractMusic\
├── Soundtrack\
└── UISounds\
```

> 提示：mod 首次启动会自动创建全部音乐文件夹（即使只复制了 DLL），之后直接往里放文件即可。
> 卸载：删除 `BepInEx\plugins\FavoriteMusicPlayer\` 文件夹与 `BepInEx\config\halfBreadPin.FavoriteMusicPlayer.cfg`。

## 音乐文件夹对照表

安装后音乐放在 `BepInEx\plugins\FavoriteMusicPlayer\` 下（完整说明见 [docs/音乐文件夹说明.md](docs/音乐文件夹说明.md)）：

```
FavoriteMusicPlayer\
├── CustomMenuMusic\sounds\          ← 主菜单背景音乐
├── Soundtrack\
│   ├── default_soundtrack\          ← 局内环境音乐（所有地图通用）
│   ├── combat_music\                ← 战斗音乐
│   ├── spawn_music\                 ← 出生音乐
│   └── map_specific_soundtrack\     ← 地图专属环境音乐
│       ├── customs\ 海关    ├── factory\ 工厂   ├── ground_zero\ 街区中心
│       ├── interchange\ 立交桥 ├── labs\ 实验室  ├── labyrinth\ 迷宫
│       ├── lighthouse\ 灯塔  ├── reserve\ 储备站 ├── shoreline\ 海岸线
│       ├── streets\ 街区    ├── terminal\ 终点站 └── woods\ 森林
├── DeathMusic\                      ← 死亡结算音乐
├── ExtractMusic\                    ← 撤离成功结算音乐
└── UISounds\                        ← 界面音效（每个子文件夹一种）
```

**支持的音频格式**：`.wav` `.ogg` `.mp3`（推荐）以及 `.mp2` `.aiff` 等白名单格式；`.flac` 不支持（Unity 引擎限制），请转换为 ogg/mp3。

## 配置

游戏内按 F1（或 F12）打开 ConfigurationManager：

- "0. 语言设置 / Language"：切换中英文显示
- 各类音量、歌单来源（混合/仅地图/仅默认）、歌单长度、战斗触发条件与时长、室内/耳机音量倍率、点唱机快捷键
- "5. 时长限制设置"：出生/死亡撤离/UI 音效最长播放时长（秒），0=不限

配置文件：`BepInEx\config\halfBreadPin.FavoriteMusicPlayer.cfg`。

## 编译

一套源码、双版本目标（`src/Common.props` 为公共配置，版本差异集中在 `Plugin.cs` 顶部的兼容层）：

```powershell
cd src
dotnet build FavoriteMusicPlayer.csproj -c Release       # SPT 4.0.13 → dist\4.0.13\FavoriteMusicPlayer\
dotnet build FavoriteMusicPlayer.4_1.csproj -c Release   # SPT 4.1.6  → dist\4.1.6\FavoriteMusicPlayer\
```

- 需 .NET SDK 8+（无 NuGet 依赖，离线可构建）
- 4.0.13 目标默认引用 `E:\ETF`、4.1.6 目标默认引用 `F:\EFT416`（本机游戏目录），可用 `-p:GameDir="..."` 覆盖；构建只读取游戏目录 DLL，绝不写入
- 发布 Release 压缩包时请按版本线命名（如 `FavoriteMusicPlayer-1.0.0-4.0.13.zip`），用户按其 SPT 版本选择下载
- 项目无游戏文件，游戏更新后请先按 AGENTS.md 的核验流程比对补丁目标签名

## 故障排查

日志位于 `游戏目录\BepInEx\LogOutput.log`：

| 现象 | 排查 |
|---|---|
| 完全没有自定义音乐 | 日志搜 `FavoriteMusicPlayer`，确认 `plugin loaded!`；搜 `找不到` 看哪个补丁未生效 |
| 任务完成时"我的音乐+系统提示音"同时响 | 正常：游戏同时触发"子目标完成"与"任务完成"两个音效事件，往 `UISounds\QuestSubtaskComplete\` 也放音乐或放静音文件即可 |
| 某文件加载失败 | 日志有 `Failed To Fetch Audio Clip`——通常是格式问题，转成 wav/mp3 |
| 环境音乐音量太小 | 默认 0.05 是刻意压低的，F1 面板调大 |
| 进图没有地图专属音乐 | 确认歌单来源配置 + 对应地图文件夹有音频文件 |

## 致谢与许可

- 原项目：[BobbyRenzobbi/Bobbys-Music-Player](https://github.com/BobbyRenzobbi/Bobbys-Music-Player)（v1.2.4，SPT 3.11.x）——本项目的全部核心机制源自它
- [SPT 官方 mod 开发文档](https://wiki.sp-tushonka.com/modding/Modding_Resources) 与 Jehree 的客户端 mod 模板
- 许可：MIT（见 [LICENSE](LICENSE)）。原项目未附带开源协议，本项目出于社区共享精神以 MIT 发布并完整署名；如原作者提出异议，本仓库将应要求下架或调整
