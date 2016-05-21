using Microsoft.Isam.Esent.Interop;
using EsentSerialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace EsentSerialization
{
	/// <summary>This class implements DB schema update mechanism.</summary>
	/// <remarks>
	/// <para>Functionally, this is a direct equivalent of Microsoft's
	/// <see href="https://msdn.microsoft.com/en-us/library/microsoft.phone.data.linq.databaseschemaupdater(v=vs.95).aspx">DatabaseSchemaUpdater</see> class
	/// that serves similar purpose.</para>
	/// <para>
	/// You need to use this class if you've made changes in your DB schema (i.e. you've added, removed or changed column[s] or index/indices within a table),
	/// and you want the new version of your software to upgrade the database while preserving the data in it.
	/// </para>
	/// <para>You don't need to use it if you've only added an extra table in some version: the new table will be created automatically.</para>
	/// <para>
	/// You need to perform schema upgrade in the first session of the database before any tables are opened.<br />
	/// If you've constructed EseSerializer directly, pass false to <see cref="EseSerializer.OpenDatabase">OpenDatabase</see> method when creating that first session, then perform the upgrade, then open the tables you need.<br />
	/// If you're using SessionPool, set the <see cref="SessionPool.updateSchema">updateSchema</see> delegate to your method that will handle the schema upgrade for you.
	/// </para>
	/// <para>
	/// To simplify upgrade code, it's recommended to perform upgrades sequentially. You can even do that in separate transactions, by calling <see cref="Execute"/> method several times.<br />
	/// Here's a sample code:
	/// <code>            const CURRENT_SCHEMA_VERSION = 3;
	///
	///static void upgradeSchema( DatabaseSchemaUpdater upgrade )
	///{
	///	if( upgrade.isNewDatabase )
	///	{
	///		// This is a newly-created database. We don't need to upgrade anything, just store the current schema version.
	///		upgrade.DatabaseSchemaVersion = CURRENT_SCHEMA_VERSION;
	///		upgrade.Execute();
	///		return;
	///	}
	///
	///	if( CURRENT_SCHEMA_VERSION == upgrade.DatabaseSchemaVersion )
	///		return;     // This is already the latest version
	///
	///	switch( upgrade.DatabaseSchemaVersion )
	///	{
	///	case 1:
	///		// Upgrade 1 -&gt; 2
	///		upgrade.AddColumn&lt;record&gt;( "newColumn" );             // The column was added in ver.2
	///		upgrade.DatabaseSchemaVersion = 2;
	///		upgrade.Execute();
	///		goto case 2:
	///	case 2:
	///		// Upgrade 2 -&gt; 3
	///		upgrade.RemoveColumn&lt;record&gt;( "deprecatedColumn" );   // The column was removed in ver.3
	///		upgrade.CreateIndex&lt;record&gt;( "newIndex" );            // The index was added in ver.3
	///		upgrade.DatabaseSchemaVersion = 3;
	///		upgrade.Execute();
	///		return;
	///	}
	///
	///	throw new ApplicationException( "Unknown database version " + upgrade.DatabaseSchemaVersion.ToString() );
	///}</code>
	/// </para>
	/// </remarks>
	public class DatabaseSchemaUpdater
	{
		/// <summary>The internal table to hold schema version.</summary>
		[EseTable( "EsentSerializerSchema" )]
		internal class DatabaseSchema
		{
			[EseInt]
			public int schemaVersion;
		}

		/// <summary>True if the DB has been just created.</summary>
		public bool isNewDatabase { get { return this.session.serializer.isNewDatabase; } }

		readonly iSerializerSession session;

		Action actUpgrade = () => { };

		/// <summary>Construct the schema updater.</summary>
		/// <param name="sess"></param>
		public DatabaseSchemaUpdater( iSerializerSession sess )
		{
			this.session = sess;
			sess.AddType( typeof( DatabaseSchema ) );

			Cursor<DatabaseSchema> cursor;
			sess.getTable( out cursor );
			if( cursor.TryMoveFirst() )
				this.DatabaseSchemaVersion = cursor.getCurrent().schemaVersion;
			else
				this.DatabaseSchemaVersion = 0;
		}

		/// <summary>Get/set the DB schema version.</summary>
		/// <remarks>The changes will only be written in the DB during the next <see cref="Execute"/> call.</remarks>
		public int DatabaseSchemaVersion { get; set; }

		/// <summary>Submits schema changes to the database in a single transaction.</summary>
		/// <remarks>This call will also flush the list of the pending actions, so you can upgrade sequentially, i.e. upgrade from ver.1 to ver.2, Execute(), upgrade from ver.2 to ver.3, Execute().</remarks>
		public void Execute()
		{
			using( var trans = this.session.BeginTransaction() )
			{
				this.actUpgrade();

				Cursor<DatabaseSchema> cursor;
				this.session.getTable( out cursor );
				if( cursor.TryMoveFirst() )
				{
					DatabaseSchema schema = cursor.getCurrent();
					if( this.DatabaseSchemaVersion < schema.schemaVersion )
						throw new SerializationException( "Schema version number must not decrement" );
					if( this.DatabaseSchemaVersion > schema.schemaVersion )
					{
						schema.schemaVersion = this.DatabaseSchemaVersion;
						cursor.Update( schema );
					}
				}
				else
				{
					if( this.DatabaseSchemaVersion <= 0 )
						throw new SerializationException( "Schema version number must be positive" );
					DatabaseSchema schema = new DatabaseSchema();
					schema.schemaVersion = this.DatabaseSchemaVersion;
					cursor.Add( schema );
				}
				trans.Commit();
			}

			// Since the transaction was committed successfully, we clear the actions so that Updater can be reused several times.
			this.actUpgrade = () => { };
		}

		// Serializers are expensive to construct - does reflection, constructs a lot of internal objects. That's why we cache 'em: it's very likely we'll need to update several columns in the same table.
		private readonly Dictionary<Type, TypeSerializer> serializers = new Dictionary<Type, TypeSerializer>();

		internal TypeSerializer serializerForType( Type t )
		{
			TypeSerializer ser;
			if( serializers.TryGetValue( t, out ser ) )
				return ser;

			EseTableAttribute attrTable = t.getTableAttribute();
			if( null == attrTable )
				throw new SerializationException( "The [EseTable] attribute is not applied to '" + t.Name + "' type more then once." );

			ser = new TypeSerializer( t, attrTable );
			serializers.Add( t, ser );
			return ser;
		}

		/// <summary>Add a new column to the DB.</summary>
		/// <remarks>
		/// <para>The column must exist in the record's type.</para>
		/// <para>If you're creating a column that's no longer in the schema (because you're doing a sequential upgrade step to the non-latest version),
		/// you better leave the column + attribute in the record class, but mark it with <see cref="System.ObsoleteAttribute">[Obsolete]</see> attribute.</para>
		/// </remarks>
		/// <typeparam name="tRow">Record type</typeparam>
		/// <param name="name">Name of the ESENT column to add</param>
		public void AddColumn<tRow>( string name ) where tRow : new()
		{
			TypeSerializer ser = serializerForType( typeof( tRow ) );

			TypeSerializer.ColumnInfo ci = ser.getColumnsSchema().FirstOrDefault( c => c.columnName == name );
			if( null == ci )
				throw new SerializationException( "The column '" + name + "' was not found on row type '" + typeof( tRow ).Name + "'." );

			Action act = () =>
			{
				JET_TABLEID idTable;
				Api.JetOpenTable( this.session.idSession, this.session.idDatabase, ser.tableName, null, 0, OpenTableGrbit.None, out idTable );
				try
				{
					JET_COLUMNID idColumn;
					Api.JetAddColumn( this.session.idSession, idTable, ci.columnName, ci.attrib.getColumnDef(), null, 0, out idColumn );

					if( !ci.attrib.bFieldNullable )
					{
						// The new column is not nullable.
						// It means attempts to de-serialize the column will throw saying "Nullable object must have a value"
						// To fix, we update all records of the table, setting the default value of the column.

						// TODO: instead of creating default value for type, use reflection to find out is there a default value in the record class
						object objVal = Activator.CreateInstance( ci.tpValue );

						Cursor<tRow> cur = new Cursor<tRow>( this.session, ser, idTable, false );
						if( cur.TryMoveFirst() )
						{
							do
							{
								cur.SaveSingleField( ci.columnName, objVal );
								// TODO [low]: pulse the transaction here.. The version store can be exhausted for large tables.
							}
							while( cur.tryMoveNext() );
						}
					}
				}
				finally
				{
					Api.JetCloseTable( this.session.idSession, idTable );
				}
			};
			this.actUpgrade += act;
		}

		/// <summary>Remove a column from the DB.</summary>
		/// <remarks>The column need not to exist in the record's type, the column name is directly passed to the ESENT API.</remarks>
		/// <typeparam name="tRow">Record type</typeparam>
		public void RemoveColumn<tRow>( string name ) where tRow : new()
		{
			TypeSerializer ser = serializerForType( typeof( tRow ) );

			Action act = () =>
			{
				JET_TABLEID idTable;
				Api.JetOpenTable( this.session.idSession, this.session.idDatabase, ser.tableName, null, 0, OpenTableGrbit.None, out idTable );
				try
				{
					Api.JetDeleteColumn( this.session.idSession, idTable, name );
				}
				finally
				{
					Api.JetCloseTable( this.session.idSession, idTable );
				}
			};
			this.actUpgrade += act;
		}

		/// <summary>Create or update an index in the DB.</summary>
		/// <typeparam name="tRow">Record type</typeparam>
		/// <param name="name">Index name</param>
		/// <remarks>
		/// <para>If an old index exists with the same name, old one is deleted.</para>
		/// <para>The index must exist in the record's type.</para>
		/// <para>If you're creating an index that's no longer in the schema (because you're doing a sequential upgrade step to the non-latest version),
		/// you better leave the index attribute in the record class, but specify <see cref="EseIndexAttribute.Obsolete">Obsolete</see> = true in the attribute.</para>
		/// </remarks>
		public void CreateIndex<tRow>( string name ) where tRow : new()
		{
			TypeSerializer ser = serializerForType( typeof( tRow ) );
			JET_INDEXCREATE def = ser.getIndexDef( name );

			Action act = () =>
			{
				JET_TABLEID idTable;
				Api.JetOpenTable( this.session.idSession, this.session.idDatabase, ser.tableName, null, 0, OpenTableGrbit.None, out idTable );
				try
				{
					bool oldExists = Api.GetTableIndexes( this.session.idSession, idTable ).Any( ii => ii.Name == name );
					if( oldExists )
						Api.JetDeleteIndex( this.session.idSession, idTable, name );

					Api.JetCreateIndex2( this.session.idSession, idTable, new JET_INDEXCREATE[ 1 ] { def }, 1 );
				}
				finally
				{
					Api.JetCloseTable( this.session.idSession, idTable );
				}
			};
			this.actUpgrade += act;
		}

		/// <summary>Delete an index from the DB.</summary>
		/// <typeparam name="tRow">Record type</typeparam>
		/// <param name="name">Index name</param>
		/// <remarks>The index need not to exist in the record's type, the name is directly passed to the ESENT API.</remarks>
		public void DeleteIndex<tRow>( string name ) where tRow : new()
		{
			TypeSerializer ser = serializerForType( typeof( tRow ) );

			Action act = () =>
			{
				JET_TABLEID idTable;
				Api.JetOpenTable( this.session.idSession, this.session.idDatabase, ser.tableName, null, 0, OpenTableGrbit.None, out idTable );
				try
				{
					Api.JetDeleteIndex( this.session.idSession, idTable, name );
				}
				finally
				{
					Api.JetCloseTable( this.session.idSession, idTable );
				}
			};
			this.actUpgrade += act;
		}

		/// <summary>Drop the complete table from the DB.</summary>
		/// <param name="name">Table name.</param>
		/// <remarks>The table name need not to be a valid [EseTable] class, the name is directly passed to the ESENT API.</remarks>
		public void DropTable( string name )
		{
			Action act = () =>
			{
				Api.JetDeleteTable( this.session.idSession, this.session.idDatabase, name );
			};
			this.actUpgrade += act;
		}
	}
}