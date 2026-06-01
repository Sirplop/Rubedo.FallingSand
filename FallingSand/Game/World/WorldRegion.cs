#define USE_MULTITHREADING

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Graphics;
using System.Threading.Tasks;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldRegion, and I don't have a summary yet.
/// </summary>
public class WorldRegion
{
    public bool active = true;

    private Color[] textureData;
    private readonly Texture2D texture;
    private int regionSize;
    private int chunkSize;
    private int chunksPerRegion;
    private int regionX;
    private int regionY;
    private readonly int sizeShift;

    public WorldChunk[] chunks;

    public WorldRegion(SandWorld world, int chunkSize, int chunksPerRegion, int x, int y)
    {
        this.chunkSize = chunkSize;
        this.chunksPerRegion = chunksPerRegion;
        this.regionSize = chunkSize * chunksPerRegion;
        this.regionX = x * regionSize;
        this.regionY = y * regionSize;

        sizeShift = Rubedo.Lib.Math.GetPower2Exponent(this.chunkSize);

        textureData = new Color[regionSize * regionSize];
        texture = new Texture2D(RubedoEngine.Graphics.GraphicsDevice, regionSize, regionSize);
        chunks = new WorldChunk[chunksPerRegion * chunksPerRegion];
        for (int my = 0; my < chunksPerRegion; my++)
        {
            int dY = (y * chunksPerRegion) + my; //y >= 0 ? (y * chunksPerRegion) + my : (chunksPerRegion - my - 1) + (y * chunksPerRegion);
            for (int mx = 0; mx < chunksPerRegion; mx++)
            {
                int dX = (x * chunksPerRegion) + mx; //x >= 0 ? (x * chunksPerRegion) + mx : (chunksPerRegion - mx - 1) + (x * chunksPerRegion);
                chunks[mx + (my * chunksPerRegion)] = new WorldChunk(world, dX, dY, chunkSize);
            }
        }
    }

    public WorldChunk GetChunk(int x, int y)
    {
        int regionMask = regionSize - 1; // must be power of 2

        int localX = x & regionMask;
        int localY = y & regionMask;

        int ax = localX >> sizeShift;
        int ay = localY >> sizeShift;

        int index = ay * chunksPerRegion + ax;
        return chunks[index];
    }

    public void MultithreadSetup(SandWorld world)
    {
#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            chunks[i].MultithreadSetup(world);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
            chunks[i].MultithreadSetup(world);
#endif
    }
    public void MultithreadFinish(SandWorld world)
    {
#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            chunks[i].MultithreadFinish(world);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
            chunks[i].MultithreadFinish(world);
#endif
    }

    public int GetDrawIndex(int x, int y)
    {
        return (x - regionX) + ((y - regionY) * chunksPerRegion * chunkSize);
    }

    public void Draw(Renderer renderer, Camera camera, float layer)
    {
        bool updated = false;
#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            updated |= chunks[i].Draw(this, camera, ref textureData);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
        {
            updated |= chunks[i].Draw(this, camera, ref textureData);
        }
#endif
        if (updated)
            texture.SetData(textureData);

        renderer.Draw(
            texture,
            new Vector2(regionX, regionY),
            null,
            Color.White,
            0,
            Vector2.UnitY,
            Vector2.One,
            SpriteEffects.FlipVertically, layer);
    }
}