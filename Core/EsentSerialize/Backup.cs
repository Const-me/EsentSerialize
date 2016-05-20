using Microsoft.Isam.Esent.Interop;
using System;
using System.IO;
using System.IO.Compression;

namespace EsentSerialization
{
	public static class Backup
	{
		public static void BackupDatabase( this EseSerializer serializer, Stream stm )
		{
			BackupDatabase( serializer, stm, CompressionLevel.Optimal );
		}

		public static void BackupDatabase( this EseSerializer serializer, Stream stm, CompressionLevel compressionLevel )
		{
			using( ZipArchive archive = new ZipArchive( stm, ZipArchiveMode.Create, false ) )
				BackupDatabaseImpl( serializer.idInstance, archive, CompressionLevel.Optimal );
		}

		const int iBuffSize = 256 * 1024;
		delegate void JetListFilesDelegate( JET_INSTANCE instance, out string files, int maxChars, out int actualChars );

		static void BackupDatabaseImpl( JET_INSTANCE idInstance, ZipArchive destination, CompressionLevel level )
		{
			// Start the backup process
			Api.JetBeginExternalBackupInstance( idInstance, BeginExternalBackupGrbit.None );

			byte[] buff = new byte[ iBuffSize ];

			// Database files that should become part of the backup file set
			BackupFiles( idInstance, destination, level, buff, Api.JetGetAttachInfoInstance );

			// Database patch files and transaction log files that should become part of the backup file set
			BackupFiles( idInstance, destination, level, buff, Api.JetGetLogInfoInstance );

			// Delete any transaction log files that will no longer be needed once the current backup completes successfully.
			Api.JetTruncateLogInstance( idInstance );

			// End an external backup session
			Api.JetEndExternalBackupInstance( idInstance );
		}

		static void BackupFiles( JET_INSTANCE idInstance, ZipArchive archive, CompressionLevel compressionLevel, byte[] buff, JetListFilesDelegate listFilesProc )
		{
			// Call the supplied API to list the files.
			int maxChars = 1024, actualChars = 0;
			string files;
			while( true )
			{
				listFilesProc( idInstance, out files, maxChars, out actualChars );
				if( actualChars < maxChars )
					break;
				maxChars = maxChars * 16;
				actualChars = 0;
			}

			string[] arrFiles = files.Split( new char[ 1 ] { '\0' }, StringSplitOptions.RemoveEmptyEntries );

			// Backup the files.
			foreach( string strFileName in arrFiles )
			{
				JET_HANDLE hFile;
				long fsLow, fsHigh;
				Api.JetOpenFileInstance( idInstance, strFileName, out hFile, out fsLow, out fsHigh );

				ulong ulFileSize = ( (ulong)fsLow & (ulong)uint.MaxValue ) | ( ( (ulong)fsHigh & (ulong)uint.MaxValue ) << 32 );

				try
				{
					ulong ulRemaining = ulFileSize;
					string shortName = Path.GetFileName( strFileName );
					ZipArchiveEntry entry = archive.CreateEntry( shortName, compressionLevel );
					using( Stream stm = entry.Open() )
					{
						while( ulRemaining > 0 )
						{
							int cbRead;
							Api.JetReadFileInstance( idInstance, hFile, buff, iBuffSize, out cbRead );
							if( 0 == cbRead )
								throw new EndOfStreamException( "Error backing up file " + strFileName );
							stm.Write( buff, 0, cbRead );
							ulRemaining -= (ulong)cbRead;
						}
						stm.Flush();
					}
				}
				finally
				{
					Api.JetCloseFileInstance( idInstance, hFile );
				}
			}
		}
	}
}