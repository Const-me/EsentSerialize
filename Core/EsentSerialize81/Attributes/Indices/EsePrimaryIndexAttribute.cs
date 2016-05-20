using Microsoft.Isam.Esent.Interop;
using System;

namespace EsentSerialization.Attributes
{
	/// <summary>This attribute defines the primary (clustered) index.</summary>
	/// <remarks>This attribute has no effect unless the <see cref="EseTableAttribute" /> is also applied to this class.</remarks>
	[AttributeUsage( AttributeTargets.Class, Inherited = false, AllowMultiple = false )]
	public sealed class EsePrimaryIndexAttribute : EseIndexAttribute
	{
		/// <summary>Declare the index.</summary>
		/// <param name="_strName">The name of the index.</param>
		/// <param name="_strKey">Double null-terminated string of null-delimited tokens.</param>
		/// <remarks>Each token within the _strKey is of the form "&lt;direction-specifier&gt;&lt;column-name&gt;",
		/// where direction-specification is either "+" or "-".
		/// For example, a _strKey of "+abc\0-def\0+ghi\0\0" will index over the three columns
		/// "abc" (in ascending order), "def" (in descending order), and "ghi" (in ascending order).</remarks>
		/// <seealso cref="JET_INDEXCREATE" />
		public EsePrimaryIndexAttribute( string _strName, string _strKey ) :
			base( _strName, _strKey, CreateIndexGrbit.IndexPrimary )
		{ }
	}
}