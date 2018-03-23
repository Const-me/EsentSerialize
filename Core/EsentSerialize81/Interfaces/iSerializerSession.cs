using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.IO;

namespace EsentSerialization
{
	/// <summary>Tables import/export format.</summary>
	public enum ImportExportFormat: byte
	{
		/// <summary>Mostly human-readable UTF8 tab-separated data.</summary>
		TSV = 1,
	}

	/// <summary>This interface represents a database session.</summary>
	/// <remarks><para>The session object allows you to begin transactions, and obtain the cached table objects.</para>
	/// <para>If your application is multi-threaded, you should use separate session for every thread.</para>
	/// <para>If your application is an ASP.NET web application, you should use the <see cref="SessionPool"/> object.</para>
	/// </remarks>
	public interface iSerializerSession : IDisposable
	{
		/// <summary>Same as IDisposable.Dispose, but also detaches the DB just before the JetEndSession call.</summary>
		void DisposeLastSession();

		/// <summary>The EseSerializer who owns this session.</summary>
		EseSerializer serializer { get; }
		/// <summary>Native session ID.</summary>
		JET_SESID idSession { get; }
		/// <summary>Native database ID.</summary>
		JET_DBID idDatabase { get; }

		/// <summary>Get the Cursor object for a table.</summary>
		Cursor<tRow> Cursor<tRow>() where tRow : new();
		/// <summary>Same as above, but the returned cursor is optionally read-only.</summary>
		Cursor<tRow> Cursor<tRow>( bool bReadOnly ) where tRow : new();
		/// <summary>Get the Recordset object for a table.</summary>
		Recordset<tRow> Recordset<tRow>() where tRow : new();
		/// <summary>Get the BookmarkedRecordset object for a table.</summary>
		BookmarkedRecordset<tRow> BookmarkedRecordset<tRow>() where tRow : new();

		/// <summary>Begins a transaction.</summary>
		/// <remarks><para>You must ( commit or rollback ) and/or Dispose() the returned transaction, otherwise the unmanaged resources will leak pretty fast.</para>
		/// <para>In support of snapshot isolation, the database engine stores all versions of all modified data in memory since the time when the oldest active transaction on any session was first started.
		/// It is important to make transactions as short in duration as possible under high load scenarios.
		/// The class library provides <see cref="iSerializerTransaction.LazyCommitAndReopen">the method</see> to "pulse" the transaction.</para>
		/// <para>It is highly recommended that the application always be in the context of a transaction when calling ESE APIs that retrieve or update data.
		/// If this is not done, the database engine will automatically wrap each ESE API call of this type in a transaction on behalf of the application.
		/// The cost of these very short transactions can add up quickly in some cases.</para>
		/// </remarks>
		iSerializerTransaction BeginTransaction();

		/// <summary>If this session is currently in transaction, this property is set to true.</summary>
		bool isInTransaction { get; }

		/// <summary>If this session is currently in transaction, returns the transaction, otherwise returns null.</summary>
		iSerializerTransaction transaction { get; }

		/// <summary>Clear the whole table.</summary>
		/// <param name="idTable"></param>
		/// <returns>Count of the erased records.</returns>
		int ClearTable( JET_TABLEID idTable );

		/// <summary>Enumerate opened tables.</summary>
		IEnumerable<Tuple<Type, string>> GetAllTypes();

		/// <summary>Open the table in this session.</summary>
		/// <remarks>If this type was not added to the <see cref="EseSerializer" /> who owns this session,
		/// it will be added by this method. So, be ready for SerializationException about wrong DB schema.</remarks>
		void AddType( Type tRecord );

		/// <summary>Resolve table name into the record's type.</summary>
		/// <remarks>This function only looks through the tables opened in this session.</remarks>
		/// <param name="strTableName">The table name; the value is case-sensitive.</param>
		/// <returns>Record type, or null if the table was not found.</returns>
		Type GetType( string strTableName );

		/// <summary>Drop and recreate the complete table.</summary>
		/// <remarks>
		/// <para><b>NB!</b> This method will erase all data + invalidate JET_TABLEID in all sessions.
		/// Handle with care, and expect all kind of problems if you're recreating table while other threads uses it.</para>
		/// <para><b>NB!</b> If you have called <see cref="Cursor{T}.CreateOwnCopy"/> or <see cref="Recordset{T}.CreateOwnCursor" /> on cursors/recordsets in this table,
		/// and you're still holding an open copy of the cursor/recordset, this method will fail saying the table is open.</para>
		/// </remarks>
		/// <typeparam name="tRow"></typeparam>
		void recreateTable<tRow>() where tRow : new();

		/// <summary>Import the table from a stream.</summary>
		/// <param name="tRecord"></param>
		/// <param name="stm"></param>
		/// <param name="fmt">Import format</param>
		void importTable( Type tRecord, Stream stm, ImportExportFormat fmt );

		/// <summary>Export the whole table to the specified stream.</summary>
		/// <param name="tRecord"></param>
		/// <param name="stm"></param>
		/// <param name="fmt">Export format.</param>
		void exportTable( Type tRecord, Stream stm, ImportExportFormat fmt );
	}
}