namespace Test1
{
	partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( null != fileIo )
			{
				fileIo.Dispose();
				fileIo = null;
			}

			if( null != sess )
			{
				sess.Dispose();
				sess = null;
			}

			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( Form1 ) );
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.tree = new System.Windows.Forms.TreeView();
			this.menu = new System.Windows.Forms.ContextMenuStrip( this.components );
			this.newFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.extractToToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.icons16 = new System.Windows.Forms.ImageList( this.components );
			this.list = new System.Windows.Forms.ListView();
			this.icons32 = new System.Windows.Forms.ImageList( this.components );
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.menu.SuspendLayout();
			this.SuspendLayout();
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point( 0, 0 );
			this.splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add( this.tree );
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add( this.list );
			this.splitContainer1.Size = new System.Drawing.Size( 824, 573 );
			this.splitContainer1.SplitterDistance = 237;
			this.splitContainer1.TabIndex = 0;
			// 
			// tree
			// 
			this.tree.ContextMenuStrip = this.menu;
			this.tree.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tree.FullRowSelect = true;
			this.tree.ImageIndex = 0;
			this.tree.ImageList = this.icons16;
			this.tree.LabelEdit = true;
			this.tree.Location = new System.Drawing.Point( 0, 0 );
			this.tree.Name = "tree";
			this.tree.SelectedImageIndex = 0;
			this.tree.Size = new System.Drawing.Size( 237, 573 );
			this.tree.TabIndex = 0;
			this.tree.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler( this.tree_AfterLabelEdit );
			this.tree.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler( this.tree_BeforeExpand );
			this.tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler( this.tree_AfterSelect );
			this.tree.BeforeLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler( this.tree_BeforeLabelEdit );
			// 
			// menu
			// 
			this.menu.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.newFolderToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.deleteToolStripMenuItem,
            this.extractToToolStripMenuItem,
            this.toolStripSeparator1,
            this.cutToolStripMenuItem,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem} );
			this.menu.Name = "menu";
			this.menu.Size = new System.Drawing.Size( 157, 186 );
			this.menu.Opening += new System.ComponentModel.CancelEventHandler( this.menu_Opening );
			// 
			// newFolderToolStripMenuItem
			// 
			this.newFolderToolStripMenuItem.Name = "newFolderToolStripMenuItem";
			this.newFolderToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.newFolderToolStripMenuItem.Text = "New folder";
			this.newFolderToolStripMenuItem.Click += new System.EventHandler( this.newFolderToolStripMenuItem_Click );
			// 
			// renameToolStripMenuItem
			// 
			this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
			this.renameToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F2;
			this.renameToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.renameToolStripMenuItem.Text = "Rename";
			this.renameToolStripMenuItem.Click += new System.EventHandler( this.renameToolStripMenuItem_Click );
			// 
			// deleteToolStripMenuItem
			// 
			this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
			this.deleteToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
			this.deleteToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.deleteToolStripMenuItem.Text = "Delete";
			this.deleteToolStripMenuItem.Click += new System.EventHandler( this.deleteToolStripMenuItem_Click );
			// 
			// extractToToolStripMenuItem
			// 
			this.extractToToolStripMenuItem.Name = "extractToToolStripMenuItem";
			this.extractToToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.extractToToolStripMenuItem.Text = "Extract To...";
			this.extractToToolStripMenuItem.Click += new System.EventHandler( this.extractToToolStripMenuItem_Click );
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size( 153, 6 );
			// 
			// cutToolStripMenuItem
			// 
			this.cutToolStripMenuItem.Name = "cutToolStripMenuItem";
			this.cutToolStripMenuItem.ShortcutKeys = ( (System.Windows.Forms.Keys)( ( System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.Delete ) ) );
			this.cutToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.cutToolStripMenuItem.Text = "Cut";
			this.cutToolStripMenuItem.Click += new System.EventHandler( this.cutToolStripMenuItem_Click );
			// 
			// copyToolStripMenuItem
			// 
			this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
			this.copyToolStripMenuItem.ShortcutKeys = ( (System.Windows.Forms.Keys)( ( System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Insert ) ) );
			this.copyToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.copyToolStripMenuItem.Text = "Copy";
			this.copyToolStripMenuItem.Click += new System.EventHandler( this.copyToolStripMenuItem_Click );
			// 
			// pasteToolStripMenuItem
			// 
			this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
			this.pasteToolStripMenuItem.ShortcutKeys = ( (System.Windows.Forms.Keys)( ( System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.Insert ) ) );
			this.pasteToolStripMenuItem.Size = new System.Drawing.Size( 156, 22 );
			this.pasteToolStripMenuItem.Text = "Paste";
			this.pasteToolStripMenuItem.Click += new System.EventHandler( this.pasteToolStripMenuItem_Click );
			// 
			// icons16
			// 
			this.icons16.ImageStream = ( (System.Windows.Forms.ImageListStreamer)( resources.GetObject( "icons16.ImageStream" ) ) );
			this.icons16.TransparentColor = System.Drawing.Color.Transparent;
			this.icons16.Images.SetKeyName( 0, "streamer-16.png" );
			this.icons16.Images.SetKeyName( 1, "folder-16.png" );
			this.icons16.Images.SetKeyName( 2, "unknownFile-16.png" );
			// 
			// list
			// 
			this.list.ContextMenuStrip = this.menu;
			this.list.Dock = System.Windows.Forms.DockStyle.Fill;
			this.list.LabelEdit = true;
			this.list.LargeImageList = this.icons32;
			this.list.Location = new System.Drawing.Point( 0, 0 );
			this.list.Name = "list";
			this.list.Size = new System.Drawing.Size( 583, 573 );
			this.list.SmallImageList = this.icons16;
			this.list.TabIndex = 0;
			this.list.UseCompatibleStateImageBehavior = false;
			this.list.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler( this.list_MouseDoubleClick );
			this.list.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler( this.list_AfterLabelEdit );
			// 
			// icons32
			// 
			this.icons32.ImageStream = ( (System.Windows.Forms.ImageListStreamer)( resources.GetObject( "icons32.ImageStream" ) ) );
			this.icons32.TransparentColor = System.Drawing.Color.Transparent;
			this.icons32.Images.SetKeyName( 0, "streamer-16.png" );
			this.icons32.Images.SetKeyName( 1, "folder-32.png" );
			this.icons32.Images.SetKeyName( 2, "unknownFile-32.png" );
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size( 824, 573 );
			this.Controls.Add( this.splitContainer1 );
			this.MinimumSize = new System.Drawing.Size( 320, 200 );
			this.Name = "Form1";
			this.Text = "ESE File System Demo";
			this.splitContainer1.Panel1.ResumeLayout( false );
			this.splitContainer1.Panel2.ResumeLayout( false );
			this.splitContainer1.ResumeLayout( false );
			this.menu.ResumeLayout( false );
			this.ResumeLayout( false );

		}

		#endregion

		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.TreeView tree;
		private System.Windows.Forms.ListView list;
		private System.Windows.Forms.ContextMenuStrip menu;
		private System.Windows.Forms.ToolStripMenuItem newFolderToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
		private System.Windows.Forms.ImageList icons16;
		private System.Windows.Forms.ImageList icons32;
		private System.Windows.Forms.ToolStripMenuItem extractToToolStripMenuItem;

	}
}

