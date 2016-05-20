using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace EsentSerialization
{
	/// <summary>This class wraps a ESENT table, to allow editing with DataGridView.</summary>
	/// <remarks>Don't use it for long lists, since all records are stored in RAM. <br />
	/// This class also used internally by <see cref="VirtualMode{tRow}" />, to automatically populate DataGridView's columns.</remarks>
	/// <typeparam name="tRow">Type of the records stored in the table (a type marked with <see cref="Attributes.EseTableAttribute">[EseTable]</see> attribute).</typeparam>
	public class EditableObjectList<tRow>: BindingList<BookmarkedRow<tRow>>, ITypedList
		where tRow : new()
	{
		readonly Cursor<tRow> m_table;
		/// <summary></summary>
		public Cursor<tRow> table { get { return m_table; } }

		// A few shortcuts
		/// <summary></summary>
		public iSerializerSession session { get { return table.session; } }
		/// <summary></summary>
		public JET_TABLEID idTable { get { return table.idTable; } }
		/// <summary></summary>
		public iTypeSerializer serializer { get { return table.serializer; } }

		/// <summary></summary>
		public EditableObjectList()
		{
			m_table = null;
		}

		/// <summary>Construct, adding all records from the specified table.</summary>
		/// <param name="sess"></param>
		public EditableObjectList( iSerializerSession sess )
		{
			sess.getTable( out m_table );
			var bl = new BookmarkedRecordset<tRow>( m_table );
			foreach( var r in bl.all() )
				base.Add( r );

			base.AllowNew = true;
		}

		/// <summary>Construct, adding the records from the provided recordset.</summary>
		/// <param name="rs"></param>
		public EditableObjectList( BookmarkedRecordset<tRow> rs )
		{
			m_table = rs.cursor;
			foreach( var r in rs.all() )
				base.Add( r );

			base.AllowNew = true;
		}

		/// <summary></summary>
		protected override void OnAddingNew( AddingNewEventArgs e )
		{
			base.OnAddingNew( e );
			tRow newObj = new tRow();
			e.NewObject = new BookmarkedRow<tRow>( m_table, newObj );
		}

		/// <summary>The create and edit operations are implemented by the BookmarkedRow&lt;&gt;,
		/// that's why here we only need to handle the records deletion.</summary>
		protected override void RemoveItem( int index )
		{
			base.Items[ index ].Delete();
			base.RemoveItem( index );
		}

		static PropertyDescriptorCollection s_cached = null;

		PropertyDescriptorCollection ITypedList.GetItemProperties( PropertyDescriptor[] listAccessors )
		{
			// http://stackoverflow.com/questions/882214
			if( listAccessors != null && listAccessors.Length != 0 )
				throw new NotImplementedException( "Relations not implemented" );

			if( null != s_cached )
				return s_cached;

			tRow tr = new tRow();
			PropertyDescriptorCollection coll = TypeDescriptor.GetProperties( tr );
			List<PropertyDescriptor> res = new List<PropertyDescriptor>();
			foreach( PropertyDescriptor pd in coll )
			{
				res.Add( new EsePropertyDescriptor<tRow>( pd ) );
			}
			s_cached = new PropertyDescriptorCollection( res.ToArray() );
			return s_cached;
		}

		string ITypedList.GetListName( PropertyDescriptor[] listAccessors )
		{
			return "EditableObjectList<" + typeof( tRow ).Name + ">";
		}
	}
}