using System;
using System.Collections.Generic;

namespace EsentSerialization
{
	static class ByteArray
	{
		public static int CompareTo( this byte[] a, byte[] b )
		{
			if( null == a )
				return ( null == b ) ? 0 : -1;
			if( null == b )
				return 1;
			int len = Math.Min( a.Length, b.Length );
			for( int i = 0; i < len; i++ )
			{
				int res = a[ i ].CompareTo( b[ i ] );
				if( 0 != res )
					return res;
			}
			return a.Length.CompareTo( b.Length );
		}

		public static int ComputeHash( this byte[] data )
		{
			// http://stackoverflow.com/a/468084/126995
			unchecked
			{
				const int p = 16777619;
				int hash = (int)2166136261;

				for( int i = 0; i < data.Length; i++ )
					hash = ( hash ^ data[ i ] ) * p;

				hash += hash << 13;
				hash ^= hash >> 7;
				hash += hash << 3;
				hash ^= hash >> 17;
				hash += hash << 5;
				return hash;
			}
		}

		public static readonly byte[] Empty = new byte[0];

		class ByteArrayComparer : IEqualityComparer<byte[]>
		{
			bool IEqualityComparer<byte[]>.Equals( byte[] x, byte[] y )
			{
				return 0 == x.CompareTo( y );
			}

			int IEqualityComparer<byte[]>.GetHashCode( byte[] obj )
			{
				return obj.ComputeHash();
			}
		}

		public static readonly IEqualityComparer<byte[]> EqualityComparer = new ByteArrayComparer();

		public static bool isEmpty( this byte[] arr )
		{
			return null == arr || arr.Length <= 0;
		}
	}
}