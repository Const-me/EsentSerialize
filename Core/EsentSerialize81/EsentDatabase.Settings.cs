using System;
using System.IO;

namespace EsentSerialization
{
	public static partial class EsentDatabase
	{
		/// <summary>Class holding various user-adjustable parameters of the database.</summary>
		public class Settings
		{
			/// <summary>Construct with the default values.</summary>
			public Settings() { }

			/// <summary>Construct with absolute database folder location.</summary>
			public Settings( string path )
			{
				folderName = null;
				folderLocation = path;
			}

			/// <summary>Database folder name, default is "db", located in ApplicationData.Current.LocalFolder</summary>
			public string folderName = "db";

			/// <summary>Max concurrent sessions, default is 2.</summary>
			/// <remarks>Sessions are expensive: each session holds a cursor copy for each table.
			/// Two of them should be enough for most WSA applications: one session for the GUI thread, another one for some background uploading/downloading/processing.</remarks>
			public int maxConcurrentSessions = 2;

			/// <summary>Database location. The default is null, which translates to SpecialFolder.LocalApplicationData on desktop, or ApplicationData.LocalFolder on WinRT.</summary>
			public string folderLocation = null;

			/// <summary>Absolute path of the database folder.</summary>
			public string databasePath
			{
				get
				{
					if( String.IsNullOrWhiteSpace( folderName ) )
					{
						if( !String.IsNullOrWhiteSpace( folderLocation ) )
							return folderLocation;

						throw new ArgumentNullException( "folderName must not be empty" );  //< 'coz in Database.drop we wipe the whole content of the folder.
					}
					string appData = folderLocation;
					if( String.IsNullOrWhiteSpace( appData ) )
					{
#if NETFX_CORE
						appData = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#else
						appData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
						// Use entry-point assembly name to construct the application's data folder.
						appData = Path.Combine( appData, System.Reflection.Assembly.GetEntryAssembly().GetName().Name );
#endif
					}
					return Path.Combine( appData, folderName );
				}
			}
		}
	}
}