using System.Diagnostics;

namespace EdcHost.Games;

class GameRunner : IGameRunner
{
    const int TicksPerSecondExpected = 20;

    public bool IsRunning => _shouldRun;
    
    public IGame Game { get; }

    bool _shouldRun = false;
    Task? _task = null;

    public GameRunner(IGame game)
    {
        Game = game;
    }

    public void Start()
    {
        if (Game.CurrentStage is not IGame.Stage.Ready)
        {
            throw new InvalidOperationException("game is not ready");
        }

        _shouldRun = true;

        Game.Start();

        _task = Run();
    }

    public void End()
    {
        if (Game.CurrentStage is not IGame.Stage.Running && Game.CurrentStage is not IGame.Stage.Battling)
        {
            throw new InvalidOperationException("game is not running");
        }

        Debug.Assert(_task is not null);

        _shouldRun = false;
        _task.Wait();
    }

    async Task Run()
    {
        try
        {
            DateTime lastTickStartTime = DateTime.Now;

            while (_shouldRun)
            {
                if (Game.CurrentStage is not IGame.Stage.Running && Game.CurrentStage is not IGame.Stage.Battling)
                {
                    break;
                }

                // Wait for next tick
                DateTime currentTickStartTime = lastTickStartTime.AddMilliseconds((double)1000 / TicksPerSecondExpected);
                if (currentTickStartTime > DateTime.Now)
                {
                    await Task.Delay(currentTickStartTime - DateTime.Now);
                }
                else
                {
                    currentTickStartTime = DateTime.Now;
                }

                Game.Tick();

                lastTickStartTime = currentTickStartTime;
            }
        }
        catch (Exception e)
        {
            Serilog.Log.Error($"an error occurred when running the game: {e.Message}");
        }
    }
}
