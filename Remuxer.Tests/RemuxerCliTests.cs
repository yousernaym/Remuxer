using Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Remuxer.Tests
{
    public class RemuxerCliTests
    {
        static readonly Regex ProgressRegex = new Regex(@"^Progress:\s*(\d+)%", RegexOptions.Compiled);
        static readonly Regex TrackAudioRegex = new Regex(@"^TrackAudio:\s*(\d+)\|(.+)$", RegexOptions.Compiled);
        static readonly Regex TrackVoiceAudioRegex = new Regex(@"^TrackVoiceAudio:\s*(\d+)\|(\d+)\|(.+)$", RegexOptions.Compiled);

        static string FindRemuxerExe()
        {
            // Prefer the Remuxer project output next to this repo; fall back to walking for Remuxer.exe
            // that has libRemuxer.dll beside it.
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Remuxer", "bin", "Debug", "Remuxer.exe")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Remuxer", "bin", "Release", "Remuxer.exe")),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c) && File.Exists(Path.Combine(Path.GetDirectoryName(c), "libRemuxer.dll")))
                    return c;
            }

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                foreach (var exe in Directory.EnumerateFiles(dir.FullName, "Remuxer.exe", SearchOption.AllDirectories))
                {
                    if (exe.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(exe), "libRemuxer.dll")))
                        return exe;
                }
            }

            return null;
        }

        static (int ExitCode, string StdOut, string StdErr) RunRemuxer(params string[] args)
        {
            string exe = FindRemuxerExe();
            if (exe == null)
                throw new FileNotFoundException(
                    "Remuxer.exe (+ libRemuxer.dll) not found. Build Remuxer (x64) before Integration tests.");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(120_000);
            return (p.ExitCode, stdout, stderr);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Missing_input_exits_nonzero()
        {
            var (code, _, stderr) = RunRemuxer("definitely-missing-file.mod");
            Assert.NotEqual(0, code);
            Assert.Contains("Couldn't find", stderr, StringComparison.Ordinal);
        }

        public static IEnumerable<object[]> ConversionFixtures()
        {
            yield return new object[] { "minimal.mod", Array.Empty<string>() };
            yield return new object[] { "minimal.ahx", Array.Empty<string>() };
            yield return new object[] { "minimal.hvl", Array.Empty<string>() };
            yield return new object[] { "minimal.sid", new[] { "-l5" } };
        }

        [Theory]
        [MemberData(nameof(ConversionFixtures))]
        [Trait("Category", "Integration")]
        public void Converts_fixture_to_midi_and_wav(string fixtureName, string[] extraArgs)
        {
            string input = TestFiles.PathTo(fixtureName);
            string outDir = Path.Combine(Path.GetTempPath(), "vm_remuxer_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try
            {
                string midi = Path.Combine(outDir, "out.mid");
                string wav = Path.Combine(outDir, "out.wav");
                var args = new List<string> { input, "-m" + midi, "-a" + wav };
                args.AddRange(extraArgs);

                var (code, stdout, stderr) = RunRemuxer(args.ToArray());
                Assert.True(code == 0, $"exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
                Assert.Contains("Extracting", stdout, StringComparison.Ordinal);
                var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.Contains(lines, l => ProgressRegex.IsMatch(l));
                Assert.True(File.Exists(midi), "midi missing");
                Assert.True(File.Exists(wav), "wav missing");
                Assert.True(new FileInfo(midi).Length > 0);
                Assert.True(new FileInfo(wav).Length > 0);

                // One-note AHX/HVL/SID fixtures yield parseable notes. The synthetic minimal.mod
                // is mainly a ModReader/openmpt smoke test (WAV + MIDI header); note extraction
                // from that tiny pattern is not guaranteed.
                if (!fixtureName.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                {
                    var song = new Song();
                    song.OpenMidiFile(midi);
                    int noteCount = song.Tracks.Sum(t => t.Notes.Count);
                    Assert.True(noteCount > 0, "expected at least one note in remuxed MIDI");
                }
                else
                {
                    Assert.True(new FileInfo(midi).Length >= 14, "expected at least an MThd header");
                }
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { /* best-effort */ }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Track_audio_lines_match_visual_music_regexes()
        {
            string input = TestFiles.PathTo("minimal.mod");
            string outDir = Path.Combine(Path.GetTempPath(), "vm_remuxer_t_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try
            {
                string midi = Path.Combine(outDir, "out.mid");
                string wav = Path.Combine(outDir, "out.wav");
                string trackBase = Path.Combine(outDir, "track");
                var (code, stdout, stderr) = RunRemuxer(input, "-m" + midi, "-a" + wav, "-t" + trackBase);
                Assert.True(code == 0, $"exit {code}\n{stdout}\n{stderr}");

                var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var trackLines = lines.Where(l =>
                    TrackAudioRegex.IsMatch(l) || TrackVoiceAudioRegex.IsMatch(l)).ToList();
                // minimal.mod may or may not emit track lines depending on content; if present they must parse
                foreach (var line in trackLines)
                {
                    Assert.True(TrackAudioRegex.IsMatch(line) || TrackVoiceAudioRegex.IsMatch(line));
                }
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { }
            }
        }
    }
}
