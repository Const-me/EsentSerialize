using EsentSerialization.Utils.FileIO;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EsentSerialization
{
	/// <summary>This static utility class implements the most high-level entry-point of the library.</summary>
	public static partial class EsentDatabase
	{
		static string databasePath;

		/// <summary>Open database using the provided settings, open some tables.</summary>
		/// <param name="rowTypes">ESENT tables to open, identified by their record types.</param>
		public static SessionPool open( Settings settings, params Type[] rowTypes )
		{
			string path = settings.databasePath;
			databasePath = path;
			return new SessionPool( path, settings.maxConcurrentSessions, rowTypes );
		}

		/// <summary>Open database using default settings, open some tables.</summary>
		/// <param name="rowTypes">ESENT tables to open, identified by their record types.</param>
		public static SessionPool open( params Type[] rowTypes )
		{
			return open( new Settings(), rowTypes );
		}

		/// <summary>Open database using the provided settings, open all tables from the specified assembly.</summary>
		public static SessionPool open( Settings settings, Assembly ass )
		{
			string path = settings.databasePath;
			databasePath = path;
			return new SessionPool( path, settings.maxConcurrentSessions, ass );
		}

		/// <summary>Open database using default settings, open all tables from the specified assembly.</summary>
		public static SessionPool open( Assembly ass )
		{
			return open( new Settings(), ass );
		}

		/// <summary>Erase the complete database. The database must be closed before this call.</summary>
		public static void drop()
		{
			string[] files = FindFiles.EnumerateAll( databasePath ).ToArray();
			foreach( string f in files )
				File.Delete( f );
		}
	}
}