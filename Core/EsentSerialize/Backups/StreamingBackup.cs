using Microsoft.Isam.Esent.Interop;
using System.IO;

namespace EsentSerialization
{
	public static partial class Backup
	{
		/// <summary>Perform streaming backup.</summary>
		/// <param name="serializer"></param>
		/// <param name="dest">Destination folder</param>
		public static void StreamingBackup( this EseSerializer serializer, string dest )
		{
			if( !Directory.Exists( dest ) )
				Directory.CreateDirectory( dest );
			Api.JetBackupInstance( serializer.idInstance, dest, BackupGrbit.None, progress );
		}

		/// <summary>Restore a streaming backup.</summary>
		/// <param name="src">Source backup folder</param>
		/// <param name="settings"></param>
		public static void StreamingRestore( string src, EsentDatabase.Settings settings )
		{
			using( EseSerializer ser = new EseSerializer( settings, 0 ) )
				Api.JetRestoreInstance( ser.idInstance, src, settings.databasePath, progress );
		}

		static JET_err progress( JET_SESID sesid, JET_SNP snp, JET_SNT snt, object data )
		{
			return JET_err.Success;
		}
	}
}