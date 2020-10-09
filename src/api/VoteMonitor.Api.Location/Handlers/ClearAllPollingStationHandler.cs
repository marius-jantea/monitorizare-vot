using MediatR;
using System.Threading;
using System.Threading.Tasks;
using VoteMonitor.Api.Location.Commands;
using VoteMonitor.Api.Location.Services;

namespace VoteMonitor.Api.Location.Handlers
{
	class ClearAllPollingStationHandler : IRequestHandler<ClearAllPollingStationsCommand, int>
	{
		private readonly IPollingStationService _pollingStationService;
		public ClearAllPollingStationHandler(IPollingStationService pollingStationService)
		{
			_pollingStationService = pollingStationService;
		}

		public async Task<int> Handle(ClearAllPollingStationsCommand request, CancellationToken cancellationToken)
		{
			return await _pollingStationService.ClearAll(cancellationToken);
		}
	}
}
