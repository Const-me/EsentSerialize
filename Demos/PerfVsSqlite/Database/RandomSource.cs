using System;
using System.Collections.Generic;
using System.Text;

namespace PerfVsSqlite.Database
{
	/// <summary>Utility class to produce random records in wholesale quantities.</summary>
	static class RandomSource
	{
		static readonly Random rand = new Random();
		static readonly StringBuilder buff = new StringBuilder( 64 );
		const string randomChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0011223344556677889900        ";

		static string randomString()
		{
			buff.Clear();
			for( int i = 0; i < 63; i++ )
			{
				int c = rand.Next( randomChars.Length );
				buff.Append( randomChars[ c ] );
			}
			return buff.ToString();
		}

		public static IEnumerable<Record> batch( int length )
		{
			for( int i = 0; i < length; i++ )
			{
				yield return new Record()
				{
					randomInt = rand.Next(),
					shortString = randomString(),
				};
			}
		}
	}
}