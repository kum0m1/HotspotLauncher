using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace HotspotLauncher
{
    /// <summary>
    /// 静默执行与自身同目录下的 hotspot.ps1，结束后向 Windows 通知中心发送 toast 通知。
    /// 通知方案：注册表注册 AUMID + WinRT ToastNotificationManager（方案 B）。
    /// 目标框架 .NET Framework 4.8；编译器为系统自带 csc，仅支持 C# 5。
    /// </summary>
    internal static class Program
    {
        // ============================================================
        //  ★ 配置区
        // ============================================================

        // 要执行的脚本文件名（放在 exe 同目录）
        private const string ScriptFileName = "hotspot.ps1";

        // 通知的应用身份（注册到 HKCU 的 AUMID），改一次即可，保持稳定
        private const string Aumid = "Kurumi.HotspotLauncher";

        // 通知中心里显示的应用名
        private const string AppDisplayName = "Hotspot Network";

        // 通知标题（成功与失败都用它）
        private const string ToastTitle = "Hotspot Network";

        // 成功时的正文
        private const string SuccessBody = "Hotspot is completed";

        // 失败时的正文，{0} 会被替换成脚本退出码
        private const string FailBodyFormat = "Hotspot failed (exit code {0})";

        // 找不到脚本时的正文
        private const string ScriptMissingBody = "Hotspot failed: hotspot.ps1 not found";

        // ============================================================
        //  配置区结束
        // ============================================================

        private static readonly StringBuilder LogBuffer = new StringBuilder();

        [STAThread]
        private static void Main()
        {
            string exePath;
            string exeDir;
            try
            {
                exePath = Assembly.GetExecutingAssembly().Location;
                exeDir = Path.GetDirectoryName(exePath);
            }
            catch (Exception ex)
            {
                Log("无法定位自身路径: " + ex);
                Flush();
                return;
            }

            string scriptPath = Path.Combine(exeDir, ScriptFileName);

            Log("=== 启动 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            Log("exe        : " + exePath);
            Log("脚本       : " + scriptPath);
            Log("完整性级别 : " + IntegrityLevel());

            if (!File.Exists(scriptPath))
            {
                Log("脚本不存在，终止。");
                Notify(ScriptMissingBody);
                Flush();
                return;
            }

            int exitCode;
            try
            {
                // 静默执行：不弹控制台窗口
                ProcessStartInfo psi = new ProcessStartInfo(
                    "powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = exeDir;

                using (Process p = Process.Start(psi))
                {
                    Log("已启动 powershell.exe，PID=" + p.Id);
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                    Log("脚本执行结束，退出码=" + exitCode);
                }
            }
            catch (Exception ex)
            {
                Log("执行脚本时异常: " + ex);
                Notify("Hotspot failed: " + ex.Message);
                Flush();
                return;
            }

            if (exitCode == 0)
                Notify(SuccessBody);
            else
                Notify(string.Format(FailBodyFormat, exitCode));

            Flush();
        }

        // ---------------- 通知（方案 B）----------------

        /// <summary>注册 AUMID 并发送 toast。任何失败都记进日志，不抛异常。</summary>
        private static void Notify(string body)
        {
            Log("准备发送通知: [" + ToastTitle + "] " + body);
            try
            {
                // 1) 注册 AUMID（幂等；不需要管理员，写在 HKCU 下）
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                           @"SOFTWARE\Classes\AppUserModelId\" + Aumid))
                {
                    key.SetValue("DisplayName", AppDisplayName, RegistryValueKind.String);
                    key.SetValue("IconBackgroundColor", "0xFF0078D7", RegistryValueKind.String);
                }
                Log("AUMID 已注册: " + Aumid);

                // 2) 取 WinRT 类型（.NET Framework 下不需要 Windows SDK）
                Type tManager = Type.GetType(
                    "Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime", false);
                Type tToast = Type.GetType(
                    "Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime", false);
                Type tXml = Type.GetType(
                    "Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType=WindowsRuntime", false);

                if (tManager == null || tToast == null || tXml == null)
                {
                    Log("通知失败：WinRT 通知类型解析不到");
                    return;
                }

                // 3) 构造 toast XML
                string xml = "<toast><visual><binding template=\"ToastGeneric\">"
                           + "<text>" + EscapeXml(ToastTitle) + "</text>"
                           + "<text>" + EscapeXml(body) + "</text>"
                           + "</binding></visual></toast>";

                object doc = Activator.CreateInstance(tXml);
                tXml.GetMethod("LoadXml", new Type[] { typeof(string) })
                    .Invoke(doc, new object[] { xml });
                object toast = Activator.CreateInstance(tToast, new object[] { doc });

                // 4) CreateToastNotifier(AUMID)
                MethodInfo create = null;
                foreach (MethodInfo m in tManager.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (m.Name == "CreateToastNotifier" && ps.Length == 1 && ps[0].ParameterType == typeof(string))
                    {
                        create = m;
                        break;
                    }
                }
                if (create == null)
                {
                    Log("通知失败：找不到 CreateToastNotifier(string)");
                    return;
                }
                object notifier = create.Invoke(null, new object[] { Aumid });

                // 5) Show(toast)
                MethodInfo show = null;
                foreach (MethodInfo m in notifier.GetType().GetMethods())
                {
                    if (m.Name == "Show" && m.GetParameters().Length == 1)
                    {
                        show = m;
                        break;
                    }
                }
                if (show == null)
                {
                    Log("通知失败：找不到 Show 方法");
                    return;
                }
                show.Invoke(notifier, new object[] { toast });

                Log("通知已发出: [" + ToastTitle + "] " + body);
            }
            catch (Exception ex)
            {
                Exception e = ex;
                while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
                Log("通知失败: " + e.GetType().Name + " HR=0x" + e.HResult.ToString("X8") + " " + e.Message);
            }
        }

        private static string EscapeXml(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // ---------------- 诊断辅助 ----------------

        private static string IntegrityLevel()
        {
            try
            {
                IntPtr token;
                if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), 0x0008, out token))
                    return "查询失败";

                IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(512);
                try
                {
                    uint ret;
                    if (!NativeMethods.GetTokenInformationRaw(token, 25, buf, 512, out ret))
                        return "查询失败";

                    IntPtr sid = System.Runtime.InteropServices.Marshal.ReadIntPtr(buf);
                    int count = System.Runtime.InteropServices.Marshal.ReadByte(sid, 1);
                    int rid = System.Runtime.InteropServices.Marshal.ReadInt32(sid, 8 + (count - 1) * 4);

                    if (rid == 0x0000) return "Untrusted";
                    if (rid == 0x1000) return "Low（受限环境，通知和注册表可能不可用）";
                    if (rid == 0x2000) return "Medium（正常）";
                    if (rid == 0x3000) return "High";
                    return "0x" + rid.ToString("X");
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);
                }
            }
            catch
            {
                return "查询失败";
            }
        }

        private static string LogFilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HotspotLauncher");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "launcher.log");
            }
        }

        private static void Log(string line)
        {
            LogBuffer.AppendLine(line);
        }

        /// <summary>追加写入日志。失败就算了，绝不影响主流程。</summary>
        private static void Flush()
        {
            try
            {
                File.AppendAllText(LogFilePath, LogBuffer.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
            internal static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

            [System.Runtime.InteropServices.DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
            internal static extern bool GetTokenInformationRaw(IntPtr token, int infoClass, IntPtr info, uint length, out uint returnLength);

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            internal static extern IntPtr GetCurrentProcess();
        }
    }
}
