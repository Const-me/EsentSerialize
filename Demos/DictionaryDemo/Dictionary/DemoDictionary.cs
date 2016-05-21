using EsentSerialization;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;

namespace DictionaryDemo
{
	class DemoDictionary: IDisposable, IEnumerable<KeyValuePair<string, ValueType>>
	{
		SessionPool sessionPool;

		public DemoDictionary( string folder )
		{
			EsentDatabase.Settings settings = new EsentDatabase.Settings()
			{
				folderLocation = folder,
				folderName = null,
			};
			sessionPool = EsentDatabase.open( settings, typeof( DictionaryEntry ) );
		}

		public void Dispose()
		{
			if( null != sessionPool )
			{
				sessionPool.Dispose();
				sessionPool = null;
			}
		}

		public int Count { get {
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				return sess.Recordset<DictionaryEntry>().Count();
			}
		} }

		public void Add( string key, ValueType value )
		{
			this.Add( key, value, true );
		}

		public bool ContainsKey( string key )
		{
			throw new NotImplementedException();
		}

		public IEnumerable<string> Keys
		{
			get
			{
				using( var sess = sessionPool.GetSession() )
				using( var trans = sess.BeginTransaction() )
				{
					var rs = sess.Recordset<DictionaryEntry>();
					if( !rs.applyFilter() )
						yield break;
					do
					{
						// Use FetchSingleField API here, to avoid values to be deserialized.
						yield return (string)rs.cursor.FetchSingleField("key");
					}
					while( rs.tryMoveNext() );
				}
			}
		}

		public bool Remove( string key )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var rs = sess.Recordset<DictionaryEntry>();
				rs.filterFindEqual( "key", key );
				if( !rs.applyFilter() )
					return false;
				rs.cursor.delCurrent();
				trans.Commit();
				return true;
			}
		}

		public bool TryGetValue( string key, out ValueType value )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var rs = sess.Recordset<DictionaryEntry>();
				rs.filterFindEqual( "key", key );
				if( !rs.applyFilter() )
				{
					value = null;
					return false;
				}
				value = rs.cursor.getCurrent().value;
				return true;
			}
		}

		public IEnumerable<ValueType> Values
		{
			get
			{
				using( var sess = sessionPool.GetSession() )
				using( var trans = sess.BeginTransaction() )
				{
					var rs = sess.Recordset<DictionaryEntry>();
					foreach (var e in rs.all())
						yield return e.value;
				}
			}
		}

		private void Add( string key, ValueType value, bool throwIfExist )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var rs = sess.Recordset<DictionaryEntry>();
				rs.filterFindEqual( "key", key );
				if( rs.applyFilter() )
				{
					if( throwIfExist )
						throw new ArgumentException();
					rs.cursor.SaveSingleField( "value", value );
				}
				else
				{
					var ne = new DictionaryEntry();
					ne.key = key;
					ne.value = value;
					rs.cursor.Add( ne );
				}
				trans.Commit();
			}
		}

		public ValueType this[ string key ]
		{
			get
			{
				ValueType res;
				if( !this.TryGetValue( key, out res ) )
					throw new KeyNotFoundException();
				return res;
			}
			set
			{
				Add( key, value, false );
			}
		}

		public void Add( KeyValuePair<string, ValueType> item )
		{
			Add( item.Key, item.Value, true );
		}

		public void Clear()
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var rs = sess.Recordset<DictionaryEntry>();
				rs.EraseAll();
				trans.Commit();
			}
		}

		public bool Remove( KeyValuePair<string, ValueType> item )
		{
			return this.Remove( item.Key );
		}

		public IEnumerator<KeyValuePair<string, ValueType>> GetEnumerator()
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var cur = sess.Cursor<DictionaryEntry>();
				cur.ResetIndex();
				if( !cur.TryMoveFirst() )
					yield break;
				do
				{
					var i = cur.getCurrent();
					yield return new KeyValuePair<string, ValueType>( i.key, i.value );
				}
				while( cur.tryMoveNext() );
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public byte[] dbgRawValue( string key )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				var rs = sess.Recordset<DictionaryEntry>();
				rs.filterFindEqual( "key", key );
				if( !rs.applyFilter() )
					return null;
				var colId = rs.cursor.serializer.GetColumnId( rs.cursor, "value" );
				return Api.RetrieveColumn( rs.idSession, rs.idTable, colId );
			}
		}
	}
}