// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors — fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using System;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Configuration;
using Terminals.Data;
namespace Terminals.Plugins.SshNet
{
    internal static class SshCredentialGate
    {
        internal static bool TryPrepareSessionCredentials(
            IGuardedSecurity resolved,
            SshOptions sshOptions,
            KeysSection sshKeys,
            string host,
            ICredentialPromptService promptService,
            IWin32Window owner,
            out SshSessionCredentials session,
            out string error)
        {
            session = null;
            error = null;

            if (resolved == null)
            {
                error = "SSH credentials are not available.";
                return false;
            }

            AuthMethod authMethod = sshOptions != null ? sshOptions.AuthMethod : AuthMethod.Password;
            string userName = resolved.UserName ?? string.Empty;
            string password = resolved.Password ?? string.Empty;
            string domain = resolved.Domain ?? string.Empty;

            if (NeedsInteractivePrompt(authMethod, sshOptions, sshKeys, userName, password))
            {
                if (promptService == null)
                {
                    if (!Validate(authMethod, sshOptions, sshKeys, userName, password, out error))
                        return false;

                    error = "SSH login or password is required. Use Connect As or set credentials on the favorite.";
                    return false;
                }

                ConnectionCredentialPromptResult prompt = promptService.PromptForSshConnection(
                    owner,
                    host,
                    userName,
                    password,
                    domain);

                if (!prompt.Success)
                {
                    error = "SSH sign-in was cancelled.";
                    return false;
                }

                userName = prompt.UserName ?? string.Empty;
                password = prompt.Password ?? string.Empty;
                domain = prompt.Domain ?? string.Empty;
            }

            if (!Validate(authMethod, sshOptions, sshKeys, userName, password, out error))
                return false;

            session = new SshSessionCredentials
            {
                UserName = userName.Trim(),
                Password = password,
                Domain = domain
            };
            return true;
        }

        private static bool NeedsInteractivePrompt(
            AuthMethod authMethod,
            SshOptions sshOptions,
            KeysSection sshKeys,
            string userName,
            string password)
        {
            switch (authMethod)
            {
                case AuthMethod.PublicKey:
                    return string.IsNullOrWhiteSpace(userName);

                case AuthMethod.KeyboardInteractive:
                    return string.IsNullOrWhiteSpace(userName);

                case AuthMethod.Host:
                    if (HasKeySource(sshOptions) && !string.IsNullOrWhiteSpace(userName))
                        return false;
                    return string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password);

                case AuthMethod.Password:
                default:
                    return string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password);
            }
        }

        private static bool Validate(
            AuthMethod authMethod,
            SshOptions sshOptions,
            KeysSection sshKeys,
            string userName,
            string password,
            out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(userName))
            {
                error = "SSH user name is required.";
                return false;
            }

            switch (authMethod)
            {
                case AuthMethod.PublicKey:
                    if (!HasKeySource(sshOptions))
                    {
                        error = "SSH public key authentication requires KeyTag or KeyFile.";
                        return false;
                    }

                    return true;

                case AuthMethod.KeyboardInteractive:
                    return true;

                case AuthMethod.Host:
                    if (HasKeySource(sshOptions))
                        return true;
                    if (string.IsNullOrEmpty(password))
                    {
                        error = "SSH password or private key is required.";
                        return false;
                    }

                    return true;

                case AuthMethod.Password:
                default:
                    if (string.IsNullOrEmpty(password))
                    {
                        error = "SSH password is required.";
                        return false;
                    }

                    return true;
            }
        }

        private static bool HasKeySource(SshOptions sshOptions)
        {
            if (sshOptions == null)
                return false;

            return !string.IsNullOrEmpty(sshOptions.KeyTag) || !string.IsNullOrEmpty(sshOptions.KeyFile);
        }
    }
}
