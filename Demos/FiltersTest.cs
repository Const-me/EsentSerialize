using EsentSerialization;
using EsentSerialization.Attributes;
using System.Collections.Generic;

namespace Database
{
	[EseTable( "FiltersTest" )]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	[EseIndex( "1", "+c1\0+c2\0+c3\0\0" )]
	[EseIndex( "2", "+c2\0+c1\0+c3\0\0" )]
	[EseIndex( "3", "+c3\0+c2\0-c1\0\0" )]
	public class FiltersTest
	{
		[EseAutoId]
		int id;

		[EseByte]
		public byte c1;

		[EseByte]
		public byte c2;

		[EseByte]
		public byte c3;

		public override string ToString()
		{
			return string.Format( "[{0}]", id );
		}

		static IEnumerable<FiltersTest> debugData()
		{
			for( byte i = 0; i < 16; i++ )
				for( byte j = 0; j < 16; j++ )
					for( byte k = 0; k < 16; k++ )
						yield return new FiltersTest()
						{
							c1 = k,
							c2 = j,
							c3 = i
						};
		}

		public static void populateWithDebugData( iSerializerSession sess )
		{
			Cursor<FiltersTest> curTest;
			sess.getTable( out curTest );

			using( var trans = sess.BeginTransaction() )
			{
				curTest.AddRange( debugData() );
				trans.Commit();
			}
		}
	}
}