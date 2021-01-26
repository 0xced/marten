using System;
using System.Collections.Generic;
using System.Linq;
using Baseline;
using ImTools;
using Marten.Events.Aggregation;
using Marten.Events.Daemon;
using Marten.Exceptions;

namespace Marten.Events.Projections
{
    /// <summary>
    /// Used to register projections with Marten
    /// </summary>
    public class ProjectionCollection
    {
        private readonly StoreOptions _options;
        private readonly Dictionary<Type, object> _liveAggregateSources = new Dictionary<Type, object>();
        private ImHashMap<Type, object> _liveAggregators = ImHashMap<Type, object>.Empty;

        private readonly IList<ProjectionSource> _projections = new List<ProjectionSource>();

        private Lazy<Dictionary<string, IAsyncProjectionShard>> _asyncShards;

        internal ProjectionCollection(StoreOptions options)
        {
            _options = options;
        }

        internal IEnumerable<Type> AllAggregateTypes()
        {
            foreach (var kv in _liveAggregators.Enumerate())
            {
                yield return kv.Key;
            }

            foreach (var projection in _projections.OfType<IAggregateProjection>())
            {
                yield return projection.AggregateType;
            }
        }

        internal IProjection[] BuildInlineProjections(DocumentStore store)
        {
            return _projections.Where(x => x.Lifecycle == ProjectionLifecycle.Inline).Select(x => x.Build(store)).ToArray();
        }


        /// <summary>
        /// Add a projection to be executed
        /// </summary>
        /// <param name="projection">Value values are Inline/Async, The default is Inline</param>
        public void Add(IProjection projection, ProjectionLifecycle lifecycle = ProjectionLifecycle.Inline)
        {
            if (lifecycle == ProjectionLifecycle.Live)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycle),
                    $"{nameof(ProjectionLifecycle.Live)} cannot be used for IProjection");
            }

            var wrapper = new ProjectionWrapper(projection, lifecycle);
            _projections.Add(wrapper);
        }

        /// <summary>
        /// Add a projection that will be executed inline
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="lifecycle">Optionally override the lifecycle of this projection. The default is Inline</param>
        public void Add(EventProjection projection, ProjectionLifecycle? lifecycle = null)
        {
            if (lifecycle.HasValue)
            {
                projection.Lifecycle = lifecycle.Value;
            }
            projection.AssertValidity();
            _projections.Add(projection);
        }

        /// <summary>
        /// Use a "self-aggregating" aggregate of type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lifecycle">Override the aggregate lifecycle. The default is Inline</param>
        /// <returns>The extended storage configuration for document T</returns>
        public MartenRegistry.DocumentMappingExpression<T> SelfAggregate<T>(ProjectionLifecycle? lifecycle = null)
        {
            // Make sure there's a DocumentMapping for the aggregate
            var expression = _options.Schema.For<T>();

            var source = new AggregateProjection<T>()
            {
                Lifecycle = lifecycle ?? ProjectionLifecycle.Inline
            };
            source.AssertValidity();
            _projections.Add(source);

            return expression;
        }

        /// <summary>
        /// Register an aggregate projection that should be evaluated inline
        /// </summary>
        /// <param name="projection"></param>
        /// <typeparam name="T"></typeparam>
        /// <param name="lifecycle">Optionally override the ProjectionLifecycle</param>
        /// <returns>The extended storage configuration for document T</returns>
        public MartenRegistry.DocumentMappingExpression<T> Add<T>(AggregateProjection<T> projection, ProjectionLifecycle? lifecycle = null)
        {
            var expression = _options.Schema.For<T>();
            if (lifecycle.HasValue)
            {
                projection.Lifecycle = lifecycle.Value;
            }

            projection.AssertValidity();
            _projections.Add(projection);

            return expression;
        }

        internal bool Any()
        {
            return _projections.Any();
        }

        internal ILiveAggregator<T> AggregatorFor<T>() where T : class
        {
            if (_liveAggregators.TryFind(typeof(T), out var aggregator))
            {
                return (ILiveAggregator<T>) aggregator;
            }

            if (!_liveAggregateSources.TryGetValue(typeof(T), out var source))
            {
                source = new AggregateProjection<T>();
                source.As<ProjectionSource>().AssertValidity();
            }

            aggregator = source.As<ILiveAggregatorSource<T>>().Build(_options);
            _liveAggregators = _liveAggregators.AddOrUpdate(typeof(T), aggregator);

            return (ILiveAggregator<T>) aggregator;
        }

        internal void AssertValidity(DocumentStore store)
        {
            var messages = _projections.Concat(_liveAggregateSources.Values)
                .OfType<ProjectionSource>()
                .Distinct()
                .SelectMany(x => x.ValidateConfiguration(_options))
                .ToArray();

            _asyncShards = new Lazy<Dictionary<string, IAsyncProjectionShard>>(() =>
            {
                return _projections
                    .Where(x => x.Lifecycle == ProjectionLifecycle.Async)
                    .SelectMany(x => x.AsyncProjectionShards(store, store.Tenancy))
                    .ToDictionary(x => x.ProjectionOrShardName);

            });

            if (messages.Any())
            {
                throw new InvalidProjectionException(messages);
            }
        }

        internal IReadOnlyList<IAsyncProjectionShard> AllShards()
        {
            return _asyncShards.Value.Values.ToList();
        }

        internal bool TryFindAsyncShard(string projectionOrShardName, out IAsyncProjectionShard shard)
        {
            return _asyncShards.Value.TryGetValue(projectionOrShardName, out shard);
        }

        internal bool TryFindProjection(string projectionName, out ProjectionSource source)
        {
            if (_projections.FirstOrDefault(x => x.ProjectionName == projectionName) == null)
            {
                source = null;
                return false;
            }

            source = _projections.FirstOrDefault(x => x.ProjectionName == projectionName);
            return true;
        }


    }
}
