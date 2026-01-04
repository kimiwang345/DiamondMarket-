using DiamondMarket.Attributes;
using DiamondMarket.Data;
using DiamondMarket.Tasks;
using DiamondMarket.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ===== 读取是否跑任务 =====
bool runTasks = builder.Configuration.GetValue<bool>("RunTasks");

// ===== Kestrel 并发保护 =====
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 2000;
    options.Limits.MaxConcurrentUpgradedConnections = 2000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
// ===== 扩大 ThreadPool（非常关键）=====
ThreadPool.SetMinThreads(200, 200);

// DbContext
//var connStr = builder.Configuration.GetConnectionString("Default");
//builder.Services.AddDbContext<AppDbContext>(options =>
//{
//    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
//});
// ===== DB =====
builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(
            builder.Configuration.GetConnectionString("Default")
        )
    );
}, poolSize: 128);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = null; });

// 注册 HttpClient
builder.Services.AddHttpClient();
if (runTasks) {
    builder.Services.AddHostedService<RecyclingTaskWorker>();
    builder.Services.AddHostedService<UsdtWatcher>();
}

builder.Services.AddControllers(options =>
{
    options.Filters.Add<LoggingFilter>(); // 全局拦截
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = builder.Configuration.GetConnectionString("Redis") ??
                 builder.Configuration["Redis:ConnectionString"];
    return ConnectionMultiplexer.Connect(config);
});


// ====== JWT 认证配置 ======
var jwtConfig = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtConfig["Key"]);

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,

        ValidIssuer = jwtConfig["Issuer"],
        ValidAudience = jwtConfig["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// CORS
builder.Services.AddCors(o =>
{
    o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();


app.UseCors("AllowAll");
app.UseStaticFiles();

// ===== 启用 JWT 鉴权中间件 =====
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseMiddleware<VisitStatMiddleware>();

app.Run();
