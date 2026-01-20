# InfiniteWin - Virtual Canvas Desktop Application

> 一个可缩放的虚拟画布桌面应用，让您像管理图片一样管理 Windows 窗口

![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## 📖 项目简介

InfiniteWin 是一个创新的 WPF 桌面应用程序，它提供了一个无限的虚拟画布，让您可以将任何 Windows 应用窗口作为缩略图放置在画布上。所有窗口缩略图会随着画布的缩放而缩放，帮助您更好地组织和管理工作区。

灵感来源于美术工具 PureRef，但专为应用窗口管理而设计。

## ✨ 核心特性

### 虚拟画布
- 🔍 **可缩放画布**: 支持 10% 到 500% 的缩放范围
- 🖱️ **鼠标滚轮缩放**: 以鼠标位置为中心进行缩放
- ✋ **拖拽平移**: 右键或中键拖动画布
- 🎨 **暗色主题**: 现代化的深色界面设计

### 窗口管理
- 🪟 **实时窗口缩略图**: 使用 Windows DWM API 实时显示窗口内容
- 📌 **拖放定位**: 在画布上自由移动窗口缩略图
- 🖱️ **双击激活**: 双击缩略图即可激活原始窗口
- ❌ **便捷关闭**: 每个缩略图都有独立的关闭按钮
- 🔵 **视觉反馈**: 蓝色高亮边框和圆角设计

### 窗口选择
- 📋 **智能枚举**: 自动列出所有可见的窗口
- 🔍 **标题显示**: 清晰展示窗口标题
- ⚡ **快速添加**: 支持双击或点击确定添加窗口

## 🎮 使用说明

### 基本操作

**画布控制**:
- `鼠标滚轮` - 缩放画布（以鼠标位置为中心）
- `右键拖动` 或 `中键拖动` - 平移画布
- `Reset View 按钮` - 重置视图到 100% 缩放和原点位置

**窗口操作**:
- `Add Window 按钮` - 打开窗口选择器添加新窗口
- `左键拖动缩略图` - 移动窗口缩略图位置
- `双击缩略图` - 激活并切换到原始窗口
- `点击 × 按钮` - 从画布移除窗口缩略图

### 快捷键列表

| 操作 | 快捷方式 |
|------|----------|
| 放大 | 向上滚动鼠标滚轮 |
| 缩小 | 向下滚动鼠标滚轮 |
| 平移 | 右键/中键 + 拖动 |
| 移动窗口 | 左键 + 拖动缩略图 |
| 激活窗口 | 双击缩略图 |
| 重置视图 | 点击 Reset View 按钮 |

## 🛠️ 技术栈

### 框架与平台
- **.NET 8.0**: 现代化的 .NET 平台
- **WPF (Windows Presentation Foundation)**: 用于构建丰富的桌面应用

### Windows API
- **DWM API (Desktop Window Manager)**:
  - `DwmRegisterThumbnail` - 注册窗口缩略图
  - `DwmUnregisterThumbnail` - 注销缩略图
  - `DwmUpdateThumbnailProperties` - 更新缩略图属性
  - `DwmQueryThumbnailSourceSize` - 查询源窗口大小

- **User32 API**:
  - `EnumWindows` - 枚举所有顶层窗口
  - `IsWindowVisible` - 检查窗口可见性
  - `GetWindowText` - 获取窗口标题
  - `SetForegroundWindow` - 激活窗口

### 核心技术
- **P/Invoke**: 用于调用 Win32 API
- **WPF Transforms**: ScaleTransform 和 TranslateTransform 实现画布缩放和平移
- **IDisposable Pattern**: 确保资源正确清理

## 🚀 构建和运行

### 系统要求
- **操作系统**: Windows 10 或 Windows 11
- **.NET SDK**: .NET 8.0 或更高版本
- **开发工具** (可选): Visual Studio 2022 或 Visual Studio Code

### 构建步骤

1. **克隆仓库**
   ```bash
   git clone https://github.com/KOPElan/infinitewin.git
   cd infinitewin
   ```

2. **构建项目**
   ```bash
   cd InfiniteWin
   dotnet build
   ```

3. **运行应用**
   ```bash
   dotnet run
   ```

### 发布应用

创建独立可执行文件:
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

生成的可执行文件位于 `bin/Release/net6.0-windows/win-x64/publish/`

## 📁 项目结构

```
InfiniteWin/
├── InfiniteWin.csproj           # WPF 项目文件
├── App.xaml                      # 应用程序定义
├── App.xaml.cs                   # 应用程序逻辑
├── MainWindow.xaml               # 主窗口 UI
├── MainWindow.xaml.cs            # 主窗口逻辑（画布交互）
├── WindowThumbnailControl.cs     # 窗口缩略图控件（DWM API）
├── WindowSelectorDialog.xaml     # 窗口选择器 UI
└── WindowSelectorDialog.xaml.cs  # 窗口选择器逻辑
```

## 🎨 设计规范

### 颜色方案
- **背景色**: `#FF2B2B2B` (深灰)
- **窗口边框**: `#FF6496FF` (蓝色)
- **工具栏背景**: `#CC000000` (半透明黑)
- **提示文字**: `#88FFFFFF` (半透明白)
- **关闭按钮**: `#C8FF0000` (半透明红)

## 🔮 未来功能规划

- [ ] 窗口缩略图大小调整
- [ ] 布局保存和加载功能
- [ ] 更多快捷键支持
- [ ] 窗口分组功能
- [ ] 搜索和过滤窗口
- [ ] 多显示器支持优化
- [ ] 导出画布为图片

## 📄 许可证

本项目采用 [GPL-3.0 许可证](LICENSE)。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 👨‍💻 作者

**KOPElan**

## 🙏 致谢

灵感来源于 [PureRef](https://www.pureref.com/) - 一个优秀的参考图管理工具。

---

⭐ 如果这个项目对你有帮助，请给个 Star！