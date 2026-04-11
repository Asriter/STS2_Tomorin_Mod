# STS2_Tomorin_Mod

一个为《Slay the Spire 2》新增可玩角色「Tomorin」的整合型模组，核心机制为「Compose（作词）」：通过消耗手牌中特定类型的卡牌来生成或升级对应的「歌词」卡。项目采用 .NET 9 与 Godot 4.5.1，使用 HarmonyLib 注入，依赖 BaseLib 提供的通用基座与工具。

- Mod ID：`STS2_Tomorin_Mod`
- 命名空间：`STS2_Tomorin_Mod`
- 主要依赖：`Alchyr.Sts2.BaseLib`（见文末引用）


## 1. 项目简介

- 工程结构：
  - C# 脚本位于 `Scripts/`（卡牌、角色、命令、动态变量、遗物与各类池）。
  - Godot 资源位于 `STS2_Tomorin_Mod/`（图片、场景、动画、材质、本地化等）。
  - Harmony 补丁位于 `STS2_Tomorin_Mod/Scripts/Patch/`，用于注册角色与替换视觉节点。

## 2. 安装流程

1. 前往仓库 Releases 页面下载最新的模组压缩包。
2. 将压缩包完整解压到《杀戮尖塔 2》游戏根目录（与 `StS2.exe` 同级）。
3. 启动游戏后于 Mods 管理页启用本模组。

提示：压缩包内容通常包含 `mods/` 下的 `Alchyr.Sts2.BaseLib` 与 `STS2_Tomorin_Mod` 两个子目录，以及必要的清单与资源文件。


## 3. 配置流程

### 3.1 先决条件

- 安装 Godot 4.5.1（Mono 版本）。
- 安装 .NET SDK 9.0。

### 3.2 工程路径配置

编辑工程文件 `[STS2_Tomorin_Mod.csproj]`，根据你的本地环境设置下列属性（若自动检测失败）：

- `AutoSteamPath`：`true/false`。若为 `true`，项目会尝试自动解析 Steam 库与 StS2 安装路径；若解析不到，请设为 `false` 并手动配置路径。
- `GodotPath`：Godot 可执行文件路径（用于 `dotnet publish` 时打包导出 `.pck`）。示例：

```xml
<PropertyGroup>
  <AutoSteamPath>false</AutoSteamPath>
  <GodotPath>D:\\Apps\\Godot\\4.5.1\\Godot_v4.5.1_mono_win64.exe</GodotPath>
  <!-- 可按需覆盖：SteamLibraryPath、Sts2Path、ModsPath -->
</PropertyGroup>
```

若构建提示找不到游戏或 Steam 目录，请在 `.csproj` 中按注释覆盖下列变量：
- `SteamLibraryPath`：Steam 库路径。
- `Sts2Path`：StS2 安装目录。
- `ModsPath`：游戏根目录下 `mods` 目录（默认自动推导）。

### 3.3 构建与部署

开发迭代区分两种命令：

- 快速部署（仅 DLL 与 manifest，适合不改动 Godot 资源时）：

```bash
dotnet build
```

- 完整发布（DLL + manifest + Godot 资源 `.pck`，当你新增/修改图片、场景、动画、材质等资源时必须使用）：

```bash
dotnet publish
```

构建产物与落地点：
- `Alchyr.Sts2.BaseLib` 与 `STS2_Tomorin_Mod` 两个模组目录会被复制/更新到游戏根目录下的 `mods/` 文件夹。
- `dotnet build`：复制生成的 `STS2_Tomorin_Mod.dll`、清单与关联文件到 `mods/STS2_Tomorin_Mod/`；不会更新 `.pck`。
- `dotnet publish`：除上述文件外，还会导出并更新 Godot 资源包（`.pck`）到 `mods/STS2_Tomorin_Mod/`。

结论：改代码用 `build`，改资源必须 `publish`。若发布后游戏仍显示旧资源，先确认 `GodotPath` 有效，再检查 `mods/STS2_Tomorin_Mod` 下的 `.pck` 时间戳是否已刷新。


## 4. TODO List

- 平衡性调整
- 更多药水和遗物
- 更多事件
- 专属先古事件
- 动画、音效和特效（也许会有）
- 敌人和 Boss（可能吧）


## 5. 引用项目

- BaseLib（Alchyr）：https://github.com/Alchyr/BaseLib-StS2