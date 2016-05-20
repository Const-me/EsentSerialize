using EsentSerialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WsaDemo81.Database;
using TestFilter = System.Linq.Expressions.Expression<System.Func<WsaDemo81.Database.FiltersTest, bool>>;

namespace WsaDemo81
{
	/// <summary>An empty page that can be used on its own or navigated to within a Frame.</summary>
	public sealed partial class MainPage : Page
	{
		public MainPage()
		{
			this.InitializeComponent();
		}

		static IEnumerable<FiltersTest> populateFt()
		{
			for( byte i = 0; i < 16; i++ )
				for( byte j = 0; j < 16; j++ )
					for( byte k = 0; k < 16; k++ )
						yield return new FiltersTest()
						{
							c1 = k,
							c2 = j,
							c3 = i
						};
		}

		static void PopulateDemoDatabase( iSerializerSession sess )
		{
			Person[] personsTest = new Person[]
			{
				new Person( Person.eSex.Female, "Jenifer Smith", new string[0] ),
				new Person( Person.eSex.Male, "Konstantin", new string[]{ "+7 926 139 63 18" } ),
				new Person( Person.eSex.Male, "John Smith", new string[]{ "+1 800 123 4567", "+1 800 123 4568" } ),
				new Person( Person.eSex.Female, "Mary Jane", new string[]{ "555-1212" } ),
				new Person( Person.eSex.Other, "Microsoft", new string[]{ "+1 800 642 7676", "1-800-892-5234" } ),
			};

			Cursor<Person> curPerson;
			Cursor<FiltersTest> curTest;
			sess.getTable( out curPerson );
			sess.getTable( out curTest );

			using( var trans = sess.BeginTransaction() )
			{
				curPerson.AddRange( personsTest );
				curTest.AddRange( populateFt() );
				trans.Commit();
			}
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
						PopulateDemoDatabase( sess );
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