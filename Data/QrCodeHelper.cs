namespace DiamondMarket.Data
{
    using QRCoder;
    using System;

    public static class QrCodeHelper
    {
        public static string GenerateQrDataUrl(string content, int pixelsPerModule = 10)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrBytes = qrCode.GetGraphic(pixelsPerModule);

            return "data:image/png;base64," + Convert.ToBase64String(qrBytes);
        }
    }


}
