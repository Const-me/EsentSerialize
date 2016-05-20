using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace EsentSerialization.Utils.FileIO
{
	/// <summary>Unfortunately, Microsoft neglected to implement blocking file API.
	/// Fortunately there's unmanaged interop, so I can build my own one.</summary>
	internal static class FileApi
	{
		const string file121 = "api-ms-win-core-file-l1-2-1.dll";

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		[return: MarshalAs( UnmanagedType.Bool )]
		static extern bool GetFileAttributesExW( [MarshalAs( UnmanagedType.LPWStr )] string lpFileName, GET_FILEEX_INFO_LEVELS fInfoLevelId, out WIN32_FILE_ATTRIBUTE_DATA fileData );

		enum GET_FILEEX_INFO_LEVELS
		{
			GetFileExInfoStandard,
			GetFileExMaxInfoLevel
		}

		static FileAttributes? getFileAttributes( string fileName )
		{
			if( string.IsNullOrEmpty( fileName ) )
				return null;

			WIN32_FILE_ATTRIBUTE_DATA fad;
			if( !GetFileAttributesExW( fileName, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, out fad ) )
				return null;

			return fad.dwFileAttributes;
		}

		/// <summary>Checks whether the specified file exists on the file system.</summary>
		public static bool fileExists( string path )
		{
			FileAttributes? a = getFileAttributes( path );
			if( !a.HasValue )
				return false;
			if( a.Value.HasFlag( FileAttributes.Directory ) )
				return false;
			if( a.Value.HasFlag( FileAttributes.Device ) )
				return false;
			return true;
		}

		static string dirName( string path )
		{
			if( null == path )
				return null;
			if( path.EndsWith( "/" ) || path.EndsWith( "\\" ) )
				return path.Substring( 0, path.Length - 1 );
			return path;
		}

		/// <summary>Checks whether the specified directory exists on the file system.</summary>
		public static bool directoryExists( string path )
		{
			path = dirName( path );
			FileAttributes? a = getFileAttributes( path );
			if( !a.HasValue )
				return false;
			return a.Value.HasFlag( FileAttributes.Directory );
		}

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		[return: MarshalAs( UnmanagedType.Bool )]
		static extern bool CreateDirectoryW( [MarshalAs( UnmanagedType.LPWStr )] string lpPathName, IntPtr lpSecurityAttributes );

		/// <summary>Create a directory</summary>
		/// <returns>True if the directory has been created OK, or if pre-existed.</returns>
		public static void directoryCreate( string path )
		{
			path = dirName( path );
			if( String.IsNullOrEmpty( path ) )
				throw new ArgumentNullException( "path" );

			FileAttributes? a = getFileAttributes( path );
			if( a.HasValue )
			{
				if( a.Value.HasFlag( FileAttributes.Directory ) )
					return;
				throw new Exception( "Unable to create a directory: there's a file with same name." );
			}
			if( CreateDirectoryW( path, IntPtr.Zero ) )
				return;
			throw new COMException( "Unable to create a directory", Marshal.GetHRForLastWin32Error() );
		}

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		[return: MarshalAs( UnmanagedType.Bool )]
		static extern bool DeleteFileW( [MarshalAs( UnmanagedType.LPWStr )] string lpFileName );

		/// <summary>Delete a file.</summary>
		/// <returns>False if not found</returns>
		public static bool fileDelete( string file )
		{
			if( !fileExists( file ) )
				return false;
			if( !DeleteFileW( file ) )
				throw new COMException( "Unable to delete a file", Marshal.GetHRForLastWin32Error() );
			return true;
		}

		public static FILETIME getFileTime( string fileName, Func<WIN32_FILE_ATTRIBUTE_DATA, FILETIME> which )
		{
			if( String.IsNullOrWhiteSpace( fileName ) )
				throw new ArgumentNullException( fileName );

			WIN32_FILE_ATTRIBUTE_DATA fad;
			if( !GetFileAttributesExW( fileName, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, out fad ) )
				throw new COMException( "GetFileAttributesExW failed", Marshal.GetHRForLastWin32Error() );

			return which( fad );
		}
	}
}