using Microsoft.AspNetCore.Mvc;
using TaskFlow.Models;
using TaskFlow.Data;
using MongoDB.Driver;

[ApiController]
[Route("[controller]")]
public class TasksController : ControllerBase
{
    private readonly MongoDbContext _context;
     private readonly IConfiguration _configuration;
     private readonly ILogger<TasksController> _logger;

    public TasksController(MongoDbContext context, IConfiguration configuration,  ILogger<TasksController> logger)
    {
        _context = context;
         _configuration = configuration;
         _logger = logger;
    }

    [HttpGet]
public async Task<ActionResult<List<Tasks>>> GetAllTasks()
{
    var tasks = await _context.Tasks.Find(_ => true).ToListAsync();
    return Ok(tasks);
}

[HttpGet("{id:length(24)}")]
public async Task<ActionResult<Tasks>> GetTaskById(string id)
{
    var task = await _context.Tasks.Find(t => t.Id == id).FirstOrDefaultAsync();
    if (task == null)
    {
        return NotFound($"Task with ID {id} not found.");
    }
    return Ok(task);
}
[HttpPost("create")]
public async Task<IActionResult> CreateTask([FromBody] Tasks task)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    try
    {
        // Insert the new task into the database
        await _context.Tasks.InsertOneAsync(task);

        // Update the user's tasks
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, task.AssignedUserId);
        var userUpdate = Builders<User>.Update.Push(u => u.Tasks, task.Id);
        await _context.Users.UpdateOneAsync(userFilter, userUpdate);

        // Update the project's taskIds
        var projectFilter = Builders<Projects>.Filter.Eq(p => p.Id, task.ProjectId);
        var projectUpdate = Builders<Projects>.Update.Push(p => p.TaskIds, task.Id);
        await _context.Projects.UpdateOneAsync(projectFilter, projectUpdate);

        // Return a simple success response
        // Return a simple success response
            return Ok(new { message = "Task added successfully." });

    }
    catch (MongoException mongoEx)
    {
        // Log MongoDB specific errors, these are issues directly from the database
        _logger.LogError($"MongoDB exception: {mongoEx.Message}");
        return StatusCode(500, $"Database operation failed: {mongoEx.Message}");
    }
}


[HttpGet("user/{userId}/project/{projectId}")]
public async Task<ActionResult<List<Tasks>>> GetTasksByUserAndProject(string userId, string projectId)
{
    var tasks = await _context.Tasks.Find(t => t.AssignedUserId == userId && t.ProjectId == projectId).ToListAsync();
    return Ok(tasks);
}





[HttpPut("{id:length(24)}")]
public async Task<IActionResult> UpdateTask(string id, [FromBody] Tasks updatedTask)
{
    var task = await _context.Tasks.Find(t => t.Id == id).FirstOrDefaultAsync();
    if (task == null)
    {
        return NotFound($"Task with ID {id} not found.");
    }

    updatedTask.Id = task.Id; // Ensure the ID remains the same
    await _context.Tasks.ReplaceOneAsync(t => t.Id == id, updatedTask);
    return NoContent();
}

[HttpDelete("{id:length(24)}")]
public async Task<IActionResult> DeleteTask(string id)
{
    var deleteResult = await _context.Tasks.DeleteOneAsync(t => t.Id == id);
    if (deleteResult.DeletedCount == 0)
    {
        return NotFound($"Task with ID {id} not found.");
    }
    return NoContent();
}

}
