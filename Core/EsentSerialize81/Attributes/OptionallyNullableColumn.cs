using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization.Attributes
{
	/// <summary>Abstract base class for the columns that may either be nullable or not, depending on whether the field/property type is nullable or not.</summary>
	/// <remarks>
	/// <para>The specific classes are in the <see cref="Attributes" /> namespace.</para>
	/// <para>If you need to, you can also create your own column type attributes by inheriting from this base class.</para>
	/// </remarks>
	public abstract class OptionallyNullableColumn : EseColumnAttrubuteBase
	{
		/// <summary>Initialize with the default column name, which is the name of the field/property.</summary>
		public OptionallyNullableColumn() { }
		/// <summary>Initialize with non-default column name.</summary>
		public OptionallyNullableColumn( string _columnName ) : base( _columnName ) { }

		/// <summary>true if this column is nullable.</summary>
		protected bool m_bFieldNullable = false;
		/// <summary>Returns true if this column is nullable.</summary>
		public override bool bFieldNullable { get { return m_bFieldNullable; } }

		/// <summary>Get column definition.</summary>
		/// <remarks>
		/// <para>Because this class is abstract, the definition returned by this method contains no column type.</para>
		/// <para>It only has ColumndefGrbit.ColumnFixed flag, and optionally ColumndefGrbit.ColumnNotNULL flag.</para>
		/// </remarks>
		public override JET_COLUMNDEF getColumnDef()
		{
			JET_COLUMNDEF res = new JET_COLUMNDEF();
			res.grbit = ColumndefGrbit.ColumnFixed;
			if( !m_bFieldNullable )
				res.grbit |= ColumndefGrbit.ColumnNotNULL;
			return res;
		}

		/// <summary>Verify the type.
		/// Throws SerializationException if the type is incompatible.</summary>
		/// <typeparam name="tDesired">The desired basic type.</typeparam>
		/// <param name="t">The type of the property/method this attribute is applied to.</param>
		protected void verifyBasicTypeSupport<tDesired>( Type t ) where tDesired : struct
		{
			if( t.Equals( typeof( tDesired ) ) )
			{
				m_bFieldNullable = false;
				return;
			}
			if( t.Equals( typeof( Nullable<tDesired> ) ) )
			{
				m_bFieldNullable = true;
				return;
			}
			throw new System.Runtime.Serialization.SerializationException();
		}
	}
}