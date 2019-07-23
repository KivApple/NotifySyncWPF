using System.Security.Cryptography;

namespace NotifySync {
	public class NetworkCipher {
		private byte[] _key;
		private byte[] _iv;

		public NetworkCipher(byte[] key) {
			_key = key.Slice(0, key.Length / 2);
			_iv = key.Slice(key.Length / 2, key.Length);
		}

		private Aes CreateAes() {
			var aes = Aes.Create();
			aes.Key = _key;
			aes.IV = _iv;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			return aes;
		}
		
		public ICryptoTransform CreateEncryptor() {
			return CreateAes().CreateEncryptor();
		}

		public ICryptoTransform CreateDecryptor() {
			return CreateAes().CreateDecryptor();
		}
	}
}
