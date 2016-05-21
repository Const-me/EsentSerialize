using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization.Attributes
{
	// This class is not an attribute.
	// However, that's for values stored in the records, so the EsentSerialization.Attributes is already included.

	/// <summary>This class represents the value stored by <see cref="EseBinaryStreamAttribute">[EseBinaryStream]</see>.</summary>
	/// <remarks>
	/// <para>This class stores column ID, row bookmark, and value length.</para>
	/// <para>To access the data, call <see cref="Read"/> or <see cref="Write"/> methods.</para>
	/// </remarks>
	/// <seealso cref="EseBinaryStreamAttribute" />
	public class EseStreamValue
	{
		readonly JET_COLUMNID idColumn;
		readonly byte[] bk;

		/// <summary>Data length in bytes.</summary>
		/// <remarks>This field holds the length at the time this value has been deserialized.
		/// If you've called Open() and modified the stream, you'll need to deserialize the field again to update the length field.</remarks>
		public readonly int length;

		readonly string tableName;

		/// <summary>Construct the object</summary>
		/// <param name="cur"></param>
		/// <param name="_idColumn"></param>
		public EseStreamValue( EseCursorBase cur, JET_COLUMNID _idColumn )
		{
			idColumn = _idColumn;
			bk = cur.getBookmark();
			tableName = cur.tableName;

			// Fetch the length.
			// Copy-pasted from Microsoft.Isam.Esent.Interop.ColumnStream.Length get
			var retinfo = new JET_RETINFO { itagSequence = 1, ibLongValue = 0 };
			Api.JetRetrieveColumn( cur.idSession, cur.idTable, idColumn, null, 0, out length, RetrieveColumnGrbit.RetrieveCopy, retinfo );
		}

		class ReadonlyColumnStream : ColumnStream
		{
			public ReadonlyColumnStream( EseCursorBase cur, JET_COLUMNID col ) :
				base( cur.idSession, cur.idTable, col )
			{ }

			public override bool CanWrite { get { return false; } }

			public override void Write( byte[] buffer, int offset, int count )
			{
				throw new NotSupportedException( "This stream is read-only." );
			}

			public override void SetLength( long value )
			{
				throw new NotSupportedException( "This stream is read-only." );
			}
		}

		class ReadWriteColumnStream : ColumnStreamWithPulse
		{
			Update m_update = null;

			public ReadWriteColumnStream( EseCursorBase cur, JET_COLUMNID col ) :
				base( cur, col )
			{
				m_update = new Update( cur.idSession, cur.idTable, JET_prep.Replace );
			}

			protected override void Dispose( bool bDisposing )
			{
				if( bDisposing && ( null != m_update ) )
				{
					m_update.Save();
					m_update.Dispose();
					m_update = null;
				}

				base.Dispose( bDisposing );
			}
		}

		bool PositionOnTheRecord( EseCursorBase cur )
		{
			string strPassedTable = cur.tableName;
			if( strPassedTable != this.tableName )
				throw new ArgumentException( String.Format(
					"Wrong cursor supplied: expected cursor in table {0}, got table {1}", this.tableName, strPassedTable ) );

			// We try to leave the cursor state (current index, current position, index range limitations).
			// However, we will reset the hell out of the cursor, if this is necessary to get the requested data.
			byte[] bkCurr = null;
			try
			{
				bkCurr = cur.getBookmark();
			}
			catch( EsentNoCurrentRecordException )
			{
				bkCurr = ByteArray.Empty;
			}

			if( bkCurr.isEmpty() || !bkCurr.Equals( bk ) )
				if( !cur.tryGotoBookmark( bk ) )
				{
					cur.ResetIndex();
					if( !cur.tryGotoBookmark( bk ) )
						return false;
				}

			return true;
		}

		/// <summary>Open the stream as read-only.</summary>
		/// <param name="cur">The cursor; it must be on the same table.</param>
		/// <remarks>Calling this method will change DB cursor position, setting it to the row where the record is located.</remarks>
		/// <returns>The read-only ColumnStream object.</returns>
		public ColumnStream Read( EseCursorBase cur )
		{
			if( !PositionOnTheRecord( cur ) )
				return null;
			return new ReadonlyColumnStream( cur, idColumn );
		}

		/// <summary>Open the stream as read-write.</summary>
		/// <param name="cur">The cursor; it must be on the same table.</param>
		/// <returns>The read-write ColumnStream object</returns>
		/// <remarks>
		/// <para>Calling this method will change DB cursor position, setting it to the row where the record is located.</para>
		/// <para><b>NB!</b> You must call Dispose() on the returned stream.
		/// Failing to do that will cancel the update and leak the unmanaged resources.</para>
		/// </remarks>
		public ColumnStream Write( EseCursorBase cur )
		{
			if( !PositionOnTheRecord( cur ) )
				return null;
			return new ReadWriteColumnStream( cur, idColumn );
		}
	}
}