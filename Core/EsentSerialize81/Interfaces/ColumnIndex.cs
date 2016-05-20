namespace EsentSerialization
{
	/// <summary>This readonly class returns index found for the column.</summary>
	public class IndexForColumn
	{
		/// <summary>True if that column is part of the primary index.</summary>
		public readonly bool primary;

		/// <summary>Index name</summary>
		public readonly string indexName;

		/// <summary>Index attribute</summary>
		public readonly Attributes.EseIndexAttribute attrib;

		/// <summary>Zero-based position of the column within the index, i.e. 0 if this is the first column in this index./</summary>
		public readonly int columnIndex;

		/// <summary>True if the direction specification is '+', false if '-'.</summary>
		public readonly bool indexDirectionPositive;

		internal IndexForColumn( bool primary, string indexName, Attributes.EseIndexAttribute attrib, int columnIndex, bool indexDirectionPositive )
		{
			this.primary = primary;
			this.indexName = indexName;
			this.attrib = attrib;
			this.columnIndex = columnIndex;
			this.indexDirectionPositive = indexDirectionPositive;
		}
	}
}