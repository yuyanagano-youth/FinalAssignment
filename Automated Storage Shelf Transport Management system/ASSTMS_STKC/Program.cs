using ASSTMS_STKC.Data;
using ASSTMS_STKC.Data.Repositories;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

//For generation and trust certificate  , Kestrel in ASP.NET Core app:
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7196, listenOptions =>
    {
        listenOptions.UseHttps("C:\\172.16.7.6+2.p12", "changeit");
    });
});



// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddScoped<StockersRepository>();
builder.Services.AddScoped<ShelfRepository>();
builder.Services.AddScoped<LogRepository>();
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<SqlDatabaseContext>();
builder.Services.AddScoped<ASSTMS_STKC.Services.JobValidator>();
builder.Services.AddScoped<ASSTMS_STKC.Services.StubCommandService>();
builder.Services.AddHostedService<ASSTMS_STKC.Services.StockerTimeoutService>();
builder.Services.AddHostedService<ASSTMS_STKC.Services.JobDispatcher>();
builder.Logging.ClearProviders();
builder.Host.UseNLog();


builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
//app.MapStaticAssets();
//app.MapRazorPages()
//   .WithStaticAssets();
app.UseStaticFiles();

app.MapRazorPages();

app.Run();
