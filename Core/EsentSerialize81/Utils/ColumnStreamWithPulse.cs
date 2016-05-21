using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization
{
	/// <summary>This class enhances the Managed ESENT's ColumnStream class by pulsing a transaction while writing huge values to the database.</summary>
	class ColumnStreamWithPulse : ColumnStream
	{
		readonly iSerializerSession session;

		/// <summary>Construct the object</summary>
		/// <param name="cur">Cursor</param>
		/// <param name="idColumn">Column ID</param>
		public ColumnStreamWithPulse( EseCursorBase cur, JET_COLUMNID idColumn ) : base( cur.idSession, cur.idTable, idColumn )
		{
			this.session = cur.session;
		}

		int totalCount = 0;
		const int pulseFrequency = 4 * 1024 * 1024; // 4MB

		/// <summary>Write a sequence of bytes to the ESENT column.</summary>
		/// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the ESENT column.</param>
		/// <param name="offset">zero-based byte offset in buffer at which to begin copying bytes to the ESENT column</param>
		/// <param name="count">number of bytes to be written to the ESENT column.</param>
		public override void Write( byte[] buffer, int offset, int count )
		{
			base.Write( buffer, offset, count );
			totalCount += count;
			if( totalCount < pulseFrequency )
				return;
			totalCount = 0;

			var trans = session.transaction;
			if( null != trans )
				trans.LazyCommitAndReopen();
		}
	}
}