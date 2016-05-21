using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EsentSerialization.Linq
{
	/// <summary>This static class parses filter queries from C# expression tree into ESENT recordset operations.</summary>
	static class FilterQuery
	{
		enum eOperation : byte
		{
			// LessThan,
			LessThanOrEqual,
			Equal,
			GreaterThanOrEqual,
			// GreaterThan,
		}

		/// <summary>Dictionary to map ExpressionType to ESENT operation.</summary>
		static readonly Dictionary<ExpressionType, eOperation> dictTypes = new Dictionary<ExpressionType, eOperation>()
		{
			// { ExpressionType.LessThan, eOperation.LessThan },
			{ ExpressionType.LessThanOrEqual, eOperation.LessThanOrEqual },
			{ ExpressionType.Equal, eOperation.Equal },
			{ ExpressionType.GreaterThanOrEqual, eOperation.GreaterThanOrEqual },
			// { ExpressionType.GreaterThan, eOperation.GreaterThan },
		};

		/// <summary>Dictionary to invert ESENT operation, used to invert "1 &gt;= record.column" to "record.column &lt;= 1"</summary>
		static readonly Dictionary<eOperation, eOperation> dictInvert = new Dictionary<eOperation, eOperation>()
		{
			{ eOperation.LessThanOrEqual, eOperation.GreaterThanOrEqual },
			{ eOperation.Equal, eOperation.Equal },
			{ eOperation.GreaterThanOrEqual, eOperation.LessThanOrEqual },
		};

		static eOperation invert( this eOperation op )
		{
			return dictInvert[ op ];
		}

		class expression
		{
			public readonly MemberInfo column;
			public readonly eOperation op;
			public readonly Func<object, object> filterValue;
			public readonly bool multivalued;

			public IndexForColumn[] indices { get; private set; }
			public IndexForColumn selectedIndex { get; private set; }

			public expression( MemberInfo column, eOperation op, Func<object, object> filterValue, bool multivalued = false )
			{
				this.column = column;
				this.op = op;
				this.filterValue = filterValue;
				this.multivalued = multivalued;
			}
			public void lookupIndices( iTypeSerializer ser )
			{
				indices = ser.indicesFromColumn( column );
			}

			public void selectIndex( string name )
			{
				selectedIndex = indices.FirstOrDefault( ii => ii.indexName == name );
				if( null == selectedIndex )
					throw new ArgumentException( "Index {0} not found".formatWith( name ) );
			}
		}

		static MethodInfo getMethodInfo( Expression<Func<bool>> exp )
		{
			MethodCallExpression mce = (MethodCallExpression)exp.Body;
			return mce.Method;
		}

		static readonly MethodInfo miLessOrEqual = getMethodInfo( () => Queries.lessOrEqual( null,null ) );
		static readonly MethodInfo miGreaterOrEqual = getMethodInfo( () => Queries.greaterOrEqual( null,null ) );
		static readonly MethodInfo miStringContains = getMethodInfo( () => "".Contains( "" ) );
		static readonly MethodInfo miContains = getMethodInfo( () => Queries.Contains( null, null ) );

		static IEnumerable<expression> parseQuery( ParameterExpression eRecord, ParameterExpression eArgument, Expression body )
		{
			switch( body.NodeType )
			{
				case ExpressionType.Equal:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.GreaterThanOrEqual:
					{
						BinaryExpression be = (BinaryExpression)body;
						eOperation op = dictTypes[ body.NodeType ];
						return parseBinary( eRecord, eArgument, be.Left, op, be.Right );
					}

				case ExpressionType.AndAlso:
					BinaryExpression bin = (BinaryExpression)body;
					return parseQuery( eRecord, eArgument, bin.Left ).Concat( parseQuery( eRecord, eArgument, bin.Right ) );

				case ExpressionType.Call:
					MethodCallExpression mce = (MethodCallExpression)body;
					if( mce.Method == miLessOrEqual )
						return parseBinary( eRecord, eArgument, mce.Arguments[ 0 ], eOperation.LessThanOrEqual, mce.Arguments[ 1 ] );
					if( mce.Method == miGreaterOrEqual )
						return parseBinary( eRecord, eArgument, mce.Arguments[ 0 ], eOperation.GreaterThanOrEqual, mce.Arguments[ 1 ] );
					if( mce.Method == miContains )
						return parseContains( eRecord, eArgument, mce.Arguments[ 0 ], mce.Arguments[ 1 ] );
					if( isContainsMethod( mce.Method ) )
						return parseContains( eRecord, eArgument, mce.Object, mce.Arguments[ 0 ] );
					throw new NotSupportedException( "Method {0}::{1} is not supported".formatWith( mce.Method.DeclaringType.FullName, mce.Method.Name ) );
			}
			throw new NotSupportedException( "The expression {0} is not supported".formatWith( body.NodeType.ToString() ) );
		}

		static bool isContainsMethod( MethodInfo mi )
		{
			if( mi.Name == "Contains" )
			{
				Type tp = mi.DeclaringType;
				if( tp.isGenericType() && tp.GetGenericTypeDefinition() == typeof( List<> ) )
					return true;
			}
			return false;
		}

		class HasParam : ExpressionVisitor
		{
			readonly ParameterExpression eRecord;
			public HasParam( ParameterExpression r ) { eRecord = r; }
			bool found;
			protected override Expression VisitParameter( ParameterExpression node )
			{
				if( node == eRecord )
					found = true;
				return base.VisitParameter( node );
			}
			public bool hasParameter( Expression exp )
			{
				found = false;
				this.Visit( exp );
				return found;
			}
		}

		static expression[] parseBinary( ParameterExpression eRecord, ParameterExpression eArgument, Expression eLeft, eOperation op, Expression eRight )
		{
			HasParam hp = new HasParam( eRecord );
			bool leftParam = hp.hasParameter( eLeft );
			bool rightParam = hp.hasParameter( eRight );
			if( !( leftParam ^ rightParam ) )
				throw new NotSupportedException( "Binary expression is not supported: must contain a ESENT column on exactly one side of the comparison" );

			MemberInfo mi;
			Func<object, object> val;
			if( leftParam )
			{
				// The column is on the left of the expression
				mi = parseColumn( eRecord, eLeft );
				val = parseConstant( eArgument, eRight );
			}
			else
			{
				// The column is on the right of the expression
				mi = parseColumn( eRecord, eRight );
				val = parseConstant( eArgument, eLeft );
				op = op.invert();
			}
			var res = new expression( mi, op, val );
			return new expression[ 1 ] { res };
		}

		static expression[] parseContains( ParameterExpression eRecord, ParameterExpression eArgument, Expression eLeft, Expression eRight )
		{
			HasParam hp = new HasParam( eRecord );
			bool leftParam = hp.hasParameter( eLeft );
			bool rightParam = hp.hasParameter( eRight );
			if( !leftParam )
				throw new NotSupportedException( "Expression is not supported: a column must be on the left." );
			if( rightParam )
				throw new NotSupportedException( "Expression is not supported: method argument can't contain columns." );
			MemberInfo mi = parseColumn( eRecord, eLeft );
			Func<object, object> val = parseConstant( eArgument, eRight );

			var res = new expression( mi, eOperation.Equal, val, true );
			return new expression[ 1 ] { res };
		}

		static MemberInfo parseColumn( ParameterExpression eRecord, Expression exp )
		{
			if( exp.NodeType == ExpressionType.Convert || exp.NodeType == ExpressionType.ConvertChecked )
				exp = ( (UnaryExpression)exp ).Operand;

			var me = exp as MemberExpression;
			if( null == me )
				throw new NotSupportedException( "Failed to compile the query: {0} is not a member".formatWith( exp ) );

			if( me.Expression != eRecord )
				throw new NotSupportedException( "Failed to compile the query: {0} must be a column expression".formatWith( exp ) );

			return me.Member;
		}

		static Func<object, object> parseConstant( ParameterExpression eArgument, Expression exp )
		{
			if( exp.Type != typeof( object ) )
				exp = Expression.ConvertChecked( exp, typeof( object ) );
			return Expression.Lambda<Func<object, object>>( exp, eArgument ).Compile();
		}

		public static SearchQuery<tRow> query<tRow>( iTypeSerializer ser, Expression<Func<tRow, bool>> exp ) where tRow : new()
		{
			ParameterExpression eRecord = exp.Parameters[ 0 ];
			ParameterExpression eArgument = Expression.Parameter( typeof(object), "unused" );
			return queryImpl<tRow>( ser, exp.Body, eRecord, eArgument, 0 );
		}

		public static SearchQuery<tRow> query<tRow, tArg1>( iTypeSerializer ser, Expression<Func<tRow, tArg1, bool>> exp ) where tRow : new()
		{
			ParameterExpression eRecord = exp.Parameters[ 0 ];
			ParameterExpression eArgument = exp.Parameters[ 1 ];
			return queryImpl<tRow>( ser, exp.Body, eRecord, eArgument, 1 );
		}

		// For mode then 1 parameter, see this:
		// http://stackoverflow.com/a/11160067/126995

		static SearchQuery<tRow> queryImpl<tRow>( iTypeSerializer ser, Expression query, ParameterExpression eRecord, ParameterExpression eArgument, int nArguments ) where tRow : new()
		{
			// Full text search queries are handled separately
			var mce = query as MethodCallExpression;
			if( null != mce && mce.Method == miStringContains )
				return fullTextQuery<tRow>( ser, eRecord, eArgument, mce.Object, mce.Arguments[ 0 ], nArguments );

			expression[] experessions = parseQuery( eRecord, eArgument, query ).ToArray();

			if( experessions.Length <= 0 )
				throw new NotSupportedException( "Failed to parse query {0}".formatWith( query ) );

			HashSet<string> hsInds = null;
			foreach( var e in experessions )
			{
				e.lookupIndices( ser );
				IEnumerable<string> indNames = e.indices.Select( ifc => ifc.indexName );
				if( null == hsInds )
					hsInds = new HashSet<string>( indNames );
				else
					hsInds.IntersectWith( indNames );
			}
			if( hsInds.Count <= 0 )
				throw new NotSupportedException( "Failed to parse query {0}: no single index covers all referenced columns".formatWith( query ) );

			bool multi = experessions.Any( e => e.multivalued );
			foreach( string i in hsInds )
			{
				var res = tryIndex<tRow>( experessions, multi, i, nArguments );
				if( null != res )
					return res;
			}
			throw new NotSupportedException( "Failed to parse query {0}: no suitable index found".formatWith( query ) );
		}

		static SearchQuery<tRow> tryIndex<tRow>( expression[] exprs, bool multi, string indName, int argsCount ) where tRow : new()
		{
			// Choose the index
			foreach( var i in exprs )
				i.selectIndex( indName );

			// Group by column #
			var groups = exprs
				.GroupBy( e => e.selectedIndex.columnIndex )
				.OrderBy( g => g.Key )
				.ToArray();

			// Ensure the set only contains column from 0 to some number
			int[] inds = groups.Select( g => g.Key ).ToArray();
			if( inds[ 0 ] != 0 || inds[ inds.Length - 1 ] != inds.Length - 1 )
				return null;

			List<Func<object, object>> vals = new List<Func<object, object>>( inds.Length );

			for( int i = 0; i < groups.Length; i++ )
			{
				bool isLast = ( i + 1 == groups.Length );
				expression[] group = groups[ i ].ToArray();
				if( isLast )
				{
					// On the last column being queried, we support inequality operators 
					expression eq = group.FirstOrDefault( e => e.op == eOperation.Equal );
					if( null != eq )
					{
						if( group.Length > 1 )
							Debug.WriteLine( "Warning: ignoring extra conditions on the column {0}", eq.column.Name );
						vals.Add( eq.filterValue );
						var arr = vals.ToArray();
						return new SearchQuery<tRow>( ( rs, arg ) => findEqual( rs, indName, arr, arg ), argsCount, multi );
					}

					expression[] less = group.Where( e => e.op == eOperation.LessThanOrEqual ).ToArray();
					expression[] greater = group.Where( e => e.op == eOperation.GreaterThanOrEqual ).ToArray();
					if( less.Length > 1 || greater.Length > 1 )
						Debug.WriteLine( "Warning: ignoring extra conditions on the column {0}", eq.column.Name );
					Func<object, object> lastFrom = greater.Select( e => e.filterValue ).FirstOrDefault();
					Func<object, object> lastTo = less.Select( e => e.filterValue ).FirstOrDefault();
					return new SearchQuery<tRow>( ( rs, arg ) => findBetween( rs, indName, vals.ToArray(), arg, lastFrom, lastTo ), argsCount, multi );
				}
				else
				{
					// For non-last column, we require single '==' comparison
					if( group.Length != 1 )
						return null;
					if( group[ 0 ].op != eOperation.Equal )
						return null;
					vals.Add( group[ 0 ].filterValue );
				}
			}
			return null;
		}

		static void findEqual<tRow>( Recordset<tRow> rs, string indName, Func<object, object>[] values, object arg ) where tRow : new()
		{
			object[] obj = values.Select( f => f( arg ) ).ToArray();
			rs.filterFindEqual( indName, obj );
		}

		static void findBetween<tRow>( Recordset<tRow> rs, string indName, Func<object, object>[] values, object arg, Func<object, object> lastFrom, Func<object, object> lastTo ) where tRow : new()
		{
			List<object> from = new List<object>( values.Length + 1 );
			List<object> to = new List<object>( values.Length + 1 );
			for( int i = 0; i < values.Length; i++ )
			{
				object val = values[ i ]( arg );
				from.Add( val );
				to.Add( val );
			}
			if( null != lastFrom )
				from.Add( lastFrom( arg ) );
			if( null != lastTo )
				to.Add( lastTo( arg ) );
			rs.filterFindBetween( indName, from.ToArray(), to.ToArray() );
		}

		static SearchQuery<tRow> fullTextQuery<tRow>( iTypeSerializer ser, ParameterExpression eRecord, ParameterExpression eArgument, Expression eLeft, Expression eRight, int argsCount ) where tRow : new()
		{
			HasParam hp = new HasParam( eRecord );
			
			bool leftParam = hp.hasParameter( eLeft );
			if( !leftParam )
				throw new NotSupportedException( "Full-text queries must have column as the first argument" );
			MemberInfo column = parseColumn( eRecord, eLeft );

			bool rightParam = hp.hasParameter( eRight );
			if( rightParam )
				throw new NotSupportedException( "Full-text queries can't include column in the second argument" );
			Func<object, object> arg = parseConstant( eArgument, eRight );

			IndexForColumn[] inds = ser.indicesFromColumn( column );
			IndexForColumn ind = inds.FirstOrDefault( i => i.columnIndex == 0 && i.attrib is Attributes.EseTupleIndexAttribute );
			if( null == ind )
				throw new NotSupportedException( "Failed to parse full-text search query: no suitable index found" );

			string indName = ind.indexName;
			Action<Recordset<tRow>, object> act = ( rs, a ) => rs.filterFindSubstring( indName, arg( a ) );
			return new SearchQuery<tRow>( act, argsCount, true );
		}
	}
}