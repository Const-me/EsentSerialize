using Database;
using EsentSerialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsDemoApp
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		// Returns all enum values as typed array.
		public static IEnumerable<tEnum> EnumValues<tEnum>() // 'where tEnum: Enum' gives CS0702
		{
			return Enum.GetValues( typeof( tEnum ) )
				.Cast<tEnum>();
		}

		readonly iSerializerSession m_sess;

		public Form1( iSerializerSession _sess )
		{
			m_sess = _sess;
			InitializeComponent();

			colSex.DataSource = EnumValues<Person.eSex>().ToList();

			dataGridView1.AutoGenerateColumns = false;
			dataGridView1.DataSource = new EditableObjectList<Person>( _sess );
		}

		// Format the phones for DG view.
		private void dataGridView1_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			if( e.ColumnIndex == colPhones.Index )
			{
				var phones = e.Value as List<string>;
				if( null != phones && 0 != phones.Count )
					e.Value = String.Join( "; ", phones.ToArray() );
				else
					e.Value = "";
				e.FormattingApplied = true;
			}
		}

		// Parse the phones back into the List<string>
		private void dataGridView1_CellParsing( object sender, DataGridViewCellParsingEventArgs e )
		{
			if( e.ColumnIndex == colPhones.Index )
			{
				string val = e.Value as string;
				if( !string.IsNullOrEmpty( val ) )
				{
					var phones = val.Split( ';' )
						.Select( p => p.Trim() )
						.Where( p => !String.IsNullOrEmpty( p ) )
						.ToList();
					e.Value = phones;
				}
				else
					e.Value = new List<string>();
				e.ParsingApplied = true;
			}
		}

		private void btnBackup_Click( object sender, EventArgs e )
		{
			string strFilter = "ZIP archives (*.zip)|*.zip";

			string strLocation;
			using( var sfd = new SaveFileDialog() )
			{
				sfd.CreatePrompt = false;
				sfd.Filter = strFilter;
				if( DialogResult.OK != sfd.ShowDialog( this ) )
					return;
				strLocation = sfd.FileName;
			}

			using( var s = new FileStream( strLocation, FileMode.Create ) )
				m_sess.serializer.BackupDatabase( s );

			MessageBox.Show( this, "Backup complete", null, MessageBoxButtons.OK, MessageBoxIcon.Information );
		}
	}
}