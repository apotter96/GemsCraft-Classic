﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GemsCraft.fSystem;
using GemsCraft.Configuration;
using GemsCraft.Games;
using GemsCraft.MapConversion;
using GemsCraft.Network;
using GemsCraft.Physics;
using GemsCraft.Physics.Life;
using GemsCraft.Players;
using GemsCraft.Plugins;
using GemsCraft.Utils;
using JetBrains.Annotations;

namespace GemsCraft.Worlds {
    public sealed class World : IClassy {

        /// <summary>Toggles world chat</summary>
        public bool WorldOnlyChat = false;       

        /// <summary> World name (no formatting).
        /// Use WorldManager.RenameWorld() method to change this. </summary>
        [NotNull]
        public string Name { get; internal set; }

        private GrassTask _plantTask = null;
		private readonly List<PhysScheduler> _physSchedulers = new List<PhysScheduler>();

        /// <summary> Whether the world shows up on the /Worlds list.
        /// Can be assigned directly. </summary>
        public bool IsHidden { get; set; }

        //custom

        //order: highlight name, id, first position, second position, color, percentOpaque
        public Dictionary<string, Tuple<int, Vector3I, Vector3I, System.Drawing.Color, int>> Highlights = new Dictionary<string, Tuple<int, Vector3I, Vector3I, System.Drawing.Color, int>>();

        //order: block name, position, message
        public Dictionary<string, Tuple<Vector3I, string>> MessageBlocks = new Dictionary<string, Tuple<Vector3I, string>>();

        public List<Block> DisallowedBlocks = new List<Block>();

        public bool IsRealm { get; set; }

        public float VisitCount { get; set; }

        public bool RealisticEnv = false; //previously getset

        public bool ZombieGame = false;
        
        /// <summary> Whether hax are allowed on a world. True means hax allowed. </summary>
        public bool Hax = true; 

        public int FireworkCount = 0;
        public ArrayList Portals;
        public int portalID = 1;

        //physics
        public bool tntPhysics = false;
        public bool fireworkPhysics = false;
        public bool waterPhysics = false;
        public bool plantPhysics = false;
        public bool sandPhysics = false;
        public bool gunPhysics = false;

        //games
        //move all these tings
        public ConcurrentDictionary<String, Vector3I> blockCache = new ConcurrentDictionary<String, Vector3I>();
        public List<Player> redTeam = new List<Player>();
        public List<Player> blueTeam = new List<Player>();
        public int redScore = 0;
        public int blueScore = 0;
        public List<Action> Games;
        public bool GameOn = false;
        public GameMode gameMode = GameMode.NULL;
        public Vector3I footballPos;
        public Player[, ,] positions;
        public Vector3I redFlag = new Vector3I(0,0,0);
        public Vector3I blueFlag = new Vector3I(0, 0, 0);
        public Vector3I redCTFSpawn = new Vector3I(0, 0, 0);
        public Vector3I blueCTFSpawn = new Vector3I(0, 0, 0);
        public bool blueFlagTaken = false;
        public bool redFlagTaken = false;

        /// <summary> Whether this world is currently pending unload 
        /// (waiting for block updates to finish processing before unloading). </summary>
        public bool IsPendingMapUnload { get; private set; }



        [NotNull]
        public SecurityController AccessSecurity { get; internal set; }

        [NotNull]
        public SecurityController BuildSecurity { get; internal set; }



        public DateTime LoadedOn { get; internal set; }

        [CanBeNull]
        public string LoadedBy { get; internal set; }

        [NotNull]
        public string LoadedByClassy => PlayerDB.FindExactClassyName( LoadedBy );

        public DateTime MapChangedOn { get; private set; }

        [CanBeNull]
        public string MapChangedBy { get; internal set; }

        [NotNull]
        public string MapChangedByClassy => PlayerDB.FindExactClassyName( MapChangedBy );


        // used to synchronize player joining/parting with map loading/saving
        internal readonly object SyncRoot = new object();


        public BlockDB BlockDB { get; private set; }

        internal World( [NotNull] string name ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            if( !IsValidName( name ) ) {
                throw new ArgumentException( "Unacceptable world name." );
            }
            BlockDB = new BlockDB( this );
            AccessSecurity = new SecurityController();
            BuildSecurity = new SecurityController();
            Name = name;
            UpdatePlayerList();
			for (int i = 0; i < Enum.GetValues(typeof(TaskCategory)).Length; ++i)
				_physSchedulers.Add(new PhysScheduler(this));
        }


		public bool TryAddLife(Life2DZone life)
		{
			if (null == life)
			{
				Logger.Log(LogType.Error, "trying to add null life instance");
				return false;
			}
			lock (SyncRoot)
			{
				if (null==Map)
					return false;
				if (map.LifeZones.ContainsKey(life.Name.ToLower()))
					return false;
				map.LifeZones.Add(life.Name.ToLower(), life);
				if (!_physSchedulers[(int)TaskCategory.Life].Started)
					_physSchedulers[(int)TaskCategory.Life].Start();
			}
			return true;
		}
		public void DeleteLife(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				Logger.Log(LogType.Error, "null or empty string in deleting life");
				return;
			}
			lock (SyncRoot)
			{
				if (null == Map)
					return;
				map.LifeZones.Remove(name.ToLower());
				if (map.LifeZones.Count<=0)
					_physSchedulers[(int)TaskCategory.Life].Stop();
			}
		}
		public Life2DZone GetLife(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				Logger.Log(LogType.Error, "null or empty string in GetLife");
				return null;
			}
			lock (SyncRoot)
			{
				if (null == Map)
					return null;
				Life2DZone life = null;
				map.LifeZones.TryGetValue(name.ToLower(), out life);
				return life;
			}
		}
		public IEnumerable<Life2DZone> GetLifes()
		{
			lock (SyncRoot)
			{
				return Map?.LifeZones.Values.ToList();
			}
		}

    	#region Physics
        internal void StartScheduler(TaskCategory cat)
		{
			if (!_physSchedulers[(int)cat].Started)
				_physSchedulers[(int)cat].Start();
		}
		internal void AddTask(TaskCategory cat, PhysicsTask task, int Delay)
		{
			_physSchedulers[(int)cat].AddTask(task, Delay);
		}

        internal void AddPhysicsTask(PhysicsTask task, int Delay)
        {
            _physSchedulers[(int)TaskCategory.Physics].AddTask(task, Delay);
        }
        #region Plant
        internal void AddPlantTask(short x, short y, short z)
        {
			AddPhysicsTask(new PlantTask(this, x, y, z), PlantTask.GetRandomDelay());
        }
        public void EnablePlantPhysics(Player player, bool announce)
        {
            if (null != _plantTask)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            plantPhysics = true;
            CheckIfPhysicsStarted();
            _plantTask = new GrassTask(this);
			AddPhysicsTask(_plantTask, 0);
            if(announce)
            Server.Message("{0}&S enabled Plant Physics on {1}", player.ClassyName, ClassyName);
        }
        public void DisablePlantPhysics(Player player, bool announce)
        {
            if (null == _plantTask)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            _plantTask.Deleted = true;
            _plantTask = null;
            CheckIfToStopPhysics();
            plantPhysics = false;
            if (announce)
            Server.Message("{0}&S disabled Plant Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        #region TNT
        public void EnableTNTPhysics(Player player, bool announce)
        {
            if (tntPhysics == true)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            CheckIfPhysicsStarted();
            tntPhysics = true;
            if (announce)
            Server.Message("{0}&S enabled TNT Physics on {1}", player.ClassyName, ClassyName);
        }
        
        public void DisableTNTPhysics(Player player, bool announce)
        {
            if (tntPhysics == false)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            tntPhysics = false;
            CheckIfToStopPhysics();
            if (announce)
            Server.Message("{0}&S disabled TNT Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        #region Fireworks
        public void EnableFireworkPhysics(Player player, bool announce)
        {
            if (fireworkPhysics == true)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            CheckIfPhysicsStarted();
            fireworkPhysics = true;
            if (announce)
            Server.Message("{0}&S enabled Firework Physics on {1}", player.ClassyName, ClassyName);
        }

        public void DisableFireworkPhysics(Player player, bool announce)
        {
            if (fireworkPhysics == false)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            fireworkPhysics = false;
            CheckIfToStopPhysics();
            if (announce)
            Server.Message("{0}&S disabled Firework Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        #region Gun
        public void EnableGunPhysics(Player player, bool announce)
        {
            if (gunPhysics == true)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            CheckIfPhysicsStarted();
            gunPhysics = true;
            if (announce)
            Server.Message("{0}&S enabled Gun Physics on {1}", player.ClassyName, ClassyName);
        }

        public void DisableGunPhysics(Player player, bool announce)
        {
            if (gunPhysics == false)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            gunPhysics = false;
            CheckIfToStopPhysics();
            if (announce)
            Server.Message("{0}&S disabled Gun Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        #region Water
        public void EnableWaterPhysics(Player player, bool announce)
        {
            if (waterPhysics == true)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            CheckIfPhysicsStarted();
            waterPhysics = true;
            if (announce)
            Server.Message("{0}&S enabled Water Physics on {1}", player.ClassyName, ClassyName);
        }

        public void DisableWaterPhysics(Player player, bool announce)
        {
            if (waterPhysics == false)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            waterPhysics = false;
            CheckIfToStopPhysics();
            if (announce)
            Server.Message("{0}&S disabled Water Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        #region Sand
        public void EnableSandPhysics(Player player, bool announce)
        {
            if (sandPhysics == true)
            {
                player.Message("&WAlready enabled on this world");
                return;
            }
            CheckIfPhysicsStarted();
            sandPhysics = true;
            if(announce)
            Server.Message("{0}&S enabled Sand Physics on {1}", player.ClassyName, ClassyName);
        }

        public void DisableSandPhysics(Player player, bool announce)
        {
            if (sandPhysics == false)
            {
                player.Message("&WAlready disabled on this world");
                return;
            }
            sandPhysics = false;
            CheckIfToStopPhysics();
            if(announce)
            Server.Message("{0}&S disabled Sand Physics on {1}", player.ClassyName, ClassyName);
        }
        #endregion
        private void CheckIfPhysicsStarted()
        {
            if (!_physSchedulers[(int)TaskCategory.Physics].Started)
				_physSchedulers[(int)TaskCategory.Physics].Start();
        }
        private void CheckIfToStopPhysics()
        {
            if (null == _plantTask && !tntPhysics 
                && !gunPhysics && !plantPhysics 
                && !sandPhysics && !fireworkPhysics
                && !waterPhysics)  //must be extended to further phys types
				_physSchedulers[(int)TaskCategory.Physics].Stop();
        }

		private void StopSchedulers()
		{
			foreach (PhysScheduler sch in _physSchedulers)
				sch.Stop();
		}
		private void AddTasksFromNewMap()
		{
			//assuming lock(SyncRoot)
			foreach (Life2DZone life in map.LifeZones.Values)
				life.Resume();
			if (map.LifeZones.Count>0)
				_physSchedulers[(int)TaskCategory.Life].Start();
		}
        #endregion


        #region Map

        /// <summary> Map of this world. May be null if world is not loaded. </summary>
        [CanBeNull]
        public Map Map
        {
            get => map;
            set
            {
            	bool changed = !ReferenceEquals(map, value);
                if( map != null && value == null ) StopTasks();
                if( map == null && value != null ) StartTasks();
                if( value != null ) value.World = this;
                map = value;

				if (changed)
				{
					if (null == map)
						StopSchedulers();
					else
						AddTasksFromNewMap();
				}
            }
        }
        public Map map;

        /// <summary> Whether the map is currently loaded. </summary>
        public bool IsLoaded => Map != null;


        /// <summary> Loads the map file, if needed.
        /// Generates a default map if mapfile is missing or not loadable.
        /// Guaranteed to return a Map object. </summary>
        [NotNull]
        public Map LoadMap() {
            var tempMap = Map;
            if( tempMap != null ) return tempMap;

            lock( SyncRoot ) {
                if( Map != null ) return Map;

                if( File.Exists( MapFileName ) ) {
                    try {
                        Map = MapUtility.Load( MapFileName );
                    } catch( Exception ex ) {
                        Logger.Log( LogType.Error,
                                    "World.LoadMap: Failed to load map ({0}): {1}",
                                    MapFileName, ex );
                    }
                }

                // or generate a default one
                if( Map == null ) {
                    Server.Message( "&WMapfile is missing for world {0}&W. A new map has been created.", ClassyName );
                    Logger.Log( LogType.Warning,
                                "World.LoadMap: Map file missing for world {0}. Generating default flatgrass map.",
                                Name );
                    Map = MapGenerator.GenerateFlatgrass( 128, 128, 64 );
                }
                return Map;
            }
        }


        internal void UnloadMap( bool expectedPendingFlag ) {
            lock( SyncRoot ) {
                if( expectedPendingFlag != IsPendingMapUnload ) return;
                SaveMap();
                Map = null;
                IsPendingMapUnload = false;
            }
            Server.RequestGC();
        }


        /// <summary> Returns the map filename, including MapPath. </summary>
        public string MapFileName => Path.Combine( Paths.MapPath, Name + ".fcm" );


        public void SaveMap()
        {
            lock( SyncRoot )
            {
                Map?.Save( MapFileName );
            }
        }


        public void ChangeMap([NotNull] Map newMap)
        {
            if (newMap == null) throw new ArgumentNullException("newMap");
            MapChangedOn = DateTime.UtcNow;
            lock (SyncRoot)
            {
                World newWorld = new World(Name)
                {
                    AccessSecurity = (SecurityController) AccessSecurity.Clone(),
                    BuildSecurity = (SecurityController) BuildSecurity.Clone(),
                    IsHidden = IsHidden,
                    IsRealm = IsRealm,
                    BlockDB = BlockDB,
                    lastBackup = lastBackup,
                    BackupInterval = BackupInterval,
                    IsLocked = IsLocked,
                    LockedBy = LockedBy,
                    UnlockedBy = UnlockedBy,
                    LockedDate = LockedDate,
                    UnlockedDate = UnlockedDate,
                    LoadedBy = LoadedBy,
                    LoadedOn = LoadedOn,
                    MapChangedBy = MapChangedBy,
                    MapChangedOn = MapChangedOn,
                    FogColor = FogColor,
                    CloudColor = CloudColor,
                    SkyColor = SkyColor,
                    EdgeLevel = EdgeLevel,
                    SideBlock = SideBlock,
                    EdgeBlock = EdgeBlock,
                    textureURL = textureURL,
                    Map = newMap,
                    NeverUnload = neverUnload
                };
                WorldManager.ReplaceWorld( this, newWorld );
                lock( SyncRoot ) {
                    BlockDB.Clear();
                    BlockDB.World = newWorld;
                }
                foreach( Player player in Players ) {
                    player.JoinWorld( newWorld, WorldChangeReason.Rejoin );
                }
            }
        }


        bool neverUnload;
        public bool NeverUnload {
            get => neverUnload;
            set {
                lock( SyncRoot ) {
                    if( neverUnload == value ) return;
                    neverUnload = value;
                    if( neverUnload ) {
                        if( Map == null ) LoadMap();
                    } else {
                        if( Map != null && playerIndex.Count == 0 ) UnloadMap( false );
                    }
                }
            }
        }

        #endregion



        #region Flush

        public bool IsFlushing { get; private set; }


        public void Flush() {
            lock( SyncRoot ) {
                if( Map == null ) return;
                Players.Message( "&WMap is being flushed. Stay put, world will reload shortly.", MessageType.Chat );
                IsFlushing = true;
            }
        }


        internal void EndFlushMapBuffer() {
            lock( SyncRoot ) {
                IsFlushing = false;
                Players.Message( "&WMap flushed. Reloading...", MessageType.Chat );
                foreach( Player player in Players ) {
                    player.JoinWorld( this, WorldChangeReason.Rejoin, player.Position );
                }
            }
        }

        #endregion


        #region PlayerList

        readonly Dictionary<string, Player> playerIndex = new Dictionary<string, Player>();
        public Player[] Players { get; private set; }

        [CanBeNull]
        public Map AcceptPlayer( [NotNull] Player player, bool announce ) {
            if( player == null ) throw new ArgumentNullException( "player" );

            lock( SyncRoot ) {
                if( IsFull ) {
                    if( player.Info.Rank.ReservedSlot ) {
                        Player idlestPlayer = Players.Where( p => p.Info.Rank.IdleKickTimer != 0 )
                                                     .OrderBy( p => p.LastActiveTime )
                                                     .FirstOrDefault();
                        if( idlestPlayer != null ) {
                            idlestPlayer.Kick( Player.Console, "Auto-kicked to make room (idle).",
                                               LeaveReason.IdleKick, false, false, false );

                            Server.Players
                                  .CanSee( player )
                                  .Message( "&SPlayer {0}&S was auto-kicked to make room for {1}", MessageType.Chat,
                                            idlestPlayer.ClassyName, player.ClassyName );
                            Server.Players
                                  .CantSee( player )
                                  .Message( "{0}&S was kicked for being idle for {1} min", MessageType.Chat,
                                            player.ClassyName, player.Info.Rank.IdleKickTimer );
                        } else {
                            return null;
                        }
                    } else {
                        return null;
                    }
                }

                if( playerIndex.ContainsKey( player.Name.ToLower() ) ) {
                    Logger.Log( LogType.Error,
                                "This world already contains the player by name ({0}). " +
                                "Some sort of state corruption must have occured.",
                                player.Name );
                    playerIndex.Remove( player.Name.ToLower() );
                }

                playerIndex.Add( player.Name.ToLower(), player );

                // load the map, if it's not yet loaded
                IsPendingMapUnload = false;
                Map = LoadMap();

                if( ConfigKey.BackupOnJoin.Enabled() && (Map.HasChangedSinceBackup || !ConfigKey.BackupOnlyWhenChanged.Enabled()) ) {
                    string backupFileName = String.Format( JoinBackupFormat,
                                                           Name, DateTime.Now, player.Name ); // localized
                    Map.SaveBackup( MapFileName,
                                    Path.Combine( Paths.BackupPath, backupFileName ) );
                }

                UpdatePlayerList();

                if (!IsRealm && announce && ConfigKey.ShowJoinedWorldMessages.Enabled())
                {
                    Server.Players.CanSee(player)
                                  .Message("&SPlayer {0}&S joined world {1}", 0,
                                            player.ClassyName, ClassyName);

                }

                //realm joining announcer
                if (IsRealm && announce && ConfigKey.ShowJoinedWorldMessages.Enabled())
                {
                    Server.Players.CanSee(player)
                                  .Message("&SPlayer {0}&S joined realm {1}", 0,
                                            player.ClassyName, ClassyName);
                }

                if (IsRealm)
                {
                    Logger.Log(LogType.ChangedWorld,
                    "Player {0} joined realm {1}.",
                    player.Name, Name);
                }

                if (!IsRealm)
                {
                    Logger.Log(LogType.ChangedWorld,
                    "Player {0} joined world {1}.",
                    player.Name, Name);
                }

                if( IsLocked ) {
                    player.Message( "&WThis map is currently locked (read-only)." );
                }

                if( player.Info.IsHidden ) {
                    player.Message( "&8Reminder: You are still hidden." );
                }

                return Map;
            }
        }


        public bool ReleasePlayer( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( SyncRoot ) {
                if( !playerIndex.Remove( player.Name.ToLower() ) ) {
                    return false;
                }

                // clear undo & selection
                player.LastDrawOp = null;
                player.UndoClear();
                player.RedoClear();
                player.IsRepeatingSelection = false;
                player.SelectionCancel();

                // update player list
                UpdatePlayerList();

                // unload map (if needed)
                if( playerIndex.Count == 0 && !neverUnload ) {
                    IsPendingMapUnload = true;
                }
                return true;
            }
        }

        /// <summary>
        /// Find bot by name. Returns either the bot by exact name, or null.
        /// </summary>
        public Bot FindBot(string name)
        {
            var bot =
               from b in Server.Bots
               where string.Equals(b.Name, name, StringComparison.CurrentCultureIgnoreCase)
               select b;

            var enumerable = bot as Bot[] ?? bot.ToArray();
            if (enumerable.Count() != 1)
            {
                return null;
            }

            Bot bot_ = enumerable.First();
            if (bot_.World.Name == Name)
            {
                return enumerable.First();
            }
            return null;
        }

        /// <summary>
        /// Find bot by ID. Returns either the bot by exact ID, or null.
        /// </summary>
        public Bot FindBot(int ID)
        {
            var bot =
                from b in Server.Bots
                where b.ID == ID
                select b;

            if (bot.Count() != 1)
            {
                return null;
            }

            Bot bot_ = bot.First();
            if (bot_.World.Name == Name)
            {
                return bot.First();
            }
            return null;
        }

        public Player[] FindPlayers( [NotNull] Player player, [NotNull] string playerName ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( playerName == null ) throw new ArgumentNullException( "playerName" );
            Player[] tempList = Players;
            List<Player> results = new List<Player>();
            foreach (var t in tempList)
            {
                if( t != null && player.CanSee(t) ) {
                    if( t.Name.Equals( playerName, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Clear();
                        results.Add( t );
                        break;
                    } else if( t.Name.StartsWith( playerName, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Add( t );
                    }
                }
            }
            return results.ToArray();
        }


        /// <summary> Gets player by name (without autocompletion) </summary>
        [CanBeNull]
        public Player FindPlayerExact( [NotNull] string playerName ) {
            if( playerName == null ) throw new ArgumentNullException( "playerName" );
            Player[] tempList = Players;
            // ReSharper disable LoopCanBeConvertedToQuery
            foreach (Player t in tempList)
            {
                if( t != null && t.Name.Equals( playerName, StringComparison.OrdinalIgnoreCase ) ) {
                    return t;
                }
            }
            // ReSharper restore LoopCanBeConvertedToQuery
            return null;
        }


        /// <summary> Caches the player list to an array (Players -> PlayerList) </summary>
        public void UpdatePlayerList() {
            lock( SyncRoot ) {
                Players = playerIndex.Values.ToArray();
            }
        }


        /// <summary> Counts all players (optionally includes all hidden players). </summary>
        public int CountPlayers( bool includeHiddenPlayers ) {
            if( includeHiddenPlayers ) {
                return Players.Length;
            } else {
                return Players.Count( player => !player.Info.IsHidden );
            }
        }


        /// <summary> Counts only the players who are not hidden from a given observer. </summary>
        public int CountVisiblePlayers( [NotNull] Player observer ) {
            if( observer == null ) throw new ArgumentNullException( "observer" );
            return Players.Count( observer.CanSee );
        }


        public bool IsFull => (Players.Length >= ConfigKey.MaxPlayersPerWorld.GetInt());

        #endregion


        #region Lock / Unlock

        /// <summary> Whether the world is currently locked (in read-only mode). </summary>
        public bool IsLocked { get; private set; }

        public string LockedBy, UnlockedBy;
        public DateTime LockedDate, UnlockedDate;

        readonly object lockLock = new object();


        public bool Lock( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( lockLock ) {
                if( IsLocked ) {
                    return false;
                } else {
                    LockedBy = player.Name;
                    LockedDate = DateTime.UtcNow;
                    IsLocked = true;
                    Map mapCache = Map;
                    if( mapCache != null ) {
                        mapCache.ClearUpdateQueue();
                        mapCache.StopAllDrawOps();
                    }
                    Players.Message( "&WWorld was locked by {0}", 0, player.ClassyName );
                    Logger.Log( LogType.UserActivity,
                                "World {0} was locked by {1}",
                                Name, player.Name );
                    return true;
                }
            }
        }


        public bool Unlock( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( lockLock ) {
                if( IsLocked ) {
                    UnlockedBy = player.Name;
                    UnlockedDate = DateTime.UtcNow;
                    IsLocked = false;
                    Players.Message( "&WMap was unlocked by {0}", MessageType.Chat, player.ClassyName );
                    Logger.Log( LogType.UserActivity,
                                "World \"{0}\" was unlocked by {1}",
                                Name, player.Name );
                    return true;
                } else {
                    return false;
                }
            }
        }

        #endregion


        #region Patrol

        readonly object patrolLock = new object();
        static readonly TimeSpan MinPatrolInterval = TimeSpan.FromSeconds( 20 );

        public Player GetNextPatrolTarget( [NotNull] Player observer ) {
            if( observer == null ) throw new ArgumentNullException( "observer" );
            lock( patrolLock ) {
                Player candidate = Players.RankedAtMost( RankManager.PatrolledRank )
                                          .CanBeSeen( observer )
                                          .Where( p => p.LastActiveTime > p.LastPatrolTime &&
                                                       p.HasFullyConnected &&
                                                       DateTime.UtcNow.Subtract( p.LastPatrolTime ) > MinPatrolInterval )
                                          .OrderBy( p => p.LastPatrolTime.Ticks )
                                          .FirstOrDefault();
                if( candidate != null ) {
                    candidate.LastPatrolTime = DateTime.UtcNow;
                }
                return candidate;
            }
        }

        public Player GetNextPatrolTarget( [NotNull] Player observer,
                                           [NotNull] Predicate<Player> predicate,
                                           bool setLastPatrolTime ) {
            if( observer == null ) throw new ArgumentNullException( "observer" );
            if( predicate == null ) throw new ArgumentNullException( "predicate" );
            lock( patrolLock ) {
                Player candidate = Players.RankedAtMost( RankManager.PatrolledRank )
                                          .CanBeSeen( observer )
                                          .Where( p => p.LastActiveTime > p.LastPatrolTime &&
                                                       p.HasFullyConnected &&
                                                       DateTime.UtcNow.Subtract( p.LastPatrolTime ) > MinPatrolInterval )
                                          .Where( p => predicate( p ) )
                                          .OrderBy( p => p.LastPatrolTime.Ticks )
                                          .FirstOrDefault();
                if( setLastPatrolTime && candidate != null ) {
                    candidate.LastPatrolTime = DateTime.UtcNow;
                }
                return candidate;
            }
        }

        #endregion


        #region Scheduled Tasks

        SchedulerTask updateTask, saveTask;
        readonly object taskLock = new object();


        void StopTasks() {
            lock( taskLock ) {
                if( updateTask != null ) {
                    updateTask.Stop();
                    updateTask = null;
                }
                if( saveTask != null ) {
                    saveTask.Stop();
                    saveTask = null;
                }
            }
        }


        void StartTasks() {
            lock( taskLock ) {
                updateTask = Scheduler.NewTask( UpdateTask );
                updateTask.RunForever( this,
                                       TimeSpan.FromMilliseconds( ConfigKey.TickInterval.GetInt() ),
                                       TimeSpan.Zero );

                if( ConfigKey.SaveInterval.GetInt() > 0 ) {
                    saveTask = Scheduler.NewBackgroundTask( SaveTask );
                    saveTask.RunForever( this,
                                         TimeSpan.FromSeconds( ConfigKey.SaveInterval.GetInt() ),
                                         TimeSpan.FromSeconds( ConfigKey.SaveInterval.GetInt() ) );
                }
            }
        }


        void UpdateTask( SchedulerTask task ) {
            Map tempMap = Map;
            if( tempMap != null ) {
                tempMap.ProcessUpdates();
            }
        }


        const string TimedBackupFormat = "{0}_{1:yyyy-MM-dd_HH-mm}.fcm",
                     JoinBackupFormat = "{0}_{1:yyyy-MM-dd_HH-mm}_{2}.fcm";

        public static readonly TimeSpan DefaultBackupInterval = TimeSpan.FromSeconds( -1 );

        public TimeSpan BackupInterval { get; set; }

        DateTime lastBackup = DateTime.UtcNow;

        void SaveTask( SchedulerTask task ) {
            if( Map == null ) return;
            lock( SyncRoot ) {
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                // ReSharper disable HeuristicUnreachableCode
                if( Map == null ) return;
                // ReSharper restore HeuristicUnreachableCode
                // ReSharper restore ConditionIsAlwaysTrueOrFalse

                if( BackupInterval != TimeSpan.Zero &&
                    DateTime.UtcNow.Subtract( lastBackup ) > BackupInterval &&
                    (Map.HasChangedSinceBackup || !ConfigKey.BackupOnlyWhenChanged.Enabled()) ) {

                    string backupFileName = String.Format( TimedBackupFormat, Name, DateTime.Now ); // localized
                    Map.SaveBackup( MapFileName,
                                    Path.Combine( Paths.BackupPath, backupFileName ) );
                    lastBackup = DateTime.UtcNow;
                }

                if( Map.HasChangedSinceSave ) {
                    SaveMap();
                }
            }
        }

        #endregion


        #region Env 
  
        //Minecraft
        public int CloudColor = 0xFFFFFF, FogColor = 0xFFFFFF, SkyColor = 0x99CCFF, EdgeLevel = -1;
        public string Terrain { get; set; }

        public Block EdgeBlock = Block.Water;
        public Block SideBlock = Block.Admincrete;

        public string GenerateWoMConfig(bool sendMotd)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("server.name = " + ConfigKey.ServerName.GetString());
            if (sendMotd)
            {
                sb.AppendLine("server.detail = " + ConfigKey.MOTD.GetString());
            }
            else
            {
                sb.AppendLine("server.detail = " + ClassyName);
            }
            sb.AppendLine("user.detail = World " + ClassyName);
            if (CloudColor > -1) sb.AppendLine("environment.cloud = " + CloudColor);
            if (FogColor > -1) sb.AppendLine("environment.fog = " + FogColor);
            if (SkyColor > -1) sb.AppendLine("environment.sky = " + SkyColor);
            if (EdgeLevel > -1) sb.AppendLine("environment.level = " + EdgeLevel);
            sb.AppendLine("server.sendwomid = true");
            return sb.ToString();
        }


        //ClassiCube
        public int WeatherCC = 0;
        public string textureURL = "";
        
        internal void SendAllMapAppearance() {
            Packet packet = PacketWriter.MakeEnvSetMapAppearance(textureURL, SideBlock, EdgeBlock, EdgeLevel);
            foreach (Player p in Players.Where(p => p.usesCPE))
                p.Send(packet);
        }
        
        internal void SendAllMapWeather() {
            Packet packet = PacketWriter.MakeEnvWeatherAppearance((byte)WeatherCC);
            foreach (Player p in Players.Where(p => p.usesCPE))
                p.Send(packet);
        }
        
        internal void SendAllEnvColor(byte variable, int  value) {
        	Packet packet = PacketWriter.MakeEnvSetColor(variable, value);
            foreach (Player p in Players.Where(p => p.usesCPE))
                p.Send(packet);
        }

        #endregion


        /// <summary> Ensures that player name has the correct length (2-16 characters)
        /// and character set (alphanumeric chars and underscores allowed). </summary>
        public static bool IsValidName( [NotNull] string name ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            if( name.Length < 2 || name.Length > 16 ) return false;
            // ReSharper disable LoopCanBeConvertedToQuery
            foreach (var ch in name)
            {
                if( ch < '0' && ch != '-'||
                    ch > '9' && ch < 'A' ||
                    ch > 'Z' && ch < '_' ||
                    ch > '_' && ch < 'a' ||
                    ch > 'z') {
                    
                    return false;
                }
            }
            // ReSharper restore LoopCanBeConvertedToQuery
            return true;
        }


        /// <summary> Returns a nicely formatted name, with optional color codes. </summary>
        public string ClassyName {
            get
            {
                if( ConfigKey.RankColorsInWorldNames.Enabled() ) {
                    Rank maxRank;
                    maxRank = BuildSecurity.MinRank >= AccessSecurity.MinRank ? BuildSecurity.MinRank : AccessSecurity.MinRank;
                    if( ConfigKey.RankPrefixesInChat.Enabled() ) {
                        return maxRank.Color + maxRank.Prefix + Name;
                    }

                    return maxRank.Color + Name;
                }

                return Name;
            }
        }


        public override string ToString() {
            return $"World({Name})";
        }
    }
    public class PhysicsBlock
    {
        public short x, y, z;
        public Block type;
        public DateTime startTime;
        public Player player;

        public PhysicsBlock(short x, short y, short z, Block type, Player player)
        {
            this.player = player;
            this.x = x;
            this.y = y;
            this.z = z;
            this.type = type;
            this.startTime = DateTime.Now;
        }
    }
}

