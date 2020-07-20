using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using Baseline;
using Baseline.Dates;
using LamarCodeGeneration;
using Marten.Linq;
using Marten.Schema.Arguments;
using Marten.Util;
using Marten.V4Internals.Linq;
using Marten.V4Internals.Linq.Includes;
using Npgsql;

namespace Marten.V4Internals.Compiled
{
    public class QueryCompiler
    {
        internal static readonly IList<IParameterFinder> Finders = new List<IParameterFinder>{new EnumParameterFinder()};

        private static void forType<T>(Func<int, T[]> uniqueValues)
        {
            var finder = new SimpleParameterFinder<T>(uniqueValues);
            Finders.Add(finder);
        }

        static QueryCompiler()
        {
            forType(count =>
            {
                var values = new string[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Guid.NewGuid().ToString();
                }

                return values;
            });

            forType(count =>
            {
                var values = new Guid[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Guid.NewGuid();
                }

                return values;
            });

            forType(count =>
            {
                var value = -100000;
                var values = new int[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value--;
                }

                return values;
            });

            forType(count =>
            {
                var value = -200000L;
                var values = new long[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value--;
                }

                return values;
            });

            forType(count =>
            {
                var value = -300000L;
                var values = new float[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value--;
                }

                return values;
            });

            forType(count =>
            {
                var value = -300000L;
                var values = new decimal[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value--;
                }

                return values;
            });

            forType(count =>
            {
                var value = new DateTime(1600, 1, 1);
                var values = new DateTime[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value.AddDays(-1);
                }

                return values;
            });

            forType(count =>
            {
                var value = new DateTimeOffset(1600, 1, 1, 0, 0, 0, 0.Seconds());
                var values = new DateTimeOffset[count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value.AddDays(-1);
                }

                return values;
            });
        }



        public static CompiledQueryPlan BuildPlan<TDoc, TOut>(IMartenSession session, ICompiledQuery<TDoc, TOut> query,
            StoreOptions storeOptions)
        {
            eliminateStringNulls(query);

            var plan = new CompiledQueryPlan(query.GetType(), typeof(TOut));
            findIncludes(session, query, plan);
            plan.FindMembers();

            assertValidityOfQueryType(plan, query.GetType());

            // This *could* throw
            var queryTemplate = plan.CreateQueryTemplate(query);

            var statistics = plan.GetStatisticsIfAny(query);
            var builder = BuildDatabaseCommand(session, queryTemplate, statistics, plan, out var command);



            plan.HandlerPrototype = builder.BuildHandler<TOut>(statistics, new List<IIncludePlan>());

            plan.ReadCommand(command, storeOptions);

            return plan;
        }

        public static LinqHandlerBuilder BuildDatabaseCommand<TDoc, TOut>(IMartenSession session,
            ICompiledQuery<TDoc, TOut> queryTemplate,
            QueryStatistics statistics,
            CompiledQueryPlan plan,
            out NpgsqlCommand command)
        {
            Expression expression = queryTemplate.QueryIs();
            var invocation = Expression.Invoke(expression, Expression.Parameter(typeof(IMartenQueryable<TDoc>)));

            var builder = new LinqHandlerBuilder(session, invocation, forCompiled: true);

            command = builder.BuildDatabaseCommand(statistics, plan.IncludePlans);

            return builder;
        }

        private static void eliminateStringNulls(object query)
        {
            var type = query.GetType();

            foreach (var propertyInfo in type.GetProperties().Where(x => x.CanWrite && x.PropertyType == typeof(string)))
            {
                var raw = propertyInfo.GetValue(query);
                if (raw == null)
                {
                    propertyInfo.SetValue(query, string.Empty);
                }
            }

            foreach (var fieldInfo in type.GetFields().Where(x => x.FieldType == typeof(string)))
            {
                var raw = fieldInfo.GetValue(query);
                if (raw == null)
                {
                    fieldInfo.SetValue(query, string.Empty);
                }
            }
        }


        private static void findIncludes<TDoc, TOut>(IMartenSession session, ICompiledQuery<TDoc, TOut> query, CompiledQueryPlan plan)
        {
            var expression = query.QueryIs();
            var invocation = Expression.Invoke(expression, Expression.Parameter(typeof(IMartenQueryable<TDoc>)));

            var includeVisitor = new CompiledQueryExpressionVisitor<TDoc>(session, plan);
            includeVisitor.Visit(invocation.Expression);
        }

        private static void assertValidityOfQueryType(CompiledQueryPlan plan, Type type)
        {
            if (plan.InvalidMembers.Any())
            {
                var members = plan.InvalidMembers.Select(x => $"{x.GetRawMemberType().NameInCode()} {x.Name}")
                    .Join(", ");
                var message = $"Members {members} cannot be used as parameters to a compiled query";

                // TODO -- use a specific Marten exception here
                throw new InvalidOperationException(message);
            }
        }

    }
}
