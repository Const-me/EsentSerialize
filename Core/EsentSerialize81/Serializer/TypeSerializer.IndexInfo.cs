using EsentSerialization.Attributes;
using System;
using System.Linq;

namespace EsentSerialization
{
	public partial class TypeSerializer : iTypeSerializer
	{
		class sIndexInfo
		{
			public readonly Attributes.EseIndexAttribute attrib;
			public readonly string[] columnNames;
			public readonly bool[] columnDirections;
			public readonly EseColumnAttrubuteBase[] columns;
			public readonly bool hasObsoleteColumns;

			public sIndexInfo( Attributes.EseIndexAttribute _attrib, string[] columnNames, EseColumnAttrubuteBase[] _columns, bool obsoleteColumns )
			{
				attrib = _attrib;
				this.columnNames = columnNames;
				columns = _columns;
				this.hasObsoleteColumns = obsoleteColumns;

				string[] arrTokens = _attrib.strKey.Split( new char[ 1 ] { '\0' }, StringSplitOptions.RemoveEmptyEntries );
				columnDirections = arrTokens.Select( s => '+' == s[ 0 ] ).ToArray();
			}
		}
	}
}