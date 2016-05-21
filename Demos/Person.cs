using EsentSerialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using EsentSerialization;

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
		[EseAutoId( "id" )]
		private int m_id;
		public int id { get { return m_id; } }

		// Enum column
		public enum eSex { Male, Female, Other };
		[EseEnum]
		public eSex sex { get; set; }

		// Short Unicode text column.
		[EseShortText( maxChars = 71 )]
		// See http://stackoverflow.com/a/30509/126995 for why "71"
		public string name { get; set; }

		// Multi-values ASCII text column.
		[EseMultiText( bUnicode = false, maxChars = 32 )]
		public List<string> phones { get; set; }

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

		static IEnumerable<Person> debugData()
		{
			Person[] personsTest = new Person[]
			{
				new Person( Person.eSex.Female, "Jenifer Smith", new string[0] ),
				new Person( Person.eSex.Male, "Konstantin", new string[]{ "+7 926 139 63 18" } ),
				new Person( Person.eSex.Male, "John Smith", new string[]{ "+1 800 123 4567", "+1 800 123 4568" } ),
				new Person( Person.eSex.Female, "Mary Jane", new string[]{ "555-1212" } ),
				new Person( Person.eSex.Other, "Microsoft", new string[]{ "+1 800 642 7676", "1-800-892-5234" } ),
			};
			return personsTest;
		}

		public static void populateWithDebugData( iSerializerSession sess )
		{
			Cursor<Person> curTest;
			sess.getTable( out curTest );

			using( var trans = sess.BeginTransaction() )
			{
				curTest.AddRange( debugData() );
				trans.Commit();
			}
		}
	}
}