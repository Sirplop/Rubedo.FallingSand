using BenchmarkDotNet.Attributes;
using FallingSand.Game.World;
using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.VSDiagnostics;

namespace FallingSand.Benchmarks
{
    [CPUUsageDiagnoser]
    public class SwapPositionsBench
    {
        private SandWorld world;
        private WorldChunk chunk;
        private int x1, y1, x2, y2, actorID, targetID;
        [GlobalSetup]
        public void Setup()
        {
            // small world to exercise SwapPositions with minimal overhead
            int chunkSize = 16; // power of two
            int chunksPerRegion = 1;
            world = new SandWorld(chunkSize, chunksPerRegion, headless: true);
            // Get a chunk near origin
            chunk = world.GetChunk(0, 0);
            // pick coordinates well within bounds
            x1 = chunk.chunkX + 4;
            y1 = chunk.chunkY + 4;
            x2 = chunk.chunkX + 5;
            y2 = chunk.chunkY + 4;
            // Get the cell indices for these coordinates
            actorID = chunk.GetCell(x1, y1);
            targetID = chunk.GetCell(x2, y2);
            // Ensure some non-empty elements to trigger work paths
            chunk.element[actorID] = 1; // assume 1 is a valid element
            chunk.element[targetID] = 2;
        }

        [Benchmark]
        public void SwapPositions_SameChunk()
        {
            CellBehaviour.SwapPositions(chunk, x1, y1, actorID, x2, y2, targetID);
        }
    }
}