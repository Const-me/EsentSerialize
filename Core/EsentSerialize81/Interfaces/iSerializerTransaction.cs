using System;

namespace EsentSerialization
{
	/// <summary>This interface represents a database transaction.</summary>
	/// <remarks>You must ( Commit() or LazyCommit() or Rollback() ) and/or Dispose() the transaction,
	/// otherwise the unmanaged resources will leak enormously fast.</remarks>
	public interface iSerializerTransaction : IDisposable
	{
		/// <summary>Get the session that owns this transaction.</summary>
		iSerializerSession session { get; }

		/// <summary>Commit the transaction and flush the transaction log. Doing so will close this transaction.</summary>
		void Commit();

		/// <summary>Commit the transaction,
		/// but this API does not wait for the transaction to be flushed to the transaction log file before returning to the caller.
		/// Doing so will close this transaction.</summary>
		void LazyCommit();

		/// <summary>Rollback the transaction.
		/// Doing so will close this transaction.</summary>
		/// <remarks>IDisposable.Dispose does the same.</remarks>
		void Rollback();

		/// <summary>Call LazyCommit, then re-open this transaction, using the same session.</summary>
		void LazyCommitAndReopen();
	}
}