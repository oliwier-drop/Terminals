using System;
using System.IO;
using System.Text;
using Renci.SshNet;
using Terminals.Common.Configuration;
using Terminals.Configuration;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetPrivateKeyLoader
    {
        internal static bool TryLoadPrivateKey(
            string userName,
            string keyTag,
            string keyFilePath,
            string passphrase,
            KeysSection sshKeys,
            out PrivateKeyAuthenticationMethod method,
            out string error)
        {
            method = null;
            error = null;

            PrivateKeyFile keyFile;
            if (!TryOpenKeyFile(keyTag, keyFilePath, passphrase, sshKeys, out keyFile, out error))
                return false;

            try
            {
                method = new PrivateKeyAuthenticationMethod(userName, keyFile);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryOpenKeyFile(
            string keyTag,
            string keyFilePath,
            string passphrase,
            KeysSection sshKeys,
            out PrivateKeyFile keyFile,
            out string error)
        {
            keyFile = null;
            error = null;

            if (!string.IsNullOrEmpty(keyFilePath))
            {
                if (!File.Exists(keyFilePath))
                {
                    error = "Private key file was not found: " + keyFilePath;
                    return false;
                }

                try
                {
                    keyFile = string.IsNullOrEmpty(passphrase)
                        ? new PrivateKeyFile(keyFilePath)
                        : new PrivateKeyFile(keyFilePath, passphrase);
                    return true;
                }
                catch (Exception exception)
                {
                    error = "Unable to load private key file: " + exception.Message;
                    return false;
                }
            }

            if (string.IsNullOrEmpty(keyTag) || sshKeys == null)
            {
                error = "No private key file or key tag was specified.";
                return false;
            }

            try
            {
                KeyConfigElement keyElement = sshKeys.Keys[keyTag];
                if (keyElement == null)
                {
                    error = "SSH key tag was not found in the key store: " + keyTag;
                    return false;
                }

                string keyMaterial = keyElement.Key;
                if (string.IsNullOrEmpty(keyMaterial))
                {
                    error = "SSH key tag was not found in the key store: " + keyTag;
                    return false;
                }

                byte[] keyBytes = Encoding.UTF8.GetBytes(keyMaterial);
                using (var stream = new MemoryStream(keyBytes))
                {
                    keyFile = string.IsNullOrEmpty(passphrase)
                        ? new PrivateKeyFile(stream)
                        : new PrivateKeyFile(stream, passphrase);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to load private key from store: " + exception.Message;
                return false;
            }
        }
    }
}
