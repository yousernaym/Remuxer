using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Remuxer.Tests
{
    /// <summary>
    /// Minimal FastTracker II XM reader for fixture assertions (header, packed pattern, samples).
    /// </summary>
    static class XmFixture
    {
        public const int NoteKeyOff = 97; // XM note-off / key-off in pattern data

        public readonly struct Cell
        {
            public readonly byte Note;   // 0 = empty, 1..96 = C-0..B-9, 97 = key-off
            public readonly byte Ins;
            public readonly byte Vol;    // 0 = empty volume column
            public readonly byte Fx;
            public readonly byte Param;

            public Cell(byte note, byte ins, byte vol, byte fx, byte param)
            {
                Note = note;
                Ins = ins;
                Vol = vol;
                Fx = fx;
                Param = param;
            }

            public bool IsEmpty => Note == 0 && Ins == 0 && Vol == 0 && Fx == 0 && Param == 0;
        }

        public readonly struct Sample
        {
            public readonly uint Length;
            public readonly uint LoopStart;
            public readonly uint LoopLength;
            public readonly byte Type; // bit0-1: loop type (0 = none)

            public Sample(uint length, uint loopStart, uint loopLength, byte type)
            {
                Length = length;
                LoopStart = loopStart;
                LoopLength = loopLength;
                Type = type;
            }

            public bool Loops => (Type & 3) != 0;
        }

        public sealed class Module
        {
            public ushort Channels;
            public ushort NumPatterns;
            public ushort NumInstruments;
            public ushort Flags;
            public ushort Speed;
            public ushort Tempo;
            public Cell[,] Pattern0; // [row, channel]
            public int Pattern0Rows;
            public List<Sample[]> Instruments; // 1-based instruments → samples
        }

        public static Module Load(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 80)
                throw new InvalidDataException("XM too small.");
            string id = Encoding.ASCII.GetString(b, 0, 17);
            if (!id.StartsWith("Extended Module:", StringComparison.Ordinal))
                throw new InvalidDataException("Not an XM file: " + id);

            uint headerSize = BitConverter.ToUInt32(b, 60);
            var mod = new Module
            {
                Channels = BitConverter.ToUInt16(b, 68),
                NumPatterns = BitConverter.ToUInt16(b, 70),
                NumInstruments = BitConverter.ToUInt16(b, 72),
                Flags = BitConverter.ToUInt16(b, 74),
                Speed = BitConverter.ToUInt16(b, 76),
                Tempo = BitConverter.ToUInt16(b, 78),
                Instruments = new List<Sample[]> { null }, // 1-based
            };

            int afterHeader = 60 + (int)headerSize;
            if (mod.NumPatterns < 1)
                throw new InvalidDataException("XM has no patterns.");

            // Walk every pattern so instruments start after pattern N-1 (not only pattern 0).
            // Only pattern 0 is decoded; later patterns are skipped by header + packed size.
            int patternPos = afterHeader;
            for (int p = 0; p < mod.NumPatterns; p++)
            {
                if (patternPos + 9 > b.Length)
                    throw new InvalidDataException("Pattern header truncated.");
                // Pattern header: length(4), packing(1), rows(2), dataSize(2)
                uint pHeaderLen = BitConverter.ToUInt32(b, patternPos);
                ushort rows = BitConverter.ToUInt16(b, patternPos + 5);
                ushort dataSize = BitConverter.ToUInt16(b, patternPos + 7);
                int pdata = patternPos + (int)pHeaderLen;
                int end = pdata + dataSize;
                if (end > b.Length)
                    throw new InvalidDataException("Pattern data truncated.");

                if (p == 0)
                {
                    mod.Pattern0Rows = rows;
                    mod.Pattern0 = new Cell[rows, mod.Channels];
                    int i = pdata;
                    for (int row = 0; row < rows; row++)
                    {
                        for (int ch = 0; ch < mod.Channels; ch++)
                        {
                            if (i >= end)
                                throw new InvalidDataException("Pattern data truncated.");
                            byte note = 0, ins = 0, vol = 0, fx = 0, param = 0;
                            byte msb = b[i];
                            if ((msb & 0x80) != 0)
                            {
                                i++;
                                if ((msb & 1) != 0) note = b[i++];
                                if ((msb & 2) != 0) ins = b[i++];
                                if ((msb & 4) != 0) vol = b[i++];
                                if ((msb & 8) != 0) fx = b[i++];
                                if ((msb & 16) != 0) param = b[i++];
                            }
                            else
                            {
                                note = b[i++];
                                ins = b[i++];
                                vol = b[i++];
                                fx = b[i++];
                                param = b[i++];
                            }
                            mod.Pattern0[row, ch] = new Cell(note, ins, vol, fx, param);
                        }
                    }
                }

                patternPos = end;
            }

            int insStart = patternPos;
            for (int insIndex = 1; insIndex <= mod.NumInstruments; insIndex++)
            {
                if (insStart + 29 > b.Length)
                    throw new InvalidDataException("Instrument header truncated.");
                uint ihSize = BitConverter.ToUInt32(b, insStart);
                ushort numSamples = BitConverter.ToUInt16(b, insStart + 27);
                var samples = new Sample[numSamples];
                int shStart = insStart + (int)ihSize;
                uint totalSampleBytes = 0;
                for (int s = 0; s < numSamples; s++)
                {
                    if (shStart + 40 > b.Length)
                        throw new InvalidDataException("Sample header truncated.");
                    uint slen = BitConverter.ToUInt32(b, shStart);
                    uint loopStart = BitConverter.ToUInt32(b, shStart + 4);
                    uint loopLen = BitConverter.ToUInt32(b, shStart + 8);
                    byte type = b[shStart + 14];
                    samples[s] = new Sample(slen, loopStart, loopLen, type);
                    totalSampleBytes += slen;
                    shStart += 40;
                }
                mod.Instruments.Add(samples);
                insStart = insStart + (int)ihSize + 40 * numSamples + (int)totalSampleBytes;
            }

            return mod;
        }

        public static Cell CellAt(Module mod, int row, int channel) => mod.Pattern0[row, channel];
    }
}
