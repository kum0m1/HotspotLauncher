# Hotspot Launcher

> **生成声明**
> 本项目的源代码、构建脚本与文档均由 **DSH（DeepSeekHarness）** 生成。

---

## 描述

Hotspot Launcher 是一个用于**一键开启 Windows 移动热点**的轻量启动器，主程序体积约 10 KB。

双击运行后，程序会静默执行随附的 `hotspot.ps1`——热点的实际开启逻辑由该脚本完成——并在脚本执行结束后通过 Windows 通知中心反馈结果。主程序本身不需要管理员权限，也不会触发 UAC 提示。

---

## 执行流程

1. 查找与自身同目录的 `hotspot.ps1`
2. 以静默方式调用 PowerShell 执行该脚本，并等待其结束
3. 读取脚本退出码，向 Windows 通知中心发送对应通知

整个过程不显示任何窗口。程序本身不包含热点相关逻辑，只承担"启动脚本"与"汇报结果"两件事，因此可搭配不同实现的 `hotspot.ps1` 使用。

---

## 目录结构

```
Hotspot Launcher/
├─ HotspotLauncher.exe     ← 最终程序（发布时与 hotspot.ps1 同目录）
├─ hotspot.ps1             ← 热点脚本（使用者自行提供）
├─ src/
│   └─ HotspotLauncher.cs  ← 源代码
├─ build.cmd               ← 构建入口（双击即用）
├─ build.ps1               ← 构建脚本
└─ README.md
```

运行时只需要 **`HotspotLauncher.exe`** 和 **`hotspot.ps1`** 两个文件，其余为开发相关文件。

---

## 使用方法

1. 将 `HotspotLauncher.exe` 与 `hotspot.ps1` 放入**同一个文件夹**
2. 双击 `HotspotLauncher.exe`
3. 等待脚本执行完毕，查看通知

程序始终查找**自身所在目录**下的 `hotspot.ps1`，不依赖任何绝对路径，因此整个文件夹可以任意改名、移动或复制到其它机器，无需修改配置。

---

## 通知内容

| 执行情况 | 标题 | 正文 |
| --- | --- | --- |
| 脚本正常结束（退出码 0） | `Hotspot Network` | `Hotspot is completed` |
| 脚本异常结束（退出码非 0） | `Hotspot Network` | `Hotspot failed (exit code N)` |
| 未找到 `hotspot.ps1` | `Hotspot Network` | `Hotspot failed: hotspot.ps1 not found` |

> 表中的 `N` 为脚本实际返回的退出码。

通知以标准 toast 形式发送，会保留在通知中心（`Win + A`），进程退出后仍然可见。

---

## 系统要求

- Windows 10 或 Windows 11（64 位）
- .NET Framework 4.8（Windows 10/11 系统自带，无需单独安装）
- 主程序无需管理员权限
- 无需联网

---

## 日志与排障

每次运行都会向以下文件追加一段记录：

```
%LOCALAPPDATA%\HotspotLauncher\launcher.log
```

（将上述路径粘贴到资源管理器地址栏即可打开）

记录内容包括：exe 路径、脚本路径、进程完整性级别、PowerShell 进程 PID、脚本退出码、准备发送的通知内容，以及通知是否发送成功。**若通知未出现，应首先检查该日志。**

---

## 已知限制

- **专注助手会影响横幅显示。** 在"仅优先通知"或"仅闹钟"模式下，通知可能被抑制或不弹出横幅；只有在"关闭"模式、或把本程序加入优先列表时，才会正常弹出横幅。
- **脚本不结束，启动器不退出，通知也不会发出。** 程序会一直等待 `hotspot.ps1` 结束。若热点脚本在开启热点后保持常驻不退出，程序将随之挂起，通知需等脚本退出后才会到达。
- **不捕获脚本的标准输出。** 静默执行意味着脚本中 `Write-Host` 一类输出不会显示在任何位置；需要排查脚本自身问题时，应由脚本自行写入日志文件。
- **主程序不会为脚本提权。** 开启移动热点通常需要管理员权限，但本程序始终以普通用户权限运行；若 `hotspot.ps1` 需要管理员权限，应由脚本内部自行处理（例如自行以 `Start-Process -Verb RunAs` 重启自身）。

---

## 构建

双击 **`build.cmd`** 即可。构建脚本调用 Windows 自带的 C# 编译器 `csc.exe`（位于 `.NET Framework 4.x` 安装目录），不依赖 Visual Studio 或 .NET SDK，也无需联网。

构建行为：

- 成功时输出产物路径与文件大小，退出码为 `0`
- 失败时输出具体原因（源码缺失、编译器未找到、编译错误等），退出码为 `1`
- 仅重写 `dist\HotspotLauncher.exe`，不影响 `hotspot.ps1` 及其它文件

两个维护注意事项：

- Windows 自带的 `csc.exe` **仅支持 C# 5**，源码中不能使用局部函数、字符串插值、`?.`、`nameof`、表达式体成员等较新语法。
- `build.ps1` 含中文注释，**必须保存为 UTF-8 带 BOM**；否则 Windows PowerShell 5.1 会按本地代码页解析，导致语法错误。`build.cmd` 则应保持纯 ASCII。

---

## 可调整项

以下常量位于 `src/HotspotLauncher.cs` 顶部，修改后重新构建即可：

| 常量 | 默认值 | 作用 |
| --- | --- | --- |
| `ScriptFileName` | `hotspot.ps1` | 要执行的脚本文件名 |
| `Aumid` | `Kurumi.HotspotLauncher` | 通知使用的应用身份标识 |
| `AppDisplayName` | `Hotspot Network` | 通知中显示的应用名称 |
| `ToastTitle` | `Hotspot Network` | 通知标题 |
| `SuccessBody` | `Hotspot is completed` | 成功时的正文 |
| `FailBodyFormat` | `Hotspot failed (exit code {0})` | 失败时的正文，`{0}` 为退出码 |
| `ScriptMissingBody` | `Hotspot failed: hotspot.ps1 not found` | 找不到脚本时的正文 |

---

## 实现说明

### 静默执行

通过 `ProcessStartInfo` 启动 `powershell.exe`，并设置：

```csharp
UseShellExecute = false;
CreateNoWindow  = true;
WindowStyle     = ProcessWindowStyle.Hidden;
```

命令行等价于：

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<exe 同目录>\hotspot.ps1"
```

主进程调用 `WaitForExit()` 阻塞等待，随后读取 `ExitCode` 作为通知依据。

### 通知机制

使用 Windows 标准 toast 通知（`Windows.UI.Notifications`）。由于未打包的 Win32 程序没有程序包标识，直接调用通知 API 会返回 `0x80073D54`（该进程没有程序包标识符），因此程序在每次运行时会：

1. 将应用身份写入当前用户的注册表项
   `HKCU\SOFTWARE\Classes\AppUserModelId\<Aumid>`（含 `DisplayName` 等值，无需管理员权限）
2. 通过 `ToastNotificationManager.CreateToastNotifier(Aumid).Show(...)` 发送通知

WinRT 类型在 .NET Framework 下通过 `Type.GetType("..., ContentType=WindowsRuntime")` 获取，无需 Windows SDK。通知相关异常均被捕获并写入日志，不会导致程序崩溃。

### 权限

可执行文件内嵌的应用程序清单声明为 `asInvoker`，始终以调用者权限运行，不请求提升。
