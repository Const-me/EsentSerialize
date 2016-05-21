using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace EsentSerialization
{
	/// <summary>Concurrent behavior of the sessions.</summary>
	public enum eSessionCooperativeLevel : byte
	{
		/// <summary>No locking is performed (fastest way)</summary>
		Free = 0,

		/// <summary>This session is OK to co-exist with others sessions.</summary>
		NonExclusive,

		/// <summary>This session can't coexist with others because it performs some exclusive operation on the DB.</summary>
		Exclusive
	}

	/// <summary>This class maintains the pool of the ESENT sessions, to implement e.g. multithreaded software processing network requests.</summary>
	/// <remarks>This class encapsulates a <see cref="EseSerializer" /> instance.
	/// In the same process at the same time, you may use either <see cref="EseSerializer"/> or SessionPool, but not both.</remarks>
	public partial class SessionPool : IDisposable
	{
		/// <summary>The folder where the DB is located.</summary>
		public readonly string folderDatabase;

		/// <summary>Maximum number of the ESENT sessions to open.</summary>
		/// <remarks>0 = unlimited.<br/>
		/// The default is 16.</remarks>
		public readonly int SessionLimit = 16;

		/// <summary>Timeout to wait for a session to free when the SessionLimit is hit, before throwing a TimeoutException.</summary>
		static readonly TimeSpan tsWaitForFreeSession = TimeSpan.FromSeconds( 5 );

		/// <summary>If true, the nested calls to GetSession() on the same thread will return the instances of the same session.</summary>
		/// <remarks>The default is true.</remarks>
		public bool singleSessionPerThread = true;

		// SyncRoot, see e.g. http://haacked.com/archive/2006/08/08/ThreadingNeverLockThisRedux.aspx foe the reason why
		readonly object syncRoot = new object();

		/// <summary>True if the new DB has been created by the constructor of this instance.</summary>
		public bool isNewDatabase { get { return m_serializer.isNewDatabase; } }

		/// <summary>This HashSet owns all ESENT sessions.</summary>
		readonly HashSet<SerializerSession> m_allSessions;

		/// <summary>This stack holds the free sessions,
		/// i.e. the subset of m_allSessions that are ready to be immediately returned by GetSession when required.</summary>
		/// <remarks>The stack is here because "reuse the session that was just freed" strategy is more friendly to both CPU caches and Windows page file,
		/// in the scenario when the peak load period is followed by relatively off-loaded period.</remarks>
		readonly Stack<SerializerSession> m_freeSessions;

		/// <summary>The serializer object.</summary>
		EseSerializer m_serializer;

		/// <summary>The semaphore to enforce the SessionLimit limitation.
		/// If SessionLimit is set to 0, this member will be null.</summary>
		Semaphore m_semaphore = null;

		/// <summary>Get the ESENT session from the pool.</summary>
		/// <param name="coopLevel">Specify whether an exclusive session is requested, the default is NonExclusive.</param>
		/// <returns>Return the session.</returns>
		/// <remarks>
		/// <para>This operation is inexpensive: the method usually returns the last session released to the pool.</para>
		/// <para><b>NB:</b> You must call <see cref="IDisposable.Dispose">Dispose</see> exactly once
		/// for each iSerializerSession instance returned by this method. The recommended construction is:</para>
		/// <code lang="C#">            using( iSerializerSession sess = m_sessionPool.GetSession() )
		///using( iSerializerTransaction trans = sess.beginTransaction() )
		///{
		///	// Do whatever you need with the data: obtain cursors/recordsets, search, modify records, etc..
		///	trans.Commit();		// Or don't, if you need the transaction to roll back.
		///}</code>
		/// </remarks>
		public iSerializerSession GetSession( eSessionCooperativeLevel coopLevel = eSessionCooperativeLevel.NonExclusive )
		{
			return GetSessionImpl( coopLevel );
		}

		internal void TraceInfo( string msg )
		{
			// Trace.WriteLine( msg, "SessionPool" );
		}

		internal void TraceWarning( string msg )
		{
			// Trace.TraceWarning( msg + "\r\n" );
		}

		[ThreadStatic]
		static SerializerSessionImpl threadSession;

		bool m_bFirstSession = true;

		/// <summary>Set this action to your method that upgrades the DB schema.</summary>
		/// <remarks>The delegate will be called immediately after the first session is constructed.</remarks>
		public Action<DatabaseSchemaUpdater> updateSchema = null;

		readonly ReaderWriterLockSlim sessionCooperationLock = new ReaderWriterLockSlim();

		iSerializerSession GetSessionImpl( eSessionCooperativeLevel coopLevel )
		{
			if( singleSessionPerThread && null != threadSession )
			{
				if( threadSession.cooperativeLevel != coopLevel )
					throw new Exception( "You can't get sessions with different cooperative levels on the same thread at the same time" );
				threadSession.AddRef();
				return threadSession;
			}

			if( null != m_semaphore )
				if( !m_semaphore.WaitOne( tsWaitForFreeSession ) )
					throw new TimeoutException( "SessionPool.tsWaitForFreeSession timeout exceeded." );

			SerializerSession nativeSession = null;
			bool bNewSession = false;

			lock( this.syncRoot )
			{
				if( m_freeSessions.Count > 0 )
					nativeSession = m_freeSessions.Pop();
				else
				{
					m_serializer.EnsureDatabaseExists();

					nativeSession = new SerializerSession( m_serializer );
					m_allSessions.Add( nativeSession );
					bNewSession = true;

					if( m_bFirstSession )
					{
						m_bFirstSession = false;

						if( null != this.updateSchema )
						{
							DatabaseSchemaUpdater updater = new DatabaseSchemaUpdater( nativeSession );
							this.updateSchema( updater );
						}
					}
				}
			}

			if( bNewSession )
			{
				foreach( var t in m_recordTypes )
					nativeSession.AddType( t );
				TraceInfo( "GetSessionImpl: constructed a new session" );
			}

			SerializerSessionImpl sessionWrapper = new SerializerSessionImpl( nativeSession, this, coopLevel );
			sessionCooperativeLockEnter( sessionWrapper );
			sessionWrapper.AddRef();

			if( singleSessionPerThread )
				threadSession = sessionWrapper;

			// Bind new session to the current thread
			nativeSession.setThread();

			return sessionWrapper;
		}

		void ReleaseSessionImpl( SerializerSessionImpl wrapper )
		{
			SerializerSession session = wrapper.session;
			if( null == session )
				throw new ArgumentNullException( "session" );

			if( session.isInTransaction )
				throw new Exception( "You must either commit or rollback the current transaction before releasing the session." );

			sessionCooperativeLockLeave( wrapper );

			session.clearThread();

			if( singleSessionPerThread )
				threadSession = null;

			lock( this.syncRoot )
				m_freeSessions.Push( session );

			if( null != m_semaphore )
				m_semaphore.Release();
		}

		/// <summary>Construct the session pool.</summary>
		/// <remarks>You should only create a single instance of this class.</remarks>
		/// <param name="strFolder">Database folder. Must be writeable. The best place for the DB on UWP is ApplicationData.Current.LocalFolder, or some subfolder in that folder.</param>
		/// <param name="maxSessions">Maximum number of concurrent ESENT sessions to open.</param>
		/// <param name="arrTypes">The array of record types.
		/// In every session returned by this pool, the corresponding tables will be already opened for you.</param>
		public SessionPool( string strFolder, int maxSessions, params Type[] arrTypes )
		{
			folderDatabase = strFolder;

			m_serializer = new EseSerializer( folderDatabase, null );

			SessionLimit = Math.Max( maxSessions, 0 );

			if( SessionLimit > 0 )
				m_semaphore = new Semaphore( SessionLimit, SessionLimit );

			m_allSessions = new HashSet<SerializerSession>();

			m_freeSessions = new Stack<SerializerSession>();

			m_recordTypes = arrTypes.ToList();

			m_serializer.EnsureDatabaseExists();
		}

		static Type[] typesInAssembly( Assembly ass )
		{
			return ass.ExportedTypes
				.Where( tp => null != tp.getTableAttribute() )
				.ToArray();
		}

		/// <summary>Construct the session pool.</summary>
		/// <remarks>You should only create a single instance of this class.</remarks>
		/// <param name="strFolder">Database folder</param>
		/// <param name="maxSessions">Maximum number of the ESENT sessions to open.</param>
		/// <param name="ass">An assembly with the [EseTable] types to open.</param>
		public SessionPool( string strFolder, int maxSessions, Assembly ass ) :
			this( strFolder, maxSessions, typesInAssembly( ass ) )
		{ }

		List<Type> m_recordTypes;

		/// <summary></summary>
		public void Dispose()
		{
			lock( syncRoot )
			{
				// Close all sessions.
				var lSessions = m_allSessions.ToList();
				for( int i = lSessions.Count - 1; i >= 0; i-- )
				{
					// Every CloseSession may fail e.g. with JET_err.SessionContextNotSetByThisThread if it runs a transaction.
					try
					{
						var sess = lSessions[ i ];
						if( 0 == i )
							sess.DisposeLastSession();
						else
							sess.Dispose();
					}
					catch( System.Exception ex )
					{
						TraceWarning( "SessionPool.Dispose - unable to close a session: " + ex.ToString() );
					}
				}
				m_allSessions.Clear();
				m_freeSessions.Clear();

				// Close the database.
				if( null != m_serializer )
				{
					try
					{
						m_serializer.Dispose();
					}
					catch( System.Exception ex )
					{
						TraceWarning( "SessionPool.Dispose - unable to dispose a serializer: " + ex.ToString() );
					}

					m_serializer = null;
				}

				if( null != m_semaphore )
				{
					m_semaphore.Dispose();
					m_semaphore = null;
				}
			}
		}

		/// <summary>This internal class holds the sessions in which a specific table was opened, with the bool readonly flag.</summary>
		internal class TableInSession
		{
			readonly Dictionary<SerializerSession, SerializerSession.eTableState> m_list = new Dictionary<SerializerSession, SerializerSession.eTableState>();

			public void add( SerializerSession s, SerializerSession.eTableState t )
			{
				m_list.Add( s, t );
			}

			public SerializerSession.eTableState lookup( SerializerSession s )
			{
				SerializerSession.eTableState res;
				if( m_list.TryGetValue( s, out res ) )
					return res;
				return SerializerSession.eTableState.closed;
			}
		}

		internal object closeTable<tRow>( SerializerSession thisSession ) where tRow : new()
		{
			TableInSession res = new TableInSession();
			lock( syncRoot )
			{
				foreach( SerializerSession sess in m_allSessions )
				{
					if( sess == thisSession )
						continue;
					res.add( sess, sess.closeTable<tRow>() );
				}
			}
			return res;
		}

		internal void reopenTable<tRow>( object cookie ) where tRow : new()
		{
			TableInSession sessions = (TableInSession)cookie;

			TypeSerializer ser = this.m_serializer.FindSerializerForType( typeof( tRow ) );
			lock( syncRoot )
			{
				foreach( SerializerSession s in m_allSessions )
				{
					s.reopenTable<tRow>( sessions.lookup( s ) );
				}
			}
		}

		private void sessionCooperativeLockEnter( SerializerSessionImpl sess )
		{
			switch( sess.cooperativeLevel )
			{
				case eSessionCooperativeLevel.NonExclusive:
					this.sessionCooperationLock.EnterReadLock();
					return;
				case eSessionCooperativeLevel.Exclusive:
					this.sessionCooperationLock.EnterWriteLock();
					return;
			}
		}

		private void sessionCooperativeLockLeave( SerializerSessionImpl sess )
		{
			switch( sess.cooperativeLevel )
			{
				case eSessionCooperativeLevel.NonExclusive:
					this.sessionCooperationLock.ExitReadLock();
					return;
				case eSessionCooperativeLevel.Exclusive:
					this.sessionCooperationLock.ExitWriteLock();
					return;
			}
		}

		/// <summary>Get type serializer for the specified record type.</summary>
		/// <remarks>If called before the first sessions is created, this will create a new session, potentially triggering schema update.</remarks>
		public iTypeSerializer serializerForType<tRecord>()
		{
			lock( this.syncRoot )
			{
				if( m_allSessions.Count > 0 )
					return m_serializer.FindSerializerForType( typeof( tRecord ) );
				using( var sess = GetSession() )
					return sess.serializer.FindSerializerForType( typeof( tRecord ) );
			}
		}
	}
}