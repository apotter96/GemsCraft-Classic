﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using GemsCraft.Drawing;
using GemsCraft.fSystem;
using GemsCraft.Configuration;
using GemsCraft.MapConversion;
using GemsCraft.Network;
using GemsCraft.Physics.Life;
using GemsCraft.Players;
using GemsCraft.Utils;
using GemsCraft.Worlds.CustomBlocks;
using JetBrains.Annotations;

namespace GemsCraft.Worlds {

    public sealed partial class Map {
        public const MapFormat SaveFormat = MapFormat.FCMv3;

        /// <summary> The world associated with this map, if any. May be null. </summary>
        [CanBeNull]
        public World World { get; set; }

        /// <summary> Map width, in blocks. Equivalent to Notch's X (horizontal). </summary>
        public readonly int Width;

        /// <summary> Map length, in blocks. Equivalent to Notch's Z (horizontal). </summary>
        public readonly int Length;

        /// <summary> Map height, in blocks. Equivalent to Notch's Y (vertical). </summary>
        public readonly int Height;

        /// <summary> Map boundaries. Can be useful for calculating volume or interesections. </summary>
        public readonly BoundingBox Bounds;

        /// <summary> Map volume, in terms of blocks. </summary>
        public readonly int Volume;


        /// <summary> Default spawning point on the map. </summary>
        Position spawn;
        public Position Spawn {
            get => spawn;
            set {
                spawn = value;
                HasChangedSinceSave = true;
            }
        }

        /// <summary> Resets spawn to the default location (top center of the map). </summary>
        public void ResetSpawn() {
            Spawn = new Position( Width * 16,
                                  Length * 16,
                                  Math.Min( short.MaxValue, Height * 32 ) );
        }


        /// <summary> Whether the map was modified since last time it was saved. </summary>
        public bool HasChangedSinceSave { get; internal set; }

        /// <summary> Whether the map was saved since last time it was backed up. </summary>
        public bool HasChangedSinceBackup { get; private set; }

        // used by IsoCat and MapGenerator
        public short[,] Shadows;


        // FCMv3 additions
        public DateTime DateModified { get; set; }
        public DateTime DateCreated { get; set; }
        public Guid Guid { get; set; }

        /// <summary> Array of map blocks.
        /// Use Index(x,y,h) to convert coordinates to array indices.
        /// Use QueueUpdate() for working on live maps to
        /// ensure that all players get updated. </summary>
        public byte[] Blocks;

        /// <summary> Map metadata, excluding zones. </summary>
        public MetadataCollection<string> Metadata { get; private set; }

        /// <summary> All zones within a map. </summary>
        public ZoneCollection Zones { get; private set; }
		internal Dictionary<string, Life2DZone> LifeZones { get; private set; }


        /// <summary> Creates an empty new map of given dimensions.
        /// Dimensions cannot be changed after creation. </summary>
        /// <param name="world"> World that owns this map. May be null, and may be changed later. </param>
        /// <param name="width"> Width (horizontal, Notch's X). </param>
        /// <param name="length"> Length (horizontal, Notch's Z). </param>
        /// <param name="height"> Height (vertical, Notch's Y). </param>
        /// <param name="initBlockArray"> If true, the Blocks array will be created. </param>
        public Map( World world, int width, int length, int height, bool initBlockArray ) {
            if( !IsValidDimension( width ) ) throw new ArgumentException( "Invalid map dimension.", "width" );
            if( !IsValidDimension( length ) ) throw new ArgumentException( "Invalid map dimension.", "length" );
            if( !IsValidDimension( height ) ) throw new ArgumentException( "Invalid map dimension.", "height" );
            DateCreated = DateTime.UtcNow;
            DateModified = DateCreated;
            Guid = Guid.NewGuid();

            Metadata = new MetadataCollection<string>();
            Metadata.Changed += OnMetaOrZoneChange;

            Zones = new ZoneCollection();
            Zones.Changed += OnMetaOrZoneChange;

            World = world;

            Width = width;
            Length = length;
            Height = height;
            Bounds = new BoundingBox( Vector3I.Zero, Width, Length, Height );
            Volume = Bounds.Volume;

            if( initBlockArray ) {
                Blocks = new byte[Volume];
            }

        	LifeZones = new Dictionary<string, Life2DZone>();

            ResetSpawn();
        }


        void OnMetaOrZoneChange( object sender, EventArgs args ) {
            HasChangedSinceSave = true;
        }


        #region Saving

        /// <summary> Saves this map to a file in the default format (FCMv3). </summary>
        /// <returns> Whether the saving succeeded. </returns>
        public bool Save( [NotNull] string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            string tempFileName = fileName + ".temp";

            // save to a temporary file
            try {
                HasChangedSinceSave = !MapUtility.TrySave( this, tempFileName, SaveFormat );
            } catch( IOException ex ) {
                HasChangedSinceSave = true;
                Logger.Log( LogType.Error,
                            "Map.Save: Unable to open file \"{0}\" for writing: {1}",
                            tempFileName, ex );
                if( File.Exists( tempFileName ) )
                    File.Delete( tempFileName );
                return false;
            }

            // move newly-written file into its permanent destination
            try {
                Paths.MoveOrReplace( tempFileName, fileName );
                Logger.Log( LogType.SystemActivity,
                            "Saved map to {0}", fileName );
                HasChangedSinceBackup = true;

            } catch( Exception ex ) {
                HasChangedSinceSave = true;
                Logger.Log( LogType.Error,
                            "Map.Save: Error trying to replace file \"{0}\": {1}",
                            fileName, ex );
                if( File.Exists( tempFileName ) )
                    File.Delete( tempFileName );
                return false;
            }
            return true;
            // ReSharper restore EmptyGeneralCatchClause
        }

        #endregion


        #region Block Getters / Setters

        /// <summary> Converts given coordinates to a block array index. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="z"> Z coordinate (height, Notch's Y). </param>
        /// <returns> Index of the block in Map.Blocks array. </returns>
        public int Index( int x, int y, int z ) {
            return (z * Length + y) * Width + x;
        }


        /// <summary> Converts given coordinates to a block array index. </summary>
        /// <param name="coords"> Coordinate vector (X,Y,Z). </param>
        /// <returns> Index of the block in Map.Blocks array. </returns>
        public int Index( Vector3I coords ) {
            return (coords.Z * Length + coords.Y) * Width + coords.X;
        }


        /// <summary> Sets a block in a safe way.
        /// Note that using SetBlock does not relay changes to players.
        /// Use QueueUpdate() for changing blocks on live maps/worlds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="z"> Z coordinate (height, Notch's Y). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( int x, int y, int z, Block type ) {
            if( x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0 ) {
                Blocks[Index( x, y, z )] = (byte)type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Sets a block at given coordinates. Checks bounds. </summary>
        /// <param name="coords"> Coordinate vector (X,Y,Z). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( Vector3I coords, Block type ) {
            if( coords.X < Width && coords.Y < Length && coords.Z < Height && coords.X >= 0 && coords.Y >= 0 && coords.Z >= 0 && (byte)type < 50 ) {
                Blocks[Index( coords )] = (byte)type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Gets a block at given coordinates. Checks bounds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="z"> Z coordinate (height, Notch's Y). </param>
        /// <returns> Block type, as a Block enumeration. Undefined if coordinates were out of bounds. </returns>
        public Block GetBlock( int x, int y, int z ) {
            if( x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0 )
                return (Block)Blocks[Index( x, y, z )];
            return Block.Undefined;
        }


        /// <summary> Gets a block at given coordinates. Checks bounds. </summary>
        /// <param name="coords"> Coordinate vector (X,Y,Z). </param>
        /// <returns> Block type, as a Block enumeration. Undefined if coordinates were out of bounds. </returns>
        public Block GetBlock( Vector3I coords ) {
            if( coords.X < Width && coords.Y < Length && coords.Z < Height && coords.X >= 0 && coords.Y >= 0 && coords.Z >= 0 )
                return (Block)Blocks[Index( coords )];
            return Block.Undefined;
        }


        /// <summary> Checks whether the given coordinate (in block units) is within the bounds of the map. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="z"> Z coordinate (height, Notch's Y). </param>
        public bool InBounds( int x, int y, int z ) {
            return x < Width && y < Length && z < Height && x >= 0 && y >= 0 && z >= 0;
        }


        /// <summary> Checks whether the given coordinate (in block units) is within the bounds of the map. </summary>
        /// <param name="vec"> Coordinate vector (X,Y,Z). </param>
        public bool InBounds( Vector3I vec ) {
            return vec.X < Width && vec.Y < Length && vec.Z < Height && vec.X >= 0 && vec.Y >= 0 && vec.Z >= 0;
        }

        #endregion


        #region Block Updates & Simulation

        // Queue of block updates. Updates are applied by ProcessUpdates()
        ConcurrentQueue<BlockUpdate> updates = new ConcurrentQueue<BlockUpdate>();


        /// <summary> Number of blocks that are waiting to be processed. </summary>
        public int UpdateQueueLength => updates.Count;


        /// <summary> Queues a new block update to be processed.
        /// Due to concurrent nature of the server, there is no guarantee
        /// that updates will be applied in any specific order. </summary>
        public void QueueUpdate( BlockUpdate update ) {
            updates.Enqueue( update );
        }


        /// <summary> Clears all pending updates. </summary>
        public void ClearUpdateQueue() {
            updates = new ConcurrentQueue<BlockUpdate>();
        }


        // Applies pending updates and sends them to players (if applicable).
        internal void ProcessUpdates() {
            if( World == null ) {
                throw new InvalidOperationException( "Map must be assigned to a world to process updates." );
            }

            if( World.IsLocked ) {
                if( World.IsPendingMapUnload ) {
                    World.UnloadMap( true );
                }
                return;
            }

            int packetsSent = 0;
            bool canFlush = false;
            int maxPacketsPerUpdate = Server.CalculateMaxPacketsPerUpdate( World );
            while( packetsSent < maxPacketsPerUpdate ) {
                BlockUpdate update;
                if( !updates.TryDequeue( out update ) ) {
                    if( World.IsFlushing ) {
                        canFlush = true;
                    }
                    break;
                }
                if( !InBounds( update.X, update.Y, update.Z ) ) continue;
                int blockIndex = Index( update.X, update.Y, update.Z );
                Blocks[blockIndex] = (byte)update.BlockType;

                if( !World.IsFlushing ) 
                {
                    //non classicube players get fallbacks instead of the real blocks
                    Packet packet = PacketWriter.MakeSetBlock( update.X, update.Y, update.Z, update.BlockType );
                    Packet packet2 = PacketWriter.MakeSetBlock(update.X, update.Y, update.Z, Map.GetFallbackBlock(update.BlockType));

                    World.Players.Where(p => p.usesCPE).SendLowPriority(update.Origin, packet);
                    World.Players.Where(p => !p.usesCPE).SendLowPriority(update.Origin, packet2);
                }
                packetsSent++;
            }

            if( drawOps.Count > 0 ) {
                lock( drawOpLock ) {
                    if( drawOps.Count > 0 ) {
                        packetsSent += ProcessDrawOps( maxPacketsPerUpdate - packetsSent );
                    }
                }
            } else if( canFlush ) {
                World.EndFlushMapBuffer();
            }

            if( packetsSent == 0 && World.IsPendingMapUnload ) {
                World.UnloadMap( true );
            }
        }

        #endregion


        #region Draw Operations

        public int DrawQueueLength => drawOps.Count;

        public int DrawQueueBlockCount {
            get {
                lock( drawOpLock ) {
                    return drawOps.Sum( op => op.BlocksLeftToProcess );
                }
            }
        }

        readonly List<DrawOperation> drawOps = new List<DrawOperation>();

        readonly object drawOpLock = new object();


        internal void QueueDrawOp( [NotNull] DrawOperation op ) {
            if( op == null ) throw new ArgumentNullException( "op" );
            lock( drawOpLock ) {
                drawOps.Add( op );
            }
        }


        int ProcessDrawOps( int maxTotalUpdates ) {
            if( World == null ) throw new InvalidOperationException( "No world assigned" );
            int blocksDrawnTotal = 0;
            for( int i = 0; i < drawOps.Count; i++ ) {
                DrawOperation op = drawOps[i];

                // remove a cancelled drawOp from the list
                if( op.IsCancelled ) {
                    op.End();
                    drawOps.RemoveAt( i );
                    i--;
                    continue;
                }

                // draw a batch of blocks
                int blocksToDraw = maxTotalUpdates / (drawOps.Count - i);
                op.StartBatch();
#if DEBUG
                int blocksDrawn = op.DrawBatch( blocksToDraw );
#else
                int blocksDrawn;
                try{
                    blocksDrawn = op.DrawBatch( blocksToDraw );
                } catch( Exception ex ) {
                    Logger.LogAndReportCrash( "DrawOp error", "fCraft", ex, false );
                    op.Player.Message( "&WError occured in your draw command: {0}: {1}",
                                       ex.GetType().Name, ex.Message );
                    drawOps.RemoveAt( i );
                    op.End();
                    return blocksDrawnTotal;
                }
#endif
                blocksDrawnTotal += blocksDrawn;
                if( blocksDrawn > 0 ) {
                    HasChangedSinceSave = true;
                }
                maxTotalUpdates -= blocksDrawn;

                // remove a completed drawOp from the list
                if (!op.IsDone) continue;
                op.End();
                drawOps.RemoveAt( i );
                i--;
            }
            return blocksDrawnTotal;
        }


        public void StopAllDrawOps() {
            lock( drawOpLock )
            {
                foreach (var t in drawOps)
                {
                    t.Cancel();
                    t.End();
                }

                drawOps.Clear();
            }
        }

        #endregion


        #region Backup

        readonly object backupLock = new object();


        public void SaveBackup( [NotNull] string sourceName, [NotNull] string targetName ) {
            if( sourceName == null ) throw new ArgumentNullException( "sourceName" );
            if( targetName == null ) throw new ArgumentNullException( "targetName" );

            lock( backupLock ) {
                DirectoryInfo directory = new DirectoryInfo( Paths.BackupPath );

                if( !directory.Exists ) {
                    try {
                        directory.Create();
                    } catch( Exception ex ) {
                        Logger.Log( LogType.Error,
                                    "Map.SaveBackup: Error occured while trying to create backup directory: {0}", ex );
                        return;
                    }
                }

                try {
                    HasChangedSinceBackup = false;
                    File.Copy( sourceName, targetName, true );
                } catch( Exception ex ) {
                    HasChangedSinceBackup = true;
                    Logger.Log( LogType.Error,
                                "Map.SaveBackup: Error occured while trying to save backup to \"{0}\": {1}",
                                targetName, ex );
                    return;
                }

                if( ConfigKey.MaxBackups.GetInt() > 0 || ConfigKey.MaxBackupSize.GetInt() > 0 ) {
                    DeleteOldBackups( directory );
                }
            }

            Logger.Log( LogType.SystemActivity, "AutoBackup: {0}", targetName );
        }


        static void DeleteOldBackups( [NotNull] DirectoryInfo directory ) {
            if( directory == null ) throw new ArgumentNullException( "directory" );
            var backupList = directory.GetFiles( "*.fcm" ).OrderBy( fi => -fi.CreationTimeUtc.Ticks ).ToList();

            int maxFileCount = ConfigKey.MaxBackups.GetInt();

            if( maxFileCount > 0 ) {
                while( backupList.Count > maxFileCount ) {
                    FileInfo info = backupList[backupList.Count - 1];
                    backupList.RemoveAt( backupList.Count - 1 );
                    try {
                        File.Delete( info.FullName );
                    } catch( Exception ex ) {
                        Logger.Log( LogType.Error,
                                    "Map.SaveBackup: Error occured while trying delete old backup \"{0}\": {1}",
                                    info.FullName, ex );
                        break;
                    }
                    Logger.Log( LogType.SystemActivity,
                                "Map.SaveBackup: Deleted old backup \"{0}\"", info.Name );
                }
            }

            int maxFileSize = ConfigKey.MaxBackupSize.GetInt();

            if( maxFileSize > 0 ) {
                while( true ) {
                    FileInfo[] fis = directory.GetFiles();
                    long size = fis.Sum( fi => fi.Length );

                    if( size / 1024 / 1024 > maxFileSize ) {
                        FileInfo info = backupList[backupList.Count - 1];
                        backupList.RemoveAt( backupList.Count - 1 );
                        try {
                            File.Delete( info.FullName );
                        } catch( Exception ex ) {
                            Logger.Log( LogType.Error,
                                        "Map.SaveBackup: Error occured while trying delete old backup \"{0}\": {1}",
                                        info.Name, ex );
                            break;
                        }
                        Logger.Log( LogType.SystemActivity,
                                    "Map.SaveBackup: Deleted old backup \"{0}\"", info.Name );
                    } else {
                        break;
                    }
                }
            }
        }

        #endregion


        #region Utilities

        public bool ValidateHeader() {
            if( !IsValidDimension( Width ) ) {
                Logger.Log( LogType.Error,
                            "Map.ValidateHeader: Unsupported map width: {0}.", Width );
                return false;
            }

            if( !IsValidDimension( Length ) ) {
                Logger.Log( LogType.Error,
                            "Map.ValidateHeader: Unsupported map length: {0}.", Length );
                return false;
            }

            if( !IsValidDimension( Height ) ) {
                Logger.Log( LogType.Error,
                            "Map.ValidateHeader: Unsupported map height: {0}.", Height );
                return false;
            }

            if( Spawn.X > Width * 32 || Spawn.Y > Length * 32 || Spawn.Z > Height * 32 || Spawn.X < 0 || Spawn.Y < 0 || Spawn.Z < 0 ) {
                Logger.Log( LogType.Warning,
                            "Map.ValidateHeader: Spawn coordinates are outside the valid range! Using center of the map instead." );
                ResetSpawn();
            }

            return true;
        }


        /// <summary> Checks if a given map dimension (width, height, or length) is acceptible.
        /// Values between 1 and 2047 are technically allowed. </summary>
        public static bool IsValidDimension( int dimension ) {
            return dimension >= 16 && dimension <= 2048;
        }


        /// <summary> Checks if a given map dimension (width, height, or length) is among the set of recommended values
        /// Recommended values are: 16, 32, 64, 128, 256, 512, 1024 </summary>
        public static bool IsRecommendedDimension( int dimension ) {
            return dimension >= 16 && (dimension & (dimension - 1)) == 0 && dimension <= 1024;
        }


        /// <summary> Converts nonstandard (50-255) blocks using the given mapping. </summary>
        /// <param name="mapping"> Byte array of length 256. </param>
        /// <returns> True if any blocks needed conversion/mapping. </returns>
        public unsafe bool ConvertBlockTypes( [NotNull] byte[] mapping ) {
            if( mapping == null ) throw new ArgumentNullException( "mapping" );
            if( mapping.Length != 256 ) throw new ArgumentException( "Mapping must list all 256 blocks", "mapping" );

            bool mapped = false;
            fixed( byte* ptr = Blocks ) {
                for( int j = 0; j < Blocks.Length; j++ ) {
                    if( ptr[j] > 255) {
                        ptr[j] = mapping[ptr[j]];
                        mapped = true;
                    }
                }
            }
            if( mapped ) HasChangedSinceSave = true;
            return mapped;
        }

        static readonly byte[] ZeroMapping = new byte[256];

        /// <summary> Replaces all nonstandard (50-255) blocks with air. </summary>
        /// <returns> True if any blocks needed replacement. </returns>
        /*public bool RemoveUnknownBlocktypes() {
            return ConvertBlockTypes( ZeroMapping );
        }*/


        static readonly Dictionary<string, Block> BlockNames = new Dictionary<string, Block>();

        static Map() {
            // add default names for blocks, and their numeric codes
            foreach( Block block in Enum.GetValues( typeof( Block ) ) ) {
                if( block != Block.Undefined ) {
                    BlockNames.Add( block.ToString().ToLower(), block );
                    BlockNames.Add( ((int)block).ToString(), block );
                }
            }

            foreach (CustomBlock block in CustomBlock.Blocks)
            {
                BlockNames.Add(block.Name.ToLower(), (Block) block.ID);
                BlockNames.Add(block.ID.ToString(), (Block) block.ID);
            }
            // alternative names for blocks
            BlockNames["none"] = Block.Undefined;

            BlockNames["a"] = Block.Air; // common typo
            BlockNames["nothing"] = Block.Air;
            BlockNames["empty"] = Block.Air; 
            BlockNames["delete"] = Block.Air;
            BlockNames["erase"] = Block.Air;
            BlockNames["blank"] = Block.Air;

            BlockNames["cement"] = Block.Stone;
            BlockNames["concrete"] = Block.Stone;

            BlockNames["g"] = Block.Grass;
            BlockNames["gras"] = Block.Grass; // common typo

            BlockNames["soil"] = Block.Dirt;
            BlockNames["cobble"] = Block.Cobblestone; //FINALLY
            BlockNames["stones"] = Block.Cobblestone;
            BlockNames["rocks"] = Block.Cobblestone;
            BlockNames["plank"] = Block.Wood;
            BlockNames["planks"] = Block.Wood;
            BlockNames["board"] = Block.Wood;
            BlockNames["boards"] = Block.Wood;
            BlockNames["tree"] = Block.Plant;
            BlockNames["sappling"] = Block.Plant;
            BlockNames["adminium"] = Block.Admincrete;
            BlockNames["adminite"] = Block.Admincrete;
            BlockNames["opcrete"] = Block.Admincrete;
            BlockNames["hardrock"] = Block.Admincrete;
            BlockNames["solid"] = Block.Admincrete;
            BlockNames["bedrock"] = Block.Admincrete;
            BlockNames["w"] = Block.Water;
            BlockNames["l"] = Block.Lava;
            BlockNames["gold_ore"] = Block.GoldOre;
            BlockNames["iron_ore"] = Block.IronOre;
            BlockNames["copper"] = Block.IronOre;
            BlockNames["copperore"] = Block.IronOre;
            BlockNames["copper_ore"] = Block.IronOre;
            BlockNames["ore"] = Block.IronOre;
            BlockNames["coals"] = Block.Coal;
            BlockNames["coalore"] = Block.Coal;
            BlockNames["blackore"] = Block.Coal;

            BlockNames["trunk"] = Block.Log;
            BlockNames["stump"] = Block.Log;
            BlockNames["treestump"] = Block.Log;
            BlockNames["treetrunk"] = Block.Log;

            BlockNames["leaf"] = Block.Leaves;
            BlockNames["foliage"] = Block.Leaves;

            BlockNames["cheese"] = Block.Sponge;
            BlockNames["spoiled_milk"] = Block.Sponge;

            BlockNames["redcloth"] = Block.Red;
            BlockNames["redwool"] = Block.Red;
            BlockNames["orangecloth"] = Block.Orange;
            BlockNames["orangewool"] = Block.Orange;
            BlockNames["yellowcloth"] = Block.Yellow;
            BlockNames["yellowwool"] = Block.Yellow;
            BlockNames["limecloth"] = Block.Lime;
            BlockNames["limewool"] = Block.Lime;
            BlockNames["greenyellow"] = Block.Lime;
            BlockNames["yellowgreen"] = Block.Lime;
            BlockNames["lightgreen"] = Block.Lime;
            BlockNames["lightgreencloth"] = Block.Lime;
            BlockNames["lightgreenwool"] = Block.Lime;
            BlockNames["greencloth"] = Block.Green;
            BlockNames["greenwool"] = Block.Green;
            BlockNames["springgreen"] = Block.Teal;
            BlockNames["emerald"] = Block.Teal;
            BlockNames["tealwool"] = Block.Teal;
            BlockNames["tealcloth"] = Block.Teal;
            BlockNames["aquawool"] = Block.Aqua;
            BlockNames["aquacloth"] = Block.Aqua;
            BlockNames["cyanwool"] = Block.Cyan;
            BlockNames["cyancloth"] = Block.Cyan;
            BlockNames["bluewool"] = Block.Blue;
            BlockNames["bluecloth"] = Block.Blue;
            BlockNames["indigowool"] = Block.Indigo;
            BlockNames["indigocloth"] = Block.Indigo;
            BlockNames["violetwool"] = Block.Violet;
            BlockNames["violetcloth"] = Block.Violet;
            BlockNames["lightpurple"] = Block.Violet;
            BlockNames["purple"] = Block.Violet;
            BlockNames["purplewool"] = Block.Violet;
            BlockNames["purplecloth"] = Block.Violet;
            BlockNames["fuchsia"] = Block.Magenta;
            BlockNames["magentawool"] = Block.Magenta;
            BlockNames["magentacloth"] = Block.Magenta;
            BlockNames["darkpink"] = Block.Pink;
            BlockNames["pinkwool"] = Block.Pink;
            BlockNames["pinkcloth"] = Block.Pink;
            BlockNames["cloth"] = Block.White;
            BlockNames["cotton"] = Block.White;
            BlockNames["grey"] = Block.Gray;
            BlockNames["lightgray"] = Block.Gray;
            BlockNames["lightgrey"] = Block.Gray;
            BlockNames["darkgray"] = Block.Black;
            BlockNames["darkgrey"] = Block.Black;

            BlockNames["yellow_flower"] = Block.YellowFlower;
            BlockNames["flower"] = Block.YellowFlower;
            BlockNames["rose"] = Block.RedFlower;
            BlockNames["redrose"] = Block.RedFlower;
            BlockNames["red_flower"] = Block.RedFlower;

            BlockNames["mushroom"] = Block.BrownMushroom;
            BlockNames["shroom"] = Block.BrownMushroom;
            BlockNames["brown_shroom"] = Block.BrownMushroom;
            BlockNames["red_shroom"] = Block.RedMushroom;

            BlockNames["goldblock"] = Block.Gold;
            BlockNames["goldsolid"] = Block.Gold;
            BlockNames["golden"] = Block.Gold;
            BlockNames["copper"] = Block.Gold;
            BlockNames["brass"] = Block.Gold;

            BlockNames["ironblock"] = Block.Iron;
            BlockNames["steel"] = Block.Iron;
            BlockNames["metal"] = Block.Iron;
            BlockNames["silver"] = Block.Iron;

            BlockNames["slab"] = Block.Stair;
            BlockNames["slabs"] = Block.DoubleStair;
            BlockNames["steps"] = Block.DoubleStair;
            BlockNames["stairs"] = Block.DoubleStair;
            BlockNames["doublestep"] = Block.DoubleStair;
            BlockNames["double_step"] = Block.DoubleStair;
            BlockNames["double_stair"] = Block.DoubleStair;
            BlockNames["staircasefull"] = Block.DoubleStair;
            BlockNames["step"] = Block.Stair;
            BlockNames["halfstep"] = Block.Stair;
            BlockNames["halfblock"] = Block.Stair;
            BlockNames["staircasestep"] = Block.Stair;

            BlockNames["bricks"] = Block.Brick;
            BlockNames["explosive"] = Block.TNT;
            BlockNames["dynamite"] = Block.TNT;

            BlockNames["book"] = Block.Books;
            BlockNames["shelf"] = Block.Books;
            BlockNames["shelves"] = Block.Books;
            BlockNames["bookcase"] = Block.Books;
            BlockNames["bookshelf"] = Block.Books;
            BlockNames["bookshelves"] = Block.Books;

            BlockNames["moss"] = Block.MossyRocks;
            BlockNames["mossy"] = Block.MossyRocks;
            BlockNames["stonevine"] = Block.MossyRocks;
            BlockNames["mossyrock"] = Block.MossyRocks;
            BlockNames["mossystone"] = Block.MossyRocks;
            BlockNames["mossystones"] = Block.MossyRocks;
            BlockNames["greencobblestone"] = Block.MossyRocks;
            BlockNames["mossycobblestone"] = Block.MossyRocks;
            BlockNames["mossy_cobblestone"] = Block.MossyRocks;
            BlockNames["blockthathasgreypixelsonitmostlybutsomeareactuallygreen"] = Block.MossyRocks;
            
            BlockNames["onyx"] = Block.Obsidian;

            BlockNames["cobblestoneslab"] = Block.CobbleSlab;
            BlockNames["cobblestair"] = Block.CobbleSlab;
            BlockNames["cobblestep"] = Block.CobbleSlab;
            BlockNames["cobblestonestair"] = Block.CobbleSlab;
            BlockNames["cobblestonestep"] = Block.CobbleSlab;

            BlockNames["lightpinkwool"] = Block.LightPink;

            BlockNames["darkgreenwool"] = Block.DarkGreen;
            BlockNames["forestgreen"] = Block.DarkGreen;
            BlockNames["forestgreenwool"] = Block.DarkGreen;

            BlockNames["brownwool"] = Block.Brown;

            BlockNames["deepblue"] = Block.DarkBlue;
            BlockNames["deepbluewool"] = Block.DarkBlue;
            BlockNames["darkbluewool"] = Block.DarkBlue;

            BlockNames["turqoisewool"] = Block.Turquoise;
            
            BlockNames["fancybrick"] = Block.StoneBrick;

            foreach (CustomBlock b in CustomBlock.Blocks)
            {
                BlockNames[b.Name] = (Block) b.ID;
            }
        }


        public void CalculateShadows() {
            if( Shadows != null ) return;

            Shadows = new short[Width, Length];
            for( int x = 0; x < Width; x++ ) {
                for( int y = 0; y < Length; y++ ) {
                    for( short z = (short)(Height - 1); z >= 0; z-- ) {
                        switch( GetBlock( x, y, z ) ) {
                            case Block.Air:
                            case Block.BrownMushroom:
                            case Block.Glass:
                            case Block.Leaves:
                            case Block.RedFlower:
                            case Block.RedMushroom:
                            case Block.YellowFlower:
                                continue;
                            default:
                                Shadows[x, y] = z;
                                break;
                        }
                        break;
                    }
                }
            }
        }


        /// <summary> Tries to find a blocktype by name. </summary>
        /// <param name="blockName"> Name of the block. </param>
        /// <returns> Described Block, or Block.Undefined if name could not be recognized. </returns>
        public static Block GetBlockByName( [NotNull] string blockName )
        {
            if( blockName == null ) throw new ArgumentNullException( nameof(blockName) );
            return BlockNames.TryGetValue( blockName.ToLower(), out var result ) ? result : Block.Undefined;
        }


        /// <summary> Writes a copy of the current map to a given stream, compressed with GZipStream. </summary>
        /// <param name="stream"> Stream to write the compressed data to. </param>
        /// <param name="prependBlockCount"> If true, prepends block data with signed, 32bit, big-endian block count. </param>
        public void GetCompressedCopy( [NotNull] Stream stream, bool prependBlockCount ) {
            if( stream == null ) throw new ArgumentNullException( "stream" );
            using( GZipStream compressor = new GZipStream( stream, CompressionMode.Compress ) ) {
                if( prependBlockCount ) {
                    // convert block count to big-endian
                    int convertedBlockCount = IPAddress.HostToNetworkOrder( Blocks.Length );
                    // write block count to gzip stream
                    compressor.Write( BitConverter.GetBytes( convertedBlockCount ), 0, 4 );
                }
                compressor.Write( Blocks, 0, Blocks.Length );
            }
        }

        /// <summary> Writes a copy of the current map to a given stream, compressed with GZipStream. </summary>
        /// <param name="stream"> Stream to write the compressed data to. </param>
        /// <param name="prependBlockCount"> If true, prepends block data with signed, 32bit, big-endian block count. </param>
        public void GetCompressedCopy([NotNull] Stream stream, bool prependBlockCount, Player p)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            using (GZipStream compressor = new GZipStream(stream, CompressionMode.Compress))
            {
                if (prependBlockCount)
                {
                    // convert block count to big-endian
                    int convertedBlockCount = IPAddress.HostToNetworkOrder(Blocks.Length);
                    // write block count to gzip stream
                    compressor.Write(BitConverter.GetBytes(convertedBlockCount), 0, 4);
                    byte[] rawData = (p.UsesCustomBlocks ? Blocks : GetFallbackMap());
                    compressor.Write(rawData, 0, rawData.Length);
                }
            }
        }

        /// <summary> Makes an admincrete barrier, 1 block thick, around the lower half of the map. </summary>
        public void MakeFloodBarrier() {
            for( int x = 0; x < Width; x++ ) {
                for( int y = 0; y < Length; y++ ) {
                    SetBlock( x, y, 0, Block.Admincrete );
                }
            }

            for( int x = 0; x < Width; x++ ) {
                for( int z = 0; z < Height / 2; z++ ) {
                    SetBlock( x, 0, z, Block.Admincrete );
                    SetBlock( x, Length - 1, z, Block.Admincrete );
                }
            }

            for( int y = 0; y < Length; y++ ) {
                for( int z = 0; z < Height / 2; z++ ) {
                    SetBlock( 0, y, z, Block.Admincrete );
                    SetBlock( Width - 1, y, z, Block.Admincrete );
                }
            }
        }


        public int SearchColumn( int x, int y, Block id ) {
            return SearchColumn( x, y, id, Height - 1 );
        }


        public int SearchColumn( int x, int y, Block id, int zStart ) {
            for( int z = zStart; z > 0; z-- ) {
                if( GetBlock( x, y, z ) == id ) {
                    return z;
                }
            }
            return -1; // -1 means 'not found'
        }

        #endregion
    }
}
