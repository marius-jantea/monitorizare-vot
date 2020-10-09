using MediatR;

namespace VoteMonitor.Api.Location.Commands
{
	public class ClearAllPollingStationsCommand : IRequest<int>
	{
	}
}
