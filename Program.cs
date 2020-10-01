using System;
using NDesk.Options;
using System.Collections.Generic;
using System.IO;


namespace ManagerIO_Sqlite
{
	class MainClass
	{
		static public void BackupFile(String filename) {
			DateTime lastWriteTime=(new FileInfo (filename)).LastWriteTime;
			string backupFilename=Path.Combine(
				Path.GetDirectoryName(filename),
				Path.GetFileNameWithoutExtension (filename)+
				string.Format("_{0:yyyy-MM-dd-HHmmss}_backup.manager",lastWriteTime));
			if(!File.Exists(backupFilename))
				File.Copy (filename, backupFilename);
            System.Diagnostics.Process.Start ("lzip", backupFilename);
        }

        public static void Main (string[] args)
		{
			int port=8080;
			bool shouldShowHelp = false;
			string fileName=null;
			OptionSet options = new OptionSet { 
				{ "n|port=", "the name of someone to greet.", n => port = Convert.ToInt32(n) }, 
				{ "h|help", "show this message and exit", h => shouldShowHelp = h != null }
			};
			List<string> extra;
			try {
				// parse the command line
				extra = options.Parse (args);
			} catch (OptionException e) {
				// output some error message
				Console.Write ("greet: ");
				Console.WriteLine (e.Message);
				Console.WriteLine ("Try `--help' for more information.");
				return;
			}
			if(extra.Count > 0)
				fileName = extra [0];
			if(shouldShowHelp || fileName==null) {
				options.WriteOptionDescriptions(Console.Out);
				return;
			}
			Model model=new Model ();
			bool backedup = false;
			model.PreChangeEvents += new Model.ChangeEvent(delegate() {
				if(!backedup) {
					BackupFile(fileName);
					backedup=true;
				}
			});


			model.Open ("URI=file:"+fileName);
			String url = "http://localhost:" + port + "/";
			WebApp webApp = new WebApp(model,url);

			try {
				Console.WriteLine("Press a key to quit. Click here to visit: "+url);
				Console.WriteLine("Search transactions: "+url+"SearchTransactions");
				webApp.Start();
				Console.ReadKey();
			} finally {
				webApp.Stop();
				model.Close();
			}
		}
	}
}
