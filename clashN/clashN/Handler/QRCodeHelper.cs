using QRCoder;
using QRCoder.Xaml;
using System.Windows.Media;

namespace ClashN.Handler
{
    /// <summary>
    /// 含有QR码的描述类和包装编码和渲染
    /// </summary>
    public class QRCodeHelper
    {
        public static DrawingImage GetQRCode(string strContent)
        {
            try
            {
                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(strContent, QRCodeGenerator.ECCLevel.H);
                var qrCode = new XamlQRCode(qrCodeData);
                var qrCodeAsXaml = qrCode.GetGraphic(40);
                return qrCodeAsXaml;
            }
            catch
            {
                return null;
            }
        }
    }
}
