using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>Column containing long text, either ASCII of Unicode.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'string'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLongText, either Unicode (which is the default), or ASCII.</para>
	/// <para>Please spare the memory manager, don't use it for columns over a few kilobytes:
	/// the whole value resides in RAM being an instance of System.String class.</para>
	/// </remarks>
	public sealed class EseLongTextAttribute : EseTextFieldBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseLongTextAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseLongTextAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = getTextBaseColumnDef();
			res.coltyp = JET_coltyp.LongText;
			res.grbit |= ColumndefGrbit.ColumnTagged;
			return res;
		}
	}
}