using System;
using Microsoft.Isam.Esent.Interop;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace EsentSerialization
{
	/// <summary>This class represents a subset of records from the <see cref="Cursor{tRow}" />.</summary>
	/// <remarks>
	/// <para>Optionally, the row order is flipped (not every filter supports reverse order, however).</para>
	/// <para>Theoretically, this might be the right place to implement <see cref="System.Linq.IQueryable" /> interface.<br />
	/// Practically however, all my project based on ESENT can do great with only a subset of querying functionality implemented below, so I have very little motivation to do that.<br />
	/// Besides, the indices are assigned manually by user, so only simple skip/take/count methods can be implemented generically.</para>
	/// <para>Only queries that benefit from indexing are implemented in this class.<br />
	/// For the rest of the query/filter/search/sort/etc functionality, use LINQ on the IEnumerable returned by <see cref="all" /> or <see cref="uniq" /> methods.</para>
	/// <para><b>NB!</b> Navigation limit set by filter is volatile. It will be canceled if any navigation other than JetMove is performed on the cursor.<br />
	/// On the other hand, the filter is re-applied frequently, e.g. on each 'all()' call.</para>
	/// </remarks>
	/// <typeparam name="tRow">Type of the records stored in the underlying ESENT table (a type marked with <see cref="Attributes.EseTableAttribute">[EseTable]</see> attribute).</typeparam>
	public class Recordset<tRow> : IDisposable where tRow : new()
	{
		Cursor<tRow> m_cursor;

		/// <summary>ESENT session ID</summary>
		public JET_SESID idSession { get { return m_cursor.idSession; } }
		/// <summary>ESENT table ID</summary>
		public JET_TABLEID idTable { get { return m_cursor.idTable; } }
		/// <summary>The table cursor</summary>
		public Cursor<tRow> cursor { get { return m_cursor; } }
		/// <summary>EsentSerialization session</summary>
		public iSerializerSession session { get { return m_cursor.session; } }

		// The filter.
		iRecordsetFilter m_filter;

		/// <summary>Construct from the cursor</summary>
		public Recordset( Cursor<tRow> _cursor )
		{
			m_cursor = _cursor;
			filterClear();
		}

		/// <summary>Dispose the object.</summary>
		/// <remarks><b>NB!</b> If you've ever called <see cref="CreateOwnCursor"/>, you _must_ properly dispose the recordset.<br />
		/// If you didn't call CreateOwnCursor(), then this recordset will contain an instance of the cursor owned by the session; disposing of sessioon-owned cursor does absolutely nothing.</remarks>
		public void Dispose()
		{
			if( m_cursor != null )
			{
				m_cursor.Dispose();
				m_cursor = null;
			}
		}

		bool tryMoveFirstLast( bool bFirst )
		{
			if( bFirst )
				return m_cursor.TryMoveFirst();
			return m_cursor.TryMoveLast();
		}

		/// <summary>Move the cursor to the first record.</summary>
		/// <returns>False if the recordset is empty.</returns>
		/// <remarks><b>NB:</b> this method ignores the navigation limits set by "filter*" methods.</remarks>
		public bool tryMoveFirst()
		{
			return tryMoveFirstLast( !m_filter.bInverse );
		}

		/// <summary>Move the cursor to the last record.</summary>
		/// <returns>False if the recordset is empty.</returns>
		/// <remarks><b>NB:</b> this method ignores the navigation limits set by "filter*" methods.</remarks>
		public bool tryMoveLast()
		{
			return tryMoveFirstLast( m_filter.bInverse );
		}

		bool tryMoveNextPrevious( bool bNext )
		{
			if( bNext )
				return m_cursor.tryMoveNext();
			return m_cursor.tryMovePrevious();
		}

		/// <summary>Move the cursor to the next record.</summary>
		public bool tryMoveNext()
		{
			return tryMoveNextPrevious( !m_filter.bInverse );
		}

		/// <summary>Move the cursor to the previous record.</summary>
		public bool tryMovePrevious()
		{
			return tryMoveNextPrevious( m_filter.bInverse );
		}

		/// <summary>Apply the search filter, and goto the bookmark</summary>
		/// <param name="bk"></param>
		/// <returns></returns>
		public bool tryGotoBookmark( byte[] bk )
		{
			return m_filter.ApplyFilter( bk );
		}

		#region Filters

		/// <summary>Clear any filter, i.e. set the sort order to the non-reversed primary index,
		/// and cancel navigation limitations.</summary>
		/// <remarks>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</remarks>
		public void filterClear()
		{
			m_filter = new FilterAllRecords( cursor );
		}

		/// <summary>Set the sort order to the primary index, cancel navigation limitations, and optionally reverse the records.</summary>
		/// <param name="bReverseOrder">set to true to make the recordset to return the results in the reversed order, i.e. from last record to the first record.</param>
		/// <remarks>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</remarks>
		public void filterClear( bool bReverseOrder )
		{
			filterClear();
			m_filter.bInverse = bReverseOrder;
		}

		/// <summary>Set the sort order to the specified index, and optionally reverse the records.</summary>
		/// <param name="indName">Name of the index, as supplied to the <see cref="Attributes.EseIndexAttribute">[EseIndex]</see> attribute constructor.</param>
		/// <param name="bReverseOrder">set to true to make the recordset to return the results in the reversed order, i.e. from last record to the first record.</param>
		/// <remarks>
		/// <para>For example, if you have a <see cref="Attributes.EseTableAttribute">table</see> with 2 integer columns "c1" and "c2",
		/// the table has an <see cref="Attributes.EseIndexAttribute">index</see> "ind" saying "+c1\0+c2\0\0", 
		/// then calling
		/// <code>
		/// rs.filterSort( "ind", false );
		/// return rs.all();
		/// </code>
		/// will return the same sequence of records as
		/// <code>
		/// rs.filterClear();
		/// return rs.all().OrderBy( r =&gt; r.c1 ).ThenBy( r =&gt; r.c2 );
		/// </code>
		/// </para>
		/// <para>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</para>
		/// </remarks>
		public void filterSort( string indName, bool bReverseOrder )
		{
			m_filter = new FilterAllRecords( cursor, indName );
			m_filter.bInverse = bReverseOrder;
		}

		/// <summary>Include the records
		/// where the indexed columns values are exactly the same as the supplied arguments.</summary>
		/// <param name="indName">Name of the index, as supplied to the <see cref="Attributes.EseIndexAttribute">[EseIndex]</see> attribute constructor.</param>
		/// <param name="vals">Values of the indexed columns, in the same order they go in the index.</param>
		/// <remarks>
		/// <para>For example, if you have a <see cref="Attributes.EseTableAttribute">table</see> with 2 integer columns "c1" and "c2",
		/// the table has an <see cref="Attributes.EseIndexAttribute">index</see> "ind" saying "+c1\0+c2\0\0", 
		/// then calling
		/// <code>
		/// rs.filterFindEqual( "ind", 13, 254 );
		/// return rs.all();
		/// </code>
		/// will return the same sequence of records as
		/// <code>
		/// rs.filterClear();
		/// return rs.all().Where( r =&gt; r.c1 == 13 &amp;&amp; r.c2 == 254 );
		/// </code>
		/// </para>
		/// <para>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</para>
		/// </remarks>
		public void filterFindEqual( string indName, params object[] vals )
		{
			m_filter = new SearchFilterEqual( cursor, indName, vals );
		}

		/// <summary>Include the records,
		/// where the indexed columns values are exactly the same as the supplied arguments. The output is reversed.</summary>
		/// <param name="indName">Name of the index, as supplied to the <see cref="Attributes.EseIndexAttribute">[EseIndex]</see> attribute constructor.</param>
		/// <param name="vals">Values of the indexed columns, in the same order they go in the index.</param>
		/// <remarks>
		/// <para><b>NB:</b> this filter doesn't support bookmarks-based navigation,
		/// so the <see cref="tryGotoBookmark" /> will throw.</para>
		/// <para>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</para>
		/// </remarks>
		public void filterFindEqualInv( string indName, params object[] vals )
		{
			m_filter = new SearchFilterEqualInv( cursor, indName, vals );
		}

		/// <summary>Filter the recordset only including the records listed on the specified index,
		/// where the indexed columns values are the same as the supplied arguments.
		/// The last supplied argument however is threated like it contains the wildcard at the end.</summary>
		/// <param name="indName">Name of the index, as supplied to the <see cref="Attributes.EseIndexAttribute">[EseIndex]</see> attribute constructor.</param>
		/// <param name="vals">Values of the indexed columns, in the same order they go in the index.</param>
		/// <remarks>
		/// <para><b>NB:</b> this filter doesn't support bookmarks-based navigation,
		/// so the <see cref="tryGotoBookmark" /> will throw.</para>
		/// <para>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</para>
		/// </remarks>
		public void filterFindSubstring( string indName, params object[] vals )
		{
			m_filter = new SearchFilterSubstring( cursor, indName, vals );
		}

		/// <summary>Filter the recordset only including the records listed on the specified index,
		/// where the indexed columns values are between the specified values.</summary>
		/// <param name="indName">Name of the index, as supplied to the <see cref="Attributes.EseIndexAttribute">[EseIndex]</see> attribute constructor.</param>
		/// <param name="valsStart">Starting values of the indexed columns, in the same order they go in the index.</param>
		/// <param name="valsEnd">Ending values of the indexed columns.</param>
		/// <remarks>
		/// <para>For example, if you have a <see cref="Attributes.EseTableAttribute">table</see> with 2 integer columns "c1" and "c2",
		/// the table has an <see cref="Attributes.EseIndexAttribute">index</see> "ind" saying "+c1\0+c2\0\0", 
		/// then calling
		/// <code>
		/// rs.filterFindBetween( "ind", new object[ 1 ] { 50 }, new object[ 2 ] { 70, 100 } );
		/// return rs.all();
		/// </code>
		/// will return the same sequence of records as
		/// <code><![CDATA[
		/// rs.filterClear();
		/// return rs.all()
		/// 	.Where( r => r.c1 >= 50 )                                   // valsStart condition
		/// 	.Where( r => r.c1 < 70 || ( r.c1 == 70 && r.c2 <= 100 ) )   // valsEnd condition
		/// 	.OrderBy( r => r.c1 ).ThenBy( r => r.c2 );                  // Index order
		/// ]]></code>
		/// </para>
		/// <para>If you pass null as valsStart (or valsEnd),
		/// the result will be all records in the index from the beginning to the valsEnd (or from valsStart to the end of the index).
		/// This means calling
		/// <code>
		/// rs.filterFindBetween( "ind", new object[ 2 ] { 500, 900 }, null );
		/// return rs.all();
		/// </code>
		/// will return the same sequence of records as
		/// <code><![CDATA[
		/// rs.filterClear();
		/// return rs.all()
		/// 	.Where( r => ( r.c1 > 500 || ( r.c1 == 500 && r.c2 >= 900 ) ) )    // valsStart condition
		/// 	.OrderBy( r => r.c1 ).ThenBy( r => r.c2 );                         // Index order
		/// ]]></code>
		/// </para>
		/// <para>This function does not immediately affect the cursor;
		/// instead, it changes the result of e.g.
		/// <see cref="applyFilter"/>, <see cref="all"/>, and <see cref="getFirst"/> methods.</para></remarks>
		public void filterFindBetween( string indName, object[] valsStart, object[] valsEnd )
		{
			m_filter = new SearchFilterBetween( cursor, indName, valsStart, valsEnd );
		}

		/// <summary>Same as <see cref="filterFindBetween"/>, but the order is flipped, i.e. records are returned from end to start.</summary>
		public void filterFindBetweenInv( string indName, object[] valsStart, object[] valsEnd )
		{
			m_filter = new SearchFilterBetweenInv( cursor, indName, valsStart, valsEnd );
		}

		#endregion

		// Return empty IEnumerable
		static IEnumerable<tRow> nothing()
		{
			yield break;
		}

		/// <summary>Get the enumerator of the records rows.</summary>
		/// <remarks>
		/// <para>For best performance, it's recommended to execute this method within a transaction.</para>
		/// <para>Remember: by default, all recordset of given table within same session share the cursor state:
		/// current index, position, and navigation limits.</para>
		/// <para>So you have 2 options how to handle the result of this function:</para>
		/// <list type="number" >
		/// <item><description>Immediately fetch the whole result, e.g. call Enumerable.ToList().<br />
		/// While fetching the result, don't attempt to use the same table.<br />
		/// Currently, this race condition is not checked, so you'll see no exceptions, no asserts, just faulty recordset (missing records and/or duplicates).</description></item>
		/// <item><description>If you do need lazy evaluations for a good reason (your table is long and/or your objects are huge),
		/// then please make sure the recordset is cursor-owning, and don't forget to properly dispose the recordset
		/// after you no longer need it.</description></item>
		/// </list></remarks>
		/// <seealso cref="CreateOwnCursor"/>
		/// <returns>Rows enumerator</returns>
		public IEnumerable<tRow> all()
		{
			if( !m_filter.ApplyFilter() )
				return nothing();

			return currentRange();
		}

		/// <remarks><para>You should use this method instead of <see cref="all" /> when you're using tuple indices,
		/// or when you're using an index over a multi-valued column:
		/// they both may contain a duplicate index entry for the same record.</para>
		/// This method is much faster then unduplicating using LINQ: skipped rows are not even fetched from the DB.<br />
		/// See also the note on laziness in the <see cref="all" /> method documentation.</remarks>
		/// <returns>Rows enumerator</returns>
		public IEnumerable<tRow> uniq()
		{
			if( !m_filter.ApplyFilter() )
				return nothing();
			return currentRangeNoDuplicates();
		}

		/// <summary>Same as <see cref="all" /> but only return the subset of columns in each record.</summary>
		public IEnumerable<tRow> fetchFields( params string[] fields )
		{
			if( !m_filter.ApplyFilter() )
				yield break;
			tRow item = new tRow();
			do
			{
				m_cursor.FetchFields( item, fields );
				yield return item;
			}
			while( tryMoveNext() );
		}

		/// <summary>Apply the filter, and move the cursor to the first matching record.</summary>
		/// <returns>True if there's at least 1 matching record</returns>
		public bool isEmpty()
		{
			return !applyFilter();
		}

		/// <summary>Apply the filter, and move the cursor to the first matching record.</summary>
		/// <remarks>This method is called pretty often, e.g. from the <see cref="all" /> method.</remarks>
		/// <returns>Return false if there's no matching records.</returns>
		public bool applyFilter()
		{
			return m_filter.ApplyFilter();
		}

		/// <summary>Count the records from the current cursor position to the end of the current range.</summary>
		/// <remarks>
		/// <para>The current position is included in the count.</para>
		/// <para>If the direction of the currently applied filter is forward, execution time is much faster.</para>
		/// </remarks>
		/// <returns>Records count</returns>
		public int CountToEnd()
		{
			int res;
			if( m_filter.bInverse )
				for( res = 1; m_cursor.tryMovePrevious(); res++ ) ;
			else
				Api.JetIndexRecordCount( idSession, idTable, out res, 0 );
			return res;
		}

		/// <summary>Apply filter, then count the records.</summary>
		/// <remarks>
		/// <para>The execution time is proportional to the count of matched records.</para>
		/// <para>If the direction of the currently applied filter is forward, execution time is much faster.</para>
		/// </remarks>
		/// <returns>Records count.</returns>
		public int Count()
		{
			if( !applyFilter() )
				return 0;
			return CountToEnd();
		}

		/// <summary>Apply filter, then count unique records.</summary>
		/// <remarks>The execution time is proportional to the count of matched records.</remarks>
		public int CountUniq()
		{
			if( !applyFilter() )
				return 0;
			int res = 0;
			HashSet<byte[]> hashReturnedBookmarks = new HashSet<byte[]>( ByteArray.EqualityComparer );
			do
			{
				byte[] bmCurr = m_cursor.getBookmark();
				if( hashReturnedBookmarks.Contains( bmCurr ) )
					continue;
				res++;
				hashReturnedBookmarks.Add( bmCurr );
			}
			while( tryMoveNext() );
			return res;
		}

		/// <summary>Return all records, starting from the current position, 
		/// to the currently set navigation limits (or to the end of the index if there's no limits).</summary>
		/// <returns></returns>
		IEnumerable<tRow> currentRange()
		{
			do
			{
				yield return m_cursor.getCurrent();
			}
			while( tryMoveNext() );
		}

		/// <summary>Return all records from the current position 
		/// to the currently set navigation limits. No record is returned more then once.</summary>
		/// <remarks>This method is especially useful for tuple indices, or for indices over a multi-valued column.
		/// This method consumes memory proportionally to the returned records count.</remarks>
		/// <returns></returns>
		IEnumerable<tRow> currentRangeNoDuplicates()
		{
			return ForEachUniq( delegate () { return m_cursor.getCurrent(); } );
		}

		/// <summary>For every unique record within the filtered range, execute the selector functor, and return the value.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="selector"></param>
		/// <returns></returns>
		IEnumerable<T> ForEachUniq<T>( Func<T> selector )
		{
			HashSet<byte[]> hashReturnedBookmarks = new HashSet<byte[]>( ByteArray.EqualityComparer );
			do
			{
				byte[] bmCurr = m_cursor.getBookmark();
				if( hashReturnedBookmarks.Contains( bmCurr ) )
					continue;
				hashReturnedBookmarks.Add( bmCurr );
				yield return selector();
			}
			while( tryMoveNext() );
		}

		/// <summary>Get the bookmark of the first record in the recordset,
		/// or ByteArray.Empty when there's no records matching the filter.</summary>
		/// <returns></returns>
		public byte[] getFirstBookmark()
		{
			if( !applyFilter() ) return ByteArray.Empty;
			return cursor.getBookmark();
		}

		/// <summary>Return the first value in the recordset,
		/// or default( tRow ) when there's no records matching the filter.</summary>
		/// <remarks>After this method returned non-null, the cursor will be positioned on the returned item.</remarks>
		/// <returns></returns>
		public tRow getFirst()
		{
			if( !applyFilter() ) return default( tRow );
			return cursor.getCurrent();
		}

		/// <summary>Return true if the EstimateRecordsCount is supported.</summary>
		public bool canEstimateRecordsCount { get { return m_filter.canEstimateRecordsCount; } }

		/// <summary>Roughly estimate the count of the records matching the currently set filter.</summary>
		/// <remarks>Not every search filter type supports this feature, be ready for NotSupportedException.</remarks>
		public int EstimateRecordsCount() { return m_filter.EstimateRecordsCount(); }

		/// <summary>Erase all the items that match the search filter.</summary>
		/// <remarks>Don't execute this for recordsets with huge number of filtered items: the deletion is _not_ divided across several transactions.</remarks>
		/// <returns>Count of the erased records.</returns>
		public int EraseAll()
		{
			if( !m_filter.isFiltered )
				return m_cursor.RemoveAll();

			if( !applyFilter() )
				return 0;

			int res = 0;
			return ForEachUniq( delegate () { m_cursor.delCurrent(); res++; return res; } )
				.LastOrDefault();
		}

#if !NETFX_CORE
		/// <summary>Duplicate the ESENT cursor to get the own copy for this recordset.</summary>
		/// <remarks>This is required e.g. when you're fetching records from the recordset, and process them in a way that requires another query to the same table.<br/>
		/// This operation also clears the search/sort filter.<br/>
		/// <b>NB!</b> After you've called this method, you must dispose the cursor. Here's the recommended usage:
		/// <code>            rsAux.CreateOwnCursor();
		///using( rsAux )
		///{
		///	// Use rsAux to query the table.
		///}</code>
		///</remarks>
		public void CreateOwnCursor()
		{
			Debug.Assert( m_cursor != null );
			m_cursor = m_cursor.CreateOwnCopy();

			// Filter object has cached cursor reference, that's why we reconstruct the filter.
			filterClear( m_filter.bInverse );
		}

		/// <summary>The function computes the intersection between multiple sets of index entries
		/// from different secondary indices over the same table.</summary>
		/// <remarks><para>This operation is useful for finding the set of records in a table that match two or more criteria
		/// that can be expressed using filterFind*-family methods of the Recordset&lt;&gt; generic class.</para>
		/// <para>All recordsets must be with non-empty filter set (i.e. you have to call a filterFind* method on each of them).</para>
		/// <para>All recordsets must be from the same session.</para>
		/// <para>All recordsets must have different cursors. See the <see cref="CreateOwnCursor"/> method that creates own copy of the cursor.
		/// Don't forget to properly dispose the recordsets afterwards.</para>
		/// <para>This method uses the cursor of the first passed recordset to navigate through the result set.</para>
		/// </remarks>
		/// <param name="recordsets">The recordsets to intersect.</param>
		/// <returns>The rows that are result of the index intersection.
		/// The returned IEnumerable is lazy, i.e. the records are fetched sequentially, as you pull the result.</returns>
		public static IEnumerable<tRow> IntersectIndices( params Recordset<tRow>[] recordsets )
		{
			if( 0 == recordsets.Length )
				return nothing();
			if( 1 == recordsets.Length )
				return recordsets[ 0 ].all();

			// Verify all recordset have own cursors and are from the same session
			HashSet<JET_TABLEID> tables = new HashSet<JET_TABLEID>();
			JET_SESID idSession = recordsets[0].idSession;
			foreach( var rs in recordsets )
			{
				var id = rs.idTable;
				if( tables.Contains( id ) )
					throw new ArgumentException( "All input recordsets must have own cursors" );
				if( idSession != rs.idSession )
					throw new ArgumentException( "All input recordsets must be within the same session" );
				if( rs.m_filter is FilterAllRecords )
					throw new ArgumentException( "All input recordsets must have some filter set." );

				if( !rs.applyFilter() )
					return nothing();
				tables.Add( id );
			}
			tables.Clear();
			tables = null;

			JET_TABLEID[] tableIds = recordsets
				.Select( rs => rs.idTable )
				.ToArray();

			recordsets[ 0 ].cursor.ResetIndex();
			Cursor<tRow> cur = recordsets[ 0 ].cursor;
			return Api.IntersectIndexes( idSession, tableIds )
				.Select( bk =>
				{
					cur.gotoBookmark( bk );
					return cur.getCurrent();
				} );
		}
#endif
	}
}