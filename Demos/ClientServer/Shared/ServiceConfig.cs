using System;
using System.Threading;

namespace Shared
{
	public static class ServiceConfig
	{
		// Server sets when ready and listening, resets when shuts down.
		public static readonly EventWaitHandle eventServerReady = new EventWaitHandle( false, EventResetMode.ManualReset, @"{0f751dcd-99a4-4933-b540-1dca4cc8af57}_rdy" );

		const string pipeBaseAddress = @"net.pipe://localhost";

		/// <summary>Pipe name</summary>
		public const string pipeName = @"0f751dcd-99a4-4933-b540-1dca4cc8af57";

		/// <summary>Base addresses for the hosted service.</summary>
		public static Uri baseAddress { get { return new Uri( pipeBaseAddress ); } }

		/// <summary>Complete address of the named pipe endpoint.</summary>
		public static Uri endpointAddress { get { return new Uri( pipeBaseAddress + '/' + pipeName ); } }
	}
}