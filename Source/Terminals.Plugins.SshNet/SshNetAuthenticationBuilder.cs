using System.Collections.Generic;
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
                    list.Add(SshNetKeyboardInteractiveHandler.Create(userName, password, owner));
                    methods = list.ToArray();
                    return true;

                case AuthMethod.Password:
                    AddPasswordOrNone(userName, password, list);
                    methods = list.ToArray();
                    return true;

                case AuthMethod.Host:
                    TryAddPublicKey(userName, password, keyTag, keyFile, sshKeys, list, out error);
                    AddPasswordOrNone(userName, password, list);
                    list.Add(SshNetKeyboardInteractiveHandler.Create(userName, password, owner));
                    if (list.Count == 0)
                    {
                        error = error ?? "No authentication method is available for this connection.";
                        return false;
                    }

                    methods = list.ToArray();
                    return true;

                default:
                    AddPasswordOrNone(userName, password, list);
                    if (list.Count == 0)
                    {
                        error = error ?? "No authentication method is available for this connection.";
                        return false;
                    }

                    methods = list.ToArray();
                    return true;
            }
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

            PrivateKeyAuthenticationMethod keyMethod;
            if (SshNetPrivateKeyLoader.TryLoadPrivateKey(userName, keyTag, keyFile, password, sshKeys, out keyMethod, out error))
                list.Add(keyMethod);
        }

        private static void AddPasswordOrNone(string userName, string password, List<AuthenticationMethod> list)
        {
            if (!string.IsNullOrEmpty(password))
                list.Add(new PasswordAuthenticationMethod(userName, password));
            else if (list.Count == 0)
                list.Add(new NoneAuthenticationMethod(userName));
        }
    }
}
