using Database;
using EsentSerialization;
using Shared;
using System;
using System.ServiceModel;

namespace Server
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

			Cursor<Person> curPerson;
			sess.getTable( out curPerson );

			using( var trans = sess.BeginTransaction() )
			{
				curPerson.AddRange( personsTest );
				trans.Commit();
			}
		}

		static void Main( string[] args )
		{
			Console.WriteLine( "Starting the service..." );

			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				maxConcurrentSessions = 16,
				folderLocation = Environment.ExpandEnvironmentVariables( @"%APPDATA%\EsentDemoApp" )
			};

			using( SessionPool pool = EsentDatabase.open( settings, typeof( Person ) ) )
			{
				if( pool.isNewDatabase )
				{
					using( var sess = pool.GetSession() )
						PopulateDemoDatabase( sess );
				}

				Service singletoneInstance = new Service( pool );
				using( ServiceHost host = new ServiceHost( singletoneInstance, ServiceConfig.baseAddress ) )
				{
					host.AddServiceEndpoint( typeof( iPersonsService ), new NetNamedPipeBinding(), ServiceConfig.pipeName );
					host.Open();

					ServiceConfig.eventServerReady.Set();
					try
					{
						Console.WriteLine( "Server running. Press any key to shut down" );
						Console.ReadKey( true );
						Console.WriteLine( "Shutting down..." );
					}
					finally
					{
						ServiceConfig.eventServerReady.Reset();
					}
				}
			}
			Console.WriteLine( "The service has stopped." );
		}
	}
}