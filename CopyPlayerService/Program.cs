using EasyNetQ;
using Events;
using Helpers;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace CopyPlayerService;

public static class Program
{
    private static readonly IPlayer Player = new CopyPlayer();
    
    public static async Task Main()
    {
        var connectionEstablished = false;

        while (!connectionEstablished)
        {
            var bus = ConnectionHelper.GetRMQConnection();
            await bus.Rpc.RespondAsync<ServiceBRequest, ServiceBResponse>(req =>
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

            var subscriptionResult = bus.PubSub.SubscribeAsync<GameStartedEvent>("RPS_" + Player.GetPlayerId(), e =>
            {
                var moveEvent = Player.MakeMove(e);
                bus.PubSub.PublishAsync(moveEvent);
            }).AsTask();

            await subscriptionResult.WaitAsync(CancellationToken.None);
            connectionEstablished = subscriptionResult.Status == TaskStatus.RanToCompletion;
            if(!connectionEstablished) Thread.Sleep(1000);
            
            await bus.PubSub.SubscribeAsync<GameFinishedEvent>("RPS_" + Player.GetPlayerId(), e =>
            {
                Player.ReceiveResult(e);
            });
        }

        while (true) Thread.Sleep(5000);
    }
}