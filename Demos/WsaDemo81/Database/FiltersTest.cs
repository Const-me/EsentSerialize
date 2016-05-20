using EsentSerialization.Attributes;

namespace WsaDemo81.Database
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
	}
}