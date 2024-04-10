using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TaskFlow.Models
{
    public class Tasks
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("taskName")]
        [BsonRequired]
        public string TaskName { get; set; }

        [BsonElement("description")]
        public string Description { get; set; }

        [BsonElement("projectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } // Identyfikator projektu, do którego należy zadanie

        [BsonElement("assignedUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string AssignedUserId { get; set; } // Identyfikator użytkownika przypisanego do zadania

        [BsonElement("deadline")]
        [BsonRepresentation(BsonType.DateTime)]
        [BsonRequired]
        public DateTime Deadline { get; set; }
        

        [BsonElement("status")]
        public string Status { get; set; }
    }
}

