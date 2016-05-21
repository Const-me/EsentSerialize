using System.ServiceModel;

namespace Shared
{
	[ServiceContract]
	public interface iPersonsService
	{
		[OperationContract]
		void add( PersonMessage msg );

		[OperationContract]
		int getTotalCount();

		[OperationContract]
		PersonMessage[] sortBySex();

		[OperationContract]
		PersonMessage[] queryBySex( PersonMessage.eSex value );

		[OperationContract]
		PersonMessage[] queryByNameSubstring( string substring );
	}
}