using EsentSerialization.Attributes;
using Microsoft.Isam.Esent.Interop;
using System.Reflection;

namespace EsentSerialization
{
	/// <summary>Serializer instance for the specific record type</summary>
	public interface iTypeSerializer
	{
		/// <summary>Get the [EseTable] attribute instance.</summary>
		Attributes.EseTableAttribute tableAttribute { get; }

		/// <summary>Name of the table.</summary>
		string tableName { get; }

		/// <summary>Get the columns indexed by the specified index.</summary>
		/// <param name="strIndexName">Name of the index.</param>
		/// <returns>Array of indexed columns.</returns>
		EseColumnAttrubuteBase[] getIndexedColumns( string strIndexName );

		/// <summary>Fetch the specific object's field from the current record of the specified table.</summary>
		/// <param name="cur">The table cursor.</param>
		/// <param name="rec">The object to receive the new field value.</param>
		/// <param name="fName">ESE column name to load from the table.</param>
		void DeserializeField( EseCursorBase cur, object rec, string fName );

		/// <summary>Fetch the specific field from the current record of the specified table.</summary>
		/// <param name="cur">The table cursor.</param>
		/// <param name="fName">ESE column name to load from the table.</param>
		/// <returns>The field value.</returns>
		/// <remarks>This method was implemented because sometimes,
		/// e.g. while traversing the DB-backed tree searching for something,
		/// you only need the value of a single field.</remarks>
		object FetchSingleField( EseCursorBase cur, string fName );

		/// <summary>Update the value of the specific field of the current record.</summary>
		/// <param name="cur">The table cursor.</param>
		/// <param name="fName">ESE column name to load from the table.</param>
		/// <param name="value">New field value.</param>
		/// <remarks>
		/// <para>The writable transaction must be opened before this call.</para>
		/// <para>This method was implemented as the performance optimization:
		/// when you only need to update a single field, and you don't have the complete object.</para>
		/// </remarks>
		void SaveSingleField( EseCursorBase cur, string fName, object value );

		/// <summary>Resolve column name to JET_COLUMNID.</summary>
		/// <param name="cur">The table cursor.</param>
		/// <param name="fName">ESE column name to resolve.</param>
		/// <returns>Column ID.</returns>
		JET_COLUMNID GetColumnId( EseCursorBase cur, string fName );

		/// <summary>Store all fields of the record in the table.</summary>
		/// <param name="cur">The table cursor</param>
		/// <param name="rec">The object to store in the database. The type of the object must match the table.</param>
		/// <param name="bNewRecord">True for INSERT operations, false for UPDATE operations.</param>
		/// <remarks>The writable transaction must be opened before this call.</remarks>
		void Serialize( EseCursorBase cur, object rec, bool bNewRecord );

		/// <summary>Store only the specific field of the record.</summary>
		/// <param name="cur">The table cursor</param>
		/// <param name="rec">The object to store in the database. The type of the object must match the table.</param>
		/// <param name="fName">ESE column name to store in the table.</param>
		/// <remarks>The writable transaction must be opened before this call.</remarks>
		void SerializeField( EseCursorBase cur, object rec, string fName );

		/// <summary>Fetch all object's fields from the current record of the specified table.</summary>
		/// <param name="cur">The table cursor</param>
		/// <param name="rec">The fields / properties of this object will be filled with the values fetched from the database.</param>
		void Deserialize( EseCursorBase cur, object rec );

		/// <summary>Given a member info, find indices over the column mapped by that member.</summary>
		IndexForColumn[] indicesFromColumn( MemberInfo mi );
	}
}