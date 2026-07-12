using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lakerfield.Reactive;
using Lakerfield.Runtime;
using LakerProxy.ProxyApp.ApiExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Entity = LakerProxy.Models.Log;
using ApiEntity = LakerProxy.WebApi.Models.LogEntry;
using ApiError = LakerProxy.WebApi.Models.ApiError;

namespace LakerProxy.ProxyApp.Controllers
{
#if DEBUG
  [Asp.Versioning.ApiVersion("1")]
#endif
  [ApiController]
  [Route("api/v1/log")]
  public class LogController : AWebApiControllerBase
  {
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DefaultHistoryWindow = TimeSpan.FromHours(24);

    public LogController(AppServices services) : base(services)
    {
    }


    // GET: api/v1/log
    [HttpGet("", Name = RouteNames.LogGet)]
    [ProducesResponseType(typeof(IEnumerable<ApiEntity>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(DateTime? dateFrom = null, DateTime? dateTill = null, int take = 200)
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleFullAccess);
      if (!success)
        return error;
      try
      {
        var logs = await Services.Log().FindBy(new Entity.Filter()
        {
          DateFrom = dateFrom ?? DateTime.Now.Subtract(DefaultHistoryWindow),
          DateTill = dateTill,
        });

        return Ok(logs
          .OrderBy(l => l.Date)
          .TakeLast(Math.Clamp(take, 1, 10_000))
          .Select(l => l.ToWeb()));
      }
      catch (Exception e)
      {
        return e.ToActionResult();
      }
    }


    // GET: api/v1/log/stream (server-sent events)
    [HttpGet("stream", Name = RouteNames.LogStream)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Stream(int history = 100)
    {
      var (success, user, error) = await GetUser(ApiUserRights.RoleFullAccess);
      if (!success)
        return error;

      var ct = HttpContext.RequestAborted;

      Response.ContentType = "text/event-stream";
      Response.Headers.CacheControl = "no-cache";
      Response.Headers["X-Accel-Buffering"] = "no";

      // Slow consumers drop the oldest events instead of blocking the change stream
      var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
      {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
      });

      // Subscribe before sending history so no event can fall in the gap;
      // overlapping entries are deduplicated client-side on Id
      var observable = Services.Log().Observe(new Entity.Filter());
      using var subscription = observable.SubscribeAsync(
        async x =>
        {
          switch (x)
          {
            case DataChange<Entity> change:
              channel.Writer.TryWrite(BuildEventFrame(change.Entity));
              break;
            case DataLive<Entity>:
              channel.Writer.TryWrite(": live\n\n");
              break;
          }
        },
        exception => channel.Writer.TryComplete(exception),
        () => channel.Writer.TryComplete(),
        TaskPoolScheduler.Default);

      try
      {
        if (history > 0)
        {
          var logs = await Services.Log().FindBy(new Entity.Filter()
          {
            DateFrom = DateTime.Now.Subtract(DefaultHistoryWindow),
          });
          foreach (var log in logs.OrderBy(l => l.Date).TakeLast(Math.Clamp(history, 1, 1000)))
            await Response.WriteAsync(BuildEventFrame(log), ct);
          await Response.Body.FlushAsync(ct);
        }

        while (!ct.IsCancellationRequested)
        {
          var readTask = channel.Reader.WaitToReadAsync(ct).AsTask();
          var winner = await Task.WhenAny(readTask, Task.Delay(HeartbeatInterval, ct));
          if (winner != readTask)
          {
            await Response.WriteAsync(": keep-alive\n\n", ct);
            await Response.Body.FlushAsync(ct);
            continue;
          }

          if (!await readTask)
            break;

          while (channel.Reader.TryRead(out var frame))
            await Response.WriteAsync(frame, ct);
          await Response.Body.FlushAsync(ct);
        }
      }
      catch (OperationCanceledException)
      {
        // client disconnected
      }

      return new EmptyResult();
    }


    private static string BuildEventFrame(Entity log)
    {
      var json = JsonSerializer.Serialize(log.ToWeb(), JsonOptions);
      return $"id: {log.Id}\ndata: {json}\n\n";
    }

  }
}
