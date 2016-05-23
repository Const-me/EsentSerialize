using Windows.Security.ExchangeActiveSyncProvisioning;

namespace PerfVsSqlite
{
	static class Utils
	{
		public static bool isPhone()
		{
			string os = new EasClientDeviceInformation().OperatingSystem;
			os = os.ToLowerInvariant();
			return os.Contains( "phone" ) || os.Contains( "mobile" );
		}
	}
}