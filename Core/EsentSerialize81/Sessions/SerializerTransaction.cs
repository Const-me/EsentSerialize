using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization
{
	// This class represents a ESE transaction.
	class SerializerTransaction : iSerializerTransaction
	{
		iSerializerSessionImpl m_session = null;
		int m_transactionLevel = -1;

		void Open( iSerializerSessionImpl sess )
		{
			if( null != m_session )
				throw new InvalidOperationException( "Already in a transaction." );

			Api.JetBeginTransaction( sess.idSession );
			m_session = sess;
			m_transactionLevel = m_session.onTransactionBegin( this );
		}

		public SerializerTransaction( iSerializerSessionImpl session )
		{
			Open( session );
		}

		public iSerializerSession session { get { return m_session; } }

		void Commit( CommitTransactionGrbit flags )
		{
			if( null == m_session )
				throw new InvalidOperationException( "Not in a transaction." );
			Api.JetCommitTransaction( m_session.idSession, flags );
			m_session.onTransactionEnd( m_transactionLevel, true );
			m_session = null;
			m_transactionLevel = -2;
		}

		void iSerializerTransaction.Commit()
		{
			Commit( CommitTransactionGrbit.None );
		}

		void iSerializerTransaction.LazyCommit()
		{
			Commit( CommitTransactionGrbit.LazyFlush );
		}

		public void Rollback()
		{
			if( null == m_session )
				throw new InvalidOperationException( "Not in a transaction" );
			Api.JetRollback( m_session.idSession, RollbackTransactionGrbit.None );
			m_session.onTransactionEnd( m_transactionLevel, false );
			m_session = null;
			m_transactionLevel = -2;
		}

		public void LazyCommitAndReopen()
		{
			if( null == m_session )
				throw new InvalidOperationException( "Not in a transaction" );

			// 'Commit' call will clear m_session field, so we need to preserve the session in a local variable.
			var sess = m_session;
			Commit( CommitTransactionGrbit.LazyFlush );
			Open( sess );
		}

		void IDisposable.Dispose()
		{
			if( m_session != null )
				Rollback();
		}
	}
}