﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Marten.Linq.MatchesSql;
using Marten.Testing.Documents;
using Marten.Testing.Harness;
using Shouldly;
using Xunit;

namespace Marten.Testing.CoreFunctionality
{
    public class query_by_sql_where_clause_Tests: IntegrationContext
    {
        public query_by_sql_where_clause_Tests(DefaultStoreFixture fixture): base(fixture)
        {
        }

        [Fact]
        public void query_by_string_scalar()
        {
        }

        [Fact]
        public async Task stream_query_by_one_parameter()
        {
            using var session = theStore.OpenSession();
            session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
            session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
            session.Store(new User {FirstName = "Max", LastName = "Miller"});
            session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
            await session.SaveChangesAsync();

            var stream = new MemoryStream();
            await session.StreamJson<User>(stream, "where data ->> 'LastName' = ?", "Miller");

            stream.Position = 0;
            var results = theStore.Options.Serializer().FromJson<User[]>(stream);
            var firstnames = results
                .OrderBy(x => x.FirstName)
                .Select(x => x.FirstName).ToArray();

            firstnames.Length.ShouldBe(3);
            firstnames[0].ShouldBe("Jeremy");
            firstnames[1].ShouldBe("Lindsey");
            firstnames[2].ShouldBe("Max");
        }

        [Fact]
        public void query_by_one_parameter()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                var firstnames =
                    session.Query<User>("where data ->> 'LastName' = ?", "Miller").OrderBy(x => x.FirstName)
                        .Select(x => x.FirstName).ToArray();

                firstnames.Length.ShouldBe(3);
                firstnames[0].ShouldBe("Jeremy");
                firstnames[1].ShouldBe("Lindsey");
                firstnames[2].ShouldBe("Max");
            }
        }

        [Fact]
        public void query_ignores_case_of_where_keyword()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                var firstnames =
                    session.Query<User>("WHERE data ->> 'LastName' = ?", "Miller").OrderBy(x => x.FirstName)
                        .Select(x => x.FirstName).ToArray();

                firstnames.Length.ShouldBe(3);
                firstnames[0].ShouldBe("Jeremy");
                firstnames[1].ShouldBe("Lindsey");
                firstnames[2].ShouldBe("Max");
            }
        }

        [Fact]
        public void query_by_one_named_parameter()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                var firstnames =
                    session.Query<User>("where data ->> 'LastName' = :Name", new {Name = "Miller"})
                        .OrderBy(x => x.FirstName)
                        .Select(x => x.FirstName).ToArray();

                firstnames.Length.ShouldBe(3);
                firstnames[0].ShouldBe("Jeremy");
                firstnames[1].ShouldBe("Lindsey");
                firstnames[2].ShouldBe("Max");
            }
        }

        [Fact]
        public void query_by_two_parameters()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                #region sample_using_parameterized_sql

                var user =
                    session.Query<User>("where data ->> 'FirstName' = ? and data ->> 'LastName' = ?", "Jeremy",
                            "Miller")
                        .Single();

                #endregion

                user.ShouldNotBeNull();
            }
        }

        #region sample_query_by_two_named_parameters

        [Fact]
        public void query_by_two_named_parameters()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();
                var user =
                    session.Query<User>("where data ->> 'FirstName' = :FirstName and data ->> 'LastName' = :LastName",
                            new {FirstName = "Jeremy", LastName = "Miller"})
                        .Single();

                SpecificationExtensions.ShouldNotBeNull(user);
            }
        }

        #endregion

        [Fact]
        public void query_two_fields_by_one_named_parameter()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();
                var user =
                    session.Query<User>("where data ->> 'FirstName' = :Name or data ->> 'LastName' = :Name",
                            new {Name = "Jeremy"})
                        .Single();

                SpecificationExtensions.ShouldNotBeNull(user);
            }
        }

        [Fact]
        public void query_for_multiple_documents()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                var firstnames =
                    session.Query<User>("where data ->> 'LastName' = 'Miller'").OrderBy(x => x.FirstName)
                        .Select(x => x.FirstName).ToArray();

                firstnames.Length.ShouldBe(3);
                firstnames[0].ShouldBe("Jeremy");
                firstnames[1].ShouldBe("Lindsey");
                firstnames[2].ShouldBe("Max");
            }
        }


        [Fact]
        public void query_for_multiple_documents_with_ordering()
        {
            using (var session = theStore.OpenSession())
            {
                session.Store(new User {FirstName = "Jeremy", LastName = "Miller"});
                session.Store(new User {FirstName = "Lindsey", LastName = "Miller"});
                session.Store(new User {FirstName = "Max", LastName = "Miller"});
                session.Store(new User {FirstName = "Frank", LastName = "Zombo"});
                session.SaveChanges();

                var firstnames =
                    session.Query<User>("where data ->> 'LastName' = 'Miller' order by data ->> 'FirstName'")
                        .Select(x => x.FirstName).ToArray();

                firstnames.Length.ShouldBe(3);
                firstnames[0].ShouldBe("Jeremy");
                firstnames[1].ShouldBe("Lindsey");
                firstnames[2].ShouldBe("Max");
            }
        }


        #region sample_query_with_only_the_where_clause

        [Fact]
        public void query_for_single_document()
        {
            using (var session = theStore.OpenSession())
            {
                var u = new User {FirstName = "Jeremy", LastName = "Miller"};
                session.Store(u);
                session.SaveChanges();

                var user = session.Query<User>("where data ->> 'FirstName' = 'Jeremy'").Single();
                user.LastName.ShouldBe("Miller");
                user.Id.ShouldBe(u.Id);
            }
        }

        #endregion

        [Fact]
        public void query_for_single_document_where_clause_trimmed()
        {
            using (var session = theStore.OpenSession())
            {
                var u = new User {FirstName = "Jeremy", LastName = "Miller"};
                session.Store(u);
                session.SaveChanges();

                var user = session.Query<User>(@"
where data ->> 'FirstName' = 'Jeremy'").Single();
                user.LastName.ShouldBe("Miller");
                user.Id.ShouldBe(u.Id);
            }
        }

        #region sample_query_with_matches_sql

        [Fact]
        public void query_with_matches_sql()
        {
            using (var session = theStore.OpenSession())
            {
                var u = new User {FirstName = "Eric", LastName = "Smith"};
                session.Store(u);
                session.SaveChanges();

                var user = session.Query<User>().Where(x => x.MatchesSql("data->> 'FirstName' = ?", "Eric")).Single();
                user.LastName.ShouldBe("Smith");
                user.Id.ShouldBe(u.Id);
            }
        }

        #endregion

        [Fact]
        public void query_with_select_in_query()
        {
            using (var session = theStore.OpenSession())
            {
                var u = new User {FirstName = "Jeremy", LastName = "Miller"};
                session.Store(u);
                session.SaveChanges();

                #region sample_use_all_your_own_sql

                var user =
                    session.Query<User>("select data from mt_doc_user where data ->> 'FirstName' = 'Jeremy'")
                        .Single();

                #endregion

                user.LastName.ShouldBe("Miller");
                user.Id.ShouldBe(u.Id);
            }
        }

        [Fact]
        public async Task query_with_select_in_query_async()
        {
            using (var session = theStore.OpenSession())
            {
                var u = new User {FirstName = "Jeremy", LastName = "Miller"};
                session.Store(u);
                session.SaveChanges();

                #region sample_using-queryasync

                var users =
                    await
                        session.QueryAsync<User>(
                            "select data from mt_doc_user where data ->> 'FirstName' = 'Jeremy'");
                var user = users.Single();

                #endregion

                user.LastName.ShouldBe("Miller");
                user.Id.ShouldBe(u.Id);
            }
        }
    }
}
