using PerfVsSqlite.Database;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PerfVsSqlite
{
	static class Tests
	{
		public static Task<string> runTest( bool SQLite, Func<iDatabase, Tuple<int, TimeSpan>> act )
		{
			return Task.Run( () => runTestImpl( SQLite, act ) );
		}

		static string runTestImpl( bool SQLite, Func<iDatabase, Tuple<int, TimeSpan>> act )
		{
			iDatabase db;
			string title;
			if( SQLite )
			{
				title = "SQLite";
				db = new SQLite.DB();
			}
			else
			{
				title = "ESENT";
				db = new ESENT.DB();
			}

			using( db )
			{
				try
				{
					Tuple<int, TimeSpan> res = act( db );
					double rps = (double)res.Item1 / res.Item2.TotalSeconds;
					return String.Format( "[{0}] Completed OK in {1} seconds; {2} records / second average.",
						title, res.Item2.TotalSeconds, rps );
				}
				catch( Exception ex )
				{
					return String.Format( "[{0}] Failed: {1}", title, ex.Message );
				}
			}
		}

		static readonly int batchSize = Utils.isPhone() ? 20 : 100;

		public static Tuple<int, TimeSpan> populate( iDatabase db )
		{
			int records = 0;
			TimeSpan res = TimeSpan.Zero;
			for( int i = 0; i < batchSize; i++ )
			{
				res += db.insert( 1000 );
				records += 1000;
			}
			return Tuple.Create( records, res );
		}

		public static Tuple<int, TimeSpan> count( iDatabase db )
		{
			TimeSpan res = TimeSpan.Zero;
			int nTests = 1000;
			int width = 0x700000;
			int records = 0;

			Random r = new Random();
			for( int i = 0; i < nTests; i++ )
			{
				int ind;
				do
				{
					ind = r.Next();
				}
				while( ind < width );

				Stopwatch sw = Stopwatch.StartNew();
				records += db.count( ind - width, ind );
				res += sw.Elapsed;
			}
			return Tuple.Create( records, res );
		}

		public static Tuple<int, TimeSpan> fetch( iDatabase db )
		{
			TimeSpan res = TimeSpan.Zero;
			int nTests = 1000;
			int width = 0x700000;
			int records = 0;

			Random r = new Random();
			for( int i = 0; i < nTests; i++ )
			{
				int ind;
				do
				{
					ind = r.Next();
				}
				while( ind < width );

				Stopwatch sw = Stopwatch.StartNew();
				records += db.fetchAll( ind - width, ind );
				res += sw.Elapsed;
			}
			return Tuple.Create( records, res );
		}
	}
}