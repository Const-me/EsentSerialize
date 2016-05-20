using System;
using Microsoft.Isam.Esent.Interop;
using System.Runtime.Serialization;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="double" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'double' or 'double?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypIEEEDouble with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public class EseDoubleAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseDoubleAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseDoubleAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			res.coltyp = JET_coltyp.IEEEDouble;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<double>( t );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			double v = (double)( value );
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, v );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			double? res = Api.RetrieveColumnAsDouble( cur.idSession, cur.idTable, idColumn );
			if( null == res )
			{
				if( bFieldNullable ) return null;
				throw new SerializationException( "The column is marked 'ColumnNotNULL', however the NULL value was pulled from the database." );
			}
			return res.Value;
		}
	}
}