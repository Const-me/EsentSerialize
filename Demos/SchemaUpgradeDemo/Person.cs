using System;
using System.Collections.Generic;
using System.Linq;
using EsentSerialization.Attributes;

namespace SchemaUpgradeDemo
{
	[EseTable( "persons" )]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	[EseIndex( "sex", "+sex\0\0" )]
	// [EseTupleIndex( "name" )]
	[EseSimpleIndex( "name", Obsolete = true )]
	public class Person
	{
		// Sometimes it's nice to have an integer person ID, instead of just bookmarks.
		// Bookmarks are byte arrays, takes a memory allocations to deal with.
		// Fortunately, ESENT provides auto-incremented columns, which do great being a primary index, BTW.
		[EseAutoId]
		private int id;

		// Enum column
		public enum eSex { Male, Female, Other };
		[EseEnum]
		private eSex sex;

		// Short Unicode text column.
		[EseShortText( maxChars = 71 )]
		// See http://stackoverflow.com/questions/30485//30509#30509 for why "71"
		private string name;

		// Multi-values ASCII text column.
		[EseMultiText( bUnicode = false, maxChars = 32 )]
		private List<string> phones;

		[EseLongText, Obsolete]
		public string note;

		public Person() { }

		public Person( eSex _sex, string _name, IEnumerable<string> _phones )
		{
			sex = _sex;
			name = _name;
			phones = _phones
				.Where( p => !String.IsNullOrEmpty( p ) )
				.ToList();
		}

		public override string ToString()
		{
			return String.Format( @"Person {{ id={0}, name=""{1}"", sex={2}, phones={3} }}",
				id, name, sex, String.Join( "; ", phones.ToArray() ) );
		}
	}
}
