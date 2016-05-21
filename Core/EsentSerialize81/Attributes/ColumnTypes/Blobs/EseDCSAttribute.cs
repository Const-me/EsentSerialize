using Microsoft.Isam.Esent.Interop;
using System;
using System.Runtime.Serialization;
using System.Xml;

namespace EsentSerialization.Attributes
{
	/// <summary>Column containing the object serialized into binary XML using data contract serializer.</summary>
	/// <remarks>
	/// <para>May be applied to a field/property of any type marked with DataContractAttribute.</para>
	/// <para>The underlying ESENT column type is JET_coltypLongBinary.</para>
	/// <para>The attribute instance holds the System.Runtime.Serialization.DataContractSerializer object that performs the [de]serialization.<br/>
	/// <br/>
	/// The <see cref="System.Runtime.Serialization.DataContractSerializer">documentation on DataContractSerializer</see> says:<br/>
	/// <i>"Instances of this class are thread safe except when the instance is used with an implementation of the IDataContractSurrogateor DataContractResolver".</i><br/>
	/// That's why no locks is ever needed, even when different threads are [de]serializing the same column of different records,
	/// which is perfectly legal way of using the library.</para>
	/// <para>This column uses the .NET Binary XML Format, a Microsoft-specific binary representation for XML Information Sets that generally yields a smaller footprint than the equivalent XML 1.0 representation.</para>
	/// </remarks>
	public sealed class EseDCSAttribute : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public EseDCSAttribute() { }
		/// <summary>Initialize with non-default column name.</summary>
		public EseDCSAttribute( string _columnName ) : base( _columnName ) { }

		/// <summary>Is always true.</summary>
		public override bool bFieldNullable { get { return true; } }

		IXmlDictionary dict = null;

		/// <summary>A user-supplied dictionary with right values can reduce the size of the serialized document drastically.</summary>
		/// <remarks>
		/// <para>The type must have public parameterless constructor, and it must implement System.Xml.IXmlDictionary interface.</para>
		/// <para>The implementation must be thread-safe: it'll be used concurrently by every thread who serialize/deserialize this column.</para>
		/// <para>If you inherit from System.Xml.XmlDictionary,
		/// be sure to override and fix TryLookup( XmlDictionaryString, XmlDictionaryString ) method: .NET framework implementation doesn't try to lookup the string by value,
		/// instead it just fails silently.</para>
		/// <para>The dictionary must be stable, that's why it's a bad idea to build it dynamically by reflecting the serialized type.
		/// During the lifetime of your software, you may only add new entries to that dictionary, you may not remove or change the entries.
		/// Failing to do that will lead to an incompatible database format.</para>
		/// <para>Sample implementation:<code lang="C#">            class ValueTypeDictionary: XmlDictionary
		///{
		///	public ValueTypeDictionary()
		///	{
		///		base.Add( "MyClass" );
		///		base.Add( "http://schemas.datacontract.org/2004/07/My.CLR.Namespace" );
		///		base.Add( "http://www.w3.org/2001/XMLSchema-instance" );
		///		base.Add( "myMemeber1" );
		///		base.Add( "myMemeber2" );
		///	}
		///
		///	public override bool TryLookup( XmlDictionaryString value, out XmlDictionaryString result )
		///	{
		///		return base.TryLookup( value.Value, out result );
		///	}
		///}</code>
		/// </para>
		/// </remarks>
		public Type DictionaryType = null;

		/// <summary>Get column definition.</summary>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.coltyp = JET_coltyp.LongBinary;
			res.grbit = ColumndefGrbit.ColumnTagged;
			return res;
		}

		DataContractSerializer m_serializer;
		/// <summary>Construct the data contract serializer for the specified type.</summary>
		/// <param name="t">The type of the field/property this attribute is applied.</param>
		public override void verifyTypeSupport( Type t )
		{
			// Create serializer
			m_serializer = new DataContractSerializer( t );

			// Create dictionary
			if( null == this.DictionaryType )
			{
				this.dict = null;
				return;
			}
			object obj = Activator.CreateInstance( this.DictionaryType );
			this.dict = obj as IXmlDictionary;
			if( null == this.dict )
				throw new ArgumentException( "Unable to construct a dictionary of the specified type." );
		}

		/// <summary>Store the column value in the database.</summary>
		public override void Serialize( EseCursorBase cur, JET_COLUMNID idColumn, object value, bool bNewRecord )
		{
			using( var stm = new ColumnStream( cur.idSession, cur.idTable, idColumn ) )
			{
				using( XmlDictionaryWriter bw = XmlDictionaryWriter.CreateBinaryWriter( stm, this.dict ) )
				{
					this.m_serializer.WriteObject( bw, value );
					bw.Flush();
				}

				// TODO [low]: if the ( current size - new size < 4kb ), then append spaces/zeros instead of resizing the column. The comments inside the SetLength method suggest that shrinking the column is very inefficient for large values.
				if( stm.Position < stm.Length )
					stm.SetLength( stm.Position );
			}
		}

		/// <summary>Retrieve the column value from the DB.</summary>
		public override object Deserialize( EseCursorBase cur, JET_COLUMNID idColumn )
		{
			using( var stm = new ColumnStream( cur.idSession, cur.idTable, idColumn ) )
			{
				if( stm.Length < 1 )
					return null;
				using( var br = XmlDictionaryReader.CreateBinaryReader( stm, this.dict, XmlDictionaryReaderQuotas.Max ) )
					return m_serializer.ReadObject( br );
			}
		}
	}
}