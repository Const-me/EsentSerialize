using System.ComponentModel;

namespace EsentSerialization
{
	/// <summary>This class wraps a database row of some type, including the cursor and the bookmark.</summary>
	/// <remarks>Useful if you're going to update the records.
	/// It implements <see cref="IEditableObject" /> interface,
	/// allowing it to be used as e.g. data source of a WinForms control.</remarks>
	/// <typeparam name="tRow">Record type.</typeparam>
	public class BookmarkedRow<tRow> : IEditableObject where tRow : new()
	{
		readonly Cursor<tRow> m_cursor;
		/// <summary>The DB cursor.</summary>
		public Cursor<tRow> cursor { get { return m_cursor; } }

		byte[] m_bookmark;
		/// <summary>The bookmark. If this object represents a record that was not yet added to the database, this field will be empty.</summary>
		public byte[] bookmark { get { return m_bookmark; } }

		/// <summary>The record.</summary>
		tRow m_obj;
		/// <summary>The record.</summary>
		public tRow obj { get { return m_obj; } set { m_obj = value; } }

		/// <summary>Construct the new record.</summary>
		public BookmarkedRow( Cursor<tRow> cursor, tRow obj )
		{
			m_cursor = cursor;
			m_bookmark = ByteArray.Empty;
			m_obj = obj;
		}

		/// <summary>Construct the record that's already in the DB.</summary>
		public BookmarkedRow( Cursor<tRow> cursor, byte[] bookmark, tRow obj )
		{
			m_cursor = cursor;
			m_bookmark = bookmark;
			m_obj = obj;
		}

		/// <summary>Returns a String that represents the current Object.</summary>
		public override string ToString() { return m_obj.ToString(); }

		void IEditableObject.BeginEdit() { }

		void IEditableObject.CancelEdit()
		{
			if( !m_bookmark.isEmpty() )
				Reload();
			// Unfortunately we can't revert the changes unless the item is backed up by the DB:
			// the source for new items is potentially a custom EditableObjectList<> derived class..
		}

		void IEditableObject.EndEdit() { Save(); }

		/// <summary>Save the object to the DB, by either adding a new object, or updating the existing object</summary>
		public void Save()
		{
			using( var t = cursor.session.BeginTransaction() )
			{
				if( m_bookmark.isEmpty() )
					m_bookmark = cursor.Add( m_obj );
				else
					this.Update();
				t.Commit();
			}
			// TODO: notify the owner.
			// If the owning EditableObjectList<> displays a sorted or filtered rows,
			// it might be very interested in receiving the update
		}

		/// <summary>Reload this object from the DB.</summary>
		public void Reload()
		{
			m_obj = cursor.getAt( m_bookmark );
		}

		/// <summary>Save the specified field value to the DB.</summary>
		/// <remarks>The write transaction must be already opened.</remarks>
		public void UpdateField( string strFieldName )
		{
			cursor.gotoBookmark( bookmark );
			cursor.UpdateField( obj, strFieldName );
		}

		/// <summary>Save several field values to the DB.</summary>
		/// <remarks>The write transaction must be already opened.</remarks>
		public void UpdateFields( params string[] arrFields )
		{
			cursor.gotoBookmark( bookmark );
			cursor.UpdateFields( obj, arrFields );
		}

		/// <summary>Save all object's fields to the DB.</summary>
		/// <remarks>The write transaction must be already opened.</remarks>
		public void Update()
		{
			cursor.setAt( bookmark, obj );
		}

		/// <summary>Delete this record from the database.</summary>
		/// <remarks>The write transaction must be already opened.</remarks>
		public void Delete()
		{
			if( !bookmark.isEmpty() )
				cursor.delAt( bookmark );
		}
	}
}