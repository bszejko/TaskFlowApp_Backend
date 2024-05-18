using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskFlow.Models
{
    public class ArchivedTasks
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("taskName"), BsonRequired]
        [Required(ErrorMessage = "Task name cannot be null or empty.")]
        public string TaskName { get; set; }

        [BsonElement("description"), BsonRequired]
        [Required(ErrorMessage = "Description cannot be null.")]
        public string Description { get; set; }

        [BsonElement("projectId"), BsonRequired]
        [Required(ErrorMessage = "Project ID cannot be null or empty.")]
        public string ProjectId { get; set; }

        [BsonElement("assignedUserId"), BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedUserId { get; set; }

        [BsonElement("deadline"), BsonRequired]
        [Required]
        public DateTime Deadline { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }
    }
}
