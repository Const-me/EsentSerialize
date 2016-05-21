using Database;
using EsentSerialization;
using System;
using System.Collections.Generic;

namespace ConsoleDemo
{
	class Program
	{
		static void PrintPersons( IEnumerable<Person> arr )
		{
			foreach( var p in arr )
				Console.WriteLine( "{0}", p.ToString() );
			Console.WriteLine();
		}

		static void RunTests( iSerializerSession sess )
		{
			var rs = sess.Recordset<Person>();

			using( var trans = sess.BeginTransaction() )
			{
				Console.WriteLine( "Total count: {0}", rs.Count() );

				Console.WriteLine( "Sorted by sex:" );
				PrintPersons( rs.orderBy( p => p.sex ) );

				Console.WriteLine( "Males:" );
				PrintPersons( rs.where( p => Person.eSex.Male == p.sex ) );

				Console.WriteLine( @"Names containing ""Smith"":" );
				PrintPersons( rs.where( p => p.name.Contains( "Smith" ) ) );

				Console.WriteLine( "Phone" );
				PrintPersons( rs.where( p => Queries.Contains( p.phones, "+1 800 642 7676" ) ) );
				// The equivalent:
				// PrintPersons( rs.where( p => p.phones.Contains( "+1 800 642 7676" ) ) );
			}
		}

		static void Main( string[] args )
		{
			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				maxConcurrentSessions = 1,
				folderLocation = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentDemoApp" )
			};

			using( var pool = EsentDatabase.open( settings, typeof( Program ).Assembly ) )
			using( var sess = pool.GetSession() )
			{
				if( pool.isNewDatabase )
				{
					Person.populateWithDebugData( sess );
					FiltersTest.populateWithDebugData( sess );
				}
				RunTests( sess );
			}
		}
	}
}