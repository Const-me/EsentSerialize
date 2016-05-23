using EsentSerialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickStart
{
	// Prototype for test actions. Return true to commit the transaction.
	using tAction = System.Func<iSerializerSession, bool>;

	class Program
	{
		// Command -> test action
		static readonly Dictionary<char, tAction> actions = new Dictionary<char, tAction>()
		{
			{  'c', create },
			{  'r', read },
			{  'u', update },
			{  'd', delete },

			{  'a', all },
			{  's', search },
		};

		static void Main( string[] args )
		{
			using( var pool = EsentDatabase.open( typeof( Record ) ) )
			{
				while( true )
				{
					Console.WriteLine( "c = create, r = read, u = update, d = delete, s = search, a = all, q = quit" );
					char c = char.ToLower( Console.ReadKey().KeyChar );
					Console.WriteLine();
					if( 'q' == c )
						return;

					tAction act;
					if( !actions.TryGetValue( c, out act ) )
						continue;

					using( var sess = pool.GetSession() )
					using( var trans = sess.BeginTransaction() )
					{
						try
						{
							if( act( sess ) )
								trans.Commit();
						}
						catch( Exception ex )
						{
							Console.WriteLine( "Error: {0}", ex.Message );
						}
					}
				}
			}
		}

		/// <summary>Ask the ID, return null if invalid input.</summary>
		static int? askId()
		{
			Console.Write( "ID: " );
			string line = Console.ReadLine();
			int id;
			if( !int.TryParse( line, out id ) )
			{
				Console.WriteLine( "Malformed ID" );
				return null;
			}
			return id;
		}

		/// <summary>Ask user to enter a record, i.e. ID and text.</summary>
		static Record askRecord()
		{
			int? id = askId();
			if( !id.HasValue ) return null;

			Console.Write( "Text: " );
			string txt = Console.ReadLine();
			return new Record( id.Value, txt );
		}

		static bool create( iSerializerSession sess )
		{
			Record r = askRecord();
			if( null == r )
				return false;
			sess.Cursor<Record>().Add( r );
			return true;
		}

		static bool read( iSerializerSession sess )
		{
			int? id = askId();
			if( !id.HasValue ) return false;
			var rec = sess.Recordset<Record>().where( r => r.id == id.Value ).FirstOrDefault();
			if( null != rec )
				Console.WriteLine( "{0}", rec );
			else
				Console.WriteLine( "Not found" );
			return false;
		}

		static bool update( iSerializerSession sess )
		{
			Record u = askRecord();
			if( null == u )
				return false;
			var rs = sess.Recordset<Record>();
			if( !rs.seek( r => r.id == u.id ) )
			{
				Console.WriteLine( "Not found" );
				return false;
			}
			rs.cursor.Update( u );
			return true;
		}

		static bool delete( iSerializerSession sess )
		{
			int? id = askId();
			if( !id.HasValue ) return false;
			var rs = sess.Recordset<Record>();
			if( !rs.seek( r => r.id == id.Value ) )
			{
				Console.WriteLine( "Not found" );
				return false;
			}
			rs.cursor.delCurrent();
			return true;
		}

		static bool all( iSerializerSession sess )
		{
			foreach( var r in sess.Recordset<Record>().all() )
				Console.WriteLine( "{0}", r );
			return false;
		}

		static bool search( iSerializerSession sess )
		{
			Console.Write( "Text substring: " );
			string txt = Console.ReadLine();
			foreach( var r in sess.Recordset<Record>().where( r => r.text.Contains( txt ) ) )
				Console.WriteLine( "{0}", r );
			return false;
		}
	}
}