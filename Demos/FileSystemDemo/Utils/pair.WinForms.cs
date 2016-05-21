using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

static partial class Global
{
	/// <summary>Bind the List&lt;pair&lt;tValue, string&gt;&gt; to the ComboBox so that fieldFirst is the value member,
	/// and fieldSecond is display member.</summary>
	/// <param name="cb">The combobox to bind</param>
	/// <param name="coll">List&lt;pair&lt;tValue, string&gt;&gt;</param>
	public static void Bind( this ComboBox cb, System.Collections.IList coll )
	{
		cb.ValueMember = "Item1";
		cb.DisplayMember = "Item2";
		cb.DataSource = coll;
	}

	/// <summary>Bind the combobox to the source data.</summary>
	/// <param name="cb"></param>
	/// <param name="src"></param>
	/// <param name="selectValue">Functor that maps the input element to the combobox item values.</param>
	/// <param name="selectDisplayName">Functor that maps the input element to the item display name.</param>
	public static void Bind<tSource, tValue>( this ComboBox cb, IEnumerable<tSource> src,
		Func<tSource, tValue> selectValue, Func<tSource, string> selectDisplayName )
	{
		cb.Bind( src.Select( e => Tuple.Create( selectValue( e ), selectDisplayName( e ) ) ).ToList() );
	}
}