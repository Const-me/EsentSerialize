using System;
using System.IO;
using System.IO.Compression;

namespace EsentSerialization
{
	public static partial class Backup
	{
		/// <summary>Restore the external backup.</summary>
		/// <remarks>The ZIP archive backup is first unpacked to a temporary folder, then ESENT restores the database.</remarks>
		/// <param name="source">Source backup stream</param>
		/// <param name="settings">Database settings</param>
		/// <param name="tempFolder">Temporary folder to unpack the archive. Can be null, in this case a new folder will be created in %TEMP%. This folder will be removed after the restore is complete.</param>
		public static void ExternalRestore( Stream source, EsentDatabase.Settings settings, string tempFolder = null )
		{
			if( String.IsNullOrWhiteSpace( tempFolder ) )
				tempFolder = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "D" ) );
			if( !Directory.Exists( tempFolder ) )
				Directory.CreateDirectory( tempFolder );

			using( ZipArchive archive = new ZipArchive( source, ZipArchiveMode.Read ) )
				archive.ExtractToDirectory( tempFolder );

			StreamingRestore( tempFolder, settings );

			Directory.Delete( tempFolder, true );
		}
	}
}