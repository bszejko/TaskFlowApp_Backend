using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } // Nullable if Id can be null initially

        [BsonElement("email")]
        [BsonRequired]
        public string Email { get; set; }

        [BsonElement("password")]
        [BsonRequired]
        public string Password { get; set; }

        [BsonElement("firstName")]
        [BsonRequired]
        public string FirstName { get; set; } 
        [BsonElement("lastName")]
        [BsonRequired]
        public string LastName { get; set; } 

        // ...any other properties...
    }
}
