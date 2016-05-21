using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>This attribute defines a simple index attribute, that indexes only a single column in ascending order.</summary>
	/// <remarks>Index name is the same as the column's name.<br />
	/// This attribute has no effect unless the <see cref="EseTableAttribute" /> is also applied to this class.</remarks>
	public sealed class EseSimpleIndexAttribute : EseIndexAttribute
	{
		/// <summary>Declare the index.</summary>
		/// <param name="_strName">Both index name, and column name.</param>
		public EseSimpleIndexAttribute( string _strName ) :
			base( _strName, "+" + _strName + "\0\0", CreateIndexGrbit.IndexIgnoreNull )
		{ }
	}
}