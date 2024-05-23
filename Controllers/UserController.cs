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
using MongoDB.Bson;
using System.Linq;
using Microsoft.AspNetCore.Authorization;




[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly MongoDbContext _context;
    private readonly ILogger<UserController> _logger;

    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
private readonly IMongoCollection<User> _usersCollection;




    public UserController(MongoDbContext context,ILogger<UserController> logger, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _usersCollection = _context.Users;

        _httpContextAccessor = httpContextAccessor;

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
    // Sprawdzenie poprawności modelu
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // Wyciągnięcie tokena JWT z nagłówka żądania
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    // Walidacja tokena i pobranie identyfikatora administratora
    var adminId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(adminId))
    {
        return Unauthorized("Admin ID could not be determined.");
    }

    // Pobranie istniejącego użytkownika o podanym adresie e-mail
    var existingUser = await _context.Users.Find(u => u.Email == user.Email).FirstOrDefaultAsync();
    if (existingUser != null)
    {
        return BadRequest("User already exists.");
    }

    // Ustawienie domyślnej roli użytkownika jako 'user'
    user.Role = "user"; 
    
    // Haszowanie hasła użytkownika
    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

   // Wstawienie nowego użytkownika do bazy danych
    await _context.Users.InsertOneAsync(user);

// Pobranie identyfikatora nowo wstawionego użytkownika
    string newUserId = user.Id.ToString();

    // Pobranie istniejącego administratora z bazy danych
    var admin = await _context.Users.Find(u => u.Id == adminId).FirstOrDefaultAsync();

    // Dodanie identyfikatora nowego użytkownika do listy OwnerOf administratora
    if (admin != null)
    {
        if (admin.OwnerOf == null)
        {
            admin.OwnerOf = new List<string>();
        }
        admin.OwnerOf.Add(newUserId);
    }

    // Aktualizacja rekordu administratora w bazie danych z nową listą OwnerOf
    var filter = Builders<User>.Filter.Eq(u => u.Id, adminId);
    var update = Builders<User>.Update.Set(u => u.OwnerOf, admin.OwnerOf);
    await _context.Users.UpdateOneAsync(filter, update);

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
  
[HttpGet("users")]
public async Task<ActionResult<IEnumerable<User>>> GetUsers()
{

    // Tworzenie filtra dla roli "user" - admin do projektu może dodać tylko użytkownika o roli user
    var filter = Builders<User>.Filter.Eq(u => u.Role, "user");

    // Użycie filtra w zapytaniu do bazy danych
    var users = await _context.Users.Find(filter).ToListAsync();

    return Ok(users);
}

[HttpGet("ownerOf")]
public async Task<ActionResult<IEnumerable<User>>> GetAdminOwnerOf()
{
    // Wyciągnięcie tokena JWT z nagłówka żądania
    var token = ExtractToken(Request);
    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized("Authentication token is missing.");
    }

    // Walidacja tokena i pobranie identyfikatora administratora
    var adminId = ValidateTokenAndGetUserId(token);
    if (string.IsNullOrEmpty(adminId))
    {
        return Unauthorized("Admin ID could not be determined.");
    }

    // Pobranie administratora z bazy danych na podstawie adminId
    var admin = await _context.Users.Find(u => u.Id == adminId).FirstOrDefaultAsync();
    if (admin == null)
    {
        return NotFound("Administrator not found.");
    }

        // Pobranie identyfikatorów użytkowników z OwnerOf
    var userIds = admin.OwnerOf;

    // Pobranie pełnych danych użytkowników na podstawie identyfikatorów
    var users = await _context.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();

    // Zwrócenie listy użytkowników
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



//METODY

[HttpDelete("delete")]
public async Task<IActionResult> DeleteUser(string id)
{
    // Check if user exists
    var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
    if (user == null)
    {
        return NotFound();
    }
    // Delete user
    await _usersCollection.DeleteOneAsync(u => u.Id == id);
    return Ok(new { message = "User deleted successfully." });
}

[HttpPost("change-password")]
[Authorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // Sprawdź, czy użytkownik jest zalogowany i pobierz jego identyfikator
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userId == null)
    {
        return Unauthorized("User is not logged in.");
    }

    // Pobierz użytkownika z bazy danych
    var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
    if (user == null)
    {
        return NotFound("User not found.");
    }

    // Sprawdź, czy obecne hasło jest poprawne
    if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password))
    {
        return BadRequest("Invalid current password.");
    }

    // Zaktualizuj hasło użytkownika
    user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

    // Zapisz zmiany w bazie danych
    var update = Builders<User>.Update.Set(u => u.Password, user.Password);
    await _context.Users.UpdateOneAsync(u => u.Id == userId, update);

     return Ok(new { message = "Password changed successfully." });
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


private string GenerateJwtToken(User user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["JwtConfig:Secret"]);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), 
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





   
}


