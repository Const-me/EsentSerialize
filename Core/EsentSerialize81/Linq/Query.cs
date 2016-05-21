using System;

namespace EsentSerialization.Linq
{
	/// <summary>Pre-compiled query for a recordset.</summary>
	/// <remarks>Queries don't have thread or session affinity.
	/// They are readonly objects safe to be used by multiple threads at the same time.
	/// Performance-wise, it's a good idea to cache those queries: parsing C# AST isn't terribly fast, especially for complex queries.</remarks>
	public abstract class Query<tRow> where tRow : new()
	{
		internal readonly Action<Recordset<tRow>> query;

		/// <summary>True if any of the column affected by this query is multivalued, or if the index is tuple index.</summary>
		public readonly bool multivalues;

		/// <summary>Construct the query</summary>
		/// <param name="act">Action to actually filter and/or sort those recordset/</param>
		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public Query( Action<Recordset<tRow>> act, bool multivalues )
		{
			query = act;
			this.multivalues = multivalues;
		}
	}

	/// <summary>Pre-compiled sort for a recordset.</summary>
	public class SortQuery<tRow> : Query<tRow> where tRow : new()
	{
		/// <summary>Construct the query</summary>
		/// <param name="act">Action to actually filter and/or sort those recordset/</param>
		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public SortQuery( Action<Recordset<tRow>> act, bool multivalues ):
			base( act, multivalues ) { }
	}

	/// <summary>Pre-compiled sort for a recordset.</summary>
	public class SearchQuery<tRow> : Query<tRow> where tRow : new()
	{
		/// <summary>Construct the query</summary>
		/// <param name="act">Action to actually filter and/or sort those recordset/</param>
		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public SearchQuery( Action<Recordset<tRow>> act, bool multivalues ) :
			base( act, multivalues )
		{ }
	}
}