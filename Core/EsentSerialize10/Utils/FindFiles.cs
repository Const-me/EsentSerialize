using System.Collections.Generic;
using System.IO;

namespace EsentSerialization.Utils.FileIO
{
	static class FindFiles
	{
		public static IEnumerable<string> EnumerateAll( string dir )
		{
			return Directory.EnumerateFiles( dir, "*.*" );
		}
	}
}