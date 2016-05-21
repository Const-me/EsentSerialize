using Database;
using Shared;

namespace Server
{
	/// <summary>This static class converts records into messages, and vice versa.</summary>
	internal static class Converter
	{
		public static PersonMessage.eSex toMessage( this Person.eSex record )
		{
			return (PersonMessage.eSex)(int)record;
		}

		public static Person.eSex toRecord( this PersonMessage.eSex message )
		{
			return (Person.eSex)(int)message;
		}

		public static PersonMessage toMessage( this Person record )
		{
			return new PersonMessage()
			{
				id = record.id,
				sex = record.sex.toMessage(),
				name = record.name,
				phones = record.phones,
			};
		}

		public static Person toRecord( this PersonMessage message )
		{
			return new Person()
			{
				sex = message.sex.toRecord(),
				name = message.name,
				phones = message.phones,
			};
		}
	}
}