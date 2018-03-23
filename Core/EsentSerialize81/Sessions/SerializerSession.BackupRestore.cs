using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace EsentSerialization
{
	partial class SerializerSession : iSerializerSessionImpl
	{
		public void /* iSerializerSession. */ importTable( Stream stm, Type tRecord, ImportExportFormat fmt )
		{
			ImportExportImpl( true, tRecord, stm, fmt );
		}

		public void /* iSerializerSession. */ exportTable( Stream stm, Type tRecord, ImportExportFormat fmt )
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

		public void exportTables( Stream stm, Type[] tRecords )
		{
			// ANsure all types are added
			foreach( var t in tRecords )
				AddType( t );
			using( ZipArchive archive = new ZipArchive( stm, ZipArchiveMode.Update, true ) )
			using( var trans = BeginTransaction() )
			{
				foreach( var tp in tRecords )
				{
					string strFileName = m_tables[ tp ].serializer.tableName + ".tsv";
					var e = archive.GetEntry(strFileName);
					if( null != e )
						e.Delete();
					e = archive.CreateEntry( strFileName, CompressionLevel.Optimal );
					using( var s = e.Open() )
						exportTable( s, tp, ImportExportFormat.TSV );
				}
			}
		}

		public void importTables( Stream stm )
		{
			using( ZipArchive archive = new ZipArchive( stm, ZipArchiveMode.Read, true ) )
			{
				foreach( ZipArchiveEntry entry in archive.Entries )
				{
					string fn = entry.Name.ToLowerInvariant();
					if( !fn.EndsWith( ".tsv" ) )
						continue;
					fn.Replace( '\\', '/' );
					if( fn.Contains( "/" ) )
						fn = fn.Substring( fn.LastIndexOf( '/' ) + 1 );
					fn = fn.Substring( 0, fn.Length - 4 );
					// Find the type
					Type tp = GetAllTypes().FirstOrDefault( p => p.Item2.ToLowerInvariant() == fn )?.Item1;
					if( null == tp )
						continue;

					// Import the table
					using( var stmTable = entry.Open() )
						importTable( stmTable, tp, ImportExportFormat.TSV );
				}
			}
		}
	}
}