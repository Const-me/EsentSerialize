using Microsoft.Isam.Esent.Interop;
using System;
using System.Text;

namespace EsentSerialization.Attributes
{
	/// <summary>Column kind enumeration is used for some checks.</summary>
	public enum eColumnKind
	{
		/// <summary>This column is binary.</summary>
		Binary,
		/// <summary>This column is Unicode text.</summary>
		Unicode,
		/// <summary>This column is ASCII text.</summary>
		ASCII
	}

	/// <summary>Abstract base class for ESE column attributes that defines record field types.</summary>
	/// <remarks>
	/// <para>The specific classes are in the <see cref="Attributes" /> namespace.</para>
	/// <para>If you need to, you can create your own column type attributes, by inheriting from this base class.</para>
	/// <para>If you'll also apply [Obsolete] attribute to this field/property, the column won't be normally created in the DB,
	/// only by <see cref="Microsoft.Isam.Esent.Serialization.DatabaseSchemaUpdater"/> class.</para>
	/// </remarks>
	[AttributeUsage( AttributeTargets.Field | AttributeTargets.Property )]
	public abstract class EseColumnAttrubuteBase : Attribute
	{
		/// <summary>Name for this column in the DB.</summary>
		public readonly string columnName = null;

		/// <summary>Get the column kind</summary>
		/// <returns></returns>
		public virtual eColumnKind getColumnKind() { return eColumnKind.Binary; }

		/// <summary>Get column definition.</summary>
		/// <returns></returns>
		public abstract JET_COLUMNDEF getColumnDef();

		/// <summary>Returns true if this column is nullable.</summary>
		public abstract bool bFieldNullable { get; }

		/// <summary>Is called by the framework to verify the attribute is compatible with the field type.
		/// The implementation must throw an exception if it's not.</summary>
		/// <param name="t">The type of the field/property the attribute is applied to.</param>
		public abstract void verifyTypeSupport( Type t );

		/// <summary>Store the column value in the database.</summary>
		/// <param name="cur">ESENT cursor.</param>
		/// <param name="idColumn">ID of the column.</param>
		/// <param name="value">The value to store.</param>
		/// <param name="bNewRecord">Is set to true if we're inserting a new record.
		/// Is set to false if we're updating an existing record.</param>
		public abstract void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord );

		/// <summary>Retrieve the column value from the DB.</summary>
		/// <param name="cur">ESENT cursor.</param>
		/// <param name="idColumn">ID of the column.</param>
		/// <returns>The column value for the current record.</returns>
		public abstract object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn );

		/// <summary>Throw new "MakeKey failed" exception.</summary>
		/// <param name="val">The incorrect key value supplied by the user.</param>
		protected void makeKeyException( object val )
		{
			StringBuilder msg = new StringBuilder();
			msg.Append( this.GetType().Name );
			msg.Append( ".MakeKey() failed: " );
			if( null == val )
				msg.Append( "this column is not nullable." );
			else
			{
				msg.Append( "key value of type '" );
				msg.Append( val.GetType().Name );
				msg.Append( "' is not supported." );
			}
			throw new NotSupportedException( msg.ToString() );
		}

		/// <summary>Make search key for this column.</summary>
		/// <remarks>This abstract base class always throws an exception instead of calling JetMakeKey API.</remarks>
		/// <param name="cur">ESENT cursor.</param>
		/// <param name="value">The key value supplied by the user.</param>
		/// <param name="flags">Bit flags for JetMakeKey.</param>
		public virtual void MakeKey( EseCursorBase cur, object value, MakeKeyGrbit flags )
		{
			makeKeyException( value );
		}

		/// <summary>Initialize with the default column name, which is the name of the field/property, BTW.</summary>
		protected EseColumnAttrubuteBase() { columnName = null; }

		/// <summary>Initialize with non-default column name.</summary>
		/// <param name="_columnName"></param>
		protected EseColumnAttrubuteBase( string _columnName ) { columnName = _columnName; }

		/// <summary>Try to store a null value.</summary>
		/// <param name="cur">ESENT cursor</param>
		/// <param name="idColumn">ID of the column</param>
		/// <param name="value">The value that might be null. If it's not null, this method silently returns false.</param>
		/// <returns>true if the field is nullable, and the value is null, and JetSetColumn was called.</returns>
		protected bool serializeNull( EseCursorBase cur, JET_COLUMNID idColumn, object value )
		{
			if( !bFieldNullable ) return false;
			if( value != null ) return false;
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, null );
			return true;
		}

		/// <summary>Try to make a null key.</summary>
		/// <param name="cur">ESENT cursor.</param>
		/// <param name="value">The value that might be null. If it's not null, this method silently returns false. If it is null but the column is not nullable, this method throws an exception.</param>
		/// <param name="flags">Key options.</param>
		/// <returns>true if the field is nullable, and the value is null, and the JetMakeKey was called.</returns>
		protected bool makeNullKey( EseCursorBase cur, object value, MakeKeyGrbit flags )
		{
			if( value != null ) return false;
			if( !bFieldNullable )
				makeKeyException( value );
			Api.MakeKey( cur.idSession, cur.idTable, null, flags );
			return true;
		}

		/// <summary>True when the column can contain multiple values per record.</summary>
		public bool isMultiValued { get { return getColumnDef().grbit.HasFlag( ColumndefGrbit.ColumnMultiValued ); } }
	}
}