using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TaskFlow.Models
{
    public class Projects
    {
        public Projects(string projectName, string description, DateTime deadline)
        {
            if (string.IsNullOrEmpty(projectName))
                throw new ArgumentException("Project name cannot be null or empty.", nameof(projectName));

            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Description cannot be null or empty.", nameof(description));

            if (deadline == DateTime.MinValue)
                throw new ArgumentException("Deadline must be provided.", nameof(deadline));

            ProjectName = projectName;
            Description = description;
            Deadline = deadline;
            TaskIds = new List<string>();
            Members = new List<string>();
        }


        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("projectName")]
        [BsonRequired]
        public string ProjectName { get; set; }

        [BsonElement("description")]
        [BsonRequired]
        public string Description { get; set; }

        [BsonElement("deadline")]
        [BsonRepresentation(BsonType.DateTime)]
        [BsonRequired]
        public DateTime Deadline { get; set; }
        
        [BsonElement("taskIds")]
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> TaskIds { get; set; } // Lista identyfikatorów zadań przypisanych do projektu


        [BsonElement("members")]
        public List<string> Members { get; set; }

    }
}

