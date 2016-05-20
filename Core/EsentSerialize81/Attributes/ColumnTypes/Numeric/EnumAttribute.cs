using Microsoft.Isam.Esent.Interop;
using System;
using System.Reflection;

namespace EsentSerialization.Attributes
{
	/// <summary>Enum-typed column.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'e' or 'e?', where e is an enum.</para>
	/// <para>The underlying ESENT column type is JET_coltypLong with JET_bitColumnFixed flag.</para>
	/// </remarks>
	public sealed class EseEnumAttribute : EseIntAttribute
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseEnumAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseEnumAttribute( string _columnName ) : base( _columnName ) { }

		Type m_enumType;
		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			TypeInfo ti = t.GetTypeInfo();
			if( ti.IsEnum )
			{
				// Simple enum type
				m_bFieldNullable = false;
				m_enumType = t;
			}
			else if( ti.IsGenericType
				&& t.GetGenericTypeDefinition().Equals( typeof( Nullable<> ) )
				&& Nullable.GetUnderlyingType( t ).GetTypeInfo().IsEnum )
			{
				// "Enum?" type
				m_bFieldNullable = true;
				m_enumType = Nullable.GetUnderlyingType( t );
			}
			else
				throw new System.Runtime.Serialization.SerializationException();

			Type tpUnderlying = Enum.GetUnderlyingType( m_enumType );
			if( tpUnderlying == typeof( long ) || tpUnderlying == typeof( ulong ) )
				throw new NotSupportedException( "64-bit enums aren't currently supported" );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			int? res = Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, idColumn );
			if( null == res ) return null;
			return Enum.ToObject( m_enumType, res );
		}
	}
}