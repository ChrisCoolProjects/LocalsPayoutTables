using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

/// <summary>
/// GET /api/tournaments — fetches recent Quickdraw Brawl results from start.gg.
/// The start.gg token lives only on the server (STARTGG_TOKEN app setting), never in the browser.
/// The query is fixed here so the endpoint can't be used to run arbitrary queries with our token.
/// </summary>
public class Tournaments(ILogger<Tournaments> logger)
{
    private const string Query = @"
        query TournamentEventPlacements($name: String!, $perPageT: Int, $perPageS: Int, $page: Int) {
          tournaments(query: {perPage: $perPageT, filter: {name: $name}}) {
            nodes {
              name
              events {
                name
                numEntrants
                standings(query: {perPage: $perPageS, page: $page}) {
                  nodes {
                    placement
                    player {
                      id
                      gamerTag
                    }
                  }
                }
              }
            }
          }
        }";

    private static readonly string RequestBody = JsonSerializer.Serialize(new
    {
        query = Query,
        variables = new { name = "Quickdraw", perPageS = 8, perPageT = 13, page = 1 }
    });

    // Results only change weekly, so serve a cached copy for a few minutes
    // instead of calling start.gg (rate limited per token) on every page load.
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static readonly HttpClient StartGG = new();
    private static string? cachedJson;
    private static DateTime cachedAtUtc;

    [Function("tournaments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tournaments")] HttpRequestData req)
    {
        var json = await GetResultsAsync();
        if (json == null)
        {
            return req.CreateResponse(HttpStatusCode.BadGateway);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.Headers.Add("Cache-Control", $"public, max-age={(int)CacheFor.TotalSeconds}");
        await response.WriteStringAsync(json);
        return response;
    }

    private async Task<string?> GetResultsAsync()
    {
        await CacheLock.WaitAsync();
        try
        {
            if (cachedJson != null && DateTime.UtcNow - cachedAtUtc < CacheFor)
            {
                return cachedJson;
            }

            // Accept the token with or without a leading "Bearer ".
            var token = Environment.GetEnvironmentVariable("STARTGG_TOKEN")?.Trim();
            if (token?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true) token = token[7..].Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogError("STARTGG_TOKEN app setting is not configured.");
                return cachedJson;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.start.gg/gql/alpha")
            {
                Content = new StringContent(RequestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Authorization", $"Bearer {token}");

            using var startggResponse = await StartGG.SendAsync(request);
            var body = await startggResponse.Content.ReadAsStringAsync();

            // start.gg can answer 200 with only an "errors" array (e.g. query too complex), so check for data too.
            if (!startggResponse.IsSuccessStatusCode || !HasData(body))
            {
                logger.LogError("start.gg request failed ({Status}): {Body}", (int)startggResponse.StatusCode, body);
                return cachedJson; // fall back to the last good copy, if any
            }

            cachedJson = body;
            cachedAtUtc = DateTime.UtcNow;
            return cachedJson;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Calling start.gg failed.");
            return cachedJson;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static bool HasData(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
