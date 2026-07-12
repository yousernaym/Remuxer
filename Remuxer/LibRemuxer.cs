using System.Runtime.InteropServices;
using System.Text;

namespace Remuxer
{
    class LibRemuxer
    {
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "initLib")]
        public static extern void InitLib();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "closeLib")]
        public static extern void CloseLib();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "beginProcessing")]
        public static extern bool BeginProcessing(ref Args a);
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "process")]
        public static extern float Process();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "endProcessing")]
        public static extern void EndProcessing();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "getNumTrackAudioFiles")]
        public static extern int GetNumTrackAudioFiles();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "getTrackAudioFile")]
        public static extern bool GetTrackAudioFile(int index, out int midiTrack, StringBuilder path, int maxLength);
    }
}
