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
        
        return Ok(new { 
            token = token, 
            message = "User authenticated successfully.",
            firstName=firstName //passing the user name so that it can be displayed on the homepage
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


}
