using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EsentSerialization.Attributes
{
	/// <summary>Column containing short text, either ASCII of Unicode.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'string'.
	/// The value can be up to 255 ASCII characters in length or 127 Unicode characters in length.</para>
	/// <para>The underlying ESENT column type is JET_coltypText, either Unicode (which is the default), or ASCII.</para>
	/// </remarks>
	public class EseShortTextAttribute : EseTextFieldBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseShortTextAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseShortTextAttribute( string strName ) : base( strName ) { }

		bool m_bFixed = false;
		/// <summary>True if the column will always use the same amount of space,
		/// regardless of how much data is stored in the column</summary>
		public bool _fixed
		{
			get { return m_bFixed; }
			set { m_bFixed = value; }
		}

		/// <summary>Is always true, since the strings in C# are nullable.</summary>
		public override bool bFieldNullable { get { return true; } }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = getTextBaseColumnDef();

			res.coltyp = JET_coltyp.Text;

			if( bUnicode )
			{
				res.cbMax = m_maxChars * 2;
				if( m_maxChars > 127 )
					throw new ArgumentOutOfRangeException( "The Unicode text columns can't be larger then 127 characters.", (Exception)null );
			}
			else
			{
				res.cbMax = m_maxChars;
				if( m_maxChars > 255 )
					throw new ArgumentOutOfRangeException( "The ASCII text columns can't be larger then 255 characters.", (Exception)null );
			}

			if( m_bFixed )
				res.grbit |= ColumndefGrbit.ColumnFixed;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( !t.Equals( typeof( string ) ) ) throw new SerializationException();
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, value as string, getEncoding() );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			return Api.RetrieveColumnAsString( cur.idSession, cur.idTable, idColumn, getEncoding() );
		}
	}

	/// <summary>Column containing several string values, either ASCII of Unicode.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type <see cref="List{T}">List&lt;string&gt;</see>.
	/// Each value can be up to 255 ASCII characters in length, or 127 Unicode characters in length.</para>
	/// <para>The underlying ESENT column type is JET_coltypText, either Unicode (which is the default) or ASCII,
	/// with JET_bitColumnMultiValued and JET_bitColumnTagged flags.</para>
	/// </remarks>
	public sealed class EseMultiTextAttribute : EseTextFieldBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseMultiTextAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseMultiTextAttribute( string strName ) : base( strName ) { }

		const int s_cbMaxValueLength = 1024;

		/// <summary>Is always true.</summary>
		public override bool bFieldNullable { get { return true; } }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = getTextBaseColumnDef();

			res.coltyp = JET_coltyp.Text;

			if( bUnicode )
			{
				res.cbMax = m_maxChars * 2;
				if( m_maxChars > 127 )
					throw new ArgumentOutOfRangeException( "The unicode text columns can't be larger then 127 characters.", (Exception)null );
			}
			else
			{
				res.cbMax = m_maxChars;
				if( m_maxChars > 255 )
					throw new ArgumentOutOfRangeException( "The ASCII text columns can't be larger then 255 characters.", (Exception)null );
			}

			res.grbit |= ColumndefGrbit.ColumnMultiValued;
			res.grbit |= ColumndefGrbit.ColumnTagged;

			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( !t.Equals( typeof( List<string> ) ) )
				throw new SerializationException();
		}

		// Append the given value to this column
		void AddValue( EseCursorBase cur, JET_COLUMNID idColumn, string val )
		{
			byte[] data = getEncoding().GetBytes( val );
			JET_SETINFO si = new JET_SETINFO();
			si.itagSequence = 0;
			Api.JetSetColumn( cur.idSession, cur.idTable, idColumn, data, data.Length, SetColumnGrbit.None, si );
		}

		// Get the count of the values stored in the multi-valued column.
		int GetValuesCount( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			// See http://stackoverflow.com/questions/2929587 for more info
			JET_RETRIEVECOLUMN jrc = new JET_RETRIEVECOLUMN();
			jrc.columnid = idColumn;
			jrc.itagSequence = 0;
			Api.JetRetrieveColumns( cur.idSession, cur.idTable, new JET_RETRIEVECOLUMN[ 1 ] { jrc }, 1 );
			return jrc.itagSequence;
		}

		// Same as GetValuesCount, but no exception is ever thrown, instead zero is being silently returned.
		int GetValuesCountSilent( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			try
			{
				return GetValuesCount( cur, idColumn );
			}
			catch( System.Exception )
			{
				return 0;
			}
		}

		// Get the multiple values as the List<string>
		List<string> GetValues( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			int nValues = GetValuesCount( cur, idColumn );
			List<string> res = new List<string>( nValues );
			if( 0 == nValues )
				return res;

			byte[] buff = new byte[ s_cbMaxValueLength ];
			System.Text.Encoding enc = getEncoding();

			JET_RETINFO ri = new JET_RETINFO();
			for( int itg = 1; itg <= nValues; itg++ )
			{
				ri.itagSequence = itg;
				int cbSize;
				Api.JetRetrieveColumn( cur.idSession, cur.idTable, idColumn,
					buff, buff.Length, out cbSize, RetrieveColumnGrbit.None, ri );
				if( cbSize >= 0 )
					res.Add( enc.GetString( buff, 0, cbSize ) );
				else
					res.Add( null );
			}

			return res;
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			if( !bNewRecord )
			{
				// If this is an UPDATE operation, erase the old values.
				JET_SETINFO si = new JET_SETINFO();
				for( int itg = GetValuesCountSilent( cur, idColumn ); itg > 0; itg-- )
				{
					si.ibLongValue = 0;
					si.itagSequence = itg;
					Api.JetSetColumn( cur.idSession, cur.idTable, idColumn,
						null, 0, SetColumnGrbit.None, si );
				}
				si = null;
			}

			List<string> arr = value as List<string>;
			if( null == arr ) return;

			// Set new values
			foreach( string s in arr )
				AddValue( cur, idColumn, s );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			return GetValues( cur, idColumn );
		}
	}
}