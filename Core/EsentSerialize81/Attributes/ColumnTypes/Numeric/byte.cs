using Microsoft.Isam.Esent.Interop;
using System;
using System.Reflection;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="byte" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'byte' or 'byte?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypUnsignedByte with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseByteAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseByteAttribute() { }

		/// <summary>Initialize with non-default column name.</summary>
		public EseByteAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<byte>( t );
		}

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.UnsignedByte;
			return res;
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) )
				return;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, Convert.ToByte( value ) );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			byte? res = Api.RetrieveColumnAsByte( cur.idSession, cur.idTable, idColumn );
			if( !bFieldNullable ) return res.Value;
			return res;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( makeNullKey( cur, val, flags ) ) return;
			if( !val.GetType().GetTypeInfo().IsPrimitive ) makeKeyException( val );
			Api.MakeKey( cur.idSession, cur.idTable, Convert.ToByte( val ), flags );
		}
	}
}