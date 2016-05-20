using System;
using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="bool" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'bool' or 'bool?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypBit with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseBoolAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseBoolAttribute() { }

		/// <summary>Initialize with non-default column name.</summary>
		public EseBoolAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<bool>( t );
		}

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.Bit;
			return res;
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) )
				return;
			bool bValue = Convert.ToBoolean( value );
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, bValue );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			bool? res = Api.RetrieveColumnAsBoolean( cur.idSession, cur.idTable, idColumn );
			if( !bFieldNullable ) return res.Value;
			return res;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( m_bFieldNullable && val == null )
				Api.MakeKey( cur.idSession, cur.idTable, null, flags );
			else
				Api.MakeKey( cur.idSession, cur.idTable, Convert.ToBoolean( val ), flags );
		}
	}
}