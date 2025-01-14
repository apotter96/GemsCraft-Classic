﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>

using System.Diagnostics;
using GemsCraft.fSystem;
using GemsCraft.Players;
using GemsCraft.Utils;
using ServiceStack.Text;

namespace GemsCraft.Configuration {
    /// <summary> Enumeration of available configuration keys. See comments
    /// at the top of Config.cs for a history of changes. </summary>
    public enum ConfigKey {
        #region General

        [StringKey( ConfigSection.General, "[GemsCraft] Default",
@"The name of the server, as shown on the welcome screen and the
official server list (if server is public).",
            MinLength = 1, MaxLength = 64 )]
        ServerName,

        [StringKey(ConfigSection.General, "EngineerChat",
@"The name of the custom chat channel",
            MinLength = 1, MaxLength = 12)]
        CustomChatName,

        [StringKey(ConfigSection.General, "En",
@"The name of the custom chat alias",
            MinLength = 1, MaxLength = 12)]
        CustomAliasName,
        
        [StringKey(ConfigSection.General, "WebsiteURL",
@"The website used for the server. Used for $website and /BanGrief",
MinLength = 1, MaxLength = 64)]
        WebsiteURL,

        [StringKey(ConfigSection.General, "True",
@"Determines if the server should check for updates", MinLength = 4, MaxLength = 5)]
        CheckForUpdates,

        [StringKey(ConfigSection.General, "%CBlock",
@"The word which swears will be replaced with",
            MinLength = 1, MaxLength = 12)]
        SwearName,


        [StringKey( ConfigSection.General, "Welcome to the server!",
@"MOTD (Message Of The Day) is a message shown to connecting players 
right under the server name. May be left blank.
Note: If WoM extensions are enabled, non-WoM users will not see this.",
            MinLength = 0, MaxLength = 64 )]
        MOTD,


        [IntKey( ConfigSection.General, 20,
@"Maximum number of players on the server. Having more players
uses more RAM and more bandwidth. If a player's rank is given a
""reserved slot"" on the server, they can join even if server is full.",
            MinValue = 1, MaxValue = 1000 )]
        MaxPlayers,

        [IntKey(ConfigSection.General, 8,
@"Maximum number Caps a player is allowed to chat with.",
            MinValue = 1, MaxValue = 12)]
        MaxCaps,


        [IntKey( ConfigSection.General, 20,
@"Maximum number of players allowed to be on the same world at the same time.
Minecraft protocol limits total number of players per world to 128.
Note that having more people on a world increases everyone's bandwidth use.",
            MinValue = 1, MaxValue = 128 )]
        MaxPlayersPerWorld,


        [RankKey( ConfigSection.General, RankKeyAttribute.BlankValueMeaning.LowestRank,
@"New players will be assigned this rank by default.
It's generally a good idea not to give new players
many powers until they prove themselves trustworthy." )]
        DefaultRank,


        [BoolKey( ConfigSection.General, false,
            @"Public servers are listed on minecraft.net server list, so expect
random players to join. Private servers can only be joined by players
who already know the server port/address or URL. Note that the URL
changes if your computer's IP or server's port change." )]
        IsPublic,


        [IntKey( ConfigSection.General, 25565,
@"Port number on your local machine that GemsCraft uses to listen for
incoming connections. If you are behind a router, you may need
to set up port forwarding. You may also need to add a firewall
exception for ServerCLI/GemsCraft. Note that your
server's URL will change if you change the port number.",
            MinValue = 1, MaxValue = 65535 )]
        Port,

        [IntKey( ConfigSection.General, 100,
@"Total available upload bandwidth, in kilobytes. This number
is used to pace drawing commands to prevent server from
overwhelming the Internet connection with data.",
            MinValue = 1, MaxValue = short.MaxValue )]
        UploadBandwidth,

        #endregion


        #region Chat

        [BoolKey( ConfigSection.Chat, true,
@"Color player names in chat and in-game based on their rank." )]
        RankColorsInChat,

        [BoolKey( ConfigSection.Chat, true,
@"Color world names in chat based on their build and access permissions." )]
        RankColorsInWorldNames,

        [BoolKey( ConfigSection.Chat, false,
@"Show 1-character prefixes in chat before player names. This can be
used to set up IRC-style ""+"" and ""@"" prefixes for ops." )]
        RankPrefixesInChat,

        [BoolKey( ConfigSection.Chat, false,
@"Show prefixes in the player list. As a side-effect, Minecraft client
will not show custom skins for players with prefixed names." )]
        RankPrefixesInList,

        [BoolKey( ConfigSection.Chat, true,
@"Announce players joining or leaving the server in chat." )]
        ShowConnectionMessages,

        [BoolKey( ConfigSection.Chat, true,
@"Show messages when players change worlds." )]
        ShowJoinedWorldMessages,

        [ColorKey( ConfigSection.Chat, Color.SysDefault,
@"Color of normal system messages." )]
        SystemMessageColor,
        [ColorKey(ConfigSection.Chat, Color.CustomDefault,
@"Color of custom chat channel.")]
        CustomChatColor,

        [ColorKey( ConfigSection.Chat, Color.HelpDefault,
@"Color of command usage examples in help." )]
        HelpColor,

        [ColorKey( ConfigSection.Chat, Color.SayDefault,
@"Color of messages produced by ""/Say"" command." )]
        SayColor,

        [ColorKey( ConfigSection.Chat, Color.AnnouncementDefault,
@"Color of announcements and rules. Default is dark-green.
Note that this default color can be overridden by
colorcodes in announcement and rule files." )]
        AnnouncementColor,

        [ColorKey( ConfigSection.Chat, Color.PMDefault,
@"Color of private and rank-wide messages." )]
        PrivateMessageColor,

        [ColorKey( ConfigSection.Chat, Color.MeDefault,
@"Color of ""/Me"" command messages." )]
        MeColor,

        [ColorKey( ConfigSection.Chat, Color.WarningDefault,
@"Color of error and warning messages." )]
        WarningColor,

        [ColorKey(ConfigSection.Chat, Color.GlobalDefault,
@"Color of global messages.")]
        GlobalColor,

        [IntKey( ConfigSection.Chat, 0,
@"Announcement interval, in minutes. Set to 0 to disable announcements.
Announcements are shown to all players, one line at a time, in random order.",
            MinValue = 0 )]
        AnnouncementInterval,

        #endregion


        #region Worlds

        [RankKey( ConfigSection.Worlds, RankKeyAttribute.BlankValueMeaning.DefaultRank,
@"When new worlds are created with the /WLoad command,
the build permission for new worlds will default to this rank." )]
        DefaultBuildRank,

        [StringKey( ConfigSection.Worlds, "maps",
@"Custom path for storing map files. If you change this value,
make sure to move the map files before starting the server again." )]
        MapPath,

        [StringKey(ConfigSection.Worlds, "",
@"Path for custom texture pack.")]
        TexturePath,
        #endregion


        #region Security

        [EnumKey( ConfigSection.Security, NameVerificationMode.Balanced,
@"Name verification ensures that connecting players are not impersonating
someone else. Strict verification uses only the main verification method.
It can produce false negatives - for example if server has just restarted,
or if minecraft.net heartbeats are timing out. Normal verification checks
player's current and on-record IP address to eliminate false negatives.
No verification is dangerous, and may allow players to impersonate others." )]
        VerifyNames,

        [IntKey( ConfigSection.Security, 0,
@"Restricts the number of connections allowed from any one IP address.
Note that all players on the same LAN will share an IP, and may be prevented
from joining together.",
            MinValue = 0 )]
        MaxConnectionsPerIP,

        [BoolKey( ConfigSection.Security, false,
@"Allow players from your local network (LAN) to connect without name verification.
May be useful if minecraft.net is blocked on your LAN for some reason.
Warning: Unverified players can log in with ANY name - even as you!" )]
        AllowUnverifiedLAN,

        [RankKey( ConfigSection.Security, RankKeyAttribute.BlankValueMeaning.DefaultRank,
@"When players use /Patrol or /SpecPatrol commands, they will be teleported
to players of this (or lower) rank. ""Patrolling"" means teleporting
to other players to check on them, usually while hidden." )]
        PatrolledRank,

        [IntKey( ConfigSection.Security, 4,
@"Number of messages that a player needs to type to trigger the AntiSpam warning.
Set this to 0 to disable AntiSpam.",
            AlwaysAllowZero = true, MinValue = 2, MaxValue = 64 )]
        AntispamMessageCount,

        [IntKey( ConfigSection.Security, 5,
@"Number of seconds over which the player needs to type messages to trigger AntiSpam warning.
Set this to 0 to disable AntiSpam.",
            AlwaysAllowZero = true, MinValue = 1, MaxValue = 64 )]
        AntispamInterval,

        [IntKey( ConfigSection.Security, 5,
@"Duration of automatic mute if AntiSpam is triggered, in seconds.
Set this to 0 to disable automatic mute (and only leave the warning).",
            MinValue = 0, MaxValue = 86400 )]
        AntispamMuteDuration,

        [IntKey( ConfigSection.Security, 2,
@"Number of warnings given to a player (number of times AntiSpam is triggered)
before the player is kicked. Set this to 0 to disable automatic kicks.",
            AlwaysAllowZero = true, MinValue = 0, MaxValue = 64 )]
        AntispamMaxWarnings,


        [BoolKey( ConfigSection.Security, false,
@"Only allow players who have a paid Minecraft account (not recommended).
This will help filter out griefers with throwaway accounts,
but will also prevent many legitimate players from joining." )]
        PaidPlayersOnly,


        [BoolKey( ConfigSection.Security, false,
@"Require players to specify a reason/memo when banning or unbanning someone." )]
        RequireBanReason,

        [BoolKey( ConfigSection.Security, false,
@"Require players to specify a reason/memo when kicking someone." )]
        RequireKickReason,

        [BoolKey( ConfigSection.Security, false,
@"Require players to specify a reason/memo when promoting or demoting someone." )]
        RequireRankChangeReason,


        [BoolKey( ConfigSection.Security, true,
@"Announce the reason/memo in chat when someone gets kicked/banned/unbanned." )]
        AnnounceKickAndBanReasons,

        [BoolKey( ConfigSection.Security, true,
@"Announce promotions and demotions in chat." )]
        AnnounceRankChanges,

        [BoolKey( ConfigSection.Security, true,
@"Announce the reason/memo in chat when someone gets promoted or demoted." )]
        AnnounceRankChangeReasons,

        [BoolKey( ConfigSection.Security, true,
@"If enabled, allows edit information to be stored per-block. Enables /BlockDB, /BInfo, /UndoArea, and /UndoPlayer commands." )]
        BlockDBEnabled,

        [BoolKey( ConfigSection.Security, true,
@"Automatically enabled BlockDB on all worlds editable by a certain rank or below." )]
        BlockDBAutoEnable,

        [RankKey( ConfigSection.Security, RankKeyAttribute.BlankValueMeaning.DefaultRank,
@"Worlds editable by players of this rank (or below) will have BlockDB enabled automatically.
Has no effect until BlockDBAutoEnable key is set." )]
        BlockDBAutoEnableRank,

        #endregion


        #region Saving and Backup

        [IntKey( ConfigSection.SavingAndBackup, 90,
@"Whether to save maps (if modified) automatically once in a while.
If disabled, maps are only saved when a world is unloaded or server is shut down.
A higher setting (120+ seconds) is recommended for busy servers with many maps.",
            AlwaysAllowZero = true, MinValue = 10 )]
        SaveInterval,

        [BoolKey( ConfigSection.SavingAndBackup, false,
@"Whether to create a backup of every map when the server starts." )]
        BackupOnStartup,

        [BoolKey( ConfigSection.SavingAndBackup, false,
@"Create backups any time a player joins a map.
Both a timestamp and player's name are included in the filename." )]
        BackupOnJoin,

        [BoolKey( ConfigSection.SavingAndBackup, true,
@"Only create backups if the map was modified since last backup." )]
        BackupOnlyWhenChanged,

        [IntKey( ConfigSection.SavingAndBackup, 20,
@"Default interval for saving periodic map backups for loaded worlds.
A world is considered ""loaded"" if there is at least one player on it.
This setting can be overridden on a per-world basis.
Set to 0 to disable periodic backups.",
            MinValue = 0 )]
        DefaultBackupInterval,

        [IntKey( ConfigSection.SavingAndBackup, 0,
@"Maximum number of backup files that 800Craft should keep.
If exceeded, oldest backups will be deleted first.",
            MinValue = 0 )]
        MaxBackups,

        [IntKey( ConfigSection.SavingAndBackup, 0,
@"Maximum combined filesize of all backups, in MB.
If exceeded, oldest backups will be deleted first.",
            MinValue = 0 )]
        MaxBackupSize,

        [BoolKey( ConfigSection.SavingAndBackup, true,
@"Create backups of server data (PlayerDB and IPBanList) on startup." )]
        BackupDataOnStartup,
        [BoolKey(ConfigSection.SavingAndBackup, true,
@"Starts the heartbeatsaver on shutdown")]
        HbSaverKey,

        [BoolKey(ConfigSection.Chat, true,
@"Enables Global Chat (/Global) for IRC channel #800Craft esper.net")]
        GCKey,

        #endregion


        #region Logging

        [EnumKey( ConfigSection.Logging, LogSplittingType.OneFile,
@"The way log files are organized." )]
        LogMode,

        [IntKey( ConfigSection.Logging, 0,
@"Maximum number of log files to keep.
If exceeded, oldest logs will be erased first. Set this to 0 to keep all logs.",
            MinValue = 0 )]
        MaxLogs,

        #endregion


        #region IRC

        [BoolKey( ConfigSection.IRC, false,
@"GemsCraft includes an IRC (Internet Relay Chat) client for
relaying messages to and from any IRC network.
Note that encrypted IRC (via SSL) is not supported." )]
        IRCBotEnabled,

        [StringKey( ConfigSection.IRC, "MinecraftBot",
@"IRC bot's nickname. If the nickname is taken, 800Craft will append
an underscore (_) to the name and retry.",
            MinLength = 1, MaxLength = 32 )]
        IRCBotNick,

        [StringKey( ConfigSection.IRC, "irc.esper.net",
@"Host or address of the IRC network." )]
        IRCBotNetwork,

        [StringKey(ConfigSection.IRC, "defaultPass",
@"Optional, password of the IRC network.")]
        IRCBotNetworkPass,

        [IntKey( ConfigSection.IRC, 6667,
@"Port number of the IRC network. Most networks use port 6667.",
            MinValue = 1, MaxValue = 65535 )]
        IRCBotPort,

        [StringKey( ConfigSection.IRC, "#changeme",
@"Comma-separated list of channels to join. Channel names should include the hash (#).
One some IRC networks, channel names are case-sensitive." )]
        IRCBotChannels,

        [BoolKey( ConfigSection.IRC, false,
@"If checked, all chat messages on IRC are shown in the game.
Otherwise, only IRC messages starting with a hash (#) will be relayed." )]
        IRCBotForwardFromServer,

        [BoolKey( ConfigSection.IRC, false,
@"If checked, all chat messages from the server are shown on IRC.
Otherwise, only chat messages starting with a hash (#) will be relayed." )]
        IRCBotForwardFromIRC,

        [BoolKey( ConfigSection.IRC, false,
@"Show a message on IRC when someone joins or leaves the Minecraft server." )]
        IRCBotAnnounceServerJoins,

        [BoolKey( ConfigSection.IRC, false,
@"Show a message in-game when someone joins or leaves the IRC channel." )]
        IRCBotAnnounceIRCJoins,

        [BoolKey( ConfigSection.IRC, true,
@"Announce server events (kicks, bans, promotions, demotions) on IRC." )]
        IRCBotAnnounceServerEvents,

        [BoolKey( ConfigSection.IRC, false,
@"Check this if bot's nickname is registered
or requires identification/authentication." )]
        IRCRegisteredNick,

        [StringKey( ConfigSection.IRC, "NickServ",
@"Name of the nickname registration service bot
(""Q"" on QuakeNet, ""NickServ"" on most others).",
            MinLength = 1, MaxLength = 32 )]
        IRCNickServ,

        [StringKey( ConfigSection.IRC, "IDENTIFY passwordGoesHere",
@"Message to send to nickname registration service bot." )]
        IRCNickServMessage,

        [ColorKey( ConfigSection.IRC, Color.IRCDefault,
@"Color of IRC messages and event announcements, as seen on the server/in-game." )]
        IRCMessageColor,

        [BoolKey( ConfigSection.IRC, true,
@"Whether the bots should use colors and formatting on IRC." )]
        IRCUseColor,

        [IntKey( ConfigSection.IRC, 750,
@"Minimum delay (in milliseconds) between IRC messages. Many networks
have strict message rate limits, so a delay of at least 500ms is recommended.",
            MinValue = 0, MaxValue = 10000 )]
        IRCDelay,

        [IntKey( ConfigSection.IRC, 1,
@"Number of IRC bots to operate simultaneously.
Using multiple bots helps bypass message rate limits on some servers.
Note that some networks frown upon having multiple connections from one IP.
It is recommended to leave this at 1 unless you are having specific issues
with IRC bots falling behind on messages.",
            MinValue = 1, MaxValue=3 )]
        IRCThreads,

        [StringKey(ConfigSection.IRC, "password",
@"Optional, used to connect to password-protected channels.")]
        IRCChannelPassword,

        #endregion


        #region Advanced

        [BoolKey( ConfigSection.Advanced, true,
@"Crash reports are created when serious unexpected errors occur.
Being able to receive crash reports helps identify bugs and improve GemsCraft!
The report consists of the error information, OS and runtime versions,
a copy of config.xml, and last 25 lines of the log file.
Reports are confidential and are not displayed publicly." )]
        SubmitCrashReports,
        

        [StringKey( ConfigSection.Advanced, "",
@"Command to execute (via operating system's shell) before the update is applied." )]
        RunBeforeUpdate,

        [StringKey( ConfigSection.Advanced, "",
@"Command to execute (via operating system's shell) after the update is applied." )]
        RunAfterUpdate,

        [BoolKey( ConfigSection.Advanced, true,
@"It is recommended to save a backup of all data files before updating.
This setting allows the updater to do that for you." )]
        BackupBeforeUpdate,

        [BoolKey( ConfigSection.Advanced, false,
@"When a player places or deletes a block, vanilla Minecraft server
relays the action back. This is not needed, and only wastes bandwidth." )]
        RelayAllBlockUpdates,

        [BoolKey( ConfigSection.Advanced, false,
@"Minecraft protocol specifies 4 different movement packet types.
One of them sends absolute position, and other 3 send incremental relative positions.
You may use this option to disable the relative updates." )]
        NoPartialPositionUpdates,

        [EnumKey( ConfigSection.Advanced, ProcessPriorityClass.Normal,
@"It is recommended to leave 800Craft at default priority.
Setting this below ""Normal"" may starve 800Craft of resources.
Setting this above ""Normal"" may slow down other software on your machine." )]
        ProcessPriority,

        [IntKey( ConfigSection.Advanced, 2048,
@"The maximum number of block changes that can be sent to each client per second.
Unmodified Minecraft client can only handle about 2500 updates per second.
Setting this any higher may cause lag. Setting this lower will slow down
drawing commands (like cuboid).",
            MinValue = 10 )]
        BlockUpdateThrottling,

        [IntKey( ConfigSection.Advanced, 100,
@"The rate at which 800Craft applies block updates, in milliseconds. Lowering this will slightly
reduce bandwidth and CPU use, but will add latency to block placement.",
            MinValue = 10, MaxValue = 10000 )]
        TickInterval,

        [BoolKey( ConfigSection.Advanced, false,
@"This mode reduces movement and block-placement lag by up to 200ms,
at the cost of vastly increased bandwidth use. It's only practical if
you have a very fast connection with few players, or if your server is LAN-only.
Note that this may make the lag **worse** for players with low-speed,
low-bandwidth connections." )]
        LowLatencyMode,

        [IntKey( ConfigSection.Advanced, 2000000,
@"The number of blocks that players can undo at a time.
Only the most-recent draw command can be undo, so the actual
limit also depends on rank draw limits. Saving undo information
takes up 8 bytes per block. If set to 0, there is no limit.",
            MinValue = 0 )]
        MaxUndo,

        [IntKey( ConfigSection.Advanced, 5,
@"The number of /undo states that players can go back.
Each state increases the maximum potential memory use per-player.",
            MinValue = 1, MaxValue = 100 )]
        MaxUndoStates,

        [StringKey( ConfigSection.Advanced, "(console)",
@"Displayed name of the Console pseudoplayer. You may use any printable characters, and even colorcodes here.",
            MinLength = 1, MaxLength = 64 )]
        ConsoleName,
        
        [StringKey(ConfigSection.General, "Classicube.net",
@"URL to send heartbeats to. Default is classicube.net.
This config key is added in case alternative login/heartbeat services arise.")]
        HeartbeatUrl,
        
        [BoolKey( ConfigSection.Advanced, false,
@"Enable AutoRank to run on the server." )]
        AutoRankEnabled,

        [BoolKey( ConfigSection.Advanced, true,
@"Enable heartbeat to minecraft.net.
If disabled, heartbeat data is written to heartbeatdata.txt." )]
        HeartbeatEnabled,

        [BoolKey( ConfigSection.Advanced, false,
@"If enabled, sends heartbeats to WoM Direct service, http://direct.worldofminecraft.com/" )]
        HeartbeatToWoMDirect,

        [BoolKey( ConfigSection.Advanced, false,
@"If enabled, allows changing worlds' environment settings for WoM clients via /Env" )]
        WoMEnableEnvExtensions,

        [IPKey( ConfigSection.Advanced, IPKeyAttribute.BlankValueMeaning.Any,
@"If the machine has more than one available IP address (for example
if you have more than one NIC) you can use this setting to make
GemsCraft bind to the same IP every time." )]
        IP,

        [EnumKey( ConfigSection.Advanced, GemsCraft.Players.BandwidthUseMode.Normal,
@"Determines the bandwidth use mode.
High/Highest settings will reduce jitter of player movement, but increase bandwidth use.
Low/Lowest settings will introduce some popping in/out of players and increase jitter,
but will reduce bandwidth use." )]
        BandwidthUseMode,

        [IntKey( ConfigSection.Advanced, 0,
@"Automatically restarts the server after a given number of seconds." )]
        RestartInterval,

        [StringKey(ConfigSection.Advanced, "True",
@"If enabled, allows the server to respond to connect to online WebPanel at http://legend-craft.tk")]
        WebPanelEnabled,

        #endregion

        #region Misc
        
        [BoolKey(ConfigSection.Misc, false,
@"If enabled, allows users to use the GemsCraft Mobile app to directly connect to the server remotely.")]
        RemoteControlEnabled,

        [BoolKey(ConfigSection.Misc, true,
@"If set to true, requires owner or admins login with administrator password to access server remotely.")]
        RequireRemoteAdminPass,

        [StringKey(ConfigSection.Misc, "empty",
@"MD5 hash of the admin password for administrators to login to the remote server. Will only be checked if RequireAdmin
  pass is set to true.")]
        RemoteAdminPass,

        [IntKey(ConfigSection.Misc, 65509,
@"Port Remote Control Server will use to establish external connections.")]
        RemoteControlPort,
        #endregion

        #region CPE

        #region MessageTypes
        [BoolKey(ConfigSection.CPE, true,
@"If enabled, allows the use of different message types by the server and users.")]
        EnableMessageTypes,

        [BoolKey(ConfigSection.CPE, true,
@"If enabled, announcements under 64 characters will be shown as an Announcement MessageType")]
        EnableAnnouncements,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at top right of client's screen")]
        Status1Enabled,

        [StringKey(ConfigSection.CPE, "GemsCraft {version}",
            "Messages for Message Type")]
        Status1,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at top right of client's screen")]
        Status2Enabled,

        [StringKey(ConfigSection.CPE, "{servername}",
            "Messages for Message Type")]
        Status2,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at top right of client's screen")]
        Status3Enabled,

        [StringKey(ConfigSection.CPE, "{displayedname}",
            "Messages for Message Type")]
        Status3,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at bottom right of client's screen")]
        BR3Enabled,

        [StringKey(ConfigSection.CPE, "World: {world}",
            "Messages for Message Type")]
        BR3,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at bottom right of client's screen")]
        BR2Enabled,

        [StringKey(ConfigSection.CPE, "Last Command: {lastcommand}",
            "Messages for Message Type")]
        BR2,

        [BoolKey(ConfigSection.CPE, true,
            @"If enabled, shows messages at bottom right of client's screen")]
        BR1Enabled,

        [StringKey(ConfigSection.CPE, "Block: {block}",
            "Messages for Message Type")]
        BR1,
        #endregion

        #region ClickDistance

        [BoolKey(ConfigSection.CPE, true,
@"How far and how short player's click distance can be set. Determines how many blocks away a player can click.")]
        ClickDistanceEnabled,

        [IntKey(ConfigSection.CPE, 1,
@"Min. Click Distance that can be set.")]
        MinClickDistance,

        [IntKey(ConfigSection.CPE, 5,
@"Max. Click Distance that can be set.")]
        MaxClickDistance,

        #endregion

        #region HeldBlock

        [BoolKey(ConfigSection.CPE, true,
 @"If enabled, allows the use of the held block CPE Extension")]
        EnableHeldBlock,

        #endregion
        #endregion

        #region Plugins

        [BoolKey(ConfigSection.Plugins, false, "Whether to enable/disable plugins.")]
        EnablePlugins

        #endregion
    }
}
