using Microsoft.AspNetCore.Mvc;
using TaskFlow.Models;
using TaskFlow.Data;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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
        return Unauthorized("Admin ID could not be determined.");
    }

    // Proceed with fetching tasks using the extracted user ID and project ID
    return await FetchTasks(userId, projectId);
}

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
            _logger.LogWarning("No tasks found for the provided user and project on today's date");
            return NotFound("No tasks found for this user and project on today's date.");
        }

        return Ok(tasks);
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

 private string ExtractToken(HttpRequest request)
    {
        // Try to extract from cookie first
        string token = request.Cookies["JWT"];
        if (string.IsNullOrEmpty(token))
        {
            // Fallback to authorization header
            var bearerToken = request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(bearerToken) && bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
{
    token = bearerToken["Bearer ".Length..].Trim();  // More robust trimming
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
        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid"); // Adjusted to match the claim name in your JWT
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

