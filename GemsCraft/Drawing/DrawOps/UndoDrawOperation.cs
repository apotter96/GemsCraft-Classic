﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>

using System;
using GemsCraft.Commands;
using GemsCraft.Players;
using GemsCraft.Utils;
using GemsCraft.Worlds;

namespace GemsCraft.Drawing.DrawOps {
    public sealed class UndoDrawOperation : DrawOpWithBrush {
        const BlockChangeContext UndoContext = BlockChangeContext.Drawn | BlockChangeContext.UndoneSelf;

        public UndoState State { get; private set; }

        public bool Redo { get; private set; }

        public override int ExpectedMarks => 0;

        public override string Description => Name;

        public override string Name => Redo ? "Redo" : "Undo";


        public UndoDrawOperation( Player player, UndoState state, bool redo )
            : base( player ) {
            State = state;
            Redo = redo;
        }


        public override bool Prepare( Vector3I[] marks ) {
            Brush = this;
            if( !base.Prepare( marks ) ) return false;
            BlocksTotalEstimate = State.Buffer.Count;
            Context = UndoContext;
            Bounds = State.GetBounds();
            return true;
        }

        public override bool Begin() {
            if( !RaiseBeginningEvent( this ) ) return false;
            if( Redo ) {
                UndoState = Player.RedoBegin( this );
            } else {
                UndoState = Player.UndoBegin( this );
            }
            StartTime = DateTime.UtcNow;
            HasBegun = true;
            Map.QueueDrawOp( this );
            RaiseBeganEvent( this );
            return true;
        }

        int undoBufferIndex;
        Block block;

        public override int DrawBatch( int maxBlocksToDraw ) {
            int blocksDone = 0;
            for( ; undoBufferIndex < State.Buffer.Count; undoBufferIndex++ ) {
                UndoBlock blockUpdate = State.Get( undoBufferIndex );
                Coords = new Vector3I( blockUpdate.X, blockUpdate.Y, blockUpdate.Z );
                block = blockUpdate.Block;
                if( DrawOneBlock() ) {
                    blocksDone++;
                    if( blocksDone >= maxBlocksToDraw || TimeToEndBatch ) {
                        undoBufferIndex++;
                        return blocksDone;
                    }
                }
            }
            IsDone = true;
            return blocksDone;
        }


        protected override Block NextBlock() {
            return block;
        }

        public override bool ReadParams( Command cmd ) {
            return true;
        }
    }
}