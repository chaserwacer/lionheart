using lionheart.Data;
using lionheart;
using lionheart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

builder.Services.AddDbContext<ModelContext>(options =>
    options.UseSqlite("Data Source=./Data/lionheart.db"));


builder.Services.AddAuthorization();
services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<ModelContext>();



builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IActivityService, ActivityService>();
builder.Services.AddTransient<IOuraService, OuraService>();

builder.Services.AddHttpClient<IOuraService, OuraService>(client =>
{
    client.BaseAddress = new Uri("https://api.ouraring.com/v2/usercollection");
});


builder.Services.AddHttpClient();



builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Logging.AddConsole(); 
builder.Logging.AddDebug();

var app = builder.Build();

app.MapIdentityApi<IdentityUser>();



if (!app.Environment.IsDevelopment())
{
    app.UseHsts();

}
app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();
app.UseStaticFiles();


app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("about", "about.html");
app.MapFallbackToFile("index.html");

app.Run();