using EsentSerialization.Utils.FileIO;

namespace System.IO
{
	static class Directory
	{
		public static bool Exists( string path )
		{
			return FileApi.directoryExists( path );
		}

		public static void CreateDirectory( string path )
		{
			FileApi.directoryCreate( path );
		}
	}
}