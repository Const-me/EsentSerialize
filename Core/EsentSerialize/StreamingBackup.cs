using Microsoft.Isam.Esent.Interop;
using System.IO;

namespace EsentSerialization
{
	public static partial class Backup
	{
		public static void StreamingBackup( this EseSerializer serializer, string dest )
		{
			if( !Directory.Exists( dest ) )
				Directory.CreateDirectory( dest );
			Api.JetBackupInstance( serializer.idInstance, dest, BackupGrbit.None, progress );
		}

		public static void StreamingRestore( string src, EsentDatabase.Settings settings )
		{
			using( EseSerializer ser = new EseSerializer( settings.databasePath ) )
				Api.JetRestoreInstance( ser.idInstance, src, settings.databasePath, progress );
		}

		static JET_err progress( JET_SESID sesid, JET_SNP snp, JET_SNT snt, object data )
		{
			return JET_err.Success;
		}
	}
}