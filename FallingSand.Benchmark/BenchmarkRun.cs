using System;
using System.Diagnostics;
using System.Threading;
using BenchmarkDotNet.Running;
using FallingSand.Benchmarks;
using Rubedo.Resources;

namespace Rubedo.Benchmark;

/// <summary>
/// I am BenchmarkRunner, and this is my summary.
/// </summary>
internal class BenchmarkRun
{
    /// <summary>
    /// Initializes Rubedo subsystems required by benchmarks (graphics device, renderer, resources).
    /// Starts the game on a background thread and waits until the GraphicsDevice/Renderer are available,
    /// then stops the game and proceeds to run BenchmarkDotNet.
    /// </summary>
    public static void Prepare()
    {
        const int INIT_TIMEOUT_MS = 10_000;
        const int JOIN_TIMEOUT_MS = 2_000;

        try
        {
            // Create the engine instance (constructor sets RubedoEngine.Instance)
            var engine = new Rubedo.RubedoEngine();

            // Start the game loop on a background thread so MonoGame can create its GraphicsDevice.
            var gameThread = new Thread(() =>
            {
                try
                {
                    engine.Run();
                }
                catch (Exception ex)
                {
                    // Ensure any exceptions while starting the game are visible.
                    Console.Error.WriteLine($"Rubedo engine thread exception: {ex}");
                }
            })
            {
                IsBackground = true,
                Name = "Rubedo.EngineThread"
            };

            gameThread.Start();

            // Wait until graphics device + renderer are available, or timeout.
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < INIT_TIMEOUT_MS)
            {
                if (Rubedo.RubedoEngine.Instance != null)
                {
                    var inst = Rubedo.RubedoEngine.Instance;
                    var gd = inst.GraphicsDevice;
                    var rendererField = inst.GetType().GetProperty("Renderer");
                    var renderer = rendererField?.GetValue(inst);
                    if (gd != null && renderer != null)
                        break;
                }

                Thread.Sleep(50);
            }

            // Check readiness
            bool ready = false;
            if (Rubedo.RubedoEngine.Instance != null)
            {
                var inst = Rubedo.RubedoEngine.Instance;
                ready = inst.GraphicsDevice != null && inst.Renderer != null;
            }

            if (!ready)
            {
                Console.Error.WriteLine("Warning: Rubedo engine did not finish initialization within timeout. Benchmarks will still run, but graphics-dependent benchmarks may fail.");
            }
            else
            {
                Console.WriteLine("Rubedo engine initialized: GraphicsDevice and Renderer are ready.");
            }

            // Stop the game loop now that required subsystems are initialized.
            try
            {
                Rubedo.RubedoEngine.Instance?.Exit();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: exception while exiting Rubedo engine: {ex}");
            }

            // Give the game thread a short time to end gracefully.
            if (!gameThread.Join(JOIN_TIMEOUT_MS))
            {
                Console.Error.WriteLine("Warning: Rubedo engine thread did not stop within timeout.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error while initializing Rubedo engine for benchmarks: {ex}");
        }

        Assets.RootDirectory = "Content";
    }

    public static void Run()
    {
        BenchmarkRunner.Run<SwapPositionsBench>();
    }
}