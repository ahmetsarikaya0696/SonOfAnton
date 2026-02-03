using System.Diagnostics;
using System.Reflection;

namespace SonOfAnton.Backend.Infrastructure;

/// <summary>
/// Docker container'lari otomatik baslatmak icin helper
/// </summary>
public static class DockerHelper
{
    /// <summary>
    /// Docker Compose ile servisleri baslatir ve PostgreSQL hazir olana kadar bekler
    /// </summary>
    public static async Task EnsureDockerServicesRunningAsync(ILogger logger)
    {
        // Docker'in calisip calismadigini kontrol et
        if (!await IsDockerRunningAsync())
        {
            logger.LogWarning("Docker calismiyor. Lutfen Docker Desktop'i baslatin.");
            throw new InvalidOperationException("Docker calismiyor. Lutfen Docker Desktop'i baslatin ve uygulamayi yeniden calistirin.");
        }

        logger.LogInformation("Docker calisiyor.");

        // Container'larin durumunu kontrol et
        var isPostgresRunning = await IsContainerRunningAsync("sonofanton-postgres");
        
        if (isPostgresRunning)
        {
            logger.LogInformation("PostgreSQL container zaten calisiyor.");
            
            // Yine de PostgreSQL'in baglanti kabul ettiginden emin ol
            await WaitForPostgresReadyAsync(logger, TimeSpan.FromSeconds(30));
            return;
        }

        logger.LogInformation("Docker servisleri baslatiliyor...");
        
        // docker-compose.yml dosyasinin yolunu bul
        var composePath = FindDockerComposeFile(logger);
        if (composePath == null)
        {
            throw new FileNotFoundException("docker-compose.yml dosyasi bulunamadi. Lutfen solution root'ta docker-compose.yml dosyasinin oldugunden emin olun.");
        }

        logger.LogInformation("docker-compose.yml bulundu: {Path}", composePath);

        // Docker Compose up calistir
        var result = await RunCommandAsync("docker", $"compose -f \"{composePath}\" up -d postgres", TimeSpan.FromMinutes(3));
        
        if (result.ExitCode == 0)
        {
            logger.LogInformation("Docker servisleri baslatildi.");
            
            // PostgreSQL'in hazir olmasini bekle
            await WaitForPostgresReadyAsync(logger, TimeSpan.FromSeconds(60));
        }
        else
        {
            logger.LogError("Docker servisleri baslatilamadi: {Error}", result.Error);
            throw new InvalidOperationException($"Docker servisleri baslatilamadi: {result.Error}");
        }
    }

    private static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var result = await RunCommandAsync("docker", "info", TimeSpan.FromSeconds(10));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsContainerRunningAsync(string containerName)
    {
        try
        {
            var result = await RunCommandAsync("docker", $"inspect -f \"{{{{.State.Running}}}}\" {containerName}", TimeSpan.FromSeconds(5));
            return result.ExitCode == 0 && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindDockerComposeFile(ILogger logger)
    {
        var searchedPaths = new List<string>();
        
        // 1. AppContext.BaseDirectory'den yukari dogru ara
        var currentDir = AppContext.BaseDirectory;
        logger.LogDebug("Baslangic dizini: {Dir}", currentDir);
        
        for (int i = 0; i < 10; i++)
        {
            var composePath = Path.Combine(currentDir, "docker-compose.yml");
            searchedPaths.Add(composePath);
            
            if (File.Exists(composePath))
            {
                return composePath;
            }
            
            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null) break;
            currentDir = parentDir.FullName;
        }
        
        // 2. Assembly Location'dan yukari dogru ara
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            currentDir = Path.GetDirectoryName(assemblyLocation);
            if (currentDir != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    var composePath = Path.Combine(currentDir, "docker-compose.yml");
                    if (!searchedPaths.Contains(composePath))
                    {
                        searchedPaths.Add(composePath);
                        
                        if (File.Exists(composePath))
                        {
                            return composePath;
                        }
                    }
                    
                    var parentDir = Directory.GetParent(currentDir);
                    if (parentDir == null) break;
                    currentDir = parentDir.FullName;
                }
            }
        }
        
        // 3. Current Directory'den yukari dogru ara
        currentDir = Environment.CurrentDirectory;
        for (int i = 0; i < 10; i++)
        {
            var composePath = Path.Combine(currentDir, "docker-compose.yml");
            if (!searchedPaths.Contains(composePath))
            {
                searchedPaths.Add(composePath);
                
                if (File.Exists(composePath))
                {
                    return composePath;
                }
            }
            
            var parentDir = Directory.GetParent(currentDir);
            if (parentDir == null) break;
            currentDir = parentDir.FullName;
        }
        
        // Bulunamadi - aranan yollari logla
        logger.LogWarning("docker-compose.yml bulunamadi. Aranan konumlar:");
        foreach (var path in searchedPaths.Distinct())
        {
            logger.LogWarning("  - {Path}", path);
        }
        
        return null;
    }

    private static async Task WaitForPostgresReadyAsync(ILogger logger, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("PostgreSQL'in hazir olmas� bekleniyor (max {Seconds} saniye)...", timeout.TotalSeconds);
        
        while (stopwatch.Elapsed < timeout)
        {
            var result = await RunCommandAsync("docker", "exec sonofanton-postgres pg_isready -U sonofanton -d sonofanton_db", TimeSpan.FromSeconds(5));
            
            if (result.ExitCode == 0)
            {
                logger.LogInformation("PostgreSQL hazir! (Bekleme suresi: {Elapsed:F1} saniye)", stopwatch.Elapsed.TotalSeconds);
                return;
            }
            
            await Task.Delay(1000);
        }
        
        throw new TimeoutException($"PostgreSQL {timeout.TotalSeconds} saniye icinde hazir olmadi. Lutfen Docker loglarini kontrol edin.");
    }

    private static async Task<CommandResult> RunCommandAsync(string command, string arguments, TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CommandResult(-1, "", "Process timeout");
        }

        return new CommandResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private record CommandResult(int ExitCode, string Output, string Error);
}

