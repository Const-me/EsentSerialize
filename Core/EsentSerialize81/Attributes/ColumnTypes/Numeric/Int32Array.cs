using Microsoft.Isam.Esent.Interop;
using System;
using System.Runtime.Serialization;

namespace EsentSerialization.Attributes
{
	/// <summary>The column holding array of <see cref="int" />.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'int[]'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLong.</para>
	/// </remarks>
	public class EseInt32ArrayAttribute : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseInt32ArrayAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseInt32ArrayAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Is always false.</summary>
		public override bool bFieldNullable { get { return false; } }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.coltyp = JET_coltyp.Long;
			res.grbit |= ColumndefGrbit.ColumnMultiValued;
			res.grbit |= ColumndefGrbit.ColumnTagged;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( !t.Equals( typeof( int[] ) ) )
				throw new SerializationException();
		}

		// Append the given value to this column
		void AddValue( EseCursorBase cur, JET_COLUMNID idColumn, int val )
		{
			byte[] data = BitConverter.GetBytes( val );
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

		// Get the multiple values as the int[]
		int[] GetValues( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			int nValues = GetValuesCount(cur, idColumn);
			var res = new int[ nValues ];
			if( 0 == nValues )
				return res;

			byte[] buff = new byte[ sizeof( int ) ];
			JET_RETINFO ri = new JET_RETINFO();
			for( int itg = 1; itg <= nValues; itg++ )
			{
				ri.itagSequence = itg;
				int cbSize;
				Api.JetRetrieveColumn( cur.idSession, cur.idTable, idColumn,
					buff, buff.Length, out cbSize, RetrieveColumnGrbit.None, ri );
				if( cbSize != buff.Length )
					throw new SerializationException();
				res[ itg - 1 ] = BitConverter.ToInt32( buff, 0 );
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
			}

			var arr = value as int[];
			if( null == arr ) return;

			// Set new values
			foreach( var s in arr )
				AddValue( cur, idColumn, s );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			return GetValues( cur, idColumn );
		}
	}
}