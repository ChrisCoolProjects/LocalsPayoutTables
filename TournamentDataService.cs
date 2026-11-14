using DLLTesting.StartGGLibrary;
using Newtonsoft.Json;
using static DLLTesting.PayoutCalculator;

namespace LocalsPayoutTables
{
	/// <summary>
	/// Loads Quickdraw Brawl results from our API (api/tournaments) once per visit and shares them between pages.
	/// Pages just await these methods: the first caller starts the request, everyone else awaits the same one,
	/// so there's nothing to time with delays and no hand-off through session storage.
	/// </summary>
	public class TournamentDataService(HttpClient http)
	{
		private Task<TournamentResponse>? tournamentsTask;
		private Task<List<Entrant>>? leaderboardTask;

		public Task<TournamentResponse> GetTournamentsAsync()
		{
			// Retry on the next call if the previous attempt failed.
			if (tournamentsTask == null || tournamentsTask.IsFaulted)
			{
				tournamentsTask = LoadTournamentsAsync();
			}
			return tournamentsTask;
		}

		public Task<List<Entrant>> GetLeaderboardAsync()
		{
			if (leaderboardTask == null || leaderboardTask.IsFaulted)
			{
				leaderboardTask = BuildLeaderboardAsync();
			}
			return leaderboardTask;
		}

		private async Task<TournamentResponse> LoadTournamentsAsync()
		{
			var json = await http.GetStringAsync("api/tournaments");
			return JsonConvert.DeserializeObject<TournamentResponse>(json)
				?? throw new InvalidOperationException("api/tournaments returned no data.");
		}

		private async Task<List<Entrant>> BuildLeaderboardAsync()
		{
			var data = await GetTournamentsAsync();
			var leaderboard = new Dictionary<int, Entrant>();
			foreach (var eventItem in data.Data.Tournaments.Nodes.SelectMany(t => t.Events))
			{
				// Same main-games filter as the Home tables (side events like 2v2 brackets have no Player on their standings)
				if (!IsMainEvent(eventItem) || eventItem.NumEntrants < 4) continue;
				var payoutList = CalcPayout(eventItem.NumEntrants);
				for (int i = 0; i < payoutList.Count && i < eventItem.Standings.Nodes.Count; i++)
				{
					var player = eventItem.Standings.Nodes[i]?.Player;
					if (player == null) continue;
					if (!leaderboard.TryGetValue(player.Id, out var entrant))
					{
						entrant = new Entrant { Id = player.Id, Name = player.gamerTag };
						leaderboard.Add(player.Id, entrant);
					}
					entrant.Earnings += payoutList[i];
				}
			}
			return leaderboard.Values.OrderByDescending(x => x.Earnings).ToList();
		}
	}
}
