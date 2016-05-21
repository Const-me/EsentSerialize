using EsentSerialization;
using System;

namespace SchemaUpgradeDemo
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
			};

			var curPerson = sess.Cursor<Person>();

			using( var trans = sess.BeginTransaction() )
			{
				curPerson.AddRange( personsTest );
				trans.Commit();
			}
		}

		// The current version of the application.
		const int DB_SCHEMA_VERSION = 3;

		static void Main( string[] args )
		{
			string strDatabasePath = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentSchemaUpgradeDemo" );
			using( var serializer = new EseSerializer( strDatabasePath, null ) )
			using( var sess = serializer.OpenDatabase( false ) )
			{
				if( serializer.isNewDatabase )
				{
					sess.AddType( typeof( Person ) );

					PopulateDemoDatabase( sess );

					DatabaseSchemaUpdater dbUpdater = new DatabaseSchemaUpdater( sess );
					dbUpdater.DatabaseSchemaVersion = DB_SCHEMA_VERSION;
					dbUpdater.Execute();
				}
				else
				{
					DatabaseSchemaUpdater dbUpdater = new DatabaseSchemaUpdater( sess );

					switch( dbUpdater.DatabaseSchemaVersion )
					{
						case 0:
							dbUpdater.DatabaseSchemaVersion = DB_SCHEMA_VERSION;
							goto case 1;
						case 1:
							dbUpdater.AddColumn<Person>( "note" );
							dbUpdater.DatabaseSchemaVersion = 2;
							goto case 2;
						case 2:
							dbUpdater.CreateIndex<Person>( "name" );
							dbUpdater.DatabaseSchemaVersion = 3;
							goto case 3;
						case DB_SCHEMA_VERSION:
							break;
						default:
							throw new ApplicationException( "Unknown DB schema version #" + dbUpdater.DatabaseSchemaVersion.ToString() );
					}

					dbUpdater.Execute();

					sess.AddType( typeof( Person ) );
				}

				Console.WriteLine( "Initialized OK" );
			}
		}
	}
}