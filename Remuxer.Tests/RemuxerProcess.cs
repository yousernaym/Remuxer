using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Remuxer.Tests
{
    /// <summary>
    /// Locates and runs Remuxer.exe for Integration tests.
    /// </summary>
    static class RemuxerProcess
    {
        public const int DefaultTimeoutMs = 120_000;

        public static string FindExe()
        {
            // Prefer Remuxer/bin/{Debug,Release}/ next to this checkout. Walk ancestors checking only
            // that fixed relative path (File.Exists is cheap and safe). Never EnumerateFiles with
            // AllDirectories: that eventually hits the drive root, throws UnauthorizedAccessException
            // on protected folders, and can latch onto an unrelated Remuxer.exe elsewhere on the disk.
            static bool IsUsable(string exe) =>
                File.Exists(exe)
                && File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "libRemuxer.dll"));

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                foreach (var config in new[] { "Debug", "Release" })
                {
                    string candidate = Path.Combine(dir.FullName, "Remuxer", "bin", config, "Remuxer.exe");
                    if (IsUsable(candidate))
                        return candidate;
                }
            }

            return null;
        }

        public static (int ExitCode, string StdOut, string StdErr) Run(params string[] args) =>
            Run(DefaultTimeoutMs, args);

        public static (int ExitCode, string StdOut, string StdErr) Run(int timeoutMs, params string[] args)
        {
            string exe = FindExe();
            if (exe == null)
                throw new FileNotFoundException(
                    "Remuxer.exe (+ libRemuxer.dll) not found. Build Remuxer (x64) before Integration tests.");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Remuxer.exe.");

            // Read async so WaitForExit(timeout) can fire if Remuxer hangs with pipes open.
            Task<string> stdoutTask = p.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                try { p.WaitForExit(5_000); } catch { }
                throw new TimeoutException(
                    $"Remuxer timed out after {timeoutMs / 1000}s. Args: {string.Join(' ', args)}");
            }

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();
            return (p.ExitCode, stdout, stderr);
        }
    }
}
