using System;
using System.Runtime.InteropServices;

namespace EsentSerialization.Utils.FileIO
{
	sealed class SafeSearchHandle : SafeHandle
	{
		private SafeSearchHandle() : base( IntPtr.Zero, true ) { }

		public SafeSearchHandle( IntPtr preexistingHandle, bool ownsHandle ) : base( IntPtr.Zero, ownsHandle )
		{
			SetHandle( preexistingHandle );
		}

		override protected bool ReleaseHandle()
		{
			return NativeMethods.FindClose( handle );
		}

		public override bool IsInvalid
		{
			get
			{
				return handle == IntPtr.Zero || handle == new IntPtr( -1 );
			}
		}
	}
}