using System;

namespace EsentSerialization
{
	public static partial class EsentDatabase
	{
		/// <summary>Class holding advanced database parameters.</summary>
		/// <remarks>
		/// <para>For an average mobile or desktop app, the defaults should work well enough.</para>
		/// <para>If however you're building something high-loaded, you'll want to adjust them.</para>
		/// </remarks>
		public class AdvancedSettings
		{
			// Don't remember why "j11", it's here since version 1.0 of the library
			string m_BaseName = "j11";

			/// <summary>This parameter sets the three letter prefix used for many of the files used by the database engine.
			/// For example, the checkpoint file is called j11.chk by default because "j11" is the default base name. </summary>
			public string BaseName
			{
				get { return m_BaseName; }
				set
				{
					if( value.Length != 3 )
						throw new ArgumentException( "BaseName value must be 3 characters long" );
					m_BaseName = value;
				}
			}

			string m_FileName = "-esent.db";
			/// <summary>Name of the main database file, the default is "-esent.db".</summary>
			public string FileName
			{
				get { return m_FileName; }
				set
				{
					if( String.IsNullOrWhiteSpace( value ) )
						throw new ArgumentNullException();
					m_FileName = value;
				}
			}

			string m_InstanceName = "EseSerializer";

			/// <summary>A unique string identifier for the DB instance. This string must be unique within a given process hosting the database engine.</summary>
			public string InstanceName
			{
				get { return m_InstanceName; }
				set
				{
					m_InstanceName = InstanceName;
				}
			}

			int m_DatabasePageSize = 4096;

			/// <summary>Page size for the database. The page size is the smallest unit of space allocation possible for a database file.</summary>
			/// <remarks>Only 3 values are supported, 2048, 4096, and 8192 bytes. The default is 4kb.</remarks>
			public int DatabasePageSize
			{
				get { return m_DatabasePageSize; }
				set
				{
					if( value != 2048 && value != 4096 && value != 8192 )
						throw new ArgumentException();
					m_DatabasePageSize = value;
				}
			}

			int m_MaxVerPages = 0x100;

			/// <summary>Number of version store pages.</summary>
			/// <remarks>
			/// <para>Each version store page as configured by this parameter is 16KB in size.</para>
			/// <para>This is by far the most common resource to be exhausted by the database engine. When this resource is exhausted, updates to the database will be rejected with JET_errVersionStoreOutOfMemory.
			/// To release some of these resources, the oldest outstanding transaction must be aborted.</para>
			/// </remarks>
			public int MaxVerPages
			{
				get { return m_MaxVerPages; }
				set
				{
					if( value < 1 || value > 2147483647 )
						throw new ArgumentOutOfRangeException( "value" );
					m_MaxVerPages = value;
				}
			}

			int m_LogFileSize = 512;

			/// <summary>The size of the transaction log files</summary>
			/// <remarks>
			/// <para>The default in ESENT is 5120 = 5MB. The default in this library however is only 512 kb, to improve startup time assuming the DB isn't written too much.</para>
			/// <para>If you'll use this library to build a high-loaded server, you'll definitely want to increase this value to at least 5MB.</para>
			/// </remarks>
			public int kbLogFileSize
			{
				get { return m_LogFileSize; }
				set
				{
					if( value < 128 || value > 32768 )
						throw new ArgumentOutOfRangeException( "value" );
					m_LogFileSize = value;
				}
			}

			int? m_logBuffers = null;

			/// <summary>Amount of memory used to cache log records before they are written to the transaction log file.
			/// The unit for this parameter is the sector size of the volume that holds the transaction log files. The sector size is almost always 512 bytes.</summary>
			/// <remarks>
			/// <para>This parameter has an impact on performance. When the database engine is under heavy update load, this buffer can become full very rapidly.
			/// A larger cache size for the transaction log file is critical for good update performance under such a high load condition. The default is known to be too small for this case.</para>
			/// <para>The default is "Not Set" = 50% of the single log file, <see cref="kbLogFileSize" />.</para>
			/// </remarks>
			public int? LogBuffers
			{
				get
				{
					if( m_logBuffers.HasValue )
						return m_logBuffers;
					int halfLogFileSize = m_LogFileSize * 1024 / 512 / 2;
					return halfLogFileSize;
				}
				set
				{
					if( !value.HasValue )
					{
						m_logBuffers = null;
						return;
					}
					int val = value.Value;
					if( val < 80 || val > 2147483647 )
						throw new ArgumentOutOfRangeException();
					m_logBuffers = val;
				}
			}

			int m_IndexTuplesLengthMin = 3;

			/// <summary>Minimum tuple length in a tuple index.</summary>
			public int IndexTuplesLengthMin
			{
				get { return m_IndexTuplesLengthMin; }
				set
				{
					if( value < 2 || value > 255 )
						throw new ArgumentOutOfRangeException( "value" );
					m_IndexTuplesLengthMin = value;
				}
			}

			/// <summary>When this parameter is True, the database engine will use the Windows file cache as a read cache for all of its various files.</summary>
			/// <remarks>The default is False.</remarks>
			public bool EnableFileCache = false;

			/// <summary>When this parameter is True, the database engine will use database data directly from the Windows file cache rather than copying the cached data into its own private memory.</summary>
			/// <remarks>
			/// <para>The default is False.</para>
			/// <para>The intent of this mode is to further reduce the amount of private memory used by the database engine to cache database data.</para>
			/// <para>The view cache may only be used if the use of the Windows file cache is enabled by setting <see cref="EnableFileCache" /> to True.</para>
			/// </remarks>
			public bool EnableViewCache = false;
		}
	}
}