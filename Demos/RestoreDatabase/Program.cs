using EsentSerialization;
using System;

namespace RestoreDatabase
{
	class Program
	{
		static void Main( string[] args )
		{
			if( 1 != args.Length )
			{
				Console.WriteLine( "Usage: RestoreDatabase.exe <backup-path>" );
				return;
			}

			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				maxConcurrentSessions = 1,
				folderLocation = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentDemoApp" )
			};

			try
			{
				Backup.StreamingRestore( args[ 0 ], settings );
			}
			catch( Exception ex )
			{
				Console.WriteLine( "Failed: {0}", ex.Message );
			}
			Console.WriteLine( "Restored OK" );
		}
	}
}