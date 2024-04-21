using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TaskFlow.Data;
using TaskFlow.Models;
using TaskFlow.ViewModels;
using MongoDB.Driver;
using MongoDB.Bson;




namespace TaskFlow.Controllers
{
    [ApiController]
    [Route("[controller]")]
  
public class ProjectsController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly ILogger<ProjectsController> _logger;
    private readonly IConfiguration _configuration;

    public ProjectsController(MongoDbContext context, ILogger<ProjectsController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("create")]
public async Task<IActionResult> CreateProject([FromBody] Projects project)
{
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    var userId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized("User ID could not be determined.");
    }

    _logger.LogInformation("User ID extracted: {UserId}", userId);

    project.CreatedBy = userId;
    try
    {
        if (project.Members == null || project.Members.Count == 0)
        {
            return BadRequest("Member IDs are required.");
        }

        var validUserCount = await _context.Users.CountDocumentsAsync(u => project.Members.Contains(u.Id));
        if (validUserCount != project.Members.Count)
        {
            return BadRequest("One or more member IDs are invalid.");
        }

        // Insert the new project
        await _context.Projects.InsertOneAsync(project);
        var projectId = project.Id;

        // Update the project IDs list for each member
        var filterMembers = Builders<User>.Filter.In(u => u.Id, project.Members);
        var updateMembers = Builders<User>.Update.Push(u => u.ProjectIds, projectId);
        await _context.Users.UpdateManyAsync(filterMembers, updateMembers);

        // Update the ownerOf list for the project creator
        var filterOwner = Builders<User>.Filter.Eq(u => u.Id, userId);
        var updateOwner = Builders<User>.Update.Push(u => u.OwnerOf, projectId);
        await _context.Users.UpdateOneAsync(filterOwner, updateOwner);

        return Ok(new { message = "Project created successfully and users updated." });
    }
    catch (Exception ex)
    {
        return BadRequest($"Failed to create project: {ex.Message}");
    }
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
                token = bearerToken.Substring("Bearer ".Length).Trim();
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



        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetProject(string id)
        {
            try
            {
                var project = await _context.Projects.Find(p => p.Id == id).FirstOrDefaultAsync();
                if (project == null)
                    return NotFound("Project not found.");

                return Ok(project);
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to retrieve project: {ex.Message}");
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateProject(string id, [FromBody] Projects updatedProject)
        {
            try
            {
                updatedProject.Id = id;
                var result = await _context.Projects.ReplaceOneAsync(p => p.Id == id, updatedProject);
                if (result.ModifiedCount == 0)
                    return NotFound("Project not found.");

                return Ok(new { message = "Project updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to update project: {ex.Message}");
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteProject(string id)
        {
            try
            {
                var result = await _context.Projects.DeleteOneAsync(p => p.Id == id);
                if (result.DeletedCount == 0) //ile dokumentów zostało usuniętych, ówny 1, jeśli dokument został pomyślnie usunięty, lub 0, jeśli dokument o podanym identyfikatorze nie został znaleziony
                    return NotFound("Project not found.");

                return Ok(new { message = "Project deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to delete project: {ex.Message}");
            }
        }

        [HttpGet("userProjects")]
public async Task<IActionResult> GetUserProjects()
{
    // Extract the token directly from the HttpRequest
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    // Validate token and extract user ID
    var userId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized("User ID could not be determined.");
    }

    try
    {
        var projects = await _context.Projects
                                     .Find(p => p.CreatedBy == userId)
                                     .ToListAsync();

        if (projects.Count == 0)
        {
            return NotFound("No projects found for this user.");
        }

        return Ok(projects); // Return the projects associated with the user
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while retrieving user projects: {ex.Message}");
        return BadRequest($"An error occurred: {ex.Message}");
    }
}
[HttpGet("{projectId}/members")]
public async Task<IActionResult> GetProjectMembers(string projectId)
{
    try
    {
        var project = await _context.Projects.Find(p => p.Id == projectId).FirstOrDefaultAsync();
        if (project == null)
        {
            _logger.LogError($"Project with ID {projectId} not found.");
            return NotFound("Project not found.");
        }

        if (project.Members == null || !project.Members.Any())
        {
            _logger.LogInformation($"No members found for project with ID {projectId}.");
            return NotFound("No members found for this project.");
        }

        var memberFilter = Builders<User>.Filter.In(u => u.Id, project.Members);
        var users = await _context.Users.Find(memberFilter).ToListAsync();

        if (users == null || !users.Any())
        {
            _logger.LogInformation($"Users corresponding to member IDs in project {projectId} not found.");
            return NotFound("Members not found.");
        }

        return Ok(users);
    }
    catch (Exception ex)
    {
        _logger.LogError($"An error occurred while retrieving project members: {ex.Message}", ex);
        return BadRequest($"An error occurred: {ex.Message}");
    }
}

   
}



    }

