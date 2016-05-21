using Database;
using EsentSerialization;
using System;
using System.Windows.Forms;

namespace WinFormsDemoApp
{
	static class Program
	{
		/// <summary>The main entry point for the application.</summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				maxConcurrentSessions = 1,
				folderLocation = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentDemoApp" )
			};

			using( var pool = EsentDatabase.open( settings, typeof( Program ).Assembly ) )
			using( var sess = pool.GetSession() )
			{
				if( pool.isNewDatabase )
					Person.populateWithDebugData( sess );

				using( var trans = sess.BeginTransaction() )
				{
					using( var frm = new Form1( sess ) )
					{
						if( DialogResult.OK == frm.ShowDialog() )
							trans.Commit();
					}
				}
			}
		}
	}
}