using System;

namespace EsentSerialization.Attributes
{
	/// <summary>This attribute marks the object as the ESE record.</summary>
	/// <remarks>Applying this attribute to a class declares a new table in the ESE database,
	/// with its schema suitable for storing instances of the record class.<br />
	/// Table columns are defined by 'Ese*Attribute' attributes applied to the properties and/or fields of the record class.<br />
	/// Table indices are defined by 'EseIndexAttribute' (or derived classes) attributes applied to the record class.</remarks>
	[AttributeUsage( AttributeTargets.Class, Inherited = false, AllowMultiple = false )]
	public sealed class EseTableAttribute : Attribute
	{
		/// <summary>The name of the ESE table</summary>
		public readonly string tableName;

		/// <summary>Initialize with the default table name, which is the name of the class.</summary>
		public EseTableAttribute()
		{
			tableName = null;
		}

		/// <summary>Initialize with non-default table name.</summary>
		public EseTableAttribute( string strTableName )
		{
			tableName = strTableName;
		}
	}
}