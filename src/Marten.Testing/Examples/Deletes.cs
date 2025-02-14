using Marten.Testing.Documents;

namespace Marten.Testing.Examples
{
    public class Deletes
    {
        #region sample_deletes
        public void delete_documents(IDocumentSession session)
        {
            var user = new User();

            session.Delete(user);
            session.SaveChanges();

            // OR

            session.Delete(user.Id);
            session.SaveChanges();
        }

        #endregion
    }
}
