<p align="center"><img src="assets/app-icon.png" alt="DynamicResolutionSwitcher 图标" width="128" height="128"></p>

<h1 align="center">DynamicResolutionSwitcher</h1>

<p align="center">免安装的 Windows 主显示器分辨率与刷新率切换工具<br><a href="README.en.md">English</a> · <a href="https://github.com/xukunc463-droid/dynamic-resolution-switcher/releases/latest">下载最新版本</a></p>

<p align="center">
  <a href="https://github.com/xukunc463-droid/dynamic-resolution-switcher/actions/workflows/windows-build.yml"><img src="https://github.com/xukunc463-droid/dynamic-resolution-switcher/actions/workflows/windows-build.yml/badge.svg" alt="Windows build"></a>
  <a href="https://github.com/xukunc463-droid/dynamic-resolution-switcher/releases"><img src="https://img.shields.io/github/v/release/xukunc463-droid/dynamic-resolution-switcher?display_name=tag&label=release" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/xukunc463-droid/dynamic-resolution-switcher" alt="MIT license"></a>
</p>

![DynamicResolutionSwitcher 软件界面](docs/screenshot.png)

每次进游戏前都要打开显卡控制面板，手动切换分辨率和刷新率很麻烦。这个工具会列出 Windows 与显卡驱动已经提供的显示模式，直接在桌面上选择并切换。

## 下载

在 [Releases](https://github.com/xukunc463-droid/dynamic-resolution-switcher/releases/latest) 下载 DynamicResolutionSwitcher-v*-win.zip，解压后运行 DynamicResolutionSwitcher.exe。单文件、免安装：不需要管理员权限，不联网，也不收集数据。

## 功能

- 动态读取 Windows 主显示器可用的分辨率与刷新率
- 默认筛选当前刷新率，自动标出当前显示模式
- 双击、Enter 或按钮切换；F5 刷新；Esc 关闭
- 在应用前使用 Windows API 测试目标模式
- 不会创建或修改自定义分辨率

## 《无畏契约》与拉伸显示

作者在自己的《无畏契约》环境中，配合 NVIDIA 缩放设置、游戏内显示设置和已创建的自定义分辨率，观察到了真实拉伸效果。这个工具只负责切换 Windows 主显示器已存在的显示模式；它不会改显卡缩放策略、游戏内设置、FPS 或反作弊配置。不同显卡、显示器和驱动的结果可能不同。

## 已验证环境

| 项目 | 已验证结果 |
| --- | --- |
| 显示模式 | 1920×1080 @ 165 Hz ↔ 1720×1080 @ 165 Hz |
| 显卡 | NVIDIA GeForce GTX 1050 Ti |

完整范围和反馈模板见 [兼容性说明](docs/compatibility.md)。

## 注意事项

- 当前版本仅切换 Windows 主显示器。
- 可见模式取决于显示器和显卡驱动；请先在驱动控制面板中创建自定义分辨率。
- 没有倒计时自动恢复功能。画面异常时可按 Win + Ctrl + Shift + B 重启显卡驱动，或在 Windows 显示设置中恢复。
- 程序没有数字签名；请核对 Release 内的 SHA256SUMS.txt。

## 从源码构建

    powershell -ExecutionPolicy Bypass -File .\build.ps1

## 分享与反馈

- [V2EX、Reddit 与短视频真实分享文案](docs/share-copy.md)
- 欢迎按兼容性说明中的模板提交 Issue。

## License

[MIT](LICENSE)
