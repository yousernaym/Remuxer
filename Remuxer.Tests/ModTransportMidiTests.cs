using Midi;
using System.Collections.Generic;
using System.IO;
using Xunit;
using static Remuxer.Tests.ModMidi;

namespace Remuxer.Tests
{
    /// <summary>
    /// MIDI conversion coverage for libRemuxer/test-files/mod-transport/, which isolates one
    /// transport (flow-control) effect per fixture: position jump, pattern break, pattern delay,
    /// pattern loop, and set speed/tempo. These decide which rows play and for how long, so the
    /// assertions here are note start ticks, note counts and tempo-event times.
    ///
    /// Every fixture shares a template: speed 6, tempo 125, effects in ch0, one note (MIDI pitch 60)
    /// in ch1, instrument 1 = a looping sample so a note sustains until retriggered or cut. Pattern
    /// and order layout is asserted in libRemuxer GoogleTest (ModTransportFixtureTests) via libopenmpt.
    ///
    /// Durations of the final note in each fixture are deliberately not asserted: a sustaining note
    /// runs to the end of the song, which measures total song length rather than transport behaviour.
    /// </summary>
    public class ModTransportMidiTests
    {
        const int NotePitch = 60; // OpenMPT note 61 (= XM file note 49 + 12)

        static IEnumerable<object[]> Fixtures(string xm, string s3mIt)
        {
            yield return new object[] { "mod-transport/" + xm + ".XM" };
            yield return new object[] { "mod-transport/" + s3mIt + ".S3M" };
            yield return new object[] { "mod-transport/" + s3mIt + ".IT" };
        }

        public static IEnumerable<object[]> PatternJumpFixtures() =>
            Fixtures("pattern-jump-BXX", "pattern-jump-BXX");

        public static IEnumerable<object[]> PatternBreakFixtures() =>
            Fixtures("pattern-break-DXX", "pattern-break-CXX");

        public static IEnumerable<object[]> RowRepeatFixtures() =>
            Fixtures("row-repeat-EEX", "row-repeat-SEX");

        public static IEnumerable<object[]> PatternLoopFixtures() =>
            Fixtures("loop-E6X", "loop-SBX");

        public static IEnumerable<object[]> SpeedTempoFixtures() =>
            Fixtures("speed-tempo-FXX", "speed-tempo-AXX-TXX");

        /// <summary>Converts a fixture to MIDI and parses it back. Caller owns nothing; temp dir is cleaned up.</summary>
        static Song Convert(string fixtureName)
        {
            string input = TestFiles.PathTo(fixtureName);
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_transport_");
            string midi = Path.Combine(dir.Path, "out.mid");

            var (code, stdout, stderr) = RemuxerProcess.Run(input, "-m" + midi);
            Assert.True(code == 0, $"{fixtureName}: exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(midi), "midi missing");

            var song = new Song();
            song.OpenMidiFile(midi);
            Assert.Equal(MidiTicksPerBeat, song.TicksPerBeat);
            Assert.Empty(ChannelNotes(song, 0)); // ch0 only ever carries effects
            return song;
        }

        // B01 on order 0 row 0 skips to order 1 after row 0, so the row-0 note of order 1 starts one row from module start.
        [Theory]
        [MemberData(nameof(PatternJumpFixtures))]
        [Trait("Category", "Integration")]
        public void Position_jump_moves_the_note_to_the_next_order(string fixtureName)
        {
            var notes = ChannelNotes(Convert(fixtureName), 1);
            var note = Assert.Single(notes);
            Assert.Equal(Speed, ModStart(note)); // order 1 row 0 @ speed 6
            Assert.Equal(NotePitch, note.pitch);
        }

        // D01/C01 on order 0 breaks to row 1 of order 1, which plays a note (module tick 6) and
        // breaks again with B03 in the note channel, landing on row 1 of order 3 at module tick 12.
        // The order-1 note is cut by the order-3 note, so its duration is exactly one row.
        [Theory]
        [MemberData(nameof(PatternBreakFixtures))]
        [Trait("Category", "Integration")]
        public void Pattern_break_row_combines_with_position_jump_order(string fixtureName)
        {
            var notes = ChannelNotes(Convert(fixtureName), 1);
            Assert.Equal(2, notes.Count);
            Assert.Equal(Speed, ModStart(notes[0]));
            Assert.Equal(Speed, ModDuration(notes[0]));
            Assert.Equal(2 * Speed, ModStart(notes[1]));
            Assert.Equal(NotePitch, notes[1].pitch);
        }

        // EE2/SE2 plays row 0 three times (18 ticks). ModReader expands the delayed row into a single
        // 18-tick block, so the volume slide of 4 per tick is skipped only on tick 0 and then applied
        // on every tick from 1 — the volume reaches 0 on the 16th application, at module tick 16.
        [Theory]
        [MemberData(nameof(RowRepeatFixtures))]
        [Trait("Category", "Integration")]
        public void Pattern_delay_repeats_the_row_and_keeps_sliding_volume(string fixtureName)
        {
            var notes = ChannelNotes(Convert(fixtureName), 1);
            var note = Assert.Single(notes);
            Assert.Equal(0, ModStart(note));
            Assert.Equal(16, ModDuration(note));
        }

        // E60/SB0 on row 1 and E62/SB2 on row 2 replay rows 1-2 three times in total, retriggering
        // the row-1 note once per pass — 12 ticks apart. The last note sustains to the end of the song.
        [Theory]
        [MemberData(nameof(PatternLoopFixtures))]
        [Trait("Category", "Integration")]
        public void Pattern_loop_retriggers_the_note_once_per_pass(string fixtureName)
        {
            var notes = ChannelNotes(Convert(fixtureName), 1);
            Assert.Equal(3, notes.Count);

            const int LoopTicks = 2 * Speed; // loop body is rows 1-2
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Speed + i * LoopTicks, ModStart(notes[i]));
                Assert.Equal(NotePitch, notes[i].pitch);
            }
            Assert.Equal(LoopTicks, ModDuration(notes[0]));
            Assert.Equal(LoopTicks, ModDuration(notes[1]));
        }

        // F03/A03 shortens row 0 to 3 ticks, so row 1 — carrying both the note and F20/T20 — starts
        // on module tick 3, where the tempo drops from 125 to 32.
        [Theory]
        [MemberData(nameof(SpeedTempoFixtures))]
        [Trait("Category", "Integration")]
        public void Speed_change_moves_the_note_and_the_tempo_event(string fixtureName)
        {
            var song = Convert(fixtureName);

            var note = Assert.Single(ChannelNotes(song, 1));
            Assert.Equal(3, ModStart(note));
            Assert.Equal(NotePitch, note.pitch);

            Assert.Equal(2, song.TempoEvents.Count);
            Assert.Equal(0, ModTime(song.TempoEvents[0]));
            Assert.Equal(125.0, song.TempoEvents[0].Tempo, 3);
            Assert.Equal(3, ModTime(song.TempoEvents[1]));
            Assert.Equal(32.0, song.TempoEvents[1].Tempo, 3);
        }
    }
}
