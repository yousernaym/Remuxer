using System;
using System.Collections.Generic;
using System.IO;
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
			Args args = new Args();
			if (cmdLineArgs.Length == 0)
			{
				showErrorMsg("No arguments specified.");
				return;
			}

			bool audioFlag = false, midiFlag = false;
			for (int i = 0; i < cmdLineArgs.Length; i++)
			{
				string arg = cmdLineArgs[i];
				if (arg.Length >= 2 && arg[0] == '-')
				{
					char flag = arg[1];
					string flagPath = null;
					if (i + 1 < cmdLineArgs.Length && cmdLineArgs[i + 1][0] != '-') //:No path was specified
						flagPath = cmdLineArgs[++i];
					if (flag == 'm')
					{
						midiFlag = true;
						args.midiPath = flagPath;
					}
					else if (flag == 'a')
					{
						audioFlag = true;
						args.audioPath = flagPath;
					}
					else
					{
						showErrorMsg($"Invalid flag -{flag}");
						return;
					}
				}
				else
				{
					args.inputPath = cmdLineArgs[i];
					if (string.IsNullOrWhiteSpace(args.inputPath))
					{
						MessageBox.Show("No input file specified.");
						return;
					}
				}
			}
			if (midiFlag && args.midiPath == null)
				args.midiPath = Path.ChangeExtension(args.inputPath, "mid");
			if (audioFlag && args.audioPath == null)
				args.audioPath = Path.ChangeExtension(args.inputPath, "wav");

			//Check validity of input path
			if (!File.Exists(args.inputPath))
			{
				MessageBox.Show($"Couldn't find or access input file {args.inputPath}");
				return;
			}

			//Check validity of output paths
			var path = args.midiPath;
			try
			{
				checkPath(path);
				path = args.audioPath;
				checkPath(path);
			}
			catch (Exception e)
			{
				MessageBox.Show($"Invalid path/filename: \"{path}\"\n\n{e.Message}");
				return;
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

		static void checkPath(string path)
		{
			var file = File.Create(path);
			file.Close();
			File.Delete(path);
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
