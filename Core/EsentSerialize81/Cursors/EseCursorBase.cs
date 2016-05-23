using Microsoft.Isam.Esent.Interop;
using System;
using System.Text;
using System.Diagnostics;

namespace EsentSerialization
{
	/// <summary>This abstract class represents opened table cursor.</summary>
	/// <remarks>This class implements the properties &amp;methods that don't depend on the record type.<br />
	/// <b>NB!</b> Several cursors of the same table on the same session share the underlying ESENT cursor object,
	/// thus they share cursor position, current index, index range limitations, transaction state, everything.
	/// This is done to avoid JET_paramMaxCursors limitation (=1024 by default) even for a heavily loaded highly concurrent application.
	/// This also brings some side effects, of course. That's why you can always create a separate copy of the cursor, when you need that.</remarks>
	public abstract class EseCursorBase : IDisposable
	{
		readonly iSerializerSession m_session = null;
		/// <summary>The session</summary>
		public iSerializerSession session { get { return m_session; } }
		/// <summary>Session ID</summary>
		public JET_SESID idSession { get { return m_session.idSession; } }

		JET_TABLEID m_idTable = JET_TABLEID.Nil;
		/// <summary>Table ID</summary>
		public JET_TABLEID idTable { get { return m_idTable; } }

		bool m_bOwnsTable = false;
		/// <summary>Is true for table-owning cursors (that needs to be disposed).</summary>
		public bool bOwnsTable { get { return m_bOwnsTable; } }

		bool m_bReadOnly = false;
		/// <summary>Is true for readonly cursors.</summary>
		public bool bReadOnly { get { return m_bReadOnly; } }

		readonly iTypeSerializer m_serializer;
		/// <summary>The serializer for the records type.</summary>
		public iTypeSerializer serializer { get { return m_serializer; } }

		/// <summary>Get ESE table name.</summary>
		public string tableName { get { return m_serializer.tableAttribute.tableName; } }

		/// <summary></summary>
		public void Dispose()
		{
			if( m_idTable != JET_TABLEID.Nil )
			{
				if( m_bOwnsTable )
				{
					Api.JetCloseTable( m_session.idSession, m_idTable );
					m_idTable = JET_TABLEID.Nil;
				}
			}
		}

		/// <summary>Construct a detached cursor.</summary>
		/// <param name="session"></param>
		/// <param name="serializer"></param>
		/// <param name="idTable"></param>
		/// <param name="bReadonly"></param>
		protected EseCursorBase( iSerializerSession session, iTypeSerializer serializer, JET_TABLEID idTable, bool bReadonly )
		{
			m_session = session;
			m_serializer = serializer;
			m_idTable = idTable;
			m_bOwnsTable = false;
			m_bReadOnly = bReadonly;
		}

		/// <summary>Get bookmark of the current record.</summary>
		/// <returns></returns>
		public byte[] getBookmark()
		{
			return Api.GetBookmark( m_session.idSession, idTable );
		}

		/// <summary>Positions a cursor to an index entry for the record that is associated with the specified bookmark.
		/// The bookmark can be used with any index defined over a table.</summary>
		/// <param name="bk">The bookmark used to position the cursor.</param>
		public void gotoBookmark( byte[] bk )
		{
			Api.JetGotoBookmark( m_session.idSession, idTable, bk, bk.Length );
		}

		/// <summary>Try go to the bookmark, return false on JET_err.NoCurrentRecord condition.</summary>
		/// <param name="bk">The bookmark used to position the cursor.</param>
		/// <returns></returns>
		public bool tryGotoBookmark( byte[] bk )
		{
			try
			{
				gotoBookmark( bk );
			}
			catch( EsentErrorException ex )
			{
				if( ex.Error == JET_err.NoCurrentRecord )
					return false;
				throw new Exception( "JetGotoBookmark failed.", ex );
			}
			return true;
		}

		/// <summary>Get the search key of the current record in the currently set index.</summary>
		/// <returns></returns>
		public byte[] getSearchKey()
		{
			return Api.RetrieveKey( m_session.idSession, idTable, RetrieveKeyGrbit.None );
		}

		/// <summary>Navigate to the search key previously retrieved by getSearchKey().</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool trySeek( byte[] key )
		{
			Api.MakeKey( m_session.idSession, idTable, key, MakeKeyGrbit.NormalizedKey );
			return Api.TrySeek( m_session.idSession, idTable, SeekGrbit.SeekEQ );
		}

		/// <summary></summary>
		public bool TryMoveFirst()
		{
			return Api.TryMoveFirst( m_session.idSession, idTable );
		}

		/// <summary></summary>
		public bool TryMoveLast()
		{
			return Api.TryMoveLast( m_session.idSession, idTable );
		}

		/// <summary></summary>
		public bool tryMoveNext()
		{
			return Api.TryMoveNext( m_session.idSession, idTable );
		}

		/// <summary></summary>
		public bool tryMovePrevious()
		{
			return Api.TryMovePrevious( m_session.idSession, idTable );
		}

		/// <summary>Switch to the primary index, and remove the cursor's navigation limitations.</summary>
		public void ResetIndex()
		{
			Api.JetSetCurrentIndex( idSession, idTable, null );
			Api.ResetIndexRange( idSession, idTable );
		}

		/// <summary>Fetch the specific field from the current record of this table.</summary>
		/// <param name="fName">ESE column name to load from the table.</param>
		/// <returns>The field value.</returns>
		/// <remarks>This method was implemented because sometimes,
		/// e.g. while traversing the DB-backed tree searching for something,
		/// you only need the value of a single field.</remarks>
		public object FetchSingleField( string fName )
		{
			return m_serializer.FetchSingleField( this, fName );
		}

		/// <summary>Update the value of the specific field of the current record.</summary>
		/// <param name="fName">ESE column name to load from the table.</param>
		/// <param name="value">New field value.</param>
		/// <remarks>This method was implemented as the performance optimization:
		/// when you only need to update a single field, and you don't have the complete object.</remarks>
		public void SaveSingleField( string fName, object value )
		{
			using( var u = new Update( this.idSession, this.idTable, JET_prep.Replace ) )
			{
				m_serializer.SaveSingleField( this, fName, value );
				u.Save();
			}
		}

		/// <summary>Resolve column name to JET_COLUMNID.</summary>
		/// <param name="fName">ESE column name to resolve.</param>
		/// <returns>Column ID.</returns>
		/// <remarks>
		/// <para>Column IDs are persistent for the lifetime of the serializer: i.e. all sessions will get the same result.</para>
		/// <para>Knowing the Column ID, you may use Esent.Interop functionality in conjunction with the ESENT serialization framework:
		/// see <see cref="idSession">idSession</see> and <see cref="idTable">idTable</see> properties.
		/// This is useful for some special cases:
		/// to fetch values from secondary index to save HDD seeks, to use Api.EscrowUpdate method, and so on.</para>
		/// </remarks>
		public JET_COLUMNID GetColumnId( string fName )
		{
			return m_serializer.GetColumnId( this, fName );
		}

		/// <summary>Duplicate the cursor, and take the ownership of the copy.</summary>
		/// <remarks><b>NB:</b> this can only be done for a non-table owning cursor.</remarks>
		protected void Duplicate()
		{
			Debug.Assert( !m_bOwnsTable );
			JET_TABLEID idNewTable = JET_TABLEID.Nil;
#if NETFX_CORE
			// JetDupCursor is unavailable on WinRT
			OpenTableGrbit flags = ( bReadOnly ) ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None;
			Api.JetOpenTable( idSession, m_session.idDatabase, m_serializer.tableName, null, 0, flags, out idNewTable );
#else
			Api.JetDupCursor( idSession, idTable, out idNewTable, DupCursorGrbit.None );
#endif
			m_idTable = idNewTable;
			m_bOwnsTable = true;
		}
	}
}