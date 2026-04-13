using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Handles file I/O for spatial specifications.
/// Manages persistence to/from disk with automatic directory creation.
/// </summary>
public sealed class SpatialSpecPersistence
{
    private readonly string _specsDirectory;

    /// <summary>
    /// Initializes the persistence handler with a directory for storing specs.
    /// Directory is created if it doesn't exist.
    /// </summary>
    /// <param name="specsDirectory">Directory path to store spec files. Defaults to ~/.oravey2/town-specs/</param>
    public SpatialSpecPersistence(string? specsDirectory = null)
    {
        _specsDirectory = specsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".oravey2",
            "town-specs"
        );

        try
        {
            if (!Directory.Exists(_specsDirectory))
            {
                Directory.CreateDirectory(_specsDirectory);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create or access specs directory '{_specsDirectory}': {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Saves a spatial specification to a JSON file.
    /// </summary>
    /// <param name="fileName">Name of the file (without directory path)</param>
    /// <param name="spec">The spatial specification to save</param>
    /// <returns>Task representing the asynchronous save operation</returns>
    /// <exception cref="ArgumentNullException">Thrown if fileName or spec is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if file I/O fails</exception>
    public async Task SaveToFileAsync(string fileName, TownSpatialSpecification spec)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName), "File name cannot be null or empty");

        if (spec == null)
            throw new ArgumentNullException(nameof(spec), "Spatial specification cannot be null");

        try
        {
            var filePath = Path.Combine(_specsDirectory, fileName);
            var json = SpatialSpecSerializer.SerializeToJson(spec);

            await File.WriteAllTextAsync(filePath, json);
        }
        catch (ArgumentNullException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to save spatial specification to '{fileName}': {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Loads a spatial specification from a JSON file.
    /// </summary>
    /// <param name="fileName">Name of the file to load (without directory path)</param>
    /// <returns>Task that completes with the loaded TownSpatialSpecification</returns>
    /// <exception cref="ArgumentNullException">Thrown if fileName is null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist</exception>
    /// <exception cref="InvalidOperationException">Thrown if file I/O or deserialization fails</exception>
    public async Task<TownSpatialSpecification> LoadFromFileAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentNullException(nameof(fileName), "File name cannot be null or empty");

        try
        {
            var filePath = Path.Combine(_specsDirectory, fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Spatial specification file not found: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            return SpatialSpecSerializer.DeserializeFromJson(json);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (ArgumentNullException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load spatial specification from '{fileName}': {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Gets the full directory path where specs are stored.
    /// </summary>
    public string SpecsDirectory => _specsDirectory;
}
