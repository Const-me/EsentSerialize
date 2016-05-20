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
			public readonly Func<object> filterValue;

			public IndexForColumn[] indices { get; private set; }
			public IndexForColumn selectedIndex { get; private set; }

			public expression( MemberInfo column, eOperation op, Func<object> filterValue )
			{
				this.column = column;
				this.op = op;
				this.filterValue = filterValue;
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

		static IEnumerable<expression> parseQuery( ParameterExpression eParam, Expression body )
		{
			switch( body.NodeType )
			{
				case ExpressionType.Equal:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.GreaterThanOrEqual:
					{
						BinaryExpression be = (BinaryExpression)body;
						eOperation op = dictTypes[ body.NodeType ];
						return parseBinary( eParam, be.Left, op, be.Right );
					}

				case ExpressionType.AndAlso:
					BinaryExpression bin = (BinaryExpression)body;
					return parseQuery( eParam, bin.Left ).Concat( parseQuery( eParam, bin.Right ) );

				case ExpressionType.Call:
					MethodCallExpression mce = (MethodCallExpression)body;
					if( mce.Method == miLessOrEqual )
						return parseBinary( eParam, mce.Arguments[ 0 ], eOperation.LessThanOrEqual, mce.Arguments[ 1 ] );
					if( mce.Method == miGreaterOrEqual )
						return parseBinary( eParam, mce.Arguments[ 0 ], eOperation.GreaterThanOrEqual, mce.Arguments[ 1 ] );
					throw new NotSupportedException( "Method {0}::{1} is not supported".formatWith( mce.Method.DeclaringType.FullName, mce.Method.Name ) );
			}
			throw new NotSupportedException( "The expression {0} is not supported".formatWith( body.NodeType.ToString() ) );
		}

		class HasParam : ExpressionVisitor
		{
			bool found;
			protected override Expression VisitParameter( ParameterExpression node )
			{
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

		static expression[] parseBinary( ParameterExpression eParam, Expression eLeft, eOperation op, Expression eRight )
		{
			HasParam hp = new HasParam();
			bool leftParam = hp.hasParameter( eLeft );
			bool rightParam = hp.hasParameter( eRight );
			if( !( leftParam ^ rightParam ) )
				throw new NotSupportedException( "Binary expression is not supported: must contain a ESENT column on exactly one side of the comparison" );

			MemberInfo mi;
			Func<object> val;
			if( leftParam )
			{
				// The column is on the left of the expression
				mi = parseColumn( eParam, eLeft );
				val = parseConstant( eRight );
			}
			else
			{
				// The column is on the right of the expression
				mi = parseColumn( eParam, eRight );
				val = parseConstant( eLeft );
				op = op.invert();
			}
			var res = new expression( mi, op, val );
			return new expression[ 1 ] { res };
		}

		static MemberInfo parseColumn( ParameterExpression eParam, Expression exp )
		{
			if( exp.NodeType == ExpressionType.Convert || exp.NodeType == ExpressionType.ConvertChecked )
				exp = ( (UnaryExpression)exp ).Operand;

			var me = exp as MemberExpression;
			if( null == me )
				throw new NotSupportedException( "Failed to compile the query: {0} is not a member".formatWith( exp ) );

			if( me.Expression != eParam )
				throw new NotSupportedException( "Failed to compile the query: {0} must be a column expression".formatWith( exp ) );

			return me.Member;
		}

		static Func<object> parseConstant( Expression exp )
		{
			if( exp.Type != typeof( object ) )
				exp = Expression.ConvertChecked( exp, typeof( object ) );
			return Expression.Lambda<Func<object>>( exp ).Compile();
		}

		public static Query<tRow> query<tRow>( iTypeSerializer ser, Expression<Func<tRow, bool>> exp ) where tRow : new()
		{
			// Full text search queries are handled separately
			var mce = exp.Body as MethodCallExpression;
			if( null != mce && mce.Method == miStringContains )
				return fullTextQuery<tRow>( ser, exp.Parameters[ 0 ], mce.Object, mce.Arguments[ 0 ] );

			ParameterExpression eParam = exp.Parameters[ 0 ];
			expression[] experessions = parseQuery( eParam, exp.Body ).ToArray();

			if( experessions.Length <= 0 )
				throw new NotSupportedException( "Failed to parse query {0}".formatWith( exp ) );

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
				throw new NotSupportedException( "Failed to parse query {0}: no single index covers all referenced columns".formatWith( exp ) );

			foreach( string i in hsInds )
			{
				var res = tryIndex<tRow>( experessions, i );
				if( null != res )
					return res;
			}
			throw new NotSupportedException( "Failed to parse query {0}: no suitable index found".formatWith( exp ) );
		}

		static Query<tRow> tryIndex<tRow>( expression[] exprs, string indName ) where tRow : new()
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

			List<Func<object>> vals = new List<Func<object>>( inds.Length );

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
						return new Query<tRow>( rs => findEqual( rs, indName, arr ) );
					}

					expression[] less = group.Where( e => e.op == eOperation.LessThanOrEqual ).ToArray();
					expression[] greater = group.Where( e => e.op == eOperation.GreaterThanOrEqual ).ToArray();
					if( less.Length > 1 || greater.Length > 1 )
						Debug.WriteLine( "Warning: ignoring extra conditions on the column {0}", eq.column.Name );
					Func<object> lastFrom = greater.Select( e => e.filterValue ).FirstOrDefault();
					Func<object> lastTo = less.Select( e => e.filterValue ).FirstOrDefault();
					return new Query<tRow>( rs => findBetween( rs, indName, vals.ToArray(), lastFrom, lastTo ) );
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

		static void findEqual<tRow>( Recordset<tRow> rs, string indName, Func<object>[] values ) where tRow : new()
		{
			object[] obj = values.Select( f => f()).ToArray();
			rs.filterFindEqual( indName, obj );
		}

		static void findBetween<tRow>( Recordset<tRow> rs, string indName, Func<object>[] values, Func<object> lastFrom, Func<object> lastTo ) where tRow : new()
		{
			List<object> from = new List<object>( values.Length + 1 );
			List<object> to = new List<object>( values.Length + 1 );
			for( int i = 0; i < values.Length; i++ )
			{
				object val = values[i]();
				from.Add( val );
				to.Add( val );
			}
			if( null != lastFrom )
				from.Add( lastFrom() );
			if( null != lastTo )
				to.Add( lastTo() );
			rs.filterFindBetween( indName, from.ToArray(), to.ToArray() );
		}

		static Query<tRow> fullTextQuery<tRow>( iTypeSerializer ser, ParameterExpression eParam, Expression eLeft, Expression eRight ) where tRow : new()
		{
			HasParam hp = new HasParam();
			
			bool leftParam = hp.hasParameter( eLeft );
			if( !leftParam )
				throw new NotSupportedException( "Full-text queries must have column as the first argument" );
			MemberInfo column = parseColumn( eParam, eLeft );

			bool rightParam = hp.hasParameter( eRight );
			if( rightParam )
				throw new NotSupportedException( "Full-text queries can't include column in the second argument" );
			Func<object> arg = parseConstant( eRight );

			IndexForColumn[] inds = ser.indicesFromColumn( column );
			IndexForColumn ind = inds.FirstOrDefault( i => i.columnIndex == 0 && i.attrib is Attributes.EseTupleIndexAttribute );
			if( null == ind )
				throw new NotSupportedException( "Failed to parse full-text search query: no suitable index found" );

			string indName = ind.indexName;
			Action<Recordset<tRow>> act = rs => rs.filterFindSubstring( indName, arg() );
			return new Query<tRow>( act, true );
		}
	}
}