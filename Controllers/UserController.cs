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
public async Task<IActionResult> Login([FromBody] User loginUser)
{
    var existingUser = await _context.Users.Find(u => u.Email == loginUser.Email).FirstOrDefaultAsync();
    if (existingUser != null && BCrypt.Net.BCrypt.Verify(loginUser.Password, existingUser.Password))
    {
        var token = GenerateJwtToken(existingUser);
        string firstName = existingUser.FirstName;
        string role = existingUser.Role;


        HttpContext.Response.Cookies.Append("JWT", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Ensure using HTTPS
            SameSite = SameSiteMode.None, // Needed for cross-origin where applicable
            Path = "/", // Ensures cookie is sent for all paths
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });




        return Ok(new {
            message = "User authenticated successfully.",
            firstName,
            role
        });
    }

    return Unauthorized("Invalid credentials.");
}

private string GenerateJwtToken(User user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["JwtConfig:Secret"]);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Ensure this claim is correctly set
            new Claim(ClaimTypes.Email, user.Email),
            // Additional claims can be added here
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

[HttpGet("{id}")]
public async Task<IActionResult> GetById(string id)
{
    // Użycie _context do bezpośredniego dostępu do kolekcji Users w MongoDB i wyszukiwania po ID
    var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
    if (user == null)
    {
        return NotFound();
    }
    return Ok(user);
}

[HttpPost("logout")]
public IActionResult Logout()
{
    // Usuń cookie z tokenem JWT
    Response.Cookies.Delete("JWT");

    // Opcjonalnie: Możesz też zwrócić odpowiedź informującą o sukcesie
    return Ok(new { message = "Wylogowano pomyślnie." });
}






   
}


