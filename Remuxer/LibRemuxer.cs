using System.Runtime.InteropServices;

namespace Remuxer
{
    class LibRemuxer
    {
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void initLib();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void closeLib();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool beginProcessing(ref Args a);
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern float process();
        [DllImport("libRemuxer.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern float endProcessing();
    }
}
