using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace EsentSerialization.Linq
{
	static partial class FilterQuery
	{
		/// <summary>Visitor that searches the specific parameter in the tree.</summary>
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

			/// <summary>Search for the parameter.</summary>
			public bool hasParameter( Expression exp )
			{
				found = false;
				this.Visit( exp );
				return found;
			}
		}

		/// <summary>Visitor that produces expressions which converts parameter from object to original type, and invokes the original expression passing that value.</summary>
		class ConvertParamType : ExpressionVisitor
		{
			readonly ParameterExpression eValue;
			public readonly ParameterExpression newParam;
			readonly Expression replacement;
			public ConvertParamType( ParameterExpression v )
			{
				eValue = v;
				if( eValue.Type == typeof( object ) )
				{
					newParam = eValue;
					replacement = null;
					return;
				}
				newParam = Expression.Parameter( typeof( object ), "arg1" );
				replacement = Expression.Convert( newParam, eValue.Type );
			}

			protected override Expression VisitParameter( ParameterExpression node )
			{
				if( node != eValue || null == newParam )
					return base.VisitParameter( node );
				return replacement;
			}
		}

		/// <summary>Visitor that produces expressions which converts parameter from object to array of objects, and invokes the original expression passing the items of that array.</summary>
		class ConvertParamsToArray : ExpressionVisitor
		{
			public readonly ParameterExpression pRecord;
			public readonly ParameterExpression pArray;
			readonly Dictionary<ParameterExpression, Expression> replacements;

			public ConvertParamsToArray( ReadOnlyCollection<ParameterExpression> args )
			{
				pRecord = args[ 0 ];

				pArray = Expression.Parameter( typeof( object ), "arg1" );
				Expression objArray = Expression.Convert( pArray, typeof( object[] ) );
				replacements = new Dictionary<ParameterExpression, Expression>( args.Count - 1 );

				for( int i = 1; i < args.Count; i++ )
				{
					Expression ind = Expression.Constant( i, typeof( int ) );
					Expression replace = Expression.ArrayIndex( objArray, ind );
					ParameterExpression p = args[ i ];

					if( p.GetType() != typeof( object ) )
						replace = Expression.Convert( replace, p.GetType() );

					replacements[ p ] = replace;
				}
			}

			protected override Expression VisitParameter( ParameterExpression node )
			{
				Expression r;
				if( replacements.TryGetValue( node, out r ) )
					return r;
				return base.VisitParameter( node );
			}
		}
	}
}