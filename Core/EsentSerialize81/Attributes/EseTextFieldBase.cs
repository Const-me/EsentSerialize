using Microsoft.Isam.Esent.Interop;
using System;
using System.Runtime.Serialization;
using System.Text;

namespace EsentSerialization.Attributes
{
	/// <summary>Base class for text attributes.</summary>
	public abstract class EseTextFieldBase : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property, BTW.</summary>
		public EseTextFieldBase() { }

		/// <summary>Initialize with non-default column name.</summary>
		public EseTextFieldBase( string strName ) : base( strName ) { }

		bool m_bUnicode = true;
		/// <summary>Is this column stores the Unicode text?</summary>
		public bool bUnicode
		{
			get { return m_bUnicode; }
			set { m_bUnicode = value; }
		}

		static readonly Encoding nonUnicodeEncoding = new UTF8Encoding( false, true );

		/// <summary>Get the System.Text.Encoding to use.</summary>
		/// <returns>Either Unicode or ASCII.</returns>
		protected System.Text.Encoding getEncoding()
		{
			return m_bUnicode ? Encoding.Unicode : nonUnicodeEncoding;
		}

		/// <summary>Maximum value length, in characters.</summary>
		protected int m_maxChars = 0;

		/// <summary>Maximum value length, in characters.</summary>
		public int maxChars
		{
			get { return m_maxChars; }
			set
			{
				int iMax = ( bUnicode ) ? 127 : 255;
				if( value > iMax ) throw new ArgumentOutOfRangeException();
				m_maxChars = value;
				return;
			}
		}

		/// <summary>True if this column is nullable.</summary>
		public bool allowNulls = true;

		/// <summary></summary>
		protected JET_COLUMNDEF getTextBaseColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.cp = bUnicode ? JET_CP.Unicode : JET_CP.ASCII;
			res.cbMax = bUnicode ? m_maxChars * 2 : m_maxChars;

			if( !allowNulls )
				res.grbit |= ColumndefGrbit.ColumnNotNULL;
			return res;
		}

		/// <summary></summary>
		public override eColumnKind getColumnKind()
		{
			return m_bUnicode ? eColumnKind.Unicode : eColumnKind.ASCII;
		}

		/// <summary>Make the search key for this column.</summary>
		/// <param name="cur">The cursor to create the key on.</param>
		/// <param name="val">This object must be a string, otherwise an exception will be thrown.</param>
		/// <param name="flags">Key options.</param>
		public override void MakeKey( EseCursorBase cur, object val, MakeKeyGrbit flags )
		{
			if( val is string )
				Api.MakeKey( cur.idSession, cur.idTable, val as string, getEncoding(), flags );
			else
				makeKeyException( val );
		}

		/// <summary>Is always true, since the strings in C# are nullable.</summary>
		public override bool bFieldNullable { get { return true; } }

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
}