using Midi;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Remuxer.Tests
{
    /// <summary>
    /// Helpers for reading Remuxer's MIDI output back in module terms.
    /// Module resolution is 24 tpb and MIDI conversion uses 480 tpb, so 1 module tick = 20 MIDI ticks.
    /// </summary>
    static class ModMidi
    {
        public const int MidiTicksPerBeat = 480;
        public const int MidiPerModTick = MidiTicksPerBeat / 24;
        public const int Speed = 6;

        /// <summary>Note start in module ticks.</summary>
        public static int ModStart(Note n)
        {
            Assert.Equal(0, n.start % MidiPerModTick);
            return n.start / MidiPerModTick;
        }

        /// <summary>Note duration in module ticks.</summary>
        public static int ModDuration(Note n)
        {
            Assert.Equal(0, (n.stop - n.start) % MidiPerModTick);
            return (n.stop - n.start) / MidiPerModTick;
        }

        /// <summary>Tempo-event time in module ticks.</summary>
        public static int ModTime(TempoEvent e)
        {
            Assert.Equal(0, e.Time % MidiPerModTick);
            return e.Time / MidiPerModTick;
        }

        /// <summary>Notes on a module channel, ordered by start. Track 0 is tempo; channel c → track c+1.</summary>
        public static List<Note> ChannelNotes(Song song, int channel)
        {
            int track = channel + 1;
            Assert.True(track < song.Tracks.Count, $"missing MIDI track for channel {channel}");
            return song.Tracks[track].Notes.OrderBy(n => n.start).ThenBy(n => n.pitch).ToList();
        }
    }
}
