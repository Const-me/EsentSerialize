using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>This attribute defines a tuple index.</summary>
	/// <remarks>The minimum tuple length is specified in the <see cref="EseSerializer.s_paramIndexTuplesLengthMin" /> field.
	/// The default setting is 3.<br />
	/// This attribute has no effect unless the <see cref="EseTableAttribute" /> is also applied to this class.</remarks>
	/// <seealso href="http://en.wikipedia.org/wiki/Extensible_Storage_Engine#Tuple_Indexes" >Tuple indices in wikipedia</seealso>
	public sealed class EseTupleIndexAttribute : EseIndexAttribute
	{
		/// <summary>Declare the index.</summary>
		/// <param name="_strName">Both index name, and column name.</param>
		public EseTupleIndexAttribute( string _strName ) :
			base( _strName, "+" + _strName + "\0\0", CreateIndexGrbit.IndexIgnoreNull )
		{ }

		/// <summary>Construct the JET_INDEXCREATE structure for this index.</summary>
		public override JET_INDEXCREATE getIndexDef()
		{
			JET_INDEXCREATE ic = base.getIndexDef();
			ic.ulDensity = 55;
			ic.grbit |= Ext.JET_bitIndexTuples;
			return ic;
		}
	}
}