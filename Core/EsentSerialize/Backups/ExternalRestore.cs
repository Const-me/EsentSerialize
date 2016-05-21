using System;
using System.IO;
using System.IO.Compression;

namespace EsentSerialization
{
	public static partial class Backup
	{
		public static void ExternalRestore( Stream source, EsentDatabase.Settings settings )
		{
			string dir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "D" ) );
			Directory.CreateDirectory( dir );

			using( ZipArchive archive = new ZipArchive( source, ZipArchiveMode.Read ) )
				archive.ExtractToDirectory( dir );

			StreamingRestore( dir, settings );

			Directory.Delete( dir, true );
		}
	}
}