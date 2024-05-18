using System.Linq.Expressions;
using MongoDB.Driver;
using TaskFlow.Models;

namespace TaskFlow.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Projects> Projects => _database.GetCollection<Projects>("Projects");
        public IMongoCollection<Tasks> Tasks => _database.GetCollection<Tasks>("Tasks");

         public IMongoCollection<ArchivedTasks> ArchivedTasks => _database.GetCollection<ArchivedTasks>("ArchivedTasks");

        public async Task<IEnumerable<T>> FindAsync<T>(string collectionName, Expression<Func<T, bool>> filterExpression) //wyszukiwania danych w kolekcji MongoDB na podstawie określonego filtru, zwraca kolekcję dokumentów spełniających kryteria
        {
            var collection = _database.GetCollection<T>(collectionName);
            var result = await collection.FindAsync(filterExpression);
            return await result.ToListAsync();
        }

    }

}
