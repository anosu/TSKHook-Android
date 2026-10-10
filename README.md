# TSKHook Android

本版本针对 [LemonLoader v0.7.4-android.3](https://github.com/LemonLoaderX/LemonLoader/releases/tag/v0.7.4-android.3) 构建，Mod 目标为 `net10.0`，需要 .NET 10 SDK；加载器运行时为 .NET 11，历史 `net6` 目录仍作为引用和安装路径。Loader 编译 DLL 来自正式发行包，不随 Mod ZIP 部署。使用 Patcher 安装该加载器时需 2.1.0 或更新版；从原始游戏 APK 准备新 payload，保留包名、签名及用户数据，勿混用旧运行时。游戏 Interop 保持各项目现有导出，运行验证需使用对应游戏版本。

TSKHook 的 LemonLoader Android 移植版，为 Unity IL2CPP 客户端提供运行参数控制和繁体中文本地化。

## 功能

- 可选固定游戏速度倍率并在游戏重置后自动恢复（默认关闭）
- 设置并保持目标帧率
- 角色名、剧情标题和剧情文本翻译
- 翻译下载、本地缓存和网络失败回退
- Utage 与 TextMeshPro 中文字体
- 图鉴角色缩放倍率调整
- Mod 加载、运行设置、配置变更、翻译和字体状态 Toast 通知

## 环境

- Android ARM64
- Unity IL2CPP
- LemonLoader 或兼容的 MelonLoader Android 环境

## 安装

从 Releases 下载 `TSKHook-Android.zip`，解压到游戏的 `MelonLoader` base 目录并保留归档中的 `Mods` 和 `UserData` 路径。配置文件首次启动后生成在 `MelonLoader/UserData/TSKHook.cfg`。

`[General]` 中的 `GameSpeedEnabled` 默认是 `false`，不干预游戏速度。设为 `true` 后按 `GameSpeed` 指定的倍率运行；两项修改均自动生效。运行中关闭开关会恢复启用前的速度，之后不再强制修改游戏速度。目标帧率设置不受此开关影响。

## 构建

Utility 通过固定提交的 Git submodule 与 `ProjectReference` 从源码构建，不再维护公共库 DLL 副本。

```powershell
git submodule update --init --recursive
python shared/ModEngineering/scripts/project.py package
```

输出位于 `artifacts/release/v<version>/`。本地共享开发目录、依赖升级和 CI 配置见 [shared/ModEngineering/docs/CONVENTIONS.md](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。游戏和加载器编译引用见 [dependencies/README.md](dependencies/README.md)。

## 自动发布

推送任意分支或创建 Pull Request 会执行 Release 构建验证，但不会上传占用 Actions 存储配额的 artifact。推送与项目版本一致的 `v*` 标签时，工作流会把 ZIP 和校验文件直接发布到对应 GitHub Release；标签和项目版本不一致会直接失败。

PC 版依赖键盘、Windows 通知或桌面窗口的功能没有迁移。

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。
