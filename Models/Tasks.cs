using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace TaskFlow.Models
{
    public class Tasks
    {
         public Tasks(string taskName, string projectId, DateTime deadline, string description)
        {
            if (string.IsNullOrEmpty(taskName))
                throw new ArgumentException("Task name cannot be null or empty.", nameof(taskName));

            if (string.IsNullOrEmpty(projectId))
                throw new ArgumentException("Project ID cannot be null or empty.", nameof(projectId));

            if (deadline == DateTime.MinValue)
                throw new ArgumentException("Deadline must be provided.", nameof(deadline));

            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Description cannot be null", nameof(description));
            TaskName = taskName;
            ProjectId = projectId;
            Deadline = deadline;
            Description = description;
            Status = "false"; // Domyślny status na "niewykonane"
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("taskName")]
        [BsonRequired]
        public string TaskName { get; set; }

        [BsonElement("description")]
        [BsonRequired]
        public string Description { get; set; }

        [BsonElement("projectId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } // Identyfikator projektu, do którego należy zadanie

        [BsonElement("assignedUserId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedUserId { get; set; } // Identyfikator użytkownika przypisanego do zadania

        [BsonElement("deadline")]
        [BsonRepresentation(BsonType.DateTime)]
        [BsonRequired]
        public DateTime Deadline { get; set; }
        

        [BsonElement("status")]
        public string Status { get; set; }
    }
}

