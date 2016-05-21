using System.Xml;

namespace DictionaryDemo
{
	// This class optimizes storage size of the ValueType, by providing a pre-built XML dictionary.
	// When EseDCS attribute is supplied with the right dictionary, the storage overhead for data contract serialization is lowered dramatically.
	// Supplying XmlDictionary is completely optional: is no DictionaryType is specified in the [EseDCS] attribute, raw strings will be stored in the database inatead of integer keys.
	// See this article for more details on Microsoft's Binary XML format, and why it's superior to any other methods of serialization:
	// http://const.me/articles/net-tcp/
	class ValueTypeDictionary: XmlDictionary
	{
		public ValueTypeDictionary()
		{
			base.Add( "ValueType" );
			base.Add( "http://schemas.datacontract.org/2004/07/DictionaryDemo" );
			base.Add( "http://www.w3.org/2001/XMLSchema-instance" );
			base.Add( "myObjectMemeber" );
			base.Add( "nodes" );
			base.Add( "SomeNode" );
			base.Add( "data" );
			base.Add( "nil" );
		}

		public override bool TryLookup( XmlDictionaryString value, out XmlDictionaryString result )
		{
			return base.TryLookup( value.Value, out result );
		}
	}
}