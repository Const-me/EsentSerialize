using EsentSerialization.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EsentSerialization
{
	/// <summary>This static class implements LINQ-like operations on ESENT recordsets.</summary>
	/// <remarks>
	/// <para>This static class dramatically simplifies ESENT searches, exposing LINQ-like API.</para>
	/// <para>This brings some performance overhead, but you can minimize the overhead by pre-compiling your queries. A pre-compiled query is just as efficient as manual recordset filtering.</para>
	/// <para>Only a small subset of LINQ is supported: you can only search/sort if your table has index that supports the operation. If there’s no such index, you’ll get NotSupportedException while compiling the query.
	/// The performance difference between index queries and sequential scan is too large, that’s why this class doesn’t fall back to sequential scan if unable to find the suitable index.</para>
	/// <para>To compile a sort query, call Queries.sort, to compile a search query, call Queries.filter.</para>
	/// <para>Search queries support arguments. To compile such query, call Queries.filter&lt;tRow, tArg1&gt or Queries.filter&lt;tRow, tArg1, tArg2&gt or Queries.filter&lt;tRow, tArg1, tArg2, tArg3&gt.
	/// To supply values of the arguments when running a query, pass them in Queries.all variadic parameters. If you’ll fail to supply correct count of arguments, or correct types of arguments, an exception will be thrown in runtime.</para>
	/// </remarks>
	public static class Queries
	{
		/// <summary>Run a pre-compiled query, return all matching records.</summary>
		public static IEnumerable<tRow> all<tRow>( this Recordset<tRow> rs, Query<tRow> q, params object[] args ) where tRow : new()
		{
			q.query( rs, args );
			if( q.multivalues )
				return rs.uniq();
			return rs.all();
		}

		/// <summary>Run a pre-compiled query, return count of matching records.</summary>
		public static int count<tRow>( this Recordset<tRow> rs, Query<tRow> q, params object[] args ) where tRow : new()
		{
			q.query( rs, args );
			if( q.multivalues )
				return rs.CountUniq();
			return rs.Count();
		}

		/// <summary>Functionally similar to Enumerable.OrderBy.</summary>
		/// <param name="keySelector">Must be a simple property/field expression on the record class returning the column value. The table must have the index to sort on this column.</param>
		public static IEnumerable<tRow> orderBy<tRow, tKey>( this Recordset<tRow> rs, Expression<Func<tRow, tKey>> keySelector ) where tRow : new()
		{
			return rs.orderBy( keySelector, false );
		}

		/// <summary>Functionally similar to Enumerable.OrderByDescending.</summary>
		/// <param name="keySelector">Must be a simple property/field expression on the record class returning the column value. The table must have the index to sort on this column.</param>
		public static IEnumerable<tRow> orderByDescending<tRow, tKey>( this Recordset<tRow> rs, Expression<Func<tRow, tKey>> keySelector ) where tRow : new()
		{
			return rs.orderBy( keySelector, true );
		}

		static string sortIndex( this iTypeSerializer ser, MemberInfo mi, out bool indexDirectionPositive, out bool multi )
		{
			IndexForColumn[] indices = ser.indicesFromColumn( mi );

			IndexForColumn found = null;

			foreach( var i in indices )
			{
				if( 0 != i.columnIndex )
					continue;	// For sorting, the column have to be the first on the index
				if( i.primary )
				{
					// Primary indices should be the best for performance
					found = i;
					break;
				}
				if( null == found )
					found = i;
			}
			if( null == found )
				throw new ArgumentException( "No sort index found for the column {0}".formatWith( mi.Name ) );

			indexDirectionPositive = found.indexDirectionPositive;
			multi = mi.getColumnAttribute().isMultiValued;
			return found.indexName;
		}

		/// <summary>Compile query to sort the table by the index.</summary>
		public static SortQuery<tRow> sort<tRow, tKey>( iTypeSerializer ser, Expression<Func<tRow, tKey>> exp, bool descending ) where tRow : new()
		{
			var me = exp.Body as MemberExpression;
			if( null == me )
				throw new NotSupportedException( "Currently, orderBy[Descending] only supports ordering by a single column." );

			IndexForColumn[] indices = ser.indicesFromColumn( me.Member );

			bool indexDirectionPositive, multi;

			string ind = ser.sortIndex( me.Member, out indexDirectionPositive, out multi );

			bool shouldInvert = descending ^ ( !indexDirectionPositive);

			return new SortQuery<tRow>( r => r.filterSort( ind, shouldInvert ), multi );
		}

		static IEnumerable<tRow> orderBy<tRow, tKey>( this Recordset<tRow> rs, Expression<Func<tRow, tKey>> keySelector, bool flip ) where tRow : new()
		{
			Query<tRow> q = sort( rs.cursor.serializer, keySelector, flip );
			return rs.all( q );
		}

		/// <summary>Compile query to filter the table by index.</summary>
		/// <seealso cref="where" />
		public static SearchQuery<tRow> filter<tRow>( iTypeSerializer ser, Expression<Func<tRow, bool>> exp ) where tRow : new()
		{
			return FilterQuery.query( ser, exp );
		}

		/// <summary>Compile query to filter the table by index, where query has one parameter.</summary>
		public static SearchQuery<tRow> filter<tRow, tArg1>( iTypeSerializer ser, Expression<Func<tRow, tArg1, bool>> exp ) where tRow : new()
		{
			return FilterQuery.query( ser, exp );
		}

		/// <summary>Compile query to filter the table by index, where query has two parameter.</summary>
		public static SearchQuery<tRow> filter<tRow, tArg1, tArg2>( iTypeSerializer ser, Expression<Func<tRow, tArg1, tArg2, bool>> exp ) where tRow : new()
		{
			return FilterQuery.query( ser, exp );
		}

		/// <summary>Compile query to filter the table by index, where query has three parameter.</summary>
		public static SearchQuery<tRow> filter<tRow, tArg1, tArg2, tArg3>( iTypeSerializer ser, Expression<Func<tRow, tArg1, tArg2, tArg3, bool>> exp ) where tRow : new()
		{
			return FilterQuery.query( ser, exp );
		}

		/// <summary>Functionally similar to Enumerable.Where</summary>
		/// <remarks>
		/// <para>The table must have the index to perform the query. This method doesn't fall back to the MS-provided LINQ when there's no index, it will throw an exception instead.
		/// This is by design: ESENT indices can be faster than linear table scan by orders of magnitudes. If you need to filter usnig linear scan, call Recordset.all() and then filter however you like using the full power of LINQ.</para>
		/// <para>Only a small subset of expressions is supported by the query compiler.</para>
		/// <para>It's recommended to precompile your queries on startup to save some CPU time.</para>
		/// <seealso cref="filter" />
		/// </remarks>
		public static IEnumerable<tRow> where<tRow>( this Recordset<tRow> rs, Expression<Func<tRow, bool>> exp ) where tRow : new()
		{
			Query<tRow> q = filter( rs.cursor.serializer, exp );
			return rs.all( q );
		}

		/// <summary>When encountered in queries, this static method is equal to "&lt;=" operator.</summary>
		/// <remarks>You need that if you want to query by string column, or binary column, or some other column type that doesn't have comparison operators in the .NET type mapped to that column.</remarks>
		public static bool lessOrEqual( object a, object b )
		{
			return false;
		}
		/// <summary>When encountered in queries, this static method is equal to "&gt;=" operator.</summary>
		/// <remarks>You need that if you want to query by string column, or binary column, or some other column type that doesn't have comparison operators in the .NET type mapped to that column.</remarks>
		public static bool greaterOrEqual( object a, object b )
		{
			return false;
		}

		/// <summary>When encountered in queries, this static method checks the value's present in a multivalued column.</summary>
		/// <remarks>You need that if you want to query by multi-valued column that's mapped to array CLR type. For generic List this also work, but for them you can also use List.Contains method.</remarks>
		public static bool Contains( IEnumerable arr, object o )
		{
			return false;
		}
	}
}