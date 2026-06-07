// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Terminals.Common.Connections;

namespace Terminals.Plugins.SshNet
{
    internal class SshNetOptionsConverter : OptionsConverterTemplate<SshOptions>, IOptionsConverter
    {
        protected override void FromConfigFavorite(FavoriteConfigurationElement source, SshOptions options)
        {
            options.SessionName = source.SshSessionName;
            options.Verbose = source.SshVerbose;
            options.EnablePagentAuthentication = source.SshEnablePagentAuthentication;
            options.EnablePagentForwarding = source.SshEnablePagentForwarding;
            options.X11Forwarding = source.SshX11Forwarding;
            options.EnableCompression = source.SshEnableCompression;
            options.SshVersion = (SshVersion)source.SshVersion;
            options.ConnectionProfile = (SshConnectionProfile)source.SshConnectionProfile;
            options.AuthMethod = source.AuthMethod;
            options.KeyTag = source.KeyTag;
            options.KeyFile = source.SSHKeyFile;
        }

        protected override void ToConfigFavorite(FavoriteConfigurationElement destination, SshOptions options)
        {
            destination.SshSessionName = options.SessionName;
            destination.SshVerbose = options.Verbose;
            destination.SshEnablePagentAuthentication = options.EnablePagentAuthentication;
            destination.SshEnablePagentForwarding = options.EnablePagentForwarding;
            destination.SshX11Forwarding = options.X11Forwarding;
            destination.SshEnableCompression = options.EnableCompression;
            destination.SshVersion = (byte)options.SshVersion;
            destination.SshConnectionProfile = (byte)options.ConnectionProfile;
            destination.AuthMethod = options.AuthMethod;
            destination.KeyTag = options.KeyTag ?? string.Empty;
            destination.SSHKeyFile = options.KeyFile ?? string.Empty;
        }
    }
}
