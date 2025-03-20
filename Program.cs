using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory dictionary to store user objects with Id as key
var users = new Dictionary<int, User>();

// Adding example users to the dictionary
users.Add(1, new User(1, "alice", 25));
users.Add(2, new User(2, "bob", 30));
users.Add(3, new User(3, "charlie", 35));
users.Add(4, new User(4, "diana", 28));
users.Add(5, new User(5, "edward", 40));

// Add Exception Handling Middleware first
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Add Token Validation Middleware second
app.UseMiddleware<TokenValidationMiddleware>();

// Add Logging Middleware last
app.UseMiddleware<LoggingMiddleware>();


// Endpoint to retrieve all users (sorted by Id)
app.MapGet("/users", () =>
{
    var sortedUsers = users.Values;
    return Results.Ok(sortedUsers);
});

// Endpoint to retrieve a specific user by Id
app.MapGet("/users/{id:int}", (int id) =>
{
    if (users.TryGetValue(id, out var user))
    {
        return Results.Ok(user);
    }
    return Results.NotFound("User not found");
});

// Endpoint to create a new user
var usernames = new HashSet<string>();

app.MapPost("/users", (User newUser) =>
{
    if (string.IsNullOrWhiteSpace(newUser.Username))
    {
        return Results.BadRequest("Username cannot be empty or whitespace.");
    }

    if (newUser.Userage <= 0)
    {
        return Results.BadRequest("Userage must be greater than 0.");
    }

    if (!usernames.Add(newUser.Username))
    {
        return Results.BadRequest("Username must be unique.");
    }

    newUser.Id = users.Count > 0 ? users.Keys.Max() + 1 : 1;
    users[newUser.Id] = newUser;

    return Results.Created($"/users/{newUser.Id}", newUser);
});



// Endpoint to update a user by Id
app.MapPut("/users/{id:int}", (int id, User updatedUser) =>
{
    try
    {
        
        if (updatedUser.Id != id)
        {
            return Results.BadRequest("User Id in the body does not match the Id in the URL.");
        }
        
        if (!users.ContainsKey(id))
        {
            return Results.NotFound($"User with Id {id} not found.");
        }

        if (string.IsNullOrWhiteSpace(updatedUser.Username))
        {
            return Results.BadRequest("Username cannot be empty.");
        }

        if (updatedUser.Userage <= 0)
        {
            return Results.BadRequest("Userage must be greater than 0.");
        }

        updatedUser.Id = id;
        users[id] = updatedUser;

        return Results.Ok(updatedUser);
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}");
    }
});


// Endpoint to delete a user by Id
app.MapDelete("/users/{id:int}", (int id) =>
{
    if (!users.Remove(id))
    {
        return Results.NotFound("User not found");
    }
    return Results.Ok("User deleted");
});

app.Run();

// User class definition
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public int Userage { get; set; }

    public User(int id, string username, int userage)
    {
        Id = id;
        Username = username;
        Userage = userage;
    }
}

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the HTTP method and request path
        Console.WriteLine($"HTTP Method: {context.Request.Method}");
        Console.WriteLine($"Request Path: {context.Request.Path}");

        // Invoke the next middleware in the pipeline
        await _next(context);

        // Log the response status code
        Console.WriteLine($"Response Status Code: {context.Response.StatusCode}");
    }
}

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Proceed with the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            // Handle the exception and return a consistent error response
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log the exception details (you can use a logging framework here)
        Console.WriteLine($"Unhandled Exception: {exception.Message}");

        // Set the response properties
        context.Response.StatusCode = 500; // Internal Server Error
        context.Response.ContentType = "application/json";

        // Create a consistent error response object
        var errorResponse = new
        {
            error = "Internal server error."
        };

        // Serialize the error response to JSON and write it to the response body
        return context.Response.WriteAsJsonAsync(errorResponse);
    }
}


public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get the Authorization header
        var authorizationHeader = context.Request.Headers["Authorization"].ToString();

        // Check if the Authorization header is present and follows the "Bearer {token}" format
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("Unauthorized: Missing or invalid token.");
            return;
        }

        // Extract the token (remove "Bearer " prefix)
        var token = authorizationHeader.Substring("Bearer ".Length);

        // Validate the token (implement your token validation logic here)
        if (!ValidateToken(token))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("Unauthorized: Token validation failed.");
            return;
        }

        // If the token is valid, proceed to the next middleware
        await _next(context);
    }

    private bool ValidateToken(string token)
    {
        // Implement token validation logic
        // For example, decode and verify a JWT token using a library like System.IdentityModel.Tokens.Jwt
        // Here, we'll just simulate validation by returning true for a specific token
        return token == "valid-token-example";
    }
}

