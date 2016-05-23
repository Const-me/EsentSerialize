using PerfVsSqlite.Database;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PerfVsSqlite.SQLite
{
	class DB : iDatabase
	{
		const SQLiteOpenFlags flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache;
		readonly SQLiteConnection conn;

		public DB()
		{
			string appData = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
			string db = Path.Combine( appData, "SQLite" );

			if( Directory.Exists( db ) )
				Directory.CreateDirectory( db );

			conn = new SQLiteConnection( db, flags, true );
			conn.BeginTransaction();
			conn.CreateTable<Record>();
			conn.Commit();
		}

		void IDisposable.Dispose()
		{
			conn.Dispose();
		}
		TimeSpan iDatabase.insert( int howMany )
		{
			List<Record> batch = RandomSource.batch( howMany ).ToList();
			Stopwatch sw = Stopwatch.StartNew();
			conn.InsertAll( batch, typeof( Record ), true );
			return sw.Elapsed;
		}

		int iDatabase.count( int from, int to )
		{
			conn.BeginTransaction();
			try
			{
				return conn.Table<Record>()
					.Where( r => r.randomInt >= from && r.randomInt <= to )
					.Count();
			}
			finally
			{
				conn.Rollback();
			}
		}

		int iDatabase.fetchAll( int from, int to )
		{
			conn.BeginTransaction();
			try
			{
				int res = 0;
				foreach( var rec in conn.Table<Record>().Where( r => r.randomInt >= from && r.randomInt <= to ) )
					res++;
				return res;
			}
			finally
			{
				conn.Rollback();
			}
		}
	}
}