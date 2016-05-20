using Microsoft.Isam.Esent.Interop;
using System;
using System.Reflection;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="uint" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'uint' or 'uint?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLong with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseUintAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseUintAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseUintAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>If true, specifies that a column is an escrow update column.</summary>
		/// <remarks>
		/// <para>An escrow update column can be updated concurrently
		/// by different sessions with Api.EscrowUpdate and will maintain transactional consistency.</para>
		/// <para>The default is false.</para>
		/// </remarks>
		public bool bEscrowUpdate = false;

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.Long;
			if( this.bEscrowUpdate )
				res.grbit |= ColumndefGrbit.ColumnEscrowUpdate;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<uint>( t );
		}

		static int convert( object obj )
		{
			uint u = Convert.ToUInt32( obj );
			int i = unchecked( (int)u );
			return i;
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) )
				return;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, convert( value ) );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			int? res = Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, idColumn );
			if( !bFieldNullable || res.HasValue )
				return unchecked((uint)res.Value);
			return null;
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( makeNullKey( cur, val, flags ) ) return;

			if( val.GetType().GetTypeInfo().IsPrimitive )
				Api.MakeKey( cur.idSession, cur.idTable, convert( val ), flags );
			else
				makeKeyException( val );
		}
	}
}