using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace Zhengyan.Commons
{
    public enum AuthenticateResult
    {
        /// <summary>
        /// 通过
        /// </summary>
        Passed = 0,

        /// <summary>
        /// 解密失败
        /// </summary>
        DecryptFailed = 1,

        /// <summary>
        /// ID错误
        /// </summary>
        IDError = 2,

        /// <summary>
        /// 密码错误
        /// </summary>
        SecretError = 4,

        /// <summary>
        /// 证书过期
        /// </summary>
        Expired = 8
    }
    public class ClientSecret
    {
        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 客户端密码
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public long Expires { get; set; }

        public ClientSecret()
        {
            Expires = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(
                value: this,
                options: new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    WriteIndented = true
                });
        }

        public byte[] ToBytes()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(ID);
                writer.Write(Secret);
                writer.Write(Expires);
                return stream.ToArray();
            }
        }

        public static ClientSecret FromBytes(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                ClientSecret clientSecret = new ClientSecret();
                clientSecret.ID = reader.ReadString();
                clientSecret.Secret = reader.ReadString();
                clientSecret.Expires = reader.ReadInt64();

                return clientSecret;
            }
        }

        /// <summary>
        /// 加密成证书
        /// </summary>
        /// <param name="key">密钥</param>
        /// <returns>证书串</returns>
        public string Encrypt(string key)
        {
            key = key == null ? "" : key;
            // return Convert.ToBase64String(XXTea.Encrypt(Encoding.UTF8.GetBytes(this.ToString()), Encoding.UTF8.GetBytes(key)));

            return XXTea.Encrypt(this.ToBytes(), Encoding.UTF8.GetBytes(key)).ToHexString();
        }

        /// <summary>
        /// 验证证书
        /// </summary>
        /// <param name="encryptedString">证书加密串</param>
        /// <param name="key">密钥</param>
        /// <param name="result">验证结果</param>
        /// <returns>是否通过</returns>
        public bool Authenticate(string encryptedString, string key,out AuthenticateResult result)
        {
            result = AuthenticateResult.Passed;
            try
            {
                // string json = Encoding.UTF8.GetString(XXTea.Decrypt(Convert.FromBase64String(encryptedString), Encoding.UTF8.GetBytes(key)));
                byte[] bytes = XXTea.Decrypt(encryptedString.HexStringToBytes(), Encoding.UTF8.GetBytes(key));
                var clientSecret = FromBytes(bytes);
                if (clientSecret.ID != this.ID)
                    result |= AuthenticateResult.IDError;
                if (clientSecret.Secret != this.Secret)
                    result |= AuthenticateResult.SecretError;
                if (clientSecret.Expires < this.Expires)
                    result |= AuthenticateResult.Expired;
            }
            catch
            {
                result = AuthenticateResult.DecryptFailed;
            }

            return result == AuthenticateResult.Passed;
        }
    }
    public class ClientCertificateProvider
    {
        public string Key { get; private set; }

        internal ClientCertificateProvider(string key) 
        {
            Key = key;
        }

        public static ClientCertificateProvider CreateInstance()
        {
            return new ClientCertificateProvider("ZhengYanIsGenius");
        }

        public ClientCertificateProvider SetKey(string key)
        {
            this.Key = key;
            return this;
        }

        public string CreateCertificate(ClientSecret clientSecret)
        {
            if (clientSecret == null)
                throw new ArgumentNullException();
            return clientSecret.Encrypt(Key);
        }

        public AuthenticateResult AuthenticateCertificate(string id, string secret, string certificate)
        {
            ClientSecret clientSecret = new ClientSecret() { ID = id, Secret = secret };
            clientSecret.Authenticate(certificate, Key, out AuthenticateResult result);
            return result;
        }
    }
}
