using Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Remuxer.Tests
{
    /// <summary>
    /// Effect-lane MIDI conversion coverage for libRemuxer/test-files/FX.{XM,S3M,IT}.
    /// Pattern/speed layout is asserted in libRemuxer GoogleTest (FxFixtureTests) via libopenmpt.
    /// Durations are in module ticks; module resolution is 24 tpb, MIDI conversion uses 480 tpb
    /// → 1 module tick = 20 MIDI ticks.
    /// </summary>
    public class FxXmTests
    {
        const int MidiTicksPerBeat = 480;
        const int MidiPerModTick = MidiTicksPerBeat / 24; // 24 tpb → 20 MIDI ticks per module tick
        const int Speed = 6;
        // Ch1 XM: file note 50 → OpenMPT 62 → MIDI 61 (sample-end duration depends on this pitch).
        const int Ch1XmMidiPitch = 61;

        static int ModStart(Note n)
        {
            Assert.Equal(0, n.start % MidiPerModTick);
            return n.start / MidiPerModTick;
        }

        static int ModDuration(Note n)
        {
            Assert.Equal(0, (n.stop - n.start) % MidiPerModTick);
            return (n.stop - n.start) / MidiPerModTick;
        }

        static List<Note> ChannelNotes(Song song, int channel)
        {
            int track = channel + 1; // track 0 = tempo; channel c → track c+1
            Assert.True(track < song.Tracks.Count, $"missing MIDI track for channel {channel}");
            return song.Tracks[track].Notes.OrderBy(n => n.start).ThenBy(n => n.pitch).ToList();
        }

        public static IEnumerable<object[]> FxFixtures()
        {
            yield return new object[] { "FX.XM" };
            yield return new object[] { "FX.S3M" };
            yield return new object[] { "FX.IT" };
        }

        [Theory]
        [MemberData(nameof(FxFixtures))]
        [Trait("Category", "Integration")]
        public void Fx_converts_effect_channels_to_expected_midi_notes(string fixtureName)
        {
            string input = TestFiles.PathTo(fixtureName);
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_fx_");
            string midi = Path.Combine(dir.Path, "out.mid");

            var (code, stdout, stderr) = RemuxerProcess.Run(input, "-m" + midi);
            Assert.True(code == 0, $"{fixtureName}: exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(midi), "midi missing");

            var song = new Song();
            song.OpenMidiFile(midi);
            Assert.Equal(MidiTicksPerBeat, song.TicksPerBeat);

            // Ch0: Early note end (EC1 / SC1) → duration 1
            {
                var notes = ChannelNotes(song, 0);
                Assert.Single(notes);
                Assert.Equal(0, ModStart(notes[0]));
                Assert.Equal(1, ModDuration(notes[0]));
            }
            // Ch1: Sample end = note end → duration 1 (XM pitch fixed for sample-length math)
            {
                var notes = ChannelNotes(song, 1);
                Assert.Single(notes);
                if (fixtureName.Equals("FX.XM", StringComparison.OrdinalIgnoreCase))
                    Assert.Equal(Ch1XmMidiPitch, notes[0].pitch);
                Assert.Equal(1, ModDuration(notes[0]));
            }
            // Ch2: Volume 0 on next row → duration 6
            {
                var notes = ChannelNotes(song, 2);
                Assert.Single(notes);
                Assert.Equal(6, ModDuration(notes[0]));
            }
            // Ch3: Note delay ED1 / SD1 → start tick 1, duration 5
            {
                var notes = ChannelNotes(song, 3);
                Assert.Single(notes);
                Assert.Equal(1, ModStart(notes[0]));
                Assert.Equal(5, ModDuration(notes[0]));
            }
            // Ch4: Note-off on row 1 → duration 6
            {
                var notes = ChannelNotes(song, 4);
                Assert.Single(notes);
                Assert.Equal(6, ModDuration(notes[0]));
            }
            // Ch5: note dur 6, silence 6, then same pitch
            {
                var notes = ChannelNotes(song, 5);
                Assert.Equal(2, notes.Count);
                Assert.Equal(6, ModDuration(notes[0]));
                int silence = ModStart(notes[1]) - (ModStart(notes[0]) + ModDuration(notes[0]));
                Assert.Equal(6, silence);
                Assert.Equal(notes[0].pitch, notes[1].pitch);
            }
            // Ch6 / Ch7: volume down → duration 38
            {
                Assert.Equal(38, ModDuration(Assert.Single(ChannelNotes(song, 6))));
                Assert.Equal(38, ModDuration(Assert.Single(ChannelNotes(song, 7))));
            }
            // Ch8 / Ch9: volume down from next row → duration 44
            {
                Assert.Equal(44, ModDuration(Assert.Single(ChannelNotes(song, 8))));
                Assert.Equal(44, ModDuration(Assert.Single(ChannelNotes(song, 9))));
            }
            // Ch10: fine volume down → duration 186
            {
                Assert.Equal(186, ModDuration(Assert.Single(ChannelNotes(song, 10))));
            }
            // Ch11: arpeggio → 6 notes dur 1; pitches x, x+2, x+5, …
            {
                var notes = ChannelNotes(song, 11);
                Assert.Equal(6, notes.Count);
                int x = notes[0].pitch;
                int[] expected = { x, x + 2, x + 5, x, x + 2, x + 5 };
                for (int i = 0; i < 6; i++)
                {
                    Assert.Equal(1, ModDuration(notes[i]));
                    Assert.Equal(i, ModStart(notes[i]));
                    Assert.Equal(expected[i], notes[i].pitch);
                }
            }
            // Ch12: new note ends previous → first dur 6, second at module tick 6
            {
                var notes = ChannelNotes(song, 12);
                Assert.Equal(2, notes.Count);
                Assert.Equal(6, ModDuration(notes[0]));
                Assert.Equal(Speed, ModStart(notes[1])); // row 1 @ speed 6
            }
            // Ch13: E92 / Q02 retrigger → 3 notes dur 2 at starts 0, 2, 4
            {
                var notes = ChannelNotes(song, 13);
                Assert.Equal(3, notes.Count);
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(2, ModDuration(notes[i]));
                    Assert.Equal(i * 2, ModStart(notes[i]));
                }
            }
            // Ch14 / Ch15: porta retrigger → notes at module ticks 0 and 6; second pitch +1
            foreach (int ch in new[] { 14, 15 })
            {
                var notes = ChannelNotes(song, ch);
                Assert.Equal(2, notes.Count);
                Assert.Equal(0, ModStart(notes[0]));
                Assert.Equal(Speed, ModStart(notes[1]));
                Assert.Equal(notes[0].pitch + 1, notes[1].pitch);
            }
            // Ch16: Clamp volume at <=64, then >=0, then volume + 1 → revival on tick 13
            // (row 2 @ speed 6; normal slides skip the first tick of the row).
            // S3M/IT use effect D F0 / D 0F / D 10 (same ±F then +1 as XM vol column).
            {
                var notes = ChannelNotes(song, 16);
                Assert.Equal(2, notes.Count);
                Assert.Equal(0, ModStart(notes[0]));
                Assert.Equal(11, ModDuration(notes[0]));
                Assert.Equal(13, ModStart(notes[1]));
            }
            // Ch17: zero-volume note suppressed; revival on first slide tick (row 1, tick 7)
            {
                var notes = ChannelNotes(song, 17);
                Assert.Single(notes);
                Assert.Equal(7, ModStart(notes[0]));
            }
        }
    }
}
