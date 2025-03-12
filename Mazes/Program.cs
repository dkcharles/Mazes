using System;
using System.IO;
using System.Collections.Generic;

class Program
{
    // Random instance with a seed based on system clock by default
    static Random random = new Random();
    
    // Constants for cell types
    const int WALL = 1;
    const int PATH = 0;
    const int MIN_ROOM_SIZE = 20; // Minimum size for a room to be kept
    const int START_POINT = 2;    // New constant for start point
    const int END_POINT = 3;      // New constant for end point
    
    static void Main()
    {
        Console.WriteLine("Cave Map Generator");
        Console.WriteLine("------------------");
        
        // Set default parameters
        int width = 128;
        int height = 128;
        float fillProbability = 0.45f;
        int smoothIterations = 5;
        
        bool useCustomSeed = false;
        int seed;
        
        if (useCustomSeed)
        {
            Console.Write("Enter seed value (integer): ");
            if (int.TryParse(Console.ReadLine(), out seed))
            {
                // Initialize random with the specified seed
                random = new Random(seed);
                Console.WriteLine($"Using seed: {seed}");
            }
            else
            {
                seed = Environment.TickCount;
                random = new Random(seed);
                Console.WriteLine($"Invalid seed, using system-generated seed: {seed}");
            }
        }
        else
        {
            // Get current seed for display/saving purposes
            seed = Environment.TickCount;
            random = new Random(seed);
            Console.WriteLine($"Using system-generated seed: {seed}");
        }
        
        // Generate and process the map
        int[,] map = GenerateCaveMap(width, height, fillProbability, smoothIterations);
        EnsureConnectivity(map);
        RemoveSmallWallClusters(map, 3); // Remove wall clusters smaller than size 3
        
        // Add start and end points
        PlaceStartAndEndPoints(map);
        
        // Save to file with seed in filename for reference
        string filename = $"cave_map_seed{seed}.txt";
        SaveMapToFile(map, filename);
        
        // Print just a preview
        PrintMapPreview(map);
    }
    
    static int[,] GenerateCaveMap(int width, int height, float fillProbability, int iterations)
    {
        // Step 1: Initialize with random walls
        int[,] map = new int[height, width];
        
        // Fill borders
        for (int x = 0; x < width; x++)
        {
            map[0, x] = WALL;
            map[height - 1, x] = WALL;
        }
        
        for (int y = 0; y < height; y++)
        {
            map[y, 0] = WALL;
            map[y, width - 1] = WALL;
        }
        
        // Randomly fill interior
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                map[y, x] = random.NextDouble() < fillProbability ? WALL : PATH;
            }
        }
        
        // Step 2: Apply cellular automata smoothing
        for (int i = 0; i < iterations; i++)
        {
            map = SmoothMap(map);
            // Progress reporting for large maps
            Console.WriteLine($"Smoothing iteration {i+1}/{iterations} complete");
        }
        
        return map;
    }
    
    static int[,] SmoothMap(int[,] map)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int[,] newMap = new int[height, width];
        
        // Copy borders
        for (int x = 0; x < width; x++)
        {
            newMap[0, x] = WALL;
            newMap[height - 1, x] = WALL;
        }
        
        for (int y = 0; y < height; y++)
        {
            newMap[y, 0] = WALL;
            newMap[y, width - 1] = WALL;
        }
        
        // Apply rule: If a cell has more wall neighbors than path neighbors, it becomes a wall
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int wallCount = CountAdjacentWalls(map, x, y);
                
                // Rule: if 5+ neighboring cells are walls, this cell becomes a wall
                if (wallCount >= 5)
                {
                    newMap[y, x] = WALL;
                }
                // Rule: if 3 or fewer neighboring cells are walls, this cell becomes a path
                else if (wallCount <= 3)
                {
                    newMap[y, x] = PATH;
                }
                else
                {
                    newMap[y, x] = map[y, x];
                }
            }
        }
        
        return newMap;
    }
    
    static int CountAdjacentWalls(int[,] map, int x, int y)
    {
        int count = 0;
        
        // Check all 8 neighbors (including diagonals)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                // Skip the center cell
                if (dx == 0 && dy == 0) continue;
                
                int nx = x + dx;
                int ny = y + dy;
                
                // Count walls (including out-of-bounds as walls)
                if (map[ny, nx] == WALL)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    static void EnsureConnectivity(int[,] map)
    {
        Console.WriteLine("Identifying disconnected rooms and ensuring connectivity...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Step 1: Find all separate rooms using flood fill
        List<(int x, int y, int size)> rooms = FindRooms(map);
        
        if (rooms.Count <= 1)
        {
            Console.WriteLine("Map is already connected or contains no rooms.");
        }
        else
        {
            // Step 2: Sort rooms by size (largest first)
            rooms.Sort((a, b) => b.size.CompareTo(a.size));
            
            Console.WriteLine($"Found {rooms.Count} separate rooms. Largest room size: {rooms[0].size}");
            
            // Step 3: Handle isolated rooms
            int filledRooms = 0;
            int connectedRooms = 0;
            
            for (int i = 1; i < rooms.Count; i++)
            {
                (int x, int y, int size) = rooms[i];
                
                if (size < MIN_ROOM_SIZE)
                {
                    // Fill in small rooms (convert back to walls)
                    FillRoom(map, x, y);
                    filledRooms++;
                }
                else
                {
                    // Connect this room to the main room
                    ConnectRooms(map, rooms[0].x, rooms[0].y, x, y);
                    connectedRooms++;
                }
            }
            
            Console.WriteLine($"Filled in {filledRooms} small rooms, connected {connectedRooms} larger rooms");
        }
        
        // Create additional random hallways to increase connectivity
        int hallways = width * height / 5000;
        
        for (int i = 0; i < hallways; i++)
        {
            int x = random.Next(1, width - 1);
            int y = random.Next(1, height - 1);
            
            // Create a small random hallway
            int direction = random.Next(2); // 0 = horizontal, 1 = vertical
            int length = random.Next(5, 20); // Longer hallways for larger maps
            
            if (direction == 0)
            {
                for (int j = 0; j < length && x + j < width - 1; j++)
                {
                    map[y, x + j] = PATH;
                }
            }
            else
            {
                for (int j = 0; j < length && y + j < height - 1; j++)
                {
                    map[y + j, x] = PATH;
                }
            }
        }
        
        Console.WriteLine($"Added {hallways} additional connectivity hallways");
    }
    
    // Find all separate rooms in the map using flood fill
    static List<(int x, int y, int size)> FindRooms(int[,] map)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        List<(int x, int y, int size)> rooms = new List<(int x, int y, int size)>();
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[y, x] == PATH && !visited[y, x])
                {
                    // Start of a new room
                    int roomSize = FloodFill(map, x, y, visited);
                    rooms.Add((x, y, roomSize));
                }
            }
        }
        
        return rooms;
    }
    
    // Flood fill algorithm to mark a room and return its size
    static int FloodFill(int[,] map, int startX, int startY, bool[,] visited)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int roomSize = 0;
        
        // Using queue for breadth-first search
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            roomSize++;
            
            // Check 4 adjacent cells (non-diagonal neighbors)
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return roomSize;
    }
    
    // Fill a room (convert it back to walls)
    static void FillRoom(int[,] map, int startX, int startY)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            map[y, x] = WALL; // Convert to wall
            
            // Check 4 adjacent cells
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }
    
    // Connect two rooms by creating a path between them
    static void ConnectRooms(int[,] map, int x1, int y1, int x2, int y2)
    {
        // Create an L-shaped corridor between the rooms
        // First horizontal part
        int xMin = Math.Min(x1, x2);
        int xMax = Math.Max(x1, x2);
        
        for (int x = xMin; x <= xMax; x++)
        {
            map[y1, x] = PATH;
        }
        
        // Then vertical part
        int yMin = Math.Min(y1, y2);
        int yMax = Math.Max(y1, y2);
        
        for (int y = yMin; y <= yMax; y++)
        {
            map[y, x2] = PATH;
        }
    }
    
    static void SaveMapToFile(int[,] map, string filename)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        Console.WriteLine($"Saving {width}x{height} map to {filename}...");
        
        using (StreamWriter writer = new StreamWriter(filename))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char symbol;
                    switch (map[y, x])
                    {
                        case WALL: symbol = '#'; break;
                        case PATH: symbol = '.'; break;
                        case START_POINT: symbol = 'S'; break;
                        case END_POINT: symbol = 'E'; break;
                        default: symbol = '?'; break;
                    }
                    writer.Write(symbol);
                }
                writer.WriteLine();
            }
        }
        
        Console.WriteLine("Map saved successfully");
    }
    
    static void PrintMapPreview(int[,] map)
    {
        // For a large map, printing the entire map to console would be impractical
        int previewSize = Math.Min(128, Math.Min(map.GetLength(0), map.GetLength(1)));
        Console.WriteLine($"Preview of top-left {previewSize}x{previewSize} section:");
        
        for (int y = 0; y < previewSize; y++)
        {
            for (int x = 0; x < previewSize; x++)
            {
                switch (map[y, x])
                {
                    case WALL: Console.Write("██"); break;
                    case PATH: Console.Write("  "); break;
                    case START_POINT: Console.Write("SS"); break;
                    case END_POINT: Console.Write("EE"); break;
                    default: Console.Write("??"); break;
                }
            }
            Console.WriteLine();
        }
    }
    
    // New function to place start and end points
    static void PlaceStartAndEndPoints(int[,] map)
    {
        Console.WriteLine("Placing start and end points...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Find the largest room to place points in (using existing room finding code)
        bool[,] visited = new bool[height, width];
        List<(int x, int y, int size)> rooms = FindRooms(map);
        
        if (rooms.Count == 0)
        {
            Console.WriteLine("Error: No suitable rooms found for placing start/end points");
            return;
        }
        
        // Sort rooms by size (largest first)
        rooms.Sort((a, b) => b.size.CompareTo(a.size));
        
        // Get a list of accessible path cells in the largest room
        List<(int x, int y)> accessibleCells = GetAccessibleCells(map, rooms[0].x, rooms[0].y);
        
        if (accessibleCells.Count < 2)
        {
            Console.WriteLine("Error: Not enough accessible cells for start/end points");
            return;
        }
        
        // Find two points that are far apart
        (int startX, int startY, int endX, int endY) = FindDistantPoints(map, accessibleCells);
        
        // Mark the points on the map
        map[startY, startX] = START_POINT;
        map[endY, endX] = END_POINT;
        
        Console.WriteLine($"Start point placed at ({startX}, {startY})");
        Console.WriteLine($"End point placed at ({endX}, {endY})");
        
        double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        Console.WriteLine($"Straight-line distance between points: {distance:F1} cells");
    }
    
    // Get all accessible cells in a room starting from a seed point
    static List<(int x, int y)> GetAccessibleCells(int[,] map, int startX, int startY)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        List<(int x, int y)> cells = new List<(int x, int y)>();
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            cells.Add((x, y));
            
            // Check 4 adjacent cells (non-diagonal neighbors)
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1 && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return cells;
    }
    
    // Find two points that are far apart in the accessible cells
    static (int startX, int startY, int endX, int endY) FindDistantPoints(int[,] map, List<(int x, int y)> cells)
    {
        // Option 1: Simple approach - take random cells from opposite parts of the map
        int n = cells.Count;
        int quarterIndex = n / 4;
        int threeQuarterIndex = 3 * n / 4;
        
        // Get a point from first quarter
        int startIndex = random.Next(0, quarterIndex);
        (int startX, int startY) = cells[startIndex];
        
        // Get a point from fourth quarter
        int endIndex = random.Next(threeQuarterIndex, n);
        (int endX, int endY) = cells[endIndex];
        
        // Ensure minimum distance between points
        double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
        int attempts = 0;
        const int MIN_DISTANCE = 30; // Minimum distance between start and end
        
        // Try to find better points up to 20 times
        while (distance < MIN_DISTANCE && attempts < 20)
        {
            startIndex = random.Next(0, n);
            (startX, startY) = cells[startIndex];
            
            endIndex = random.Next(0, n);
            (endX, endY) = cells[endIndex];
            
            distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));
            attempts++;
        }
        
        return (startX, startY, endX, endY);
    }
    
    // New function to remove small wall clusters
    static void RemoveSmallWallClusters(int[,] map, int maxSize)
    {
        Console.WriteLine($"Identifying and removing wall clusters smaller than size {maxSize}...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        int clustersRemoved = 0;
        
        // Skip the border cells - we always want to keep those as walls
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // If this is a wall cell and hasn't been visited yet
                if (map[y, x] == WALL && !visited[y, x])
                {
                    List<(int x, int y)> cluster = new List<(int x, int y)>();
                    
                    // Find the entire connected wall cluster
                    FindConnectedWallCluster(map, x, y, visited, cluster);
                    
                    // If the cluster is smaller than the threshold, remove it
                    if (cluster.Count > 0 && cluster.Count < maxSize)
                    {
                        foreach (var (cx, cy) in cluster)
                        {
                            map[cy, cx] = PATH; // Convert wall to path
                        }
                        clustersRemoved++;
                    }
                }
            }
        }
        
        Console.WriteLine($"Removed {clustersRemoved} small wall clusters");
    }
    
    // Find all connected wall cells in a cluster
    static void FindConnectedWallCluster(int[,] map, int startX, int startY, bool[,] visited, List<(int x, int y)> cluster)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            cluster.Add((x, y));
            
            // Check all 8 neighbors (including diagonals)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    // Skip the center cell
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                        map[ny, nx] == WALL && !visited[ny, nx])
                    {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }
    }
}
