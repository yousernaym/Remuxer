using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Remuxer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>

		[STAThread]
		static void Main(string[] cmdLineArgs)
		{
			cmdLineArgs = "test.sid -m -a".Split(' '); ;
			bool midiFlag = false, audioFlag = false;
			Args args = new Args();
			if (cmdLineArgs == null || cmdLineArgs.Length == 0)
			{
				showErrorMsg("No arguments specified.");
				return;
			}

			for (int i = 0; i < cmdLineArgs.Length; i++)
			{
				string arg = cmdLineArgs[i];
				if (arg[0] == '-')
				{
					if (arg == "-m")
						midiFlag = true;
					else if (arg == "-a")
						audioFlag = true;
					else
					{
						showErrorMsg($"Invalid flag{arg}");
						return;
					}
				}
				else
				{
					if (midiFlag)
						args.midiPath = arg;
					else if (audioFlag)
						args.audioPath = arg;
					else
						args.inputPath = arg;

				}
			}
			LibRemuxer.initLib();
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1(args));
			LibRemuxer.closeLib();
		}

		static void showErrorMsg(string msg)
		{
			MessageBox.Show(msg);
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
	}
}
