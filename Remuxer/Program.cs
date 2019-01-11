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
		static void Main()
		{
			string cmdLine = Environment.CommandLine;
			//MessageBox.Show(cmdLineArgs);
			cmdLine = "test.mod -m \"out put.mid\" -a output.wav";
			Args args = new Args();
			if (string.IsNullOrWhiteSpace(cmdLine))
			{
				showErrorMsg("No arguments specified.");
				return;
			}

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
						args.midiPath = readCmdLinePath(cmdLine, ref pos);
					else if (flag == 'a')
						args.audioPath = readCmdLinePath(cmdLine, ref pos);
					else
					{
						showErrorMsg($"Invalid flag -{cmdLine[pos]}");
						return;
					}
	
				}
				else
					args.inputPath = readCmdLinePath(cmdLine, ref pos);
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
