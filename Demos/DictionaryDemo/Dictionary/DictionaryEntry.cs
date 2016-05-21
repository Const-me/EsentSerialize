using EsentSerialization.Attributes;

namespace DictionaryDemo
{
	[EseTable]
	[EsePrimaryIndex( "key", "+key\0\0" )]
	class DictionaryEntry
	{
		[EseShortText]
		public string key;

		[EseDCS( DictionaryType = typeof( ValueTypeDictionary ) )]
		public ValueType value;
	}
}