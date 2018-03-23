using System;
using System.Runtime.Serialization;
using Microsoft.Isam.Esent.Interop;

// This file defines 2 column types to store big integer values: one stores all 16 bytes of System.Decimal, another one holds 8 bytes of VT_CURRENCY value.

namespace EsentSerialization.Attributes
{
	/// <summary>This fixed length 16-bytes binary column stores an instance of <see cref="System.Decimal" /> structure.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'decimal' or 'decimal?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypBinary, fixed, 16 bytes long.</para>
	/// <para>Indexing over such columns is useless hence unsupported. If you need indexing, use [EseOacDecimal] instead.</para>
	/// </remarks>
	/// <seealso cref="EseOacDecimalAttribute" />
	public class EseDecimalAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseDecimalAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseDecimalAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.Binary;
			res.cbMax = 16;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<decimal>( t );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			int[] bits = Decimal.GetBits( (decimal)( value ) );

			byte[] byteArray = new byte[ 16 ];
			Array.Copy( BitConverter.GetBytes( bits[ 0 ] ), 0,  byteArray, 0, 4 );
			Array.Copy( BitConverter.GetBytes( bits[ 1 ] ), 4,  byteArray, 0, 4 );
			Array.Copy( BitConverter.GetBytes( bits[ 2 ] ), 8,  byteArray, 0, 4 );
			Array.Copy( BitConverter.GetBytes( bits[ 3 ] ), 12, byteArray, 0, 4 );

			Api.SetColumn( cur.idSession, cur.idTable, idColumn, byteArray );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			byte[] res = Api.RetrieveColumn( cur.idSession, cur.idTable, idColumn );
			if( null == res || 0 == res.Length )
			{
				if( bFieldNullable )
					return null;
				throw new SerializationException( "The column is marked 'ColumnNotNULL', however the NULL value was pulled from the database." );
			}
			if( 16 != res.Length )
				throw new SerializationException( "The column of type Decimal must contain exactly 16 bytes." );

			Int32[] bits = new Int32[ 4 ]
			{
				BitConverter.ToInt32(res, 0),
				BitConverter.ToInt32(res, 4),
				BitConverter.ToInt32(res, 8),
				BitConverter.ToInt32(res, 12),
			};

			return new Decimal( bits );
		}

		// No MakeKey API is possible, because the binary columns are sorted as memcmp,
		// which is completely useless with System.Decimal represented as Decimal.GetBits.
		// If you need to index over this column, use [EseOacDecimal] instead.
	}

	/// <summary>This column type holds a <see cref="System.Decimal" /> value stored in the "OLE Automation Currency" format.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'decimal' or 'decimal?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypCurrency with JET_bitColumnFixed flag.</para>
	/// <para>The "OLE Automation Currency" format is signed 64-bit integer holding ( value * 10000 ).<br />
	/// Unlike <see cref="EseDecimalAttribute" />, this allows indexing over such column, for the downside of degraded precision and range.</para>
	/// </remarks>
	/// <seealso cref="EseDecimalAttribute" />
	public class EseOacDecimalAttribute : EseInt64Attribute
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseOacDecimalAttribute() { }

		/// <summary>Initialize with non-default column name.</summary>
		public EseOacDecimalAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<decimal>( t );
		}

		static long toOac( decimal value )
		{
#if WINDOWS_APP || WINDOWS_UWP
			return Convert.ToInt64( Math.Round( value * 10000 ) );
#else
			return Decimal.ToOACurrency( value );
#endif
		}

		static decimal fromOac( long value )
		{
#if WINDOWS_APP || WINDOWS_UWP
			return Convert.ToDecimal( value ) * 0.0001m;
#else
			return Decimal.FromOACurrency( value );
#endif
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			long lv = toOac( Convert.ToDecimal( value ) );
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, lv );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			long? res = Api.RetrieveColumnAsInt64( cur.idSession, cur.idTable, idColumn );
			if( res.HasValue )
				return fromOac( res.Value );
			if( bFieldNullable )
				return null;
			throw new SerializationException( "The column is marked 'ColumnNotNULL', however the NULL value was pulled from the database." );
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object value, MakeKeyGrbit flags )
		{
			if( makeNullKey( cur, value, flags ) ) return;
			Api.MakeKey( cur.idSession, cur.idTable, toOac( Convert.ToDecimal( value ) ), flags );
		}
	}
}