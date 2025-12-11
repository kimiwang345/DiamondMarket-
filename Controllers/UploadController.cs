using DiamondMarket.Attributes.DiamondMarket.Attributes;
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
        [RateLimit(1, 3)]
        public async Task<IActionResult> UploadQrcode(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { code = 400, msg = "未选择文件" });

            // =============== 1. 限制文件大小（2MB） ===============
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { code = 400, msg = "文件过大（最大 2MB）" });

            // =============== 2. 检查 MIME 类型 ===============
            var allowedContentTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { code = 400, msg = "非法 Content-Type" });

            // =============== 3. 检查后缀名 ===============
            var ext = Path.GetExtension(file.FileName).ToLower();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
            if (!allowedExt.Contains(ext))
                return BadRequest(new { code = 400, msg = "非法文件后缀" });

            // =============== 4. 检查魔法头（Magic Number）==============
            byte[] header = new byte[8];
            using (var reader = file.OpenReadStream())
            {
                await reader.ReadAsync(header, 0, header.Length);
            }

            // JPEG: FF D8 FF
            bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
                         header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A;

            if (!isJpg && !isPng)
                return BadRequest(new { code = 400, msg = "文件不是合法图片（伪造后缀）" });

            // =============== 5. 保存文件 =====================
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


        [HttpPost("uploadChatImg")]
        [RateLimit(1, 3)]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { code = 400, msg = "未选择文件" });

            // =============== 1. 限制文件大小（2MB） ===============
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { code = 400, msg = "文件过大（最大 2MB）" });

            // =============== 2. 检查 MIME 类型 ===============
            var allowedContentTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedContentTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { code = 400, msg = "非法 Content-Type" });

            // =============== 3. 检查后缀名 ===============
            var ext = Path.GetExtension(file.FileName).ToLower();
            var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
            if (!allowedExt.Contains(ext))
                return BadRequest(new { code = 400, msg = "非法文件后缀" });

            // =============== 4. 检查魔法头（Magic Number）==============
            byte[] header = new byte[8];
            using (var reader = file.OpenReadStream())
            {
                await reader.ReadAsync(header, 0, header.Length);
            }

            // JPEG: FF D8 FF
            bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
                         header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A;

            if (!isJpg && !isPng)
                return BadRequest(new { code = 400, msg = "文件不是合法图片（伪造后缀）" });

            // =============== 5. 保存文件 =====================
            string dir = Path.Combine(_env.WebRootPath, "upload/chat");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string filename = $"qr_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
            string savePath = Path.Combine(dir, filename);

            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string url = "/upload/chat/" + filename;

            return Ok(new { code = 0, msg = "上传成功", url });
        }

    }
}
