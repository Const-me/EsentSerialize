using EsentSerialization.Attributes;
using System;

namespace QuickStart
{
	// The record class
	[EseTable]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	[EseTupleIndex( "text" )]
	class Record
	{
		[EseInt]
		public int id { get; private set; }

		[EseLongText]	// Long so we don't have to worry about that 127 Unicode characters limit.
		public string text;

		public Record() { }
		public Record( int i, string t )
		{
			id = i;
			text = t;
		}

		public override string ToString()
		{
			return String.Format( "{0}\t{1}", id, text );
		}
	}
}