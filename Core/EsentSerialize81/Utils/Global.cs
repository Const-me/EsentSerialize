using System;
using System.Runtime.Serialization;

namespace EsentSerialization
{
	static class Global
	{
#if( DEBUG )
		/// <summary>In debug builds we don't re-throw anything.
		/// Using all power of Visual Studio debugger right when it's needed makes the debugging fun again.
		/// Same behavior can be achieved by checking "Thrown" in Debug->Exceptions, however the .NET XML serializer used internally throws exceptions even while being constructed normally.</summary>
		public static void TryCatch( Action act, string exText ) { act(); }
#else
		/// <summary>In release builds, we properly stack the exceptions inside each other, to store them in the crash log.</summary>
		/// <param name="act"></param>
		/// <param name="exText"></param>
		public static void TryCatch( Action act, string exText ){ try { act(); } catch( System.Exception ex ) { throw new SerializationException( exText, ex ); } }
#endif
	}
}