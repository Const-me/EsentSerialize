using EsentSerialization.Attributes;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace EsentSerialization
{
	/// <summary>Represents a ESENT database.</summary>
	/// <remarks><para>This class is the heart of the whole EsentSerialization library.</para>
	/// <para>It owns the database's JET_INSTANCE, and it owns a collection of the <see cref="TypeSerializer" /> objects for every record type.</para>
	/// <para>You should not attempt to create more then one EseSerializer instance in the same process:
	/// the DB instance name is hard-coded.</para>
	/// <para>In the same process, you may use either EseSerializer or <see cref="SessionPool"/> but not both.</para>
	/// <para>Here's the C# code that creates and uses EseSerializer object:</para>
	///<code lang="C#">            // The application entry point
	///static void Main()
	///{
	///	string strDatabasePath = Environment.ExpandEnvironmentVariables( @"%APPDATA%\MyCompany\MySoftware" );
	///	using( EseSerializer serializer = new EseSerializer( strDatabasePath ) )
	///	using( iSerializerSession sess = serializer.OpenDatabase( true ) )
	///	{
	///		if( serializer.isNewDatabase )
	///		{
	///			// If you need some records to be presented in a newly created DB, add them here.
	///		}
	///
	///		// Run your software; use 'sess' variable to access the DB.
	///	}
	///}</code></remarks>
	public class EseSerializer : IDisposable
	{
		readonly string m_instanceName;
		JET_INSTANCE m_idInstance;

		/// <summary></summary>
		public JET_INSTANCE idInstance { get { return m_idInstance; } }

		/// <summary>The folder where the DB is located.</summary>
		public readonly string folderDatabase;

		string m_pathDatabase;
		/// <summary>The full path of the main DB file, which is located inside the folderDatabase.</summary>
		public string pathDatabase { get { return m_pathDatabase; } }

		readonly object syncRoot = new object();

		/// <summary>Close the DB.</summary>
		public void Dispose()
		{
			if( JET_INSTANCE.Nil != idInstance )
			{
				Api.JetTerm( idInstance );
				m_idInstance = JET_INSTANCE.Nil;
			}
		}

		/// <summary>Internal constructor that initializes the parameters but doesn't call JetInit.</summary>
		internal EseSerializer( EsentDatabase.Settings settings )
		{
			string strFolder = settings.databasePath;
			if( !Directory.Exists( strFolder ) )
				Directory.CreateDirectory( strFolder );

			folderDatabase = strFolder;

			m_instanceName = settings.advanced.InstanceName;

			SystemParameters.DatabasePageSize = settings.advanced.DatabasePageSize;

			JET_INSTANCE i;
			Api.JetCreateInstance( out i, m_instanceName );
			SetupInstanceParams( settings.advanced, i, strFolder );
			m_idInstance = i;
		}

		/// <summary>Construct the serializer.</summary>
		/// <param name="settings">Database settings.</param>
		/// <param name="typesToAdd">Record types to add.</param>
		public EseSerializer( EsentDatabase.Settings settings, IEnumerable<Type> typesToAdd ):
			this( settings )
		{
			if( null != typesToAdd )
			{
				foreach( Type t in typesToAdd )
				{
					EseTableAttribute a = t.getTableAttribute();
					if( null == a ) continue;
					m_tables.Add( new TypeSerializer( t, a ) );
				}
			}
			Api.JetInit( ref m_idInstance );
		}

		void SetupInstanceParams( EsentDatabase.AdvancedSettings settings, JET_INSTANCE i, string strFolder )
		{
			InstanceParameters Parameters = new InstanceParameters( i );

			m_pathDatabase = Path.Combine( strFolder, settings.FileName );

			// Mostly copy-pasted from Microsoft.Isam.Esent.Collections.Generic.PersistentDictionary<>.__ctor()
			Parameters.SystemDirectory = strFolder;
			Parameters.LogFileDirectory = strFolder;
			Parameters.TempDirectory = strFolder;
			Parameters.AlternateDatabaseRecoveryDirectory = strFolder;
			Parameters.CreatePathIfNotExist = true;
			Parameters.BaseName = settings.BaseName;
			Parameters.EnableIndexChecking = false;
			Parameters.CircularLog = true;
			Parameters.CheckpointDepthMax = 0x4010000;
			Parameters.PageTempDBMin = 0;
			Parameters.MaxVerPages = settings.MaxVerPages;

			Parameters.LogFileSize = settings.kbLogFileSize;
			Parameters.LogBuffers = settings.LogBuffers.Value;

			// Ext. parameters
			Api.JetSetSystemParameter( i, JET_SESID.Nil, Ext.JET_paramIndexTuplesLengthMin, settings.IndexTuplesLengthMin, null );
		}

		/// <summary>Add the record type to serializer.</summary>
		/// <param name="t">The record type to add.</param>
		/// <returns>False if the type was already added.</returns>
		public bool AddSerializedType( Type t )
		{
			EseTableAttribute a = t.getTableAttribute();
			if( null == a ) throw new SerializationException( "The type must have [EseTable] attribute applied." );

			lock( syncRoot )
			{
				if( m_tables.FindIndex( ts => ts.recordType.Equals( t ) ) >= 0 )
					return false;   // the type is already added.
				m_tables.Add( new TypeSerializer( t, a ) );
				return true;
			}
		}

		bool m_bNewDatabase = true;
		/// <summary>True if the new DB has been created by the last OpenDatabase call.</summary>
		public bool isNewDatabase { get { return m_bNewDatabase; } }

		/// <summary>It's crucial to have EnsureDatabaseExists method executed exactly once, otherwise the isNewDatabase value is lost, which is bad esp. if doing schema upgrades.</summary>
		private bool bEnsureDatabaseExistsCalled = false;

		/// <summary>Create the database if it's not exists on the HDD.</summary>
		/// <remarks>Normally, this is called from <see cref="OpenDatabase" />.<br/>
		/// You should only use this method if you're creating database session manually.</remarks>
		public void EnsureDatabaseExists()
		{
			if( bEnsureDatabaseExistsCalled )
				return;

			lock( syncRoot )
			{
				if( bEnsureDatabaseExistsCalled )
					return;
				bEnsureDatabaseExistsCalled = true;

				if( !File.Exists( m_pathDatabase ) )
				{
					CreateDatabase();
					m_bNewDatabase = true;
				}
				else
					m_bNewDatabase = false;
			}
		}

		/// <summary>Open or create the DB,and return the <see cref="iSerializerSession">session</see> interface of the newly created DB session.</summary>
		/// <param name="bOpenAllTables">True to open tables for every record type currently added to the serializer.</param>
		/// <returns>iSerializerSession to access the DB.</returns>
		/// <remarks><para>You should call this method on every thread (including the main thread)
		/// that's going to use the database.</para>
		/// <para>Below is the recommended C# code:</para>
		/// <code lang="C#">            // Your thread procedure
		///void ThreadProc()
		///{
		///	using( iSerializerSession sess = m_serializer.OpenDatabase( false ) )
		///	{
		///		// Open the table in this session ( note we've passed false to the OpenDatabase call )
		///		sess.AddType( typeof( MyRecordClass ) );
		///		
		///		// Run your thread; use 'sess' variable to access the DB.
		///	}
		///}</code></remarks>
		public iSerializerSession OpenDatabase( bool bOpenAllTables )
		{
			EnsureDatabaseExists();

			SerializerSession res = new SerializerSession( this );
			if( bOpenAllTables )
			{
				foreach( var i in m_tables )
				{
					Global.TryCatch( delegate () { res.addType( i, false ); }, "Unable to open ESE table '" + i.tableName + "'." );
				}
			}
			return res;
		}

		// Mostly copy-pasted from Microsoft.Isam.Esent.Collections.Generic.PersistentDictionary<>.CreateDatabase()
		void CreateDatabase()
		{
			using( Session session = new Session( idInstance ) )
			{
				JET_DBID idDatabase;
				Api.JetCreateDatabase( (JET_SESID)session, m_pathDatabase, string.Empty, out idDatabase, CreateDatabaseGrbit.None );
				try
				{
					using( Transaction transaction = new Transaction( (JET_SESID)session ) )
					{
						foreach( var ts in m_tables )
							ts.CreateTableAndIndices( session, idDatabase );

						transaction.Commit( CommitTransactionGrbit.None );
						Api.JetCloseDatabase( (JET_SESID)session, idDatabase, CloseDatabaseGrbit.None );
						Api.JetDetachDatabase( (JET_SESID)session, m_pathDatabase );
					}
				}
				catch( Exception ex )
				{
					Api.JetCloseDatabase( (JET_SESID)session, idDatabase, CloseDatabaseGrbit.None );
					Api.JetDetachDatabase( (JET_SESID)session, m_pathDatabase );
					File.Delete( m_pathDatabase );
					throw new Exception( "Unable to create the database.", ex );
				}
			}
		}

		List<TypeSerializer> m_tables = new List<TypeSerializer>();

		/// <summary>Find the TypeSerializer for the specified record type.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public TypeSerializer FindSerializerForType( Type t )
		{
			lock( syncRoot )
				return m_tables.Where( tbl => tbl.recordType.Equals( t ) ).FirstOrDefault();
		}
	}
}