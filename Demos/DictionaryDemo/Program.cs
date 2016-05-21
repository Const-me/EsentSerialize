using System;
using System.Linq;

namespace DictionaryDemo
{
	class Program
	{
		static string ask( string prompt, bool bNewLine )
		{
			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.Write(prompt );
			if( bNewLine )
				Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.Green;
			string res = Console.ReadLine();
			Console.ForegroundColor = ConsoleColor.Gray;
			return res;
		}

		static void TestMain( DemoDictionary dict )
		{
			while( true )
			{
				string line = ask( "L = List, S = Set, R = remove, Q = quit, W = view raw, SH = set to huge value", true ).ToLower();
				
				if( line == "l" )
				{
					foreach (var kvp in dict)
						Console.WriteLine( "{0}\t{1}", kvp.Key, kvp.Value.myObjectMemeber );
					continue;
				}
				if( line == "s" )
				{
					string k = ask( "Key: ", false );
					string v = ask( "Value: ", false );
					ValueType val = new ValueType();
					val.myObjectMemeber = v;
					dict[ k ] = val;
					Console.WriteLine( "OK" );
					continue;
				}
				if( line == "sh" )
				{
					string k = ask( "Key: ", false );
					ValueType val = new ValueType();
					val.initRandom();
					dict[ k ] = val;
					Console.WriteLine( "OK" );
					continue;
				}
				if( line == "r" )
				{
					string k = ask( "Key: ", false );
					bool res = dict.Remove( k );
					Console.WriteLine( res ? "Removed OK" : "No such key" );
					continue;
				}
				if( line == "q" )
					return;
				if( line == "w" )
				{
					string k = ask( "Key: ", false ); byte[] val = dict.dbgRawValue( k );
					if( null == val )
					{
						Console.WriteLine( "Key not found" );
						continue;
					}
					Console.WriteLine( "OK, {0} bytes total.", val.Length );
					if( val.Length > 4 * 1024 )
						Console.WriteLine( "Value to large, only the first 4 kb shown" );
					val = val.Take( 4 * 1024 ).ToArray();
					Console.WriteLine( "Raw: {0}", Console.OutputEncoding.GetString( val ) );
					Console.WriteLine( "Hex: {0}", BitConverter.ToString( val ).Replace( "-", "" ) );
				}
			}
		}

		static void Main( string[] args )
		{
			string folder = Environment.ExpandEnvironmentVariables( @"%TEMP%\DictionaryDemo" );
			using( var dict = new DemoDictionary( folder ) )
			{
				TestMain( dict );
			}
		}
	}
}