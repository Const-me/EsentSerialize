using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization.Attributes
{
	/// <summary>Column containing arbitrary binary data.</summary>
	/// <remarks>
	/// <para>Must be applied to a field/property of type '<see cref="EseStreamValue" />'.</para>
	/// <para>The underlying ESENT column type is JET_coltypLongBinary.</para>
	/// <para>Unlike the rest of the column types, this one does not save or load anything.<br />
	/// Instead, it only supplies the <see cref="EseStreamValue" /> value, that can be used later to save or load the data.<br />
	/// To access the data, you should use <see cref="EseStreamValue.Read" /> / <see cref="EseStreamValue.Write" /> methods.</para>
	/// <para>You must open a transaction before calling <see cref="EseStreamValue.Read" /> / <see cref="EseStreamValue.Write" /> methods,
	/// and you must keep the transaction open for the lifetime of the ColumnStream object returned by those methods.</para>
	/// </remarks>
	/// <seealso cref="EseStreamValue" />
	public class EseBinaryStreamAttribute : EseByteArrayAttribute
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseBinaryStreamAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseBinaryStreamAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>The maximum length, in GigaBytes, of a column.</summary>
		public double maxGB
		{
			get { return maxMB / 1024.0; }
			set
			{
				if( maxGB > 1024.0 * 2 )
					throw new ArgumentOutOfRangeException();
				maxMB = value * 1024.0;
			}
		}

		/// <summary>Throw an exception if this attribute was not applied to the class member of type <see cref="EseStreamValue" />.</summary>
		public override void verifyTypeSupport( Type t )
		{
			Type tpStreamValue = typeof( EseStreamValue );
			if( t == tpStreamValue )
				return;
			throw new System.Runtime.Serialization.SerializationException();
		}

		/// <summary>This method does nothing.</summary>
		/// <remarks>To save the column value, call <see cref="EseStreamValue.Write" /> while in a transaction.</remarks>
		/// <param name="cur"></param>
		/// <param name="idColumn"></param>
		/// <param name="value"></param>
		/// <param name="bNewRecord"></param>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			return;
		}

		/// <summary>Just save column ID and row bookmark in the EseStreamValue instance.</summary>
		/// <param name="cur"></param>
		/// <param name="idColumn"></param>
		/// <returns></returns>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			return new EseStreamValue( cur, idColumn );
		}
	}
}