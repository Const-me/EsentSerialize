using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace EsentSerialization.Utils.FileIO
{
	[StructLayout( LayoutKind.Sequential )]
	struct WIN32_FILE_ATTRIBUTE_DATA
	{
		public FileAttributes dwFileAttributes;
		public FILETIME ftCreationTime;
		public FILETIME ftLastAccessTime;
		public FILETIME ftLastWriteTime;
		public uint nFileSizeHigh;
		public uint nFileSizeLow;
	}
}