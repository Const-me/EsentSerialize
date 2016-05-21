using EsentSerialization;
using System;
using System.IO;

namespace RestoreDatabase
{
	class Program
	{
		enum eKind : byte
		{
			Streaming,
			External
		}

		static void Main( string[] args )
		{
			if( 1 != args.Length )
			{
				Console.WriteLine( "Usage: RestoreDatabase.exe <backup-path>" );
				return;
			}

			string path = args [ 0];

			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				maxConcurrentSessions = 1,
				folderLocation = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentDemoApp" )
			};

			eKind kind;
			if( Directory.Exists( path ) )
				kind = eKind.Streaming;
			else if( File.Exists( path ) )
				kind = eKind.External;
			else
			{
				Console.WriteLine( "The argument is neither file nor directory." );
				return;
			}

			try
			{
				switch( kind )
				{
					case eKind.External:
						Backup.ExternalRestore( new FileStream( path, FileMode.Open, FileAccess.Read ), settings );
						break;
					case eKind.Streaming:
						Backup.StreamingRestore( path, settings );
						break;
				}
			}
			catch( Exception ex )
			{
				Console.WriteLine( "Failed: {0}", ex.Message );
			}
			Console.WriteLine( "Restored OK" );
		}
	}
}