using System;
using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding <see cref="Guid" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'Guid' or 'Guid?'.</para>
	/// <para>The underlying ESENT column type is JET_coltypGUID on Vista and above, or JET_coltypBinary on 2003 and below.</para>
	/// </remarks>
	public class EseGuidAttribute : OptionallyNullableColumn
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property, BTW.</summary>
		public EseGuidAttribute() { }

		/// <summary>Initialize with non-default column name.</summary>
		public EseGuidAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = base.getColumnDef();
			if ( EsentVersion.SupportsVistaFeatures )
			{
				res.coltyp = Microsoft.Isam.Esent.Interop.Vista.VistaColtyp.GUID;
			}
			else
			{
				res.coltyp = JET_coltyp.Binary;
				res.cbMax = 16;
			}
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			verifyBasicTypeSupport<Guid>( t );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( serializeNull( cur, idColumn, value ) ) return;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, (Guid)value );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			Guid? res = Api.RetrieveColumnAsGuid( cur.idSession, cur.idTable, idColumn );
			if( !bFieldNullable )
				return res.Value;
			return res;
		}

		/// <summary>Make the search key for this column.</summary>
		/// <remarks><b>NB!</b> The native GUID columns are only supported by ESENT shipped with Vista and above.
		/// You'll get different sort order between Server 2003 and Vista.
		/// On Server 2003 and below, the sort order will be just memcmp() of the GUID's 16 bytes, while on Vista and above the sort order will be OK.</remarks>
		public override void MakeKey( EseCursorBase cur, object value, MakeKeyGrbit flags )
		{
			if( m_bFieldNullable && value == null )
				Api.MakeKey( cur.idSession, cur.idTable, null, flags );
			else if( value is Guid )
				Api.MakeKey( cur.idSession, cur.idTable, (Guid)value, flags );
			else
				makeKeyException( value );
		}
	}
}