using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EsentSerialization
{
	static class TSV
	{
		/// <summary>Escape the input ASCII data.</summary>
		/// <param name="buff"></param>
		/// <param name="cbSize"></param>
		/// <returns>string where all the weird characters are escaped with \## </returns>
		public static string EscapeASCII( byte[] buff, int cbSize )
		{
			StringBuilder sb = new StringBuilder();
			foreach( var c in buff.Take( cbSize ) )
			{
				if( c == '\\' || c == '/' || c < 0x20 /* || ( c >=0x7F && c < 0xA0 ) */ )
				{
					sb.Append( '\\' );
					sb.Append( ( (int)c ).ToString( "X2" ) );
				}
				else
					sb.Append( (char)( c ) );
			}
			return sb.ToString();
		}

		// Below are the routines to convert the text data between ESENT column and TSV file.
		// Remember, the ESE strings may contain e.g. any count of \0, it doesn't have any associated encoding
		// (e.g. objects are XmlSerialized into ASCII columns in  UTF8 format),

		/// <summary>Unescape ASCII data</summary>
		/// <param name="chars"></param>
		/// <returns></returns>
		public static IEnumerable<byte> UnescapeASCII( IEnumerable<char> chars )
		{
			IEnumerator<char> enm = chars.GetEnumerator();

			while( true )
			{
				if( !enm.MoveNext() ) yield break;
				char c = enm.Current;
				if( c != '\\' )
				{
					yield return (byte)c;
					continue;
				}
				char[] cc = new char[ 2 ];
				if( !enm.MoveNext() ) yield break;
				cc[ 0 ] = enm.Current;
				if( !enm.MoveNext() ) yield break;
				cc[ 1 ] = enm.Current;

				int res = Convert.ToInt32( new String( cc, 0, 2 ), 16 );
				yield return (byte)( res );
			}
		}

		/// <summary>Escape USC2 Unicode data so it doesn't break the TSV file structure.</summary>
		/// <param name="buff"></param>
		/// <param name="cbSize"></param>
		/// <returns></returns>
		public static string EscapeUnicode( byte[] buff, int cbSize )
		{
			if( 0 != ( cbSize % 2 ) )
			{
				// throw new ArgumentException( "Odd number of bytes in the unicode string" );
				buff[ cbSize ] = 0;
				cbSize++;
			}

			StringBuilder sb = new StringBuilder();
			for( int i = 0; i < cbSize; i += 2 )
			{
				int c = buff[ i + 1 ];
				c = c << 8;
				c |= buff[ i ];

				if( c == '\\' || c == '/' || c < 0x20 /* || ( c >= 0x7F && c < 0xA0 ) */ )
				{
					sb.Append( '\\' );
					sb.Append( ( (int)c ).ToString( "X4" ) );
				}
				else
					sb.Append( (char)( c ) );
			}
			return sb.ToString();
		}

		/// <summary>Unescape Unicode text previously escaped with <see cref="EscapeUnicode" /></summary>
		/// <param name="chars"></param>
		/// <returns></returns>
		public static IEnumerable<byte> UnescapeUnicode( IEnumerable<char> chars )
		{
			IEnumerator<char> enm = chars.GetEnumerator();
			char[] cc = new char[ 4 ];

			while( true )
			{
				if( !enm.MoveNext() )
					yield break;
				int c = enm.Current;
				if( c == '\\' )
				{
					for( int i = 0; i < 4; i++ )
					{
						if( !enm.MoveNext() )
							yield break;
						cc[ i ] = enm.Current;
					}

					c = Convert.ToInt32( new String( cc, 0, 4 ), 16 );
				}

				yield return (byte)( c );
				yield return (byte)( c >> 8 );
			}
		}
	}
}