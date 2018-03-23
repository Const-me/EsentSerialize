using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EsentSerialization
{
	// This class implements basic import-export operation on a single table.
	// The columns schema must exactly match the backup.
	// For the whole-database backups, use the corresponding ESE API instead.
	abstract class ImportExport
	{
		protected readonly iSerializerSession sess;
		protected readonly JET_TABLEID idTable;
		protected readonly TypeSerializer serializer;
		protected readonly IList<TypeSerializer.ColumnInfo> schema;

		protected readonly byte[] buff = new byte[ 256 * 1024 ];

		protected ImportExport( EseCursorBase cur ) :
			this( cur.session, cur.idTable, cur.serializer ) {}

		protected ImportExport( iSerializerSession _sess, JET_TABLEID _idTable, iTypeSerializer _serializer )
		{
			sess = _sess;
			idTable = _idTable;
			serializer = _serializer as TypeSerializer;

			// Ensure the column IDs were lookup
			serializer.LookupColumnIDs( sess.idSession, idTable );
			schema = serializer.getColumnsSchema().ToList();
		}

		protected bool isLongType( JET_COLUMNDEF cd )
		{
			return ( cd.coltyp == JET_coltyp.LongBinary || cd.coltyp == JET_coltyp.LongText );
		}

		protected bool isMultiValued( JET_COLUMNDEF cd )
		{
			return 0 != ( cd.grbit & ColumndefGrbit.ColumnMultiValued );
		}

		abstract public int Export( Stream stm );
		abstract public int Import( Stream stm );

		// Execute the ExportRecord once for each record
		protected int ExportData( Action<int> ExportRecord )
		{
			Api.ResetIndexRange( sess.idSession, idTable );
			Api.JetSetCurrentIndex( sess.idSession, idTable, null );
			if( !Api.TryMoveFirst( sess.idSession, idTable ) )
				return 0;

			int iRecordsCounter = 0;
			do
			{
				ExportRecord( iRecordsCounter );
				iRecordsCounter++;
			}
			while( Api.TryMoveNext( sess.idSession, idTable ) );
			return iRecordsCounter;
		}

		// Try to read record bu calling ReadRecord delegate.
		// If it returns false = no more records.
		// Then add a record by calling StoreRecord delegate.
		// If it returns false = the record was not added and the update should be discarded.
		protected int ImportData( Func< int, bool > ReadRecord, Func< int, bool > StoreRecord )
		{
			int iRecordsCounter = 0;
			using( var t = sess.BeginTransaction() )
			{
				// Clean up the whole destination table.
				while( Api.TryMoveLast( sess.idSession, idTable ) )
					Api.JetDelete( sess.idSession, idTable );

				// Import the records
				while(true)
				{
					if( !ReadRecord( iRecordsCounter ) )
						break;
					using( Update update = new Update( sess.idSession, idTable, JET_prep.Insert ) )
					{
						if( StoreRecord( iRecordsCounter ) )
						{
							update.Save();
							iRecordsCounter++;
							if( 0 == ( iRecordsCounter % 64 ) )
								t.LazyCommitAndReopen();
						}
						else
							update.Cancel();
					}
				}
				t.Commit();
				// serializer.tableAttribute.onTableChanged( this, new NotifyTableChangedArgs( NotifyTableChangedAction.Reset ) );
				return iRecordsCounter;
			}
		}
	}
}
