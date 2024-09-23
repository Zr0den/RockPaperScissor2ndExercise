using EasyNetQ;
using Events;
using Helpers;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Serilog;
using System.Diagnostics;

namespace Monolith;

public class RandomPlayer : IPlayer
{
    private const string PlayerId = "Mr. Random";

    public PlayerMovedEvent MakeMove(GameStartedEvent e)
    {
        var bus = ConnectionHelper.GetRMQConnection();
        bus.Rpc.RespondAsync<ServiceBRequest, ServiceBResponse>(req =>
        {
            var propagator = new TraceContextPropagator();
            var parentContext = propagator.Extract(default, req, (r, key) =>
            {
                return new List<string>(new[] { r.Header.ContainsKey(key) ? r.Header[key].ToString() : String.Empty });
            });

            Baggage.Current = parentContext.Baggage;
            using var activity = Monitoring.ActivitySource.StartActivity("Game", ActivityKind.Consumer, parentContext.ActivityContext);
            return new ServiceBResponse();
        });

        var random = new Random();
        var next = random.Next(3);
        var move = next switch
        {
            0 => Move.Rock,
            1 => Move.Paper,
            _ => Move.Scissor
        };

        Log.Logger.Debug("Player {PlayerId} has decided to perform the move {Move}", PlayerId, move);
        return new PlayerMovedEvent
        {
            GameId = e.GameId,
            PlayerId = PlayerId,
            Move = move
        };
    }

    public void ReceiveResult(GameFinishedEvent e)
    {
        var bus = ConnectionHelper.GetRMQConnection();
        bus.Rpc.RespondAsync<ServiceBRequest, ServiceBResponse>(req =>
        {
            var propagator = new TraceContextPropagator();
            var parentContext = propagator.Extract(default, req, (r, key) =>
            {
                return new List<string>(new[] { r.Header.ContainsKey(key) ? r.Header[key].ToString() : String.Empty });
            });

            Baggage.Current = parentContext.Baggage;
            using var activity = Monitoring.ActivitySource.StartActivity("Game", ActivityKind.Consumer, parentContext.ActivityContext);
            return new ServiceBResponse();
        });
    }

    public string GetPlayerId()
    {
        return PlayerId;
    }
}

