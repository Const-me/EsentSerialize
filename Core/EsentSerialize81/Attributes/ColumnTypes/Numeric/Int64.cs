using System;
using Microsoft.Isam.Esent.Interop;
using System.Diagnostics;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="long" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'long' or 'long?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypCurrency with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseInt64Attribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseInt64Attribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseInt64Attribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.Currency;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<long>( t );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, Convert.ToInt64( value ) );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			long? res = Api.RetrieveColumnAsInt64( cur.idSession, cur.idTable, idColumn );
			if( !m_bFieldNullable ) return res.Value;
			return res;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( makeNullKey( cur, val, flags ) ) return;
			Api.MakeKey( cur.idSession, cur.idTable, Convert.ToInt64( val ), flags );
		}
	}

	/// <summary>The column holding <see cref="ulong" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'ulong' or 'ulong?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypCurrency with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseUInt64Attribute : EseInt64Attribute
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseUInt64Attribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseUInt64Attribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<ulong>( t );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, Convert.ToUInt64( value ) );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			ulong? res = Api.RetrieveColumnAsUInt64( cur.idSession, cur.idTable, idColumn );
			if( !m_bFieldNullable ) return res.Value;
			return res;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( makeNullKey( cur, val, flags ) ) return;
			Api.MakeKey( cur.idSession, cur.idTable, Convert.ToUInt64( val ), flags );
		}
	}
}