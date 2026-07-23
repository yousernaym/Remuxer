using System;
using System.IO;

namespace Remuxer.Tests
{
    static class TestFiles
    {
        /// <summary>
        /// Resolves a fixture under the test assembly's copied <c>test-files/</c> tree
        /// (see Content items in the test csproj). Format fixtures live in
        /// <c>libRemuxer/test-files/</c> and are copied here at build time.
        /// </summary>
        public static string PathTo(string relative)
        {
            string candidate = Path.Combine(AppContext.BaseDirectory, "test-files", relative);
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    "Could not locate test fixture '" + relative + "' under " +
                    Path.Combine(AppContext.BaseDirectory, "test-files") + ".");
            }
            return candidate;
        }

        /// <summary>
        /// Unique path under the system temp dir; deletes the file or directory on Dispose.
        /// </summary>
        public sealed class TempPath : IDisposable
        {
            public string Path { get; }
            readonly bool _isDirectory;

            TempPath(string path, bool isDirectory)
            {
                Path = path;
                _isDirectory = isDirectory;
            }

            /// <summary>Unique directory; created empty.</summary>
            public static TempPath Directory(string prefix)
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    prefix + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(path);
                return new TempPath(path, isDirectory: true);
            }

            /// <summary>
            /// Unique directory whose name includes non-ASCII characters. Used by Integration
            /// tests so a regression to ANSI P/Invoke marshalling / narrow CRT opens fails
            /// loudly — ASCII-only temp paths would still pass under the system code page.
            /// </summary>
            public static TempPath NonAsciiDirectory(string asciiPrefix = "vm_utf8_")
            {
                // café is Latin-1; 日本語 is outside Windows-1252. Together they corrupt under ANSI.
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    asciiPrefix + "café_日本語_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(path);
                return new TempPath(path, isDirectory: true);
            }

            public void Dispose()
            {
                try
                {
                    if (_isDirectory)
                    {
                        if (System.IO.Directory.Exists(Path))
                            System.IO.Directory.Delete(Path, recursive: true);
                    }
                    else if (System.IO.File.Exists(Path))
                        System.IO.File.Delete(Path);
                }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
