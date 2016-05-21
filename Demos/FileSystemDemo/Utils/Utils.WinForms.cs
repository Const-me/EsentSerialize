using System;
using System.Collections.Generic;
using System.Windows.Forms;

static partial class Global
{
	public static IEnumerable<TreeNode> All( this TreeNodeCollection tnc )
	{
		if( null == tnc || tnc.Count <= 0 )
			yield break;

		for( int i = 0; i < tnc.Count; i++ )
			yield return tnc[ i ];
	}

	public static IEnumerable<TreeNode> AllRecursive( this TreeNodeCollection tnc )
	{
		if( null == tnc || tnc.Count <= 0 )
			yield break;

		for( int i = 0; i < tnc.Count; i++ )
		{
			var n = tnc[ i ];
			yield return n;
			foreach( var c in n.Nodes.AllRecursive() )
				yield return c;
		}
	}

	public static IEnumerable<ListViewItem> All( this ListView lv )
	{
		ListView.ListViewItemCollection lvic = lv.Items;

		if( null == lvic || lvic.Count <= 0 )
			yield break;

		for( int i = 0; i < lvic.Count; i++ )
			yield return lvic[ i ];
	}

	public static IEnumerable<ListViewItem> Selected( this ListView lv )
	{
		var slvic = lv.SelectedItems;

		if( null == slvic || slvic.Count <= 0 )
			yield break;

		for( int i = 0; i < slvic.Count; i++ )
			yield return slvic[ i ];
	}
}