using Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Remuxer.Tests
{
    /// <summary>
    /// Effect-lane coverage for libRemuxer/test-files/FX.XM.
    /// Durations are in module ticks; XM resolution is 24 tpb, MIDI conversion uses 480 tpb
    /// → 1 module tick = 20 MIDI ticks.
    /// </summary>
    public class FxXmTests
    {
        const int MidiTicksPerBeat = 480;
        const int MidiPerModTick = MidiTicksPerBeat / 24; // XM 24 tpb → 20 MIDI ticks per module tick
        const int Speed = 6;
        // Ch1: sample-end duration depends on pitch. OpenMPT's XM loader adds +12 to file notes
        // (Load_xm.cpp ReadXMPatterns: `m.note += 12`), then ModReader emits MIDI = openmptNote - 1
        // → file note 50 → OpenMPT 62 → MIDI 61.
        const int Ch1XmNote = 50;
        const int Ch1MidiPitch = 61;

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

        static XmFixture.Cell C(XmFixture.Module mod, int row, int ch) => XmFixture.CellAt(mod, row, ch);

        [Fact]
        public void Fx_xm_fixture_matches_described_pattern_and_speed()
        {
            var mod = XmFixture.Load(TestFiles.PathTo("FX.XM"));

            Assert.Equal(6, mod.Speed);
            Assert.Equal(18, mod.Channels);
            Assert.Equal(1, mod.NumPatterns); // FX.XM is single-pattern; XmFixture only asserts on pattern 0
            Assert.Equal(2, mod.NumInstruments);
            // Linear freq table bit unused by our asserts; speed/tempo are what matter.
            Assert.Equal(125, mod.Tempo);

            // Ins1 looped, ins2 short non-looped (sample-end case).
            Assert.Single(mod.Instruments[1]);
            Assert.Single(mod.Instruments[2]);
            Assert.True(mod.Instruments[1][0].Loops, "instrument 1 should loop");
            Assert.False(mod.Instruments[2][0].Loops, "instrument 2 should not loop");
            Assert.Equal(10u, mod.Instruments[2][0].Length);

            // Ch0: note + EC1 on row 0
            AssertNoteCell(C(mod, 0, 0), ins: 1, fx: 0xE, param: 0xC1);
            // Ch1: note with instrument 2 (short non-looped sample; pitch fixed for sample-end duration)
            AssertNoteCell(C(mod, 0, 1), ins: 2, fx: 0, param: 0);
            Assert.Equal(Ch1XmNote, C(mod, 0, 1).Note);
            // Ch2: note; volume 0 on next row (XM vol column 0x10 = set vol 0)
            AssertNoteCell(C(mod, 0, 2), ins: 1, fx: 0, param: 0);
            Assert.Equal(0x10, C(mod, 1, 2).Vol);
            // Ch3: note + ED1; note-off next row
            AssertNoteCell(C(mod, 0, 3), ins: 1, fx: 0xE, param: 0xD1);
            Assert.Equal(XmFixture.NoteKeyOff, C(mod, 1, 3).Note);
            // Ch4: note; note-off next row
            AssertNoteCell(C(mod, 0, 4), ins: 1, fx: 0, param: 0);
            Assert.Equal(XmFixture.NoteKeyOff, C(mod, 1, 4).Note);
            // Ch5: note; note-off row 1; non-zero instrument row 2
            AssertNoteCell(C(mod, 0, 5), ins: 1, fx: 0, param: 0);
            Assert.Equal(XmFixture.NoteKeyOff, C(mod, 1, 5).Note);
            Assert.True(C(mod, 2, 5).Ins != 0);
            // Ch6: A02 then A00 × 6; no effect on the following row
            AssertNoteCell(C(mod, 0, 6), ins: 1, fx: 0xA, param: 0x02);
            for (int r = 1; r <= 6; r++)
                Assert.True(C(mod, r, 6).Fx == 0xA && C(mod, r, 6).Param == 0x00, $"ch6 row {r}");
            AssertNoEffect(C(mod, 7, 6), "ch6 row 7");
            // Ch7: vol-column slide down 2 (0x62) on note row and following 6 rows; none after
            AssertNoteCell(C(mod, 0, 7), ins: 1, fx: 0, param: 0);
            Assert.Equal(0x62, C(mod, 0, 7).Vol);
            for (int r = 1; r <= 6; r++)
                Assert.Equal(0x62, C(mod, r, 7).Vol);
            AssertNoEffect(C(mod, 7, 7), "ch7 row 7");
            // Ch8: note; 502 next row then 500 × 6; no effect after
            AssertNoteCell(C(mod, 0, 8), ins: 1, fx: 0, param: 0);
            Assert.True(C(mod, 1, 8).Fx == 0x5 && C(mod, 1, 8).Param == 0x02);
            for (int r = 2; r <= 7; r++)
                Assert.True(C(mod, r, 8).Fx == 0x5 && C(mod, r, 8).Param == 0x00, $"ch8 row {r}");
            AssertNoEffect(C(mod, 8, 8), "ch8 row 8");
            // Ch9: note; 602 next row then 600 × 6; no effect after
            AssertNoteCell(C(mod, 0, 9), ins: 1, fx: 0, param: 0);
            Assert.True(C(mod, 1, 9).Fx == 0x6 && C(mod, 1, 9).Param == 0x02);
            for (int r = 2; r <= 7; r++)
                Assert.True(C(mod, r, 9).Fx == 0x6 && C(mod, r, 9).Param == 0x00, $"ch9 row {r}");
            AssertNoEffect(C(mod, 8, 9), "ch9 row 8");
            // Ch10: EB2 then EB0 on following 31 rows; no effect after
            AssertNoteCell(C(mod, 0, 10), ins: 1, fx: 0xE, param: 0xB2);
            for (int r = 1; r <= 31; r++)
                Assert.True(C(mod, r, 10).Fx == 0xE && C(mod, r, 10).Param == 0xB0, $"ch10 row {r}");
            AssertNoEffect(C(mod, 32, 10), "ch10 row 32");
            // Ch11: arpeggio 025; note-off next row
            AssertNoteCell(C(mod, 0, 11), ins: 1, fx: 0x0, param: 0x25);
            Assert.Equal(XmFixture.NoteKeyOff, C(mod, 1, 11).Note);
            // Ch12: note on row 0 and row 1
            AssertNoteCell(C(mod, 0, 12), ins: 1, fx: 0, param: 0);
            AssertNoteCell(C(mod, 1, 12), ins: 1, fx: 0, param: 0);
            // Ch13: E92; note-off next row
            AssertNoteCell(C(mod, 0, 13), ins: 1, fx: 0xE, param: 0x92);
            Assert.Equal(XmFixture.NoteKeyOff, C(mod, 1, 13).Note);
            // Ch14: porta retrigger — note rows 0–1; row 1 effect 300; one pitch step up
            AssertNoteCell(C(mod, 0, 14), ins: 1, fx: 0, param: 0);
            AssertNoteCell(C(mod, 1, 14), ins: 1, fx: 0x3, param: 0x00);
            Assert.Equal(C(mod, 0, 14).Note + 1, C(mod, 1, 14).Note);
            // Ch15: porta retrigger — same as ch14 with effect 500
            AssertNoteCell(C(mod, 0, 15), ins: 1, fx: 0, param: 0);
            AssertNoteCell(C(mod, 1, 15), ins: 1, fx: 0x5, param: 0x00);
            Assert.Equal(C(mod, 0, 15).Note + 1, C(mod, 1, 15).Note);
            // Ch16: note + vol-column +f on row 0; -f on row1; +1 on row 2.
            AssertNoteCell(C(mod, 0, 16), ins: 1, fx: 0, param: 0);
            Assert.Equal(0x7F, C(mod, 0, 16).Vol);
            Assert.Equal(0x6F, C(mod, 1, 16).Vol);
            Assert.Equal(0x71, C(mod, 2, 16).Vol);

            // Ch17: note at zero volume then volume +1
            AssertNoteCell(C(mod, 0, 17), ins: 1, fx: 0, param: 0);
            Assert.Equal(0x10, C(mod, 0, 17).Vol); // set vol 0
            Assert.Equal(0x71, C(mod, 1, 17).Vol); //+1
        }

        /// <summary>Assert a sounding note is present (pitch unconstrained) with the given ins/fx/param.</summary>
        static void AssertNoteCell(XmFixture.Cell cell, byte ins, byte fx, byte param)
        {
            Assert.True(cell.Note > 0 && cell.Note != XmFixture.NoteKeyOff, "expected a note");
            Assert.Equal(ins, cell.Ins);
            Assert.Equal(fx, cell.Fx);
            Assert.Equal(param, cell.Param);
        }

        /// <summary>Assert effect column and volume column are empty (end of a volume-slide run).</summary>
        static void AssertNoEffect(XmFixture.Cell cell, string label)
        {
            Assert.True(cell.Fx == 0 && cell.Param == 0 && cell.Vol == 0, label + ": expected no effect");
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Fx_xm_converts_effect_channels_to_expected_midi_notes()
        {
            string input = TestFiles.PathTo("FX.XM");
            using var dir = TestFiles.TempPath.Directory("vm_remuxer_fx_");
            string midi = Path.Combine(dir.Path, "out.mid");

            var (code, stdout, stderr) = RemuxerProcess.Run(input, "-m" + midi);
            Assert.True(code == 0, $"exit {code}\nstdout:\n{stdout}\nstderr:\n{stderr}");
            Assert.True(File.Exists(midi), "midi missing");

            var song = new Song();
            song.OpenMidiFile(midi);
            Assert.Equal(MidiTicksPerBeat, song.TicksPerBeat);

            // Ch0: Early note end (EC1) → duration 1
            {
                var notes = ChannelNotes(song, 0);
                Assert.Single(notes);
                Assert.Equal(0, ModStart(notes[0]));
                Assert.Equal(1, ModDuration(notes[0]));
            }
            // Ch1: Sample end = note end → duration 1
            {
                var notes = ChannelNotes(song, 1);
                Assert.Single(notes);
                Assert.Equal(Ch1MidiPitch, notes[0].pitch);
                Assert.Equal(1, ModDuration(notes[0]));
            }
            // Ch2: Volume 0 on next row → duration 6
            {
                var notes = ChannelNotes(song, 2);
                Assert.Single(notes);
                Assert.Equal(6, ModDuration(notes[0]));
            }
            // Ch3: Note delay ED1 → start tick 1, duration 5
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
            // Ch10: EB fine volume down → duration 186
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
            // Ch13: E92 retrigger → 3 notes dur 2 at starts 0, 2, 4
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
            // (row 2 @ speed 6; normal slides skip the first tick of the row)
            {
                var notes = ChannelNotes(song, 16);
                Assert.Equal(0, ModStart(notes[0]));
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
