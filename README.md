# TSKHook Android

TSKHook 的 LemonLoader Android 移植版，为 Unity IL2CPP 客户端提供运行参数控制和繁体中文本地化。

## 功能

- 固定游戏速度倍率并在游戏重置后自动恢复
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

## 构建

仓库跟踪字体、Utility 和项目实际引用的最小 MelonLoader/Interop DLL，可以在干净环境中直接构建：

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
```

输出位于 `artifacts/release/v<version>/`。游戏或 LemonLoader 更新后，按照 [dependencies/README.md](dependencies/README.md) 使用 `scripts/sync-dependencies.ps1` 刷新最小引用集。

## 自动发布

推送任意分支或创建 Pull Request 会执行 Release 构建验证，但不会上传占用 Actions 存储配额的 artifact。推送与项目版本一致的 `v*` 标签时，工作流会把 ZIP 和校验文件直接发布到对应 GitHub Release；标签和项目版本不一致会直接失败。

PC 版依赖键盘、Windows 通知或桌面窗口的功能没有迁移。
