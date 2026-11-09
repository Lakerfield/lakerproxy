using System.Linq;
using System.Threading.Tasks;
using Lakerfield.Runtime;
using Entity = LakerProxy.Models.Website;
using WebEntity = LakerProxy.WebApi.Models.Certificate;

namespace LakerProxy.ProxyApp.ApiExtensions
{
  public static class CertificateConvert
  {

    public static async Task<WebEntity> ToWebCertificate(this Entity entity, AppServices services)
    {
      if (entity == null)
        return null;

      var result = new WebEntity()
      {
        Id = entity.Id,
        Hostname = entity.Hostname,
        State = LakerProxy.WebApi.CertificateStates.Unknown,
      };

      var certificate = entity.Certificates
        .Where(c => c.State == CertificateStates.Active)
        .OrderByDescending(c => c.ValidTill)
        .FirstOrDefault();

      if (certificate == null)
        return result;

      result.ValidFrom = certificate.ValidFrom;
      result.ValidTill = certificate.ValidTill;
      result.State = (LakerProxy.WebApi.CertificateStates)certificate.State;

      result.PemCertificate = certificate.Certificate ?? "";
      result.PemCertificateKey = certificate.CertificateKey ?? "";

      result.PfxCertificate = certificate.CertificatePfx ?? new byte[0];


      // There are 2 certificate bugs in windows and wsl (and will not be fixed in .net 5.0 because of dependencies) 
      //
      // - Cannot export key from pfx loaded in X509Certificate2 to a pem private key
      // - Cannot use a X509Certificate2 created from pem certificate and key in a SslStream
      //
      /*
      var x509Cert = new X509Certificate2(certificate.CertificatePfx, WebsiteContextExtensions.PfxPassword, X509KeyStorageFlags.Exportable);

      //X509Certificate2.CreateFromPem()

      //https://stackoverflow.com/questions/43928064/export-private-public-keys-from-x509-certificate-to-pem
      //https://stackoverflow.com/a/43941142
      byte[] certificateBytes = x509Cert.RawData;
      char[] certificatePem = PemEncoding.Write("CERTIFICATE", certificateBytes);

      AsymmetricAlgorithm key = x509Cert.GetRSAPrivateKey() ?? (AsymmetricAlgorithm) x509Cert.GetECDsaPrivateKey();
      byte[] pubKeyBytes = key.ExportSubjectPublicKeyInfo();

      // export private key workaround/hack
      //https://github.com/dotnet/runtime/issues/36899
      if (key is RSACng cng1)
      {
        byte[] exportValue = new byte[] { 0x02, 0x00, 0x00, 0x00 }; // 0x02 DWORD in little endian
        cng1.Key.SetProperty(new CngProperty("Export Policy", exportValue, CngPropertyOptions.None));
      }
      if (key is ECDsaCng cng2)
      {
        byte[] exportValue = new byte[] { 0x02, 0x00, 0x00, 0x00 }; // 0x02 DWORD in little endian
        cng2.Key.SetProperty(new CngProperty("Export Policy", exportValue, CngPropertyOptions.None));
      }

      byte[] privKeyBytes = key.ExportPkcs8PrivateKey();
      char[] pubKeyPem = PemEncoding.Write("PUBLIC KEY", pubKeyBytes);
      char[] privKeyPem = PemEncoding.Write("PRIVATE KEY", privKeyBytes);

      result.CertificatePem = new string(certificatePem);
      result.PublicKeyPem = new string(pubKeyPem);
      result.PrivateKeyPem = new string(privKeyPem);
      */
      return result;
    }

  }
}
