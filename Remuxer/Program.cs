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
		static void Main()
		{
			string cmdLine = Environment.CommandLine;
			//MessageBox.Show(cmdLineArgs);
			//cmdLine = "test.sid -m \"out put.mid\" -a output.wav";
			cmdLine = "test.sid -m -a";

			Args args = new Args();
			if (string.IsNullOrWhiteSpace(cmdLine))
			{
				showErrorMsg("No arguments specified.");
				return;
			}

			bool audioFlag = false, midiFlag = false;
			int pos = 0;
			while (pos < cmdLine.Length)
			{
				while (pos < cmdLine.Length && cmdLine[pos] == ' ')
					pos++;
				if (cmdLine[pos] == '-' && pos < cmdLine.Length - 1)
				{
					pos++;
					char flag = cmdLine[pos++];
					if (flag == 'm')
					{
						midiFlag = true;
						args.midiPath = readCmdLinePath(cmdLine, ref pos);
					}
					else if (flag == 'a')
					{
						audioFlag = true;
						args.audioPath = readCmdLinePath(cmdLine, ref pos);
					}
					else
					{
						showErrorMsg($"Invalid flag -{cmdLine[pos]}");
						return;
					}

				}
				else
				{
					args.inputPath = readCmdLinePath(cmdLine, ref pos);
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

		static string readCmdLinePath(string cmdLine, ref int pos)
		{
			while (pos < cmdLine.Length && cmdLine[pos] == ' ')
				pos++;
			if (pos >= cmdLine.Length || cmdLine[pos] == '-') //:No path was specified
				return null;
			char endingChar = ' ';
			if (cmdLine[pos] == '\"')
			{
				endingChar = '\"';
				pos++;
			}
			int startPos = pos;
			while (pos < cmdLine.Length && cmdLine[pos] != endingChar)
				pos++;
			return cmdLine.Substring(startPos, pos++ - startPos);
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
