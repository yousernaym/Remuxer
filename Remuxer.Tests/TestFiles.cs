using System;
using System.IO;

namespace Remuxer.Tests
{
    static class TestFiles
    {
        public static string FindRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "test-files");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "minimal.sid")))
                    return candidate;
            }
            throw new DirectoryNotFoundException(
                "Could not locate test-files/. Run tests from the Visual Music repo tree.");
        }

        public static string PathTo(string relative) => Path.Combine(FindRoot(), relative);
    }
}
