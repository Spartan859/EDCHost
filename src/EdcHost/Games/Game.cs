namespace EdcHost.Games;

/// <summary>
/// Game handles the game logic.
/// </summary>
public partial class Game : IGame
{
    /// <summary>
    /// Time interval between two ticks.
    /// </summary>
    private readonly TimeSpan TickInterval = TimeSpan.FromSeconds(0.05d);

    /// <summary>
    /// When will Battling stage start.
    /// </summary>
    private readonly TimeSpan StartBattlingTime = TimeSpan.FromSeconds(600);

    /// <summary>
    /// How much time a player should wait until respawn.
    /// </summary>
    private readonly TimeSpan RespawnTimeInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Current stage of the game.
    /// </summary>
    public IGame.Stage CurrentStage { get; private set; }

    /// <summary>
    /// Elapsed time of the game.
    /// </summary>
    public TimeSpan ElapsedTime { get; private set; }

    /// <summary>
    /// Current tick of game.
    /// </summary>
    public int CurrentTick { get; private set; }

    /// <summary>
    /// Winner of the game.
    /// </summary>
    /// <remarks>
    /// Winner can be null in case there is no winner.
    /// </remarks>
    public IPlayer? Winner { get; private set; }

    /// <summary>
    /// When game starts.
    /// </summary>
    private DateTime? _startTime;

    /// <summary>
    /// Last tick.
    /// </summary>
    private DateTime? _lastTickTime;

    /// <summary>
    /// The game map.
    /// </summary>
    private readonly IMap _map;

    /// <summary>
    /// All mines.
    /// </summary>
    private readonly List<IMine> _mines;

    /// <summary>
    /// The tick task.
    /// </summary>
    private readonly Task _tickTask;

    /// <summary>
    /// Constructor
    /// </summary>
    public Game()
    {
        CurrentStage = IGame.Stage.Ready;
        ElapsedTime = TimeSpan.FromSeconds(0);
        Winner = null;
        CurrentTick = 0;

        _startTime = null;
        _lastTickTime = null;

        _map = new Map();

        _players = new(2);

        //TODO: Set player's initial position and spawnpoint

        for (int i = 0; i < 2; i++)
        {
            _players[i] = new Player(i, 0f, 0f, 0f, 0f);
        }
        _playerLastAttackTime = new(2);
        for (int i = 0; i < 2; i++)
        {
            _playerLastAttackTime[i] = DateTime.Now - TimeSpan.FromSeconds(20);
        }
        _playerDeathTime = new(2);
        for (int i = 0; i < 2; i++)
        {
            _playerDeathTime[i] = null;
        }

        for (int i = 0; i < 2; i++)
        {
            _players[i].OnMove += HandlePlayerMoveEvent;
            _players[i].OnAttack += HandlePlayerAttackEvent;
            _players[i].OnPlace += HandlePlayerPlaceEvent;
            _players[i].OnDie += HandlePlayerDieEvent;
        }

        _mines = new();
        GenerateMines();

        _tickTask = new(Tick);
    }

    /// <summary>
    /// Starts the game.
    /// </summary>
    public void Start()
    {
        if (_startTime is not null)
        {
            throw new InvalidOperationException("The game is already started.");
        }

        //TODO: Start game after all players are ready

        foreach (Mine mine in _mines)
        {
            mine.GenerateOre();
        }

        for (int i = 0; i < 2; i++)
        {
            _playerLastAttackTime[i] = DateTime.Now - TimeSpan.FromSeconds(20);
        }

        for (int i = 0; i < 2; i++)
        {
            _playerDeathTime[i] = null;
        }

        CurrentStage = IGame.Stage.Running;
        Winner = null;
        CurrentTick = 0;

        DateTime initTime = DateTime.Now;
        _startTime = initTime;
        _lastTickTime = initTime;
        ElapsedTime = TimeSpan.FromSeconds(0);

        _tickTask.Start();

        AfterGameStartEvent?.Invoke(this, new AfterGameStartEventArgs(this, _startTime));
    }

    /// <summary>
    /// Stops the game.
    /// </summary>
    public void Stop()
    {
        if (_startTime is null)
        {
            Serilog.Log.Warning("The game has not started yet.");
        }
        lock (this)
        {
            Judge();

            _startTime = null;
            _lastTickTime = null;

            for (int i = 0; i < 2; i++)
            {
                _playerLastAttackTime[i] = DateTime.Now - TimeSpan.FromSeconds(20);
            }

            for (int i = 0; i < 2; i++)
            {
                _playerDeathTime[i] = null;
            }

            ElapsedTime = TimeSpan.FromSeconds(0);
            CurrentTick = 0;
        }
        _tickTask.Wait();
    }

    /// <summary>
    /// Ticks the game.
    /// </summary>
    public void Tick()
    {
        while (true)
        {
            try
            {
                lock (this)
                {
                    if (_startTime is null)
                    {
                        break;
                    }

                    DateTime currentTime = DateTime.Now;
                    ElapsedTime = currentTime - (DateTime)_startTime;

                    Update();

                    //TODO: Destroy all beds when battling

                    if (CurrentStage == IGame.Stage.Finished)
                    {
                        Stop();
                    }

                    if (currentTime - _lastTickTime >= TickInterval)
                    {

                        _lastTickTime = currentTime;
                        CurrentTick++;

                        AfterGameTickEvent?.Invoke(
                            this, new AfterGameTickEventArgs(this, CurrentTick));
                    }
                }
            }
            catch (Exception e)
            {
                Serilog.Log.Error($"An exception has been caught: {e}");
            }
        }
    }

    /// <summary>
    /// Generate mines when game start.
    /// </summary>
    private void GenerateMines()
    {
        //TODO: Generate mines according to game rule
    }

    /// <summary>
    /// Whether all players are ready.
    /// </summary>
    /// <returns>True if all reasy, false otherwise.</returns>
    private bool IsReady()
    {
        //TODO: Check whether all players are ready
        return true;
    }

    /// <summary>
    /// Update game.
    /// </summary>
    private void Update()
    {
        if (_startTime is null)
        {
            throw new InvalidOperationException("The game is not running.");
        }

        UpdatePlayerInfo();
        UpdateMines();
        UpdateGameStage();
    }

    /// <summary>
    /// Update game stage.
    /// </summary>
    private void UpdateGameStage()
    {
        if (IsFinished())
        {
            CurrentStage = IGame.Stage.Finished;
        }
        else if (ElapsedTime >= StartBattlingTime)
        {
            CurrentStage = IGame.Stage.Battling;
        }
        else
        {
            CurrentStage = IGame.Stage.Running;
        }
    }

    /// <summary>
    /// Update player infomation.
    /// </summary>
    /// <remarks>
    /// Similar things are done by player event handlers.
    /// But this function is still called in case there are
    /// something player event handlers can't do.
    /// </remarks>
    private void UpdatePlayerInfo()
    {
        for (int i = 0; i < 2; i++)
        {
            //TODO: Break a player's bed if the chunk is void

            if (_players[i].IsAlive == true && IsValidPosition(
                ToIntPosition(_players[i].PlayerPosition)) == false)
            {
                _players[i].Hurt(InstantDeathDamage);
            }
            else if (_players[i].IsAlive == true && _map.GetChunkAt(
                ToIntPosition(_players[i].PlayerPosition)).IsVoid == true)
            {
                _players[i].Hurt(InstantDeathDamage);
            }
        }
    }

    /// <summary>
    /// Update mines.
    /// </summary>
    private void UpdateMines()
    {
        DateTime currentTime = DateTime.Now;
        foreach (Mine mine in _mines)
        {
            if (currentTime - mine.LastOreGeneratedTime >= mine.AccumulateOreInterval)
            {
                mine.GenerateOre();
            }
        }

        //TODO: Update AccumulatedOreCount when a player collects ore
    }

    /// <summary>
    /// Whether the game is finished or not.
    /// </summary>
    /// <returns>True if finished, false otherwise.</returns>
    private bool IsFinished()
    {
        for (int i = 0; i < 2; i++)
        {
            if (_players[i].IsAlive == false && _players[i].HasBed == false)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Judge the game. Choose a winner or report there is no winner.
    /// </summary>
    private void Judge()
    {
        int remainingPlayers = 0;
        for (int i = 0; i < 2; i++)
        {
            if (_players[i].IsAlive == true || _players[i].HasBed == true)
            {
                remainingPlayers++;
            }
        }

        if (remainingPlayers == 0 || remainingPlayers == 2)
        {
            Winner = null;
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                if (_players[i].IsAlive == true || _players[i].HasBed == true)
                {
                    Winner = _players[i];
                    break;
                }
            }
        }

        AfterJudgementEvent?.Invoke(this, new AfterJudgementEventArgs(this, Winner));
    }
}
