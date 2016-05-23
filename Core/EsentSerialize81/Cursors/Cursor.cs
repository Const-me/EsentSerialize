using System;
using System.Linq;
using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization
{
	/// <summary>This generic class represents opened table cursor, and implements operations like add and remove.</summary>
	/// <remarks>For the search/sort/query functionality use <see cref="Recordset{tRow}" />
	/// or <see cref="BookmarkedRecordset{tRow}" />.<br />
	/// For data binding use either <see cref="EditableObjectList{tRow}" /> ( small to medium datasets ) 
	/// or <see cref="VirtualMode{tRow}" /> ( large datasets ).</remarks>
	/// <typeparam name="tRow">Type of the records stored in the table (a type marked with <see cref="Attributes.EseTableAttribute">[EseTable]</see> attribute).</typeparam>
	public sealed class Cursor<tRow> : EseCursorBase where tRow : new()
	{
		/// <summary>Construct the cursor.</summary>
		public Cursor( iSerializerSession session, iTypeSerializer serializer, JET_TABLEID idTable, bool isReadOnly )
			: base( session, serializer, idTable, isReadOnly ) { }

		/// <summary>Get the item at the current cursor position</summary>
		/// <returns></returns>
		public tRow getCurrent()
		{
			tRow ret = new tRow();
			serializer.Deserialize( this, ret );
			return ret;
		}

		/// <summary>Update all fields of the item at the current cursor position</summary>
		/// <param name="item"></param>
		public void Update( tRow item )
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			if( !session.isInTransaction ) throw new NotSupportedException( "You must open a transaction before 'Update' operation" );

			using( Update update = new Update( idSession, idTable, JET_prep.Replace ) )
			{
				serializer.Serialize( this, item, false );
				update.Save();
			}
		}

		/// <summary>Update the specified field of the item at the current cursor position.</summary>
		/// <param name="item"></param>
		/// <param name="field"></param>
		public void UpdateField( tRow item, string field )
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			if( !session.isInTransaction ) throw new NotSupportedException( "You must open a transaction before 'UpdateField' operation" );

			using( Update update = new Update( idSession, idTable, JET_prep.Replace ) )
			{
				serializer.SerializeField( this, item, field );
				update.Save();
			}
		}

		/// <summary>Update the specified fields of the item at the current cursor position.</summary>
		/// <param name="item"></param>
		/// <param name="fields"></param>
		public void UpdateFields( tRow item, params string[] fields )
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			if( !session.isInTransaction ) throw new NotSupportedException( "You must open a transaction before 'UpdateFields' operation" );

			using( Update update = new Update( idSession, idTable, JET_prep.Replace ) )
			{
				foreach( string f in fields )
					serializer.SerializeField( this, item, f );

				update.Save();
			}
		}

		/// <summary>Fetch the specified fields from the database.</summary>
		/// <param name="item"></param>
		/// <param name="fields"></param>
		public void FetchFields( tRow item, params string[] fields )
		{
			foreach( string f in fields )
				serializer.DeserializeField( this, item, f );
		}

		/// <summary>Delete the record at the current cursor position.</summary>
		public void delCurrent()
		{
			Api.JetDelete( idSession, idTable );
		}

		/// <summary>Get the item at the specified bookmark.</summary>
		/// <param name="bookmark"></param>
		/// <returns></returns>
		public tRow getAt( byte[] bookmark )
		{
			gotoBookmark( bookmark );
			return getCurrent();
		}

		/// <summary>Update all fields of the item at the specified bookmark.</summary>
		/// <param name="bookmark"></param>
		/// <param name="obj"></param>
		public void setAt( byte[] bookmark, tRow obj )
		{
			ResetIndex();
			gotoBookmark( bookmark );
			Update( obj );
		}

		/// <summary>Delete the record at the specified bookmark.</summary>
		/// <param name="bookmark"></param>
		public void delAt( byte[] bookmark )
		{
			Api.JetSetCurrentIndex( idSession, idTable, null );
			gotoBookmark( bookmark );
			delCurrent();
		}

		byte[] m_buffBookmark = new byte[ 1024 ];
		/// <summary>Add an item to the table.</summary>
		/// <param name="newItem"></param>
		/// <returns>The bookmark of the newly added item.</returns>
		public byte[] Add( tRow newItem )
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			if( !session.isInTransaction ) throw new NotSupportedException( "You must open a transaction before 'Add' operation" );

			int bmLength;
			using( Update update = new Update( idSession, idTable, JET_prep.Insert ) )
			{
				serializer.Serialize( this, newItem, true );
				update.Save( m_buffBookmark, m_buffBookmark.Length, out bmLength );
			}
			byte[] bk = m_buffBookmark.Take( bmLength ).ToArray(); ;
			return bk;
		}

		/// <summary>Add several items to the table.</summary>
		/// <remarks>Only fires single NotifyTableChanged event with 'Reset' action.</remarks>
		/// <param name="newItems"></param>
		public void AddRange( System.Collections.Generic.IEnumerable<tRow> newItems )
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			if( !session.isInTransaction ) throw new NotSupportedException( "You must open a transaction before 'AddRange' operation" );

			foreach( tRow i in newItems )
				using( Update update = new Update( idSession, idTable, JET_prep.Insert ) )
				{
					serializer.Serialize( this, i, true );
					update.Save();
				}
		}

		/// <summary>Remove all records from the table.</summary>
		/// <remarks>
		/// <para>Only fires single NotifyTableChanged event with 'Reset' action.</para>
		/// <para>This method could be slow for large tables.
		/// If you aren't happy with the performance, use <see cref="iSerializerSession.recreateTable{T}" /> instead.
		/// This method however is safer.</para>
		/// </remarks>
		/// <returns>The count of the erased records.</returns>
		public int RemoveAll()
		{
			if( bReadOnly ) throw new NotSupportedException( "This cursor is read-only" );
			int nDeletedRecords = session.ClearTable( idTable );
			return nDeletedRecords;
		}

		/// <summary>Construct Recordset&lt;&gt; object containing all records in this table</summary>
		public Recordset<tRow> Recordset()
		{
			return new Recordset<tRow>( this );
		}

		/// <summary>Duplicate the cursor within the session, so the returned copy has its own position, selected index, index range.</summary>
		public Cursor<tRow> CreateOwnCopy()
		{
			var res = new Cursor<tRow>( session, serializer, idTable, bReadOnly );
			res.Duplicate();
			return res;
		}
	}
}