#define USE_MULTITHREADING

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Graphics;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldRegion, and I don't have a summary yet.
/// </summary>
public class WorldRegion
{
    public bool active = true;

    private Color[] textureData;
    private readonly Texture2D texture;
    private readonly int regionSize;
    private readonly int chunkSize;
    private readonly int chunksPerRegion;
    private readonly int regionX;
    private readonly int regionY;
    private readonly int sizeShift;
    private readonly int regionMask;

    public int RegionX => regionX;
    public int RegionY => regionY;

    public WorldChunk[] chunks;

    public WorldRegion(SandWorld world, int chunkSize, int chunksPerRegion, int x, int y)
    {
        this.chunkSize = chunkSize;
        this.chunksPerRegion = chunksPerRegion;
        this.regionSize = chunkSize * chunksPerRegion;
        this.regionX = x * regionSize;
        this.regionY = y * regionSize;

        sizeShift = Rubedo.Lib.Math.GetPower2Exponent(this.chunkSize);
        regionMask = regionSize - 1;

        textureData = new Color[regionSize * regionSize];
        if (!world.headless)
        {
            texture = new Texture2D(RubedoEngine.Graphics.GraphicsDevice, regionSize, regionSize);
        }
        chunks = new WorldChunk[chunksPerRegion * chunksPerRegion];

        for (int my = 0; my < chunksPerRegion; my++)
        {
            int dY = (y * chunksPerRegion) + my;
            for (int mx = 0; mx < chunksPerRegion; mx++)
            {
                int dX = (x * chunksPerRegion) + mx;
                chunks[mx + (my * chunksPerRegion)] = new WorldChunk(world, this, dX, dY, chunkSize);
            }
        }
    }

#region Querying
    public WorldChunk GetChunk(int x, int y)
    {
        int localX = x & regionMask;
        int localY = y & regionMask;

        int ax = localX >> sizeShift;
        int ay = localY >> sizeShift;

        int index = ay * chunksPerRegion + ax;
        return chunks[index];
    }
    #endregion

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
#if USE_MULTITHREADING
        int len = chunks.Length;
        bool[] updated = new bool[len];
        Parallel.For(0, len, (i) =>
        {
            updated[i] = chunks[i].Draw(this, camera, ref textureData);
        });
        for (int i = 0; i < len; i++)
        {
            if (updated[i])
            {
                texture.SetData(textureData);
                break;
            }
        }
#else
        bool updated = false;
        for (int i = 0; i < chunks.Length; i++)
        {
            updated |= chunks[i].Draw(this, camera, ref textureData);
        }
        if (updated)
            texture.SetData(textureData);
#endif

        renderer.Draw(
            texture,
            new Vector2(regionX, regionY),
            null,
            Color.White,
            0,
            Vector2.Zero,
            Vector2.One,
            SpriteEffects.FlipVertically, layer);
    }
}