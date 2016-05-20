using System;

namespace EsentSerialization.Linq
{
	/// <summary>Pre-compiled query for a recordset.</summary>
	/// <remarks>Queries don't have thread or session affinity.
	/// They are readonly objects safe to be used by multiple threads at the same time.
	/// Performance-wise, it's a good idea to cache those queries: parsing C# AST isn't terribly fast, especially for complex queries.</remarks>
	public class Query<tRow> where tRow : new()
	{
		internal readonly Action<Recordset<tRow>> query;

		public readonly bool multivalues;

		public Query( Action<Recordset<tRow>> act, bool multivalues = false )
		{
			query = act;
			this.multivalues = multivalues;
		}
	}
}