using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GBTools.Common
{
    public interface IRandomIdService
    {
        string GenerateRandomId();
    }

    public class RandomIdService : IRandomIdService
    {
        public string GenerateRandomId()
        {
            // Using RNGCryptoServiceProvider for a more cryptographically secure random number

            using var rng = RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[8]; // 64 bits
            rng.GetBytes(randomBytes);
            return BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
        }
    }
}
