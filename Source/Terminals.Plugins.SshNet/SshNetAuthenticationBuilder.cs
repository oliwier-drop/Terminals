using System;
using System.Collections.Generic;
using System.IO;
using Renci.SshNet;
using Terminals.Common.Configuration;
using Terminals.Configuration;
using Terminals.Data;
using Terminals.Plugins.Putty;

namespace Terminals.Plugins.SshNet
{
    internal static class SshNetAuthenticationBuilder
    {
        internal static bool TryBuildMethods(
            string userName,
            string password,
            SshOptions sshOptions,
            KeysSection sshKeys,
            System.Windows.Forms.IWin32Window owner,
            out AuthenticationMethod[] methods,
            out string error)
        {
            methods = null;
            error = null;

            AuthMethod authMethod = sshOptions != null ? sshOptions.AuthMethod : AuthMethod.Password;
            string keyTag = sshOptions != null ? sshOptions.KeyTag : null;
            string keyFile = sshOptions != null ? sshOptions.KeyFile : null;

            var list = new List<AuthenticationMethod>();

            switch (authMethod)
            {
                case AuthMethod.PublicKey:
                    return TryAddPublicKeyOnly(userName, password, keyTag, keyFile, sshKeys, list, out methods, out error);

                case AuthMethod.KeyboardInteractive:
                    if (!ValidateUserName(userName, out error))
                        return false;
                    list.Add(SshNetKeyboardInteractiveHandler.Create(userName, password, owner));
                    methods = list.ToArray();
                    return true;

                case AuthMethod.Password:
                    TryAddDefaultUserPrivateKeys(userName, password, list);
                    if (!TryAddPasswordMethods(userName, password, owner, list, true, false, out error))
                        return false;
                    methods = list.ToArray();
                    return true;

                case AuthMethod.Host:
                    TryAddPublicKey(userName, password, keyTag, keyFile, sshKeys, list, out error);
                    TryAddPasswordMethods(userName, password, owner, list, false, true, out error);
                    if (list.Count == 0)
                    {
                        if (!ValidateUserName(userName, out error))
                            return false;
                        list.Add(SshNetKeyboardInteractiveHandler.Create(userName, password ?? string.Empty, owner));
                    }

                    if (list.Count == 0)
                    {
                        error = error ?? "No authentication method is available for this connection.";
                        return false;
                    }

                    methods = list.ToArray();
                    return true;

                default:
                    if (!TryAddPasswordMethods(userName, password, owner, list, true, false, out error))
                        return false;
                    methods = list.ToArray();
                    return true;
            }
        }

        private static bool ValidateUserName(string userName, out string error)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                error = "SSH user name is required.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryAddPublicKeyOnly(
            string userName,
            string password,
            string keyTag,
            string keyFile,
            KeysSection sshKeys,
            List<AuthenticationMethod> list,
            out AuthenticationMethod[] methods,
            out string error)
        {
            if (!ValidateUserName(userName, out error))
            {
                methods = null;
                return false;
            }

            PrivateKeyAuthenticationMethod keyMethod;
            if (!SshNetPrivateKeyLoader.TryLoadPrivateKey(userName, keyTag, keyFile, password, sshKeys, out keyMethod, out error))
            {
                methods = null;
                return false;
            }

            list.Add(keyMethod);
            methods = list.ToArray();
            return true;
        }

        private static void TryAddPublicKey(
            string userName,
            string password,
            string keyTag,
            string keyFile,
            KeysSection sshKeys,
            List<AuthenticationMethod> list,
            out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(keyTag) && string.IsNullOrEmpty(keyFile))
                return;

            if (string.IsNullOrWhiteSpace(userName))
                return;

            PrivateKeyAuthenticationMethod keyMethod;
            if (SshNetPrivateKeyLoader.TryLoadPrivateKey(userName, keyTag, keyFile, password, sshKeys, out keyMethod, out error))
                list.Add(keyMethod);
        }

        private static void TryAddDefaultUserPrivateKeys(string userName, string passphrase, List<AuthenticationMethod> list)
        {
            string sshDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            if (!Directory.Exists(sshDirectory))
                return;

            string[] keyFiles = { "id_ed25519", "id_rsa", "id_ecdsa" };
            foreach (string keyFileName in keyFiles)
            {
                string path = Path.Combine(sshDirectory, keyFileName);
                if (!File.Exists(path))
                    continue;

                PrivateKeyAuthenticationMethod keyMethod;
                string error;
                if (SshNetPrivateKeyLoader.TryLoadPrivateKey(userName, null, path, passphrase, null, out keyMethod, out error))
                    list.Add(keyMethod);
            }
        }

        private static bool TryAddPasswordMethods(
            string userName,
            string password,
            System.Windows.Forms.IWin32Window owner,
            List<AuthenticationMethod> list,
            bool required,
            bool includeKeyboardInteractive,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(userName))
            {
                if (!required)
                    return true;

                error = "SSH user name is required.";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                if (!required)
                    return true;

                error = "SSH password is required.";
                return false;
            }

            if (includeKeyboardInteractive)
                list.Add(SshNetKeyboardInteractiveHandler.Create(userName, password, owner));

            list.Add(new PasswordAuthenticationMethod(userName, password));
            return true;
        }
    }
}
