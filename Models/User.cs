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

        [BsonElement("role")]
        [BsonRequired]
        public List<string> Roles { get; set; }

        [BsonElement("projectIds")]
        public List<string> ProjectIds { get; set; } // Lista identyfikatorów projektów, do których użytkownik jest przypisany

        // ...any other properties...
    }
}
