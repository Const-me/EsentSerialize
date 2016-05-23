using PerfVsSqlite.Database;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PerfVsSqlite
{
	public sealed partial class MainPage : Page
	{
		public MainPage()
		{
			this.InitializeComponent();
			Debug.WriteLine( "Appdata: " + Windows.Storage.ApplicationData.Current.LocalFolder.Path );
		}

		bool SQLite = false;

		void btnEngine_Click( object sender, RoutedEventArgs e )
		{
			SQLite = !SQLite;
			btnEngine.Content = SQLite ? "SQLite" : "ESENT";
		}

		async Task messageBox( string message, params object[] args )
		{
			string m = String.Format(message, args);
			MessageDialog a = new MessageDialog( m);
			await a.ShowAsync();
		}

		async void Test1_Click( object sender, RoutedEventArgs e )
		{
			await runTest( Tests.populate );
		}

		async void Count_Click( object sender, RoutedEventArgs e )
		{
			await runTest( Tests.count0 );
		}
		async void Fetch_Click( object sender, RoutedEventArgs e )
		{
			await runTest( Tests.fetch0 );
		}

		async void CountWhere_Click( object sender, RoutedEventArgs e )
		{
			await runTest( Tests.count );
		}
		async void FetchWhere_Click( object sender, RoutedEventArgs e )
		{
			await runTest( Tests.fetch );
		}

		async Task runTest( Func<iDatabase, Tuple<int, TimeSpan>> act )
		{
			VisualStateManager.GoToState( this, "pending", true );
			string message = await Tests.runTest( SQLite, act );

			VisualStateManager.GoToState( this, "normal", true );
			await messageBox( message );
		}
	}
}