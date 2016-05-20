using System.Collections.Generic;
using System.Linq;

namespace EsentSerialization
{
	/// <summary>This class represents a subset of records from the <see cref="Cursor{tRow}" />,
	/// with records kept together with its bookmarks, allowing to modify the records.</summary>
	/// <remarks>The class is pretty much the same as <see cref="Recordset{tRow}" />.
	/// The only difference is it enumerates <see cref="BookmarkedRow{tRow}" /> instead of just tRow,
	/// thus allowing to modify or remove records.</remarks>
	/// <typeparam name="tRow">Record type.</typeparam>
	public class BookmarkedRecordset<tRow>: Recordset<tRow> where tRow : new()
	{
		/// <summary></summary>
		public BookmarkedRecordset( Cursor<tRow> _cursor ) : base( _cursor ) { }

		IEnumerable<BookmarkedRow<tRow>> bookmark( IEnumerable<tRow> col )
		{
			return col.Select( r => new BookmarkedRow<tRow>( cursor, cursor.getBookmark(), r ) );
		}

		/// <summary>Get the enumerator of the records rows.</summary>
		/// <seealso cref="Recordset{tRow}.all"/>
		new public IEnumerable<BookmarkedRow<tRow>> all()
		{
			return bookmark( base.all() );
		}

		/// <summary>Same as all(), but unduplicates the output.</summary>
		/// <seealso cref="Recordset{tRow}.uniq"/>
		new public IEnumerable<BookmarkedRow<tRow>> uniq()
		{
			return bookmark( base.uniq() );
		}

		/// <summary>Return the first value in the recordset,
		/// or null when there's no records matching the filter.</summary>
		new public BookmarkedRow<tRow> getFirst()
		{
			tRow val = base.getFirst();
			if( val.Equals( default( tRow ) ) )
				return null;
			return new BookmarkedRow<tRow>( cursor, cursor.getBookmark(), val );
		}
	}
}