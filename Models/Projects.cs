using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TaskFlow.Models
{
    public class Projects
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("projectName")]
        [BsonRequired]
        public string ProjectName { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("deadline")]
        [BsonRepresentation(BsonType.DateTime)]
        [BsonRequired]
        public DateTime Deadline { get; set; }
        
        [BsonElement("taskIds")]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonRequired]
        public List<string> TaskIds { get; set; } // Lista identyfikatorów zadań przypisanych do projektu


        [BsonElement("members")]
        public List<string> Members { get; set; }

    }
}

