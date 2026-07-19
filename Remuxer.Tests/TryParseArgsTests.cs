using Xunit;

namespace Remuxer.Tests
{
    public class TryParseArgsTests
    {
        [Fact]
        public void Parses_input_and_flags()
        {
            Assert.True(Program.TryParseArgs(
                new[] { "song.mod", "-i", "-e", "-s2", "-l5.5", "-ccancel.tmp", "-tbase", "-mout.mid", "-aout.wav" },
                out var args, out string cancel, out bool midi, out bool audio, out string error));

            Assert.Null(error);
            Assert.Equal("song.mod", args.inputPath);
            Assert.True(args.modInsTrack);
            Assert.True(args.suppressErrors);
            Assert.Equal(2, args.subSong);
            Assert.Equal(5.5f, args.songLengthS);
            Assert.Equal("cancel.tmp", cancel);
            Assert.Equal("base", args.trackAudioPath);
            Assert.True(midi);
            Assert.True(audio);
            Assert.Equal("out.mid", args.midiPath);
            Assert.Equal("out.wav", args.audioPath);
        }

        [Fact]
        public void Flags_without_paths_set_midi_and_audio_flags()
        {
            Assert.True(Program.TryParseArgs(
                new[] { "in.mod", "-m", "-a" },
                out var args, out _, out bool midi, out bool audio, out _));
            Assert.True(midi);
            Assert.True(audio);
            Assert.Null(args.midiPath);
            Assert.Null(args.audioPath);
        }

        [Fact]
        public void Invalid_flag_fails()
        {
            Assert.False(Program.TryParseArgs(
                new[] { "in.mod", "-z" },
                out _, out _, out _, out _, out string error));
            Assert.Contains("-z", error);
        }

        [Fact]
        public void Invalid_subSong_fails()
        {
            Assert.False(Program.TryParseArgs(
                new[] { "in.sid", "-sx" },
                out _, out _, out _, out _, out string error));
            Assert.Contains("-s", error);
        }

        [Fact]
        public void Invalid_length_fails()
        {
            Assert.False(Program.TryParseArgs(
                new[] { "in.sid", "-lnope" },
                out _, out _, out _, out _, out string error));
            Assert.Contains("-l", error);
        }
    }
}
