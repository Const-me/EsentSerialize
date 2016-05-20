using System;
using System.Runtime.InteropServices;

namespace EsentSerialization.Utils.FileIO
{
	static class NativeMethods
	{
		const string file121 = "api-ms-win-core-file-l1-2-1.dll";

		enum FINDEX_INFO_LEVELS
		{
			FindExInfoStandard,
			FindExInfoBasic,
			FindExInfoMaxInfoLevel
		};

		enum FINDEX_SEARCH_OPS
		{
			FindExSearchNameMatch,
			FindExSearchLimitToDirectories,
			FindExSearchLimitToDevices
		};

		[Flags]
		enum FINDEX_ADDITIONAL_FLAGS : uint
		{
			None = 0,
			FIND_FIRST_EX_CASE_SENSITIVE = 1,
			FIND_FIRST_EX_LARGE_FETCH = 2,
		}

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		static extern SafeSearchHandle FindFirstFileExW( [MarshalAs( UnmanagedType.LPWStr )] string lpFileName,
			FINDEX_INFO_LEVELS fInfoLevelId, out WIN32_FIND_DATA lpFindFileData,
			FINDEX_SEARCH_OPS fSearchOp, IntPtr lpSearchFilter, FINDEX_ADDITIONAL_FLAGS dwAdditionalFlags );

		public static SafeSearchHandle FindFirstFile( string lpFileName, out WIN32_FIND_DATA lpFindFileData )
		{
			return FindFirstFileExW( lpFileName, FINDEX_INFO_LEVELS.FindExInfoBasic, out lpFindFileData,
				FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.None );
		}

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool FindNextFileW( SafeSearchHandle hFindFile, out WIN32_FIND_DATA lpFindFileData );

		[DllImport( file121, SetLastError = true, CharSet = CharSet.Unicode )]
		[return: MarshalAs( UnmanagedType.Bool )]
		public static extern bool FindClose( IntPtr hFindFile );
	}
}