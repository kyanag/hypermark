# WinForms → Avalonia UI 迁移计划

> 目标：将 HyperMark.Desktop 从 WinForms 迁移到 Avalonia UI，以支持 Native AOT 编译。

## 背景

当前 `HyperMark.Desktop` 使用 WinForms 实现系统托盘功能，但 WinForms 不支持 AOT 编译。Desktop 项目的 UI 极轻量（仅托盘图标 + 右键菜单），适合迁移到 Avalonia UI。

## 迁移范围

| 文件 | 状态 | 说明 |
|------|------|------|
| `Program.cs` | **重写** | 替换 WinForms 初始化和消息循环为 Avalonia |
| `TrayIcon.cs` | **重写** | 用 Avalonia 的 `TrayIcon` API 替换 WinForms `NotifyIcon` |
| `StartupManager.cs` | **保留** | 纯注册表操作，不依赖 WinForms，无需修改 |
| `HyperMark.Desktop.csproj` | **修改** | 移除 WinForms，添加 Avalonia 包引用，启用 AOT |
| `Resources/app.ico` | **保留** | 图标资源不变 |

## 执行步骤

### 步骤 1：修改 csproj — 切换到 Avalonia

将 `HyperMark.Desktop.csproj` 改为：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <!-- 移除 net10.0-windows，Avalonia 跨平台 -->
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>HyperMark.Desktop</RootNamespace>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <StripSymbols>true</StripSymbols>
    <BuiltInComInteropSupport>false</BuiltInComInteropSupport>
    <SelfContained>false</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\HyperMark.Web\HyperMark.Web.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Resources\app.ico" />
    <Content Include="..\HyperMark.Web\wwwroot\**" CopyToOutputDirectory="PreserveNewest" LinkBase="wwwroot" />
  </ItemGroup>

  <!-- Avalonia XAML 自动生成 -->
  <ItemGroup>
    <AvaloniaResource Include="Resources\**" />
  </ItemGroup>

</Project>
```

关键变更：
- 移除 `UseWindowsForms`
- 移除 `net10.0-windows`，改为 `net10.0`（Avalonia 跨平台）
- 添加 Avalonia NuGet 包
- 启用 `PublishAot`
- 移除 `FrameworkReference` 对 `Microsoft.AspNetCore.App` 的显式引用（由 ProjectReference 隐式传递）

### 步骤 2：重写 Program.cs — Avalonia 启动

```csharp
using Avalonia;
using HyperMark.Config;
using HyperMark.Web;

namespace HyperMark.Desktop;

internal static class Program
{
    private static WebApplication? _app;
    private static TrayIconManager? _trayIcon;

    [STAThread]
    public static async Task Main(string[] args)
    {
#if !DEBUG
        // 单实例检查（保留原有逻辑）
        var current = Process.GetCurrentProcess();
        if (Process.GetProcessesByName(current.ProcessName)
            .Any(p => p.Id != current.Id))
        {
            MessageBox.Show("HyperMark 已在运行中，请检查系统托盘。", "提示");
            return;
        }
#endif

        AppConfig.EnsureInitialized();

        // 创建并启动 Web 服务
        _app = WebHost.CreateApp(args);
        await _app.StartAsync();

        // 构建并运行 Avalonia 应用（含托盘图标）
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _trayIcon?.Dispose();
            if (_app != null)
                await _app.StopAsync();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

### 步骤 3：创建 Avalonia App 类

新建 `App.cs`：

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace HyperMark.Desktop;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 无主窗口 — 仅托盘模式
        // 不创建任何 Window，应用将在托盘运行
        base.OnFrameworkInitializationCompleted();
    }
}
```

新建 `App.axaml`：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HyperMark.Desktop.App">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

### 步骤 4：重写 TrayIcon — Avalonia NativeMenu

新建 `TrayIconManager.cs`（替代原 `TrayIcon.cs`）：

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using HyperMark.Config;

namespace HyperMark.Desktop;

public class TrayIconManager : IDisposable
{
    private readonly WebApplication _app;
    private readonly TrayIcon _trayIcon;
    private bool _isRunning = true;

    public TrayIconManager(WebApplication app)
    {
        _app = app;

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://HyperMark.Desktop/Resources/app.ico"))),
            ToolTipText = "HyperMark - 服务运行中",
            IsVisible = true,
        };

        // 构建右键菜单
        var menu = new NativeMenu();

        // 状态指示
        var statusItem = new NativeMenuItem { Header = "✅ 服务运行中", IsEnabled = false };
        menu.Add(statusItem);

        // 暂停/启动
        var toggleItem = new NativeMenuItem { Header = "暂停服务" };
        toggleItem.Click += (_, _) => ToggleService(toggleItem, statusItem);
        menu.Add(toggleItem);

        menu.Add(new NativeMenuItemSeparator());

        // 打开 Web 界面
        var openItem = new NativeMenuItem { Header = "打开 Web 界面" };
        openItem.Click += (_, _) => OpenWebInterface();
        menu.Add(openItem);

        // 打开数据目录
        var dataItem = new NativeMenuItem { Header = "打开数据目录" };
        dataItem.Click += (_, _) => OpenDataDirectory();
        menu.Add(dataItem);

        // 设置子菜单
        var settingsMenu = new NativeMenu();
        var autoStartItem = new NativeMenuItem
        {
            Header = "开机自动启动",
            IsChecked = StartupManager.IsEnabled()
        };
        autoStartItem.Click += (_, _) =>
        {
            autoStartItem.IsChecked = !autoStartItem.IsChecked;
            StartupManager.SetEnabled(autoStartItem.IsChecked);
        };
        settingsMenu.Add(autoStartItem);

        var settingsItem = new NativeMenuItem { Header = "设置" };
        settingsItem.Menu = settingsMenu;
        menu.Add(settingsItem);

        menu.Add(new NativeMenuItemSeparator());

        // 退出
        var exitItem = new NativeMenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApp();
        menu.Add(exitItem);

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (_, _) => OpenWebInterface();
    }

    private async void ToggleService(NativeMenuItem toggleItem, NativeMenuItem statusItem)
    {
        if (_isRunning)
        {
            await _app.StopAsync();
            _trayIcon.ToolTipText = "HyperMark - 服务已停止";
            statusItem.Header = "⏹ 服务已停止";
            toggleItem.Header = "启动服务";
            _isRunning = false;
        }
        else
        {
            await _app.StartAsync();
            _trayIcon.ToolTipText = "HyperMark - 服务运行中";
            statusItem.Header = "✅ 服务运行中";
            toggleItem.Header = "暂停服务";
            _isRunning = true;
        }
    }

    private void OpenWebInterface()
    {
        var url = AppConfig.Instance.Http;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OpenDataDirectory()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hypermark");
        Process.Start(new ProcessStartInfo(dataDir) { UseShellExecute = true });
    }

    private void ExitApp()
    {
        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    public void Dispose()
    {
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }
}
```

### 步骤 5：在 App 中初始化 TrayIcon

修改 `App.cs` 的 `OnFrameworkInitializationCompleted`：

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        // 无主窗口，仅托盘
        // TrayIcon 在 Program.Main 中创建
    }
    base.OnFrameworkInitializationCompleted();
}
```

并在 `Program.Main` 中，Avalonia 启动前初始化 TrayIcon：

```csharp
// 在 BuildAvaloniaApp().StartWithClassicDesktopLifetime(args) 之前
_trayIcon = new TrayIconManager(_app);

// 处理 --minimized 参数（开机自启静默模式）
if (!args.Contains("--minimized"))
{
    _trayIcon.ShowStartupNotification();
}
```

### 步骤 6：删除旧文件

- 删除 `TrayIcon.cs`（已被 `TrayIconManager.cs` 替代）
- 保留 `StartupManager.cs`（无需修改）

### 步骤 7：验证编译和运行

```bash
# 普通编译测试
dotnet build HyperMark.Desktop

# AOT 发布测试
dotnet publish HyperMark.Desktop -c Release -r win-x64

# 运行测试
dotnet run --project HyperMark.Desktop
```

## 注意事项

1. **AOT 兼容性**：Avalonia 11.x 官方支持 Native AOT，但需确保不使用反射相关的 API
2. **StartupManager.cs**：该文件使用 `Microsoft.Win32.Registry`，在 `net10.0`（非 windows TFM）下需要条件编译或改为 `net10.0-windows`
3. **单实例检查**：`MessageBox.Show` 是 WinForms API，需要替换为 Avalonia 的 `Window` 或直接用 `Console.Error.WriteLine` + 退出
4. **TrayIcon 差异**：Avalonia 的 `TrayIcon` API 与 WinForms 的 `NotifyIcon` 有差异，双击事件可能需要用 `Clicked` 替代
5. **图标资源**：Avalonia 使用 `avares://` URI 方案访问嵌入资源，需将 ico 放入 `AvaloniaResource`

## 风险评估

| 风险 | 级别 | 缓解措施 |
|------|------|----------|
| Avalonia TrayIcon 在某些 Windows 版本上行为不一致 | 低 | Avalonia 11.x 托盘支持已成熟 |
| AOT 编译后反射相关代码失败 | 中 | 使用 source-generated JSON，避免反射 |
| StartupManager 依赖 Windows API | 低 | 保持 `net10.0-windows` TFM 或条件编译 |
| 无主窗口模式的 Avalonia 生命周期管理 | 中 | 参考 Avalonia 官方 TrayIcon 示例 |
