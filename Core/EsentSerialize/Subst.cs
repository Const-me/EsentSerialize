using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;

namespace EsentSerialize
{
	/// <summary>This utility class maps drive letters just like command line 'subst.exe'.</summary>
	/// <remarks><para>When ESENT is asked to open/create a database located in "A:\BBB\CCC\DDD",
	/// the database engine first scans the folders "A:", "A:\BBB" and "A:\BBB\CCC", looking for reparse points.</para>
	/// <para>If the user who's running the code has no permission to access any of those folders (which happenned on my shared Windows hosting),
	/// this behavior will result in EsentFileAccessDeniedException.</para>
	/// <para>This class provides a workaround for the issue.</para>
	/// <para>To do that, it uses the
	/// <see href="http://msdn.microsoft.com/en-us/library/aa363904(VS.85).aspx">DefineDosDevice</see> Win32 API, to mount "A:\BBB\CCC" as a DOS device "Z:".<br/>
	/// When ESENT is instructed to open/create a database located in Z:\DDD,
	/// it no longer tries to access A:\ and A:\BBB, and the database engine is initialized successfully.</para>
	/// </remarks>
	/// <seealso cref="Subst.Mount"/>
	public static class Subst
	{
		[DllImport( "kernel32.dll" )]
		static extern bool DefineDosDevice( uint dwFlags, string lpDeviceName, string lpTargetPath );

		const uint DDD_EXACT_MATCH_ON_REMOVE = 0x00000004;
		const uint DDD_NO_BROADCAST_SYSTEM = 0x00000008;
		const uint DDD_RAW_TARGET_PATH = 0x00000001;
		const uint DDD_REMOVE_DEFINITION = 0x00000002;

		[DllImport( "kernel32.dll" )]
		static extern uint QueryDosDevice( string lpDeviceName, IntPtr lpTargetPath, uint ucchMax );

		/// <summary>Find the free drive letter, and mount the specified path to that.</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		static string DefineDevice( string path )
		{
			if( !Directory.Exists( path ) )
				throw new ArgumentException( "The directory does not exists." );

			var hsUsedDrives =
				DriveInfo.GetDrives()
				.Select( di => di.Name.ToUpperInvariant()[ 0 ] )
				.ToHashSet();

			// Find free drive letter
			for( char c = 'Z'; c >= 'A'; c-- )
			{
				if( hsUsedDrives.Contains( c ) )
					continue;

				String lpDeviceName = String.Format( "{0}:", c );
				if( !DefineDosDevice( DDD_NO_BROADCAST_SYSTEM, lpDeviceName, path ) )
					Marshal.ThrowExceptionForHR( Marshal.GetLastWin32Error() );

				return lpDeviceName;
			}

			throw new ApplicationException( "Unable to find a free drive letter." );
		}

		/// <summary>Retrieves information about MS-DOS device names.
		/// The function can obtain the current mapping for a particular MS-DOS device name.
		/// The function can also obtain a list of all existing MS-DOS device names.</summary>
		/// <remarks></remarks>
		/// <param name="strDeviceName"></param>
		/// <returns></returns>
		static string[] QueryDevice( string strDeviceName )
		{
			// Allocate some memory to get a list of all system devices.
			// Start with a small size and dynamically give more space until we have enough room.
			uint returnSize = 0;
			uint maxSize = 100;
			string allDevices = null;
			IntPtr mem;
			string[] retval = null;

			const int ERROR_INSUFFICIENT_BUFFER = 122;

			while( returnSize == 0 )
			{
				mem = Marshal.AllocHGlobal( (int)maxSize );
				if( mem != IntPtr.Zero )
				{
					// mem points to memory that needs freeing
					try
					{
						returnSize = QueryDosDevice( strDeviceName, mem, maxSize );
						if( returnSize != 0 )
						{
							allDevices = Marshal.PtrToStringAnsi( mem, (int)returnSize );
							retval = allDevices.Split( '\0' );
							break;    // not really needed, but makes it more clear...
						}
						else if( Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER )
						{
							maxSize *= 10;
						}
						else
						{
							Marshal.ThrowExceptionForHR( Marshal.GetLastWin32Error() );
						}
					}
					finally
					{
						Marshal.FreeHGlobal( mem );
					}
				}
				else
				{
					throw new OutOfMemoryException();
				}
			}
			return retval;
		}

		static string ValidatePath( string path )
		{
			if( String.IsNullOrEmpty( path ) )
				throw new ArgumentException( "E_POINTER", "path" );
			if( path.EndsWith( @"\" ) )
				path = path.Substring( 0, path.Length - 1 );
			if( String.IsNullOrEmpty( path ) )
				throw new ArgumentException( "E_FAIL", "path" );
			if( !Directory.Exists( path ) )
				Directory.CreateDirectory( path );
			return path;
		}

		static string FindDosDevice( string path )
		{
			path = ValidatePath( path );
			path = path.ToLowerInvariant();

			// Search for the existing DOS device with the same path.
			foreach( var dev in QueryDevice( null ) )
			{
				if( String.IsNullOrEmpty( dev ) )
					continue;

				string sPath = QueryDevice( dev )[ 0 ].ToLowerInvariant();
				if( sPath == path )
					return dev;
				if( sPath == @"\??\" + path )
					return dev;
			}
			return null;
		}

		static string MountImpl( string path )
		{
			path = ValidatePath( path );

			string strFile, strDirectory;
			strFile = Path.GetFileName( path );
			strDirectory = Path.GetDirectoryName( path );

			// Search for the existing DOS device with the same path.
			string strFoundDev = FindDosDevice( strDirectory );

			string res;
			if( !string.IsNullOrEmpty( strFoundDev ) )
				res = strFoundDev;
			else
				res = DefineDevice( strDirectory );

			return Path.Combine( res, strFile );
		}

		/// <summary>Mount the folder using DefineDosDevice API (a.k.a. "subst").</summary>
		/// <remarks><para>If supplied "A:\BBB\CCC\DDD\", mounts "A:\BBB\CCC" as "Z:",
		/// and returns "Z:\DDD\" (where 'Z' if the last free drive letter).</para>
		/// <para>Before doing that however, this method looks for the existing subst'ed drive which reference "A:\BBB\CCC".
		/// If found, it will not mount anything; instead, it will return the path on the previously mounted drive.</para></remarks>
		/// <param name="path">The path to mount.</param>
		/// <returns>The path on the newly (or previously) mounted drive, which is the alias for the input path.</returns>
		public static string Mount( string path )
		{
			string res = MountImpl( path );
			if( !res.EndsWith( "\\" ) )
				res += '\\';
			return res;
		}

		/// <summary>Remove the DOS device previously mounted with <see cref="Mount" /> method.</summary>
		/// <remarks><b>NB:</b> This method is not implemented, and will always throw an exception.</remarks>
		/// <param name="strDriveLetter">The string e.g. "Y:" with the mounted drive letter.</param>
		public static void UnMount( string strDriveLetter )
		{
			throw new NotImplementedException();

			/* strDriveLetter = ValidatePath( strDriveLetter );

			if( !DefineDosDevice( DDD_REMOVE_DEFINITION, strDriveLetter, null ) )
				Marshal.ThrowExceptionForHR( Marshal.GetLastWin32Error() ); */
		}
	}
}