using System;

namespace PerfVsSqlite.Database
{
	/// <summary>This interface abstracts the DB engine.</summary>
	interface iDatabase: IDisposable
	{
		TimeSpan insert( int howMany );

		int count( int from, int to );

		int fetchAll( int from, int to );
	}
}