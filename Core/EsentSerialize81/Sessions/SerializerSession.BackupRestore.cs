using System;
using System.IO;

namespace EsentSerialization
{
	partial class SerializerSession : iSerializerSessionImpl
	{
		public void /* iSerializerSession. */ importTable( Type tRecord, Stream stm, ImportExportFormat fmt )
		{
			ImportExportImpl( true, tRecord, stm, fmt );
		}

		public void /* iSerializerSession. */ exportTable( Type tRecord, Stream stm, ImportExportFormat fmt )
		{
			ImportExportImpl( false, tRecord, stm, fmt );
		}

		void ImportExportImpl( bool bImporting, Type tRecord, Stream stm, ImportExportFormat fmt )
		{
			sTable tbl;
			if( !m_tables.TryGetValue( tRecord, out tbl ) )
				throw new ArgumentException( "Table with records of type '" + tRecord.Name + "' doesn't exist in this session." );

			ImportExport ie = null;
			if( fmt == ImportExportFormat.TSV )
				ie = new ImportExportTSV( this, tbl.idTable, tbl.serializer );
			// else if( fmt == ImportExportFormat.Binary )
			// 	ie = new ImportExportBinary( this, tbl.idTable, tbl.serializer );
			else
				throw new NotSupportedException( "The ImportExportFormat is not supported." );

			if( bImporting )
			{
				ClearTable( tbl.idTable );
				ie.Import( stm );
			}
			else
				ie.Export( stm );
		}
	}
}