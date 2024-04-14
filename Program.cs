using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaskFlow.Data;



var builder = WebApplication.CreateBuilder(args);

// Konfiguracja JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSecretKey")), // Użyj bezpiecznego klucza
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// CORS configuration 
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyAllowSpecificOrigins",
    corsBuilder =>
    {
        corsBuilder.WithOrigins("http://localhost:8100") // Replace with the origin of Ionic app
                   .AllowAnyHeader()
                   .AllowAnyMethod();
    });
});

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

        

// MongoDB configuration
var mongoDbSettings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
builder.Services.AddSingleton<MongoDbContext>(sp =>
    new MongoDbContext(mongoDbSettings.ConnectionString, mongoDbSettings.DatabaseName));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("MyAllowSpecificOrigins");

app.UseAuthentication(); // Dodaj to przed UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();

// MongoDbSettings class
public class MongoDbSettings
{
    public required string ConnectionString { get; set; }
    public required string DatabaseName { get; set; }
}
