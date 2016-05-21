using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Isam.Esent.Interop;
using System.ComponentModel;
using EsentSerialization;

namespace EseFileSystem
{
	static class EFS
	{
		static JET_COLUMNID colId = JET_COLUMNID.Nil;
		static JET_COLUMNID colIsDirectory = JET_COLUMNID.Nil;

		static int m_idRootFolder;
		public static int idRootFolder { get { return m_idRootFolder; } }

		static void LookupColumnIds( Cursor<EfsEntry> cur )
		{
			colId = cur.GetColumnId( "id" );
			colIsDirectory = cur.GetColumnId( "isDirectory" );
		}

		public static void Initialize( Recordset<EfsEntry> rs )
		{
			LookupColumnIds( rs.cursor );

			rs.filterFindEqual( "name", null );
			m_idRootFolder = rs.getFirst().id;
		}

		static int GetId( EseCursorBase cur )
		{
			return Api.RetrieveColumnAsInt32( cur.idSession, cur.idTable, colId, RetrieveColumnGrbit.RetrieveFromIndex ).Value;
		}

		static bool isDirectory( EseCursorBase cur )
		{
			bool? val = Api.RetrieveColumnAsBoolean( cur.idSession, cur.idTable,
				colIsDirectory, RetrieveColumnGrbit.RetrieveFromIndex );
			return ( val.HasValue && val.Value );
		}

		/// <summary>Find the file in a given folder.</summary>
		/// <remarks>If found, the rs.cursor will be positioned on it.</remarks>
		/// <param name="rs"></param>
		/// <param name="idParent"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static int? FindFile( Recordset<EfsEntry> rs, int? idParent, string name )
		{
			if( name.IndexOfAny( s_PathSplitCharacters ) >= 0 )
				throw new IOException( "Invalid name" );
			if( name.Length > EfsEntry.ccMaxName )
				throw new PathTooLongException();

			rs.filterFindEqual( "name", idParent, name );
			if( !rs.applyFilter() ) return null;

			return GetId( rs.cursor );
		}

		public static int? FindFile( Recordset<EfsEntry> rs, int? idParent, string name, out bool bIsDirectory )
		{
			int? res = FindFile( rs, idParent, name );
			bIsDirectory = false;
			if( res.HasValue )
				bIsDirectory = isDirectory( rs.cursor );
			return res;
		}

		static readonly char[] s_PathSplitCharacters = new char[ 2 ] { '\\', '/' };

		/// <summary>Split the input path into components</summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static IEnumerable<string> SplitPath( string path )
		{
			if( String.IsNullOrEmpty( path ) ) return null;

			return path
				.Split( s_PathSplitCharacters, StringSplitOptions.RemoveEmptyEntries )
				.Select( c =>
				{
					if( c.Length <= EfsEntry.ccMaxName)
						return c;
					// return c.Substring( 0, EfsEntry.ccMaxName );
					throw new PathTooLongException();
				} );
		}

		public static int? FindFileRecursively( Recordset<EfsEntry> rs, string filename )
		{
			var components = SplitPath( filename );
			if( null == components )
				return m_idRootFolder;

			int? id = null;
			bool bPrevComponentIsFolder = true;
			IEnumerator<string> e = components.GetEnumerator();
			while( e.MoveNext() )
			{
				if( !bPrevComponentIsFolder )
					return null;
				id = FindFile( rs, id, e.Current );
				if( !id.HasValue )
					return null;
				bPrevComponentIsFolder = isDirectory( rs.cursor );
			}
			return id;
		}

		public static int? FindFileRecursively( Recordset<EfsEntry> rs, string filename, out bool isFolder )
		{
			int? res = FindFileRecursively( rs, filename );
			isFolder = ( res.HasValue ) ? isDirectory( rs.cursor ) : false;
			return res;
		}

		/// <summary>Create a new folder</summary>
		/// <param name="cur"></param>
		/// <param name="idParent">Parent folder ID</param>
		/// <param name="name">Folder name</param>
		/// <returns></returns>
		public static EfsEntry CreateFolder( Cursor<EfsEntry> cur, int idParent, string name )
		{
			if( name.IndexOfAny( s_PathSplitCharacters ) >= 0 )
				throw new IOException( "Invalid name" );
			var ne = EfsEntry.NewFolder( idParent, name );
			cur.Add( ne );
			return ne;
		}

		public static EfsEntry CreateFolderEx( Cursor<EfsEntry> cur, int idParent, string path )
		{
			DirectoryInfo di = new DirectoryInfo( path );
			if( !di.Exists )
				throw new FileNotFoundException();
			var ne = EfsEntry.NewFolder( idParent, di );
			cur.Add( ne );
			return ne;
		}

		public static int CreateFolderRecursively( Recordset<EfsEntry> rs, int? idParent, IEnumerable<string> components )
		{
			if( !idParent.HasValue )
				idParent = m_idRootFolder;

			// Find existing portion.
			int? id = m_idRootFolder;

			IEnumerator<string> e = components.GetEnumerator();

			// Find existing part of the path
			while( true )
			{
				if( !e.MoveNext() )
					return id.Value;
				int? idNext = FindFile( rs, id, e.Current );
				if( !idNext.HasValue )
					break;

				/* bool? bIsDirectory = (bool?)rs.cursor.FetchSingleField( "isDirectory" );
				if( !bIsDirectory.HasValue || !bIsDirectory.Value )
					return null; */

				id = idNext;
			}

			// Create missing part of the path
			rs.cursor.ResetIndex();

			while (true)
			{
				EfsEntry ne = EfsEntry.NewFolder( id.Value, e.Current );
				rs.cursor.Add( ne );
				id = ne.id;

				if( !e.MoveNext() )
					return id.Value;
			}
		}

		public static bool TruncateFile( Recordset<EfsEntry> rs, int idFile )
		{
			rs.filterFindEqual( "id", idFile );
			if( !rs.applyFilter() )
				return false;
			var val = rs.cursor.FetchSingleField( "data" ) as EsentSerialization.Attributes.EseStreamValue;
			if( val.length <= 0 )
				return true;
			using( var stm = val.Write( rs.cursor ) )
				stm.SetLength( 0 );
			return true;
		}

		public static int DeleteFile( Recordset<EfsEntry> rs, string path )
		{
			var components = SplitPath( path );
			if( null == components )
				return 0;

			bool dir;
			int? idFile = FindFileRecursively( rs, path, out dir );
			if( !idFile.HasValue || idFile.Value == m_idRootFolder )
				return 0;

			rs.cursor.delCurrent();

			if( !dir ) return 1;

			// Recursive erase..
			return 1 + DeleteChildren( rs, idFile.Value );
		}

		public static int DeleteFile( Recordset<EfsEntry> rs, int id )
		{
			if( id == m_idRootFolder )
				return 0;

			rs.filterFindEqual( "id", id );
			if( !rs.applyFilter() )
				return 0;

			rs.cursor.delCurrent();
			return 1 + DeleteChildren( rs, id );
		}

		static int DeleteChildren( Recordset<EfsEntry> rs, int idParent )
		{
			HashSet<int> foldersToTraverse = new HashSet<int>();
			HashSet<int> objectsToErase = new HashSet<int>();

			foldersToTraverse.Add( idParent );

			while( foldersToTraverse.Count > 0 )
			{
				int idFolder = foldersToTraverse.First();
				foldersToTraverse.Remove( idFolder );

				rs.filterFindEqual( "name", idFolder );
				if( !rs.applyFilter() )
					continue;

				do
				{
					int idFile = GetId( rs.cursor );
					objectsToErase.Add( idFile );
					if( isDirectory( rs.cursor ) )
						foldersToTraverse.Add( idFile );
				}
				while( rs.cursor.tryMoveNext() );
			}

			if( objectsToErase.Count <= 0 ) return 0;

			int nErased = 0;
			foreach( int idFile in objectsToErase.OrderByDescending( i => i ) )
			{
				rs.filterFindEqual( "id", idFile );
				if( !rs.applyFilter() ) continue;
				rs.cursor.delCurrent();
				nErased++;
			}

			return nErased;
		}

		/* public static void FillFileInfo( EfsEntry e, FileInformation dest )
		{
			dest.Attributes = e.attributes;
			dest.FileName = e.name;
			dest.Length = e.Length;
			dest.CreationTime = e.dtCreation;
			dest.LastWriteTime = e.dtModification;
			dest.LastAccessTime = e.dtAccess;
		}

		public static IEnumerable<FileInformation> ListChildren( Recordset<EfsEntry> rs, int idParent )
		{
			rs.filterFindEqual( "name", idParent );

			foreach( var e in rs.all() )
			{
				FileInformation res = new FileInformation();
				FillFileInfo( e, res );
				yield return res;
			}
		} */

		public static IEnumerable<EfsEntry> ListChildren( Recordset<EfsEntry> rs, int idParent )
		{
			rs.filterFindEqual( "name", idParent );
			return rs.all();
		}

		public static IEnumerable<EfsEntry> ListChildrenFolders( Recordset<EfsEntry> rs, int idParent )
		{
			rs.filterFindEqual( "name", idParent );
			if( !rs.applyFilter() ) yield break;
			do 
			{
				if( !isDirectory( rs.cursor ) )
					continue;
				yield return rs.cursor.getCurrent();
			}
			while( rs.cursor.tryMoveNext() );
		}

		public static void MoveFile( Recordset<EfsEntry> rs, int idFile, int idNewParent, string strNewName )
		{
			rs.filterFindEqual( "id", idFile );
			if( !rs.applyFilter() )
				throw new FileNotFoundException();

			EfsEntry ee = EfsEntry.NewFile( idNewParent, strNewName );
			rs.cursor.UpdateFields( ee, "idParent", "name" );
			// rs.cursor.UpdateFields( ee, "idParent", "name", "dtAccess" );
		}

		public static int GetParent( Recordset<EfsEntry> rs, int idFile )
		{
			rs.filterFindEqual( "id", idFile );
			if( !rs.applyFilter() )
				throw new FileNotFoundException();
			return (int)rs.cursor.FetchSingleField( "idParent" );
		}

		public static EfsEntry byId( Recordset<EfsEntry> rs, int idFile )
		{
			rs.filterFindEqual( "id", idFile );
			if( !rs.applyFilter() )
				throw new FileNotFoundException();
			return rs.cursor.getCurrent();
		}

		const int copyBufferSize = 256 * 1024;

		public static void CopyStream( Stream input, Stream output )
		{
			byte[] buffer = new byte[ copyBufferSize ];
			while( true )
			{
				int read = input.Read( buffer, 0, buffer.Length );
				if( read <= 0 )
					return;
				output.Write( buffer, 0, read );
			}
		}

		public static EfsEntry AddFile( Recordset<EfsEntry> rs, int idParent, iFileIo io, string sourcePath, out long cbBytes )
		{
			FileInfo fi = new FileInfo( sourcePath );
			if( !fi.Exists )
				throw new FileNotFoundException();

			// Overwrite the old one
			int? idExisting = FindFile( rs, idParent, fi.Name );
			if( idExisting.HasValue )
				DeleteFile( rs, idExisting.Value );

			using( var trans = rs.session.BeginTransaction() )
			{
				// Create a new one.
				var ne = EfsEntry.NewFile( idParent, fi );
				rs.cursor.Add( ne );
				trans.LazyCommitAndReopen();

				// Copy the data.
				rs.filterFindEqual( "id", ne.id );
				ne = rs.getFirst();

				using( var stm = ne.data.Write( rs.cursor ) )
					cbBytes = io.Read( stm, sourcePath );

				trans.Commit();

				return ne;
			}
		}
	}
}