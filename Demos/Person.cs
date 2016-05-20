using EsentSerialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Database
{
	[EseTable( "persons" )]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	[EseIndex( "sex", "+sex\0\0" )]
	[EseTupleIndex( "name" )]
	[EseIndex( "phones", "+phones\0\0" )]
	public class Person
	{
		// Sometimes it's nice to have an integer person ID, instead of just bookmarks.
		// Bookmarks are byte arrays, they take memory allocations to deal with.
		// Fortunately, ESENT provides auto-incremented columns, which do great being a primary index, BTW.
		[EseAutoId]
		private int id;

		// Enum column
		public enum eSex { Male, Female, Other };
		[EseEnum]
		public eSex sex { get; private set; }

		// Short Unicode text column.
		[EseShortText( maxChars = 71 )]
		// See http://stackoverflow.com/a/30509/126995 for why "71"
		public string name;

		// Multi-values ASCII text column.
		[EseMultiText( bUnicode = false, maxChars = 32 )]
		public List<string> phones;

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