using EsentSerialization;
using EsentSerialization.Linq;
using PerfVsSqlite.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PerfVsSqlite.ESENT
{
	class DB : iDatabase
	{
		readonly SessionPool pool;
		readonly Query<Record> qBetween;

		public DB()
		{
			// This demo app opens/closes the database with each test.
			// That's not normally done: usually, the database is initialized only once when application is launched.
			// To optimize for such unusual use case, we're tuning a few advanced DB parameters here.
			EsentDatabase.Settings settings = new EsentDatabase.Settings();
			settings.advanced.EnableFileCache = true;   // Use Windows file cache
			settings.advanced.EnableViewCache = true;   // Read directly from the memory-mapped DB

			pool = EsentDatabase.open( settings, typeof( Record ) );

			// Precompile query to filter records by that random column.
			qBetween = pool.filter( ( Record r, int from, int to ) => r.randomInt >= from && r.randomInt <= to );
		}

		public void Dispose()
		{
			pool.Dispose();
		}

		public TimeSpan insert( int howMany )
		{
			List<Record> batch = RandomSource.batch( howMany ).ToList();

			Stopwatch sw = Stopwatch.StartNew();
			using( var sess = pool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				sess.Cursor<Record>().AddRange( batch );
				trans.Commit();
			}
			return sw.Elapsed;
		}

		public int count( int from, int to )
		{
			using( var sess = pool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				return sess.Recordset<Record>().count( qBetween, from, to );
			}
		}

		public int fetchAll( int from, int to )
		{
			int c = 0;
			using( var sess = pool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				foreach( var r in sess.Recordset<Record>().all( qBetween, from, to ) )
					c++;
			}
			return c;
		}

		int iDatabase.countAll()
		{
			using( var sess = pool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				return sess.Recordset<Record>().Count();
			}
		}

		int iDatabase.fetchAll()
		{
			int res = 0;
			using( var sess = pool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				foreach( var r in sess.Recordset<Record>().all() )
					res++;
			}
			return res;
		}
	}
}