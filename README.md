# 灵契之争 · Lingqi Card Game

基于 Unity 与 C# 的回合制身份卡牌游戏原型。项目目录保留早期名称 `ThreeKingdomsCardGame`，当前主菜单标题为 **灵契之争**。游戏提供二人、三人和五人场，围绕角色技能、实体牌面、装备、身份与公开疑迹展开策略对局。

本文依据 2026-10-13 本地最新工作目录代码整理，包含原 Git 索引之外的新增、修改与删除状态。

## 当前玩法

- **二人场**：玩家与 AI 的对战流程，独立选角和战斗场景。
- **三人场**：霸主与协战阵营对抗，包含阵营选择、AI 回合和阵营胜负结算。
- **五人身份局**：1 名执令者、1 名护令者、2 名逆契者、1 名游离者；执令者身份公开，其余身份按规则逐步揭示。
- **公开疑迹**：对执令者的敌对行为形成疑迹，配合查验、制造伪迹与消除疑迹等卡牌处理；AI 按可见身份和疑迹决策。
- **卡牌与装备**：攻击、防御、回复、锦囊、武器、防具、宝物与坐骑，支持花色、点数、属性伤害、距离、判定和响应链等结算逻辑。
- **标准 / 军争牌表**：`OfficialDeckCatalog` 构建 108 张标准/EX 与 52 张军争牌面的基础牌表；可选自定义补充牌由 Resources 加载，因此启用扩展后实际牌堆不固定为 160 张。
- **对局表现**：角色面板、卡牌展示、目标指示、出牌动画、战斗日志、玩法引导与结算界面。
- **音频与设置**：背景音乐、动作音效、卡牌通用语音和角色专属卡牌/技能语音；未配置专属语音时使用相应回退逻辑。

当前多人数模式为本地玩家加 AI 的场景，不是网络联机多人游戏。

## 五人场胜负规则

以下概括当前 `TurnManager.CheckFivePlayerGameOver()` 的实际判断顺序：

1. 如果唯一存活者是游离者，游离者获胜。
2. 如果所有逆契者已出局且执令者存活，执令者阵营获胜。当前实现不额外要求游离者出局。
3. 否则，如果执令者出局，逆契者阵营获胜。

身份公开、疑迹与 AI 决策相关实现仍在持续迭代，以当前源码为准。

## 环境与运行

- Unity Editor：**2022.3.61f1c1**，见 `ProjectSettings/ProjectVersion.txt`。
- 主要依赖：TextMesh Pro **3.0.7**、UGUI **1.0.0**、Unity 2D Feature **2.0.1**；完整清单见 `Packages/manifest.json`。

1. 克隆仓库，在 Unity Hub 中添加仓库根目录。
2. 使用对应版本打开并等待依赖解析与资源导入。
3. 打开 `Assets/Scenes/MainMenu.unity`，点击 Play。
4. 选择二人、三人或五人场，完成选角后进入战斗。
5. 使用鼠标选择手牌、目标及界面按钮，按当前回合和提示进行出牌、响应与结束回合。

Build Settings 已启用主菜单、三个选角场景和三个战斗场景。不要只从战斗场景启动来判断完整选角流程。

## 工程结构

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/Core/BattleGameManager.cs` | 新局初始化、角色与 AI 设置、重开 |
| `Assets/Scripts/Core/TurnManager.cs` | 回合、响应、效果结算、疑迹、AI 与胜负判断 |
| `Assets/Scripts/Core/GameSelectionData.cs` | 模式、角色选择与扩展牌堆偏好 |
| `Assets/Scripts/Core/IdentityAllocator.cs` | 五人身份随机分配 |
| `Assets/Scripts/Cards` | 卡牌数据/实例、基础牌表、补充牌、牌堆与牌面资源 |
| `Assets/Scripts/Players` | 玩家状态、角色数据与技能/语音配置 |
| `Assets/Scripts/UI` | 菜单、选角、手牌、目标、动画、设置、引导与结算 |
| `Assets/Scenes` | MainMenu、CharacterSelect2P/3P/5P、Battle2P/3P/5P |
| `Assets/ScriptableObjects`、`Assets/Resources` | 角色、运行时加载的数据与音频资源 |
| `Assets/Editor` | Windows 构建入口和五人规则回归检查 |
| `Tools` | 可选的本地语音生成工具 |

## 构建与规则检查

Windows 64 位构建可在 Unity 菜单 **Build → Windows 64** 执行。`BuildScript.BuildWindows64` 使用 Build Settings 中已启用场景，输出到 `build/ThreeKingdomsCardGame.exe`。

已有编辑器回归检查入口：**Tools → 灵契之争 → Run Five-Player Rules Regression**，对应 `FivePlayerRulesRegressionRunner.Run()`，覆盖身份比例、角色设置、疑迹与揭示、伪迹/洗迹/查验、AI 可见信息、胜负条件、濒死响应与语音映射等情况。

本文整理与上传不改动玩法代码，也不宣称上述回归检查已在本次执行通过。修改规则后应在对应 Unity 版本内运行检查，并实际游玩相关场景。

## 可选语音生成工具

`Tools/generate_game_voices.py` 与 `Tools/generate_character_voices.py` 用于本地生成语音。它们依赖另行配置的 CosyVoice 环境、模型和参考音频；`COSYVOICE_ROOT` 可覆盖默认本地路径。相关模型与外部环境不包含在仓库中，打开现有 Unity 游戏不需要运行这些工具。

## 仓库范围

保留源码、场景、资源、Unity `.meta`、Packages 与 ProjectSettings；排除 Library、Temp、Logs、UserSettings、构建目录和旧 ZIP。原目录已有的设计 Markdown 作为历史资料保留，若与当前源码不同，以源码及本文说明为准。

第三方字体、音乐、语音与卡牌相关素材需遵守各自权利和许可；本仓库未为第三方资源授予额外的再分发许可。项目当前定位为开发中的本地游戏原型。
