using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EsentSerialization
{
	public partial class SessionPool
	{
		/// <summary>This is the helper interface supported by <see cref="iSerializerSession"/>
		/// objects being returned from <see cref="SessionPool.GetSession"/> method.</summary>
		/// <remarks>
		/// <para>This interface is internal, and is not designed to be callable by the user.</para>
		/// <para>The SessionPool extends iSerializerSession supporting the reference counting.</para>
		/// <para>When you get iSerializerSession from <see cref="EseSerializer.OpenDatabase"/> method, a new session being opened.
		/// When you later call Dispose() on that object, the underlying ESE session is closed.</para>
		/// <para>This behavior is undesired for a server kind of software: ESE sessions are limited resource,
		/// and when the framework opens a new session for you it usually also open cursors on every table.</para>
		/// <para>That's why SessionPool class implements reusable and reference-counted sessions.</para>
		/// </remarks>
		public interface iPooledSession : iSerializerSession, IDisposable
		{
			/// <summary>Increments reference count.</summary>
			/// <returns>Returns an integer from 1 to n, the value of the new reference count.</returns>
			int AddRef();

			/// <summary>Decrements reference count.</summary>
			/// <returns>Returns the resulting value of the reference count.</returns>
			int Release();

			/// <summary>Reset reference counter to the specific value.</summary>
			/// <remarks>You should never call this method.</remarks>
			void ResetReferenceCounter( int val );
		}

		/// <summary>This class implements reference-counted wrapper around the underlying native SerializerSession.</summary>
		private class SerializerSessionImpl : iPooledSession
		{
			readonly SessionPool pool;
			public SerializerSession session { get; private set; }

			public readonly eSessionCooperativeLevel cooperativeLevel;

			/// <summary>how many times this session has been returned by AspSessionPool.GetSession</summary>
			int refCount = 0;

			public SerializerSessionImpl( SerializerSession session, SessionPool pool, eSessionCooperativeLevel coopLevel )
			{
				this.pool = pool;
				this.session = session;
				refCount = 0;
				this.cooperativeLevel = coopLevel;
			}

			public void Dispose()
			{
				if( 0 == refCount )
					throw new ObjectDisposedException( "SerializerSessionImpl" );

				int rc = this.Release();
				if( rc > 0 )
				{
					// still referenced by the client
					return;
				}

				if( null != actAfterRelease )
				{
					actAfterRelease();
					actAfterRelease = null;
				}

				// Return the native session to the pool
				pool.ReleaseSessionImpl( this );
				this.session = null;
			}

			public int AddRef()
			{
				refCount++;
				return refCount;
			}
			public int Release()
			{
				refCount--;
				return refCount;
			}
			public void ResetReferenceCounter( int val ) { refCount = val; }

			void iSerializerSession.DisposeLastSession()
			{
				throw new NotSupportedException();
			}

			EseSerializer iSerializerSession.serializer
			{
				get { return this.session.serializer; }
			}

			JET_SESID iSerializerSession.idSession
			{
				get { return this.session.idSession; }
			}

			JET_DBID iSerializerSession.idDatabase
			{
				get { return this.session.idDatabase; }
			}

			Cursor<tRow> iSerializerSession.Cursor<tRow>()
			{
				return session.Cursor<tRow>();
			}

			Cursor<tRow> iSerializerSession.Cursor<tRow>( bool bReadOnly )
			{
				return session.Cursor<tRow>( bReadOnly );
			}

			Recordset<tRow> iSerializerSession.Recordset<tRow>()
			{
				return session.Recordset<tRow>();
			}
			BookmarkedRecordset<tRow> iSerializerSession.BookmarkedRecordset<tRow>()
			{
				return session.BookmarkedRecordset<tRow>();
			}

			iSerializerTransaction iSerializerSession.BeginTransaction()
			{
				return this.session.BeginTransaction();
			}

			bool iSerializerSession.isInTransaction
			{
				get { return this.session.isInTransaction; }
			}

			iSerializerTransaction iSerializerSession.transaction
			{
				get { return this.session.transaction; }
			}

			int iSerializerSession.ClearTable( JET_TABLEID idTable )
			{
				return this.session.ClearTable( idTable );
			}

			IEnumerable<Tuple<Type, string>> iSerializerSession.GetAllTypes()
			{
				return this.session.GetAllTypes();
			}

			void iSerializerSession.AddType( Type tRecord )
			{
				this.session.AddType( tRecord );
			}

			Type iSerializerSession.GetType( string strTableName )
			{
				return this.session.GetType( strTableName );
			}

			void iSerializerSession.recreateTable<tRow>()
			{
				if( this.cooperativeLevel != eSessionCooperativeLevel.Exclusive )
					throw new Exception( "To call recreateTable API, this session must be opened with eSessionCooperativeLevel.Exclusive option" );

				SerializerSession.eTableState stateInThisSession = this.session.closeTable<tRow>();

				// Close this table in all sessions.
				object k = this.pool.closeTable<tRow>( this.session );

				try
				{
					// Recreate the table
					this.session.recreateTable<tRow>();
				}
				finally
				{
					// Reopen the table in this session
					this.session.reopenTable<tRow>( stateInThisSession );

					// For the rest of the sessions however, delay reopening till the end of this session
					// Due to session isolations other session can't see the changes until the transaction is committed,
					// and because we require eSessionCooperativeLevel.Exclusive the reopen is safe to delay until the end of the session (it's much harder to track the exact moment when the transaction is committed - they can be nested).
					Action actReopenInOtherSessions = () => this.pool.reopenTable<tRow>( k );
					if( null == actAfterRelease )
						actAfterRelease = actReopenInOtherSessions;
					else
						actAfterRelease += actReopenInOtherSessions;
				}
			}

			/// <summary>An action to be delayed to the moment immediately after this session is released to the pool.</summary>
			Action actAfterRelease = null;
		}
	}
}