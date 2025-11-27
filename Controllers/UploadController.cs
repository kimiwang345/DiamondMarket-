using Microsoft.AspNetCore.Mvc;

namespace DiamondMarket.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("qrcode")]
        public async Task<IActionResult> UploadQrcode(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { code = 400, msg = "未选择文件" });

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                return BadRequest(new { code = 400, msg = "仅支持 JPG/PNG 格式" });

            string dir = Path.Combine(_env.WebRootPath, "upload");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string filename = $"qr_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
            string savePath = Path.Combine(dir, filename);

            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string url = "/upload/" + filename;

            return Ok(new { code = 0, msg = "上传成功", url });
        }
    }
}
