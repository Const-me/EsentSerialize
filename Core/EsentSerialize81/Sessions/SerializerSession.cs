using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EsentSerialization
{
	// This class represents the database session used by the serializer.
	// It's designed to be created once in each thread that uses the ESE.
	// The EsentResource-derived types in Microsoft.Isam.Esent.Interop namespace are great, however they are too low-level and granular.
	// This single class encapsulates pretty much all resources a typical thread ever needs from the ESE.
	partial class SerializerSession : iSerializerSessionImpl
	{
		// Session ID
		public JET_SESID m_idSession = JET_SESID.Nil;
		public JET_SESID idSession { get { return m_idSession; } }

		// Database ID
		public JET_DBID m_idDatabase = JET_DBID.Nil;
		public JET_DBID idDatabase { get { return m_idDatabase; } }

		struct sTable
		{
			public readonly JET_TABLEID idTable;
			public readonly bool bIsReadonly;
			public readonly TypeSerializer serializer;

			public sTable( JET_TABLEID _idTable, bool _bIsReadonly, TypeSerializer _serializer )
			{
				idTable = _idTable;
				bIsReadonly = _bIsReadonly;
				serializer = _serializer;
			}
		}

		// Opened tables.
		// Please mind that unless explicitly duplicated, only one cursor exists for each table.
		readonly Dictionary<Type, sTable> m_tables = new Dictionary<Type, sTable>();

		public IEnumerable<Tuple<Type, string>> /* iSerializerSession. */ GetAllTypes()
		{
			foreach( var p in m_tables )
				yield return Tuple.Create( p.Key, p.Value.serializer.tableName );
		}

		// Open a session + database
		readonly EseSerializer m_serializer;
		public EseSerializer serializer { get { return m_serializer; } }

		public SerializerSession( EseSerializer _ser )
		{
			m_serializer = _ser;
			Api.JetBeginSession( serializer.idInstance, out m_idSession, null, null );
			Api.JetAttachDatabase( m_idSession, serializer.pathDatabase, AttachDatabaseGrbit.None );
			Api.JetOpenDatabase( m_idSession, serializer.pathDatabase, null, out m_idDatabase, OpenDatabaseGrbit.None );

			this.setThread();
		}

		public void AddType( Type tRecord )
		{
			iTypeSerializer ts = m_serializer.FindSerializerForType( tRecord );
			if( null == ts )
			{
				m_serializer.AddSerializedType( tRecord );
				ts = m_serializer.FindSerializerForType( tRecord );
			}

			this.addType( ts as TypeSerializer, false );
		}

		public bool addType( TypeSerializer ts, bool bOpenAsReadonly )
		{
			Type t = ts.recordType;
			sTable tmp;
			if( m_tables.TryGetValue( t, out tmp ) )
				return false;
			// throw new SerializationException( " The table for type '" + t.Name + "' is already opened." );

			ts.VerifyTableSchema( m_idSession, m_idDatabase );

			OpenTableGrbit flags = ( bOpenAsReadonly ) ? OpenTableGrbit.ReadOnly : OpenTableGrbit.None;
			JET_TABLEID idTable;
			Api.JetOpenTable( m_idSession, m_idDatabase, ts.tableName, null, 0, flags, out idTable );

			m_tables[ t ] = new sTable( idTable, bOpenAsReadonly, ts );

			return true;
		}

		/// <summary>Close the session.</summary>
		/// <param name="bLastSession">Set to true if for the last session to close the database.</param>
		protected void DisposeImpl( bool bLastSession )
		{
			foreach( var i in m_tables )
				Api.JetCloseTable( m_idSession, i.Value.idTable );
			m_tables.Clear();

			if( JET_DBID.Nil != m_idDatabase )
			{
				Api.JetCloseDatabase( m_idSession, m_idDatabase, CloseDatabaseGrbit.None );
				m_idDatabase = JET_DBID.Nil;
			}

			if( JET_SESID.Nil != m_idSession )
			{
				if( bLastSession )
					Api.JetDetachDatabase( m_idSession, serializer.pathDatabase );
				Api.JetEndSession( m_idSession, EndSessionGrbit.None );
				m_idSession = JET_SESID.Nil;
			}
		}
		public void DisposeLastSession()
		{
			DisposeImpl( true );
		}

		public void Dispose()
		{
			DisposeImpl( false );
		}

		sTable findTable( Type t )
		{
			this.verifyThread();
			sTable res;
			if( !m_tables.TryGetValue( t, out res ) )
				throw new ArgumentException( "The table storing the records of type '" + t.Name + "' was not opened." );
			return res;
		}

		public Cursor<tRow> Cursor<tRow>() where tRow : new()
		{
			sTable tbl = findTable( typeof( tRow ) );
			return new Cursor<tRow>( this, tbl.serializer, tbl.idTable, tbl.bIsReadonly );
		}

		public Cursor<tRow> Cursor<tRow>( bool bReadOnly ) where tRow : new()
		{
			sTable tbl = findTable( typeof( tRow ) );
			if( tbl.bIsReadonly && ( !bReadOnly ) )
				throw new NotSupportedException( "Can't provide read-write access to the table that was opened as read-only." );
			return new Cursor<tRow>( this, tbl.serializer, tbl.idTable, bReadOnly );
		}

		public Recordset<tRow> Recordset<tRow>() where tRow : new()
		{
			Cursor<tRow> cur = Cursor<tRow>();
			return cur.Recordset();
		}

		public BookmarkedRecordset<tRow> BookmarkedRecordset<tRow>() where tRow : new()
		{
			Cursor<tRow> cur = Cursor<tRow>();
			return new BookmarkedRecordset<tRow>( cur );
		}

		readonly Stack<iSerializerTransaction> m_transactions = new Stack<iSerializerTransaction>();

		public iSerializerTransaction BeginTransaction()
		{
			this.verifyThread();
			SerializerTransaction res = new SerializerTransaction( this );
			return res;
		}

		int iSerializerSessionImpl.onTransactionBegin( iSerializerTransaction trans )
		{
			nOpened++;
			m_transactions.Push( trans );
			return m_transactions.Count;
		}

		void iSerializerSessionImpl.onTransactionEnd( int lvl, bool bCommitted )
		{
			this.verifyThread();
			if( lvl != m_transactions.Count )
				throw new InvalidOperationException( "The transaction was finished out of order." );

			// You must finish your transactions in the reverse order you've started them.
			// You can't begin transaction #1, then begin nested transaction #2, then end transaction #1 - this would be an error, you must end (=commit or rollback) transaction #2 before ending #1.

			if( bCommitted )
				nCommitted++;
			else
				nRolledBack++;

			m_transactions.Pop();
		}

		public bool /* iSerializerSession. */ isInTransaction { get { return m_transactions.Count > 0; } }

		public iSerializerTransaction /* iSerializerSession. */ transaction
		{
			get
			{
				if( m_transactions.Count > 0 )
					return m_transactions.Peek();
				return null;
			}
		}

		public int /* iSerializerSession. */ ClearTable( JET_TABLEID idTable )
		{
			Api.JetSetCurrentIndex( idSession, idTable, null );
			Api.ResetIndexRange( idSession, idTable );

			int nDeletedRecords = 0;
			using( var t = BeginTransaction() )
			{
				nDeletedRecords = 0;
				while( Api.TryMoveLast( idSession, idTable ) )
				{
					Api.JetDelete( idSession, idTable );
					nDeletedRecords++;
					if( 0 == ( nDeletedRecords % 64 ) )
						t.LazyCommitAndReopen();
				}
				t.Commit();
			}
			return nDeletedRecords;
		}

		int nCommitted = 0, nRolledBack = 0, nOpened = 0;

#if( DEBUG )
		// TODO [low]: implement a debug-only functionality that will will check for cursor conflict.
		// A conflict scenario:

		// 1. Recordset A: IEnumerable.GetEnumerator
		// 2. Recordset B using same JET_TABLEID: IEnumerable.GetEnumerator, any other cursor navigation
		// 3. recordset A: IEnumerator.MoveNext

		/* readonly Dictionary<JET_TABLEID, WeakReference> m_dictCursors;

		void onCursorConstruct( EseCursorBase cur, object obj )
		{
			Debug.Assert( !m_dictCursors.ContainsKey( cur.idTable ) );
			m_dictCursors.Add( cur.idTable, new WeakReference( obj ) );
		}

		void onCursorUse( EseCursorBase cur, object obj )
		{
		}

		void onCursorDestroy( EseCursorBase cur )
		{
			
		} */
#endif
		int currentlyRunningThread { get { return Environment.CurrentManagedThreadId; } }
		int? m_idThread;

		public void setThread()
		{
			int currThread = this.currentlyRunningThread;
			if( m_idThread.HasValue && m_idThread.Value != currThread )
				throw new InvalidOperationException( "This session is already in use by another thread." );
			m_idThread = currThread;
			return;
		}

		public void verifyThread()
		{
			int currThread = this.currentlyRunningThread;
			if( currThread != m_idThread.Value )
				throw new InvalidOperationException( "You must use different sessions to access the database from different threads." );
		}

		/// <summary>Reset this session's thread to null.</summary>
		/// <remarks>It will be set to the currently running managed thread ID next time you'll do something with this session.</remarks>
		public void clearThread()
		{
			if( isInTransaction )
				throw new InvalidOperationException( "clearThread is called while in a transaction." );
			m_idThread = null;
		}

		public Type GetType( string strTableName )
		{
			strTableName = strTableName.ToLowerInvariant();
			return m_tables.Where( kvp => kvp.Value.serializer.tableName.ToLowerInvariant() == strTableName )
				.Select( kvp => kvp.Key )
				.FirstOrDefault();
		}

		internal enum eTableState : byte
		{
			closed,
			opennedReadOnly,
			openned,
		}

		internal eTableState closeTable<tRow>() where tRow : new()
		{
			Type typeRow = typeof( tRow );

			sTable tbl;
			if( !m_tables.TryGetValue( typeRow, out tbl ) )
				return eTableState.closed;

			m_tables.Remove( typeRow );
			Api.JetCloseTable( m_idSession, tbl.idTable );

			return tbl.bIsReadonly ? eTableState.opennedReadOnly : eTableState.openned;
		}

		internal void reopenTable<tRow>( eTableState state ) where tRow : new()
		{
			if( eTableState.closed == state )
				return;
			TypeSerializer ts = (TypeSerializer)this.serializer.FindSerializerForType( typeof( tRow ) );
			this.addType( ts, eTableState.opennedReadOnly == state );
		}

		public void recreateTable<tRow>() where tRow : new()
		{
			eTableState oldState = closeTable<tRow>();

			Type typeRow = typeof( tRow );
			TypeSerializer ts = (TypeSerializer)this.serializer.FindSerializerForType( typeRow );
			ts.RecreateTable( this );
			reopenTable<tRow>( oldState );
		}
	}
}