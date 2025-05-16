using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using GodotPathfindingShader;

public partial class Map : Node2D
{
    private const int CellSize = 5;
    private const int Width = 800 / CellSize - 1;
    private const int Height = 600 / CellSize - 1;
    private const int SpawnObjectsCount = 1;

    private bool[,] _maze;
    private List<PathFindItem> _pairs;
    private HashSet<Vector2I> _sourcePoints;
    private HashSet<Vector2I> _destinationPoints;
    private Vector2[][] _polyLines;
    private List<Vector2I> _spawnPoints;
    private CpuPathFinder _cpuPathFinder;
    private GlslPathFinder _gpuPathFinder;
    private bool _useGpu = true;
    private TimeSpan _averageTime;
    private SystemFont _font;

    public override void _Ready()
    {
        Position = new Vector2(CellSize / 2, CellSize / 2);

        _cpuPathFinder = new CpuPathFinder(2000);
        _gpuPathFinder = new GlslPathFinder(maxIterations: 2000);

        CreateMap();

        _font = new SystemFont()
        {
            FontNames = ["Roboto"],
            Antialiasing = TextServer.FontAntialiasing.Lcd
        };


    }

    void SpawnObjects()
    {
        _pairs = new List<PathFindItem>();

        _sourcePoints = new HashSet<Vector2I>();
        _destinationPoints = new HashSet<Vector2I>();

        var spawnPoints = _spawnPoints.OrderBy(_ => Random.Shared.Next()).Take(SpawnObjectsCount * 2).ToArray();

        for (var i = 0; i < SpawnObjectsCount; i++)
        {
            var sourcePoint = spawnPoints[i * 2];
            var destinationPoint = spawnPoints[i * 2 + 1];
            _pairs.Add(new PathFindItem(sourcePoint, destinationPoint));

            _sourcePoints.Add(sourcePoint);
            _destinationPoints.Add(destinationPoint);
        }

        FindPath();
    }

    void FindPath()
    {
        IPathFinder finder = _useGpu ? _gpuPathFinder : _cpuPathFinder;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {

            finder.FindPaths(_pairs);
        }
        sw.Stop();

        _averageTime = sw.Elapsed / 10;


        _polyLines = _pairs.Where(q => q.Path != null).Select(q => q.Path.Select(w => new Vector2(w.X * CellSize + CellSize * 0.5f, w.Y * CellSize + CellSize * 0.5f)).ToArray())
            .ToArray();

        QueueRedraw();
    }

    void CreateMap()
    {
        _maze = CreateMaze(Width, Height);
        _spawnPoints = new List<Vector2I>();
        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                if (_maze[i, j] == false)
                {
                    _spawnPoints.Add(new Vector2I(i, j));
                }
            }
        }

        var mapData = new MapData()
        {
            Cells = _maze,
            Width = Width,
            Height = Height
        };

        _cpuPathFinder.SetMap(mapData);
        _gpuPathFinder.SetMap(mapData);

        SpawnObjects();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey inputEventKey && inputEventKey.Pressed)
        {
            if (inputEventKey.Keycode == Key.Z)
            {
                SpawnObjects();
            }
            else if (inputEventKey.Keycode == Key.X)
            {
                CreateMap();

            }
            else if (inputEventKey.Keycode == Key.G)
            {
                _useGpu = !_useGpu;
                FindPath();
            }
        }
    }

    public override void _Draw()
    {

        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                var rect = new Rect2(i * CellSize, j * CellSize, CellSize, CellSize);
                if (_maze[i, j])
                {
                    DrawRect(rect, Colors.Orange);
                }
                else
                {
                    var point = new Vector2I(i, j);
                    if (_sourcePoints.Contains(point))
                    {
                        DrawRect(rect, Colors.Blue);
                    }
                    else if (_destinationPoints.Contains(point))
                    {
                        DrawRect(rect, Colors.Green);
                    }
                }
            }
        }

        foreach (var line in _polyLines)
        {
            DrawPolyline(line, Colors.Wheat, 2f);
        }

        DrawString(_font, new Vector2(0, 12),
            $"Time for calculation paths: {_averageTime:g} (Mode: {(_useGpu ? "GPU" : "CPU")})", fontSize: 12,
            modulate: Colors.White);

    }

    public override void _PhysicsProcess(double delta)
    {
    }

    private static bool[,] CreateMaze(int width, int height)
    {
        if (width % 2 == 0 || height % 2 == 0)
        {
            throw new Exception("Width and Height must be odd");
        }
        bool[,] maze = new bool[width, height];
        // Initialize all cells as walls
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                maze[x, y] = true;

        if (width == 0 || height == 0)
            return maze;

        Random rand = new Random();

        // Choose a starting point, preferably at an odd index to allow proper maze generation
        int startX = rand.Next(width);
        int startY = rand.Next(height);

        // Adjust to odd indices if possible
        if (width > 1 && startX % 2 == 0)
            startX = (startX + 1) % width;
        if (height > 1 && startY % 2 == 0)
            startY = (startY + 1) % height;

        Stack<Point> stack = new Stack<Point>();
        stack.Push(new Point(startX, startY));
        maze[startX, startY] = false;

        // Directions: up, down, left, right (each moves 2 steps)
        int[] dx = { 0, 0, -2, 2 };
        int[] dy = { -2, 2, 0, 0 };

        while (stack.Count > 0)
        {
            Point current = stack.Pop();
            List<int> possibleDirections = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                int nx = current.X + dx[i];
                int ny = current.Y + dy[i];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height && maze[nx, ny])
                {
                    possibleDirections.Add(i);
                }
            }

            if (possibleDirections.Count > 0)
            {
                stack.Push(current);

                int dir = possibleDirections[rand.Next(possibleDirections.Count)];
                int nextX = current.X + dx[dir];
                int nextY = current.Y + dy[dir];

                // Carve the wall between current and next cell
                int wallX = current.X + dx[dir] / 2;
                int wallY = current.Y + dy[dir] / 2;
                maze[wallX, wallY] = false;
                maze[nextX, nextY] = false;

                stack.Push(new Point(nextX, nextY));
            }
        }

        return maze;
    }
}
