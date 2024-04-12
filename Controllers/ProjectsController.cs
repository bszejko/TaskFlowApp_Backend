using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskFlow.Data;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public ProjectsController(MongoDbContext context)
        {
            _context = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProject([FromBody] Projects project)
        {
            try
            {
                await _context.Projects.InsertOneAsync(project);
                return Ok(new { message = "Project created successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to create project: {ex.Message}");
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
    }
}