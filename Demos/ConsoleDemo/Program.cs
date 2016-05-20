using Database;
using EsentSerialization;
using System;
using System.Collections.Generic;

namespace ConsoleDemo
{
	class Program
	{
		static void PopulateDemoDatabase( iSerializerSession sess )
		{
			Person[] personsTest = new Person[]
			{
				new Person( Person.eSex.Female, "Jenifer Smith", new string[0] ),
				new Person( Person.eSex.Male, "Konstantin", new string[]{ "+7 926 139 63 18" } ),
				new Person( Person.eSex.Male, "John Smith", new string[]{ "+1 800 123 4567", "+1 800 123 4568" } ),
				new Person( Person.eSex.Female, "Mary Jane", new string[]{ "555-1212" } ),
				new Person( Person.eSex.Other, "Microsoft", new string[]{ "+1 800 642 7676", "1-800-892-5234" } ),
				new Person( Person.eSex.Other, "Contoso Ltd.", new string[]{ "+1 800 642 7676", "+1 800 642 7676", "+1 800 642 7676", "+1 800 555 7676" } ),
			};

			Cursor<Person> curPerson;
			sess.getTable( out curPerson );

			using( var trans = sess.BeginTransaction() )
			{
				curPerson.AddRange( personsTest );
				trans.Commit();
			}
		}

		static void PrintPersons( IEnumerable<Person> arr )
		{
			foreach( var p in arr )
				Console.WriteLine( "{0}", p.ToString() );
			Console.WriteLine();
		}

		static void RunTests( iSerializerSession sess )
		{
			Recordset<Person> rs;
			sess.getTable( out rs );

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
					PopulateDemoDatabase( sess );
				RunTests( sess );
			}
		}
	}
}