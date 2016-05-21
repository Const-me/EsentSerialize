using EsentSerialization.Attributes;
using System;
using System.IO;

namespace EseFileSystem
{
	[EseTable( "filesystem" )]
	[EsePrimaryIndex( "id", "+id\0\0" )]
	// This one is handy: it contains everything required for recursive searches.
	// The EFS static class uses this a lot, BTW it sometimes retrieve "id" and "isDirectory" columns directly from this index.
	[EseIndex( "name", "+idParent\0+name\0+id\0+isDirectory\0\0" )]

	class EfsEntry
	{
		public const int ccMaxName = 127;

		[EseAutoId( "id" )]
		public int id { get; private set; }

		[EseInt( "idParent" )]
		public int? idParent { get; private set; }

		[EseShortText( "name", allowNulls = false, bUnicode = true, maxChars = ccMaxName )]
		public string name { get; private set; }

		// NB! this field is never set to false. It's either true, or null.
		// This is done to simplify conditional indexing.
		[EseBool( "isDirectory" )]
		bool? m_isDirectory;
		public bool isDirectory { get { return m_isDirectory.HasValue && m_isDirectory.Value; } }

		[EseBool( "isReadOnly" )]
		bool isReadOnly;

		[EseBool( "isHidden" )]
		bool isHidden;

		[EseBool( "isSystem" )]
		bool isSystem;

		[EseBool( "isArchive" )]
		bool isArchive;

		public FileAttributes attributes
		{
			get
			{
				FileAttributes res = 0;

				if( isDirectory ) res |= FileAttributes.Directory;
				if( isReadOnly ) res |= FileAttributes.ReadOnly;
				if( isHidden ) res |= FileAttributes.Hidden;
				if( isArchive ) res |= FileAttributes.Archive;
				if( isSystem ) res |= FileAttributes.System;

				if( 0 == res ) res = FileAttributes.Normal;
				return res;
			}
			set
			{
				isReadOnly = ( 0 != ( value & FileAttributes.ReadOnly ) );
				isHidden = ( 0 != ( value & FileAttributes.Hidden ) );
				isArchive = ( 0 != ( value & FileAttributes.Archive ) );
				isSystem = ( 0 != ( value & FileAttributes.System ) );
			}
		}

		[EseDateTime( "dtCreation" )]
		public DateTime dtCreation { get; private set; }

		[EseDateTime( "dtAccess" )]
		public DateTime dtAccess { get; private set; }

		[EseDateTime( "dtModification" )]
		public DateTime dtModification { get; private set; }

		public void SetTime( DateTime creation, DateTime access, DateTime modification )
		{
			dtCreation = creation;
			dtAccess = access;
			dtModification = modification;
		}

		[EseBinaryStream( "data" )]
		public EseStreamValue data { get; private set; }

		void SetAllTimestamps()
		{
			DateTime dt = DateTime.UtcNow;
			dtCreation = dtAccess = dtModification = dt;
		}

		public static EfsEntry NewFile( int idParent, string name )
		{
			EfsEntry res = new EfsEntry();
			res.SetAllTimestamps();
			res.idParent = idParent;
			res.name = name;
			return res;
		}

		void SetInfo(FileSystemInfo info)
		{
			this.name = info.Name;
			this.attributes = info.Attributes;
			this.dtCreation = info.CreationTimeUtc;
			this.dtModification = info.LastWriteTimeUtc;
			this.dtAccess = info.LastAccessTimeUtc;
		}

		public static EfsEntry NewFile( int idParent, FileInfo info )
		{
			var res = new EfsEntry();
			res.idParent = idParent;
			res.SetInfo( info );
			return res;
		}

		public static EfsEntry NewFolder( int idParent, string name )
		{
			EfsEntry res = NewFile( idParent, name );
			res.m_isDirectory = true;
			return res;
		}

		public static EfsEntry NewFolder( int idParent, DirectoryInfo info )
		{
			var res = new EfsEntry();
			res.idParent = idParent;
			res.m_isDirectory = true;
			res.SetInfo( info );
			return res;
		}

		public static EfsEntry NewRoot()
		{
			EfsEntry res = NewFolder( 0, "" );
			res.idParent = null;
			return res;
		}

		public long Length { get
		{
			if( isDirectory )
				return 0;
			return data.length;
		} }
	}
}