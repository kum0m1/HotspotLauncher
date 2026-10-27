## 构建

适用于无Visual Studio或.NET.SDK的情况。

双击 **`build.cmd`** 即可。构建脚本调用 Windows 自带的 C# 编译器 `csc.exe`（位于 `.NET Framework 4.x` 安装目录），不依赖 Visual Studio 或 .NET SDK，也无需联网。

构建行为：

- 成功时输出产物路径与文件大小，退出码为 `0`
- 失败时输出具体原因（源码缺失、编译器未找到、编译错误等），退出码为 `1`
- 仅重写 `dist\HotspotLauncher.exe`，不影响 `hotspot.ps1` 及其它文件

两个维护注意事项：

- Windows 自带的 `csc.exe` **仅支持 C# 5**，源码中不能使用局部函数、字符串插值、`?.`、`nameof`、表达式体成员等较新语法。
- `build.ps1` 含中文注释，**必须保存为 UTF-8 带 BOM**；否则 Windows PowerShell 5.1 会按本地代码页解析，导致语法错误。`build.cmd` 则应保持纯 ASCII。
