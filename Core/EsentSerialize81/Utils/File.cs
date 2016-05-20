using EsentSerialization.Utils.FileIO;
using System.Runtime.InteropServices.ComTypes;

namespace System.IO
{
	static class File
	{
		public static bool Exists( string path )
		{
			return FileApi.fileExists( path );
		}

		public static bool Delete( string path )
		{
			return FileApi.fileDelete( path );
		}

		public static DateTime dateTime( this FILETIME ft )
		{
			unchecked
			{
				ulong ticks = (uint)ft.dwHighDateTime;
				ticks = ticks << 32;
				ticks = ticks | (uint)ft.dwLowDateTime;
				return DateTime.FromFileTimeUtc( (long)ticks );
			}
		}

		public static DateTime GetCreationTimeUtc( string path )
		{
			return FileApi.getFileTime( path, a => a.ftCreationTime ).dateTime();
		}

		public static DateTime GetLastAccessTimeUtc( string path )
		{
			return FileApi.getFileTime( path, a => a.ftLastAccessTime ).dateTime();
		}

		public static DateTime GetLastWriteTimeUtc( string path )
		{
			return FileApi.getFileTime( path, a => a.ftLastWriteTime ).dateTime();
		}
	}
}