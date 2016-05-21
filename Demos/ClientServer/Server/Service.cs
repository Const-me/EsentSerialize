using Database;
using EsentSerialization;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace Server
{
	[ServiceBehavior( InstanceContextMode = InstanceContextMode.Single )]
	class Service : iPersonsService
	{
		readonly SessionPool sessionPool;

		public Service( SessionPool sessionsPool )
		{
			this.sessionPool = sessionsPool;
		}

		internal T withRecordset<T>( Func<Recordset<Person>, T> produce )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				Recordset<Person> rs;
				sess.getTable( out rs );
				return produce( rs );
			}
		}

		static internal PersonMessage[] array( IEnumerable<Person> enm )
		{
			return enm.Select( rec => rec.toMessage() ).ToArray();
		}

		void iPersonsService.add( PersonMessage msg )
		{
			using( var sess = sessionPool.GetSession() )
			using( var trans = sess.BeginTransaction() )
			{
				Cursor<Person> cur;
				sess.getTable( out cur );
				cur.Add( msg.toRecord() );
				trans.Commit();
			}
		}

		int iPersonsService.getTotalCount()
		{
			return withRecordset( rs => rs.Count() );
		}

		PersonMessage[] iPersonsService.sortBySex()
		{
			return withRecordset( rs => 
			{
				rs.filterSort( "sex", false );
				return array( rs.all() );
			} );
		}

		PersonMessage[] iPersonsService.queryBySex( PersonMessage.eSex value )
		{
			return withRecordset( rs =>
			{
				rs.filterFindEqual( "sex", value.toRecord() );
				return array( rs.all() );
			} );
		}

		PersonMessage[] iPersonsService.queryByNameSubstring( string substring )
		{
			return withRecordset( rs =>
			{
				rs.filterFindSubstring( "name", substring );
				return array( rs.uniq() );
			} );
		}
	}
}