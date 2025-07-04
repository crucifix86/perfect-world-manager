// PerfectWorldManager.Core\Utils\PasswordHelper.cs
using System;
using System.Security.Cryptography;
using System.Text;

namespace PerfectWorldManager.Core.Utils
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Replicates the pw_encode function from the JSP script.
        /// Hashes (MD5) the lowercase login + password, then Base64 encodes the hash.
        /// </summary>
        /// <param name="login">The account login name.</param>
        /// <param name="password">The account password.</param>
        /// <returns>The Base64 encoded MD5 hash string.</returns>
        public static string PwEncode(string login, string password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                // Or throw ArgumentNullException, depending on desired handling
                return string.Empty;
            }

            string salt = login.ToLower() + password;
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(salt);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Validates if the given string contains only allowed characters.
        /// Allowed: 0-9, a-z, A-Z, -, _
        /// </summary>
        public static bool IsValidCharacterSet(string input)
        {
            if (string.IsNullOrEmpty(input)) return false; // Or true if empty is allowed for some fields

            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";
            foreach (char c in input)
            {
                if (alphabet.IndexOf(c) == -1)
                {
                    return false;
                }
            }
            return true;
        }
    }
}