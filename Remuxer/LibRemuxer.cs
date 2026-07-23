using System.Runtime.InteropServices;

namespace Remuxer
{
    class LibRemuxer
    {
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "initLib")]
        public static extern void InitLib();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "closeLib")]
        public static extern void CloseLib();
        // Path fields in Args are UTF-8 (LPUTF8Str). Default ANSI marshalling corrupts non-ASCII
        // paths (e.g. under a non-Latin user profile).
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "beginProcessing")]
        public static extern bool BeginProcessing(ref Args a);
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "process")]
        public static extern float Process();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "endProcessing")]
        public static extern void EndProcessing();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "getNumTrackAudioFiles")]
        public static extern int GetNumTrackAudioFiles();
        // Native writes a UTF-8 path into the caller buffer (nul-terminated).
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "getTrackAudioFile")]
        public static extern bool GetTrackAudioFile(int index, out int midiTrack, out int channel,
            [Out] byte[] pathUtf8, int maxLength);
    }
}
