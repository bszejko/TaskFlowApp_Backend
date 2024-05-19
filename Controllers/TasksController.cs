using Microsoft.AspNetCore.Mvc;
using TaskFlow.Models;
using TaskFlow.Data;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.DTOs;
using System.Linq.Expressions;



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
        // Inserting the new task into the database
        await _context.Tasks.InsertOneAsync(task);

        // Updating the user's tasks
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, task.AssignedUserId);
        var userUpdate = Builders<User>.Update.Push(u => u.Tasks, task.Id);
        await _context.Users.UpdateOneAsync(userFilter, userUpdate);

        // Updating the project's taskIds
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

[HttpGet("project/{projectId}/today")]
public async Task<ActionResult<List<Tasks>>> GetTodayTasksByProject(string projectId)
{
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    // Walidacja tokena i pobranie identyfikatora administratora
    var userId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized("User ID could not be determined.");
    }

    return await FetchTasks(userId, projectId);
}

[HttpGet("project/{projectId}/all-tasks")]
public async Task<ActionResult<List<Tasks>>> GetAllTasksByProject(string projectId)
{
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    // Validate the token and get the user ID from it
    var userId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized("User ID could not be determined.");
    }

    try
    {
        var tasks = await _context.Tasks
            .Find(t => t.AssignedUserId == userId && t.ProjectId == projectId)
            .ToListAsync();

        if (!tasks.Any())
        {
            return Ok($"No tasks found for project ID {projectId} assigned to the current user.");
        }

        return Ok(tasks);
    }
    catch (MongoException mongoEx)
    {
        _logger.LogError($"MongoDB exception: {mongoEx.Message}");
        return StatusCode(500, $"Database operation failed: {mongoEx.Message}");
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while processing your request: {ex}");
        return StatusCode(500, "An error occurred while processing your request.");
    }
}

[HttpGet("project/{projectId}/all-tasks-admin")]
public async Task<ActionResult<List<Tasks>>> GetAllTasksForProjectAdmin(string projectId)
{
    
    try
    {
        var tasks = await _context.Tasks
            .Find(t => t.ProjectId == projectId)
            .ToListAsync();

        if (!tasks.Any())
        {
            return Ok($"No tasks found for project ID {projectId}.");
        }

        return Ok(tasks);
    }
    catch (MongoException mongoEx)
    {
        _logger.LogError($"MongoDB exception: {mongoEx.Message}");
        return StatusCode(500, $"Database operation failed: {mongoEx.Message}");
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while processing your request: {ex}");
        return StatusCode(500, "An error occurred while processing your request.");
    }
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

 
[HttpPut("update-status/{id:length(24)}")]
public async Task<IActionResult> UpdateTaskStatus(string id, [FromBody] StatusUpdateDto update)
{
    var task = await _context.Tasks.Find(t => t.Id == id).FirstOrDefaultAsync();
    if (task == null)
    {
        return NotFound($"Task with ID {id} not found.");
    }

    var updateDefinition = Builders<Tasks>.Update.Set(t => t.Status, update.Status);
    await _context.Tasks.UpdateOneAsync(t => t.Id == id, updateDefinition);
    return NoContent(); // or Ok() to indicate success more clearly
}

[HttpPost("archive-and-delete-completed-tasks")]
public async Task<IActionResult> ArchiveAndDeleteCompletedTasks()
{
    try
    {
        // Define the filter to find completed tasks with a deadline that has already passed
        var filter = Builders<Tasks>.Filter.And(
            Builders<Tasks>.Filter.Eq(t => t.Status, "completed"),
            Builders<Tasks>.Filter.Lt(t => t.Deadline, DateTime.UtcNow)
        );

        var tasksToArchive = await _context.Tasks.Find(filter).ToListAsync();

        if (!tasksToArchive.Any())
        {
            return Ok(new { success = true, message = "No completed tasks with past deadlines to archive." });
        }

        foreach (var task in tasksToArchive)
        {
            var archivedTask = new ArchivedTasks
            {
                TaskName = task.TaskName,
                Description = task.Description,
                ProjectId = task.ProjectId,
                AssignedUserId = task.AssignedUserId,
                Deadline = task.Deadline,
                Status = task.Status
            };

            // Insert the task into the ArchivedTasks collection
            await _context.ArchivedTasks.InsertOneAsync(archivedTask);
            
            // Delete the task from the Tasks collection
            await _context.Tasks.DeleteOneAsync(t => t.Id == task.Id);
        }

        return Ok(new { success = true, message = $"Archived and deleted {tasksToArchive.Count} completed tasks." });
    }
    catch (Exception ex)
    {
        // Log the exception details for debugging
        _logger.LogError("Failed to archive and delete tasks: " + ex.Message);
        return StatusCode(500, new { success = false, message = "An error occurred while trying to archive and delete tasks." });
    }
}

//METODY

private async Task<ActionResult<List<Tasks>>> FetchTasks(string userId, string projectId)
{
    if (string.IsNullOrEmpty(projectId))
    {
        _logger.LogWarning("Project ID not provided");
        return BadRequest("Project ID must be provided.");
    }

    try
    {
        DateTime today = DateTime.UtcNow.Date;
        DateTime tomorrow = today.AddDays(1);

        var tasks = await _context.Tasks
            .Find(t => t.AssignedUserId == userId && t.ProjectId == projectId && t.Deadline >= today && t.Deadline < tomorrow)
            .ToListAsync();

        if (tasks == null || !tasks.Any())
        {
           
            return Ok("No tasks found for this user and project on today's date.");
        }

        return Ok(tasks);
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while processing your request: {ex}");
        return StatusCode(500, "An error occurred while processing your request.");
    }
}

private string ExtractToken(HttpRequest request)
    {
        //  Extracting the cookie
        string token = request.Cookies["JWT"];
        if (string.IsNullOrEmpty(token))
        {
            // Fallback to authorization header
            var bearerToken = request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(bearerToken) && bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
{
    token = bearerToken["Bearer ".Length..].Trim();  
}

        }
        return token;
    }

    private string ValidateTokenAndGetUserId(string token)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["JwtConfig:Secret"]);
    try
    {
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out SecurityToken validatedToken);

        var jwtToken = (JwtSecurityToken)validatedToken;
        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid"); 
        if (userIdClaim == null)
        {
            _logger.LogError("User ID claim ('nameid') not found in token");
            return null;
        }
        return userIdClaim.Value;
    }
    catch (SecurityTokenExpiredException ex)
    {
        _logger.LogError($"Token expired: {ex.Message}");
        return null;
    }
    catch (SecurityTokenValidationException ex)
    {
        _logger.LogError($"Token validation failed: {ex.Message}");
        return null;
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while validating token: {ex.Message}");
        return null;
    }
}



}




