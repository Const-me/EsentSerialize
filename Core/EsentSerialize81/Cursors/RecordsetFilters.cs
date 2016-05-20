// Comment out the following line if you don't want the search filter verify the value with the provided bookmark falls in the search range.
// Doing so will improve performance a bit, for the price of removing some arguments checks.
#define bVerifyBookmarkIsInRange

using System;
using Microsoft.Isam.Esent.Interop;
using System.Linq;
using EsentSerialization.Attributes;

namespace EsentSerialization
{
	// Interface for recordset filter
	interface iRecordsetFilter
	{
		// Apply the filter, goto the 1-st record.
		// Return false if no matching records were found.
		bool ApplyFilter();

		// Apply the filter, goto the bookmarked record.
		// Returns false if no matching records were found, or if record with the bookmark provided is out of range.
		bool ApplyFilter( byte[] bk );

		bool bInverse { get; set; }

		bool canEstimateRecordsCount { get; }

		int EstimateRecordsCount();

		bool isFiltered { get; }
	}

	// Filter that doesn't filter anything, just orders the item as specified in the index.
	// It supports order flipping.
	class FilterAllRecords : iRecordsetFilter
	{
		protected readonly EseCursorBase cur;

		bool m_bInverse = false;
		public bool bInverse { get { return m_bInverse; } set { m_bInverse = value; } }

		protected readonly string indName;

		// Construct as "All records in the clustered index"
		public FilterAllRecords( EseCursorBase _cur )
		{
			cur = _cur;
			indName = null;
		}

		// Construct as "All records in a secondary index"
		public FilterAllRecords( EseCursorBase _cur, string _index )
		{
			cur = _cur;
			indName = _index;
		}

		bool iRecordsetFilter.ApplyFilter()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );
			Api.ResetIndexRange( cur.idSession, cur.idTable );
			if( !m_bInverse )
				return cur.TryMoveFirst();
			return cur.TryMoveLast();
		}

		bool iRecordsetFilter.ApplyFilter( byte[] bk )
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );
			Api.ResetIndexRange( cur.idSession, cur.idTable );
			return cur.tryGotoBookmark( bk );
		}

		bool iRecordsetFilter.canEstimateRecordsCount { get { return true; } }

		// http://blogs.msdn.com/laurionb/archive/2009/02/10/cheaply-estimating-the-number-of-records-in-a-table.aspx
		// http://webcache.googleusercontent.com/search?q=cache:budnenpY5RUJ:blogs.msdn.com/b/laurionb/archive/2009/02/10/cheaply-estimating-the-number-of-records-in-a-table.aspx+http://blogs.msdn.com/laurionb/archive/2009/02/10/cheaply-estimating-the-number-of-records-in-a-table.aspx&hl=en&strip=1
		int iRecordsetFilter.EstimateRecordsCount()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );
			Api.ResetIndexRange( cur.idSession, cur.idTable );

			const int cSamples = 16;

			long cRecordsTotal = 0;
			JET_RECPOS recpos = new JET_RECPOS();
			for( int i = 0; i < cSamples; i++ )
			{
				recpos.centriesTotal = cSamples + 1;
				recpos.centriesLT = i + 1;

				Api.JetGotoPosition( cur.idSession, cur.idTable, recpos );
				Api.JetGetRecordPosition( cur.idSession, cur.idTable, out recpos );
				cRecordsTotal += recpos.centriesTotal;
			}

			return (int)( cRecordsTotal / cSamples );
		}

		public bool isFiltered { get { return false; } }
	}

	// An abstract base class for search filter.
	abstract class SearchFilterBase
	{
		// Cursor
		protected readonly EseCursorBase cur;

		// Index name
		protected readonly string indName;

		// Array of the column attributes
		protected readonly EseColumnAttrubuteBase[] indColumns;

		// Order flipping is _not_ supported.
		public bool bInverse { get { return false; } set { if( false == value ) return; throw new NotSupportedException(); } }

		protected SearchFilterBase( EseCursorBase _cur, string _index )
		{
			cur = _cur;
			indName = _index;
			if( String.IsNullOrEmpty( indName ) )
				throw new ArgumentException( "No index is specified. " +
					"To cancel filtering, use Recordset.filterClear() method instead." );

			indColumns = cur.serializer.getIndexedColumns( indName );
		}

		// Make the search key.
		// The first MakeKey call is made with MakeKeyGrbit.NewKey flag.
		// The final MakeKey call is made with the 'lastColumnFlags' flag.
		// The rest of MakeKey calls (if any) is made with zero flag.
		protected void makeKey( object[] vals, MakeKeyGrbit lastColumnFlags )
		{
			System.Diagnostics.Debug.Assert( indColumns.Length >= vals.Length );
			for( int i = 0; i < vals.Length; i++ )
			{
				MakeKeyGrbit flags = MakeKeyGrbit.None;
				if( 0 == i ) flags |= MakeKeyGrbit.NewKey;
				if( i + 1 == vals.Length ) flags |= lastColumnFlags;
				indColumns[ i ].MakeKey( cur, vals[ i ], flags );
			}
		}

		// Make search key from the input values, return the normalized search key.
		protected byte[] getSearchKey( object[] vals, MakeKeyGrbit lastColumnFlag )
		{
			makeKey( vals, lastColumnFlag );
			return Api.RetrieveKey( cur.idSession, cur.idTable, RetrieveKeyGrbit.RetrieveCopy );
		}

		public bool canEstimateRecordsCount { get { return false; } }

		public bool isFiltered { get { return true; } }
	}

	// Search filter to find the records where the first column[s] value exactly match the search criteria.
	class SearchFilterEqual : SearchFilterBase, iRecordsetFilter
	{
		protected object[] vals { get; private set; }

		public SearchFilterEqual( EseCursorBase _cur, string _index, params object[] _vals )
			: base( _cur, _index )
		{
			// Handle the null issue this way
			if( null == _vals )
				vals = new object[ 1 ] { null };
			else
				vals = _vals;

			if( vals.Length > indColumns.Length )
				throw new IndexOutOfRangeException( "Index '" + indName + "' does not contain that many columns." );
		}

		// Get the correct flags for the JetMakeKey call.
		// Returns either 0, FullColumnStartLimit or FullColumnEndLimit.
		protected virtual MakeKeyGrbit getMakeKeyFlags( bool bIsStart )
		{
			if( vals.Length == indColumns.Length ) return MakeKeyGrbit.None;
			if( bIsStart ) return MakeKeyGrbit.FullColumnStartLimit;
			return MakeKeyGrbit.FullColumnEndLimit;
		}

		// Make the search key.
		protected void makeKey( bool bIsStart )
		{
			makeKey( vals, getMakeKeyFlags( bIsStart ) );
		}

		bool iRecordsetFilter.ApplyFilter()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			makeKey( true );
			if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekGE ) ) return false;

			makeKey( false );
			return Api.TrySetIndexRange( cur.idSession, cur.idTable,
				SetIndexRangeGrbit.RangeUpperLimit | SetIndexRangeGrbit.RangeInclusive );
		}

#if bVerifyBookmarkIsInRange
		bool iRecordsetFilter.ApplyFilter( byte[] bk )
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			byte[] keyStartIndex = getSearchKey( vals, getMakeKeyFlags( true ) );

			if( !cur.tryGotoBookmark( bk ) ) return false;
			var keyProvidedBookmark = cur.getSearchKey();
			if( keyProvidedBookmark.CompareTo( keyStartIndex ) < 0 )
				return false; // The provided bookmark is out of range

			byte[] keyEndIndex = getSearchKey( vals, getMakeKeyFlags( false ) );
			if( keyProvidedBookmark.CompareTo( keyEndIndex ) > 0 )
				return false; // The provided bookmark is out of range

			return Api.TrySetIndexRange( cur.idSession, cur.idTable,
				SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit );
		}
#else
		bool iRecordsetFilter.ApplyFilter( ByteArray bk )
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			if( !cur.tryGotoBookmark( bk ) ) return false;

			makeKey( vals, getMakeKeyFlags( false ) );
			return Api.TrySetIndexRange( cur.idSession, cur.idTable, SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit );
		}
#endif

		int iRecordsetFilter.EstimateRecordsCount()
		{
			throw new NotImplementedException();

			// The code below doesn't work at all. It seems rpBegin and rpEnd contain random data for multi-column secondary indices.

			/* Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			const int cSamples = 16;
			LeastSquareCalculator lsq = new LeastSquareCalculator();

			// Goto the 1-st point
			makeKey( true );
			if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekGE ) )
				return 0;

			JET_RECPOS rpBegin = new JET_RECPOS();
			Api.JetGetRecordPosition( cur.idSession, cur.idTable, out rpBegin );
			lsq.AddDataPoint( 0, rpBegin.centriesLT );

			// Goto the last point
			makeKey( false );
			if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekLE ) )
				return 0;

			JET_RECPOS rpEnd = new JET_RECPOS();
			Api.JetGetRecordPosition( cur.idSession, cur.idTable, out rpEnd );
			lsq.AddDataPoint( cSamples, rpEnd.centriesLT );

			// Goto the middle points
			JET_RECPOS recpos = new JET_RECPOS();
			for( long i = 1; i < cSamples; i++ )
			{
				recpos.centriesTotal = ( rpEnd.centriesTotal * i + rpBegin.centriesTotal * ( cSamples - i ) ) / cSamples;
				recpos.centriesLT =    (  rpEnd.centriesLT   * i +  rpBegin.centriesLT   * ( cSamples - i ) ) / cSamples;

				Api.JetGotoPosition( cur.idSession, cur.idTable, recpos );
				Api.JetGetRecordPosition( cur.idSession, cur.idTable, out recpos );
				lsq.AddDataPoint( i, recpos.centriesLT );
			}

			int res = (int)Math.Round( lsq.GetLsqValueAt( cSamples ) - lsq.GetLsqValueAt( 0 ) );

			return res; */
		}
	}

	class SearchFilterEqualInv : SearchFilterEqual, iRecordsetFilter
	{
		public SearchFilterEqualInv( EseCursorBase _cur, string _index, params object[] _vals )
			: base( _cur, _index, _vals ) { }

		bool iRecordsetFilter.bInverse
		{
			get { return true; }
			set
			{
				if( value != true )
					throw new NotSupportedException( "SearchFilterEqualInv is always reverted." );
			}
		}

		bool iRecordsetFilter.ApplyFilter()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );
			makeKey( false );
			if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekLE ) ) return false;

			makeKey( true );
			return Api.TrySetIndexRange( cur.idSession, cur.idTable,
				SetIndexRangeGrbit.RangeInclusive );
		}

		bool iRecordsetFilter.ApplyFilter( byte[] bk )
		{
			throw new NotSupportedException( "SearchFilterEqualInv doesn't support bookmarks navigation." );
		}
	}

	class SearchFilterBetween : SearchFilterBase, iRecordsetFilter
	{
		protected readonly object[] valsStart, valsEnd;
		protected readonly int lenStart, lenEnd;

		public SearchFilterBetween( EseCursorBase _cur, string _index, object[] _start, object[] _end )
			: base( _cur, _index )
		{
			valsStart = _start;
			valsEnd = _end;

			lenStart = ( null == valsStart ) ? 0 : valsStart.Length;
			lenEnd = ( null == valsEnd ) ? 0 : valsEnd.Length;

			if( lenStart > indColumns.Length || lenEnd > indColumns.Length )
				throw new IndexOutOfRangeException( "Index '" + indName + "' does not contain that many columns." );
		}

		bool iRecordsetFilter.ApplyFilter()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			MakeKeyGrbit mkb;
			if( 0 == lenStart )
			{
				if( !cur.TryMoveFirst() )
					return false;
			}
			else
			{
				mkb = ( lenStart == indColumns.Length ) ? MakeKeyGrbit.None : MakeKeyGrbit.FullColumnStartLimit;
				makeKey( valsStart, mkb );
				if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekGE ) )
					return false;
			}

			if( 0 == lenEnd )
				return true;

			mkb = ( lenEnd == indColumns.Length ) ? MakeKeyGrbit.None : MakeKeyGrbit.FullColumnEndLimit;
			makeKey( valsEnd, mkb );
			return Api.TrySetIndexRange( cur.idSession, cur.idTable,
				SetIndexRangeGrbit.RangeUpperLimit | SetIndexRangeGrbit.RangeInclusive );
		}

		bool iRecordsetFilter.ApplyFilter( byte[] bk )
		{
			throw new NotImplementedException();
		}

		int iRecordsetFilter.EstimateRecordsCount()
		{
			throw new NotImplementedException();
		}
	}

	class SearchFilterBetweenInv : SearchFilterBetween, iRecordsetFilter
	{
		public SearchFilterBetweenInv( EseCursorBase _cur, string _index, object[] _start, object[] _end ) :
			base( _cur, _index, _start, _end )
		{ }

		bool iRecordsetFilter.bInverse
		{
			get { return true; }
			set
			{
				if( value != true )
					throw new NotSupportedException( "SearchFilterBetweenInv is always reverted." );
			}
		}

		bool iRecordsetFilter.ApplyFilter()
		{
			Api.JetSetCurrentIndex( cur.idSession, cur.idTable, indName );

			MakeKeyGrbit mkb;
			if( 0 == lenEnd )
			{
				if( !cur.TryMoveLast() )
					return false;
			}
			else
			{
				mkb = ( lenEnd == indColumns.Length ) ? MakeKeyGrbit.None : MakeKeyGrbit.FullColumnEndLimit;
				makeKey( valsEnd, mkb );
				if( !Api.TrySeek( cur.idSession, cur.idTable, SeekGrbit.SeekLE ) )
					return false;
			}

			if( 0 == lenStart )
				return true;

			mkb = ( lenStart == indColumns.Length ) ? MakeKeyGrbit.None : MakeKeyGrbit.FullColumnStartLimit;
			makeKey( valsStart, mkb );
			return Api.TrySetIndexRange( cur.idSession, cur.idTable, SetIndexRangeGrbit.RangeInclusive );
		}
	}

	// Same as SearchFilterEqual, but treat the last value as the value followed by wildcard
	class SearchFilterSubstring : SearchFilterEqual, iRecordsetFilter
	{
		public SearchFilterSubstring( EseCursorBase _cur, string _index, params object[] _vals )
			: base( _cur, _index, _vals )
		{
			// TODO [low]: only check for the first time this filter is constructed.
			var endColType = indColumns[ vals.Length - 1 ].getColumnDef().coltyp;
			switch( endColType )
			{
				case JET_coltyp.Binary:
				case JET_coltyp.Text:
				case JET_coltyp.LongBinary:
				case JET_coltyp.LongText:
					break;
				default:
					throw new NotSupportedException( "Substring search filter can only be used" +
						" when the key column is a text column or a variable binary column." );
			}
		}

		protected override MakeKeyGrbit getMakeKeyFlags( bool bIsStart )
		{
			if( bIsStart ) return MakeKeyGrbit.PartialColumnStartLimit;
			return MakeKeyGrbit.PartialColumnEndLimit;
		}

		int iRecordsetFilter.EstimateRecordsCount()
		{
			throw new NotImplementedException();
		}
	}
}