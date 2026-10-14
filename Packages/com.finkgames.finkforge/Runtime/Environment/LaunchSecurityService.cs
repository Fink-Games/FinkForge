using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FinkForge.Environment
{
    internal sealed class LaunchSecurityService
    {
        private const string AlgorithmHeaderName = "alg";
        private static readonly byte[] Sha256DigestInfoPrefix =
        {
            0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
            0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05,
            0x00, 0x04, 0x20
        };

        internal bool Validate(FinkEnvironment environment, out string error)
        {
            if (environment == null)
            {
                error = "Launch environment is unavailable.";
                return false;
            }

            var parts = environment.AccessToken?.Split('.');
            if (parts == null || parts.Length != 3)
            {
                error = "Access token format is invalid.";
                return false;
            }

            try
            {
                var header = JsonUtility.FromJson<TokenHeader>(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                var payload = JsonUtility.FromJson<TokenPayload>(payloadJson);
                var signature = Base64UrlDecode(parts[2]);

                if (header == null || payload == null || string.IsNullOrWhiteSpace(header.alg))
                {
                    error = "Access token header or payload is invalid.";
                    return false;
                }

                if (!string.Equals(header.alg, environment.AccessTokenAlgorithm, StringComparison.Ordinal) ||
                    !string.Equals(header.alg, "RS256", StringComparison.Ordinal))
                {
                    error = "Access token algorithm is invalid.";
                    return false;
                }

                if (!VerifyRs256Signature(environment.AccessTokenPublicKeyPem, parts[0] + "." + parts[1], signature))
                {
                    error = "Access token signature is invalid.";
                    return false;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!HasJsonProperty(payloadJson, "exp") || payload.exp + environment.TokenClockSkewSeconds <= now)
                {
                    error = "Access token is expired or has no expiration.";
                    return false;
                }

                if (HasJsonProperty(payloadJson, "nbf") && payload.nbf - environment.TokenClockSkewSeconds > now)
                {
                    error = "Access token is not active yet.";
                    return false;
                }

                if (!ClaimEquals(payload.iss, environment.AccessTokenIssuer))
                    return FailClaim("issuer", out error);
                if (!ClaimEquals(payload.aud, environment.AccessTokenAudience))
                    return FailClaim("audience", out error);
                if (!ClaimEquals(payload.purpose, environment.AccessTokenPurpose))
                    return FailClaim("purpose", out error);
                if (!ClaimEqualsAny(payload.installation, payload.installation_id, environment.InstallationId))
                    return FailClaim("installation", out error);
                if (!ClaimEqualsAny(payload.device, payload.device_id, environment.DeviceId))
                    return FailClaim("device", out error);
                if (!ClaimEquals(payload.nonce, environment.LaunchNonce))
                    return FailClaim("nonce", out error);

                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is CryptographicException || exception is ArgumentException)
            {
                error = "Access token could not be validated.";
                return false;
            }
        }

        private static bool ClaimEquals(string actual, string expected)
        {
            return !string.IsNullOrWhiteSpace(actual) && !string.IsNullOrWhiteSpace(expected) &&
                   string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static bool ClaimEqualsAny(string first, string second, string expected)
        {
            return ClaimEquals(first, expected) || ClaimEquals(second, expected);
        }

        private static bool FailClaim(string claimName, out string error)
        {
            error = "Access token " + claimName + " claim does not match the launch environment.";
            return false;
        }

        private static bool HasJsonProperty(string json, string propertyName)
        {
            return !string.IsNullOrWhiteSpace(json) && json.Contains("\"" + propertyName + "\"");
        }

        private static bool VerifyRs256Signature(string publicKeyPem, string signedContent, byte[] signature)
        {
            var key = ParsePublicKey(publicKeyPem);
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
                return VerifyPkcs1Sha256Signature(key.modulus, key.exponent, hash, signature);
            }
        }

        private static bool VerifyPkcs1Sha256Signature(byte[] modulus, byte[] exponent, byte[] hash, byte[] signature)
        {
            var keyLength = modulus.Length;
            if (keyLength < Sha256DigestInfoPrefix.Length + hash.Length + 11 || signature.Length > keyLength) return false;
            var signatureBlock = LeftPad(signature, keyLength);
            var decryptedNumber = BigInteger.ModPow(ToPositiveBigInteger(signatureBlock), ToPositiveBigInteger(exponent), ToPositiveBigInteger(modulus));
            var decryptedBlock = ToFixedLengthBytes(decryptedNumber, keyLength);
            var digestInfoLength = Sha256DigestInfoPrefix.Length + hash.Length;
            var paddingLength = keyLength - digestInfoLength - 3;
            var expectedBlock = new byte[keyLength];
            expectedBlock[0] = 0x00;
            expectedBlock[1] = 0x01;
            for (var index = 0; index < paddingLength; index++) expectedBlock[2 + index] = 0xff;
            var separatorIndex = 2 + paddingLength;
            Buffer.BlockCopy(Sha256DigestInfoPrefix, 0, expectedBlock, separatorIndex + 1, Sha256DigestInfoPrefix.Length);
            Buffer.BlockCopy(hash, 0, expectedBlock, separatorIndex + 1 + Sha256DigestInfoPrefix.Length, hash.Length);
            return FixedTimeEquals(decryptedBlock, expectedBlock);
        }

        private static (byte[] modulus, byte[] exponent) ParsePublicKey(string publicKeyPem)
        {
            var root = new DerReader(DecodePem(publicKeyPem)).ReadSequence();
            if (root.PeekTag() == DerReader.SequenceTag)
            {
                root.ReadSequence();
                var bitString = root.ReadBitString();
                return ReadRsaPublicKey(new DerReader(bitString).ReadSequence());
            }
            return ReadRsaPublicKey(root);
        }

        private static (byte[] modulus, byte[] exponent) ReadRsaPublicKey(DerReader reader)
        {
            return (TrimUnsignedInteger(reader.ReadInteger()), TrimUnsignedInteger(reader.ReadInteger()));
        }

        private static byte[] DecodePem(string pem)
        {
            var normalized = pem.Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
                .Replace("-----END PUBLIC KEY-----", string.Empty)
                .Replace("-----BEGIN RSA PUBLIC KEY-----", string.Empty)
                .Replace("-----END RSA PUBLIC KEY-----", string.Empty)
                .Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", string.Empty).Trim();
            return Convert.FromBase64String(normalized);
        }

        private static byte[] TrimUnsignedInteger(byte[] value)
        {
            var index = 0;
            while (index < value.Length - 1 && value[index] == 0) index++;
            if (index == 0) return value;
            var trimmed = new byte[value.Length - index];
            Buffer.BlockCopy(value, index, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static BigInteger ToPositiveBigInteger(byte[] value)
        {
            var littleEndian = new byte[value.Length + 1];
            for (var index = 0; index < value.Length; index++) littleEndian[index] = value[value.Length - index - 1];
            return new BigInteger(littleEndian);
        }

        private static byte[] ToFixedLengthBytes(BigInteger value, int length)
        {
            var littleEndian = value.ToByteArray();
            var result = new byte[length];
            var count = Math.Min(littleEndian.Length, length);
            for (var index = 0; index < count; index++) result[length - index - 1] = littleEndian[index];
            return result;
        }

        private static byte[] LeftPad(byte[] value, int length)
        {
            if (value.Length == length) return value;
            var result = new byte[length];
            Buffer.BlockCopy(value, 0, result, length - value.Length, value.Length);
            return result;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            if (padded.Length % 4 == 2) padded += "==";
            else if (padded.Length % 4 == 3) padded += "=";
            return Convert.FromBase64String(padded);
        }

        [Serializable] private sealed class TokenHeader { public string alg; }
        [Serializable] private sealed class TokenPayload
        {
            public string iss;
            public string aud;
            public long exp;
            public long nbf;
            public string purpose;
            public string installation;
            public string installation_id;
            public string device;
            public string device_id;
            public string nonce;
        }

        private sealed class DerReader
        {
            public const byte SequenceTag = 0x30;
            private const byte IntegerTag = 0x02;
            private const byte BitStringTag = 0x03;
            private readonly byte[] data;
            private int position;

            public DerReader(byte[] data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }
            public byte PeekTag() { if (position >= data.Length) throw new CryptographicException("Invalid public key."); return data[position]; }
            public DerReader ReadSequence() { return new DerReader(ReadTag(SequenceTag)); }
            public byte[] ReadInteger() { return ReadTag(IntegerTag); }
            public byte[] ReadBitString()
            {
                var content = ReadTag(BitStringTag);
                if (content.Length == 0 || content[0] != 0) throw new CryptographicException("Invalid public key.");
                var result = new byte[content.Length - 1];
                Buffer.BlockCopy(content, 1, result, 0, result.Length);
                return result;
            }

            private byte[] ReadTag(byte expectedTag)
            {
                if (PeekTag() != expectedTag) throw new CryptographicException("Invalid public key.");
                position++;
                var length = ReadLength();
                if (length < 0 || position + length > data.Length) throw new CryptographicException("Invalid public key.");
                var content = new byte[length];
                Buffer.BlockCopy(data, position, content, 0, length);
                position += length;
                return content;
            }

            private int ReadLength()
            {
                if (position >= data.Length) throw new CryptographicException("Invalid public key.");
                var lengthByte = data[position++];
                if ((lengthByte & 0x80) == 0) return lengthByte;
                var byteCount = lengthByte & 0x7f;
                if (byteCount == 0 || byteCount > 4 || position + byteCount > data.Length) throw new CryptographicException("Invalid public key.");
                var length = 0;
                for (var index = 0; index < byteCount; index++) length = (length << 8) | data[position++];
                return length;
            }
        }
    }
}
