using Database;
using EsentSerialization;
using System;
using System.Windows.Forms;

namespace WinFormsDemoApp
{
	static class Program
	{
		static void PopulateDemoDatabase( iSerializerSession sess )
		{
			Person[] personsTest = new Person[]
			{
				new Person( Person.eSex.Female, "Jenifer Smith", new string[0] ),
				new Person( Person.eSex.Male, "Konstantin", new string[]{ "+7 926 012 34 56" } ),
				new Person( Person.eSex.Male, "John Smith", new string[]{ "+1 800 123 4567", "+1 800 123 4568" } ),
				new Person( Person.eSex.Female, "Mary Jane", new string[]{ "555-1212" } ),
				new Person( Person.eSex.Other, "Microsoft", new string[]{ "+1 800 642 7676", "1-800-892-5234" } ),
			};

			Cursor<Person> curPerson;
			sess.getTable( out curPerson );

			using( var trans = sess.BeginTransaction() )
			{
				curPerson.AddRange( personsTest );
				trans.Commit();
			}
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
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
					PopulateDemoDatabase( sess );

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