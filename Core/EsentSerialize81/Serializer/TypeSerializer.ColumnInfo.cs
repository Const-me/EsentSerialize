using EsentSerialization.Attributes;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace EsentSerialization
{
	public partial class TypeSerializer
	{
		/// <summary>This class represents the table column, and implements the get/set operations.</summary>
		/// <remarks>
		/// <para>Hopefully, the boxing/unboxing on every getValue/setValue operation is not that big deal in terms of performance.</para>
		/// </remarks>
		public class ColumnInfo
		{
			readonly string m_columnName;
			readonly EseColumnAttrubuteBase m_attribute;

			/// <summary>True if this column is also marked with [Obsolete] attribute.</summary>
			public readonly bool isObsolete;
			JET_COLUMNID m_idColumn;
			internal readonly Type tpValue;

			/// <summary></summary>
			/// <param name="_name">The name of the column.</param>
			/// <param name="member">Either FieldInfo or PropertyInfo of the member who has the _attr attribute applied.</param>
			/// <param name="_attr">The attribute.</param>
			/// <param name="obsolete">True if the column is also marked with [Obsolete] attribute.</param>
			public ColumnInfo( string _name, MemberInfo member, EseColumnAttrubuteBase _attr, bool obsolete )
			{
				m_columnName = _name;
				m_attribute = _attr;
				m_idColumn = JET_COLUMNID.Nil;
				this.isObsolete = obsolete;

				// http://www.palmmedia.de/Blog/2012/2/4/reflection-vs-compiled-expressions-vs-delegates-performance-comparision

				ParameterExpression targetExpObject = Expression.Parameter( typeof( object ), "record" );   //  object record
				UnaryExpression targetExp = Expression.Convert( targetExpObject, member.DeclaringType );    //  (RecordType)record

				MemberExpression memberExp = null;

				if( member is FieldInfo )
				{
					FieldInfo field = member as FieldInfo;
					tpValue = field.FieldType;
					memberExp = Expression.Field( targetExp, field );    //  (((RecordType)record).field)
				}
				else if( member is PropertyInfo )
				{
					PropertyInfo property = member as PropertyInfo;
					tpValue = property.PropertyType;
					memberExp = Expression.Property( targetExp, property );   //  (((RecordType)record).property)
				}
				else
					throw new ArgumentException();

				UnaryExpression memberExpObject = Expression.Convert( memberExp, typeof( object ) );      //  (object)(((RecordType)record).property)
				this.getValue = Expression.Lambda<Func<object, object>>( memberExpObject, targetExpObject ).Compile();

				ParameterExpression valueExpObject = Expression.Parameter( typeof( object ), "value" );   //  object value
				UnaryExpression valueExp = Expression.Convert( valueExpObject, tpValue );   //  (PropertyType)value
				BinaryExpression assignExp = Expression.Assign( memberExp, valueExp );      //  ((RecordType)record).property = (PropertyType)value
				this.setValue = Expression.Lambda<Action<object, object>>( assignExp, targetExpObject, valueExpObject ).Compile();
			}

			/// <summary>The name of the ESENT column.</summary>
			public string columnName { get { return m_columnName; } }
			/// <summary>The instance of the EseColumnAttrubuteBase-derived attribute applied to the property or method of the record class.</summary>
			public EseColumnAttrubuteBase attrib { get { return m_attribute; } }
			/// <summary>Column ID.</summary>
			public JET_COLUMNID idColumn { get { return m_idColumn; } }

			/// <summary>This operation is very rare, and is only used internally.
			/// That's why it's the separate method, instead of just 'set' accessor for the idColumn property.</summary>
			internal void setColumnID( JET_COLUMNID _idColumn ) { m_idColumn = _idColumn; }

			/// <summary>Get the value from the record object.</summary>
			readonly Func<object, object> getValue;

			/// <summary>Update the value of the field / property of the record object.</summary>
			readonly Action<object, object> setValue;

			/// <summary>Get the value from object, store it in the DB.</summary>
			/// <param name="cur">The table cursor.</param>
			/// <param name="rec">The object.</param>
			/// <param name="bNewRecord">True for INSERT operations, false for UPDATE operations.</param>
			public void Serialize( EseCursorBase cur, object rec, bool bNewRecord )
			{
				attrib.Serialize( cur, idColumn, getValue( rec ), bNewRecord );
			}

			/// <summary>Update a single columt in the DB.</summary>
			/// <param name="cur">The table cursor.</param>
			/// <param name="value">The new value for the column.</param>
			public void SerializeSingleField( EseCursorBase cur, object value )
			{
				attrib.Serialize( cur, idColumn, value, false );
			}

			/// <summary>Just get the value from the DB, and return.</summary>
			/// <param name="cur">The table cursor.</param>
			/// <returns>Column value for the current record of the cursor.</returns>
			public object DeSerialize( EseCursorBase cur )
			{
				return attrib.Deserialize( cur, idColumn );
			}

			/// <summary>Get the value from the DB, and put it to the object.</summary>
			/// <param name="cur">The table cursor.</param>
			/// <param name="rec">The record object.</param>
			public void DeSerialize( EseCursorBase cur, object rec )
			{
				object val = DeSerialize( cur );
				setValue( rec, val );
			}

			/// <summary>Retrieve an auto-increment column value</summary>
			/// <param name="cur">The table cursor.</param>
			/// <param name="rec">The object.</param>
			/// <seealso href="http://managedesent.codeplex.com/wikipage?title=HowDoI" />
			public void RefreshAutoincValue( EseCursorBase cur, object rec )
			{
				var aia = attrib as Attributes.EseAutoIdAttribute;
				if( null != aia )
				{
					object val = aia.RetrieveCopy( cur, idColumn );
					if( null != val )
						setValue( rec, val );
					return;
				}

				throw new NotSupportedException( "RefreshAutoincValue method is only supported for AutoID columns." );
			}
		}
	}
}