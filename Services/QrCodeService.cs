using QRCoder;

namespace CondoSystem.Services
{
    public interface IQrCodeService
    {
        string GenerateQrCodeBase64(string data);
    }

    public class QrCodeService : IQrCodeService
    {
        public string GenerateQrCodeBase64(string data)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using var pngQrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = pngQrCode.GetGraphic(20);
            return Convert.ToBase64String(qrCodeBytes);
        }
    }
}
