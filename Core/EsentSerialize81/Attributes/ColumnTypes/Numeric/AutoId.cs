using System;
using Microsoft.Isam.Esent.Interop;
using System.Diagnostics;

namespace EsentSerialization.Attributes
{
	/// <summary>Automatically-incremented integer column.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'int' or 'long'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLong or JET_coltypCurrency, with JET_bitColumnFixed and JET_bitColumnAutoincrement flags.</para>
	/// </remarks>
	public sealed class EseAutoIdAttribute : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseAutoIdAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseAutoIdAttribute( string _columnName ) : base( _columnName ) { }

		private JET_coltyp m_cp = JET_coltyp.Nil;

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.coltyp = m_cp;
			res.grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL | ColumndefGrbit.ColumnAutoincrement;
			return res;
		}

		/// <summary>Always false.</summary>
		public override bool bFieldNullable { get { return false; } }

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( t.Equals( typeof( int ) ) )
				m_cp = JET_coltyp.Long;
			else if( t.Equals( typeof( long ) ) )
				m_cp = JET_coltyp.Currency;
			else
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
			if( this.m_cp == JET_coltyp.Long )
				return Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, idColumn ).Value;
			if( this.m_cp == JET_coltyp.Currency )
				return Api.RetrieveColumnAsInt64( cur.idSession, cur.idTable, idColumn ).Value;
			throw new System.Runtime.Serialization.SerializationException();
		}

		/// <summary>Make the search key for this column.</summary>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( this.m_cp == JET_coltyp.Long )
				Api.MakeKey( cur.idSession, cur.idTable, Convert.ToInt32( val ), flags );
			else if( this.m_cp == JET_coltyp.Currency )
				Api.MakeKey( cur.idSession, cur.idTable, Convert.ToInt64( val ), flags );
			else
				throw new System.Runtime.Serialization.SerializationException();
		}

		/// <summary>Retrieve copy of the auto-incremented value.</summary>
		/// <remarks>See the <a href="http://managedesent.codeplex.com/wikipage?title=HowDoI" target="_blank" >"How do I?" page in managed ESENT wiki</a>,
		/// section "How Do I Retrieve an Auto-Increment Column Value?"</remarks>
		/// <param name="cur"></param>
		/// <param name="idColumn"></param>
		public object RetrieveCopy( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			if( this.m_cp == JET_coltyp.Long )
				return Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, idColumn, RetrieveColumnGrbit.RetrieveCopy ) ?? null;
			if( this.m_cp == JET_coltyp.Currency )
				return Api.RetrieveColumnAsInt64( cur.idSession, cur.idTable, idColumn, RetrieveColumnGrbit.RetrieveCopy ) ?? null;
			throw new System.Runtime.Serialization.SerializationException();
		}
	}
}