using Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Remuxer.Tests
{
    public class RemuxerCliTests
    {
        const int RemuxerTimeoutMs = 120_000;

        static readonly Regex ProgressRegex = new Regex(@"^Progress:\s*(\d+)%", RegexOptions.Compiled);
        static readonly Regex TrackAudioRegex = new Regex(@"^TrackAudio:\s*(\d+)\|(.+)$", RegexOptions.Compiled);
        static readonly Regex TrackVoiceAudioRegex = new Regex(@"^TrackVoiceAudio:\s*(\d+)\|(\d+)\|(.+)$", RegexOptions.Compiled);

        static string FindRemuxerExe()
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
            if (!p.WaitForExit(RemuxerTimeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                try { p.WaitForExit(5_000); } catch { }
                throw new TimeoutException(
                    $"Remuxer timed out after {RemuxerTimeoutMs / 1000}s. Args: {string.Join(' ', args)}");
            }

            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();
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

                // Each minimal.* fixture includes at least one note for remux note extraction.
                var song = new Song();
                song.OpenMidiFile(midi);
                int noteCount = song.Tracks.Sum(t => t.Notes.Count);
                Assert.True(noteCount > 0, "expected at least one note in remuxed MIDI");
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
            // Use AHX: its channels produce TrackAudio stdout lines under -t.
            string input = TestFiles.PathTo("minimal.ahx");
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
                Assert.NotEmpty(trackLines);
                foreach (var line in trackLines)
                {
                    var audio = TrackAudioRegex.Match(line);
                    var voice = TrackVoiceAudioRegex.Match(line);
                    Assert.True(audio.Success || voice.Success, line);
                    string path = audio.Success ? audio.Groups[2].Value : voice.Groups[3].Value;
                    Assert.True(File.Exists(path), "track wav missing: " + path);
                }
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { }
            }
        }

        [Theory]
        [MemberData(nameof(ConversionFixtures))]
        [Trait("Category", "Integration")]
        public void Converts_fixture_under_non_ascii_paths(string fixtureName, string[] extraArgs)
        {
            // Copy input + write outputs under a non-ASCII directory so ANSI marshalling /
            // narrow CRT opens fail. Exercises Remuxer.exe → libRemuxer UTF-8 path handling
            // for every format reader (Mod / HVL / SID).
            using var dir = TestFiles.TempPath.NonAsciiDirectory("vm_remuxer_utf8_");
            string input = Path.Combine(dir.Path, fixtureName);
            File.Copy(TestFiles.PathTo(fixtureName), input);
            string midi = Path.Combine(dir.Path, "out.mid");
            string wav = Path.Combine(dir.Path, "out.wav");

            var args = new List<string> { input, "-m" + midi, "-a" + wav };
            args.AddRange(extraArgs);

            var (code, stdout, stderr) = RunRemuxer(args.ToArray());
            Assert.True(code == 0, $"exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(midi), "midi missing under non-ASCII path");
            Assert.True(File.Exists(wav), "wav missing under non-ASCII path");
            Assert.True(new FileInfo(midi).Length > 0);
            Assert.True(new FileInfo(wav).Length > 0);

            var song = new Song();
            song.OpenMidiFile(midi);
            int noteCount = song.Tracks.Sum(t => t.Notes.Count);
            Assert.True(noteCount > 0, "expected at least one note in remuxed MIDI");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Track_audio_under_non_ascii_paths()
        {
            using var dir = TestFiles.TempPath.NonAsciiDirectory("vm_remuxer_t_utf8_");
            string input = Path.Combine(dir.Path, "minimal.ahx");
            File.Copy(TestFiles.PathTo("minimal.ahx"), input);
            string midi = Path.Combine(dir.Path, "out.mid");
            string wav = Path.Combine(dir.Path, "out.wav");
            string trackBase = Path.Combine(dir.Path, "track");

            var (code, stdout, stderr) = RunRemuxer(input, "-m" + midi, "-a" + wav, "-t" + trackBase);
            Assert.True(code == 0, $"exit {code}\n{stdout}\n{stderr}");

            var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var trackLines = lines.Where(l =>
                TrackAudioRegex.IsMatch(l) || TrackVoiceAudioRegex.IsMatch(l)).ToList();
            Assert.NotEmpty(trackLines);
            foreach (var line in trackLines)
            {
                var audio = TrackAudioRegex.Match(line);
                var voice = TrackVoiceAudioRegex.Match(line);
                Assert.True(audio.Success || voice.Success, line);
                string path = audio.Success ? audio.Groups[2].Value : voice.Groups[3].Value;
                Assert.Contains("日本語", path, StringComparison.Ordinal);
                Assert.True(File.Exists(path), "track wav missing: " + path);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Cancel_signal_exits_with_code_2()
        {
            // Pre-create the cancel file so IsCancelRequested trips on the first process loop.
            string input = TestFiles.PathTo("minimal.ahx");
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_cancel_");
            string midi = Path.Combine(dir.Path, "out.mid");
            string wav = Path.Combine(dir.Path, "out.wav");
            string cancel = Path.Combine(dir.Path, "cancel.signal");
            File.WriteAllText(cancel, "cancel");

            var (code, stdout, stderr) = RunRemuxer(input, "-m" + midi, "-a" + wav, "-c" + cancel);
            Assert.True(code == 2, $"expected exit 2, got {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Per_instrument_mode_emits_TrackVoiceAudio_lines()
        {
            string input = TestFiles.PathTo("minimal.ahx");
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_voice_");
            string midi = Path.Combine(dir.Path, "out.mid");
            string wav = Path.Combine(dir.Path, "out.wav");
            string trackBase = Path.Combine(dir.Path, "track");

            var (code, stdout, stderr) = RunRemuxer(input, "-i", "-m" + midi, "-a" + wav, "-t" + trackBase);
            Assert.True(code == 0, $"exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");

            var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var voiceLines = lines.Where(l => TrackVoiceAudioRegex.IsMatch(l)).ToList();
            var channelLines = lines.Where(l => TrackAudioRegex.IsMatch(l)).ToList();
            Assert.NotEmpty(voiceLines);
            Assert.Empty(channelLines);
            foreach (var line in voiceLines)
            {
                var voice = TrackVoiceAudioRegex.Match(line);
                Assert.True(voice.Success, line);
                Assert.True(File.Exists(voice.Groups[3].Value), "track wav missing: " + voice.Groups[3].Value);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Unparseable_input_exits_1_with_stderr_unless_suppressErrors()
        {
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_bad_");
            string garbage = Path.Combine(dir.Path, "not-music.bin");
            File.WriteAllBytes(garbage, new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF });
            string midi = Path.Combine(dir.Path, "out.mid");
            string wav = Path.Combine(dir.Path, "out.wav");

            var (code, _, stderr) = RunRemuxer(garbage, "-m" + midi, "-a" + wav);
            Assert.Equal(1, code);
            Assert.Contains("Couldn't parse", stderr, StringComparison.Ordinal);

            var (codeSuppressed, _, stderrSuppressed) = RunRemuxer(garbage, "-e", "-m" + midi, "-a" + wav);
            Assert.Equal(1, codeSuppressed);
            Assert.DoesNotContain("Couldn't parse", stderrSuppressed, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Invalid_midi_output_path_exits_nonzero()
        {
            string input = TestFiles.PathTo("minimal.ahx");
            // Parent directory does not exist → CheckPath / File.Create fails.
            string badMidi = Path.Combine(Path.GetTempPath(), "vm_remuxer_no_such_dir_" + Guid.NewGuid().ToString("N"), "out.mid");
            var (code, _, stderr) = RunRemuxer(input, "-m" + badMidi);
            Assert.NotEqual(0, code);
            Assert.Contains("Invalid -m path", stderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Empty_argv_prints_usage_and_exits_0()
        {
            var (code, stdout, stderr) = RunRemuxer();
            Assert.Equal(0, code);
            Assert.Contains("Syntax: remuxer", stdout, StringComparison.Ordinal);
            Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("Error:", StringComparison.Ordinal));
        }
    }
}
