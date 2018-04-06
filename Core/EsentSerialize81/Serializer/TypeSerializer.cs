using EsentSerialization.Attributes;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace EsentSerialization
{
	/// <summary>This class performs table management task for a single serialized type.</summary>
	/// <remarks>Among other things, this class implements the reflection magic of the record types.</remarks>
	public partial class TypeSerializer : iTypeSerializer
	{
		// A regular expressions to check table, column and index columnNames.
		// TODO [low]: allow a few more special characters that are stated as 'allowed' in the spec.
		// See http://msdn.microsoft.com/en-us/library/ms683248(EXCHG.10).aspx article, 'szTableName' argument description for the naming requirements.
		static readonly Regex s_reName = new Regex( "^[0-9A-Za-z_][0-9A-Za-z_ ]{0,63}$", RegexOptions.CultureInvariant );
		const string s_strNameError = "It must be from 1 to 64 alphanumeric English characters.";

		string m_tableName;

		/// <summary>Name of the table</summary>
		public string tableName { get { return m_tableName; } }

		/// <summary>Type of the records</summary>
		public readonly Type recordType;

		/// <summary>The instance of the EseTableAttribute applied to the record class.</summary>
		public readonly Attributes.EseTableAttribute tableAttribute;
		Attributes.EseTableAttribute iTypeSerializer.tableAttribute { get { return this.tableAttribute; } }

		List<ColumnInfo> m_columns = new List<ColumnInfo>();
		Dictionary<string, ColumnInfo> m_dictColumns;

		Dictionary<string, sIndexInfo> m_indices = new Dictionary<string, sIndexInfo>();

		/// <summary></summary>
		public TypeSerializer( Type t, Attributes.EseTableAttribute tInfo )
		{
			recordType = t;
			tableAttribute = tInfo;
			Global.TryCatch( delegate () { newTypeImpl(); }, "Error reflecting type '" + t.Name + "'." );
		}

		#region Construct the type info by reflecting
		void newTypeImpl()
		{
			// Derive and validate the tableName
			if( tableAttribute.tableName != null )
				m_tableName = tableAttribute.tableName;
			else
				m_tableName = recordType.Name;

			if( m_tableName == "EsentSerializerSchema" )
				if( recordType != typeof( DatabaseSchemaUpdater.DatabaseSchema ) )
					throw new SerializationException( "\"EsentSerializerSchema\" table name is reserved for internal use, please pick another name for your table." );

			if( !s_reName.IsMatch( m_tableName ) )
				throw new SerializationException( "Invalid table name. " + s_strNameError );

			// Deal with the fields / properties
			foreach( var m in recordType.enumMembers() )
			{
				bool obsolete = ( null != m.GetCustomAttribute<ObsoleteAttribute>() );

				EseColumnAttrubuteBase[] attribs = m.getColumnAttributes();
				if( null == attribs || attribs.Length <= 0 )
					continue;

				Type tp = null;
				if( m is FieldInfo )
					tp = ( m as FieldInfo ).FieldType;
				else if( m is PropertyInfo )
					tp = ( m as PropertyInfo ).PropertyType;
				else
					continue;

				addColumn( m, tp, attribs, obsolete );
			}

			// Ensure no name collision is here
			HashSet<string> columnNames = new HashSet<string>();
			foreach( var i in m_columns )
			{
				if( columnNames.Contains( i.columnName ) )
					throw new SerializationException( "Several columns in the table '" + m_tableName + "' are named '" + i.columnName + "'" );
				columnNames.Add( i.columnName );
			}
			columnNames = null;

			// Deal with the indices
			addIndices();
		}

		void addColumn( MemberInfo f, Type tMember, EseColumnAttrubuteBase[] attributes, bool isObsolete )
		{
			Global.TryCatch( delegate ()
			{
				if( null == attributes || attributes.Length <= 0 )
					return;
				if( attributes.Length > 1 )
					throw new SerializationException( "More then one ESE column attribute is applied to the field '" + f.Name + "'" );
				EseColumnAttrubuteBase columnAttrBase = attributes[ 0 ];

				// Verify the type
				Global.TryCatch( delegate () { columnAttrBase.verifyTypeSupport( tMember ); },
					"ESE column attribute '" + columnAttrBase.GetType().Name +
					"' is incompatible with field or property of type '" + tMember.Name + "'." );

				addColumnDefinition( f, columnAttrBase, isObsolete );
			},
			"Error reflecting field '" + f.Name + "'." );
		}

		void addColumnDefinition( MemberInfo f, EseColumnAttrubuteBase attr, bool isObsolete )
		{
			string strColumnName = attr.columnName;
			if( null == strColumnName )
				strColumnName = f.Name;
			if( !s_reName.IsMatch( strColumnName ) )
				throw new SerializationException( "Invalid column name. " + s_strNameError );

			ColumnInfo ci = new ColumnInfo( strColumnName, f, attr, isObsolete );
			m_columns.Add( ci );
		}

		/// <summary>Parse index into columns that it's composed of.</summary>
		/// <param name="strKey">double-null-terminated string of null-delimited tokens</param>
		static IEnumerable<string> getIndexedColumns( string strKey )
		{
			// Check the key is double null-terminated.
			if( !strKey.EndsWith( "\0\0" ) )
				throw new SerializationException( "the key must be double-null-terminated." );

			// Verify each token within the key.
			strKey = strKey.Substring( 0, strKey.Length - 2 );
			string[] arrTokens = strKey.Split( new char[ 1 ] { '\0' }, StringSplitOptions.None );

			EseColumnAttrubuteBase[] indexedColumns = new EseColumnAttrubuteBase[ arrTokens.Length ];

			for( int i = 0; i < arrTokens.Length; i++ )
			{
				string strToken = arrTokens[ i ];
				if( strToken[ 0 ] != '+' && strToken[ 0 ] != '-' )
					throw new SerializationException( "direction specifier not found in the token '" + strToken + "'." );

				string idColumn = strToken.Substring( 1 );
				yield return idColumn;
			}
		}

		void addIndices()
		{
			EseIndexAttribute[] arrAllIndexAttributes = recordType
				.getCustomAttributes<EseIndexAttribute>()
				.ToArray();

			foreach( EseIndexAttribute attr in arrAllIndexAttributes )
			{
				// Check the name
				if( !s_reName.IsMatch( attr.strName ) )
					throw new SerializationException( "Error adding index '" + attr.strName + "': invalid name. " + s_strNameError );

				string[] columnNames;
				try
				{
					columnNames = getIndexedColumns( attr.strKey ).ToArray();
				}
				catch( SerializationException ex )
				{
					throw new SerializationException( "Error adding index '" + attr.strName + "': " + ex.Message );
				}

				EseColumnAttrubuteBase[] indexedColumns = new EseColumnAttrubuteBase[ columnNames.Length ];
				bool indexIsObsolete = false;
				for( int i = 0; i < columnNames.Length; i++ )
				{
					string idColumn = columnNames[ i ];
					ColumnInfo ci = m_columns.FirstOrDefault( c => c.columnName == idColumn );
					if( null == ci )
						throw new SerializationException( "Error adding index '" + attr.strName + "': column '" + idColumn + "' not found." );
					indexedColumns[ i ] = ci.attrib;
					indexIsObsolete = indexIsObsolete | ci.isObsolete;
				}

				// Verify index name uniqueness.
				sIndexInfo tmp;
				if( m_indices.TryGetValue( attr.strName, out tmp ) )
					throw new SerializationException( "Error adding index '" + attr.strName + "': index with this name already exists." );

				// Verify the columns are indexable.
				attr.verifyColumnsSupport( indexedColumns );

				// Verify the conditional columns data.

				string consCols = attr.condition;
				if( !String.IsNullOrEmpty( consCols ) )
				{
					string[] arrTokens = consCols.Split( new char[ 1 ] { '\0' }, StringSplitOptions.None );

					for( int i = 0; i < arrTokens.Length; i++ )
					{
						string strToken = arrTokens[ i ];
						if( strToken[ 0 ] != '+' && strToken[ 0 ] != '-' )
							throw new SerializationException( "Error adding index '" +
								attr.strName + "': existence specifier not found in the conditional column token '" + strToken + "'." );

						string idColumn = strToken.Substring( 1 );
						int iColumn = m_columns.FindIndex( c => c.columnName == idColumn );
						if( iColumn < 0 )
							throw new SerializationException( "Error adding index '" + attr.strName + "': the condition column '" + idColumn + "' not found." );
					}
				}

				m_indices.Add( attr.strName, new sIndexInfo( attr, columnNames, indexedColumns, indexIsObsolete ) );
			}
		}
		#endregion

		#region Initialization

		/// <summary>Create the table, it's columns and indices.</summary>
		public void CreateTableAndIndices( JET_SESID idSession, JET_DBID idDatabase )
		{
			JET_TABLEID idTable;
			Api.JetCreateTable( idSession, idDatabase, m_tableName, 0, 0, out idTable );

			using( var transaction = new Transaction( idSession ) )
			{
				// Add the columns
				foreach( ColumnInfo ci in m_columns )
				{
					if( ci.isObsolete )
						continue;   // DatabaseSchemaUpdater can still create obsolete columns because it doesn't use CreateTableAndIndices method.
					JET_COLUMNID idColumn;
					Api.JetAddColumn( idSession, idTable, ci.columnName, ci.attrib.getColumnDef(), null, 0, out idColumn );
				}

				// Add the indices
				foreach( var ind in m_indices.Values )
				{
					if( ind.attrib.Obsolete )
						continue;   // DatabaseSchemaUpdater can still create obsolete indices because it doesn't use CreateTableAndIndices method.
					if( ind.hasObsoleteColumns )
						throw new SerializationException( "Error creating index '" + ind.attrib.strName + "': some of the columns being indexed are obsolete." );
					JET_INDEXCREATE i = ind.attrib.getIndexDef();
					Api.JetCreateIndex2( idSession, idTable, new JET_INDEXCREATE[ 1 ] { i }, 1 );
					// See http://managedesent.codeplex.com/WorkItem/View.aspx?WorkItemId=5605 for more info.
				}
				transaction.Commit( CommitTransactionGrbit.LazyFlush );
			}
			Api.JetCloseTable( idSession, idTable );
		}

		internal static bool areEqual( CreateIndexGrbit grDatabase, CreateIndexGrbit grDeclaration )
		{
			// For some reason, indices sometimes have IndexUnique bit even when it was not requested while creation.
			// So we ignore that bit when validating.
			grDatabase &= ~CreateIndexGrbit.IndexUnique;
			grDeclaration &= ~CreateIndexGrbit.IndexUnique;

			// The DB engine normalizes JET_bitIndexIgnore*Null bits
			if( grDeclaration.HasFlag( CreateIndexGrbit.IndexIgnoreAnyNull ) )
			{
				grDeclaration |= CreateIndexGrbit.IndexIgnoreFirstNull;
				grDeclaration |= CreateIndexGrbit.IndexIgnoreNull;
			}

			return grDatabase == grDeclaration;
		}

		/// <summary>This method is called before a table is actually opened.
		/// This is a good place to upgrade database schema, reconstruct indices, or engage in some other table schema management activity.
		/// None of that is implemented in the current version of the codebase, though.
		/// The current version just throws an exception if column schema's wrong, and completely ignores the indices schema.</summary>
		public void VerifyTableSchema( JET_SESID idSession, JET_DBID idDatabase )
		{
			// Verify the table exists
			{
				JET_TABLEID idTestTable;
				if( Api.TryOpenTable( idSession, idDatabase, m_tableName, OpenTableGrbit.None, out idTestTable ) )
					Api.JetCloseTable( idSession, idTestTable );
				else
				{
					// Just create the table
					using( Transaction transaction = new Transaction( idSession ) )
					{
						CreateTableAndIndices( idSession, idDatabase );
						transaction.Commit( CommitTransactionGrbit.None );
					}

					return;
				}
			}

			// Verify columns
			foreach( var i in m_columns )
			{
				Global.TryCatch( delegate ()
				{
					JET_COLUMNDEF defDB;
					Api.JetGetColumnInfo( idSession, idDatabase, m_tableName, i.columnName, out defDB );

					JET_COLUMNDEF defCode = i.attrib.getColumnDef();

					if( defCode.coltyp != defDB.coltyp ) throw new SerializationException( "Wrong type." );
					if( defCode.cp != defDB.cp ) throw new SerializationException( "Wrong codepage." );
					if( defCode.grbit != defDB.grbit ) throw new SerializationException( "Flags mismatch." );
				},
				"Error while validating column '" + i.columnName + "'" );
			}

			// Verify indices
			// http://stackoverflow.com/a/2345746/126995
			Dictionary<string, IndexInfo> dbIndices = Api.GetTableIndexes( idSession, idDatabase, m_tableName )
				.ToDictionary( ii => ii.Name );

			foreach( var kvp in m_indices )
			{
				sIndexInfo ind = kvp.Value;

				Global.TryCatch( () =>
				{
					IndexInfo ii;
					if( !dbIndices.TryGetValue( kvp.Key, out ii ) )
						throw new SerializationException( "The index was not found in the DB" );

					JET_INDEXCREATE icreate = ind.attrib.getIndexDef();
					if( !areEqual( ii.Grbit, icreate.grbit ) )
						throw new SerializationException( "The index grbit doesn't match what's in the DB" );

					string[] colsDatabase = ii.IndexSegments
						.Select( seg => seg.ColumnName )
						.ToArray();

					string[] colsTypeInfo = getIndexedColumns( ind.attrib.strKey ).ToArray();

					if( !colsDatabase.SequenceEqual( colsTypeInfo ) )
						throw new SerializationException( "The index is over the different column than what's in the DB" );
				},
				"Error while validating index '" + kvp.Key + "'" );
			}
		}
		#endregion

		bool m_bColumnIDsCached = false;

		// See http://blogs.msdn.com/laurionb/archive/2009/03/17/jet-columnid-scope.aspx for the reason why it's OK to cache this information once for all sessions.
		/// <summary></summary>
		public void LookupColumnIDs( JET_SESID idSession, JET_TABLEID idTable )
		{
			if( m_bColumnIDsCached ) return;
			lock( this )
			{
				if( m_bColumnIDsCached )
					return;
				for( int i = 0; i < m_columns.Count; i++ )
				{
					JET_COLUMNDEF cd;
					ColumnInfo ci = m_columns[ i ];
					if( ci.isObsolete )
						continue;
					Api.JetGetTableColumnInfo( idSession, idTable, ci.columnName, out cd );
					ci.setColumnID( cd.columnid );
					m_columns[ i ] = ci;
				}
				// Construct the dictionary
				m_dictColumns = m_columns.ToDictionary( c => c.columnName );

				m_bColumnIDsCached = true;
			}
		}

		#region Serialization and de-serialization

		// Perform some special cases processing just after a column value has been serialized.
		void onColumnSerialized( EseCursorBase cur, object rec, bool bNewRecord, ColumnInfo col )
		{
			// If this is auto-incremented column, and we're adding the new record,
			// then we set the column value ASAP.
			if( bNewRecord && ( col.attrib is Attributes.EseAutoIdAttribute ) )
				col.RefreshAutoincValue( cur, rec );

			// If this is [EseBinaryStream],
			// immediately fetch the bookmark and store it in the EseStreamValue instance, to allow access to stream data ASAP.
			// if( col.attrib is Attributes.EseBinaryStreamAttribute )
			//	col.DeSerialize( cur, rec );
		}

		void iTypeSerializer.Serialize( EseCursorBase cur, object rec, bool bNewRecord )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );

			foreach( var col in m_columns )
			{
				if( col.isObsolete )
					continue;
				col.Serialize( cur, rec, bNewRecord );
				onColumnSerialized( cur, rec, bNewRecord, col );
			}
		}

		void iTypeSerializer.SerializeField( EseCursorBase cur, object rec, string fName )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );

			ColumnInfo col;
			if( !m_dictColumns.TryGetValue( fName, out col ) )
				throw new ArgumentException( "The column with the specified name was not found", "fName" );

			col.Serialize( cur, rec, false );
			onColumnSerialized( cur, rec, false, col );
		}

		void iTypeSerializer.Deserialize( EseCursorBase cur, object rec )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );
			foreach( var col in m_columns )
			{
				if( col.isObsolete )
					continue;
				col.DeSerialize( cur, rec );
			}
		}

		void iTypeSerializer.DeserializeField( EseCursorBase cur, object rec, string fName )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );

			ColumnInfo col;
			if( !m_dictColumns.TryGetValue( fName, out col ) )
				throw new ArgumentException( "The column with the specified name was not found", "fName" );

			col.DeSerialize( cur, rec );
		}

		#endregion

		#region Save/load columns without [de]serializing

		object iTypeSerializer.FetchSingleField( EseCursorBase cur, string fName )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );

			ColumnInfo col;
			if( !m_dictColumns.TryGetValue( fName, out col ) )
				throw new ArgumentException( "The column with the specified name was not found", "fName" );

			return col.DeSerialize( cur );
		}

		void iTypeSerializer.SaveSingleField( EseCursorBase cur, string fName, object value )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );

			ColumnInfo col;
			if( !m_dictColumns.TryGetValue( fName, out col ) )
				throw new ArgumentException( "The column with the specified name was not found", "fName" );

			col.SerializeSingleField( cur, value );
		}

		JET_COLUMNID iTypeSerializer.GetColumnId( EseCursorBase cur, string fName )
		{
			LookupColumnIDs( cur.idSession, cur.idTable );
			ColumnInfo col;
			if( !m_dictColumns.TryGetValue( fName, out col ) )
				throw new ArgumentException( "The column with the specified name was not found", "fName" );
			return col.idColumn;
		}

		#endregion

		#region Advanced operations

		EseColumnAttrubuteBase[] iTypeSerializer.getIndexedColumns( string strIndexName )
		{
			sIndexInfo ind;
			if( m_indices.TryGetValue( strIndexName, out ind ) )
			{
				if( !ind.attrib.Obsolete )
					return ind.columns;
				throw new ArgumentException( "The index '" + strIndexName + "' is obsolete" );
			}
			throw new ArgumentException( "The index '" + strIndexName + "' was not found" );
		}

		/// <summary>Get index definition.</summary>
		/// <remarks>This internal method is used by DatabaseSchemaUpdater class.</remarks>
		/// <param name="strIndexName">Name of the index</param>
		/// <returns></returns>
		internal JET_INDEXCREATE getIndexDef( string strIndexName )
		{
			sIndexInfo ind;
			if( m_indices.TryGetValue( strIndexName, out ind ) )
				return ind.attrib.getIndexDef();
			throw new ArgumentException( "The index with the specified name was not found" );
		}

		/// <summary>Get the columns schema of the table.
		/// Normally, it's only called while importing or exporting the data.</summary>
		public IEnumerable<ColumnInfo> getColumnsSchema()
		{
			return m_columns;
		}

		#endregion

		/// <summary>Drop and recreate the complete table.</summary>
		public void RecreateTable( iSerializerSession sess )
		{
			using( Transaction transaction = new Transaction( sess.idSession ) )
			{
				Api.JetDeleteTable( sess.idSession, sess.idDatabase, this.m_tableName );
				CreateTableAndIndices( sess.idSession, sess.idDatabase );
				transaction.Commit( CommitTransactionGrbit.None );
			}
		}

		readonly object syncRoot = new object();
		Dictionary<MemberInfo, IndexForColumn[]> dictSortIndices = null;

		/// <summary>Query schema for the indices that index the DB column identified by the record class member.</summary>
		public IndexForColumn[] indicesFromColumn( MemberInfo mi )
		{
			lock( syncRoot )
			{
				IndexForColumn[] res;
				if( null != dictSortIndices && dictSortIndices.TryGetValue( mi, out res ) )
					return res;

				res = indicesFromColumnImpl( mi ).ToArray();
				if( res.Length <= 0 )
					throw new ArgumentException( "Member {0} is not included in any index".formatWith( mi.Name ) );

				if( null == dictSortIndices )
					dictSortIndices = new Dictionary<MemberInfo, IndexForColumn[]>();
				dictSortIndices[ mi ] = res;
				return res;
			}
		}

		IEnumerable<IndexForColumn> indicesFromColumnImpl( MemberInfo mi )
		{
			EseColumnAttrubuteBase col = mi.getColumnAttributes().FirstOrDefault();
			if( null == col )
				throw new ArgumentException( "Member {0} is not mapped to a column".formatWith( mi.Name ) );

			string colName = col.columnName;
			if( String.IsNullOrEmpty( colName ) )
				colName = mi.Name;

			foreach( var kvp in m_indices )
			{
				sIndexInfo ii = kvp.Value;
				int ind = Array.IndexOf( ii.columnNames, colName );
				if( ind < 0 )
					continue;
				bool primary = ( ii.attrib is EsePrimaryIndexAttribute );
				bool dir = ii.columnDirections[ ind ];
				yield return new IndexForColumn( primary, kvp.Key, ii.attrib, ind, dir );
			}
		}
	}
}