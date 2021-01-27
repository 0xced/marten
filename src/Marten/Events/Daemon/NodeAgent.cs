using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline.Dates;
using Marten.Events.Daemon.HighWater;
using Marten.Events.Daemon.Progress;
using Marten.Events.Projections;
using Microsoft.Extensions.Logging;

namespace Marten.Events.Daemon
{
    // TODO -- implement IAsyncDisposable
    public class NodeAgent : IDaemon, IDisposable
    {
        private readonly DocumentStore _store;
        private readonly ILogger<IProjection> _logger;
        private readonly Dictionary<string, ProjectionAgent> _agents = new Dictionary<string, ProjectionAgent>();
        private readonly CancellationTokenSource _cancellation;
        private readonly HighWaterAgent _highWater;
        private bool _hasStarted;

        // ReSharper disable once ContextualLoggerProblem
        public NodeAgent(DocumentStore store, ILogger<IProjection> logger)
        {
            _cancellation = new CancellationTokenSource();
            _store = store;
            _logger = logger;
            var detector = new HighWaterDetector(store.Tenancy.Default, store.Events);

            Tracker = new ShardStateTracker();
            _highWater = new HighWaterAgent(detector, Tracker, logger, store.Events.Daemon, _cancellation.Token);

        }

        public ShardStateTracker Tracker { get; }

        // TODO -- only start the high water when there's anything to start!
        public void StartNode()
        {
            _store.Tenancy.Default.EnsureStorageExists(typeof(IEvent));
            _highWater.Start();
            _hasStarted = true;
        }

        public async Task StartAll()
        {
            if (!_hasStarted) StartNode();
            var shards = _store.Events.Projections.AllShards();
            foreach (var shard in shards)
            {
                await StartShard(shard, _cancellation.Token);
            }

        }

        public async Task StartShard(string shardName, CancellationToken token)
        {
            // Latch it so it doesn't double start
            if (_agents.ContainsKey(shardName)) return;

            if (_store.Events.Projections.TryFindAsyncShard(shardName, out var shard))
            {
                await StartShard(shard, token);
            }
        }

        public async Task StartShard(IAsyncProjectionShard shard, CancellationToken cancellationToken)
        {
            if (!_hasStarted) StartNode();

            // TODO -- log the start, or error if it fails
            var agent = new ProjectionAgent(_store, shard, _logger, cancellationToken);
            var position = await agent.Start(Tracker);

            Tracker.Publish(new ShardState(shard.Name, position){Action = ShardAction.Started});

            _agents[shard.Name.Identity] = agent;


        }

        // TODO -- if all the shards are stopped, stop the high water agent
        public async Task StopShard(string shardName)
        {
            if (_agents.TryGetValue(shardName, out var agent))
            {
                await agent.Stop();
                _agents.Remove(shardName);

                Tracker.Publish(new ShardState(shardName, agent.Position){Action = ShardAction.Stopped});

            }
        }

        public async Task StopAll()
        {
            // TODO -- stop the high water checking??
            foreach (var agent in _agents.Values)
            {
                await agent.Stop();
            }

            _agents.Clear();


        }

        public void Dispose()
        {
            Tracker?.Dispose();
            _cancellation?.Dispose();
            _highWater?.Dispose();
        }


        public Task RebuildProjection(string projectionName, CancellationToken token)
        {
            if (!_store.Events.Projections.TryFindProjection(projectionName, out var projection))
            {
                throw new ArgumentOutOfRangeException(nameof(projectionName),
                    $"No registered projection matches that name");
            }

            return RebuildProjection(projection, token);

        }



        internal async Task RebuildProjection(ProjectionSource source, CancellationToken token)
        {
            _logger.LogInformation($"Starting to rebuild Projection {source.ProjectionName}");

            var running = _agents.Values.Where(x => x.ShardName.ProjectionName == source.ProjectionName).ToArray();
            foreach (var agent in running)
            {
                await agent.Stop();
            }

            if (token.IsCancellationRequested) return;

            if (Tracker.HighWaterMark == 0)
            {
                await _highWater.CheckNow();
            }

            if (token.IsCancellationRequested) return;

            var shards = source.AsyncProjectionShards(_store);

            // Teardown the current state
            await using (var session = _store.LightweightSession())
            {
                source.Options.Teardown(session);

                foreach (var shard in shards)
                {
                    session.QueueOperation(new DeleteProjectionProgress(_store.Events, shard.Name.Identity));
                }

                await session.SaveChangesAsync(token);
            }

            if (token.IsCancellationRequested) return;


            var waiters = shards.Select(async x =>
            {
                await StartShard(x, token);

                // TODO -- need to watch the CancellationToken here!!!!
                return Tracker.WaitForShardState(x.Name, Tracker.HighWaterMark, 5.Minutes());
            });

            await Task.WhenAll(waiters);

            foreach (var shard in shards)
            {
                await StopShard(shard.Name.Identity);
            }
        }

    }
}
