using EsentSerialization.Attributes;
using SQLite;

namespace PerfVsSqlite.Database
{
	/// <summary>The record class, mapped to both ESENT and SQLite tables.</summary>
	[Table( "RECORDS" )]
	[EseTable]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	[EseSimpleIndex( "randomInt" )]
	class Record
	{
		[EseAutoId]
		[PrimaryKey, AutoIncrement]
		public int id { get; set; }

		[EseShortText( maxChars = 64, bUnicode = false )]
		[Column( "shortString" ), MaxLength( 64 )]
		public string shortString { get; set; }

		[EseInt]
		[Column( "randomInt" ), Indexed]
		public int randomInt { get; set; }
	}
}