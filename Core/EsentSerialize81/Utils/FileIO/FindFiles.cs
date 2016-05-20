using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EsentSerialization.Utils.FileIO
{
	static class FindFiles
	{
		const int ERROR_FILE_NOT_FOUND = 2;
		const int ERROR_NO_MORE_FILES = 18;

		/// <summary>If last Win32 error is ERROR_NO_MORE_FILES do nothing, otherwise throw.</summary>
		static void throwIfNeeded( string e )
		{
			int res = Marshal.GetLastWin32Error();
			if( 0 == res || res == ERROR_NO_MORE_FILES || res == ERROR_FILE_NOT_FOUND )
				return;
			throw new COMException( e, Marshal.GetHRForLastWin32Error() );
		}

		/// <summary>Enumerate files in directory.</summary>
		static IEnumerable<WIN32_FIND_DATA> findImpl( string path, string searchPattern )
		{
			string fileName = Path.Combine( path, searchPattern );
			WIN32_FIND_DATA fd;
			using( SafeSearchHandle h = NativeMethods.FindFirstFile( fileName, out fd ) )
			{
				if( h.IsInvalid )
				{
					throwIfNeeded( "FindFirstFileExW failed" );
					yield break;
				}

				do
				{
					if( fd.cFileName == "." || fd.cFileName == ".." )
						continue;
					yield return fd;
				}
				while( NativeMethods.FindNextFileW( h, out fd ) );

				throwIfNeeded( "FindNextFileW failed" );
				yield break;
			}
		}

		/// <summary>Enumerate files in a directory, ordered by creation date</summary>
		public static IEnumerable<Tuple<DateTime, string>> EnumerateFiles( string dir, string searchPattern )
		{
			return findImpl( dir, searchPattern )
				.Select( fd => Tuple.Create( fd.ftCreationTime.dateTime(), Path.Combine( dir, fd.cFileName ) ) )
				.OrderBy( t => t.Item1 );
		}

		/// <summary>Enumerate all files in the directory.</summary>
		public static IEnumerable<string> EnumerateAll( string dir )
		{
			return findImpl( dir, "*.*" )
				.Select( fd => Path.Combine( dir, fd.cFileName ) );
		}
	}
}