using System;

namespace EsentSerialization
{
	static class MiscUtils
	{
		public static string formatWith( this string str, params object[] args )
		{
			return String.Format( str, args );
		}
	}
}