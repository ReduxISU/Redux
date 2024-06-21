using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Redux API",
        Description = "An ASP.NET Core Web API for reducing, verifying, and solving NP-Complete problems",
        // TermsOfService = new Uri("https://example.com/terms"),
        // Contact = new OpenApiContact
        // {
        //     Name = "Example Contact",
        //     Url = new Uri("https://example.com/contact")
        // },
        // License = new OpenApiLicense
        // {
        //     Name = "Example License",
        //     Url = new Uri("https://example.com/license")
        // }
    });

    options.CustomSchemaIds(type => type.FullName);

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
var app = builder.Build();

app.UseStaticFiles();

// Somewhat of a security concern. But since we are not doing POSTS im not concerned about it
app.Use((context, next) =>
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        return next.Invoke();
    });

// app.UseHttpsRedirection();
app.UseCors(x => x
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true) // allow any origin
                .AllowCredentials()); // allow credentials
    
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.InjectStylesheet("/assests/css/swagger.css")
);


app.MapControllers();

app.Run();
