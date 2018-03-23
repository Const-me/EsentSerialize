using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization.Attributes
{
	/// <summary>Automatically-incremented row version column.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'int'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLong, with JET_bitColumnFixed and JET_bitColumnVersion flags.</para>
	/// </remarks>
	public sealed class EseVersionAttribute : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseVersionAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseVersionAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.coltyp = JET_coltyp.Long;
			res.grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnVersion;
			return res;
		}

		/// <summary>Always false.</summary>
		public override bool bFieldNullable { get { return false; } }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( t.Equals( typeof( int ) ) )
				return;
			throw new System.Runtime.Serialization.SerializationException();
		}

		/// <summary>Do nothing, as the value is set by the ESENT.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			return;
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			return Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, idColumn ).Value;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			Api.MakeKey( cur.idSession, cur.idTable, Convert.ToInt32( val ), flags );
		}
	}
}