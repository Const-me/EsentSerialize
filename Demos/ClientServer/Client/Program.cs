using Shared;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace Client
{
	class Program
	{
		static void PrintPersons( IEnumerable<PersonMessage> arr )
		{
			foreach( var p in arr )
				Console.WriteLine( "{0}", p.ToString() );
			Console.WriteLine();
		}

		static void close( ICommunicationObject co )
		{
			if( null == co )
				return;
			// http://stackoverflow.com/a/9063971/126995
			try
			{
				co.Close();
			}
			catch
			{
				co.Abort();
			}
		}

		static void Main( string[] args )
		{
			Console.WriteLine( "Waiting for the server to launch..." );
			ServiceConfig.eventServerReady.WaitOne();

			Console.WriteLine( "Connecting to the server..." );

			ChannelFactory<iPersonsService> pipeFactory = new ChannelFactory<iPersonsService>( new NetNamedPipeBinding(), new EndpointAddress( ServiceConfig.endpointAddress ) );
			iPersonsService channel = pipeFactory.CreateChannel();

			Console.WriteLine( "Server us up, running the tests." );

			Console.WriteLine( "Total count: {0}", channel.getTotalCount() );

			Console.WriteLine( "Sorted by sex:" );
			PrintPersons( channel.sortBySex() );

			Console.WriteLine( "Males:" );
			PrintPersons( channel.queryBySex( PersonMessage.eSex.Male ) );

			Console.WriteLine( @"Names containing ""Smith"":" );
			PrintPersons( channel.queryByNameSubstring( "Smith" ) );

			Console.WriteLine( "Shutting down..." );
			close( channel as ICommunicationObject );
			close( pipeFactory as ICommunicationObject );
		}
	}
}