using EsentSerialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Database;
using TestFilter = System.Linq.Expressions.Expression<System.Func<Database.FiltersTest, bool>>;

namespace WsaDemo81
{
	/// <summary>An empty page that can be used on its own or navigated to within a Frame.</summary>
	public sealed partial class MainPage : Page
	{
		public MainPage()
		{
			this.InitializeComponent();
		}

		static void PrintPersons( IEnumerable<Person> arr )
		{
			foreach( var p in arr )
				Debug.WriteLine( "{0}", p.ToString() );
			Debug.WriteLine( "" );
		}

		void testPersons( iSerializerSession sess )
		{
			Recordset<Person> rs;
			sess.getTable( out rs );

			Debug.WriteLine( "Total count: {0}", rs.Count() );

			Debug.WriteLine( "Sorted by sex:" );
			PrintPersons( rs.orderBy( p => p.sex ) );

			Debug.WriteLine( "Males:" );
			PrintPersons( rs.where( p => p.sex == Person.eSex.Male ) );

			Debug.WriteLine( @"Names containing ""Smith"":" );
			PrintPersons( rs.where( p => p.name.Contains( "Smith" ) ) );
		}

		void testFilters( iSerializerSession sess )
		{
			Recordset<FiltersTest> rs;
			sess.getTable( out rs );

			TestFilter[] filters = new TestFilter[]
			{
				// ft => ft.c2 == 8,
				// ft => ft.c2 >= 9,
				// ft => ft.c2 >= 2 && ft.c2 <= 3,
				// ft => 4 == ft.c2 && 5 == ft.c1 && ft.c3 >= 2 && ft.c3 <= 3,
				ft => Queries.greaterOrEqual( ft.c2, 9 ),
			};

			foreach( var f in filters )
			{
				var q = Queries.filter( rs.cursor.serializer, f );
				Debug.WriteLine( "{0} => {1}", f.ToString(), rs.count( q ) );
			}
		}

		private void Page_Loaded( object sender, RoutedEventArgs e )
		{
			try
			{
				using( var db = EsentDatabase.open( typeof( Person ).GetTypeInfo().Assembly ) )
				using( var sess = db.GetSession() )
				{
					if( db.isNewDatabase )
					{
						Person.populateWithDebugData( sess );
						FiltersTest.populateWithDebugData( sess );
					}
					testPersons( sess );
					// testFilters( sess );
				}
			}
			catch( Exception ex )
			{
				Debug.WriteLine( "Failed: {0}", ex.Message );
			}
		}
	}
}