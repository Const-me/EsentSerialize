using EsentSerialization.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EsentSerialization
{
	/// <summary>This static class implements some LINQ-like operations on ESENT recordsets.</summary>
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

		/// <summary>Compile query to filter the table by index, with singe query parameter.</summary>
		/// <seealso cref="where" />
		public static SearchQuery<tRow> filter<tRow, tArg1>( iTypeSerializer ser, Expression<Func<tRow, tArg1, bool>> exp ) where tRow : new()
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