using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Remuxer
{
    static class Program
    {
        /// <summary>
        /// Console entry point. Converts a tracker module / SID file into MIDI + WAV via libRemuxer.
        ///
        /// Output contract:
        ///   - One description line (e.g. "Extracting notes and audio from foo.mod").
        ///   - "Progress: N%" updated as N changes. When stdout is a terminal the line is rewritten
        ///     in place with '\r'; when redirected (e.g. launched by Visual Music) each update is
        ///     emitted as its own newline-terminated line so the parent can read it line-by-line.
        ///   - Errors go to stderr; a non-zero exit code signals failure to the caller.
        /// </summary>
        static int Main(string[] cmdLineArgs)
        {
            Args args = new Args();
            if (cmdLineArgs.Length == 0)
            {
                ShowUsage();
                return 0;
            }

            bool audioFlag = false, midiFlag = false;
            string cancelPath = null;
            for (int i = 0; i < cmdLineArgs.Length; i++)
            {
                string arg = cmdLineArgs[i];
                if (arg.Length >= 2 && arg[0] == '-')
                {
                    char flag = arg[1];
                    string flagArg = null;

                    //Was an argument relating to this flag specified?
                    if (arg.Length > 2)
                        flagArg = arg.Substring(2);
                    if (flag == 'm') //Midi output
                    {
                        midiFlag = true;
                        args.midiPath = flagArg;
                    }
                    else if (flag == 'a') //Audio output
                    {
                        audioFlag = true;
                        args.audioPath = flagArg;
                    }
                    else if (flag == 's') //Sub song
                    {
                        if (flagArg != null)
                        {
                            if (!int.TryParse(flagArg, out args.subSong))
                                return ShowUsage($"Invalid -s argument \"{flagArg}\".");
                        }
                    }
                    else if (flag == 'l') //Song lenght
                    {
                        if (flagArg != null)
                        {
                            if (!float.TryParse(flagArg, out args.songLengthS))
                                return ShowUsage($"Invalid -l argument \"{flagArg}\".");
                        }
                    }
                    else if (flag == 'i') //Input note file
                    {
                        args.modInsTrack = true;
                    }
                    else if (flag == 'e') //Suppress conversion errors
                    {
                        args.suppressErrors = true;
                    }
                    else if (flag == 'c') //Cancel signal file
                    {
                        cancelPath = flagArg;
                    }
                    else if (flag == 't') //Per-track audio output base path
                    {
                        args.trackAudioPath = flagArg;
                    }
                    else
                    {
                        return ShowUsage($"Invalid flag -{flag}.");
                    }
                }
                else
                {
                    args.inputPath = cmdLineArgs[i];
                    if (string.IsNullOrWhiteSpace(args.inputPath))
                        return ShowUsage("No input file specified.");
                }
            }

            //Check if input file exists
            if (!File.Exists(args.inputPath))
                return ShowError($"Couldn't find input file \"{args.inputPath}\".");

            //Derive output paths from input path if output path is not specified or if no output flags are specified
            bool noOutputFlags = !midiFlag && !audioFlag;
            if (noOutputFlags || midiFlag && args.midiPath == null)
                args.midiPath = Path.ChangeExtension(args.inputPath, "mid");
            if (noOutputFlags || audioFlag && args.audioPath == null)
                args.audioPath = Path.ChangeExtension(args.inputPath, "wav");

            //Check validity of output paths
            try
            {
                if (midiFlag)
                    CheckPath(args.midiPath, "-m");
                if (audioFlag)
                    CheckPath(args.audioPath, "-a");
                if (!string.IsNullOrEmpty(args.trackAudioPath))
                {
                    //Base path is a file-name prefix inside a directory; ensure the directory exists
                    //and is writable.
                    string dir = Path.GetDirectoryName(args.trackAudioPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    CheckPath(args.trackAudioPath + ".probe", "-t");
                }
            }
            catch (Exception e)
            {
                return ShowError(e.Message);
            }

            LibRemuxer.InitLib();
            try
            {
                return Process(ref args, cancelPath);
            }
            finally
            {
                LibRemuxer.CloseLib();
            }
        }

        static int Process(ref Args args, string cancelPath)
        {
            if (!LibRemuxer.BeginProcessing(ref args))
            {
                if (!args.suppressErrors)
                    ShowError($"Couldn't parse input file \"{args.inputPath}\".");
                return 1;
            }

            try
            {
                string text = "Extracting";
                if (args.midiPath != null)
                {
                    text += " notes";
                    if (args.audioPath != null)
                        text += " and audio";
                }
                else
                    text += " audio";
                text += $" from {Path.GetFileName(args.inputPath)}";

                if (args.numSubSongs > 1)
                    text += $" ({args.subSong}/{args.numSubSongs}).";
                Console.Out.WriteLine(text);

                bool redirected = Console.IsOutputRedirected;
                int lastPercent = -1;
                float progress = 0;
                bool cancelled = false;
                while (progress >= 0)
                {
                    if (IsCancelRequested(cancelPath))
                    {
                        cancelled = true;
                        break;
                    }

                    int percent = (int)(progress * 100);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        if (redirected)
                            Console.Out.WriteLine($"Progress: {percent}%");
                        else
                            Console.Out.Write($"\rProgress: {percent}%");
                    }
                    progress = LibRemuxer.Process();
                }
                if (!redirected)
                    Console.Out.WriteLine(); //terminate the in-place progress line
                return cancelled ? 2 : 0;
            }
            finally
            {
                LibRemuxer.EndProcessing();

                //Enumerate the per-track WAVs that were saved (runs on cancel too — lists only
                //completed tracks). Two line types (paths are always "<base>-chCC.wav"):
                //  "TrackAudio: <miditrack>|<path>"                per-channel mode (assign as Filename)
                //  "TrackVoiceAudio: <miditrack>|<channel>|<path>" per-instrument mode (shared channel WAV)
                int numTrackFiles = LibRemuxer.GetNumTrackAudioFiles();
                var sb = new StringBuilder(1024);
                for (int i = 0; i < numTrackFiles; i++)
                {
                    if (LibRemuxer.GetTrackAudioFile(i, out int midiTrack, out int channel, sb, sb.Capacity))
                    {
                        if (channel < 0)
                            Console.Out.WriteLine($"TrackAudio: {midiTrack}|{sb}");
                        else
                            Console.Out.WriteLine($"TrackVoiceAudio: {midiTrack}|{channel}|{sb}");
                    }
                }
            }
        }

        static bool IsCancelRequested(string cancelPath) =>
            !string.IsNullOrWhiteSpace(cancelPath) && File.Exists(cancelPath);

        static void CheckPath(string path, string flag)
        {
            try
            {
                var file = File.Create(path);
                file.Close();
                File.Delete(path);
            }
            catch (Exception)
            {
                throw new Exception($"Invalid {flag} path \"{path}\".");
            }
        }

        static int ShowUsage(string errorMsg = null)
        {
            TextWriter w = errorMsg == null ? Console.Out : Console.Error;
            if (errorMsg != null)
                w.WriteLine("Error: " + errorMsg);
            w.WriteLine();
            w.WriteLine("Syntax: remuxer <input file> [-<flag>[argument]]");
            w.WriteLine();
            w.WriteLine("Flags:");
            w.WriteLine("-a[wav output file]      default = <input file>.wav");
            w.WriteLine("-m[midi output file]      default = <input file>.mid");
            w.WriteLine("-i One track per instrument instead of one per channel.");
            w.WriteLine("-c[path] Cancel when this signal file exists.");
            w.WriteLine("-t[base path] Also render per-channel WAVs as <base>-chCC.wav (same names with or");
            w.WriteLine("             without -i). Per-channel mode: one file per MIDI track. Per-instrument");
            w.WriteLine("             mode (-i): one file per source channel, shared by instrument tracks that");
            w.WriteLine("             play on it (the host app gates each track to its note ranges).");
            w.WriteLine();
            w.WriteLine("Sid/Hvl-specific:");
            w.WriteLine("-s<subsong number>");
            w.WriteLine("-l<length of song> (Sid only)");
            w.WriteLine();
            w.WriteLine("If both -a and -m are ommitted, both are set implicitly.");
            return errorMsg == null ? 0 : 1;
        }

        public static int ShowError(string errorMsg)
        {
            Console.Error.WriteLine("Error: " + errorMsg);
            return 1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Args
    {
        public string inputPath;
        public string audioPath;
        public string midiPath;
        public bool modInsTrack;
        public float songLengthS;
        public int subSong;
        public int numSubSongs; //out parameter
        public string trackAudioPath; //base path for per-track WAVs ("" = disabled); mirrors native offset 40
        public bool suppressErrors; //unread by native; stays last
    }
}
