using EasyNetQ;
using Events;
using Helpers;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Serilog;
using System.Diagnostics;

namespace CopyPlayerService;

public class CopyPlayer : IPlayer
{
    private const string PlayerId = "The Copy Cat";
    private readonly Queue<Move> _previousMoves = new Queue<Move>();

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

        Move move = Move.Paper;
        if (_previousMoves.Count > 2)
        {
            move = _previousMoves.Dequeue();
        }
        Log.Logger.Debug("Player {PlayerId} has decided to perform the move {Move}", PlayerId, move);

        var moveEvent = new PlayerMovedEvent
        {
            GameId = e.GameId,
            PlayerId = PlayerId,
            Move = move
        };
        return moveEvent;
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

        var otherMove = e.Moves.SingleOrDefault(m => m.Key != PlayerId).Value;
        Log.Logger.Debug("Received result from game {GameId} - other player played {Move} queue now has {QueueSize} elements", e.GameId, otherMove, _previousMoves.Count);
        _previousMoves.Enqueue(otherMove);
    }

    public string GetPlayerId()
    {
        return PlayerId;
    }
}