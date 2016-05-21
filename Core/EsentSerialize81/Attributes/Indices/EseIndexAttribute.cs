using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;

namespace EsentSerialization.Attributes
{
	/// <summary>This attribute defines an index over a table.</summary>
	/// <remarks>This attribute has no effect unless the <see cref="EseTableAttribute" /> is also applied to this class.</remarks>
	[AttributeUsage( AttributeTargets.Class, Inherited = false, AllowMultiple = true )]
	public class EseIndexAttribute : Attribute
	{
		// Define useful CreateIndexGrbit constants.
		// Client code is not expected to be using Microsoft.Isam.Esent.Interop namespace.

		/// <summary>Duplicate index entries (keys) are disallowed</summary>
		public const CreateIndexGrbit flagsUnique = CreateIndexGrbit.IndexUnique;

		/// <summary>Do not add an index entry for a row if any of the columns being indexed are NULL.</summary>
		public const CreateIndexGrbit flagsNoNulls = CreateIndexGrbit.IndexIgnoreAnyNull;

		/// <summary>The combination of flagsUnique and flagsNoNulls.</summary>
		public const CreateIndexGrbit flagsUniqueNoNulls = CreateIndexGrbit.IndexIgnoreAnyNull | CreateIndexGrbit.IndexUnique;

		/// <summary>The name of the index.</summary>
		public readonly string strName;
		/// <summary>Double null-terminated string of null-delimited tokens, defining the indexed columns.</summary>
		public readonly string strKey;
		readonly CreateIndexGrbit flags;

		/// <summary>Is true if this is the primary (clustering) index.</summary>
		public bool isPrimaryIndex { get { return CreateIndexGrbit.IndexPrimary == ( this.flags | CreateIndexGrbit.IndexPrimary ); } }

		/// <summary>Declare the index.</summary>
		/// <param name="_strName">The name of the index.</param>
		/// <param name="_strKey">Double null-terminated string of null-delimited tokens.</param>
		/// <remarks>Each token within the _strKey is of the form "&lt;direction-specifier&gt;&lt;column-name&gt;",
		/// where direction-specification is either "+" or "-".
		/// For example, a _strKey of "+abc\0-def\0+ghi\0\0" will index over the three columns
		/// "abc" (in ascending order), "def" (in descending order), and "ghi" (in ascending order).</remarks>
		/// <seealso cref="JET_INDEXCREATE" />
		public EseIndexAttribute( string _strName, string _strKey )
		{
			strName = _strName;
			strKey = _strKey;
			flags = CreateIndexGrbit.None;
		}

		/// <summary>Declare the index, specifying the flags as well.</summary>
		/// <param name="_strName">The name of the index.</param>
		/// <param name="_strKey">Double null-terminated string of null-delimited tokens.</param>
		/// <param name="_flags">Options for JetCreateIndex.</param>
		/// <remarks>Each token within the _strKey is of the form "&lt;direction-specifier&gt;&lt;column-name&gt;",
		/// where direction-specification is either "+" or "-".
		/// For example, a _strKey of "+abc\0-def\0+ghi\0\0" will index over the three columns
		/// "abc" (in ascending order), "def" (in descending order), and "ghi" (in ascending order).</remarks>
		/// <seealso cref="JET_INDEXCREATE" />
		public EseIndexAttribute( string _strName, string _strKey, CreateIndexGrbit _flags )
		{
			strName = _strName;
			strKey = _strKey;
			flags = _flags;
		}

		/// <summary>Construct the basic JET_INDEXCREATE structure.
		/// Override this method to set additional parameters of the structure, such as conditional columns, or density.</summary>
		public virtual JET_INDEXCREATE getIndexDef()
		{
			JET_INDEXCREATE ic = new JET_INDEXCREATE();
			ic.szIndexName = strName;
			ic.szKey = strKey;
			ic.grbit = flags;
			ic.cbKey = strKey.Length;
			ic.ulDensity = 77;
			setConditionalColumns( ic );

			return ic;
		}

		/// <summary>Verify the index is compatible with the columns.</summary>
		/// <remarks>This method is called once for every index.<br/>
		/// This is the right place to check the compatibility of the index and indexed columns.<br/>
		/// The implementation should throw exceptions if they are not compatible.<br/>
		/// NB! This method is not implemented and does nothing.</remarks>
		/// <param name="arrIndexedColumns">Array of the columns covered by this index</param>
		public virtual void verifyColumnsSupport( EseColumnAttrubuteBase[] arrIndexedColumns ) { }

		/// <summary>The string specifying the conditional columns for this attribute.</summary>
		/// <remarks>The value must be e.g. "+col1" or "-col1\0+col2\0+col3",<br />
		/// where "col1", "col2" and "col3" the columns defined in this table,<br />
		/// "+" stands for "the column must be non-NULL for an index entry for a given row to appear in this index",<br />
		/// "-" stands for "the column must be NULL for an index entry in order for a given row to appear in this index".</remarks>
		public string condition = null;

		/// <summary>Add conditional columns data to the supplied <see cref="JET_INDEXCREATE" />.</summary>
		/// <param name="ic"></param>
		protected void setConditionalColumns( JET_INDEXCREATE ic )
		{
			if( String.IsNullOrEmpty( condition ) )
			{
				ic.cConditionalColumn = 0;
				ic.rgconditionalcolumn = null;
				return;
			}

			var res = new List<JET_CONDITIONALCOLUMN>();

			var arrTokens = condition.Split( new char[ 1 ] { '\0' }, StringSplitOptions.None );
			foreach( string strToken in arrTokens )
			{
				JET_CONDITIONALCOLUMN cc = new JET_CONDITIONALCOLUMN();
				if( '+' == strToken[ 0 ] )
					cc.grbit = ConditionalColumnGrbit.ColumnMustBeNonNull;
				else if( '-' == strToken[ 0 ] )
					cc.grbit = ConditionalColumnGrbit.ColumnMustBeNull;
				else
					throw new System.Runtime.Serialization.SerializationException( "existence specifier not found in the conditional column token " + strToken );

				cc.szColumnName = strToken.Substring( 1 );

				res.Add( cc );
			}

			ic.cConditionalColumn = res.Count;
			ic.rgconditionalcolumn = res.ToArray();
		}

		/// <summary>Set this to true to declare index as obsolete.</summary>
		/// <remarks>Obsolete indices aren't created in the DB, they can't normally be used to search/filter records,
		/// but they can be created by <see cref="EsentSerialization.DatabaseSchemaUpdater"/> class.</remarks>
		public bool Obsolete = false;
	}
}