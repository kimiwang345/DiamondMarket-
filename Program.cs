using DiamondMarket.Data;
using DiamondMarket.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DbContext
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
});

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o => { o.JsonSerializerOptions.PropertyNamingPolicy = null; });

// 注册 HttpClient
builder.Services.AddHttpClient();
builder.Services.AddHostedService<RecyclingTaskWorker>();
builder.Services.AddHostedService<UsdtWatcher>();

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

app.Run();
