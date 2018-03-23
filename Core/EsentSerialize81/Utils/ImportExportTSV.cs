using System;
using System.Linq;
using System.Text;
using Microsoft.Isam.Esent.Interop;
using System.IO;
using System.Runtime.Serialization;
using System.Diagnostics;
using EsentSerialization.Attributes;

namespace EsentSerialization
{
	// This file implements tab-delimited values imported and exporter for a ESE database.
	// Only suitable for small records: in the process each record being kept in RAM as a Unicode string, so don't use it for 1GB long values.
	// Initial design goal was Excel-friendliness, so the records are saved as Tab Separated Values (TSV), one record per line.
	// My tests revealed Microsoft Excel 2007 handles UTF-16 encoded TSV documents pretty good; the only drawback is it shows a yes/no message box after each Ctrl+S.
	// Later this same data format was employed for data backup/restore operations: ZIP compression minimizes size overhead, while keeping the format human-readable greatly simplifies some things.
	class ImportExportTSV : ImportExport
	{
		// Retrieve the column value from the ESE.
		// Returns the properly-escaped string suitable for TSV file.
		string getStringValue( TextWriter bw, JET_COLUMNID idColumn, int itagSequence, JET_coltyp ct, JET_CP cp )
		{
			JET_RETINFO ri = new JET_RETINFO();
			ri.itagSequence = itagSequence;

			int cbSize;
			Api.JetRetrieveColumn( sess.idSession, idTable, idColumn,
				buff, buff.Length, out cbSize, RetrieveColumnGrbit.None, ri );
			if( cbSize <= 0 ) return "";

			if ( ct == JET_coltyp.Currency )	 // int64
			{
				Debug.Assert( 8 == cbSize );
				return BitConverter.ToUInt64( buff, 0 ).ToString( "X16" );
			}

			if ( ct == JET_coltyp.Long )         // int32
			{
				Debug.Assert( 4 == cbSize );
				return BitConverter.ToUInt32( buff, 0 ).ToString( "X8" );
			}

			if( ct == JET_coltyp.Short )         // int16
			{
				Debug.Assert( 2 == cbSize );
				return BitConverter.ToUInt16( buff, 0 ).ToString( "X4" );
			}

			if( ct == JET_coltyp.UnsignedByte )  // uint8
			{
				Debug.Assert( 1 == cbSize );
				return buff[ 0 ].ToString( "X2" );
			}

			if( ct == JET_coltyp.DateTime )      // DateTime
			{
				Debug.Assert( 8 == cbSize );
				double dbl = BitConverter.ToDouble( buff, 0 );
				DateTime dt = Conversions.ConvertDoubleToDateTime( dbl );
				return dt.ToString( "o" );
			}

			if( ct == JET_coltyp.Text || ct == JET_coltyp.LongText ) // Text
			{
				if( cp == JET_CP.Unicode )
					return TSV.EscapeUnicode( buff, cbSize );
				else
					return TSV.EscapeASCII( buff, cbSize );
			}
			return Convert.ToBase64String( buff, 0, cbSize );        // Anything else
		}

		// Put the column value to the ESE.
		void LoadVal( string strVal, JET_COLUMNID idColumn, JET_coltyp ct, JET_CP cp )
		{
			if( String.IsNullOrEmpty( strVal ) ) return;

			byte[] val = null;
			if ( ct == JET_coltyp.Currency )                                 // int64
				val = BitConverter.GetBytes( Convert.ToUInt64( strVal, 16 ) );
			else if ( ct == JET_coltyp.Long )                                // int32
				val = BitConverter.GetBytes( Convert.ToUInt32( strVal, 16 ) );
			else if ( ct == JET_coltyp.Short )                               // int16
				val = BitConverter.GetBytes( Convert.ToUInt16( strVal, 16 ) );
			else if ( ct == JET_coltyp.UnsignedByte )                        // uint8
				val = new byte[ 1 ] { Convert.ToByte( strVal, 16 ) };
			else if ( ct == JET_coltyp.DateTime )                            // DateTime
			{
				DateTime dt = DateTime.Parse( strVal, null, System.Globalization.DateTimeStyles.RoundtripKind );
				double dbl = dt.ToOADate();
				val = BitConverter.GetBytes( dbl );
			}
			else if ( ct == JET_coltyp.Text || ct == JET_coltyp.LongText )   // Text
			{
				if ( cp == JET_CP.Unicode )
					val = TSV.UnescapeUnicode( strVal ).ToArray();
				else
					val = TSV.UnescapeASCII( strVal ).ToArray();
			}
			else val = Convert.FromBase64String( strVal );                   // Anything else

			JET_SETINFO si = new JET_SETINFO();
			si.itagSequence = 0;
			Api.JetSetColumn( sess.idSession, idTable, idColumn, val, val.Length, SetColumnGrbit.None, si );
		}

		int Export( TextWriter tw )
		{
			int cols = schema.Count;

			// Header
			tw.WriteLine( this.GetType().FullName );
			tw.WriteLine( cols );

			// Columns schema
			JET_COLUMNDEF[] arrCd = new JET_COLUMNDEF[ cols ];
			string[] arr;
			for( int i=0; i < cols; i++ )
			{
				TypeSerializer.ColumnInfo ci = schema[ i ];
				JET_COLUMNDEF cd = ci.attrib.getColumnDef();
				arrCd[ i ] = cd;

				arr = new string[]
				{
					ci.columnName,
					ci.attrib.getColumnKind().ToString(),
					((int)cd.coltyp).ToString(),
					((int)cd.cp).ToString(),
					((int)cd.grbit).ToString(),
				};
				tw.WriteLine( string.Join( "\t", arr ) );
			}

			// Rows
			arr = new string[ cols ];
			return ExportData
			(
				( int nRecord ) =>
				{
					for( int i=0; i < cols; i++ )
					{
						JET_COLUMNDEF cd = arrCd[ i ];

						if( isMultiValued( cd ) )
						{
							for( int j=1; true; j++ )
							{
								string val = getStringValue( tw, schema[ i ].idColumn, j, cd.coltyp, cd.cp );
								if( val == "" ) break;
								if( j > 1 )
									tw.Write( '/' );
								tw.Write( val );
							}
						}
						else
						{
							string val = getStringValue( tw, schema[ i ].idColumn, 1, cd.coltyp, cd.cp );
							tw.Write( val );
						}
						if( i + 1 < cols ) tw.Write( '\t' );
					}
					tw.WriteLine();
				}
			);
		}

		static readonly char[] s_cTab = new char[ 1 ] { '\t' };
		static readonly char[] s_cSlash = new char[ 1 ] { '/' };

		int Import( TextReader tr )
		{
			string line;

			// Header; we also trim that extra trailing tabs being appended by Excel when saving documents in the TSV format.
			line = tr.ReadLine().TrimEnd( s_cTab );
			if( line != this.GetType().FullName ) throw new SerializationException( "Wrong signature: stored " + line + ", must be " + this.GetType().FullName + "." );
			line = tr.ReadLine().TrimEnd( s_cTab );
			int cols = int.Parse( line );
			if( schema.Count != cols ) throw new SerializationException( "Wrong columns count: stored " + cols + ", must be " + schema.Count + "." );

			// Validate the stored columns schema.
			JET_COLUMNDEF[] arrCd = new JET_COLUMNDEF[ cols ];
			string[] arr;
			for( int i=0; i < cols; i++ )
			{
				TypeSerializer.ColumnInfo ci = schema[ i ];
				JET_COLUMNDEF cd = ci.attrib.getColumnDef();
				arrCd[ i ] = cd;

				line = tr.ReadLine();
				arr = line.Split( new char[] { '\t' } );
				if( arr[ 0 ] != ci.columnName ) throw new SerializationException( "Wrong column name: stored '" + arr[ 0 ] + "', must be '" + ci.columnName + "'." );

				eColumnKind ck = (eColumnKind)( Enum.Parse( typeof( eColumnKind ), arr[ 1 ] ) );
				if( ck != ci.attrib.getColumnKind() ) throw new SerializationException( "Wrong column kind: stored '" + ck.ToString() + "', must be '" + ci.attrib.getColumnKind().ToString() + "'." );

				if( cd.coltyp != (JET_coltyp)( int.Parse( arr[ 2 ] ) ) ) throw new SerializationException( "Mismatched type of column '" + ci.columnName + "'" );
				if( cd.cp != (JET_CP)( int.Parse( arr[ 3 ] ) ) ) throw new SerializationException( "Mismatched codepage of column '" + ci.columnName + "'" );
				if( cd.grbit != (ColumndefGrbit)( int.Parse( arr[ 4 ] ) ) ) throw new SerializationException( "Mismatched bit flags of column '" + ci.columnName + "'" );
			}

			// Import the rows.
			return ImportData
			(
				( int nRecord ) =>
				{
					line = tr.ReadLine();
					return line != null;
				},
				( int nRecord ) =>
				{
					if( line == "" ) return false;
					arr = line.Split( s_cTab );
					if( arr.Length < cols )
						throw new SerializationException( "Too few columns in the line '" + line + "'" );
					for( int i=0; i < cols; i++ )
					{
						JET_COLUMNDEF cd = arrCd[ i ];
						if( 0 != ( cd.grbit & ColumndefGrbit.ColumnAutoincrement ) )
						{
							// TODO: look for a way to restore ColumnAutoincrement values.
							continue;
						}

						if( !isMultiValued( cd ) )
						{
							LoadVal( arr[ i ], schema[ i ].idColumn, cd.coltyp, cd.cp );
						}
						else
						{
							string[] arr2 = arr[ i ].Split( s_cSlash );
							foreach( string item in arr2 )
								LoadVal( item, schema[ i ].idColumn, cd.coltyp, cd.cp );
						}
					}
					return true;
				}
			);
		}

		public ImportExportTSV( EseCursorBase cur ) : base(cur) { }

		public ImportExportTSV( iSerializerSession _sess, JET_TABLEID _idTable, TypeSerializer _serializer )
			 : base( _sess, _idTable, _serializer ) { }

		static readonly Encoding encoding = new UTF8Encoding( true, true );

		public override int Export( Stream stm )
		{
			using( TextWriter tw = new StreamWriter( stm, encoding ) )
				return Export( tw );
		}

		public override int Import( Stream stm )
		{
			using( TextReader tr = new StreamReader( stm ) )
				return Import( tr );
		}
	}
}