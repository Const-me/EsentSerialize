using EseFileSystem;
using EsentSerialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Test1
{
	public partial class Form1 : Form
	{
		// This one is only called by the IDE.
		public Form1()
		{
			InitializeComponent();
		}

		SessionPool pool;

		iFileIo fileIo;

		// Main thread's session
		iSerializerSession sess;

		// Main thread's recordset
		readonly Recordset<EfsEntry> rs;

		TreeNode nodeRoot;

		TreeNode nodeCurrent;

		int? idCurrentFolder { get
		{
			if( null == nodeCurrent )
				return null;
			return (int)nodeCurrent.Tag;
		} }

		// This one is the real.
		public Form1( SessionPool _pool )
		{
			InitializeComponent();

			pool = _pool;
			sess = pool.GetSession();
			rs = sess.Recordset<EfsEntry>();

			EFS.Initialize( rs );

			fileIo = new FileIoThread();
			// fileIo = new TrivialFileIo();

			InitFoldersView();
		}

		readonly Dictionary<int, TreeNode> m_dictNodes = new Dictionary<int, TreeNode>();

		/// <summary>Construct tree node for a folder</summary>
		/// <param name="f"></param>
		/// <returns></returns>
		TreeNode newNode( EfsEntry f )
		{
			TreeNode res = new TreeNode();
			res.Tag = f.id;
			if( string.IsNullOrEmpty( f.name ) )
			{
				res.Text = "[root]";
				res.ImageIndex = 0;
			}
			else
			{
				res.Text = f.name;
				res.ImageIndex = 1;
				res.SelectedImageIndex = 1;
			}
			m_dictNodes[ f.id ] = res;
			return res;
		}

		/// <summary>Construct list item for a file or folder.</summary>
		/// <param name="f"></param>
		/// <returns></returns>
		ListViewItem newItem( EfsEntry f )
		{
			ListViewItem res = new ListViewItem();
			res.Tag = f.id;
			res.Text = f.name;
			if( f.isDirectory )
				res.ImageIndex = 1;
			else
				res.ImageIndex = 2;
			return res;
		}

		#region Tree View Display

		TreeNode getFakeNode()
		{
			return new TreeNode();
		}

		bool isFakeNode( TreeNode n )
		{
			if( null != n.Tag )
				return false;
			if( !String.IsNullOrEmpty( n.Text ) )
				return false;
			return true;
		}

		void InitFoldersView()
		{
			tree.Nodes.Clear();
			m_dictNodes.Clear();

			using( var trans = sess.BeginTransaction() )
			{
				// Add the root node
				rs.filterFindEqual( "id", EFS.idRootFolder );
				var eRoot = rs.getFirst();
				nodeRoot = newNode( eRoot );
				tree.Nodes.Add( nodeRoot );

				// Add the fake child, foe the "+" to show up
				nodeRoot.Nodes.Add( getFakeNode() );
			}
		}

		private void tree_BeforeExpand( object sender, TreeViewCancelEventArgs e )
		{
			var nodes = e.Node.Nodes;

			if( !isFakeNode( nodes[ 0 ] ) )
				return;

			nodes.Clear();
			int idParent = (int)e.Node.Tag;

			using( var trans = sess.BeginTransaction() )
				foreach( var fsEntry in EFS.ListChildrenFolders( rs, idParent ) )
				{
					var nodeNewReal = newNode( fsEntry );
					nodes.Add( nodeNewReal );
					nodeNewReal.Nodes.Add( getFakeNode() );
				}

			if( 0 == nodes.Count )
				e.Cancel = true;
		}

		#endregion

		#region Basic navigation

		private void tree_AfterSelect( object sender, TreeViewEventArgs e )
		{
			nodeCurrent = tree.SelectedNode;
			if( null == nodeCurrent )
			{
				list.Visible = false;
			}
			else
			{
				list.Visible = true;
				DisplayFolder( (int)nodeCurrent.Tag );
			}
		}

		void DisplayFolder( int idFolder )
		{
			list.Items.Clear();
			using( var trans = sess.BeginTransaction() )
				foreach( var e in EFS.ListChildren( rs, idFolder ) )
					list.Items.Add( newItem( e ) );
		}

		private void list_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var fi = list.FocusedItem;
			if( null == fi || null == fi.Tag )
				return;

			int idFile = (int)( fi.Tag );
			EfsEntry eFile = EFS.byId( rs, idFile );

			if( eFile.isDirectory )
			{
				if( !nodeCurrent.IsExpanded )
					nodeCurrent.Expand();

				tree.SelectedNode = nodeCurrent.Nodes.All()
					.Where( n => n.Tag != null && (int)n.Tag == idFile )
					.FirstOrDefault();
			}
		}

		#endregion

		#region Context menu - general

		enum eCurrentPane
		{
			NULL, cpTree, cpList
		};

		eCurrentPane getPane()
		{
			if( menu.SourceControl == tree )
				return eCurrentPane.cpTree;	// Context menu, tree
			if( menu.SourceControl == list )
				return eCurrentPane.cpList; // Context menu, list

			if( null == menu.SourceControl )
			{
				// Hot key
				if( tree.Focused )
					return eCurrentPane.cpTree;
				if( list.Focused )
					return eCurrentPane.cpList;
			}

			return eCurrentPane.NULL;
		}

		private void menu_Opening( object sender, CancelEventArgs e )
		{
			bool bSomethingSelected = getSelectedItems().Any();

			copyToolStripMenuItem.Enabled = bSomethingSelected;
			cutToolStripMenuItem.Enabled = bSomethingSelected;
			extractToToolStripMenuItem.Enabled = bSomethingSelected;

			pasteToolStripMenuItem.Enabled = Clipboard.ContainsFileDropList();
		}

		#endregion

		#region Folders Creation

		// Create the folder in the DB
		private EfsEntry CreateFolder( int idParent )
		{
			EfsEntry ne;
			using( var trans = sess.BeginTransaction() )
			{
				string strNameBase = "New folder";

				string strName = strNameBase;

				if( EFS.FindFile( rs, idParent, strName ).HasValue )
				{
					for( int i = 2; true; i++ )
					{
						strName = String.Format( "{0} {1}", strNameBase, i );
						if( !EFS.FindFile( rs, idParent, strName ).HasValue )
							break;
					}
				}
				ne = EFS.CreateFolder( rs.cursor, idParent, strName );
				trans.Commit();
			}
			return ne;
		}

		/// <summary>Construct file or folder's UI items.</summary>
		/// <remarks>This method may create duplicate UI items if called more then once.</remarks>
		/// <param name="ne"></param>
		/// <returns></returns>
		private Tuple<TreeNode, ListViewItem> AddUiItem( EfsEntry ne )
		{
			// List
			ListViewItem lvi = null;
			if( ne.idParent == this.idCurrentFolder )
			{
				lvi = newItem( ne );
				list.Items.Add( lvi );
			}

			// Tree
			TreeNode tn = null;
			if( ne.isDirectory )
			{
				var p = tree.Nodes.AllRecursive()
					.Where( n => ( ne.idParent == (int)n.Tag ) )
					.FirstOrDefault();
				if( null != p )
				{
					tn = newNode( ne );
					p.Nodes.Add( tn );
				}
			}

			return Tuple.Create( tn, lvi );
		}

		private void newFolderToolStripMenuItem_Click( object sender, EventArgs e )
		{
			int idParent = EFS.idRootFolder;
			if( null != tree.SelectedNode )
				idParent = (int)tree.SelectedNode.Tag;

			EfsEntry newFolder = CreateFolder( idParent );

			var items = AddUiItem( newFolder );

			switch( getPane() )
			{
				case eCurrentPane.cpTree:
					if( null != items.Item1 )
						items.Item1.BeginEdit();
					return;
				case eCurrentPane.cpList:
					if( null != items.Item2 )
						items.Item2.BeginEdit();
					return;
			}
		}

		#endregion

		#region Rename files / folders

		// Rename the file in the DB.
		void RenameFile( int idFile, string strNewName )
		{
			using( var trans = sess.BeginTransaction() )
			{
				EFS.MoveFile( rs, idFile, EFS.GetParent( rs, idFile ), strNewName );
				trans.Commit();
			}
		}

		// tree.BeforeLabelEdit: disable renaming of the root folder.
		void tree_BeforeLabelEdit( object sender, NodeLabelEditEventArgs e )
		{
			if( null == e.Node || e.Node == nodeRoot )
				e.CancelEdit = true;
		}

		// "Rename" menu click: begin label edit
		void renameToolStripMenuItem_Click( object sender, EventArgs e )
		{
			switch( getPane() )
			{
				case eCurrentPane.cpTree:
					var node = tree.SelectedNode;
					if( null == node ) return;
					if( nodeRoot == node ) return;
					if( !node.IsEditing )
						node.BeginEdit();
					return;
				case eCurrentPane.cpList:
					var item = list.FocusedItem;
					if( null == item ) return;
					item.BeginEdit();
					return;
			}
		}

		// tree.AfterLabelEdit: rename the file
		private void tree_AfterLabelEdit( object sender, NodeLabelEditEventArgs e )
		{
			if( String.IsNullOrEmpty( e.Label ) )
			{
				e.CancelEdit = true;
				return;
			}

			int idFile = (int)e.Node.Tag;
			RenameFile( idFile, e.Label );

			// Update the list
			var lvi = list.All()
				.Where( _lvi => (int)_lvi.Tag == idFile )
				.FirstOrDefault();
			if( null != lvi )
				lvi.Text = e.Label;
		}

		// list.AfterLabelEdit: rename the file
		private void list_AfterLabelEdit( object sender, LabelEditEventArgs e )
		{
			if( String.IsNullOrEmpty( e.Label ) )
			{
				e.CancelEdit = true;
				return;
			}

			int idFile = (int)( list.Items[ e.Item ].Tag );
			RenameFile( idFile, e.Label );

			// Update the tree
			TreeNode node;
			if( m_dictNodes.TryGetValue( idFile, out node ) )
				node.Text = e.Label;
		}

		#endregion

		#region Delete files/ folders

		void RemoveUiItem( int id )
		{
			// tree
			TreeNode tn;
			if( m_dictNodes.TryGetValue( id, out tn ) )
			{
				m_dictNodes.Remove( id );
				tn.Remove();
			}

			// list
			ListViewItem lvi = null;
			lvi = list.All().Where( l => id == (int)( l.Tag ) ).FirstOrDefault();
			if( null != lvi )
				lvi.Remove();
		}

		private void deleteToolStripMenuItem_Click( object sender, EventArgs e )
		{
			using( var trans = sess.BeginTransaction() )
			{
				foreach( var idFile in getSelectedItems() )
				{
					EFS.DeleteFile( rs, idFile );
					RemoveUiItem( idFile );
				}
				trans.Commit();
			}
		}

		#endregion

		#region Copy-paste, import, export

		string FormatCopyRateSummary( int nFiles, long nBytes, TimeSpan ts )
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine( "Copy operation complete." );

			sb.Append( "Transferred " );

			if( nBytes < 1024 ) // up to 1 KB
				sb.AppendFormat( "{0} {1}", nBytes, ( 1 == nBytes ) ? "byte" : "bytes" );
			else if ( nBytes < 1024 * 1024 ) // up to 1 MB
				sb.AppendFormat( "{1:N2} kB ( {0} bytes )", nBytes, (double)nBytes / 1024.0 );
			else if ( nBytes < 1024 * 1024 * 1024 ) // Up to 1 GB
				sb.AppendFormat( "{1:N2} MB ( {0} bytes )", nBytes, (double)nBytes / ( 1024.0 * 1024.0 ) );
			else	// More then 1 GB
				sb.AppendFormat( "{1:N2} GB ( {0} bytes )", nBytes, (double)nBytes / ( 1024.0 * 1024.0 * 1024.0 ) );

			sb.AppendFormat(" in {0} {1}.", nFiles, ( 1 == nFiles ) ? "file" : "files");

			sb.AppendLine();

			if( ts.TotalMilliseconds < 1 )
				return sb.ToString();

			sb.Append( "This took " );
			// Unfortunately, the argument-taking version of TimeSpan.ToString() if only in .NET 4
			if( ts.TotalSeconds < 1.0 )
				sb.AppendFormat( "{0:N2} milliseconds", ts.TotalMilliseconds );
			else if( ts.TotalSeconds < 60 ) // less then 1 min
				sb.AppendFormat( "{0:N3} seconds", ts.TotalSeconds );
			else if( ts.TotalMinutes < 60 ) // less then 1 hour
				sb.AppendFormat( "{0}:{1:N3} min:sec", ts.Minutes, ts.TotalSeconds - ( ts.Minutes * 60.0 ) );
			else
				sb.Append( ts.ToString() );
			sb.AppendLine( "." );

			sb.Append( "Average transfer rate was " );

			double bps = (double)( nBytes ) / ts.TotalSeconds;
			
			if( bps < 1024 )
				sb.AppendFormat( "{0:N2} bytes / second", bps );
			else if( bps < 1024 * 1024 )
				sb.AppendFormat( "{0:N2} kB / second", bps / 1024.0 );
			else if ( bps < 1024 * 1024 * 1024 )
				sb.AppendFormat( "{0:N2} MB / second", bps / ( 1024.0 * 1024.0 ) );
			else
				sb.AppendFormat( "{0:N2} GB / second", bps / ( 1024.0 * 1024.0 * 1024.0 ) );
			sb.AppendLine( "." );

			return sb.ToString();
		}

		#region Copy to clipboard, and extract

		void BeforeTransfer( Delay.VirtualFileDataObject v )
		{
			Cursor.Current = Cursors.WaitCursor;
		}

		void AfterTransfer( Delay.VirtualFileDataObject v )
		{
			Cursor.Current = Cursors.Default;
		}

		/// <summary>Iterate over the selected items.</summary>
		/// <returns>Enumerable containing the IDs of the selected EFS file system entries.</returns>
		/// <remarks>This method only returns what's selected on the UI: no resursive search is performed.</remarks>
		IEnumerable<int> getSelectedItems()
		{
			switch( getPane() )
			{
				case eCurrentPane.cpTree:
					int? f = idCurrentFolder;
					if( !f.HasValue || f == EFS.idRootFolder )
						break;
					return new int[ 1 ] { f.Value };

				case eCurrentPane.cpList:
					return list.Selected()
						.Select( lvi => (int)lvi.Tag )
						.ToList();
			}
			return new int[ 0 ];
		}

		/// <summary>Copy a file from EFS to the specified System.Stream.</summary>
		/// <remarks>Unlike the rest of the code in this class, this method is safe to call from any thread.</remarks>
		/// <param name="sOutput"></param>
		/// <param name="idFile"></param>
		void CopyFileToStream( Stream sOutput, int idFile )
		{
			using( var s = pool.GetSession() )
			using( var trans = s.BeginTransaction() )
			{
				var e = EFS.byId( rs, idFile );
				if( null == e )
					throw new FileNotFoundException();

				using( var sInput = e.data.Read( rs.cursor ) )
					EFS.CopyStream( sInput, sOutput );
			}
		}

		/// <summary>Create a virtual file from the EFS file.</summary>
		/// <param name="ee">EFS entry</param>
		/// <param name="path">Relative file name.</param>
		/// <returns>A newly-created file descriptor for <see cref="VirtualFileDataObject"/>.</returns>
		Delay.VirtualFileDataObject.FileDescriptor getFileDescr( EfsEntry ee, string path )
		{
			var res = new Delay.VirtualFileDataObject.FileDescriptor();
			res.Name = path;
			res.Length = ee.Length;
			res.ChangeTimeUtc = ee.dtModification;
			res.StreamContents = stm => CopyFileToStream( stm, ee.id );
			return res;
		}

		/// <summary>Iterate over the selected files and folders.</summary>
		/// <param name="itemIds">Selected item IDs.</param>
		/// <param name="actFile">Will be called for every file in the selection, including the files in a sumbolders.</param>
		/// <param name="actFolder">Will be called for every folder in the selection.</param>
		void IterateSelection( IEnumerable<int> itemIds, Action<EfsEntry, string> actFile, Action<EfsEntry, string> actFolder )
		{
			foreach( int idFile in itemIds )
			{
				EfsEntry ee = EFS.byId( rs, idFile );
				if( ee.isDirectory )
				{
					actFolder( ee, ee.name );
					var pending = new Queue<Tuple<string, int>>();
					pending.Enqueue( Tuple.Create( ee.name, ee.id ) );

					while( pending.Count > 0 )
					{
						var p = pending.Dequeue();

						foreach( EfsEntry eChild in EFS.ListChildren( rs, p.Item2 ) )
						{
							string path = Path.Combine( p.Item1, eChild.name );
							if( eChild.isDirectory )
							{
								pending.Enqueue( Tuple.Create( path, eChild.id ) );
								actFolder( eChild, path );
							}
							else
								actFile( eChild, path );
						}
					}
				}
				else
				{
					actFile( ee, ee.name );
				}
			}
		}

		IEnumerable<Delay.VirtualFileDataObject.FileDescriptor> getFiles( IEnumerable<int> itemIds )
		{
			var res = new List<Delay.VirtualFileDataObject.FileDescriptor>();

			IterateSelection( itemIds,
					( eFile, path ) => res.Add( getFileDescr( eFile, path ) ),
					( eFolder, path ) => { }
				);

			return res;
		}

		private void cutToolStripMenuItem_Click( object sender, EventArgs e )
		{
			throw new NotImplementedException();
		}

		private void copyToolStripMenuItem_Click( object sender, EventArgs e )
		{
			var items = getSelectedItems().ToList();
			if( items.Count < 1 )
				return;

			var vfdo = new Delay.VirtualFileDataObject( BeforeTransfer, AfterTransfer );
			vfdo.SetData( getFiles( items ) );
			Clipboard.SetDataObject( vfdo );
		}

		string m_lastExtractPath = null;

		private void extractToToolStripMenuItem_Click( object sender, EventArgs e )
		{
			var items = getSelectedItems().ToList();
			if( items.Count < 1 )
				return;

			string pathDestRoot;

			using( var fd = new Ionic.Utils.FolderBrowserDialogEx() )
			{
				fd.Description = "Choose destination folder";
				fd.ShowNewFolderButton = true;
				fd.ShowEditBox = true;
				if( !String.IsNullOrEmpty( m_lastExtractPath ) )
					fd.SelectedPath = m_lastExtractPath;
				fd.ShowFullPathInEditBox = true;
				fd.RootFolder = System.Environment.SpecialFolder.MyComputer;

				DialogResult result = fd.ShowDialog();
				if( DialogResult.OK != result )
					return;
				pathDestRoot = fd.SelectedPath;
			}

			m_lastExtractPath = pathDestRoot;

			if( !Directory.Exists( pathDestRoot ) )
				throw new FileNotFoundException();

			Cursor.Current = Cursors.WaitCursor;

			Stopwatch swAll = new Stopwatch();
			Stopwatch swLastPulse = new Stopwatch();
			TimeSpan tsPulseInterval = TimeSpan.FromSeconds( 5 );
			int nFiles = 0;
			long cbTotalBytes = 0;

			swAll.Start();
			using( var trans = sess.BeginTransaction() )
			{
				swLastPulse.Start();

				IterateSelection
				(
					items,
					( eFile, path ) =>
					{
						string pathDest = Path.Combine( pathDestRoot, path );
						if( File.Exists( pathDest ) )
							File.Delete( pathDest );

						using( var sInput = eFile.data.Read( rs.cursor ) )
							fileIo.Write( sInput, pathDest );

						FileInfo fi = new FileInfo( pathDest );
						fi.CreationTimeUtc = eFile.dtCreation;
						fi.LastWriteTimeUtc = eFile.dtModification;
						fi.LastAccessTimeUtc = eFile.dtAccess;
						fi.Attributes = eFile.attributes;

						if( swLastPulse.Elapsed > tsPulseInterval )
						{
							trans.LazyCommitAndReopen();
							swLastPulse.Reset();
							swLastPulse.Start();
						}

						nFiles++;
						cbTotalBytes += eFile.Length;
					},
					( eFolder, path ) =>
					{
						string pathDest = Path.Combine( pathDestRoot, path );
						if( File.Exists( pathDest ) )
							File.Delete( pathDest );
						if( !Directory.Exists( pathDest ) )
						{
							Directory.CreateDirectory( pathDest );

							DirectoryInfo di = new DirectoryInfo( pathDest );
							di.CreationTimeUtc = eFolder.dtCreation;
							di.LastWriteTimeUtc = eFolder.dtModification;
							di.LastAccessTimeUtc = eFolder.dtAccess;
							di.Attributes = eFolder.attributes;
						}
					}
				);

				swAll.Stop();
			}
			Cursor.Current = Cursors.Default;

			string msg = FormatCopyRateSummary( nFiles, cbTotalBytes, swAll.Elapsed );
			MessageBox.Show( this, msg, "Extract Complete", MessageBoxButtons.OK, MessageBoxIcon.Information );
		}

		#endregion

		#region Paste

		EfsEntry AddFolder( iSerializerTransaction trans, int idParent, string strPath, ref int nFiles, ref long cbTotalBytes )
		{
			string name = Path.GetFileName( strPath );
			int? idFile = EFS.FindFile( rs, idParent, name );
			if( idFile.HasValue )
			{
				EFS.DeleteFile( rs, idFile.Value );
				trans.LazyCommitAndReopen();
			}

			EfsEntry res = EFS.CreateFolder( rs.cursor, idParent, name );

			idParent = res.id;
			// Inspired by http://stackoverflow.com/questions/2085452/fast-lowlevel-method-to-recursively-process-files-in-folders/2085872#2085872
			var pending = new Queue<Tuple<string, int>>();
			pending.Enqueue( Tuple.Create( strPath, idParent ) );

			string[] tmp;
			while( pending.Count > 0 )
			{
				Tuple<string, int> p = pending.Dequeue();

				tmp = Directory.GetDirectories( p.Item1 );
				foreach( var childFolder in tmp )
				{
					name = Path.GetFileName( childFolder );
					idParent = EFS.CreateFolder( rs.cursor, p.Item2, name ).id;
					pending.Enqueue( Tuple.Create( childFolder, idParent ) );
					trans.LazyCommitAndReopen();
				}

				tmp = Directory.GetFiles( p.Item1 );
				foreach( var filePath in tmp )
				{
					nFiles++;
					long len;
					EFS.AddFile( rs, p.Item2, fileIo, filePath, out len );
					cbTotalBytes += len;
					trans.LazyCommitAndReopen();
				}
			}

			return res;
		}

		private void pasteToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( !Clipboard.ContainsFileDropList() )
				return;

			int? idWhere = this.idCurrentFolder;
			if( !idWhere.HasValue )
				return;

			int nFiles = 0;
			long nBytes = 0;

			Cursor.Current = Cursors.WaitCursor;
			Stopwatch swAll = new Stopwatch();
			List<EfsEntry> newEntries = new List<EfsEntry>();
			swAll.Start();
			using( var trans = sess.BeginTransaction() )
			{
				foreach( string strPath in Clipboard.GetFileDropList().Cast<string>() )
				{
					EfsEntry ne = null;
					if( File.Exists( strPath ) )	// Single file
					{
						nFiles++;
						long len;
						ne = EFS.AddFile( rs, idWhere.Value, fileIo, strPath, out len );
						nBytes += len;
						trans.LazyCommitAndReopen();
					}
					else if( Directory.Exists( strPath ) )
					{
						ne = AddFolder( trans, idWhere.Value, strPath, ref nFiles, ref nBytes );
					}
					if( null != ne )
						newEntries.Add( ne );
				}
				trans.Commit();
				swAll.Stop();
			}
			Cursor.Current = Cursors.Default;

			newEntries.ForEach( ne => this.AddUiItem( ne ) );

			string msg = FormatCopyRateSummary( nFiles, nBytes, swAll.Elapsed );
			MessageBox.Show( this, msg, "Paste Complete", MessageBoxButtons.OK, MessageBoxIcon.Information );
		}
		#endregion

		#endregion
	}
}