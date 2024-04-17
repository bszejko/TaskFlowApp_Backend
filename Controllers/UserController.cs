using Microsoft.AspNetCore.Mvc;
using TaskFlow.Data;
using TaskFlow.Models;
using MongoDB.Driver;
using System.Threading.Tasks;
using BCrypt.Net;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;



[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly MongoDbContext _context;

    private readonly IConfiguration _configuration;


    public UserController(MongoDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

 [HttpPost("register")]
public async Task<IActionResult> Register([FromBody] User user)
{
    if (!ModelState.IsValid) //checks if the data sent in the user request is valid based on the model annotations
    {
        return BadRequest(ModelState);
    }

    var existingUser = await _context.Users.Find(u => u.Email == user.Email).FirstOrDefaultAsync();
    if (existingUser != null)
    {
        // if user already exists
        return BadRequest("User already exists.");
    }

    // Setting default role as 'admin'
    user.Role = "admin"; 

    //hashing the password
    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

    await _context.Users.InsertOneAsync(user);
    return Ok(new { message = "User registered successfully." });
}

[HttpPost("registerByAdmin")]
public async Task<IActionResult> RegisterByAdmin([FromBody] User user)
{
    if (!ModelState.IsValid) //checks if the data sent in the user request is valid based on the model annotations
    {
        return BadRequest(ModelState);
    }

    var existingUser = await _context.Users.Find(u => u.Email == user.Email).FirstOrDefaultAsync();
    if (existingUser != null)
    {
        // if user already exists
        return BadRequest("User already exists.");
    }

    // Setting default role as 'user'
    user.Role = "user"; 

    //hashing the password
    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

    await _context.Users.InsertOneAsync(user);
    return Ok(new { message = "User registered successfully." });
}



    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] User user)
    {
        
        
        var existingUser = await _context.Users.Find(u => u.Email == user.Email).FirstOrDefaultAsync(); //Retrieves the user from MongoDB based on the provided email
        if (existingUser != null && BCrypt.Net.BCrypt.Verify(user.Password, existingUser.Password))
    {
        
        var token = GenerateJwtToken(existingUser); //generates a JWT token for the user
        string firstName = existingUser.FirstName; 
        string role = existingUser.Role;
        
        return Ok(new { 
            token = token, 
            message = "User authenticated successfully.",
            firstName=firstName, //passing the user name so that it can be displayed on the homepage
            role=role

        });
    }
        return Unauthorized("Invalid credentials.");
    }

   private string GenerateJwtToken(User user) //method for generating the token
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var secret = _configuration["JwtConfig:Secret"]; 
    var key = Encoding.ASCII.GetBytes(secret); 
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new Claim[] 
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
           
        }),
        Expires = DateTime.UtcNow.AddDays(7),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}

public static string GenerateSecretKey()
{
    var randomNumber = new byte[32]; // 32 bajty = 256 bitów
    using (var rng = new RNGCryptoServiceProvider())
    {
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
    // GET: /users
   
[HttpGet("users")]
public async Task<ActionResult<IEnumerable<User>>> GetUsers()
{
    // Tworzenie filtra dla roli "user" - admin do projektu może dodać tylko użytkownika o roli user
    var filter = Builders<User>.Filter.Eq(u => u.Role, "user");

    // Użycie filtra w zapytaniu do bazy danych
    var users = await _context.Users.Find(filter).ToListAsync();

    return Ok(users);
}





   
}


