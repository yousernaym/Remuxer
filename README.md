# The Retro Music Converter
A command-line tool written in C# that converts tracker modules and sid files to midi and wav.  

Made for the purpose of adding mod and sid support to [Visual Music](https://github.com/yousernaym/vm) but can be used as a stand-alone tool as well.
Based on [libRemuxer](https://github.com/yousernaym/libremuxer) which is based on the third-party libraries [libmikmod](http://mikmod.sourceforge.net/), [libopenmpt](https://lib.openmpt.org/libopenmpt/) and [libsidplayfp](https://sourceforge.net/projects/sidplay-residfp/). The original intention was to link libRemuxer directly to Visual Music, but because of an incompatibility between the MS-PL license of MonoGame and the GPL license of libsidplayfp, I had to separate the address spaces.