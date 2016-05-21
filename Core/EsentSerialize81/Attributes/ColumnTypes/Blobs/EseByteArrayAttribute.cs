using System;
using System.IO;
using Microsoft.Isam.Esent.Interop;
using System.Runtime.Serialization;

namespace EsentSerialization.Attributes
{
	/// <summary>Column containing arbitrary binary data.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type 'byte[]'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLongBinary.</para>
	/// <para>Don't use it with items over few kilobytes: the whole value resides in RAM when deserialized.
	/// If you ever need handling megabytes-sized binary fields, use <see cref="EseBinaryStreamAttribute"/> instead.</para>
	/// </remarks>
	public class EseByteArrayAttribute : EseColumnAttrubuteBase
	{
		/// <summary>The maximum length, in bytes, of a column.</summary>
		public int maxBytes = 1024 * 4;

		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseByteArrayAttribute() { maxBytes = 0; }
		/// <summary>Initialize with non-default column name.</summary>
		public EseByteArrayAttribute( string _columnName ) : base( _columnName ) { maxBytes = 0; }

		/// <summary>The maximum length, in KiloBytes, of a column.</summary>
		public double maxKB
		{
			get { return (double)( maxBytes ) / 1024.0; }
			set
			{
				if( value <= 0 || value > ( int.MaxValue / 1024 ) )
					throw new ArgumentOutOfRangeException( "value" );
				maxBytes = (int)( value * 1024.0 );
			}
		}

		/// <summary>The maximum length, in MegaBytes, of a column.</summary>
		public double maxMB
		{
			get { return maxKB / 1024.0; }
			set { maxKB = value * 1024.0; }
		}

		/// <summary>Always true.</summary>
		public override bool bFieldNullable { get { return true; } }

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.coltyp = JET_coltyp.LongBinary;
			res.grbit = ColumndefGrbit.ColumnTagged;
			res.cbMax = maxBytes;
			return res;
		}

		/// <summary>Throw an exception if this attribute is not compatible with the type of the field/property.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			if( !typeof( byte[] ).Equals( t ) )
				throw new SerializationException( "The type must be 'byte[]'" );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			Api.SetColumn( cur.idSession, cur.idTable, idColumn, (byte[])value );
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			byte[] bytes = Api.RetrieveColumn( cur.idSession, cur.idTable, idColumn );
			return bytes;
		}
	}
}