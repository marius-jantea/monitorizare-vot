using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoteMonitor.Api.Core.Services;
using VoteMonitor.Api.Location.Models;
using VoteMonitor.Entities;

namespace VoteMonitor.Api.Location.Services
{
	public class PollingStationService : IPollingStationService
	{
		private readonly VoteMonitorContext _context;
		private readonly ICacheService _cacheService;
		private readonly ILogger _logger;

		public PollingStationService(VoteMonitorContext context, ICacheService cacheService, ILogger<PollingStationService> logger)
		{
			_context = context;
			_cacheService = cacheService;
			_logger = logger;
		}

		public async Task<int> GetPollingStationByCountyCode(int pollingStationNumber, string countyCode)
		{
			try
			{
				var cacheKey = $"polling-station-countyCode-{pollingStationNumber}-{countyCode}";

				return await _cacheService.GetOrSaveDataInCacheAsync(cacheKey, async () =>
				 {
					 var countyId = _context.Counties.FirstOrDefault(c => c.Code == countyCode)?.Id;
					 if (countyId == null)
						 throw new ArgumentException($"Could not find County with code: {countyCode}");

					 return await GetPollingStationByCountyId(pollingStationNumber, countyId.Value);
				 });
			}
			catch (Exception ex)
			{
				_logger.LogError(new EventId(), ex.Message);
			}

			return -1;
		}

		public async Task<int> GetPollingStationByCountyId(int pollingStationNumber, int countyId)
		{
			try
			{
				var cacheKey = $"polling-station-{pollingStationNumber}-{countyId}";
				return await _cacheService.GetOrSaveDataInCacheAsync<int>(cacheKey, async () =>
				 {
					 var idSectie = await
					 _context.PollingStations
						 .Where(a => a.IdCounty == countyId && a.Number == pollingStationNumber)
						 .Select(a => a.Id).ToListAsync();

					 if (idSectie.Count == 0)
						 throw new ArgumentException($"No Polling station found for: {new { countyId, pollingStationNumber }}");


					 if (idSectie.Count > 1) // TODO[bv] add unique constraint on PollingStations [CountyId, Number]
						 throw new ArgumentException($"More than one polling station found for: {new { countyId, idSectie }}");

					 return idSectie.Single();
				 });
			}
			catch (Exception ex)
			{
				_logger.LogError(new EventId(), ex.Message);
			}

			return -1;
		}

		public async Task<IEnumerable<CountyPollingStationLimit>> GetPollingStationsAssignmentsForAllCounties(bool? diaspora)
		{
			var cacheKey = "all-polling-stations";

			if (diaspora.HasValue)
			{
				cacheKey = $"polling-station-diaspora-{diaspora.Value}";
			}

			var data = await _cacheService
				 .GetOrSaveDataInCacheAsync(cacheKey, async () => await _context.Counties
					 .Where(c => diaspora == null || c.Diaspora == diaspora)
					 .OrderBy(c => c.Order)
					 .Select(c => new CountyPollingStationLimit
					 {
						 Name = c.Name,
						 Code = c.Code,
						 Limit = c.NumberOfPollingStations,
						 Id = c.Id,
						 Diaspora = c.Diaspora,
						 Order = c.Order
					 }).ToListAsync());

			return data;
		}

		public async Task<int> ClearAll(CancellationToken cancellationToken)
		{
			try
			{
				int result = 0;
				using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
				{
					//if there are any performance issues, DELETE can be changed to TRUNCATE - but then we need to tackle a way to 
					//bypass foreign key relationship while removing data
					await _context.Database.ExecuteSqlRawAsync($"DELETE FROM {nameof(_context.Answers)}"
						, cancellationToken);
					await _context.Database.ExecuteSqlRawAsync($"DELETE FROM {nameof(_context.Notes)}"
						, cancellationToken);
					await _context.Database.ExecuteSqlRawAsync($"DELETE FROM {nameof(_context.PollingStationInfos)}"
						, cancellationToken);
					await _context.Database.ExecuteSqlRawAsync($"UPDATE {nameof(_context.Counties)} SET {nameof(County.NumberOfPollingStations)} = 0"
						, cancellationToken);
					result = await _context.Database.ExecuteSqlRawAsync($"DELETE FROM {nameof(_context.PollingStations)}"
						, cancellationToken);

					await transaction.CommitAsync(cancellationToken);

					return result;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError("Error while removing polling stations.", ex);
			}

			return -1;
		}
	}
}
