using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Shared
{
	[DataContract]
	public class PersonMessage
	{
		[DataMember]
		public int id;

		public enum eSex { Male, Female, Other };
		[DataMember]
		public eSex sex;

		[DataMember]
		public string name;

		[DataMember]
		public List<string> phones;

		public PersonMessage() { }

		public PersonMessage( eSex _sex, string _name, IEnumerable<string> _phones )
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