using BenchmarkDotNet.Attributes;
using FallingSand.Game.World;
using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;

namespace FallingSand.Benchmarks
{
    public class SwapPositionsBench
    {
        private SandWorld world;
        private WorldChunk chunk;
        private WorldChunk chunkTarget;
        private int x1, y1, x2, y2, actorID, targetID;
        private int x3, y3, x4, y4, actorID2, targetID2;
        [GlobalSetup]
        public void Setup()
        {
            ElementManager.Initialize();
            ElementManager.LoadElements("Content/materials");
            ElementManager.FinishInitialize();

            // small world to exercise SwapPositions with minimal overhead
            int chunkSize = 16; // power of two
            int chunksPerRegion = 1;
            world = new SandWorld(chunkSize, chunksPerRegion, headless: true);
            // Get a chunk near origin
            chunk = world.GetChunk(0, 0);
            chunkTarget = world.GetChunk(1, 0);
            // pick coordinates well within bounds
            x1 = chunk.chunkX + 4;
            y1 = chunk.chunkY + 4;
            x2 = chunk.chunkX + 5;
            y2 = chunk.chunkY + 4;

            x3 = chunk.chunkX + 15;
            y3 = chunk.chunkY;
            x4 = chunkTarget.chunkX;
            y4 = chunkTarget.chunkY;
            // Get the cell indices for these coordinates
            actorID = chunk.GetCellIndex(x1, y1);
            targetID = chunk.GetCellIndex(x2, y2);

            actorID2 = chunk.GetCellIndex(x3, y3);
            targetID2 = chunkTarget.GetCellIndex(x4, y4);
            // Ensure some non-empty elements to trigger work paths
            chunk.element[actorID] = 1; // assume 1 is a valid element
            chunk.element[targetID] = 2;
            chunk.element[actorID2] = 1; // assume 1 is a valid element
            chunkTarget.element[targetID2] = 2;
        }

        [Benchmark]
        public void SwapPositions_SameChunk()
        {
            CellBehaviour.SwapPositions(chunk, x1, y1, ref actorID, x2, y2, targetID);
        }
        [Benchmark]
        public void SwapPositions_DifferentChunk()
        {
            CellBehaviour.SwapPositions(ref chunk, chunkTarget, x3, y3, ref actorID2, x4, y4, ref targetID2);
        }
    }
}