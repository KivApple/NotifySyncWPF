using System.Security.Cryptography;

namespace NotifySync {
	public class NetworkCipher {
		private Aes _aes = Aes.Create();

		public NetworkCipher(byte[] key) {
			_aes.Key = key.Slice(0, key.Length / 2);
			_aes.IV = key.Slice(key.Length / 2, key.Length);
		}
		
		public ICryptoTransform CreateEncryptor() {
			return _aes.CreateEncryptor();
		}

		public ICryptoTransform CreateDecryptor() {
			return _aes.CreateDecryptor();
		}
	}
}
