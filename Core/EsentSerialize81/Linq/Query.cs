using System;

namespace EsentSerialization.Linq
{
	/// <summary>Pre-compiled query for a recordset.</summary>
	/// <remarks>Queries don't have thread or session affinity.
	/// They are readonly objects safe to be used by multiple threads at the same time.
	/// Performance-wise, it's a good idea to cache those queries: parsing C# AST isn't terribly fast, especially for complex queries.</remarks>
	public abstract class Query<tRow> where tRow : new()
	{
		/// <summary>True if any of the column affected by this query is multivalued, or if the index is tuple index.</summary>
		public readonly bool multivalues;

		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public Query( bool multivalues )
		{
			this.multivalues = multivalues;
		}

		/// <summary>Run the query.</summary>
		public abstract void query( Recordset<tRow> rs, params object[] args );
	}

	/// <summary>Pre-compiled sort for a recordset.</summary>
	public class SortQuery<tRow> : Query<tRow> where tRow : new()
	{
		internal readonly Action<Recordset<tRow>> m_query;

		/// <summary>Construct the query</summary>
		/// <param name="act">Action to actually filter and/or sort those recordset/</param>
		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public SortQuery( Action<Recordset<tRow>> act, bool multivalues ) :
			base( multivalues )
		{
			m_query = act;
		}

		/// <summary>Run the query.</summary>
		public override void query( Recordset<tRow> rs, params object[] args )
		{
			if( null != args && args.Length > 0 )
				throw new ArgumentException( "No arguments expected" );
			m_query( rs );
		}
	}

	/// <summary>Pre-compiled sort for a recordset.</summary>
	public class SearchQuery<tRow> : Query<tRow> where tRow : new()
	{
		readonly Action<Recordset<tRow>, object> m_query;
		readonly int nArguments;

		/// <summary>Construct the query</summary>
		/// <param name="act">Action to actually filter and/or sort those recordset/</param>
		/// <param name="nArguments">How many arguments in the query, can be 0 = this query takes no arguments.</param>
		/// <param name="multivalues">True to use uniq() for fetching the results.</param>
		public SearchQuery( Action<Recordset<tRow>, object> act, int nArguments, bool multivalues ) :
			base( multivalues )
		{
			m_query = act;
			this.nArguments = nArguments;
		}

		/// <summary>Run the query.</summary>
		public override void query( Recordset<tRow> rs, params object[] args )
		{
			if( args.Length != nArguments )
				throw new ArgumentException( "Expected {0} arguments, got {1}".formatWith( nArguments, args.Length ) );
			if( 0 == nArguments )
				m_query( rs, null );
			else if( 1 == nArguments )
				m_query( rs, args[ 0 ] );
			else
				m_query( rs, args );
		}
	}
}