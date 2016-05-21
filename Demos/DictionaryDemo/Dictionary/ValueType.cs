using System;
using System.Linq;
using System.Runtime.Serialization;

namespace DictionaryDemo
{
	[DataContract]
	class SomeNode
	{
		[DataMember]
		byte[] data;

		public static SomeNode initRandom( Random r )
		{
			SomeNode res = new SomeNode();
			res.data = new byte[ 1024 * 1024 ];
			r.NextBytes( res.data );
			return res;
		}
	}

	[DataContract]
	class ValueType
	{
		[DataMember]
		public string myObjectMemeber { get; set; }

		[DataMember]
		SomeNode[] nodes;

		public void initRandom()
		{
			Random r = new Random();
			nodes = Enumerable.Range( 0, 90 )
				.Select( i => SomeNode.initRandom( r ) ).ToArray();
			myObjectMemeber = "Huge val";
		}
	}
}