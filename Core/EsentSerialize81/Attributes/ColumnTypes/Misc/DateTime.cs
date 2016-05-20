using System;
using System.Runtime.Serialization;
using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>A DateTime column that holds int64 number with 100-nanoseconds ticks.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'DateTime' or 'DateTime?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypCurrency holding the FILETIME value, i.e. 64-bit value representing the number of 100-nanosecond intervals since January 1, 1601 UTC.</para>
	/// </remarks>
	public class EseDateTimeAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseDateTimeAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseDateTimeAttribute( string _columnName ) : base( _columnName ) { }

		public DateTimeKind kind { get; set; } = DateTimeKind.Utc;

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
			verifyBasicTypeSupport<DateTime>( t );
		}

		long getTicks( DateTime val )
		{
			if( val.Kind == kind || kind == DateTimeKind.Unspecified )
				return val.Ticks;

			if( kind == DateTimeKind.Utc )
				return val.ToUniversalTime().Ticks;

			if( kind == DateTimeKind.Local )
				return val.ToLocalTime().Ticks;

			throw new SerializationException( "Unexpected DateTimeKind value" );
		}

		DateTime getDateTime( long ticks )
		{
			return new DateTime( ticks, kind );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			DateTime dt = (DateTime)( value );
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, getTicks( dt ) );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			long? val = Api.RetrieveColumnAsInt64( cur.idSession, cur.idTable, idColumn );
			if( null == val )
			{
				if( bFieldNullable ) return null;
				throw new SerializationException( "The column is marked 'ColumnNotNULL', however the NULL value was pulled from the database." );
			}
			return getDateTime( val.Value );
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( val == null )
				Api.MakeKey( cur.idSession, cur.idTable, null, flags );
			else if( val is DateTime )
				Api.MakeKey( cur.idSession, cur.idTable, getTicks( (DateTime)val ), flags );
			else
				makeKeyException( val );
		}
	}
}