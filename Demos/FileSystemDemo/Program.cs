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
			settings.PresetMedium();

			using( var sp = EsentDatabase.open(settings, typeof( EseFileSystem.EfsEntry ) ) )
			{
				if( sp.isNewDatabase )
				{
					using(var sess = sp.GetSession())
					using( var trans = sess.BeginTransaction() )
					{
						var cur = sess.Cursor<EseFileSystem.EfsEntry>();
						cur.Add( EseFileSystem.EfsEntry.NewRoot() );
						trans.Commit();
					}
				}
				Application.Run( new Form1( sp ) );
			}
		}
	}
}