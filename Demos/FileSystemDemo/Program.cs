using EsentSerialization;
using System;
using System.Windows.Forms;

namespace Test1
{
	static class Program
	{
		/// <summary>The main entry point for the application.</summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			EsentDatabase.Settings settings = new EsentDatabase.Settings(Environment.ExpandEnvironmentVariables( @"%APPDATA%\EseFileSystemDemo" ));
			settings.maxConcurrentSessions = 5;

			using( var sp = EsentDatabase.open(settings, typeof( EseFileSystem.EfsEntry ) ) )
			{
				if( sp.isNewDatabase )
				{
					using(var sess = sp.GetSession())
					using( var trans = sess.BeginTransaction() )
					{
						Cursor<EseFileSystem.EfsEntry> cur;
						sess.getTable( out cur );
						cur.Add( EseFileSystem.EfsEntry.NewRoot() );
						trans.Commit();
					}
				}
				Application.Run( new Form1( sp ) );
			}
		}
	}
}
